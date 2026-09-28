using System.Collections.Generic;
using System.Linq;
using GuildMaster.Core;
using GuildMaster.Data;
using GuildMaster.Debugging;
using NUnit.Framework;

namespace GuildMaster.Tests
{
    /// <summary>Статьи журнала только для тестов: расходы обоих видов и доход, чтобы вывести казну в плюс.</summary>
    internal static class TestLedger
    {
        public static readonly LedgerCategory Mandatory = new LedgerCategory("testMandatory", LedgerFlow.Expense, ExpenseKind.Mandatory);
        public static readonly LedgerCategory IfAffordable = new LedgerCategory("testIfAffordable", LedgerFlow.Expense, ExpenseKind.IfAffordable);
        public static readonly LedgerCategory Income = new LedgerCategory("testIncome", LedgerFlow.Income);

        /// <summary>Такты до начала следующего месяца (00:00 первого числа).</summary>
        public static void TickToMonthStart(Simulation simulation)
        {
            do simulation.Tick();
            while (!(simulation.World.Time.Hour == 0 && simulation.World.Time.Day == 1));
        }

        /// <summary>Журнал сходится с казной: старт + сумма записей = деньги, у каждой записи — казна после неё.</summary>
        public static void AssertLedgerMatchesMoney(Treasury treasury)
        {
            int money = treasury.StartMoney;
            foreach (LedgerEntry entry in treasury.Ledger)
            {
                Assert.IsNotNull(entry.Category);
                Assert.AreNotEqual(0, entry.Amount);
                money += entry.Amount;
                Assert.AreEqual(money, entry.BalanceAfter);
            }
            Assert.AreEqual(treasury.Money, money);
        }
    }

    public sealed class TreasuryTests
    {
        [Test]
        public void Start_MoneyFromBalance_DefaultCommission_EmptyLedger()
        {
            using var data = new TestData();
            var simulation = Simulation.CreateDefault(data.Registry, 1u);
            Treasury treasury = simulation.World.Treasury;

            Assert.AreEqual(data.Balance.Guild.StartMoney, treasury.Money);
            Assert.AreEqual(2000, treasury.StartMoney);
            Assert.AreEqual(0.2f, treasury.Commission, 1e-6f);
            Assert.IsEmpty(treasury.Ledger);
            Assert.IsFalse(treasury.Bankruptcy.Active);
            Assert.IsFalse(simulation.IsFinished);
        }

        [Test]
        public void CreditAndDebit_WriteLedger_WithCategoryAmountCommentAndRelated()
        {
            using var data = new TestData();
            var simulation = Simulation.CreateDefault(data.Registry, 1u);
            SimulationRun.Do(simulation, ctx =>
            {
                TreasuryService.Credit(ctx, TestLedger.Income, 150, "gift", 7);
                Assert.IsTrue(TreasuryService.Debit(ctx, TestLedger.Mandatory, 40, "repair"));
                TreasuryService.Credit(ctx, TestLedger.Income, 0); // нулевая сумма записи не даёт
            });

            Treasury treasury = simulation.World.Treasury;
            Assert.AreEqual(2110, treasury.Money);
            Assert.AreEqual(2, treasury.Ledger.Count);
            LedgerEntry credit = treasury.Ledger[0];
            Assert.AreSame(TestLedger.Income, credit.Category);
            Assert.AreEqual(150, credit.Amount);
            Assert.AreEqual("gift", credit.Comment);
            Assert.AreEqual(7, credit.RelatedId);
            Assert.AreEqual(simulation.World.Time.TotalHours, credit.TimeHours);
            Assert.AreEqual(-40, treasury.Ledger[1].Amount);
            TestLedger.AssertLedgerMatchesMoney(treasury);
        }

