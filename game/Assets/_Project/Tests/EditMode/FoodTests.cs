using System.Collections.Generic;
using System.IO;
using System.Linq;
using GuildMaster.Core;
using GuildMaster.Data;
using GuildMaster.Debugging;
using NUnit.Framework;

namespace GuildMaster.Tests
{
    /// <summary>Еда и сытость: приёмы пищи, выбор типа по бюджету, голод и истощение, задание, распоряжение для новичков.</summary>
    public sealed class FoodTests
    {
        private StateWorld world;

        [TearDown]
        public void TearDown() => world?.Dispose();

        private static List<SimEvent> MealsOf(List<SimEvent> events, Adventurer adventurer, SimEventType type) =>
            events.Where(e => e.Type == type && e.Participants[0] == adventurer.Id).ToList();

        [Test]
        public void ThreeMealsADay_PaidFromWallet_TierByWallet_TavernIncomeByTier()
        {
            world = new StateWorld();
            Adventurer rich = world.Add();
            Adventurer modest = world.Add();
            Adventurer poor = world.Add();
            Adventurer broke = world.Add();
            rich.State.Wallet = 1000;
            modest.State.Wallet = 150; // остаток над резервом 108: на 10 дней простой хватает (60), роскошной — нет (150)
            poor.State.Wallet = 60;
            broke.State.Wallet = 0;

            List<SimEvent> events = world.Collect(() => world.TickToHour(23));

            Assert.IsTrue(MealsOf(events, rich, SimEventType.MealEaten).All(e => e.TryGet("tier", out MealTier t) && t == MealTier.Luxury));
            Assert.AreEqual(3, MealsOf(events, rich, SimEventType.MealEaten).Count, "три приёма в день");
            Assert.AreEqual(MealTier.Simple, modest.State.LastMealTier);
            Assert.AreEqual(MealTier.Cheap, poor.State.LastMealTier);
            Assert.AreEqual(1000 - 3 * 5, rich.State.Wallet);
            Assert.AreEqual(150 - 3 * 2, modest.State.Wallet);
            Assert.AreEqual(60 - 3 * 1, poor.State.Wallet);

            Assert.AreEqual(3, MealsOf(events, broke, SimEventType.MealSkipped).Count, "нет денег — приёмы пропущены");
            Assert.IsNull(broke.State.LastMealTier);
            Assert.IsTrue(broke.State.IsWalletEmpty);
            Assert.AreEqual(0f, broke.State.TasteToday);
            Assert.AreEqual(3 * 10f, rich.State.TasteToday, 1e-4f, "вкус: роскошная +10 за приём");
            Assert.AreEqual(3 * 3f, modest.State.TasteToday, 1e-4f, "простая +3");
            Assert.AreEqual(3 * -5f, poor.State.TasteToday, 1e-4f, "дешёвая −5");

            world.TickToHour(0);
            LedgerEntry food = world.Simulation.World.Treasury.Ledger.Single(e => e.Category == LedgerCategories.TavernFood);
            Assert.AreEqual("cheap=3 simple=3 luxury=3", food.Comment);
            Assert.AreEqual(6, food.Amount, "3 × 0,2 + 3 × 0,5 + 3 × 1,5 = 6,6 → 6, остаток — на завтра");
        }

