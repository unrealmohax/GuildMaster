using System.Collections.Generic;
using System.Linq;
using GuildMaster.Core;
using GuildMaster.Data;
using NUnit.Framework;

namespace GuildMaster.Tests
{
    /// <summary>Показатели состояния: занятия, усталость, стресс и срывы, довольство, лояльность, уход.</summary>
    public sealed class StateTests
    {
        private StateWorld world;

        [TearDown]
        public void TearDown() => world?.Dispose();

        [Test]
        public void StartState_OfGeneratedPeople_MatchesSpec()
        {
            world = new StateWorld();
            var rng = new Rng(5u);
            var names = new HashSet<string>();
            var people = Enumerable.Range(1, 1000)
                .Select(i => AdventurerGenerator.Generate(rng, world.Registry, new WorldState(), i, atStart: false, names).Adventurer.State)
                .ToList();

            Assert.IsTrue(people.All(s => s.Fatigue == 10f && s.Contentment == 50f));
            Assert.IsTrue(people.All(s => s.Stress >= 10f && s.Stress <= 30f));
            Assert.IsTrue(people.All(s => s.Loyalty >= 40f && s.Loyalty <= 60f));
            Assert.IsTrue(people.All(s => s.DebtToGuild == 0 && s.Conditions.Count == 0 && !s.IsWalletEmpty));
            Assert.That(people.Average(s => s.Stress), Is.EqualTo(20f).Within(1f));
            Assert.That(people.Average(s => s.Loyalty), Is.EqualTo(50f).Within(1f));
            Assert.AreEqual(21, people.Select(s => s.Stress).Distinct().Count(), "10–30 включительно");
        }

        [TestCase(Activity.OnQuestTravel, 3f, -0.2f)]
        [TestCase(Activity.OnQuestRound, 3f, -0.2f)]
        [TestCase(Activity.OnQuestCamp, -4f, -0.2f)]
        [TestCase(Activity.Training, 2f, -0.2f)]
        [TestCase(Activity.Resting, -3f, -0.5f)]
        [TestCase(Activity.Sleeping, -8f, -0.5f)]
        [TestCase(Activity.Tavern, -1f, -1f)]
        [TestCase(Activity.Infirmary, -3f, -0.5f)]
        [TestCase(Activity.Binge, 0f, -1f)]
        public void HourOfActivity_ChangesFatigueAndStress(Activity activity, float fatigue, float stress)
        {
            world = new StateWorld();
            Adventurer adventurer = world.Add();
            adventurer.State.Fatigue = 50f;
            adventurer.State.Stress = 50f;
            adventurer.State.Activity = activity;

            world.Do(ctx => StateService.ApplyHour(ctx, adventurer, PartyContext.None));

            Assert.That(adventurer.State.Fatigue, Is.EqualTo(50f + fatigue).Within(1e-4f));
            Assert.That(adventurer.State.Stress, Is.EqualTo(50f + stress).Within(1e-4f));
        }

        [Test]
        public void Values_AreClampedTo0And100()
        {
            world = new StateWorld();
            Adventurer adventurer = world.Add();
            adventurer.State.Fatigue = 99f;
            adventurer.State.Stress = 0.1f;
            adventurer.State.Activity = Activity.OnQuestTravel;

            world.Do(ctx => StateService.ApplyHour(ctx, adventurer, PartyContext.None));
            Assert.AreEqual(100f, adventurer.State.Fatigue);
            Assert.AreEqual(0f, adventurer.State.Stress);

            world.Do(ctx =>
            {
                StateService.AddStress(ctx, adventurer, 500f);
                StateService.ChangeContentment(ctx, adventurer, -500f);
                StateService.ChangeLoyalty(ctx, adventurer, 500f);
                StateService.AddFatigue(ctx, adventurer, -500f);
            });
            Assert.AreEqual(100f, adventurer.State.Stress);
            Assert.AreEqual(0f, adventurer.State.Contentment);
            Assert.AreEqual(100f, adventurer.State.Loyalty);
            Assert.AreEqual(0f, adventurer.State.Fatigue);
        }

