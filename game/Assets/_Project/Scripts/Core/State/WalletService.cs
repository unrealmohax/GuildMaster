using System;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>Откуда деньги в кошельке: от вида зависит, что уходит домой и в счёт долга.</summary>
    public enum IncomeKind
    {
        /// <summary>Доля награды за задание (ТЗ 09): Семейный отсылает часть домой, из неё гасится долг.</summary>
        Reward,

        /// <summary>Доля добычи (ТЗ 09): Семейный отсылает часть домой.</summary>
        Loot,
    }

    /// <summary>
    /// Личные деньги (ТЗ 05 → «Кошелёк»). Кошелёк целочисленный: доли (Семейный, долг) округляются вниз,
    /// цены из <see cref="ExpensesBalance"/> — до целого. Не хватает — платит сколько может, остаток не списывается,
    /// ставится флаг «кошелёк пуст». До ТЗ 10 платы гильдии (Общежитие, двор, Лазарет, таверна) в казну не зачисляются —
    /// только списываются из кошелька (решение 2026-09-26).
    /// </summary>
    public static class WalletService
    {
        /// <summary>Заплатить <paramref name="amount"/>; возвращает, сколько заплачено. Недостача — флаг «кошелёк пуст».</summary>
        public static int Pay(SimContext ctx, Adventurer adventurer, int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));

            AdventurerState state = adventurer.State;
            int paid = Math.Min(state.Wallet, amount);
            state.Wallet -= paid;
            if (paid < amount && !state.IsWalletEmpty)
            {
                state.IsWalletEmpty = true;
                ctx.Events.Publish(SimEventType.WalletEmptied, EventImportance.Notable, adventurer.Id)
                    .With("unpaid", amount - paid);
            }
            // ТЗ 10: платы гильдии — в казну.
            return paid;
        }

        /// <summary>
        /// Доход (<see cref="IncomeKind"/>): Семейный отсылает домой <c>familySendHomeShare</c> (30%) каждого дохода,
        /// из доли награды гасится долг гильдии — <c>debtRepaymentShare</c> (20%), не больше долга. Остальное — в кошелёк,
        /// флаг «кошелёк пуст» снимается. Возвращает, сколько попало в кошелёк.
        /// </summary>
        public static int ReceiveIncome(SimContext ctx, Adventurer adventurer, int amount, IncomeKind kind)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));

            AdventurerState state = adventurer.State;
            int home = TraitRules.FindWithHook(adventurer, TraitHook.FamilySendsMoneyHome, ctx.Data) != null
                ? Share(amount, ctx.Data.Balance.Traits.FamilySendHomeShare)
                : 0;
            int debt = kind == IncomeKind.Reward ? Math.Min(state.DebtToGuild, Share(amount, ctx.Data.Balance.State.DebtRepaymentShare)) : 0;

            int kept = amount - home - debt;
            state.DebtToGuild -= debt;   // ТЗ 10: погашение долга — в казну
            state.Wallet += kept;
            if (state.Wallet > 0) state.IsWalletEmpty = false;
            return kept;
        }

        /// <summary>Гильдия дала в долг (дилемма «Просьба в долг», ТЗ 13): кошелёк и долг растут на сумму.</summary>
        public static void TakeLoan(SimContext ctx, Adventurer adventurer, int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));

            AdventurerState state = adventurer.State;
            state.Wallet += amount;
            state.DebtToGuild += amount;
            if (state.Wallet > 0) state.IsWalletEmpty = false;
        }

        /// <summary>Доля монет, округление вниз.</summary>
        public static int Share(int amount, float share) => (int)Math.Floor(amount * (double)share + 1e-6);

        /// <summary>Цена из баланса в целых монетах.</summary>
        public static int Coins(float price) => (int)Math.Round(price, MidpointRounding.AwayFromZero);

        /// <summary>Суточные расходы на жизнь: еда (в таверне — дороже) и жильё (город или Общежитие).</summary>
        public static int DailyLivingCost(Adventurer adventurer, bool ateInTavern, ExpensesBalance expenses) =>
            Coins(ateInTavern ? expenses.TavernFood : expenses.Food)
            + Coins(adventurer.Housing == Housing.Dorm ? expenses.DormHousing : expenses.CityHousing);

        /// <summary>
        /// ❔ «Расходы на неделю»: <c>reserveDays</c> × (обычная еда + жильё). От неё — еда в таверне (кошелёк не меньше)
        /// и мотив «Деньги» (кошелёк меньше).
        /// </summary>
        public static int WeeklyExpenses(Adventurer adventurer, ExpensesBalance expenses) =>
            expenses.ReserveDays * DailyLivingCost(adventurer, ateInTavern: false, expenses);

        /// <summary>Кошелёк меньше расходов на неделю — мотив «Деньги» × <c>lowWalletMoneyMultiplier</c> (ТЗ 06).</summary>
        public static bool IsBelowWeeklyExpenses(Adventurer adventurer, ExpensesBalance expenses) =>
            adventurer.State.Wallet < WeeklyExpenses(adventurer, expenses);

        /// <summary>Множитель мотива «Деньги» от кошелька (ТЗ 06 → «Состояние»): 2 или 1.</summary>
        public static float MoneyMotiveMultiplier(Adventurer adventurer, BalanceSettings balance) =>
            IsBelowWeeklyExpenses(adventurer, balance.Expenses) ? balance.State.LowWalletMoneyMultiplier : 1f;

        /// <summary>Суточные расходы (00:00): еда за прошедшие сутки и жильё.</summary>
        internal static void PayDaily(SimContext ctx, Adventurer adventurer) =>
            Pay(ctx, adventurer, DailyLivingCost(adventurer, adventurer.State.AteInTavernToday, ctx.Data.Balance.Expenses));
    }
}
