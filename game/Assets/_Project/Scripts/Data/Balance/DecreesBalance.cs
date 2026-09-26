using System;
using UnityEngine;

namespace GuildMaster.Data
{
    /// <summary>Раздел <c>Decrees</c>: сроки, цены и эффекты распоряжений (numbers.md → Распоряжения, ТЗ 12).</summary>
    [Serializable]
    public sealed class DecreesBalance
    {
        [Header("Общее")]
        [SerializeField, Min(1)] private int weekDurationDays = 7;
        [SerializeField, Min(1)] private int monthDurationDays = 30;

        [Tooltip("Отмена льготы игроком: довольство затронутых")]
        [SerializeField] private float benefitCancelContentment = -5f;

        [Header("№2 Еда и жильё для новичков")]
        [Tooltip("Гильдия платит за новичка в день")]
        [SerializeField, Min(0f)] private float newcomerDailyCost = 5f;

        [Tooltip("Новичок — в гильдии меньше, дней")]
        [SerializeField, Min(1)] private int newcomerPeriodDays = 30;

        [Tooltip("Шанс кандидата ×")]
        [SerializeField, Min(1f)] private float newcomerInfluxMultiplier = 1.5f;

        [Tooltip("«Халявщики»: ось Труд у кандидатов +")]
        [SerializeField, Range(-100f, 100f)] private float freeloaderWorkShift = -20f;

        [Header("№5 Задания от ранга — только группой")]
        [Tooltip("Одиночки (ось Люди ≤ порога) недовольны, пока включено: условие довольства")]
        [SerializeField] private float groupOnlyLonerContentment = -3f;
        [SerializeField, Range(-100f, 0f)] private float groupOnlyLonerAxis = -30f;

        [Header("№11 Компенсация за ранение")]
        [SerializeField, Min(0)] private int compensationLightWound = 20;
        [SerializeField, Min(0)] private int compensationHeavyWound = 100;
        [SerializeField, Min(0)] private int compensationMaimed = 300;

        [Tooltip("Мотив «Безопасность» в оценке заказов ×")]
        [SerializeField, Min(0f)] private float compensationSafetyMultiplier = 0.85f;

        [Tooltip("Лояльность в день у всех, пока включено")]
        [SerializeField] private float compensationLoyaltyPerDay = 0.05f;

        [Header("№14 Сухой закон")]
        [Tooltip("Доход таверны ×")]
        [SerializeField, Range(0f, 1f)] private float prohibitionTavernIncomeMultiplier = 0.6f;

        [Tooltip("Снятие стресса в таверне ×")]
        [SerializeField, Range(0f, 1f)] private float prohibitionStressReliefMultiplier = 0.7f;

        [Tooltip("Пьяница: условие довольства")]
        [SerializeField] private float prohibitionDrunkardContentment = -10f;

        [Tooltip("Пьяница: шанс пропуска дня из-за запоя")]
        [SerializeField, Range(0f, 1f)] private float prohibitionDrunkardSkipChance = 0f;

        public int WeekDurationDays => weekDurationDays;
        public int MonthDurationDays => monthDurationDays;
        public float BenefitCancelContentment => benefitCancelContentment;
        public float NewcomerDailyCost => newcomerDailyCost;
        public int NewcomerPeriodDays => newcomerPeriodDays;
        public float NewcomerInfluxMultiplier => newcomerInfluxMultiplier;
        public float FreeloaderWorkShift => freeloaderWorkShift;
        public float GroupOnlyLonerContentment => groupOnlyLonerContentment;
        public float GroupOnlyLonerAxis => groupOnlyLonerAxis;
        public int CompensationLightWound => compensationLightWound;
        public int CompensationHeavyWound => compensationHeavyWound;
        public int CompensationMaimed => compensationMaimed;
        public float CompensationSafetyMultiplier => compensationSafetyMultiplier;
        public float CompensationLoyaltyPerDay => compensationLoyaltyPerDay;
        public float ProhibitionTavernIncomeMultiplier => prohibitionTavernIncomeMultiplier;
        public float ProhibitionStressReliefMultiplier => prohibitionStressReliefMultiplier;
        public float ProhibitionDrunkardContentment => prohibitionDrunkardContentment;
        public float ProhibitionDrunkardSkipChance => prohibitionDrunkardSkipChance;
    }
}
