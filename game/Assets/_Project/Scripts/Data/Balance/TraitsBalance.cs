using System;
using UnityEngine;

namespace GuildMaster.Data
{
    /// <summary>
    /// Раздел <c>Traits</c>: числа особых правил черт (<see cref="TraitHook"/>) и их появления.
    /// Числа, которые черта задаёт стрелкой или долей, — в эффектах самой черты.
    /// </summary>
    [Serializable]
    public sealed class TraitsBalance
    {
        [Header("Ветеран войны")]
        [Tooltip("После задания с провалом раунда: шанс запоя")]
        [SerializeField, Range(0f, 1f)] private float veteranBingeChance = 0.05f;

        [Header("Пьяница")]
        [Tooltip("Утром шанс пропустить день в таверне")]
        [SerializeField, Range(0f, 1f)] private float drunkardSkipChance = 0.1f;
        [SerializeField, Range(0f, 1f)] private float drunkardSkipChanceStressed = 0.2f;

        [Tooltip("«Стрессованный»: стресс выше")]
        [SerializeField, Range(0f, 100f)] private float drunkardStressThreshold = 50f;

        [Header("Хвастун")]
        [Tooltip("Репутация гильдии за каждый успех")]
        [SerializeField] private float braggartReputationPerSuccess = 1f;

        [Tooltip("Раскрытие: взял заказ с воспринимаемым перекрытием от…")]
        [SerializeField, Range(0f, 1f)] private float braggartRevealPerceivedOverlap = 0.6f;

        [Tooltip("…а реальным ниже")]
        [SerializeField, Range(0f, 1f)] private float braggartRevealRealOverlap = 0.4f;

        [Header("Железные нервы")]
        [Tooltip("Стресс от гибели товарищей ×")]
        [SerializeField, Range(0f, 1f)] private float ironNervesComradeDeathMultiplier = 0.5f;

        [Header("Семейный")]
        [Tooltip("Доля каждого дохода — домой")]
        [SerializeField, Range(0f, 1f)] private float familySendHomeShare = 0.3f;

        [Tooltip("Ежемесячная проверка ухода: шанс ×")]
        [SerializeField, Min(0f)] private float familyLeaveChanceMultiplier = 1.5f;

        [Tooltip("Раскрытие: отказ от заказа с воспринимаемым перекрытием ниже")]
        [SerializeField, Range(0f, 1f)] private float familyRevealPerceivedOverlap = 0.5f;

        [Header("Соперник")]
        [Tooltip("Опыт на совместном задании ×")]
        [SerializeField, Min(0f)] private float rivalExperienceMultiplier = 1.2f;

        [Header("Кошмары (приобр.)")]
        [Tooltip("Появляется после Катастрофы или гибели товарища на задании: шанс")]
        [SerializeField, Range(0f, 1f)] private float nightmaresAcquireChance = 0.3f;

        [Tooltip("Шанс отказаться от заказа того же типа")]
        [SerializeField, Range(0f, 1f)] private float nightmaresRefuseChance = 0.5f;

        [Header("Потерявший товарища (приобр.)")]
        [Tooltip("Друг — отношения от")]
        [SerializeField, Range(-100f, 100f)] private float grievingFriendRelation = 40f;

        [Tooltip("Срок спада, дней")]
        [SerializeField, Min(1)] private int grievingDays = 30;

        [Tooltip("По окончании: шанс «сломался» (иначе «ожесточился»)")]
        [SerializeField, Range(0f, 1f)] private float grievingBreakChance = 0.5f;

        [SerializeField] private float grievingBreakStress = 30f;
        [SerializeField] private float grievingHardenComposure = 5f;

        [Header("Проверенный (приобр.)")]
        [Tooltip("В гильдии не меньше, дней")]
        [SerializeField, Min(0)] private int testedMinDays = 180;

        [Tooltip("Лояльность от")]
        [SerializeField, Range(0f, 100f)] private float testedMinLoyalty = 50f;

        [SerializeField] private float testedLoyaltyBonus = 10f;
        [SerializeField] private float testedCohesionBonus = 5f;

        public float VeteranBingeChance => veteranBingeChance;
        public float DrunkardSkipChance => drunkardSkipChance;
        public float DrunkardSkipChanceStressed => drunkardSkipChanceStressed;
        public float DrunkardStressThreshold => drunkardStressThreshold;
        public float BraggartReputationPerSuccess => braggartReputationPerSuccess;
        public float BraggartRevealPerceivedOverlap => braggartRevealPerceivedOverlap;
        public float BraggartRevealRealOverlap => braggartRevealRealOverlap;
        public float IronNervesComradeDeathMultiplier => ironNervesComradeDeathMultiplier;
        public float FamilySendHomeShare => familySendHomeShare;
        public float FamilyLeaveChanceMultiplier => familyLeaveChanceMultiplier;
        public float FamilyRevealPerceivedOverlap => familyRevealPerceivedOverlap;
        public float RivalExperienceMultiplier => rivalExperienceMultiplier;
        public float NightmaresAcquireChance => nightmaresAcquireChance;
        public float NightmaresRefuseChance => nightmaresRefuseChance;
        public float GrievingFriendRelation => grievingFriendRelation;
        public int GrievingDays => grievingDays;
        public float GrievingBreakChance => grievingBreakChance;
        public float GrievingBreakStress => grievingBreakStress;
        public float GrievingHardenComposure => grievingHardenComposure;
        public int TestedMinDays => testedMinDays;
        public float TestedMinLoyalty => testedMinLoyalty;
        public float TestedLoyaltyBonus => testedLoyaltyBonus;
        public float TestedCohesionBonus => testedCohesionBonus;
    }
}
