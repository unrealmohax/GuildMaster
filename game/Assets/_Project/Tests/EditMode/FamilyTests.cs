using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GuildMaster.Core;
using GuildMaster.Data;
using GuildMaster.Debugging;
using NUnit.Framework;

namespace GuildMaster.Tests
{
    public sealed class FamilyGenerationTests
    {
        private PeopleData data;

        [SetUp]
        public void SetUp() => data = new PeopleData();

        [TearDown]
        public void TearDown() => data.Dispose();

        private Adventurer Person(int age, params string[] traits)
        {
            var adventurer = new Adventurer(1) { Name = "Ян", Gender = Gender.Male, Age = age };
            foreach (string trait in traits) TraitService.AddAtGeneration(adventurer, data.Registry.Get<SpecialTraitDefinition>(trait), 0, 0);
            return adventurer;
        }

        private List<Adventurer> Generate(int count, int age, params string[] traits)
        {
            var rng = new Rng(11u);
            var people = new List<Adventurer>();
            for (int i = 0; i < count; i++)
            {
                Adventurer adventurer = Person(age, traits);
                FamilyGenerator.Generate(rng, data.Registry, adventurer);
                people.Add(adventurer);
            }
            return people;
        }

        private static int Count(List<Adventurer> people, FamilyRole role) =>
            people.Sum(a => a.Family.Members.Count(m => m.Role == role));

        [Test]
        public void Composition_DependsOnAge()
        {
            const int n = 2000;
            List<Adventurer> young = Generate(n, 17);
            List<Adventurer> old = Generate(n, 35);

            Assert.AreEqual(0.9f * 2 * n, Count(young, FamilyRole.Parent), 2 * Frequency.Tolerance(2 * n, 0.9f), "родители молодого живы в 90%");
            Assert.AreEqual(0.54f * 2 * n, Count(old, FamilyRole.Parent), 2 * Frequency.Tolerance(2 * n, 0.54f), "родители 35-летнего — в 54%");
            Assert.AreEqual(0.1f * n, Count(young, FamilyRole.Spouse), Frequency.Tolerance(n, 0.1f), "супруг у молодого — 10%");
            Assert.AreEqual(0.64f * n, Count(old, FamilyRole.Spouse), Frequency.Tolerance(n, 0.64f), "супруг у 35-летнего — 64%");
            Assert.Greater(Count(old, FamilyRole.Child), Count(young, FamilyRole.Child) * 3, "у старших детей больше");
        }

        [Test]
        public void Children_OnlyWithSpouse_AtMostMax()
        {
            foreach (Adventurer adventurer in Generate(1000, 35))
            {
                int children = adventurer.Family.Members.Count(m => m.Role == FamilyRole.Child);
                if (children > 0) Assert.IsTrue(adventurer.Family.HasLivingSpouse(), "дети — только при супруге");
                Assert.LessOrEqual(children, data.Balance.Family.MaxChildren);
                FamilyMember spouse = adventurer.Family.Members.FirstOrDefault(m => m.Role == FamilyRole.Spouse);
                if (spouse != null) Assert.AreEqual(Gender.Female, spouse.Gender, "у мужчины — жена");
            }
        }

        [Test]
        public void Familial_AlwaysHasFamily_Lover_HasNoSpouse()
        {
            data.Set("family.parentAliveAtMin", 0f);
            Assert.IsTrue(Generate(300, 17, "Family").All(a => a.Family.HasLiving), "Семейный всегда с семьёй");
            Assert.IsTrue(Generate(500, 35, "Lover").All(a => !a.Family.HasLivingSpouse()), "у Влюблённого нет супруга за кадром");
        }

        [Test]
        public void Familial_DoublesSpouseChance_LonerHalves()
        {
            Adventurer plain = Person(25);
            Adventurer familial = Person(25, "Family");
            Adventurer loner = Person(25);
            loner.SetAxis(AxisId.People, -100f);
            float chance = FamilyRules.SpouseChance(plain, data.Registry);
            Assert.AreEqual(0.34f, chance, 1e-4f, "10% + 3% × 8 лет");
            Assert.AreEqual(chance * 2f, FamilyRules.SpouseChance(familial, data.Registry), 1e-4f);
            Assert.AreEqual(chance * 0.5f, FamilyRules.SpouseChance(loner, data.Registry), 1e-4f);
        }

