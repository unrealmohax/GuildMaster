using System;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>Стадия голода по сытости. Порядок — от сытого к истощению.</summary>
    public enum HungerStage
    {
        /// <summary>Сытость не ниже порога голода.</summary>
        Fed,

        /// <summary>Голоден: растёт стресс, мотив «Деньги» сильнее, «Безопасность» слабее.</summary>
        Hungry,

        /// <summary>Голод: характеристики ниже.</summary>
        Starving,

        /// <summary>Истощение (сытость 0): характеристики ещё ниже, довольство падает каждые сутки.</summary>
        Exhausted,
    }

    /// <summary>
    /// Чистые правила еды: стадии голода, приём пищи, бюджет на еду и выбор типа, штраф характеристик, расходы на день.
    /// Мир не меняют, случайных чисел не тратят. Числа — <see cref="FoodBalance"/>.
    /// <para>
    /// Выбор типа — правило, а не решение: резерв = <c>reserveDays</c> × (жильё + приёмы в день × цена дешёвой); с остатка
    /// кошелька над резервом человек берёт лучший тип, на который остатка хватит на <c>budgetDays</c> дней такого питания;
    /// не хватает ни на какой — дешёвую, если она есть в кошельке, иначе пропускает приём. Жадный (по силе полюса) и
    /// Семейный экономят: им нужно больше дней запаса.
    /// </para>
    /// </summary>
    public static class FoodRules
    {
        public static HungerStage StageOf(float satiety, FoodBalance food)
        {
            if (satiety <= 0f) return HungerStage.Exhausted;
            if (satiety < food.StarvingThreshold) return HungerStage.Starving;
            if (satiety < food.HungryThreshold) return HungerStage.Hungry;
            return HungerStage.Fed;
        }

        public static HungerStage StageOf(Adventurer adventurer, FoodBalance food) => StageOf(adventurer.State.Satiety, food);

        /// <summary>Голоден или хуже: сытость ниже порога голода.</summary>
        public static bool IsHungry(AdventurerState state, FoodBalance food) => state.Satiety < food.HungryThreshold;

        /// <summary>Сытость после приёма: прибавка до потолка типа; если человек уже сытее потолка — не меняется.</summary>
        public static float AfterMeal(float satiety, MealBalance meal) =>
            Math.Max(satiety, Math.Min(meal.SatietyCap, satiety + meal.SatietyGain));

        /// <summary>Жильё за сутки: город или Общежитие, в целых монетах (обычная цена, без зимы).</summary>
        public static int HousingCost(Adventurer adventurer, ExpensesBalance expenses) =>
            WalletService.Coins(adventurer.Housing == Housing.Dorm ? expenses.DormHousing : expenses.CityHousing);

        /// <summary>Жильё за сутки: город или Общежитие; зимой жильё в городе + <c>winterCityHousing</c> (дрова). В целых монетах.</summary>
        public static int HousingCost(Adventurer adventurer, BalanceSettings balance, bool winter)
        {
            ExpensesBalance expenses = balance.Expenses;
            if (adventurer.Housing == Housing.Dorm) return WalletService.Coins(expenses.DormHousing);
            return WalletService.Coins(expenses.CityHousing + (winter ? balance.Crises.WinterCityHousing : 0f));
        }

        /// <summary>Цена еды или пайка: зимой × <c>winterFoodPriceMultiplier</c>, до целого от нуля (1 → 2, 2 → 3, 5 → 8).</summary>
        public static int Price(int basePrice, bool winter, CrisesBalance crises) =>
            winter ? (int)Math.Round(basePrice * crises.WinterFoodPriceMultiplier, MidpointRounding.AwayFromZero) : basePrice;

        /// <summary>Цена приёма пищи этого типа с учётом зимы.</summary>
        public static int MealPrice(MealTier tier, BalanceSettings balance, bool winter) => Price(balance.Food.Meal(tier).Price, winter, balance.Crises);

        /// <summary>Цена пайка этого типа с учётом зимы.</summary>
        public static int RationPrice(RationTier tier, BalanceSettings balance, bool winter) => Price(balance.Food.Ration(tier).Price, winter, balance.Crises);

        /// <summary>
        /// Резерв, который человек не тратит на еду получше: <c>reserveDays</c> × (жильё + приёмы в день × дешёвая); зимой — по зимним
        /// ценам.
        /// </summary>
        public static int Reserve(Adventurer adventurer, BalanceSettings balance, bool winter = false)
        {
            FoodBalance food = balance.Food;
            return food.ReserveDays * (HousingCost(adventurer, balance, winter) + food.MealsPerDay * MealPrice(MealTier.Cheap, balance, winter));
        }

        /// <summary>
        /// Сколько дней такого питания должно хватать остатка: <c>budgetDays</c>; Жадный — × (1 + <c>greedyBudgetBonus</c> × сила
        /// полюса), Семейный — × <c>familyBudgetMultiplier</c>.
        /// </summary>
        public static float BudgetDays(Adventurer adventurer, DataRegistry data)
        {
            FoodBalance food = data.Balance.Food;
            float days = food.BudgetDays;
            days *= 1f + food.GreedyBudgetBonus * AxisMath.Strength(adventurer.GetAxis(AxisId.Money), AxisPole.Positive);
            if (TraitRules.FindWithHook(adventurer, TraitHook.FamilySavesOnFood, data) != null) days *= food.FamilyBudgetMultiplier;
            return days;
        }

        /// <summary>
        /// Какой тип человек возьмёт в этот приём по своему кошельку; <c>null</c> — нет денег даже на дешёвую, приём пропущен.
        /// <paramref name="spare"/> — остаток кошелька над резервом. Зимой бюджет считается по зимним ценам.
        /// </summary>
        public static MealTier? Choose(Adventurer adventurer, DataRegistry data, bool winter, out int spare)
        {
            FoodBalance food = data.Balance.Food;
            int wallet = adventurer.State.Wallet;
            spare = wallet - Reserve(adventurer, data.Balance, winter);
            float days = BudgetDays(adventurer, data);
            for (int i = Vocabulary.MealTierCount - 1; i > 0; i--)
            {
                var tier = (MealTier)i;
                if (spare >= food.MealsPerDay * MealPrice(tier, data.Balance, winter) * days) return tier;
            }
            return wallet >= MealPrice(MealTier.Cheap, data.Balance, winter) ? MealTier.Cheap : (MealTier?)null;
        }

        /// <summary>
        /// Штраф характеристик от голода: голод — × <c>hungerStatMultiplier</c>, истощение — × <c>exhaustionStatMultiplier</c>
        /// (вместо голода). Навыки не меняет.
        /// </summary>
        public static float StatMultiplier(Adventurer adventurer, StatId stat, FoodBalance food)
        {
            if (!Vocabulary.IsCharacteristic(stat)) return 1f;
            switch (StageOf(adventurer, food))
            {
                case HungerStage.Starving: return food.HungerStatMultiplier;
                case HungerStage.Exhausted: return food.ExhaustionStatMultiplier;
                default: return 1f;
            }
        }

        /// <summary>Вкус дня: среднее вкуса приёмов за сутки (пропущенный приём — 0), вклад в цель довольства.</summary>
        public static float DayTaste(AdventurerState state, FoodBalance food) =>
            food.MealsPerDay > 0 ? state.TasteToday / food.MealsPerDay : 0f;
    }
}