        [Test]
        public void Choice_BudgetRule_ReserveThenDays_GreedyAndFamilySaveMore()
        {
            world = new StateWorld();
            DataRegistry data = world.Registry;
            Adventurer plain = world.Add();
            Adventurer greedy = world.Add();
            greedy.SetAxis(AxisId.Money, 100f); // дни × 2
            Adventurer family = world.Add("Family"); // дни × 1,5

            MealTier? Choose(Adventurer a, int wallet)
            {
                a.State.Wallet = wallet;
                return FoodRules.Choose(a, data, out _);
            }

            Assert.AreEqual(42, FoodRules.Reserve(plain, world.Balance), "7 × (город 3 + 3 × дешёвая 1)");
            Assert.AreEqual(MealTier.Luxury, Choose(plain, 42 + 150));
            Assert.AreEqual(MealTier.Simple, Choose(plain, 42 + 149));
            Assert.AreEqual(MealTier.Simple, Choose(plain, 42 + 60));
            Assert.AreEqual(MealTier.Cheap, Choose(plain, 42 + 59));
            Assert.AreEqual(MealTier.Cheap, Choose(plain, 1), "меньше резерва — дешёвая, если она есть в кошельке");
            Assert.IsNull(Choose(plain, 0), "нет денег — приём пропущен");

            Assert.AreEqual(20f, FoodRules.BudgetDays(greedy, data), 1e-4f);
            Assert.AreEqual(MealTier.Cheap, Choose(greedy, 42 + 60), "жадному нужно 120 на простую");
            Assert.AreEqual(MealTier.Simple, Choose(greedy, 42 + 120));
            Assert.AreEqual(15f, FoodRules.BudgetDays(family, data), 1e-4f);
            Assert.AreEqual(MealTier.Cheap, Choose(family, 42 + 89));
            Assert.AreEqual(MealTier.Simple, Choose(family, 42 + 90));
        }

        [Test]
        public void AfterMeal_RaisesUpToCap_NeverLowers()
        {
            FoodBalance food = new FoodBalance();
            Assert.AreEqual(45f, FoodRules.AfterMeal(20f, food.Cheap), 1e-4f);
            Assert.AreEqual(60f, FoodRules.AfterMeal(50f, food.Cheap), 1e-4f, "дешёвая — не выше 60");
            Assert.AreEqual(70f, FoodRules.AfterMeal(70f, food.Cheap), 1e-4f, "сытее потолка — не меняется");
            Assert.AreEqual(85f, FoodRules.AfterMeal(70f, food.Simple), 1e-4f);
            Assert.AreEqual(100f, FoodRules.AfterMeal(70f, food.Luxury), 1e-4f);
        }

        [Test]
        public void ThreeMealsADay_KeepSatietyInBand()
        {
            world = new StateWorld();
            Adventurer poor = world.Add();
            Adventurer modest = world.Add();
            var poorValues = new List<float>();
            var modestValues = new List<float>();
            for (int hour = 0; hour < 24 * 10; hour++)
            {
                poor.State.Wallet = 60;
                modest.State.Wallet = 150;
                world.Simulation.Tick();
                if (hour < 48) continue; // со старта сытость 100 — ждём установившегося
                poorValues.Add(poor.State.Satiety);
                modestValues.Add(modest.State.Satiety);
            }
            Assert.That(poorValues.Min(), Is.GreaterThanOrEqualTo(34f), "дешёвая держит без стресса");
            Assert.That(poorValues.Max(), Is.LessThanOrEqualTo(60f));
            Assert.That(modestValues.Min(), Is.GreaterThanOrEqualTo(59f));
            Assert.That(modestValues.Max(), Is.LessThanOrEqualTo(85f));
        }

        [Test]
        public void NoMoney_Hungry_Starving_Exhausted_StressStatsContentmentAndAutopause()
        {
            world = new StateWorld();
            Adventurer person = world.Add();
            Adventurer fed = world.Add(); // такой же, но с деньгами: те же занятия и решения
            person.State.Wallet = 0;
            person.State.Stress = fed.State.Stress = 50f; // выше нуля: снятие стресса отдыхом не упирается в 0
            DataRegistry data = world.Registry;

            var events = new List<SimEvent>();
            while (person.State.Satiety > 0f) events.AddRange(world.Collect(() => world.Simulation.Tick()));
            events.AddRange(world.Collect(() => world.Simulation.Tick()));

            Assert.Greater(person.State.Stress, fed.State.Stress + 3f, "голодному — +0,5 стресса в час ниже 30, сытому — нет");
            SimEvent hunger = events.Single(e => e.Type == SimEventType.HungerStarted);
            Assert.AreEqual(EventImportance.Notable, hunger.Importance);
            SimEvent exhausted = events.Single(e => e.Type == SimEventType.Exhausted);
            Assert.AreEqual(EventImportance.Important, exhausted.Importance);
            Assert.AreEqual(AutopauseKind.Exhausted, AutopauseRules.Default[SimEventType.Exhausted], "истощение ставит автопаузу");

            Assert.AreEqual(HungerStage.Exhausted, FoodRules.StageOf(person, world.Balance.Food));
            Assert.AreEqual(30f * 0.75f, AdventurerStats.Effective(person, StatId.Strength, data), 1e-3f, "истощение: характеристики × 0,75");
            Assert.AreEqual(30f, AdventurerStats.Effective(person, StatId.Knowledge, data), 1e-3f, "навыки не меняются");
            Assert.AreEqual(30f, AdventurerStats.Permanent(person, StatId.Strength, data), 1e-3f, "в архетип не входит");
            person.State.Satiety = 5f;
            Assert.AreEqual(30f * 0.9f, AdventurerStats.Effective(person, StatId.Strength, data), 1e-3f, "голод: × 0,9");
            person.State.Satiety = 0f;

            world.TickToHour(23);
            float contentment = person.State.Contentment;
            float target = StateRules.ContentmentTarget(person, world.Simulation.World.Treasury.Commission, data, world.Simulation.World);
            world.Simulation.Tick();
            float moved = contentment > target ? contentment - 1f : contentment + 1f;
            Assert.AreEqual(moved - 10f, person.State.Contentment, 1e-3f, "истощение: −10 довольства в сутки после сдвига к цели");
        }

