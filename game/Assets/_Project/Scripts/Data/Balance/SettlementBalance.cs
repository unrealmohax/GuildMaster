using System;
using UnityEngine;

namespace GuildMaster.Data
{
    /// <summary>
    /// Раздел <c>Settlement</c>: поселение рядом с гильдией — Безопасность, уровни, население, нападения, покидание,
    /// доход таверны от жителей. Сдвиги «× ранг» — × номер ранга заказа (G = 1 … C = 5).
    /// </summary>
    [Serializable]
    public sealed class SettlementBalance
    {
        [Header("Старт")]
        [SerializeField, Range(0f, 100f)] private float startSecurity = 60f;
        [SerializeField, Min(1)] private int startPopulation = 80;

        [Tooltip("Безопасность не выше")]
        [SerializeField, Min(1f)] private float maxSecurity = 100f;

        [Header("Безопасность: сдвиги")]
        [Tooltip("Спад в 00:00 каждых суток")]
        [SerializeField, Min(0f)] private float dailyDecay = 0.3f;

        [Tooltip("Выполнен заказ-угроза: + это × ранг")]
        [SerializeField, Min(0f)] private float threatDonePerRank = 0.5f;

        [Tooltip("Выполнен другой заказ: + это × ранг")]
        [SerializeField, Min(0f)] private float otherDonePerRank = 0.2f;

        [Tooltip("Задание не выполнено (отступили, погибли все): − это × ранг")]
        [SerializeField, Min(0f)] private float failedPerRank = 0.5f;

        [Tooltip("Погибла вся группа: ещё − это")]
        [SerializeField, Min(0f)] private float partyLost = 5f;

        [Tooltip("Заказ-угроза снят по сроку или отклонён: − это × ранг")]
        [SerializeField, Min(0f)] private float threatMissedPerRank = 1f;

        [Header("Уровни: Безопасность от")]
        [SerializeField, Range(0f, 100f)] private float calmFrom = 70f;
        [SerializeField, Range(0f, 100f)] private float normalFrom = 40f;
        [SerializeField, Range(0f, 100f)] private float alarmFrom = 20f;

        [Header("Население")]
        [Tooltip("В начале месяца при уровне «спокойно»: × (1 + это)")]
        [SerializeField, Range(0f, 1f)] private float calmGrowth = 0.02f;

        [Tooltip("В начале месяца при уровне «тревога»: × (1 − это)")]
        [SerializeField, Range(0f, 1f)] private float alarmDecline = 0.02f;

        [Tooltip("В начале месяца при уровне «осада»: × (1 − это)")]
        [SerializeField, Range(0f, 1f)] private float siegeDecline = 0.05f;

        [Tooltip("За каждую готовую постройку гильдии (кроме стартовых): + жителей")]
        [SerializeField, Min(0)] private int populationPerBuilding = 5;

        [Header("Нападения в осаде")]
        [Tooltip("Шанс нападения за ночь")]
        [SerializeField, Range(0f, 1f)] private float attackChancePerNight = 0.1f;

        [Tooltip("Погибло жителей")]
        [SerializeField] private IntRange attackKilled = new IntRange(2, 5);

        [Tooltip("Стресс каждому человеку гильдии, кто не на задании")]
        [SerializeField, Min(0f)] private float attackStress = 10f;

        [Tooltip("Ущерб казне")]
        [SerializeField] private IntRange attackDamage = new IntRange(100, 300);

        [Header("Покидание")]
        [Tooltip("Безопасность на нуле столько суток подряд — поселение покинуто")]
        [SerializeField, Min(1)] private int abandonDays = 30;

        [Header("Таверна")]
        [Tooltip("Доход таверны в сутки с одного жителя")]
        [SerializeField, Min(0f)] private float localsTavernIncome = 0.05f;

        [Tooltip("В осаде доход от жителей ×")]
        [SerializeField, Range(0f, 1f)] private float siegeTavernMultiplier = 0.5f;

        [Header("Интерфейс")]
        [Tooltip("Последних изменений Безопасности хранится")]
        [SerializeField, Min(1)] private int recentChanges = 30;

        public float StartSecurity => startSecurity;
        public int StartPopulation => startPopulation;
        public float MaxSecurity => maxSecurity;
        public float DailyDecay => dailyDecay;
        public float ThreatDonePerRank => threatDonePerRank;
        public float OtherDonePerRank => otherDonePerRank;
        public float FailedPerRank => failedPerRank;
        public float PartyLost => partyLost;
        public float ThreatMissedPerRank => threatMissedPerRank;
        public float CalmFrom => calmFrom;
        public float NormalFrom => normalFrom;
        public float AlarmFrom => alarmFrom;
        public float CalmGrowth => calmGrowth;
        public float AlarmDecline => alarmDecline;
        public float SiegeDecline => siegeDecline;
        public int PopulationPerBuilding => populationPerBuilding;
        public float AttackChancePerNight => attackChancePerNight;
        public IntRange AttackKilled => attackKilled;
        public float AttackStress => attackStress;
        public IntRange AttackDamage => attackDamage;
        public int AbandonDays => abandonDays;
        public float LocalsTavernIncome => localsTavernIncome;
        public float SiegeTavernMultiplier => siegeTavernMultiplier;
        public int RecentChanges => recentChanges;
    }
}
