using System;
using UnityEngine;

namespace GuildMaster.Data
{
    /// <summary>
    /// Раздел <c>Decisions</c>: выбор, стрелки, множители состояния, формулы ценности,
    /// оценка напарника и постоянные группы. Ошибки оценки риска — эффекты черт.
    /// </summary>
    [Serializable]
    public sealed class DecisionsBalance
    {
        [Header("Выбор")]
        [Tooltip("Шанс выбрать лучший вариант, иначе — второй")]
        [SerializeField, Range(0f, 1f)] private float bestChoiceChance = 0.8f;

        [SerializeField, Min(0f)] private float defaultMotiveWeight = 1f;

        [Header("Стрелки: множитель на крайнем полюсе")]
        [SerializeField, Min(0f)] private float strongUpMultiplier = 2f;
        [SerializeField, Min(0f)] private float upMultiplier = 1.5f;
        [SerializeField, Min(0f)] private float downMultiplier = 0.5f;
        [SerializeField, Min(0f)] private float strongDownMultiplier = 0.25f;

        [Header("Множители состояния")]
        [Tooltip("Усталость выше — Отдых × (1 + (усталость − порог) / делитель)")]
        [SerializeField, Range(0f, 100f)] private float fatigueRestThreshold = 50f;
        [SerializeField, Min(1f)] private float fatigueRestDivisor = 25f;

        [Tooltip("Стресс выше — Утешение × (1 + (стресс − порог) / делитель), Безопасность ×")]
        [SerializeField, Range(0f, 100f)] private float stressComfortThreshold = 40f;
        [SerializeField, Min(1f)] private float stressComfortDivisor = 30f;
        [SerializeField, Min(0f)] private float stressSafetyMultiplier = 1.3f;

        [Tooltip("Лёгкая рана: Безопасность ×")]
        [SerializeField, Min(0f)] private float lightWoundSafetyMultiplier = 1.3f;

        [Header("Ценность заказа")]
        [Tooltip("Деньги: доля делится на расходы за столько дней")]
        [SerializeField, Min(1)] private int moneyExpenseDays = 7;

        [Tooltip("Слава: + за задание на повышение")]
        [SerializeField] private float promotionGloryBonus = 0.2f;

        [Tooltip("Товарищи: в группе")]
        [SerializeField] private float companionsInGroup = 0.5f;

        [Tooltip("Товарищи: + если в группе друг")]
        [SerializeField] private float companionsFriendBonus = 0.3f;

        [Tooltip("Товарищи: + если группа постоянная")]
        [SerializeField] private float companionsPermanentBonus = 0.2f;

        [Tooltip("Отдых: 1 − усталость/100 − это")]
        [SerializeField] private float orderRestOffset = 0.5f;

        [Header("Ценность: тренировка")]
        [SerializeField] private float trainingGlory = 0.4f;
        [SerializeField] private float trainingSafety = 1f;
        [Tooltip("Товарищи, если тренируются другие")]
        [SerializeField] private float trainingCompanions = 0.2f;
        [SerializeField] private float trainingRest = -0.3f;

        [Header("Ценность: отдых (Отдых = усталость/100)")]
        [SerializeField] private float restSafety = 1f;
        [SerializeField] private float restComfort = 0.3f;

        [Header("Ценность: лечение")]
        [SerializeField] private float healSafety = 1f;
        [SerializeField] private float healRest = 0.8f;
        [SerializeField] private float healComfort = 0.3f;

        [Header("Ценность: таверна (Деньги = −цена/кошелёк, Утешение = стресс/100 + добавка)")]
        [SerializeField] private float tavernSafety = 1f;
        [SerializeField] private float tavernCompanions = 0.6f;
        [SerializeField] private float tavernCompanionsPerFriend = 0.1f;
        [SerializeField] private float tavernRest = 0.3f;
        [SerializeField] private float tavernComfortBonus = 0.2f;

        [Tooltip("Днём в таверну — только Пьяница или стресс выше")]
        [SerializeField, Range(0f, 100f)] private float daytimeTavernStress = 60f;

