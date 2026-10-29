using System;
using System.Collections.Generic;
using UnityEngine;

namespace GuildMaster.Data
{
    /// <summary>
    /// Раздел <c>Crises</c>: траты, которые растут с гильдией, Арсенал, проекты поселения и кризисы по календарю — набег, налог
    /// лорда, зима, мор. Цены, сроки и содержание Арсенала и проектов — в их определениях построек. Месяцы — номера в году с 1;
    /// год начинается весной.
    /// </summary>
    [Serializable]
    public sealed class CrisesBalance
    {
        [Header("Содержание")]
        [Tooltip("Зал гильдии: + это в месяц за каждого человека в гильдии")]
        [SerializeField, Min(0)] private int upkeepPerAdventurer = 10;

        [Header("Арсенал")]
        [Tooltip("У людей на задании профиль × (1 + это × уровень Арсенала)")]
        [SerializeField, Range(0f, 1f)] private float armoryProfilePerLevel = 0.05f;

        [Header("Проекты поселения")]
        [Tooltip("Частокол: нападение в осаде — только если Безопасность ниже")]
        [SerializeField, Range(0f, 100f)] private float palisadeAttackBelow = 10f;

        [Tooltip("Частокол: ущерб и погибшие жители при нападении ×")]
        [SerializeField, Range(0f, 1f)] private float palisadeDamageMultiplier = 0.5f;

        [Tooltip("Дозорная вышка: суточный спад Безопасности − это")]
        [SerializeField, Min(0f)] private float watchtowerDecayReduction = 0.15f;

        [Tooltip("Колодец: население + это, когда готов")]
        [SerializeField, Min(0)] private int wellPopulation = 10;

        [Tooltip("Колодец: рост населения в спокойствии + это (доля в месяц)")]
        [SerializeField, Range(0f, 1f)] private float wellGrowth = 0.01f;

        [Tooltip("Дорога: население + это, когда готова")]
        [SerializeField, Min(0)] private int roadPopulation = 15;

        [Tooltip("Дорога: заказов в день + это")]
        [SerializeField, Min(0f)] private float roadOrdersPerDay = 0.5f;

        [Header("Кризисы")]
        [Tooltip("Предупреждение о кризисе — за столько дней")]
        [SerializeField, Min(0)] private int warningDays = 30;

        [Header("Набег (раз в год)")]
        [Tooltip("Месяц набега (заказ появляется 1-го числа)")]
        [SerializeField, Min(1)] private int raidMonth = 3;

        [Tooltip("Ранг заказа по году игры: 1-й год, 2-й, …; дальше — последний")]
        [SerializeField] private List<GuildRank> raidRankByYear = new List<GuildRank> { GuildRank.F, GuildRank.E, GuildRank.D, GuildRank.C };

        [Tooltip("Заказ на доске, дней")]
        [SerializeField, Min(1)] private int raidDays = 10;

        [Tooltip("Награда от поселения: это × номер ранга (G = 1 … C = 5)")]
        [SerializeField, Min(0)] private int raidReward = 100;

        [Tooltip("Отбит: Безопасность + это")]
        [SerializeField, Min(0f)] private float raidWonSecurity = 10f;

        [Tooltip("Проигран: Безопасность − это, затем нападение")]
        [SerializeField, Min(0f)] private float raidLostSecurity = 30f;

        [Tooltip("Проигран: население − эта доля")]
        [SerializeField, Range(0f, 1f)] private float raidLostPopulationShare = 0.1f;

        [Header("Налог лорда")]
        [Tooltip("Налог — в конце этих месяцев")]
        [SerializeField] private List<int> taxMonths = new List<int> { 3, 6, 9, 12 };

        [Tooltip("Налог: это + доля доходов гильдии за прошедший квартал")]
        [SerializeField, Min(0)] private int taxBase = 200;

        [SerializeField, Range(0f, 1f)] private float taxShareOfIncome = 0.1f;

        [Tooltip("Казна ушла в минус от налога: репутация − это")]
        [SerializeField, Min(0f)] private float taxUnpaidReputation = 10f;