        [Test]
        public void Schedule_NightSleep_DayRest_EveningTavern()
        {
            world = new StateWorld();
            Adventurer adventurer = world.Add();
            TimeBalance time = world.Balance.Time;

            var byHour = new Dictionary<int, Activity>();
            for (int i = 0; i < 24; i++)
            {
                world.Simulation.Tick();
                byHour[world.Time.Hour] = adventurer.State.Activity;
            }

            for (int hour = 0; hour < 24; hour++)
            {
                Activity expected = hour >= time.NightHour || hour < time.MorningHour ? Activity.Sleeping
                    : hour >= time.EveningHour ? Activity.Tavern
                    : Activity.Resting;
                Assert.AreEqual(expected, byHour[hour], $"{hour}:00");
            }
        }

        [Test]
        public void Schedule_HeavyWound_RestsInsteadOfTavern()
        {
            world = new StateWorld();
            Adventurer adventurer = world.Add();
            world.Do(ctx => HealthService.Wound(ctx, adventurer, ConditionKind.HeavyWound));

            world.TickToHour(world.Balance.Time.EveningHour);
            Assert.AreEqual(Activity.Resting, adventurer.State.Activity);
            world.TickToHour(world.Balance.Time.NightHour);
            Assert.AreEqual(Activity.Sleeping, adventurer.State.Activity);
        }

        [Test]
        public void Fatigue_Above70_LowersProfile_Above90_BansQuests()
        {
            world = new StateWorld();
            Adventurer adventurer = world.Add();
            StateBalance balance = world.Balance.State;

            adventurer.State.Fatigue = 70f;
            Assert.AreEqual(30f, AdventurerStats.Effective(adventurer, StatId.Strength, world.Registry));
            adventurer.State.Fatigue = 70.5f;
            Assert.That(AdventurerStats.Effective(adventurer, StatId.Strength, world.Registry), Is.EqualTo(24f).Within(1e-4f));
            Assert.AreEqual(30f, AdventurerStats.Permanent(adventurer, StatId.Strength, world.Registry), "архетип усталость не учитывает");

            adventurer.State.Fatigue = 90f;
            Assert.IsTrue(StateRules.CanTakeQuests(adventurer.State, balance));
            adventurer.State.Fatigue = 90.5f;
            Assert.IsTrue(StateRules.IsTooTiredForQuests(adventurer.State, balance));
            Assert.IsFalse(StateRules.CanTakeQuests(adventurer.State, balance));
        }

        [Test]
        public void Breakdown_Above80_TenPercentADay()
        {
            List<Adventurer> people = null;
            world = new StateWorld(3u, beforeState: new LambdaSystem("PinStress", ctx =>
            {
                if (ctx.World.Time.Hour != 0) return;
                foreach (Adventurer adventurer in people)
                {
                    adventurer.State.Stress = 95f;
                    adventurer.State.Breakdown = BreakdownKind.None;
                }
            }));
            people = world.AddMany(40);

            const int days = 300;
            List<SimEvent> breakdowns = world.Collect(() => world.Days(days)).Where(e => e.Type == SimEventType.Breakdown).ToList();

            int trials = people.Count * days;
            float expected = 0.1f * trials;
            float tolerance = Frequency.Tolerance(trials, 0.1f);
            TestContext.WriteLine($"{breakdowns.Count} breakdowns, expected {expected:0} ± {tolerance:0}");
            Assert.That(breakdowns.Count, Is.EqualTo(expected).Within(tolerance));
            Assert.IsTrue(breakdowns.All(e => world.Simulation.Calendar.At(e.TimeHours).Hour == 0), "раз в сутки, в 00:00");
            Assert.IsTrue(breakdowns.All(e => e.Importance == EventImportance.Important));
            Assert.AreEqual(breakdowns.Count, breakdowns.Select(e => (e.Participants[0], e.TimeHours)).Distinct().Count());
        }