        [Test]
        public void MandatoryExpense_GoesNegative_IfAffordableExpense_IsSkippedWithoutMoney()
        {
            using var data = new TestData();
            var simulation = Simulation.CreateDefault(data.Registry, 1u);
            bool affordable = true;
            bool mandatory = false;
            SimulationRun.Do(simulation, ctx =>
            {
                affordable = TreasuryService.Debit(ctx, TestLedger.IfAffordable, 2500);
                mandatory = TreasuryService.Debit(ctx, TestLedger.Mandatory, 2500);
            });

            Treasury treasury = simulation.World.Treasury;
            Assert.IsFalse(affordable);
            Assert.IsTrue(mandatory);
            Assert.AreEqual(-500, treasury.Money);
            Assert.AreEqual(1, treasury.Ledger.Count);
            Assert.AreEqual(simulation.World.Time.TotalHours, treasury.Bankruptcy.NegativeSinceHours);
            TestLedger.AssertLedgerMatchesMoney(treasury);
        }

        [Test]
        public void WrongFlow_IsRejected()
        {
            using var data = new TestData();
            var simulation = Simulation.CreateDefault(data.Registry, 1u);
            Assert.Throws<System.ArgumentException>(() => SimulationRun.Do(simulation, ctx => TreasuryService.Credit(ctx, TestLedger.Mandatory, 5)));
            Assert.Throws<System.ArgumentException>(() => SimulationRun.Do(simulation, ctx => TreasuryService.Debit(ctx, TestLedger.Income, 5)));
        }
    }

    public sealed class CommissionTests
    {
        [Test]
        public void SetCommission_ClampsToLimits_PublishesChange_SameValueIsSilent()
        {
            using var data = new TestData();
            var simulation = Simulation.CreateDefault(data.Registry, 1u);
            List<SimEvent> events = SimulationRun.Collect(simulation, sim =>
            {
                sim.Send(new SetCommissionCommand(0.9f));
                sim.Tick();
                sim.Send(new SetCommissionCommand(0.5f));
                sim.Send(new SetCommissionCommand(-1f));
                sim.Tick();
            });

            List<SimEvent> changes = events.Where(e => e.Type == SimEventType.CommissionChanged).ToList();
            Assert.AreEqual(2, changes.Count, "0.9 → 0.5; the second 0.5 is not a change; −1 → 0");
            Assert.IsTrue(changes[0].TryGet("to", out float first));
            Assert.AreEqual(0.5f, first, 1e-6f);
            Assert.AreEqual(0f, simulation.World.Treasury.Commission, 1e-6f);
        }

        [Test]
        public void CommissionAbove25_LowersContentment_AtOrBelow25_DoesNot()
        {
            float ContentmentAfter(float? commission)
            {
                using var world = new StateWorld(3u);
                Adventurer adventurer = world.Add();
                if (commission.HasValue) world.Simulation.Send(new SetCommissionCommand(commission.Value));
                world.Days(10);
                return adventurer.State.Contentment;
            }

            float byDefault = ContentmentAfter(null);
            Assert.AreEqual(byDefault, ContentmentAfter(0.25f), 1e-4f);
            Assert.Less(ContentmentAfter(0.4f), byDefault - 1f);
        }

        [Test]
        public void ContentmentTarget_UsesCurrentCommission()
        {
            using var world = new StateWorld(3u);
            Adventurer adventurer = world.Add();
            float before = StateRules.ContentmentTarget(adventurer, world.Simulation.World.Treasury.Commission, world.Registry);
            world.Simulation.Send(new SetCommissionCommand(0.4f));
            world.Simulation.ApplyCommandsNow();
            float after = StateRules.ContentmentTarget(adventurer, world.Simulation.World.Treasury.Commission, world.Registry);
            Assert.AreEqual(before - 15f, after, 1e-4f, "three full 5% steps above 25%");
        }
    }

    public sealed class TavernIncomeTests
    {
        [Test]
        public void Servings_PayHalfCoinEach_RemainderCarriesOver()
        {
            using var data = new TestData();
            var simulation = Simulation.CreateDefault(data.Registry, 1u);
            SimulationRun.Do(simulation, ctx =>
            {
                TreasuryService.CountTavernFood(ctx);
                TreasuryService.CountTavernDrink(ctx);
                TreasuryService.CountTavernDrink(ctx);
                TreasuryService.SettleTavern(ctx); // 1,5 → 1, остаток 0,5
                TreasuryService.CountTavernFood(ctx);
                TreasuryService.SettleTavern(ctx); // 0,5 + 0,5 → 1
                TreasuryService.SettleTavern(ctx); // порций нет
            });

            Treasury treasury = simulation.World.Treasury;
            Assert.AreEqual(2, treasury.Ledger.Count);
            Assert.IsTrue(treasury.Ledger.All(e => e.Category == LedgerCategories.Tavern && e.Amount == 1));
            Assert.AreEqual("food=1 drinks=2", treasury.Ledger[0].Comment);
            Assert.AreEqual(2002, treasury.Money);
        }

