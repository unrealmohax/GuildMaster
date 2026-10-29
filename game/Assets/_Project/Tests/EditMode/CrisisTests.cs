using System.Collections.Generic;
using System.IO;
using System.Linq;
using GuildMaster.Core;
using GuildMaster.Data;
using GuildMaster.Debugging;
using GuildMaster.UI;
using NUnit.Framework;

namespace GuildMaster.Tests
{
    /// <summary>
    /// Траты и кризисы: содержание за людей, Арсенал и его уровни, проекты поселения, набег, налог, зима, мор, детерминизм.
    /// </summary>
    public sealed class CrisisTests
    {
        private static Building Ready(StateWorld w, string definitionId) => w.Do(ctx =>
        {
            Building building = ctx.World.Buildings.Create(definitionId);
            building.State = BuildingState.Ready;
            return building;
        });

        private static void Money(StateWorld w, int money) => w.Do(ctx => TreasuryService.Credit(ctx, LedgerCategories.DebugIncome, money, "test"));

        private static void TickUntil(StateWorld w, System.Func<bool> done, string what, int maxDays = 720) =>
            SimulationRun.TickUntil(w.Simulation, done, what, maxDays);

        /// <summary>Сумма изменений населения по причине в событиях.</summary>
        private static int Population(List<SimEvent> events, string reason)
        {
            int sum = 0;
            foreach (SimEvent e in events)
            {
                if (e.Type != SimEventType.PopulationChanged || !e.TryGet("reason", out string why) || why != reason) continue;
                e.TryGet("from", out int from);
                e.TryGet("to", out int to);
                sum += to - from;
            }
            return sum;
        }

        // ---------- Содержание ----------

        [Test]
        public void Upkeep_GrowsWithPeople()
        {
            using var world = new StateWorld();
            world.AddMany(5);
            TickUntil(world, () => world.Time.Day == 1 && world.Time.Month == 2 && world.Time.Hour == 1, "1-е число");
            LedgerEntry people = world.Simulation.World.Treasury.Ledger.Single(e => e.Comment == BuildingService.PeopleUpkeepComment);
            Assert.AreEqual(-10 * 5, people.Amount, "10 в месяц за каждого человека");
            Assert.AreEqual(LedgerCategories.Upkeep, people.Category);
            Assert.AreEqual(5, people.RelatedId, "сколько людей");
            int buildings = world.Registry.Get<BuildingDefinition>("GuildHall").UpkeepPerMonth + world.Registry.Get<BuildingDefinition>("Tavern").UpkeepPerMonth;
            Assert.AreEqual(buildings + 50, BuildingRules.MonthlyUpkeep(world.Simulation.World, world.Registry), "Зал и Таверна + люди");
        }

        // ---------- Арсенал ----------

        [Test]
        public void Armory_UpgradesToLevelThree_SameQueue_CostAndTimeGrow()
        {
            using var world = new StateWorld();
            Money(world, 100_000);
            int upkeep = BuildingRules.MonthlyUpkeep(world.Simulation.World, world.Registry);
            Building armory = Ready(world, "Armory");
            BuildingDefinition definition = world.Registry.Get<BuildingDefinition>("Armory");
            Assert.AreEqual(3, definition.MaxLevel);
            Assert.AreEqual(2250, BuildingService.CostFor(definition, 2), "цена × 1,5");
            Assert.AreEqual(15, BuildingService.DaysFor(definition, 2), "срок × 1,5");
            Assert.AreEqual(3375, BuildingService.CostFor(definition, 3), "цена × 1,5²");

            world.Do(ctx => new UpgradeBuildingCommand(armory.Id).Apply(ctx));
            Assert.AreEqual(armory, world.Simulation.World.Buildings.Current, "улучшение — та же стройка");
            Assert.AreEqual(1, armory.Level, "пока идёт — прежний уровень");
            Assert.IsTrue(armory.IsReady);
            world.Do(ctx => new UpgradeBuildingCommand(armory.Id).Apply(ctx));
            Assert.AreEqual(0, world.Simulation.World.Buildings.Queue.Count, "второй раз то же улучшение не заказать");

            List<SimEvent> events = world.Collect(() => TickUntil(world, () => armory.Level == 2, "уровень 2"));
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.BuildingUpgraded));
            Assert.IsFalse(events.Any(e => e.Type == SimEventType.BuildingReady), "улучшение — не новая постройка");