        [Header("Оценка напарника")]
        [Tooltip("Прирост воспринимаемого перекрытия ×")]
        [SerializeField] private float partnerOverlapGainWeight = 3f;

        [Tooltip("Отношения / это")]
        [SerializeField, Min(1f)] private float partnerRelationDivisor = 100f;

        [SerializeField] private float partnerFriendBonus = 0.3f;

        [Tooltip("− |разница рангов| ×")]
        [SerializeField] private float partnerRankDifferencePenalty = 0.2f;

        [Header("Постоянные группы")]
        [Tooltip("Складываются после стольких совместных успехов")]
        [SerializeField, Min(1)] private int permanentPartySuccesses = 3;

        [Tooltip("…при попарных отношениях от")]
        [SerializeField, Range(-100f, 100f)] private float permanentPartyMinRelation = 20f;

        [Tooltip("Отношения двух членов ниже или равно — один уходит")]
        [SerializeField, Range(-100f, 100f)] private float permanentPartyBreakRelation = -40f;

        [Tooltip("Одиночка (ось Люди ≤ −крайний полюс) уходит после стольких совместных заданий")]
        [SerializeField, Min(1)] private int lonerLeavesAfterQuests = 3;

        [Tooltip("Новичок становится членом после стольких успехов с группой")]
        [SerializeField, Min(1)] private int newMemberSuccesses = 3;

        public float BestChoiceChance => bestChoiceChance;
        public float DefaultMotiveWeight => defaultMotiveWeight;

        /// <summary>Множитель стрелки на крайнем полюсе оси (None — 1).</summary>
        public float ArrowMultiplier(Arrow arrow)
        {
            switch (arrow)
            {
                case Arrow.StrongUp: return strongUpMultiplier;
                case Arrow.Up: return upMultiplier;
                case Arrow.Down: return downMultiplier;
                case Arrow.StrongDown: return strongDownMultiplier;
                default: return 1f;
            }
        }

        public float FatigueRestThreshold => fatigueRestThreshold;
        public float FatigueRestDivisor => fatigueRestDivisor;
        public float StressComfortThreshold => stressComfortThreshold;
        public float StressComfortDivisor => stressComfortDivisor;
        public float StressSafetyMultiplier => stressSafetyMultiplier;
        public float LightWoundSafetyMultiplier => lightWoundSafetyMultiplier;
        public int MoneyExpenseDays => moneyExpenseDays;
        public float PromotionGloryBonus => promotionGloryBonus;
        public float CompanionsInGroup => companionsInGroup;
        public float CompanionsFriendBonus => companionsFriendBonus;
        public float CompanionsPermanentBonus => companionsPermanentBonus;
        public float OrderRestOffset => orderRestOffset;
        public float TrainingGlory => trainingGlory;
        public float TrainingSafety => trainingSafety;
        public float TrainingCompanions => trainingCompanions;
        public float TrainingRest => trainingRest;
        public float RestSafety => restSafety;
        public float RestComfort => restComfort;
        public float HealSafety => healSafety;
        public float HealRest => healRest;
        public float HealComfort => healComfort;
        public float TavernSafety => tavernSafety;
        public float TavernCompanions => tavernCompanions;
        public float TavernCompanionsPerFriend => tavernCompanionsPerFriend;
        public float TavernRest => tavernRest;
        public float TavernComfortBonus => tavernComfortBonus;
        public float DaytimeTavernStress => daytimeTavernStress;
        public float PartnerOverlapGainWeight => partnerOverlapGainWeight;
        public float PartnerRelationDivisor => partnerRelationDivisor;
        public float PartnerFriendBonus => partnerFriendBonus;
        public float PartnerRankDifferencePenalty => partnerRankDifferencePenalty;
        public int PermanentPartySuccesses => permanentPartySuccesses;
        public float PermanentPartyMinRelation => permanentPartyMinRelation;
        public float PermanentPartyBreakRelation => permanentPartyBreakRelation;
        public int LonerLeavesAfterQuests => lonerLeavesAfterQuests;
        public int NewMemberSuccesses => newMemberSuccesses;
    }
}