        [Test]
        public void Hungry_MotivesMoneyUpSafetyDown_LeaveCauseHunger()
        {
            world = new StateWorld();
            DataRegistry data = world.Registry;
            Adventurer person = world.Add();
            MotiveWeights fed = Motives.Weigh(person, data, false);
            person.State.Satiety = 20f;
            MotiveWeights hungry = Motives.Weigh(person, data, false);

            Assert.AreEqual(fed[Motive.Money] * 1.5f, hungry[Motive.Money], 1e-4f);
            Assert.AreEqual(fed[Motive.Safety] * 0.7f, hungry[Motive.Safety], 1e-4f);
            Assert.IsTrue(hungry.TryGetFactor(Motive.Money, out StateFactor factor, out _));
            Assert.AreEqual(StateFactor.Hunger, factor);

            List<LeaveCause> causes = LeaveReasons.Of(person, data, world.Time.TotalHours, world.Simulation.Calendar);
            Assert.AreEqual(LeaveCause.Hunger, causes[0]);
            Assert.IsTrue(LeaveReasons.IsMoney(LeaveCause.Hunger));
            Assert.AreEqual("не мог прокормиться", world.Do(ctx => LeaveReasons.Text(ctx, person, new[] { LeaveCause.Hunger })));
        }

        [Test]
        public void OnQuest_SatietyDecays_EatsRations_NotTavern()
        {
            using var quests = new QuestWorld();
            Adventurer person = quests.Add();
            person.State.Satiety = 50f;
            Order order = quests.AddOrder(distance: OrderDistance.Far);
            quests.Start(order, person);
            int wallet = person.State.Wallet;
            int rations = person.Rations.Total;
            Assert.Greater(rations, 0, "пайки куплены при выходе");

            FoodBalance food = quests.Registry.Balance.Food;
            int meals = 0;
            bool decayed = false;
            List<SimEvent> events = quests.World.Collect(() =>
            {
                for (int i = 0; i < 24 && person.State.IsOnQuest(); i++)
                {
                    if (food.IsMealHour(quests.Simulation.World.Time.Hour)) meals++;
                    float before = person.State.Satiety;
                    quests.Simulation.Tick();
                    decayed |= person.State.Satiety < before;
                }
            });

            Assert.IsTrue(person.State.IsOnQuest(), "всё ещё в пути");
            Assert.Greater(meals, 0);
            Assert.AreEqual(rations - meals, person.Rations.Total, "в каждый приём пищи — паёк");
            Assert.IsTrue(decayed, "на задании сытость убывает");
            Assert.AreEqual(wallet - 3, person.State.Wallet, "в пути за еду не платит — только жильё в 00:00");
            Assert.IsFalse(events.Any(e => (e.Type == SimEventType.MealEaten || e.Type == SimEventType.MealSkipped) && e.Participants[0] == person.Id),
                "в таверне не ест");
        }