        [Test]
        public void TavernVisitors_BringIncome_OncePerDayAtMidnight()
        {
            using var world = new StateWorld(4u);
            world.Data.Set("decisions.bestChoiceChance", 1f); // вечером всегда таверна
            List<Adventurer> people = world.AddMany(4);
            foreach (Adventurer adventurer in people.Take(2)) adventurer.State.Stress = 50f; // эти ещё и пьют

            world.Days(10);

            Treasury treasury = world.Simulation.World.Treasury;
            Assert.Greater(treasury.Ledger.Count, 5);
            Assert.Greater(treasury.Money, treasury.StartMoney);
            foreach (LedgerEntry entry in treasury.Ledger)
            {
                Assert.AreSame(LedgerCategories.Tavern, entry.Category);
                Assert.AreEqual(0, world.Simulation.Calendar.At(entry.TimeHours).Hour);
            }
            // 4 еды (2 монеты) и до 2 выпивок в сутки.
            Assert.IsTrue(treasury.Ledger.Skip(1).All(e => e.Amount >= 2 && e.Amount <= 3));
            TestLedger.AssertLedgerMatchesMoney(treasury);
        }

        [Test]
        public void EmptyWallet_NoDrink_NoIncome()
        {
            using var world = new StateWorld(4u);
            world.Data.Set("decisions.bestChoiceChance", 1f);
            Adventurer adventurer = world.Add();
            adventurer.State.Wallet = 0;
            adventurer.State.Stress = 50f;

            world.Days(3);

            Assert.IsEmpty(world.Simulation.World.Treasury.Ledger);
        }
    }

    public sealed class BankruptcyTests
    {
        /// <summary>Такты, пока не случится событие (включая такт с ним), не дольше срока. События — все за эти такты.</summary>
        private static List<SimEvent> TickUntil(Simulation simulation, SimEventType type, int maxDays)
        {
            var events = new List<SimEvent>();
            bool found = false;
            void Append(IReadOnlyList<SimEvent> tickEvents)
            {
                events.AddRange(tickEvents);
                found |= tickEvents.Any(e => e.Type == type);
            }

            long limit = simulation.Calendar.DaysToHours(maxDays);
            simulation.TickCompleted += Append;
            try
            {
                for (long i = 0; i < limit && !found && !simulation.IsFinished; i++) simulation.Tick();
            }
            finally
            {
                simulation.TickCompleted -= Append;
            }
            return events;
        }

        [Test]
        public void ThirtyDaysNegative_StartsBankruptcy_WithAutopauseAndFeedLine()
        {
            using var world = new StateWorld(5u);
            Simulation simulation = world.Simulation;
            long negativeSince = world.Time.TotalHours;
            world.Do(ctx => TreasuryService.Debit(ctx, TestLedger.Mandatory, 3000));

            SimEvent started = TickUntil(simulation, SimEventType.BankruptcyStarted, 31).Single(e => e.Type == SimEventType.BankruptcyStarted);
            bool paused = simulation.ConsumePauseRequest();
            Assert.IsTrue(simulation.World.Autopause.Triggers.Any(t => t.Kind == AutopauseKind.BankruptcyStarted));

            Bankruptcy bankruptcy = simulation.World.Treasury.Bankruptcy;
            Assert.AreEqual(negativeSince + simulation.Calendar.DaysToHours(30), started.TimeHours);
            Assert.AreEqual(EventImportance.Important, started.Importance);
            Assert.IsTrue(paused);
            Assert.IsTrue(bankruptcy.Active);
            Assert.AreEqual(started.TimeHours + simulation.Calendar.HoursPerMonth * 4, bankruptcy.EndsAtHours);
            FeedEntry line = simulation.World.Feed.Guild.Last(e => e.TemplateKey == FeedKeys.BankruptcyStarted);
            Assert.AreEqual("Казна пуста уже месяц. Начинается банкротство", line.Text);
        }

