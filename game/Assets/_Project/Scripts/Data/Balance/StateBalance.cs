using System;
using System.Collections.Generic;
using UnityEngine;

namespace GuildMaster.Data
{
    /// <summary>
    /// Раздел <c>State</c>: усталость, стресс, довольство, лояльность, кошелёк (numbers.md → Состояние, ТЗ 05).
    /// Изменения по занятиям — в час; знак — направление.
    /// </summary>
    [Serializable]
    public sealed class StateBalance
    {
        [Header("Старт у нового человека")]
        [SerializeField, Range(0f, 100f)] private float startFatigue = 10f;
        [SerializeField] private IntRange startStress = new IntRange(10, 30);
        [SerializeField, Range(0f, 100f)] private float startContentment = 50f;
        [SerializeField] private IntRange startLoyalty = new IntRange(40, 60);

        [Header("Усталость в час по занятиям")]
        [SerializeField] private float questFatigue = 3f;
        [SerializeField] private float campFatigue = -4f;
        [SerializeField] private float trainingFatigue = 2f;
        [SerializeField] private float restFatigue = -3f;
        [SerializeField] private float sleepFatigue = -8f;
        [SerializeField] private float tavernFatigue = -1f;
        [SerializeField] private float infirmaryFatigue = -3f;
        [SerializeField] private float bingeFatigue = 0f;

        [Header("Стресс в час по занятиям")]
        [Tooltip("Само по себе (задание, ночлег, тренировка)")]
        [SerializeField] private float passiveStress = -0.2f;
        [SerializeField] private float restStress = -0.5f;
        [SerializeField] private float sleepStress = -0.5f;
        [SerializeField] private float tavernStress = -1f;
        [SerializeField] private float infirmaryStress = -0.5f;
        [SerializeField] private float bingeStress = -1f;

        [Header("Усталость: пороги")]
        [Tooltip("Выше — профиль ×")]
        [SerializeField, Range(0f, 100f)] private float fatigueProfileThreshold = 70f;
        [SerializeField, Range(0f, 1f)] private float fatigueProfileMultiplier = 0.8f;

        [Tooltip("Выше — нельзя брать задания")]
        [SerializeField, Range(0f, 100f)] private float fatigueQuestBanThreshold = 90f;

        [Header("Стресс от событий")]
        [SerializeField] private float ownLightWoundStress = 10f;
        [SerializeField] private float ownHeavyWoundStress = 20f;
        [SerializeField] private float comradeWoundStress = 5f;
        [SerializeField] private float comradeDeathStress = 25f;
        [SerializeField] private float friendDeathStress = 40f;
        [SerializeField] private float loverDeathStress = 60f;

        [Header("Срыв")]
        [Tooltip("Стресс выше — раз в сутки шанс срыва")]
        [SerializeField, Range(0f, 100f)] private float breakdownThreshold = 80f;
        [SerializeField, Range(0f, 1f)] private float breakdownChancePerDay = 0.1f;

        [Tooltip("После срыва стресс +")]
        [SerializeField] private float stressAfterBreakdown = -30f;

        [Tooltip("Ось для вида срыва: Безрассудный от +этого, Трус от −этого")]
        [SerializeField, Range(0f, 100f)] private float breakdownAxisThreshold = 30f;

        [Tooltip("Запой: дней")]
        [SerializeField, Min(0)] private int bingeDays = 2;

        [Tooltip("Запой: на выпивку в день")]
        [SerializeField, Min(0f)] private float bingeDrinkPerDay = 5f;

        [Tooltip("Драка: отношения со случайным человеком")]
        [SerializeField] private float brawlRelation = -10f;

        [Tooltip("Драка: шанс лёгкой раны у обоих")]
        [SerializeField, Range(0f, 1f)] private float brawlWoundChance = 0.2f;

        [Tooltip("Трус: отказ от заданий, дней")]
        [SerializeField, Min(0)] private int refuseQuestsDays = 3;

        [Tooltip("«Сел и не смог подняться»: только отдых, дней")]
        [SerializeField, Min(0)] private int collapseDays = 2;

        [Header("Довольство: цель = база + условия")]
        [SerializeField, Range(0f, 100f)] private float contentmentBase = 50f;
        [SerializeField, Min(0f)] private float contentmentDailyStep = 1f;
        [SerializeField] private float cityHousingContentment = -10f;
        [SerializeField] private float dormHousingContentment = 0f;
        [SerializeField] private float tavernFoodContentment = 5f;
        [SerializeField] private float emptyWalletContentment = -15f;