        [TestCase(80.5f, false)]
        [TestCase(80.6f, true)]
        public void Breakdown_OnlyWhenStressAbove80(float pinned, bool expectAny)
        {
            // Пин до StateSystem, затем час сна (−0,5): к проверке стресс 80 или 80,1.
            List<Adventurer> people = null;
            world = new StateWorld(4u, beforeState: new LambdaSystem("PinStress", ctx =>
            {
                if (ctx.World.Time.Hour != 0) return;
                foreach (Adventurer adventurer in people)
                {
                    adventurer.State.Stress = pinned;
                    adventurer.State.Breakdown = BreakdownKind.None;
                }
            }));
            people = world.AddMany(20);

            int count = world.Collect(() => world.Days(60)).Count(e => e.Type == SimEventType.Breakdown);
            Assert.AreEqual(expectAny, count > 0, $"{count} breakdowns");
        }

        [Test]
        public void BreakdownKind_DependsOnTraits_FirstMatchWins()
        {
            world = new StateWorld();
            DataRegistry data = world.Registry;

            Assert.AreEqual(BreakdownKind.Binge, StateService.BreakdownKindOf(world.Add("Drunkard"), data));
            Assert.AreEqual(BreakdownKind.Binge, StateService.BreakdownKindOf(world.Add("Veteran"), data));

            Adventurer cowardDrunkard = world.Add("Drunkard");
            cowardDrunkard.SetAxis(AxisId.Risk, -80f);
            Assert.AreEqual(BreakdownKind.Binge, StateService.BreakdownKindOf(cowardDrunkard, data), "Пьяница — первым");

            Adventurer reckless = world.Add();
            reckless.SetAxis(AxisId.Risk, 30f);
            Assert.AreEqual(BreakdownKind.Brawl, StateService.BreakdownKindOf(reckless, data));

            Adventurer coward = world.Add();
            coward.SetAxis(AxisId.Risk, -30f);
            Assert.AreEqual(BreakdownKind.RefuseQuests, StateService.BreakdownKindOf(coward, data));

            Adventurer calm = world.Add();
            calm.SetAxis(AxisId.Risk, 29f);
            Assert.AreEqual(BreakdownKind.Collapse, StateService.BreakdownKindOf(calm, data));
        }

        [Test]
        public void Breakdown_Binge_TwoDaysInTavern_DrinksFiveADay_ThenStressMinus30()
        {
            world = new StateWorld();
            Adventurer veteran = world.Add("Veteran");

            world.TickToHour(0);
            veteran.State.Stress = 90f;
            veteran.State.Wallet = 100;
            List<SimEvent> events = world.Collect(() =>
            {
                world.Do(ctx => StateService.StartBreakdown(ctx, veteran, BreakdownKind.Binge));
                world.Simulation.Tick();
            });
            Assert.AreEqual(59f, veteran.State.Stress, 1e-3f, "−30 после срыва, затем час запоя −1");
            Assert.AreEqual(1, events.Count(e => e.Type == SimEventType.Breakdown));
            Assert.IsTrue(veteran.Traits.Single().Revealed, "Ветеран раскрывается первым срывом");
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.TraitRevealed));
            Assert.IsFalse(StateRules.CanTakeQuests(veteran.State, world.Balance.State));

            var activities = new List<Activity>();
            for (int i = 0; i < 46; i++)
            {
                activities.Add(veteran.State.Activity);
                world.Simulation.Tick();
            }
            Assert.IsTrue(activities.All(a => a == Activity.Binge), "весь срок, и ночью тоже");
            Assert.AreEqual(Activity.Binge, veteran.State.Activity, "23:00 второго дня");
            world.Simulation.Tick();
            Assert.AreEqual(BreakdownKind.None, veteran.State.Breakdown);
            Assert.AreEqual(Activity.Sleeping, veteran.State.Activity);