        [Test]
        public void BackToPositive_BeforeThirtyDays_ResetsCounter()
        {
            using var world = new StateWorld(5u);
            world.Do(ctx => TreasuryService.Debit(ctx, TestLedger.Mandatory, 3000));
            world.Days(20);
            world.Do(ctx => TreasuryService.Credit(ctx, TestLedger.Income, 1500));
            Assert.IsNull(world.Simulation.World.Treasury.Bankruptcy.NegativeSinceHours);
            world.Do(ctx => TreasuryService.Debit(ctx, TestLedger.Mandatory, 1000));

            List<SimEvent> events = world.Collect(() => world.Days(20));
            Assert.IsFalse(events.Any(e => e.Type == SimEventType.BankruptcyStarted), "the 30 days start again after going positive");
        }

        [Test]
        public void BackToPositive_LiftsBankruptcy_WithFeedLine()
        {
            using var world = new StateWorld(5u);
            Simulation simulation = world.Simulation;
            world.Do(ctx => TreasuryService.Debit(ctx, TestLedger.Mandatory, 3000));
            TickUntil(simulation, SimEventType.BankruptcyStarted, 31);
            Assert.IsTrue(simulation.World.Treasury.Bankruptcy.Active);

            // До нуля: за месяц к долгу добавились содержание и зарплаты.
            world.Do(ctx => TreasuryService.Credit(ctx, TestLedger.Income, -ctx.World.Treasury.Money));
            List<SimEvent> events = world.Collect(() => simulation.Tick());

            Assert.IsTrue(events.Any(e => e.Type == SimEventType.BankruptcyLifted));
            Assert.IsFalse(simulation.World.Treasury.Bankruptcy.Active);
            Assert.IsNull(simulation.World.Treasury.Bankruptcy.NegativeSinceHours);
            FeedEntry line = simulation.World.Feed.Guild.Last();
            Assert.AreEqual(FeedKeys.BankruptcyLifted, line.TemplateKey);
            Assert.AreEqual("Казна снова в плюсе. Из долговой ямы выбрались", line.Text);
        }

        [Test]
        public void TimerRunsOutNegative_ClosesGuild_AndStopsSimulation()
        {
            using var world = new StateWorld(5u);
            Simulation simulation = world.Simulation;
            List<Adventurer> people = world.AddMany(3);
            long negativeSince = world.Time.TotalHours;
            world.Do(ctx => TreasuryService.Debit(ctx, TestLedger.Mandatory, 1_000_000));

            List<SimEvent> events = TickUntil(simulation, SimEventType.GuildClosed, 30 + 4 * 30 + 2);

            SimEvent closed = events.Single(e => e.Type == SimEventType.GuildClosed);
            Assert.AreEqual(negativeSince + simulation.Calendar.DaysToHours(30) + simulation.Calendar.HoursPerMonth * 4, closed.TimeHours);
            Assert.IsTrue(simulation.IsFinished);
            Assert.IsTrue(simulation.World.Treasury.IsClosed);
            Assert.AreEqual(closed.TimeHours, simulation.World.Treasury.ClosedAtHours);
            Assert.IsTrue(closed.TryGet("days", out int days));
            Assert.AreEqual(150, days);
            Assert.AreEqual(3, closed.Participants.Count, "the best people are named");
            Assert.AreEqual(people.Count, simulation.World.Adventurers.Active.Count);
            Assert.AreEqual(FeedKeys.GuildClosed, simulation.World.Feed.Guild.Last().TemplateKey);

            GameTime time = simulation.World.Time;
            long ticks = simulation.TicksDone;
            simulation.Tick();
            simulation.Send(new SetCommissionCommand(0.4f));
            simulation.ApplyCommandsNow();
            Assert.AreEqual(time.TotalHours, simulation.World.Time.TotalHours, "time does not move after the guild closed");
            Assert.AreEqual(ticks, simulation.TicksDone);
            Assert.AreEqual(0.2f, simulation.World.Treasury.Commission, 1e-6f);
        }

