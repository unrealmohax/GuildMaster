using System;
using UnityEngine;

namespace GuildMaster.Data
{
    /// <summary>Один тип еды в таверне: цена приёма, сытость, вкус и доля гильдии с порции.</summary>
    [Serializable]
    public sealed class MealBalance
    {
        [Tooltip("Цена приёма, монет")]
        [SerializeField, Min(0)] private int price;

        [Tooltip("Сколько сытости прибавляет приём")]
        [SerializeField, Range(0f, 100f)] private float satietyGain;

        [Tooltip("Выше этого значения тип сытость не поднимает")]
        [SerializeField, Range(0f, 100f)] private float satietyCap;

        [Tooltip("Вкус: вклад приёма в довольство дня (среднее трёх приёмов)")]
        [SerializeField] private float taste;

        [Tooltip("Доля гильдии с порции — доход таверны, монет")]
        [SerializeField, Min(0f)] private float guildShare;

        public MealBalance()
        {
        }

        public MealBalance(int price, float satietyGain, float satietyCap, float taste, float guildShare)
        {
            this.price = price;
            this.satietyGain = satietyGain;
            this.satietyCap = satietyCap;
            this.taste = taste;
            this.guildShare = guildShare;
        }

        public int Price => price;
        public float SatietyGain => satietyGain;
        public float SatietyCap => satietyCap;
        public float Taste => taste;
        public float GuildShare => guildShare;
    }

    /// <summary>
    /// Раздел <c>Food</c>: сытость, приёмы пищи, типы еды, бюджет на еду, голод и истощение.
    /// </summary>
    [Serializable]
    public sealed class FoodBalance
    {
        [Header("Сытость")]
        [Tooltip("Сытость нового человека")]
        [SerializeField, Range(0f, 100f)] private float startSatiety = 100f;

        [Tooltip("Убывание сытости в час (на задании не убывает)")]
        [SerializeField, Min(0f)] private float satietyDecayPerHour = 2f;

        [Tooltip("Часы приёмов пищи, по возрастанию")]
        [SerializeField] private int[] mealHours = { 7, 13, 19 };

        [Header("Типы еды")]
        [SerializeField] private MealBalance cheap = new MealBalance(1, 25f, 60f, -5f, 0.2f);
        [SerializeField] private MealBalance simple = new MealBalance(2, 35f, 85f, 3f, 0.5f);
        [SerializeField] private MealBalance luxury = new MealBalance(5, 50f, 100f, 10f, 1.5f);

        [Header("Бюджет на еду")]
        [Tooltip("Резерв: столько дней жилья и дешёвой еды человек не тратит на еду получше")]
        [SerializeField, Min(0)] private int reserveDays = 7;

        [Tooltip("Тип по карману: остатка над резервом хватает на столько дней такого питания")]
        [SerializeField, Min(0f)] private float budgetDays = 10f;

        [Tooltip("Жадный: дни бюджета × (1 + это × сила полюса)")]
        [SerializeField, Min(0f)] private float greedyBudgetBonus = 1f;

        [Tooltip("Семейный: дни бюджета ×")]
        [SerializeField, Min(1f)] private float familyBudgetMultiplier = 1.5f;

        [Header("Голод")]
        [Tooltip("Ниже — голоден: растёт стресс, мотивы «Деньги» и «Безопасность» меняются")]
        [SerializeField, Range(0f, 100f)] private float hungryThreshold = 30f;

        [Tooltip("Ниже — голод: штраф характеристик")]
        [SerializeField, Range(0f, 100f)] private float starvingThreshold = 10f;

        [Tooltip("Голоден: стресс в час")]
        [SerializeField, Min(0f)] private float hungryStressPerHour = 0.5f;

        [Tooltip("Голод: характеристики ×")]
        [SerializeField, Range(0f, 1f)] private float hungerStatMultiplier = 0.9f;

        [Tooltip("Истощение (сытость 0): характеристики × (вместо голода)")]
        [SerializeField, Range(0f, 1f)] private float exhaustionStatMultiplier = 0.75f;

        [Tooltip("Истощение: довольство в сутки (00:00), разово")]
        [SerializeField, Min(0f)] private float exhaustionContentmentPerDay = 10f;

        [Tooltip("Голоден: мотив «Деньги» ×")]
        [SerializeField, Min(0f)] private float hungerMoneyMultiplier = 1.5f;

        [Tooltip("Голоден: мотив «Безопасность» ×")]
        [SerializeField, Min(0f)] private float hungerSafetyMultiplier = 0.7f;

        public float StartSatiety => startSatiety;
        public float SatietyDecayPerHour => satietyDecayPerHour;
        public int[] MealHours => mealHours;
        public int MealsPerDay => mealHours.Length;
        public MealBalance Cheap => cheap;
        public MealBalance Simple => simple;
        public MealBalance Luxury => luxury;
        public int ReserveDays => reserveDays;
        public float BudgetDays => budgetDays;
        public float GreedyBudgetBonus => greedyBudgetBonus;
        public float FamilyBudgetMultiplier => familyBudgetMultiplier;
        public float HungryThreshold => hungryThreshold;
        public float StarvingThreshold => starvingThreshold;
        public float HungryStressPerHour => hungryStressPerHour;
        public float HungerStatMultiplier => hungerStatMultiplier;
        public float ExhaustionStatMultiplier => exhaustionStatMultiplier;
        public float ExhaustionContentmentPerDay => exhaustionContentmentPerDay;
        public float HungerMoneyMultiplier => hungerMoneyMultiplier;
        public float HungerSafetyMultiplier => hungerSafetyMultiplier;

        /// <summary>Числа типа еды.</summary>
        public MealBalance Meal(MealTier tier)
        {
            switch (tier)
            {
                case MealTier.Cheap: return cheap;
                case MealTier.Simple: return simple;
                case MealTier.Luxury: return luxury;
                default: throw new ArgumentOutOfRangeException(nameof(tier), tier, null);
            }
        }

        /// <summary>Час приёма пищи.</summary>
        public bool IsMealHour(int hour) => Array.IndexOf(mealHours, hour) >= 0;
    }
}