        [Tooltip("Казна ушла в минус от налога: Безопасность − это")]
        [SerializeField, Min(0f)] private float taxUnpaidSecurity = 10f;

        [Header("Зима")]
        [Tooltip("Зимние месяцы (идут подряд)")]
        [SerializeField] private List<int> winterMonths = new List<int> { 10, 11, 12 };

        [Tooltip("Еда и пайки зимой: цена × это, до целого от нуля")]
        [SerializeField, Min(1f)] private float winterFoodPriceMultiplier = 1.5f;

        [Tooltip("Жильё в городе зимой: + это в сутки (дрова)")]
        [SerializeField, Min(0f)] private float winterCityHousing = 1f;

        [Tooltip("Заказов в день зимой ×")]
        [SerializeField, Range(0f, 1f)] private float winterOrdersMultiplier = 0.7f;

        [Tooltip("Суточный спад Безопасности зимой ×")]
        [SerializeField, Min(1f)] private float winterDecayMultiplier = 1.5f;

        [Header("Мор (случайно)")]
        [Tooltip("Шанс мора в году (бросок в начале года, месяц — тоже бросок)")]
        [SerializeField, Range(0f, 1f)] private float plagueChance = 0.25f;

        [Tooltip("Мор длится, дней")]
        [SerializeField, Min(1)] private int plagueDays = 30;

        [Tooltip("Население − это каждые 7 дней мора")]
        [SerializeField, Min(0)] private int plaguePopulationPerWeek = 3;

        [Tooltip("То же, если Лазарет готов и Лекарь нанят")]
        [SerializeField, Min(0)] private int plagueWithHealerPerWeek = 1;

        [Tooltip("Доход таверны от жителей во время мора ×")]
        [SerializeField, Range(0f, 1f)] private float plagueTavernMultiplier = 0.5f;

        public int UpkeepPerAdventurer => upkeepPerAdventurer;
        public float ArmoryProfilePerLevel => armoryProfilePerLevel;
        public float PalisadeAttackBelow => palisadeAttackBelow;
        public float PalisadeDamageMultiplier => palisadeDamageMultiplier;
        public float WatchtowerDecayReduction => watchtowerDecayReduction;
        public int WellPopulation => wellPopulation;
        public float WellGrowth => wellGrowth;
        public int RoadPopulation => roadPopulation;
        public float RoadOrdersPerDay => roadOrdersPerDay;
        public int WarningDays => warningDays;
        public int RaidMonth => raidMonth;
        public IReadOnlyList<GuildRank> RaidRankByYear => raidRankByYear;
        public int RaidDays => raidDays;
        public int RaidReward => raidReward;
        public float RaidWonSecurity => raidWonSecurity;
        public float RaidLostSecurity => raidLostSecurity;
        public float RaidLostPopulationShare => raidLostPopulationShare;
        public IReadOnlyList<int> TaxMonths => taxMonths;
        public int TaxBase => taxBase;
        public float TaxShareOfIncome => taxShareOfIncome;
        public float TaxUnpaidReputation => taxUnpaidReputation;
        public float TaxUnpaidSecurity => taxUnpaidSecurity;
        public IReadOnlyList<int> WinterMonths => winterMonths;
        public float WinterFoodPriceMultiplier => winterFoodPriceMultiplier;
        public float WinterCityHousing => winterCityHousing;
        public float WinterOrdersMultiplier => winterOrdersMultiplier;
        public float WinterDecayMultiplier => winterDecayMultiplier;
        public float PlagueChance => plagueChance;
        public int PlagueDays => plagueDays;
        public int PlaguePopulationPerWeek => plaguePopulationPerWeek;
        public int PlagueWithHealerPerWeek => plagueWithHealerPerWeek;
        public float PlagueTavernMultiplier => plagueTavernMultiplier;

        /// <summary>Ранг заказа набега в году <paramref name="year"/> (с 1); после списка — последний.</summary>
        public GuildRank RaidRankFor(int year) =>
            raidRankByYear.Count == 0 ? GuildRank.F : raidRankByYear[Math.Max(0, Math.Min(year, raidRankByYear.Count) - 1)];
    }
}