        [Test]
        public void NewcomerDecree_GuildPaysSimpleMeal_LuxuryIsOwnMoney()
        {
            world = new StateWorld();
            Adventurer poorNewcomer = world.Add();
            Adventurer richNewcomer = world.Add();
            poorNewcomer.State.Wallet = 60;   // сам взял бы дешёвую
            richNewcomer.State.Wallet = 1000; // сам берёт роскошную
            world.Simulation.Send(new ToggleDecreeCommand("FreeLodgingForNewcomers", true));
            world.Simulation.ApplyCommandsNow();

            world.TickToHour(world.Balance.Food.MealHours[0]);

            Assert.AreEqual(MealTier.Simple, poorNewcomer.State.LastMealTier, "гильдия кормит простой едой");
            Assert.AreEqual(60, poorNewcomer.State.Wallet, "новичок сам не платит");
            Assert.AreEqual(MealTier.Luxury, richNewcomer.State.LastMealTier);
            Assert.AreEqual(1000 - 5, richNewcomer.State.Wallet, "роскошную платит сам целиком");

            List<LedgerEntry> decrees = world.Simulation.World.Treasury.Ledger.Where(e => e.Category == LedgerCategories.Decrees).ToList();
            Assert.AreEqual(1, decrees.Count, "гильдия платит только за простую еду бедного");
            Assert.AreEqual(-2, decrees[0].Amount);
            Assert.AreEqual(poorNewcomer.Id, decrees[0].RelatedId);
        }
    }

    public sealed class FoodDeterminismTests
    {
        private PeopleData data;

        [SetUp]
        public void SetUp() => data = new PeopleData();

        [TearDown]
        public void TearDown() => data.Dispose();

        [Test]
        public void SameSeed_SameLog_WithMeals()
        {
            string Run()
            {
                var writer = new StringWriter();
                HeadlessRun.Result result = HeadlessRun.Run(data.Registry, 18u, 60, PlayerBots.Simple(), new SimLogger(SimLogLevel.Debug, writer));
                return writer + PeopleDump.Of(result.Simulation);
            }

            string first = Run();
            StringAssert.Contains("meal #", first);
            Assert.AreEqual(first, Run());
        }

        [Test]
        public void Eating_SpendsNoRandomNumbers()
        {
            uint NextAfter(bool eat)
            {
                using var world = new StateWorld(3u);
                List<Adventurer> people = world.AddMany(5);
                people[0].State.Wallet = 0;
                people[1].State.Wallet = 60;
                people[2].State.Satiety = 0f;
                return world.Do(ctx =>
                {
                    if (eat)
                    {
                        foreach (Adventurer adventurer in people)
                        {
                            FoodService.Eat(ctx, adventurer);
                            FoodService.ApplyHour(ctx, adventurer);
                            FoodService.ApplyDay(ctx, adventurer);
                        }
                    }
                    return ctx.Rng.NextUInt();
                });
            }

            Assert.AreEqual(NextAfter(eat: false), NextAfter(eat: true), "еда не тратит случайных чисел");
        }

        [Test]
        public void Hunger_DoesNotShiftOtherSystems()
        {
            data.NoQuests();
            string Run(bool starve)
            {
                List<ISimSystem> systems = SimulationSystems.CreateDefault();
                if (starve)
                {
                    systems.Add(new LambdaSystem("Starve", ctx =>
                    {
                        foreach (Adventurer adventurer in ctx.World.Adventurers.Active)
                        {
                            adventurer.State.Wallet = 0;
                            adventurer.State.Satiety = 5f;
                        }
                    }));
                }
                systems.Add(new NoiseSystem("Veteran", issueIds: false));
                var simulation = new Simulation(data.Registry, 5u, systems);
                string log = SimulationLog.Record(simulation, sim => SimulationRun.Days(sim, 60));
                string[] others = { "[TimeSystem]", "[RecruitSystem]", "[AdventurerSystem]", "[Veteran]" };
                return string.Join("\n", log.Split('\n').Where(line => others.Any(line.Contains)));
            }

            string fed = Run(starve: false);
            StringAssert.Contains("CandidateArrived", fed);
            Assert.AreEqual(fed, Run(starve: true));
        }
    }
}