        [Test]
        public void HeadlessRun_EndsEarly_WhenGuildCloses()
        {
            using var data = new PeopleData();
            data.Set("guild.startMoney", -100_000); // доход таверны минус не покроет
            data.Set("economy.bankruptcyStartDays", 1);
            data.Set("economy.bankruptcyMonths", 1);

            HeadlessRun.Result result = HeadlessRun.Run(data.Registry, 3u, 360, PlayerBots.Passive());

            Assert.IsTrue(result.GuildClosed);
            Assert.AreEqual(result.Simulation.TicksDone, result.Ticks);
            Assert.AreEqual(24 * (1 + 30), result.Ticks, "one day to start bankruptcy, one month to close");
            Assert.Greater(result.Summary.Months.Count, 0);
        }
    }

    public sealed class MonthReportTests
    {
        [Test]
        public void Report_AtMonthStart_MatchesLedger_AndListsPeople()
        {
            using var world = new StateWorld(6u);
            world.Data.Set("decisions.bestChoiceChance", 1f);
            Simulation simulation = world.Simulation;
            world.Days(2);
            Adventurer joined = world.Add();
            Adventurer leaver = world.Add();
            world.Days(3);
            world.Do(ctx =>
            {
                AdventurerLifecycle.Retire(ctx, leaver, LeaveReason.Left);
                RevealService.TryRevealBalanced(ctx, joined, AxisId.Risk);
                TreasuryService.Debit(ctx, TestLedger.Mandatory, 70);
            });

            List<SimEvent> events = world.Collect(() => TestLedger.TickToMonthStart(simulation));

            Assert.AreEqual(1, simulation.World.Reports.Reports.Count);
            MonthReport report = simulation.World.Reports.GetLast();
            Treasury treasury = simulation.World.Treasury;
            Assert.AreEqual(1, report.Year);
            Assert.AreEqual(1, report.Month);
            Assert.AreEqual(simulation.World.Time.TotalHours, report.ToHours);
            Assert.AreEqual(2000, report.MoneyAtStart);
            Assert.AreEqual(treasury.Money, report.MoneyAtEnd);
            Assert.AreEqual(0, report.LedgerFrom);
            Assert.AreEqual(treasury.Ledger.Count, report.LedgerTo, "the midnight tavern income is in the report");

            int income = treasury.Ledger.Where(e => e.Amount > 0).Sum(e => e.Amount);
            Assert.Greater(income, 0);
            Assert.AreEqual(income, report.Income);
            // 1-го числа — ещё содержание построек и зарплаты стартового персонала.
            int guildCosts = -treasury.Ledger.Where(e => e.Category == LedgerCategories.Upkeep || e.Category == LedgerCategories.Salaries).Sum(e => e.Amount);
            Assert.AreEqual(90 + StaffRules.MonthlySalaries(simulation.World), guildCosts);
            Assert.AreEqual(70 + guildCosts, report.Expense);
            Assert.AreEqual(report.MoneyAtEnd, report.MoneyAtStart + report.Income - report.Expense);

            Assert.IsTrue(report.TryGetSection(MonthReportSections.MoneyTitle, out ReportSection money));
            Assert.IsTrue(money.TryGetLine(LedgerCategories.Tavern.TextKey, out ReportLine tavern));
            Assert.AreEqual(income, tavern.Amount);
            Assert.IsTrue(money.TryGetLine(TestLedger.Mandatory.TextKey, out ReportLine expense));
            Assert.AreEqual(-70, expense.Amount);

            Assert.IsTrue(report.TryGetSection(MonthReportSections.PeopleTitle, out ReportSection people));
            Assert.IsTrue(people.TryGetLine(MonthReportSections.PeopleJoined, out ReportLine joinedLine));
            CollectionAssert.AreEqual(new[] { joined.Id, leaver.Id }, joinedLine.Items.Select(i => i.PersonId));
            Assert.IsTrue(people.TryGetLine(MonthReportSections.PeopleLeft, out ReportLine leftLine));
            Assert.AreEqual(leaver.Id, leftLine.Items.Single().PersonId, "the starting six retired at the start hour are not news");
            Assert.IsTrue(people.TryGetLine(MonthReportSections.PeopleRevealed, out ReportLine revealed));
            Assert.AreEqual(MonthReportSections.AxisDetail(AxisId.Risk, null), revealed.Items.Single().Detail);

            SimEvent ready = events.Single(e => e.Type == SimEventType.MonthReportReady);
            Assert.AreEqual(EventImportance.Notable, ready.Importance);
            Assert.IsFalse(AutopauseRules.Default.ContainsKey(SimEventType.MonthReportReady));
            FeedEntry line = simulation.World.Feed.Guild.Last(e => e.TemplateKey == FeedKeys.MonthReport);
            Assert.AreEqual($"Прошёл месяц. Заработано {income}, потрачено {70 + guildCosts}, погибших — 0", line.Text);

            var errors = new List<string>();
            List<string> text = MonthReportText.Lines(report, simulation.World, world.Registry, errors);
            CollectionAssert.Contains(text, "Деньги");
            CollectionAssert.Contains(text, "Казна на начало месяца: 2000");
            CollectionAssert.Contains(text, "Таверна: +" + income);
            CollectionAssert.Contains(text, "Люди");
            CollectionAssert.Contains(text, "Ушли: Тест");
            Assert.IsTrue(text.Any(t => t.StartsWith("Раскрылись: Тест — ") && t.EndsWith(": уравновешенность")), string.Join("\n", text));
            CollectionAssert.AreEqual(new[] { "no template ledger.testMandatory" }, errors, "only the test category has no name");
        }