        [Tooltip("Комиссия выше — штраф")]
        [SerializeField, Range(0f, 1f)] private float commissionThreshold = 0.25f;

        [Tooltip("Штраф за каждый шаг комиссии выше порога")]
        [SerializeField, Range(0.01f, 1f)] private float commissionStep = 0.05f;

        [SerializeField] private float commissionStepContentment = -5f;

        [Tooltip("Стресс выше — условие")]
        [SerializeField, Range(0f, 100f)] private float highStressThreshold = 70f;
        [SerializeField] private float highStressContentment = -5f;

        [Header("Лояльность")]
        [Tooltip("Сдвиг к довольству в день")]
        [SerializeField, Min(0f)] private float loyaltyDailyStep = 0.1f;

        [Tooltip("Ежемесячная проверка ухода: лояльность ниже")]
        [SerializeField, Range(0f, 100f)] private float leaveCheckThreshold = 25f;

        [SerializeField, Range(0f, 1f)] private float leaveChance = 0.2f;

        [Tooltip("Границы слов лояльности в карточке, по возрастанию")]
        [SerializeField] private List<float> loyaltyWordThresholds = new List<float> { 25f, 45f, 65f, 85f };

        [Header("Кошелёк")]
        [Tooltip("Кошелёк меньше расходов на неделю: мотив «Деньги» ×")]
        [SerializeField, Min(0f)] private float lowWalletMoneyMultiplier = 2f;

        [Tooltip("Долг гильдии гасится этой долей каждой доли награды")]
        [SerializeField, Range(0f, 1f)] private float debtRepaymentShare = 0.2f;

        public float StartFatigue => startFatigue;
        public IntRange StartStress => startStress;
        public float StartContentment => startContentment;
        public IntRange StartLoyalty => startLoyalty;
        public float QuestFatigue => questFatigue;
        public float CampFatigue => campFatigue;
        public float TrainingFatigue => trainingFatigue;
        public float RestFatigue => restFatigue;
        public float SleepFatigue => sleepFatigue;
        public float TavernFatigue => tavernFatigue;
        public float InfirmaryFatigue => infirmaryFatigue;
        public float BingeFatigue => bingeFatigue;
        public float PassiveStress => passiveStress;
        public float RestStress => restStress;
        public float SleepStress => sleepStress;
        public float TavernStress => tavernStress;
        public float InfirmaryStress => infirmaryStress;
        public float BingeStress => bingeStress;
        public float FatigueProfileThreshold => fatigueProfileThreshold;
        public float FatigueProfileMultiplier => fatigueProfileMultiplier;
        public float FatigueQuestBanThreshold => fatigueQuestBanThreshold;
        public float OwnLightWoundStress => ownLightWoundStress;
        public float OwnHeavyWoundStress => ownHeavyWoundStress;
        public float ComradeWoundStress => comradeWoundStress;
        public float ComradeDeathStress => comradeDeathStress;
        public float FriendDeathStress => friendDeathStress;
        public float LoverDeathStress => loverDeathStress;
        public float BreakdownThreshold => breakdownThreshold;
        public float BreakdownChancePerDay => breakdownChancePerDay;
        public float StressAfterBreakdown => stressAfterBreakdown;
        public float BreakdownAxisThreshold => breakdownAxisThreshold;
        public int BingeDays => bingeDays;
        public float BingeDrinkPerDay => bingeDrinkPerDay;
        public float BrawlRelation => brawlRelation;
        public float BrawlWoundChance => brawlWoundChance;
        public int RefuseQuestsDays => refuseQuestsDays;
        public int CollapseDays => collapseDays;
        public float ContentmentBase => contentmentBase;
        public float ContentmentDailyStep => contentmentDailyStep;
        public float CityHousingContentment => cityHousingContentment;
        public float DormHousingContentment => dormHousingContentment;
        public float TavernFoodContentment => tavernFoodContentment;
        public float EmptyWalletContentment => emptyWalletContentment;
        public float CommissionThreshold => commissionThreshold;
        public float CommissionStep => commissionStep;
        public float CommissionStepContentment => commissionStepContentment;
        public float HighStressThreshold => highStressThreshold;
        public float HighStressContentment => highStressContentment;
        public float LoyaltyDailyStep => loyaltyDailyStep;
        public float LeaveCheckThreshold => leaveCheckThreshold;
        public float LeaveChance => leaveChance;
        public IReadOnlyList<float> LoyaltyWordThresholds => loyaltyWordThresholds;
        public float LowWalletMoneyMultiplier => lowWalletMoneyMultiplier;
        public float DebtRepaymentShare => debtRepaymentShare;
    }
}
