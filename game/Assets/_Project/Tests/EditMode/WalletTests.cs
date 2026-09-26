using System.Collections.Generic;
using System.Linq;
using GuildMaster.Core;
using GuildMaster.Data;
using NUnit.Framework;

namespace GuildMaster.Tests
{
    /// <summary>Кошелёк: суточные расходы, таверна, пустой кошелёк, доходы, долг (ТЗ 05 → «Кошелёк»).</summary>
    public sealed class WalletTests
    {
        private StateWorld world;

        [SetUp]
        public void SetUp() => world = new StateWorld();

        [TearDown]
        public void TearDown() => world.Dispose();

        [Test]
        public void DailyExpenses_FoodAndCityHousing_TavernMealIfWalletCoversWeek()
        {
            ExpensesBalance expenses = world.Balance.Expenses;
            Adventurer rich = world.Add();
            Adventurer modest = world.Add();
            rich.State.Wallet = 100;
            modest.State.Wallet = 34;
            Assert.AreEqual(35, WalletService.WeeklyExpenses(modest, expenses), "7 × (еда 2 + город 3)");

            world.TickToHour(0);

            Assert.AreEqual(100 - 3 - 3, rich.State.Wallet, "ел в таверне: еда 3, жильё 3");
            Assert.AreEqual(34 - 2 - 3, modest.State.Wallet, "меньше недели — еда 2");
            Assert.IsFalse(rich.State.AteInTavernToday, "учёт суток сброшен");
        }

        [Test]
        public void Drinks_WhenStressAbove30_OrDrunkard_TwoToFiveAnEvening()
        {
            world.Data.Set("traits.drunkardSkipChance", 0f);
            world.Data.Set("traits.drunkardSkipChanceStressed", 0f);
            Adventurer calm = world.Add();
            Adventurer stressed = world.Add();
            Adventurer drunkard = world.Add("Drunkard");
            var spent = new Dictionary<Adventurer, List<int>> { [calm] = new List<int>(), [stressed] = new List<int>(), [drunkard] = new List<int>() };

            for (int day = 0; day < 200; day++)
            {
                world.TickToHour(world.Balance.Time.EveningHour - 1);
                foreach (Adventurer adventurer in spent.Keys)
                {
                    adventurer.State.Stress = adventurer == stressed ? 31f : 10f;
                    adventurer.State.Wallet = 1000;
                }
                world.TickToHour(world.Balance.Time.NightHour);
                foreach (Adventurer adventurer in spent.Keys) spent[adventurer].Add(1000 - adventurer.State.Wallet);
            }

            Assert.IsTrue(spent[calm].All(x => x == 0));
            CollectionAssert.AreEquivalent(new[] { 2, 3, 4, 5 }, spent[stressed].Distinct());
            CollectionAssert.AreEquivalent(new[] { 2, 3, 4, 5 }, spent[drunkard].Distinct());
        }

        [Test]
        public void NotEnoughMoney_PaysWhatItCan_FlagsEmptyWallet_ThenIncomeClearsIt()
        {
            Adventurer adventurer = world.Add();
            adventurer.State.Wallet = 3;

            List<SimEvent> events = world.Collect(() => world.TickToHour(0));
            Assert.AreEqual(0, adventurer.State.Wallet);
            Assert.IsTrue(adventurer.State.IsWalletEmpty);
            SimEvent emptied = events.Single(e => e.Type == SimEventType.WalletEmptied);
            Assert.AreEqual(EventImportance.Notable, emptied.Importance);
            Assert.IsTrue(emptied.TryGet("unpaid", out int unpaid));
            Assert.AreEqual(2, unpaid);

            world.Days(1);
            Assert.AreEqual(0, adventurer.State.Wallet, "не уходит в минус");

            world.Do(ctx => WalletService.ReceiveIncome(ctx, adventurer, 10, IncomeKind.Loot));
            Assert.IsFalse(adventurer.State.IsWalletEmpty);
            Assert.AreEqual(10, adventurer.State.Wallet);
        }

        [Test]
        public void LowWallet_DoublesMoneyMotive()
        {
            Adventurer adventurer = world.Add();
            adventurer.State.Wallet = 35;
            Assert.AreEqual(1f, WalletService.MoneyMotiveMultiplier(adventurer, world.Balance));
            adventurer.State.Wallet = 34;
            Assert.IsTrue(WalletService.IsBelowWeeklyExpenses(adventurer, world.Balance.Expenses));
            Assert.AreEqual(2f, WalletService.MoneyMotiveMultiplier(adventurer, world.Balance));
        }

        [Test]
        public void Income_FamilySendsThirtyPercentHome_RoundedDown()
        {
            Adventurer family = world.Add("Family");
            Adventurer single = world.Add();
            family.State.Wallet = 0;
            single.State.Wallet = 0;

            Assert.AreEqual(11, world.Do(ctx => WalletService.ReceiveIncome(ctx, family, 15, IncomeKind.Loot)), "15 − ⌊4,5⌋");
            Assert.AreEqual(70, world.Do(ctx => WalletService.ReceiveIncome(ctx, family, 100, IncomeKind.Reward)));
            Assert.AreEqual(15, world.Do(ctx => WalletService.ReceiveIncome(ctx, single, 15, IncomeKind.Loot)));
            Assert.AreEqual(81, family.State.Wallet);
        }

        [Test]
        public void Debt_RepaidWithTwentyPercentOfRewards_NotLoot_WrittenOffOnDeath()
        {
            Adventurer debtor = world.Add();
            debtor.State.Wallet = 0;
            world.Do(ctx => WalletService.TakeLoan(ctx, debtor, 30));
            Assert.AreEqual(30, debtor.State.Wallet);
            Assert.AreEqual(30, debtor.State.DebtToGuild);

            Assert.AreEqual(100, world.Do(ctx => WalletService.ReceiveIncome(ctx, debtor, 100, IncomeKind.Loot)), "с добычи долг не гасится");
            Assert.AreEqual(80, world.Do(ctx => WalletService.ReceiveIncome(ctx, debtor, 100, IncomeKind.Reward)));
            Assert.AreEqual(10, debtor.State.DebtToGuild);
            Assert.AreEqual(6, world.Do(ctx => WalletService.ReceiveIncome(ctx, debtor, 7, IncomeKind.Reward)), "⌊1,4⌋ = 1");
            Assert.AreEqual(9, debtor.State.DebtToGuild);
            Assert.AreEqual(91, world.Do(ctx => WalletService.ReceiveIncome(ctx, debtor, 100, IncomeKind.Reward)), "не больше долга");
            Assert.AreEqual(0, debtor.State.DebtToGuild);

            Adventurer family = world.Add("Family");
            world.Do(ctx => WalletService.TakeLoan(ctx, family, 50));
            Assert.AreEqual(50, world.Do(ctx => WalletService.ReceiveIncome(ctx, family, 100, IncomeKind.Reward)), "домой 30, долг 20 — от всей доли");

            Adventurer gone = world.Add();
            world.Do(ctx => WalletService.TakeLoan(ctx, gone, 40));
            world.Do(ctx => AdventurerLifecycle.Retire(ctx, gone, LeaveReason.Died));
            Assert.AreEqual(0, gone.State.DebtToGuild);
            Adventurer left = world.Add();
            world.Do(ctx => WalletService.TakeLoan(ctx, left, 40));
            world.Do(ctx => AdventurerLifecycle.Retire(ctx, left, LeaveReason.Left));
            Assert.AreEqual(40, left.State.DebtToGuild, "ушёл сам — долг остаётся");
        }
    }
}