        [Test]
        public void NextReport_StartsWhereThePreviousEnded_AndDoesNotRepeatPeople()
        {
            using var world = new StateWorld(6u);
            world.Data.Set("decisions.bestChoiceChance", 1f);
            Simulation simulation = world.Simulation;
            world.Days(1);
            world.Add();
            TestLedger.TickToMonthStart(simulation);
            world.Do(ctx => TreasuryService.Credit(ctx, TestLedger.Income, 5)); // на паузе в час отчёта — уже следующий месяц
            TestLedger.TickToMonthStart(simulation);

            IReadOnlyList<MonthReport> reports = simulation.World.Reports.Reports;
            Assert.AreEqual(2, reports.Count);
            Assert.AreEqual(reports[0].MoneyAtEnd, reports[1].MoneyAtStart);
            Assert.AreEqual(reports[0].LedgerTo, reports[1].LedgerFrom);
            Assert.AreEqual(reports[0].ToHours, reports[1].FromHours);
            Assert.AreEqual(2, reports[1].Month);
            Assert.IsTrue(reports[1].TryGetSection(MonthReportSections.MoneyTitle, out ReportSection money));
            Assert.IsTrue(money.TryGetLine(TestLedger.Income.TextKey, out ReportLine gift));
            Assert.AreEqual(5, gift.Amount);
            Assert.IsTrue(reports[1].TryGetSection(MonthReportSections.PeopleTitle, out ReportSection people));
            Assert.IsFalse(people.TryGetLine(MonthReportSections.PeopleJoined, out _));

            int total = reports.Sum(r => r.Income - r.Expense);
            Assert.AreEqual(simulation.World.Treasury.Money - simulation.World.Treasury.StartMoney, total);
        }
    }

    public sealed class HardTimesTests
    {
        private static SpecialTraitDefinition Tested(StateWorld world) => TraitRules.FindByHook(world.Registry, TraitHook.TestedOnAcquire);

        /// <summary>Ветеран гильдии с высокой лояльностью и Преданный с низкой.</summary>
        private static (Adventurer veteran, Adventurer loyal) Setup(StateWorld world)
        {
            Adventurer veteran = world.Add();
            veteran.JoinedAtHours = world.Time.TotalHours - world.Simulation.Calendar.DaysToHours(200);
            veteran.State.Loyalty = 70f;
            Adventurer loyal = world.Add();
            loyal.SetAxis(AxisId.Loyalty, 60f);
            loyal.State.Loyalty = 30f;
            return (veteran, loyal);
        }