            world.Do(ctx => new UpgradeBuildingCommand(armory.Id).Apply(ctx));
            TickUntil(world, () => armory.Level == 3, "уровень 3");
            world.Do(ctx => new UpgradeBuildingCommand(armory.Id).Apply(ctx));
            Assert.IsNull(world.Simulation.World.Buildings.Current, "выше третьего нельзя");
            Assert.AreEqual(2250 + 3375, armory.PaidCost, "два улучшения (сама постройка — готовая)");
            Assert.AreEqual(3, BuildingRules.LevelOf(world.Simulation.World, world.Registry, BuildingFunction.Armory));
            Assert.AreEqual(upkeep + 60 * 3, BuildingRules.MonthlyUpkeep(world.Simulation.World, world.Registry), "содержание × уровень");
        }

        [Test]
        public void Armory_ProfileOnQuestOnly()
        {
            using var quests = new QuestWorld();
            Adventurer a = quests.Add();
            float before = AdventurerStats.Effective(a, StatId.Strength, quests.Registry);
            Building armory = Ready(quests.World, "Armory");
            quests.World.Do(ctx => armory.Level = 2);
            Assert.AreEqual(before, AdventurerStats.Effective(a, StatId.Strength, quests.Registry), 1e-4, "в городе — без снаряжения");

            QuestRun run = quests.Start(quests.AddOrder(), a);
            Assert.AreEqual(2, a.State.GearLevel);
            Assert.AreEqual(before * 1.1f, AdventurerStats.Effective(a, StatId.Strength, quests.Registry), 1e-3, "на задании × (1 + 0,05 × 2)");
            quests.RunToEnd(run);
            Assert.AreEqual(0, a.State.QuestRunId);
            float back = AdventurerStats.Effective(a, StatId.Strength, quests.Registry);
            quests.World.Do(ctx => a.State.GearLevel = 0);
            Assert.AreEqual(AdventurerStats.Effective(a, StatId.Strength, quests.Registry), back, 1e-4, "вернулся — снаряжение не действует");
        }

        // ---------- Проекты ----------

        [Test]
        public void Projects_ChangeDecayAttacksPopulationAndOrders()
        {
            using var world = new StateWorld(settlement: true);
            WorldState w = world.Simulation.World;
            DataRegistry data = world.Registry;
            Assert.AreEqual(0.3f, CrisisRules.DailyDecay(w, data), 1e-5);
            Ready(world, "Watchtower");
            Assert.AreEqual(0.15f, CrisisRules.DailyDecay(w, data), 1e-5, "вышка: спад − 0,15");

            world.Do(ctx => SettlementService.ChangeSecurity(ctx, -45f, "test"));
            Assert.IsTrue(CrisisRules.AttackPossible(w, data));
            Ready(world, "Palisade");
            Assert.IsFalse(CrisisRules.AttackPossible(w, data), "Частокол: при Безопасности не ниже 10 нападений нет");
            Assert.AreEqual(5, CrisisRules.AttackLoss(10, w, data), "ущерб × 0,5");
            world.Do(ctx => SettlementService.ChangeSecurity(ctx, -10f, "test"));
            Assert.IsTrue(CrisisRules.AttackPossible(w, data));

            float orders = CrisisRules.OrdersPerDay(w, data);
            Money(world, 10_000);
            world.Do(ctx => new StartBuildingCommand("Road").Apply(ctx));
            List<SimEvent> road = world.Collect(() => TickUntil(world, () => w.Buildings.IsReady("Road"), "Дорога"));
            Assert.IsTrue(w.Treasury.Ledger.Any(e => e.Category == LedgerCategories.Projects && e.Amount == -3000), "статья «Проекты поселения»");
            Assert.AreEqual(15, Population(road, SettlementReasons.Project), "Дорога: + 15 жителей");
            Assert.AreEqual(5, Population(road, SettlementReasons.Building), "и обычные + 5 за постройку");
            Assert.Greater(CrisisRules.OrdersPerDay(w, data), orders + 0.49f, "Дорога: заказов в день + 0,5");

            Assert.AreEqual(0f, CrisisRules.ExtraCalmGrowth(w, data));
            Ready(world, "Well");
            Assert.AreEqual(0.01f, CrisisRules.ExtraCalmGrowth(w, data), 1e-6, "Колодец: рост в спокойствии + 1%");
            Assert.AreEqual(3, SettlementRules.MonthlyPopulationChange(100, SettlementLevel.Calm, data.Balance.Settlement, 0.01f), "100 × 3%");
        }

        // ---------- Кризисы ----------

        [Test]
        public void Year_IsPlanned_RaidTaxWinter_WarnedThirtyDaysAhead()
        {
            using var world = new StateWorld(crises: true);
            world.Simulation.Tick();
            CrisisBook book = world.Simulation.World.Crises;
            Calendar calendar = world.Simulation.Calendar;
            Assert.AreEqual(1, book.PlannedYear);
            Crisis raid = book.All.Single(c => c.Kind == CrisisKind.Raid);
            Assert.AreEqual(calendar.ToTotalHours(1, 3, 1, 6), raid.StartsAtHours, "набег — 1-го числа 3-го месяца утром");
            Assert.AreEqual(raid.StartsAtHours - calendar.DaysToHours(30), raid.WarnAtHours);
            CollectionAssert.AreEqual(new[] { calendar.ToTotalHours(1, 4, 1, 0), calendar.ToTotalHours(1, 7, 1, 0), calendar.ToTotalHours(1, 10, 1, 0),
                calendar.ToTotalHours(2, 1, 1, 0) }, book.All.Where(c => c.Kind == CrisisKind.Tax).Select(c => c.StartsAtHours).ToArray(), "налог в конце кварталов");
            Crisis winter = book.All.Single(c => c.Kind == CrisisKind.Winter);
            Assert.AreEqual(calendar.ToTotalHours(1, 10, 1, 0), winter.StartsAtHours);
            Assert.AreEqual(calendar.ToTotalHours(2, 1, 1, 0), winter.EndsAtHours);

            List<SimEvent> events = world.Collect(() => TickUntil(world, () => raid.Status == CrisisStatus.Warned, "объявление набега"));
            SimEvent warning = events.Single(e => e.Type == SimEventType.CrisisWarning);
            Assert.AreEqual(raid.WarnAtHours, warning.TimeHours);
            Assert.IsTrue(warning.TryGet("count", out int days) && days == 30);
            Assert.IsTrue(world.Simulation.World.Feed.Guild.Any(f => f.TemplateKey == FeedKeys.CrisisWarningRaid));
            Assert.AreEqual(AutopauseKind.CrisisWarning, AutopauseRules.Default[SimEventType.CrisisWarning]);
        }

        [Test]
        public void Raid_OrderOnBoard_NotTaken_Lost_SecurityAttackAndPopulation()
        {
            using var data = new PeopleData();
            data.NoQuests(); // никто не берёт заказ: он снимается по сроку
            data.NoBankruptcy();
            data.Set("crises.raidLostSecurity", 30f);
            data.Set("crises.raidLostPopulationShare", 0.1f);
            data.Set("crises.raidDays", 10);
            var simulation = Simulation.CreateDefault(data.Registry, 3u);
            simulation.Tick();
            WorldState w = simulation.World;
            Crisis raid = w.Crises.All.Single(c => c.Kind == CrisisKind.Raid);
            SimulationRun.TickUntil(simulation, () => raid.IsActive, "набег");
            Assert.IsTrue(w.Orders.TryGetOpen(raid.OrderId, out Order order));
            Assert.IsTrue(order.IsRaid);
            Assert.AreEqual(GuildRank.F, order.Rank, "1-й год — F");
            Assert.AreEqual(200, order.Reward, "100 × 2");
            Assert.AreEqual(OrderDistance.Near, order.Distance);
            Assert.IsTrue(data.Registry.Get<QuestTypeDefinition>(order.TypeId).IsRaid);
            Assert.AreEqual(raid.StartsAtHours + simulation.Calendar.DaysToHours(10), order.ExpiresAtHours);

            float security = w.Settlement.Security;
            int population = w.Settlement.Population;
            List<SimEvent> events = SimulationRun.Collect(simulation, sim => SimulationRun.TickUntil(sim, () => raid.IsOver, "итог набега"));
            Assert.AreEqual(CrisisOutcome.Lost, raid.Outcome);
            Assert.AreEqual(1, events.Count(e => e.Type == SimEventType.SettlementAttacked), "нападение без броска");
            Assert.IsTrue(w.Settlement.Changes.Any(c => c.Reason == SettlementReasons.RaidLost && c.Delta <= -29.9f), "Безопасность − 30");
            Assert.IsFalse(w.Settlement.Changes.Any(c => c.Reason == SettlementReasons.Expired && c.OrderId == order.Id), "обычный сдвиг за угрозу — не у набега");
            Assert.Less(w.Settlement.Population, population, "часть жителей ушла");
            Assert.Less(w.Settlement.Security, security);
            Assert.AreEqual(1, w.Crises.Totals.RaidsLost);
        }

        [Test]
        public void Raid_QuestDone_Won()
        {
            using var quests = new QuestWorld(crises: true);
            quests.Simulation.Tick();
            WorldState w = quests.Simulation.World;
            Crisis raid = w.Crises.All.Single(c => c.Kind == CrisisKind.Raid);
            SimulationRun.TickUntil(quests.Simulation, () => raid.IsActive, "набег");
            w.Orders.TryGetOpen(raid.OrderId, out Order order);
            foreach (StatId stat in quests.Registry.Stats.RadarOrder) order.SetRequirement(stat, 10f);
            Adventurer a = PartyTestSupport.Person(quests, stats: 90f, rank: GuildRank.F);
            QuestRun run = quests.Start(order, a);
            List<SimEvent> events = quests.RunToEnd(run);
            Assert.IsTrue(run.Completed);
            Assert.AreEqual(CrisisOutcome.Won, raid.Outcome);
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.CrisisEnded));
            Assert.IsTrue(w.Settlement.Changes.Any(c => c.Reason == SettlementReasons.RaidWon && c.Delta > 0f), "Безопасность + 10");
            Assert.IsFalse(w.Settlement.Changes.Any(c => c.Reason == SettlementReasons.ThreatDone && c.OrderId == order.Id));
        }

        [Test]
        public void Tax_BasePlusShareOfQuarterIncome_UnpaidCostsReputationAndSecurity()
        {
            using var world = new StateWorld(crises: true);
            world.Simulation.Tick();
            WorldState w = world.Simulation.World;
            Crisis tax = w.Crises.All.First(c => c.Kind == CrisisKind.Tax);
            Money(world, 1000);
            TickUntil(world, () => tax.IsOver, "налог");
            long income = CrisisRules.IncomeSince(w, 0);
            Assert.AreEqual(CrisisRules.Tax(income, world.Registry.Balance.Crises), tax.Amount);
            Assert.AreEqual(200 + (int)System.Math.Round(income * 0.1, System.MidpointRounding.AwayFromZero), tax.Amount);
            Assert.AreEqual(-tax.Amount, w.Treasury.Ledger.Single(e => e.Category == LedgerCategories.Tax).Amount);

            Crisis second = w.Crises.All.Where(c => c.Kind == CrisisKind.Tax).ElementAt(1);
            world.Do(ctx => TreasuryService.Debit(ctx, LedgerCategories.DebugExpense, w.Treasury.Money + 50, "test"));
            float reputation = w.Guild.Reputation + 20f;
            world.Do(ctx => ReputationService.Change(ctx, 20f, ReputationReasons.Debug));
            List<SimEvent> events = world.Collect(() => TickUntil(world, () => second.IsOver, "второй налог"));
            Assert.AreEqual(CrisisOutcome.Unpaid, second.Outcome);
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.CrisisStarted && e.TryGet("unpaid", out bool unpaid) && unpaid));
            Assert.IsTrue(w.Guild.Changes.Any(c => c.Reason == ReputationReasons.TaxUnpaid), "репутация − 10");
            Assert.Less(w.Guild.Reputation, reputation);
            Assert.IsTrue(w.Settlement.Changes.Any(c => c.Reason == SettlementReasons.TaxUnpaid), "Безопасность − 10");
        }

        [Test]
        public void Winter_FoodAndHousingDearer_FewerOrders_FasterDecay()
        {
            using var world = new StateWorld(crises: true);
            Money(world, 1_000_000); // без доходов содержание и зарплаты разорили бы гильдию до зимы
            world.Simulation.Tick();
            WorldState w = world.Simulation.World;
            DataRegistry data = world.Registry;
            Crisis winter = w.Crises.All.Single(c => c.Kind == CrisisKind.Winter);
            float orders = CrisisRules.OrdersPerDay(w, data);
            Adventurer a = world.Add();
            int housing = FoodRules.HousingCost(a, data.Balance, false);

            TickUntil(world, () => winter.IsActive, "зима", 400);
            Assert.IsTrue(CrisisRules.IsWinter(w));
            Assert.AreEqual(2, FoodRules.MealPrice(MealTier.Cheap, data.Balance, true), "дешёвая 1 → 2");
            Assert.AreEqual(3, FoodRules.MealPrice(MealTier.Simple, data.Balance, true), "простая 2 → 3");
            Assert.AreEqual(8, FoodRules.MealPrice(MealTier.Luxury, data.Balance, true), "роскошная 5 → 8");
            Assert.AreEqual(housing + 1, FoodRules.HousingCost(a, data.Balance, true), "жильё в городе + 1");
            Assert.AreEqual(0.45f, CrisisRules.DailyDecay(w, data), 1e-5, "спад × 1,5");
            Assert.Less(CrisisRules.OrdersPerDay(w, data), orders, "заказов × 0,7");
            Assert.AreEqual(Season.Winter, CrisisRules.SeasonOf(w.Time.Month, world.Simulation.Calendar.MonthsPerYear));

            List<SimEvent> events = world.Collect(() => TickUntil(world, () => winter.IsOver, "конец зимы", 100));
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.CrisisEnded));
            Assert.IsFalse(CrisisRules.IsWinter(w));
            Assert.AreEqual(2, w.Crises.PlannedYear, "расписание на второй год");
        }

        [Test]
        public void Plague_PopulationFallsWeekly_HealerSlowsIt_TavernHalved()
        {
            using var world = new StateWorld(crises: true);
            world.Data.Set("crises.plagueChance", 1f);
            Money(world, 1_000_000);
            world.Simulation.Tick();
            WorldState w = world.Simulation.World;
            Crisis plague = w.Crises.All.Single(c => c.Kind == CrisisKind.Plague);
            List<SimEvent> start = world.Collect(() => TickUntil(world, () => plague.IsActive, "мор", 400));
            Assert.IsFalse(start.Any(e => e.Type == SimEventType.CrisisWarning && e.TryGet("kind", out CrisisKind kind) && kind == CrisisKind.Plague),
                "у мора объявления нет");
            Assert.AreEqual(0.5f, CrisisRules.LocalsTavernMultiplier(w, world.Registry));
            List<SimEvent> week = world.Collect(() => TickUntil(world, () => plague.WeeksApplied == 1, "неделя мора"));
            Assert.AreEqual(-3, Population(week, SettlementReasons.Plague), "− 3 в неделю");

            Ready(world, "Infirmary");
            world.Do(ctx => ctx.World.Staff.AddMember(new StaffMember(ctx.World.Staff.NextId(), "Лекарь", Gender.Male, "Healer", 50, 100, ctx.World.Time.TotalHours)));
            List<SimEvent> second = world.Collect(() => TickUntil(world, () => plague.WeeksApplied == 2, "вторая неделя"));
            Assert.AreEqual(-1, Population(second, SettlementReasons.Plague), "с Лазаретом и Лекарем − 1");
            TickUntil(world, () => plague.IsOver, "конец мора");
            Assert.AreEqual(30, w.Crises.Totals.PlagueDays, 1);
        }

        // ---------- Интерфейс ----------

        [Test]
        public void Ui_NextCrisis_Projects_ArmoryUpgrade_Calendar()
        {
            using var world = new StateWorld(crises: true);
            ISimulationClient client = world.Simulation;
            world.Simulation.Tick();
            TickUntil(world, () => world.Simulation.World.Crises.GetNextWarned() != null, "объявление");
            StringAssert.StartsWith("Набег через 30 дн.", CrisisModel.TopLine(client));
            Assert.IsTrue(CrisisModel.Lines(client).Any(l => l.StartsWith("Набег через")));
            var calendar = new CalendarModel();
            calendar.Refresh(client);
            Assert.IsTrue(calendar.Items.Any(i => i.Text == UiStrings.CrisisRaid), "набег в календаре");

            Assert.AreEqual(4, CrisisModel.Projects(client).Count, "четыре проекта поселения");
            Assert.IsTrue(CrisisModel.Projects(client).All(p => p.CanOrder && p.Effect.Length > 0));
            Assert.IsFalse(BuildingsModel.Build(client).Any(r => world.Registry.Get<BuildingDefinition>(r.DefinitionId).SettlementProject),
                "проекты — не во вкладке «Постройки»");

            Building armory = Ready(world, "Armory");
            BuildingRow row = BuildingsModel.Build(client).Single(r => r.DefinitionId == "Armory");
            Assert.IsTrue(row.CanUpgrade);
            Assert.AreEqual(2, row.NextLevel);
            Assert.AreEqual(2250, row.UpgradeCost);
            StringAssert.Contains("+10%", row.NextEffect);
            StringAssert.Contains("уровень 1", row.State);

            Money(world, 100_000);
            BuildingActions.Upgrade(client, armory.Id);
            world.Simulation.ApplyCommandsNow();
            Assert.AreEqual(armory, world.Simulation.World.Buildings.Current);
            StringAssert.Contains("улучшается до уровня 2", BuildingsModel.Build(client).Single(r => r.DefinitionId == "Armory").State);
        }

        // ---------- Детерминизм ----------

        [Test]
        public void SameSeed_SameLog_WithCrises()
        {
            using var data = new PeopleData();
            data.Set("crises.plagueChance", 1f);
            string Run()
            {
                var writer = new StringWriter();
                HeadlessRun.Run(data.Registry, 31u, 400, PlayerBots.Smart(), new SimLogger(SimLogLevel.Debug, writer));
                return writer.ToString();
            }

            string first = Run();
            StringAssert.Contains("crisis start", first);
            StringAssert.Contains("roll plague", first);
            Assert.AreEqual(first, Run());
        }

        [Test]
        public void CrisisSystem_DoesNotShiftOtherSystems()
        {
            using var data = new PeopleData();
            data.NoQuests();
            data.NeutralRumor();
            string Run(bool withCrises)
            {
                List<ISimSystem> systems = SimulationSystems.CreateDefault();
                if (!withCrises) systems.RemoveAll(s => s is CrisisSystem);
                var simulation = new Simulation(data.Registry, 5u, systems);
                string log = SimulationLog.Record(simulation, sim => SimulationRun.Days(sim, 120));
                // Заказы не сравниваются: заказ набега берёт номер на доске.
                string[] others = { "[TimeSystem]", "[RecruitSystem]", "[AdventurerSystem]" };
                return string.Join("\n", log.Split('\n').Where(line => others.Any(line.Contains)));
            }

            string without = Run(withCrises: false);
            StringAssert.Contains("CandidateArrived", without);
            Assert.AreEqual(without, Run(withCrises: true));
        }
    }
}