            // Два дня: выпивка 5, еда 2 (в запое за еду таверны не считается), жильё 3.
            Assert.AreEqual(100 - 2 * 5 - 2 * (2 + 3), veteran.State.Wallet);
        }

        [Test]
        public void Breakdown_Collapse_OnlyRests_RefuseQuests_BlocksQuestsThreeDays()
        {
            world = new StateWorld();
            Adventurer tired = world.Add();
            Adventurer coward = world.Add();
            world.TickToHour(0);
            world.Do(ctx =>
            {
                StateService.StartBreakdown(ctx, tired, BreakdownKind.Collapse);
                StateService.StartBreakdown(ctx, coward, BreakdownKind.RefuseQuests);
            });

            for (int hour = 0; hour < 48; hour++)
            {
                world.Simulation.Tick();
                if (world.Time.TotalHours < tired.State.BreakdownEndsAtHours) Assert.AreEqual(Activity.Resting, tired.State.Activity, world.Time.ToString());
            }
            Assert.AreEqual(BreakdownKind.None, tired.State.Breakdown);
            Assert.AreEqual(BreakdownKind.RefuseQuests, coward.State.Breakdown);
            Assert.IsFalse(StateRules.CanTakeQuests(coward.State, world.Balance.State));

            world.Days(1);
            Assert.AreEqual(BreakdownKind.None, coward.State.Breakdown);
            Assert.IsTrue(StateRules.CanTakeQuests(coward.State, world.Balance.State));
        }

        [Test]
        public void Breakdown_Brawl_HurtsRelation_TwentyPercentBothWounded()
        {
            world = new StateWorld(9u);
            Adventurer reckless = world.Add();
            Adventurer other = world.Add();

            const int brawls = 2000;
            int woundedPairs = 0;
            for (int i = 0; i < brawls; i++)
            {
                reckless.State.Stress = other.State.Stress = 0f;   // раны драк не доводят до своих срывов
                List<SimEvent> events = world.Collect(() =>
                {
                    world.Do(ctx => StateService.StartBreakdown(ctx, reckless, BreakdownKind.Brawl));
                    world.Simulation.Tick();
                });
                SimEvent brawl = events.Single(e => e.Type == SimEventType.Breakdown);
                CollectionAssert.AreEqual(new[] { reckless.Id, other.Id }, brawl.Participants);
                int wounds = events.Count(e => e.Type == SimEventType.AdventurerWounded);
                Assert.That(wounds, Is.EqualTo(0).Or.EqualTo(2), "оба или никто");
                if (wounds == 2) woundedPairs++;
            }

            Assert.AreEqual(-100f, world.Simulation.World.Relations.GetValue(reckless.Id, other.Id), "−10 за драку, до −100");
            float tolerance = Frequency.Tolerance(brawls, 0.2f);
            TestContext.WriteLine($"{woundedPairs} of {brawls} brawls wounded both, expected {0.2f * brawls:0} ± {tolerance:0}");
            Assert.That(woundedPairs, Is.EqualTo(0.2f * brawls).Within(tolerance));
        }

        [Test]
        public void Contentment_MovesToTargetByOneADay()
        {
            world = new StateWorld();
            Adventurer rich = world.Add();       // город −10, ест в таверне +5 → цель 45
            Adventurer poor = world.Add();       // город −10, кошелёк меньше недели — не ест в таверне → цель 40
            poor.State.Wallet = 20;
            rich.State.Contentment = 50f;
            poor.State.Contentment = 30f;

            world.TickToHour(1);
            Assert.AreEqual(49f, rich.State.Contentment, 1e-4f);
            Assert.AreEqual(31f, poor.State.Contentment, 1e-4f);

            world.Days(20);
            Assert.AreEqual(45f, rich.State.Contentment, 1e-4f, "не проскакивает цель");
            Assert.IsTrue(poor.State.IsWalletEmpty, "20 монет кончились на пятый день");
            Assert.AreEqual(25f, poor.State.Contentment, 1e-4f, "пустой кошелёк: цель 40 − 15");
        }

        [Test]
        public void ContentmentTarget_SumsConditions()
        {
            world = new StateWorld();
            DataRegistry data = world.Registry;
            Adventurer adventurer = world.Add();
            float commission = world.Balance.Economy.DefaultCommission;

            Assert.AreEqual(40f, StateRules.ContentmentTarget(adventurer, commission, data), "город −10, комиссия 20% — 0");
            adventurer.State.AteInTavernToday = true;
            Assert.AreEqual(45f, StateRules.ContentmentTarget(adventurer, commission, data));
            adventurer.State.IsWalletEmpty = true;
            Assert.AreEqual(30f, StateRules.ContentmentTarget(adventurer, commission, data));
            adventurer.State.Stress = 71f;
            Assert.AreEqual(25f, StateRules.ContentmentTarget(adventurer, commission, data));
            adventurer.State.Stress = 70f;
            Assert.AreEqual(30f, StateRules.ContentmentTarget(adventurer, commission, data));

            adventurer.State.AteInTavernToday = false;
            adventurer.State.IsWalletEmpty = false;
            Assert.AreEqual(35f, StateRules.ContentmentTarget(adventurer, 0.30f, data), "30% — один шаг −5");
            Assert.AreEqual(30f, StateRules.ContentmentTarget(adventurer, 0.35f, data));
            Assert.AreEqual(35f, StateRules.ContentmentTarget(adventurer, 0.34f, data), "только за полные 5%");

            adventurer.SetAxis(AxisId.Money, 100f);
            Assert.AreEqual(20f, StateRules.ContentmentTarget(adventurer, 0.35f, data), "Жадный — × 2");
        }

        [TestCase(0f, 40f, 0.1f, 0.1f)]
        [TestCase(100f, 40f, 0.2f, 0.05f)]     // Преданный: рост × 2, падение × 0,5
        [TestCase(-100f, 40f, 0.05f, 0.2f)]    // Наёмник: наоборот
        [TestCase(50f, 40f, 0.15f, 0.075f)]    // сила по формуле оси
        public void Loyalty_MovesToContentment_ScaledByLoyaltyAxis(float axis, float start, float growth, float decay)
        {
            world = new StateWorld();
            Adventurer rising = world.Add();
            Adventurer falling = world.Add();
            rising.SetAxis(AxisId.Loyalty, axis);
            falling.SetAxis(AxisId.Loyalty, axis);
            rising.State.Loyalty = start;
            falling.State.Loyalty = 80f;
            // Довольство держится на цели 45 (ест в таверне), лояльность идёт к нему.
            rising.State.Contentment = 45f;
            falling.State.Contentment = 45f;

            world.TickToHour(1);
            world.Days(9);

            Assert.AreEqual(start + 10 * growth, rising.State.Loyalty, 1e-3f);
            Assert.AreEqual(80f - 10 * decay, falling.State.Loyalty, 1e-3f);
        }

        [Test]
        public void MonthlyLeave_BelowLoyalty25_TwentyPercent_FamilyTimesOnePointFive()
        {
            world = new StateWorld(21u);
            List<Adventurer> plain = world.AddMany(600);
            List<Adventurer> family = world.AddMany(600, "Family");
            List<Adventurer> loyal = world.AddMany(100);
            foreach (Adventurer adventurer in plain.Concat(family)) adventurer.State.Loyalty = adventurer.State.Contentment = 10f;
            // Граница: пустой кошелёк держит цель довольства на 25 (50 − 10 город − 15), лояльность стоит ровно на 25 весь месяц.
            foreach (Adventurer adventurer in loyal)
            {
                adventurer.State.Loyalty = adventurer.State.Contentment = 25f;
                adventurer.State.Wallet = 0;
                adventurer.State.IsWalletEmpty = true;
            }

            List<SimEvent> events = world.Collect(() => world.TickToHour(0));
            Assert.IsFalse(events.Any(e => e.Type == SimEventType.AdventurerLeft), "проверка — только в начале месяца");

            // До начала месяца 2.
            while (world.Time.Day != 1) world.TickToHour(0);
            int plainLeft = plain.Count(a => !world.Simulation.World.Adventurers.IsActive(a.Id));
            int familyLeft = family.Count(a => !world.Simulation.World.Adventurers.IsActive(a.Id));
            TestContext.WriteLine($"left: plain {plainLeft}/600 (≈120), family {familyLeft}/600 (≈180)");

            Assert.That(plainLeft, Is.EqualTo(120).Within(30));
            Assert.That(familyLeft, Is.EqualTo(180).Within(35));
            Assert.IsTrue(loyal.All(a => a.State.Loyalty == 25f && a.State.Contentment == 25f), "к проверке лояльность ровно 25");
            Assert.IsTrue(loyal.All(a => world.Simulation.World.Adventurers.IsActive(a.Id)), "лояльность 25 — не ниже 25");
            Assert.IsTrue(plain.Where(a => !world.Simulation.World.Adventurers.IsActive(a.Id)).All(a => a.LeaveReason == LeaveReason.Left));
        }

        [Test]
        public void Leaving_IsImportantEvent_WithAutopause()
        {
            world = new StateWorld(2u);
            List<Adventurer> people = world.AddMany(40);
            foreach (Adventurer adventurer in people) adventurer.State.Loyalty = adventurer.State.Contentment = 5f;

            while (!(world.Time.Day == 1 && world.Time.Hour == 0)) world.Simulation.Tick();
            // Такт 00:00 дня 1 уже прошёл: автопауза запрошена в нём.
            Assert.IsTrue(world.Simulation.ConsumePauseRequest());
            Assert.IsTrue(world.Simulation.World.Autopause.Triggers.Any(t => t.Kind == AutopauseKind.MemberLeftGuild));
            SimEvent leftEvent = world.Simulation.World.Autopause.Triggers.First(t => t.Kind == AutopauseKind.MemberLeftGuild).Event;
            Assert.AreEqual(SimEventType.AdventurerLeft, leftEvent.Type);
            Assert.AreEqual(EventImportance.Important, leftEvent.Importance);
            Assert.IsTrue(leftEvent.TryGet("cause", out LeaveCause cause));
            Assert.AreEqual(LeaveCause.LowLoyalty, cause);
        }

        [TestCase(10f, 0)]
        [TestCase(25f, 1)]
        [TestCase(44.9f, 1)]
        [TestCase(45f, 2)]
        [TestCase(65f, 3)]
        [TestCase(84.9f, 3)]
        [TestCase(85f, 4)]
        public void LoyaltyWords_ByThresholds(float loyalty, int index)
        {
            world = new StateWorld();
            StateBalance balance = world.Balance.State;
            // Ровно на границе — слово верхнего интервала.
            Assert.AreEqual(index, StateRules.LoyaltyWordIndex(loyalty, balance));
            Assert.AreEqual(StateRules.LoyaltyWordsMale[index], StateRules.LoyaltyWord(loyalty, Gender.Male, balance));
        }

        [Test]
        public void LoyaltyWords_AgreeWithGender()
        {
            world = new StateWorld();
            StateBalance balance = world.Balance.State;
            Assert.AreEqual("Кажется, подумывает уйти", StateRules.LoyaltyWord(0f, Gender.Female, balance));
            Assert.AreEqual("Кажется, доволен гильдией", StateRules.LoyaltyWord(70f, Gender.Male, balance));
            Assert.AreEqual("Кажется, довольна гильдией", StateRules.LoyaltyWord(70f, Gender.Female, balance));
            Assert.AreEqual("Кажется, предана гильдии", StateRules.LoyaltyWord(90f, Gender.Female, balance));
        }
    }
}