        [Test]
        public void SomeoneLeft_TestedAppears_LoyalIsRevealed()
        {
            using var world = new StateWorld(8u);
            (Adventurer veteran, Adventurer loyal) = Setup(world);
            Adventurer newcomer = world.Add();
            newcomer.State.Loyalty = 70f;
            Adventurer leaver = world.Add();
            world.Days(5);
            world.Do(ctx => AdventurerLifecycle.Retire(ctx, leaver, LeaveReason.Left));

            float loyaltyBefore = 0f;
            List<SimEvent> events = world.Collect(() =>
            {
                while (!(world.Time.Day == 30 && world.Time.Hour == 23)) world.Simulation.Tick();
                loyaltyBefore = veteran.State.Loyalty;
                world.Simulation.Tick();
            });

            Assert.IsTrue(veteran.TryGetTrait(Tested(world).Id, out TraitInstance tested));
            Assert.IsTrue(tested.Revealed);
            Assert.Greater(veteran.State.Loyalty, loyaltyBefore + 5f);
            Assert.IsFalse(newcomer.HasTrait(Tested(world).Id), "fewer than 180 days in the guild");
            Assert.IsTrue(loyal.IsAxisRevealed(AxisId.Loyalty));
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.AxisRevealed && e.Participants[0] == loyal.Id));

