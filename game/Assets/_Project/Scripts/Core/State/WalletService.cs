using System;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>Откуда деньги в кошельке: от вида зависит, что уходит в счёт долга.</summary>
    public enum IncomeKind
    {
        /// <summary>Доля награды за задание: из неё гасится долг.</summary>
        Reward,

        /// <summary>Доля добычи.</summary>
        Loot,

        /// <summary>Компенсация гильдии за рану.</summary>
        Compensation,
    }

    /// <summary>
    /// Личные деньги. Кошелёк целочисленный: доли (долг) округляются вниз,
    /// цены из <see cref="ExpensesBalance"/> — до целого. Не хватает — платит сколько может, остаток не списывается,
    /// ставится флаг «кошелёк пуст». Сам кошелёк зачисляет в казну погашение долга и плату за Общежитие; долю таверны с еды
    /// и выпивки, плату за Лазарет и тренировку засчитывают системы, которые за них берут плату (<see cref="TreasuryService"/>).
    /// Еду человек покупает сам в часы приёмов (<see cref="FoodService"/>), семье платит раз в неделю (<see cref="FamilyService"/>).
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
            if (paid < amount) MarkUnpaid(ctx, adventurer, amount - paid);
            return paid;
        }

        /// <summary>Расход не оплачен на <paramref name="unpaid"/>: флаг «кошелёк пуст» (событие — при первом).</summary>
        internal static void MarkUnpaid(SimContext ctx, Adventurer adventurer, int unpaid)
        {
            AdventurerState state = adventurer.State;
            if (state.IsWalletEmpty) return;
            state.IsWalletEmpty = true;
            ctx.Events.Publish(SimEventType.WalletEmptied, EventImportance.Notable, adventurer.Id).With("unpaid", unpaid);
        }

        /// <summary>
        /// Доход (<see cref="IncomeKind"/>): из доли награды гасится долг гильдии — <c>debtRepaymentShare</c> (20%), не больше долга; погашенное зачисляется
        /// в казну (<see cref="LedgerCategories.DebtRepayment"/>, связанный объект — человек). Остальное — в кошелёк,
        /// флаг «кошелёк пуст» снимается. Возвращает, сколько попало в кошелёк.
        /// </summary>
        public static int ReceiveIncome(SimContext ctx, Adventurer adventurer, int amount, IncomeKind kind)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));

            AdventurerState state = adventurer.State;
            int debt = kind == IncomeKind.Reward ? Math.Min(state.DebtToGuild, Share(amount, ctx.Data.Balance.State.DebtRepaymentShare)) : 0;

            int kept = amount - debt;
            state.DebtToGuild -= debt;
            TreasuryService.Credit(ctx, LedgerCategories.DebtRepayment, debt, "debt", adventurer.Id);
            state.Wallet += kept;
            if (state.Wallet > 0) state.IsWalletEmpty = false;
            return kept;
        }

        /// <summary>Гильдия дала в долг (дилемма «Просьба в долг»): кошелёк и долг растут на сумму.</summary>
        public static void TakeLoan(SimContext ctx, Adventurer adventurer, int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));

            AdventurerState state = adventurer.State;
            state.Wallet += amount;
            state.DebtToGuild += amount;
            if (state.Wallet > 0) state.IsWalletEmpty = false;
        }

        /// <summary>Деньги семье: из кошелька до <paramref name="amount"/>, уходят из мира. Возвращает, сколько отправлено.</summary>
        internal static int SendHome(Adventurer adventurer, int amount) => Take(adventurer, amount);

        /// <summary>Гильдия дала денег без долга (оплатила расходы): всё в кошелёк.</summary>
        internal static void Give(Adventurer adventurer, int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            adventurer.State.Wallet += amount;
            if (adventurer.State.Wallet > 0) adventurer.State.IsWalletEmpty = false;
        }

        /// <summary>Забрать из кошелька до <paramref name="amount"/> (штраф, возврат утаенного); возвращает, сколько забрано.</summary>
        internal static int Take(Adventurer adventurer, int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            int taken = Math.Min(adventurer.State.Wallet, amount);
            adventurer.State.Wallet -= taken;
            return taken;
        }

        /// <summary>Доля монет, округление вниз.</summary>
        public static int Share(int amount, float share) => (int)Math.Floor(amount * (double)share + 1e-6);

        /// <summary>Цена из баланса в целых монетах.</summary>
        public static int Coins(float price) => (int)Math.Round(price, MidpointRounding.AwayFromZero);

        /// <summary>
        /// Расходы на жизнь за сутки — для займов, обращений и порога мотива «Деньги»: жильё (город или Общежитие) и приёмы
        /// пищи в день × цена простой еды.
        /// </summary>
        public static int DailyLivingCost(Adventurer adventurer, BalanceSettings balance) =>
            FoodRules.HousingCost(adventurer, balance.Expenses) + balance.Food.MealsPerDay * balance.Food.Simple.Price;

        /// <summary>«Расходы на неделю»: <c>reserveDays</c> × расходы на день. Кошелёк меньше — мотив «Деньги» сильнее.</summary>
        public static int WeeklyExpenses(Adventurer adventurer, BalanceSettings balance) =>
            balance.Expenses.ReserveDays * DailyLivingCost(adventurer, balance);

        /// <summary>Кошелёк меньше расходов на неделю — мотив «Деньги» × <c>lowWalletMoneyMultiplier</c>.</summary>
        public static bool IsBelowWeeklyExpenses(Adventurer adventurer, BalanceSettings balance) =>
            adventurer.State.Wallet < WeeklyExpenses(adventurer, balance);

        /// <summary>Множитель мотива «Деньги» от кошелька: 2 или 1.</summary>
        public static float MoneyMotiveMultiplier(Adventurer adventurer, BalanceSettings balance) =>
            IsBelowWeeklyExpenses(adventurer, balance) ? balance.State.LowWalletMoneyMultiplier : 1f;

        /// <summary>
        /// Жильё за сутки (00:00; зимой в городе дороже). Жильцы Общежития платят гильдии — плата в казну (<see cref="LedgerCategories.Dormitory"/>).
        /// Возвращает, сколько заплачено.
        /// </summary>
        internal static int PayDaily(SimContext ctx, Adventurer adventurer)
        {
            int paid = Pay(ctx, adventurer, FoodRules.HousingCost(adventurer, ctx.Data.Balance, CrisisRules.IsWinter(ctx.World)));
            if (adventurer.Housing == Housing.Dorm) TreasuryService.Credit(ctx, LedgerCategories.Dormitory, paid, "dormitory", adventurer.Id);
            return paid;
        }
    }
}