        /// <summary>Семья бросается последней: без бросков семьи всё прочее у людей то же.</summary>
        [Test]
        public void FamilyRolls_DoNotShiftEarlierGenerationRolls()
        {
            string Run(bool family)
            {
                using var local = new PeopleData();
                if (!family)
                {
                    local.Set("family.parentAliveAtMin", 0f);
                    local.Set("family.spouseBase", 0f);
                    local.Set("family.spousePerYear", 0f);
                }
                var simulation = new Simulation(local.Registry, 9u, SimulationSystems.CreateDefault());
                var text = new StringBuilder(PeopleDump.WithoutFamily(simulation));
                var rng = new Rng(3u);
                var names = new HashSet<string>();
                for (int i = 0; i < 40; i++)
                {
                    Adventurer a = AdventurerGenerator.Generate(rng, local.Registry, simulation.World, 100 + i, atStart: false, names).Adventurer;
                    a.Family.Members.ToList().ForEach(m => a.Family.Remove(m));
                    text.Append(PeopleDump.Line(a));
                }
                return text.ToString();
            }

            // Семейный без семьи бросает ещё «какой родитель жив» — тоже последним, поэтому следующий человек сдвигается
            // у обоих вариантов одинаково, только если таких нет; сверяем старт целиком и первого кандидата.
            string with = Run(true);
            string without = Run(false);
            string[] a = with.Split('\n');
            string[] b = without.Split('\n');
            Assert.AreEqual(a.TakeWhile(l => !l.StartsWith("candidate")).ToArray(), b.TakeWhile(l => !l.StartsWith("candidate")).ToArray(),
                "стартовая шестёрка та же");
            Assert.AreEqual(a.First(l => l.StartsWith("candidate")), b.First(l => l.StartsWith("candidate")), "первый кандидат тот же");
        }
    }

    public sealed class FamilyTests
    {
        private StateWorld world;

        [SetUp]
        public void SetUp() => world = new StateWorld();

        [TearDown]
        public void TearDown() => world.Dispose();

        private static FamilyMember Add(Adventurer adventurer, FamilyRole role, Gender gender = Gender.Female)
        {
            var member = new FamilyMember(role, gender, "Ида");
            adventurer.Family.Add(member);
            return member;
        }

        /// <summary>Жена и двое детей: 10 + 6 + 6 = 22 в неделю.</summary>
        private static Adventurer WithFamily(Adventurer adventurer)
        {
            Add(adventurer, FamilyRole.Spouse);
            Add(adventurer, FamilyRole.Child, Gender.Male);
            Add(adventurer, FamilyRole.Child);
            return adventurer;
        }

        [Test]
        public void Norm_ByRoles_IllCostsMore_DeadCostsNothing()
        {
            Adventurer a = WithFamily(world.Add());
            FamilyBalance balance = world.Balance.Family;
            Assert.AreEqual(22, FamilyRules.Norm(a, balance));
            a.Family.Members[1].Health = FamilyHealth.Ill;
            Assert.AreEqual(28, FamilyRules.Norm(a, balance), "больной ребёнок × 2");
            a.Family.Members[1].Alive = false;
            Assert.AreEqual(16, FamilyRules.Norm(a, balance), "умерший из нормы выпадает");
        }

        [Test]
        public void Ordinary_PaysAboveReserve_Familial_PaysFirstWithExtra()
        {
            Adventurer ordinary = WithFamily(world.Add());
            Adventurer familial = WithFamily(world.Add("Family"));
            int reserve = FoodRules.Reserve(ordinary, world.Balance);

            ordinary.State.Wallet = reserve + 10;
            familial.State.Wallet = reserve + 10;
            world.Do(ctx =>
            {
                Assert.IsFalse(FamilyService.PayWeek(ctx, ordinary), "обычный не трогает резерв: 10 из 22");
                Assert.IsTrue(FamilyService.PayWeek(ctx, familial), "Семейный платит первым");
            });
            Assert.AreEqual(reserve, ordinary.State.Wallet);
            Assert.IsTrue(ordinary.State.FamilyInNeed);
            Assert.AreEqual(reserve + 10 - 29, familial.State.Wallet, "22 × 1,3 = 28,6 → 29");
            Assert.IsFalse(familial.State.FamilyInNeed);

            familial.State.Wallet = 5;
            world.Do(ctx => Assert.IsFalse(FamilyService.PayWeek(ctx, familial)));
            Assert.AreEqual(0, familial.State.Wallet, "Семейный отдаёт последнее");
            Assert.IsTrue(familial.State.FamilyInNeed);

            ordinary.State.Wallet = 1000;
            world.Do(ctx => Assert.IsTrue(FamilyService.PayWeek(ctx, ordinary)));
            Assert.AreEqual(978, ordinary.State.Wallet);
            Assert.IsFalse(ordinary.State.FamilyInNeed, "полная плата снимает нужду");
        }