            MonthReport report = world.Simulation.World.Reports.GetLast();
            Assert.IsTrue(report.HasItem(MonthReportSections.PeopleRevealed, loyal.Id, MonthReportSections.AxisDetail(AxisId.Loyalty, AxisPole.Positive)));
            Assert.IsTrue(report.HasItem(MonthReportSections.PeopleRevealed, veteran.Id, MonthReportSections.TraitDetail(Tested(world).Id)));
        }

        [Test]
        public void Bankruptcy_IsHardTimes()
        {
            using var world = new StateWorld(8u);
            (Adventurer veteran, Adventurer loyal) = Setup(world);
            world.Do(ctx => TreasuryService.Debit(ctx, TestLedger.Mandatory, 10_000));
            TestLedger.TickToMonthStart(world.Simulation); // месяц 2: банкротства ещё нет, никто не ушёл
            Assert.IsFalse(veteran.HasTrait(Tested(world).Id));
            Assert.IsFalse(loyal.IsAxisRevealed(AxisId.Loyalty));
            loyal.State.Loyalty = 30f; // Преданный за месяц подтянул лояльность к довольству

            TestLedger.TickToMonthStart(world.Simulation); // месяц 3: банкротство идёт
            Assert.IsTrue(world.Simulation.World.Treasury.Bankruptcy.Active);
            Assert.IsTrue(veteran.HasTrait(Tested(world).Id));
            Assert.IsTrue(loyal.IsAxisRevealed(AxisId.Loyalty));
        }

        [Test]
        public void NoHardTimes_NothingHappens()
        {
            using var world = new StateWorld(8u);
            (Adventurer veteran, Adventurer loyal) = Setup(world);
            TestLedger.TickToMonthStart(world.Simulation);

            Assert.IsFalse(HardTimes.IsNow(world.Simulation.World, world.Simulation.Calendar, out int left));
            Assert.AreEqual(0, left);
            Assert.IsFalse(veteran.HasTrait(Tested(world).Id));
            Assert.IsFalse(loyal.IsAxisRevealed(AxisId.Loyalty));
        }
    }

    public sealed class EconomyDeterminismTests
    {
        private PeopleData data;

        [SetUp]
        public void SetUp() => data = new PeopleData();

        [TearDown]
        public void TearDown() => data.Dispose();

        [Test]
        public void SameSeed_SameTreasuryLedgerLogAndFeed()
        {
            string Run()
            {
                HeadlessRun.Result result = HeadlessRun.Run(data.Registry, 42u, 360, PlayerBots.Simple());
                WorldState world = result.Simulation.World;
                var text = new System.Text.StringBuilder();
                text.Append(world.Treasury.Money).Append('\n');
                foreach (LedgerEntry entry in world.Treasury.Ledger)
                    text.Append(entry.TimeHours).Append(' ').Append(entry.Category.Id).Append(' ').Append(entry.Amount).Append(' ').Append(entry.Comment).Append('\n');
                foreach (MonthReport report in world.Reports.Reports)
                    text.Append(string.Join("|", MonthReportText.Lines(report, world, data.Registry))).Append('\n');
                foreach (FeedEntry entry in world.Feed.Guild) text.Append(entry.Text).Append('\n');
                return text.ToString();
            }

            string first = Run();
            StringAssert.Contains("tavern", first);
            Assert.AreEqual(first, Run());
        }

        [Test]
        public void EconomySystems_DoNotShiftOtherSystems()
        {
            string Run(bool withEconomy)
            {
                List<ISimSystem> systems = SimulationSystems.CreateDefault();
                if (!withEconomy) systems.RemoveAll(s => s is EconomySystem || s is MonthReportSystem);
                var simulation = new Simulation(data.Registry, 5u, systems);
                string log = SimulationLog.Record(simulation, sim => SimulationRun.Days(sim, 120));
                return string.Join("\n", log.Split('\n').Where(line => !line.Contains("[EconomySystem]") && !line.Contains("[MonthReportSystem]")
                    && !line.Contains("MonthReportReady")));
            }

            string without = Run(withEconomy: false);
            StringAssert.Contains("CandidateArrived", without);
            Assert.AreEqual(without, Run(withEconomy: true));
        }

        [Test]
        public void YearRun_SummaryHasTreasuryAndCategories_LedgerMatchesMoney()
        {
            HeadlessRun.Result result = HeadlessRun.Run(data.Registry, 11u, 360, PlayerBots.Simple());
            RunSummary summary = result.Summary;
            List<string> names = summary.MonthColumns.Select(c => c.Name).ToList();
            int money = names.IndexOf("Казна");
            int income = names.IndexOf("Доходы");
            int tavern = names.IndexOf("Доход: Таверна");
            Assert.GreaterOrEqual(money, 0);
            Assert.GreaterOrEqual(tavern, 0);
            Assert.GreaterOrEqual(names.IndexOf("Банкротство"), 0);
            Assert.AreEqual(12, summary.Months.Count);
            double categories = names.Where(n => n.StartsWith("Доход: ")).Sum(n => summary.Totals[names.IndexOf(n)]);
            Assert.AreEqual(summary.Totals[income], categories, 1e-6, "доходы — сумма статей");
            Assert.Greater(summary.Totals[names.IndexOf("Доход: Комиссия")], 0, "комиссия с заданий");
            Assert.AreEqual(2000 + summary.Totals[income] - summary.Totals[names.IndexOf("Расходы")], summary.Totals[money], 1e-6,
                "казна — старт + доходы − расходы (находки и событийные задания)");
            Assert.Greater(summary.Totals[income], 0);
            Assert.AreEqual(12, result.Simulation.World.Reports.Reports.Count, "11 month starts in the year and the first of the next");
            TestLedger.AssertLedgerMatchesMoney(result.Simulation.World.Treasury);
        }
    }

    public sealed class EconomyScenarioTests
    {
        [Test]
        public void CommissionCommand_RoundTripsThroughScenario()
        {
            Assert.IsTrue(ScenarioCommands.TryFormat(new SetCommissionCommand(0.3f), out string text));
            Assert.AreEqual("commission 0.3", text);
            var parsed = (SetCommissionCommand)ScenarioCommands.Parse("commission", new[] { "0.3" });
            Assert.AreEqual(0.3f, parsed.Commission);
        }

        [Test]
        public void SimpleBot_SetsCommission20_Once()
        {
            using var data = new TestData();
            HeadlessRun.Result result = HeadlessRun.Run(data.Registry, 1u, 3, PlayerBots.Simple());
            StringAssert.Contains("0 commission 0.2", result.Scenario.ToString());
            Assert.AreEqual(0.2f, result.Simulation.World.Treasury.Commission, 1e-6f);
        }
    }
}
