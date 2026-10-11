using System;
using System.Globalization;
using System.Text;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Единственный путь изменить деньги гильдии: зачислить доход или списать расход по статье журнала.
    /// Каждая операция — запись журнала и строка лога (<see cref="SimLogLevel.Debug"/>). Здесь же отмечается,
    /// с какого часа казна в минусе (для банкротства), и копятся порции таверны до суточного расчёта.
    /// </summary>
    public static class TreasuryService
    {
        /// <summary>Зачислить доход. Нулевая сумма записи не даёт.</summary>
        public static void Credit(SimContext ctx, LedgerCategory category, int amount, string comment = null, int relatedId = 0)
        {
            if (category == null) throw new ArgumentNullException(nameof(category));
            if (category.Flow != LedgerFlow.Income) throw new ArgumentException($"Category {category.Id} is not income", nameof(category));
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (amount == 0) return;

            Apply(ctx, category, amount, comment, relatedId);
        }

        /// <summary>
        /// Списать расход. Обязательный расход списывается всегда, даже в минус; зависящий от денег — только если в казне
        /// не меньше суммы. Возвращает, списано ли. Нулевая сумма — true без записи.
        /// </summary>
        public static bool Debit(SimContext ctx, LedgerCategory category, int amount, string comment = null, int relatedId = 0)
        {
            if (category == null) throw new ArgumentNullException(nameof(category));
            if (category.Flow != LedgerFlow.Expense) throw new ArgumentException($"Category {category.Id} is not an expense", nameof(category));
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (amount == 0) return true;

            if (category.ExpenseKind == ExpenseKind.IfAffordable && ctx.World.Treasury.Money < amount)
            {
                ctx.Log.Write(SimLogLevel.Debug, "ledger {0} -{1} skipped: money={2}", category.Id, amount, ctx.World.Treasury.Money);
                return false;
            }

            Apply(ctx, category, -amount, comment, relatedId);
            return true;
        }

        /// <summary>Хватает ли денег на расход, зависящий от денег.</summary>
        public static bool CanAfford(WorldState world, int amount) => world.Treasury.Money >= amount;

        /// <summary>Человек поел в таверне: порция этого типа засчитывается в доход таверны при суточном расчёте.</summary>
        internal static void CountTavernMeal(SimContext ctx, MealTier tier) => ctx.World.Treasury.TavernMeals[(int)tier]++;

        /// <summary>Человек выпил в таверне (и в запое): порция засчитывается в доход таверны при суточном расчёте.</summary>
        internal static void CountTavernDrink(SimContext ctx) => ctx.World.Treasury.TavernDrinks++;

        /// <summary>
        /// Доход таверны за прошедшие сутки (× Сухой закон): еда — порции каждого типа × доля гильдии с порции этого типа,
        /// выпивка — порции × <c>tavernIncomePerServing</c>. Еда и выпивка — отдельные записи журнала; зачисляются целые монеты,
        /// дробный остаток переносится на следующие сутки.
        /// </summary>
        internal static void SettleTavern(SimContext ctx)
        {
            Treasury treasury = ctx.World.Treasury;
            double multiplier = DecreeRules.TavernIncomeMultiplier(ctx.World, ctx.Data);
            FoodBalance food = ctx.Data.Balance.Food;

            int[] meals = treasury.TavernMeals;
            int mealCount = 0;
            double foodIncome = 0;
            for (int i = 0; i < meals.Length; i++)
            {
                mealCount += meals[i];
                foodIncome += meals[i] * (double)food.Meal((MealTier)i).GuildShare;
            }
            if (mealCount > 0)
            {
                string comment = string.Format(CultureInfo.InvariantCulture, "cheap={0} simple={1} luxury={2}",
                    meals[(int)MealTier.Cheap], meals[(int)MealTier.Simple], meals[(int)MealTier.Luxury]);
                treasury.TavernFoodRemainder = Settle(ctx, LedgerCategories.TavernFood, treasury.TavernFoodRemainder + foodIncome * multiplier, comment);
                Array.Clear(meals, 0, meals.Length);
            }

            int drinks = treasury.TavernDrinks;
            if (drinks > 0)
            {
                double drinkIncome = drinks * (double)ctx.Data.Balance.Economy.TavernIncomePerServing * multiplier;
                string comment = string.Format(CultureInfo.InvariantCulture, "drinks={0}", drinks);
                treasury.TavernDrinkRemainder = Settle(ctx, LedgerCategories.TavernDrinks, treasury.TavernDrinkRemainder + drinkIncome, comment);
                treasury.TavernDrinks = 0;
            }
        }

        /// <summary>Зачислить целые монеты суммы, вернуть дробный остаток.</summary>
        private static double Settle(SimContext ctx, LedgerCategory category, double total, string comment)
        {
            int coins = (int)Math.Floor(total + 1e-9);
            Credit(ctx, category, coins, comment);
            return Math.Max(0, total - coins);
        }

        private static void Apply(SimContext ctx, LedgerCategory category, int amount, string comment, int relatedId)
        {
            Treasury treasury = ctx.World.Treasury;
            long now = ctx.World.Time.TotalHours;
            treasury.Money += amount;
            treasury.Add(new LedgerEntry(now, category, amount, comment, relatedId, treasury.Money));

            Bankruptcy bankruptcy = treasury.Bankruptcy;
            if (treasury.Money < 0 && !bankruptcy.NegativeSinceHours.HasValue) bankruptcy.NegativeSinceHours = now;
            else if (treasury.Money >= 0) bankruptcy.NegativeSinceHours = null;

            if (ctx.Log.IsOn(SimLogLevel.Debug))
            {
                StringBuilder line = ctx.Log.Begin(SimLogLevel.Debug);
                line.Append("ledger ").Append(category.Id).Append(' ').Append(amount.ToString("+0;-0", CultureInfo.InvariantCulture))
                    .Append(" money=").Append(treasury.Money.ToString(CultureInfo.InvariantCulture));
                if (relatedId != 0) line.Append(" related=").Append(relatedId.ToString(CultureInfo.InvariantCulture));
                if (!string.IsNullOrEmpty(comment)) line.Append(' ').Append(comment);
                ctx.Log.Commit();
            }
        }
    }
}