        [Test]
        public void Need_Stress_Contentment_MoneyMotive_LeaveCause()
        {
            Adventurer a = WithFamily(world.Add());
            float target = StateRules.ContentmentTarget(a, 0f, world.Registry);
            float money = Motives.Weigh(a, world.Registry, false)[Motive.Money];

            a.State.FamilyInNeed = true;
            Assert.AreEqual(target - 10f, StateRules.ContentmentTarget(a, 0f, world.Registry), 1e-4f);
            MotiveWeights weights = Motives.Weigh(a, world.Registry, false);
            Assert.AreEqual(money * 1.5f, weights[Motive.Money], 1e-4f);
            Assert.IsTrue(weights.TryGetFactor(Motive.Money, out StateFactor factor, out _) && factor == StateFactor.FamilyNeed);
            Assert.AreEqual(LeaveCause.FamilyNeed, LeaveReasons.Of(a, world.Registry, 0, world.Simulation.Calendar)[0]);

            Adventurer familial = WithFamily(world.Add("Family"));
            familial.State.FamilyInNeed = true;
            a.State.Stress = familial.State.Stress = 20f;
            world.Do(ctx =>
            {
                FamilyService.ApplyDay(ctx, a);
                FamilyService.ApplyDay(ctx, familial);
            });
            Assert.AreEqual(21f, a.State.Stress, 1e-4f, "нужда: стресс +1 в сутки");
            Assert.AreEqual(22f, familial.State.Stress, 1e-4f, "у Семейного × 2");
        }

        [Test]
        public void NeedStarted_PublishedOnce_WithFeedLine()
        {
            Adventurer a = WithFamily(world.Add());
            a.State.Wallet = 0;
            List<SimEvent> events = world.Collect(() =>
            {
                world.Do(ctx => FamilyService.PayWeek(ctx, a));
                world.Do(ctx => FamilyService.PayWeek(ctx, a));
                world.Simulation.Tick();
            });
            Assert.AreEqual(1, events.Count(e => e.Type == SimEventType.FamilyNeedStarted));
            Assert.AreEqual(2, events.Count(e => e.Type == SimEventType.FamilyMoneySent));
            Assert.IsTrue(world.Simulation.World.Feed.Guild.Any(f => f.TemplateKey == FeedKeys.FamilyNeed), "строка «не смог отправить денег домой»");
        }

        [Test]
        public void Payday_EverySevenDays_AtMidnight()
        {
            Adventurer a = WithFamily(world.Add());
            List<SimEvent> events = world.Collect(() => world.Days(15));
            List<SimEvent> paid = events.Where(e => e.Type == SimEventType.FamilyMoneySent && e.Participants[0] == a.Id).ToList();
            Assert.AreEqual(2, paid.Count, "15 дней — две платы");
            Assert.IsTrue(paid.All(e => world.Simulation.Calendar.At(e.TimeHours).Hour == 0));
            Assert.Less(a.State.Wallet, 1000);
        }

        [Test]
        public void Death_StressMourningAutopause_AndKnownOnReturnFromQuest()
        {
            Adventurer a = WithFamily(world.Add());
            FamilyMember wife = a.Family.Members[0];
            a.State.Stress = 10f;
            a.State.Activity = Activity.OnQuestTravel;

            List<SimEvent> away = world.Collect(() => world.Do(ctx => FamilyService.Die(ctx, a, wife)));
            Assert.IsFalse(wife.Alive);
            Assert.AreEqual(FamilyNews.Died, wife.PendingNews, "на задании весть ждёт");
            Assert.IsFalse(away.Any(e => e.Type == SimEventType.FamilyMemberDied));
            Assert.AreEqual(10f, a.State.Stress);

            a.State.Activity = Activity.Resting;
            List<SimEvent> back = world.Collect(() => world.Simulation.Tick());
            SimEvent died = back.Single(e => e.Type == SimEventType.FamilyMemberDied);
            Assert.AreEqual(a.Id, died.Participants[0]);
            Assert.AreEqual(AutopauseKind.FamilyDeath, AutopauseRules.Default[SimEventType.FamilyMemberDied]);
            Assert.Greater(a.State.Stress, 35f, "стресс +30");
            Assert.IsTrue(a.State.IsMourning);
            Assert.AreEqual(FamilyNews.None, wife.PendingNews);
            Assert.AreEqual(2, a.Family.LivingCount, "дети остаются");
            Assert.AreEqual(12, FamilyRules.Norm(a, world.Balance.Family));
            Assert.IsTrue(world.Simulation.World.Feed.Guild.Any(f => f.TemplateKey == FeedKeys.FamilyDied && f.Text.Contains("жена")),
                "строка называет родного");
        }

        [Test]
        public void Card_ShowsFamily_Norm_Need_Mourning()
        {
            Adventurer a = WithFamily(world.Add());
            a.Family.Members[0].Health = FamilyHealth.Ill;
            a.Family.Members[1].Alive = false;
            a.State.FamilyInNeed = true;
            a.State.IsMourning = true;

            GuildMaster.UI.CardModel card = GuildMaster.UI.CardModel.Build(world.Simulation, a.Id);
            Assert.AreEqual("Жена Ида (болеет), сын Ида †, дочь Ида", card.Family);
            StringAssert.Contains("Норма: 26", card.FamilyNorm, "жена больна: 20 + дочь 6");
            Assert.IsTrue(card.FamilyInNeed);
            Assert.IsTrue(card.Mourning);

            Assert.AreEqual("семьи нет", GuildMaster.UI.CardModel.Build(world.Simulation, world.Add().Id).Family.ToLowerInvariant());
        }

        [Test]
        public void Mourning_SlowsStressRelief_EndsAfterDays()
        {
            Adventurer a = world.Add();
            float decay = StateRates.Multiplier(a, StateStat.Stress, RateDirection.Decay, world.Registry);
            a.State.IsMourning = true;
            Assert.AreEqual(decay * 0.5f, StateRates.Multiplier(a, StateStat.Stress, RateDirection.Decay, world.Registry), 1e-4f);
            Assert.AreEqual(StateRates.Multiplier(a, StateStat.Stress, RateDirection.Growth, world.Registry), 1f, 1e-4f, "рост не меняется");

            a.State.MourningEndsAtHours = world.Time.TotalHours + world.Simulation.Calendar.DaysToHours(2);
            world.Days(3);
            Assert.IsFalse(a.State.IsMourning);
        }

        [Test]
        public void Illness_DeathChanceLowerWhenPaid_Frequency()
        {
            const int n = 4000;
            int Deaths(bool paid)
            {
                int deaths = 0;
                for (int i = 0; i < n; i++)
                {
                    Adventurer a = new Adventurer(10_000 + i) { Name = "Ян" };
                    FamilyMember m = Add(a, FamilyRole.Parent);
                    m.Health = FamilyHealth.Ill;
                    m.IllUntilHours = long.MaxValue;
                    world.Do(ctx => FamilyService.WeekOfIllness(ctx, a, paid));
                    if (!m.Alive) deaths++;
                }
                return deaths;
            }
            Assert.AreEqual(0.03f * n, Deaths(true), Frequency.Tolerance(n, 0.03f));
            Assert.AreEqual(0.12f * n, Deaths(false), Frequency.Tolerance(n, 0.12f));
        }

        [Test]
        public void LoanRequest_FamilyInNeed_IllRelativeReason()
        {
            using var dilemmas = new StateWorld(dilemmas: true);
            Adventurer a = WithFamily(dilemmas.Add());
            a.Family.Members[0].Health = FamilyHealth.Ill;
            a.Family.Members[0].IllUntilHours = long.MaxValue;
            a.State.FamilyInNeed = true;
            dilemmas.TickToHour(dilemmas.Balance.Dilemmas.CheckHour);
            Dilemma loan = dilemmas.Simulation.World.Dilemmas.Open.Single(d => d.SubjectId == a.Id);
            Assert.AreEqual("Говорит, что дома болеет жена.", loan.Reason, "при деньгах в кошельке — из-за нужды семьи");
        }
    }

    public sealed class FamilyDeterminismTests
    {
        [Test]
        public void SameSeed_SameLog_WithFamilies()
        {
            using var data = new PeopleData();
            string Run()
            {
                var writer = new StringWriter();
                HeadlessRun.Result result = HeadlessRun.Run(data.Registry, 31u, 75, PlayerBots.Simple(), new SimLogger(SimLogLevel.Debug, writer));
                return writer + PeopleDump.Of(result.Simulation);
            }

            string first = Run();
            StringAssert.Contains("family pay #", first);
            StringAssert.Contains("| family ", first);
            StringAssert.Contains("roll family-ill", first);
            Assert.AreEqual(first, Run());
        }

        /// <summary>Нет родных — система семьи ничего не делает: ни плат, ни бросков, ни строк.</summary>
        [Test]
        public void NoFamilies_FamilySystemDoesNothing()
        {
            using var world = new StateWorld(seed: 13u);
            world.AddMany(4);
            world.Add("Family");
            List<SimEvent> events = world.Collect(() => world.Days(40));
            Assert.IsFalse(events.Any(e => e.Type == SimEventType.FamilyMoneySent || e.Type == SimEventType.FamilyNeedStarted));
            Assert.IsTrue(world.Simulation.World.Adventurers.Active.All(a => !a.State.FamilyInNeed && !a.State.IsMourning));
        }
    }
}
