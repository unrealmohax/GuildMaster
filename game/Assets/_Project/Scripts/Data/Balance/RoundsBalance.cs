using System;
using System.Collections.Generic;
using UnityEngine;

namespace GuildMaster.Data
{
    /// <summary>Раздел <c>Rounds</c>: расчёт раунда, синергии, лестница провалов.</summary>
    [Serializable]
    public sealed class RoundsBalance
    {
        [Tooltip("Максимум людей в группе")]
        [SerializeField, Min(1)] private int maxPartySize = 6;

        [Tooltip("Один человек: профиль ×")]
        [SerializeField, Min(0f)] private float soloProfileMultiplier = 1f;

        [Tooltip("Неточность описания: скрытый множитель профиля заказа")]
        [SerializeField] private FloatRange descriptionAccuracy = new FloatRange(0.9f, 1.1f);

        [Header("Синергии (добавка к перекрытию за пару)")]
        [SerializeField, Range(-1f, 1f)] private float friendsSynergy = 0.05f;
        [SerializeField, Range(-1f, 1f)] private float longPartnersSynergy = 0.05f;
        [SerializeField, Range(-1f, 1f)] private float loversSynergy = 0.1f;
        [SerializeField, Range(-1f, 1f)] private float rivalsSynergy = -0.1f;
        [SerializeField, Range(-1f, 1f)] private float dislikeSynergy = -0.05f;

        [Tooltip("Сумма синергий по модулю не больше")]
        [SerializeField, Range(0f, 1f)] private float maxTotalSynergy = 0.2f;

        [Header("Лестница провалов")]
        [Tooltip("По ступени на провал 1–5")]
        [SerializeField] private List<FailureStep> failureLadder = new List<FailureStep>
        {
            new FailureStep(stress: 5, lightWound: 0.3f, heavyWound: 0f, maim: 0f, bonusLoss: 0.5f, gearDamage: 0f, lootLoss: 0f, FailureDeath.None),
            new FailureStep(stress: 5, lightWound: 0.35f, heavyWound: 0.15f, maim: 0f, bonusLoss: 1f, gearDamage: 0.3f, lootLoss: 0f, FailureDeath.None),
            new FailureStep(stress: 8, lightWound: 0f, heavyWound: 0.7f, maim: 0.15f, bonusLoss: 1f, gearDamage: 0f, lootLoss: 0.5f, FailureDeath.None),
            new FailureStep(stress: 0, lightWound: 0f, heavyWound: 0f, maim: 0f, bonusLoss: 1f, gearDamage: 0f, lootLoss: 0f, FailureDeath.MostWounded),
            new FailureStep(stress: 0, lightWound: 0f, heavyWound: 0f, maim: 0f, bonusLoss: 1f, gearDamage: 0f, lootLoss: 0f, FailureDeath.Everyone),
        };

        [Tooltip("Спасение Лекарем на 4-м провале: Медицина группы / это число")]
        [SerializeField, Min(1f)] private float medicSaveDivisor = 200f;

        [Tooltip("Спасение Лекарем: шанс не больше")]
        [SerializeField, Range(0f, 1f)] private float medicSaveMax = 0.5f;

        [Tooltip("Каждый провал продлевает задание на столько раундов")]
        [SerializeField, Min(0)] private int extraRoundsPerFailure = 1;

        public int MaxPartySize => maxPartySize;
        public float SoloProfileMultiplier => soloProfileMultiplier;
        public FloatRange DescriptionAccuracy => descriptionAccuracy;
        public float FriendsSynergy => friendsSynergy;
        public float LongPartnersSynergy => longPartnersSynergy;
        public float LoversSynergy => loversSynergy;
        public float RivalsSynergy => rivalsSynergy;
        public float DislikeSynergy => dislikeSynergy;
        public float MaxTotalSynergy => maxTotalSynergy;
        public IReadOnlyList<FailureStep> FailureLadder => failureLadder;
        public float MedicSaveDivisor => medicSaveDivisor;
        public float MedicSaveMax => medicSaveMax;
        public int ExtraRoundsPerFailure => extraRoundsPerFailure;
    }

    public enum FailureDeath
    {
        None = 0,
        /// <summary>Гибнет самый тяжело раненый.</summary>
        MostWounded = 1,
        /// <summary>Гибнут все присутствующие.</summary>
        Everyone = 2,
    }

    /// <summary>Ступень лестницы провалов: что всегда и что с шансом.</summary>
    [Serializable]
    public sealed class FailureStep
    {
        [Tooltip("Стресс всем")]
        [SerializeField, Min(0f)] private float stress;

        [SerializeField, Range(0f, 1f)] private float lightWoundChance;
        [SerializeField, Range(0f, 1f)] private float heavyWoundChance;
        [SerializeField, Range(0f, 1f)] private float maimChance;

        [Tooltip("Потеря дополнительной награды (1 — точно)")]
        [SerializeField, Range(0f, 1f)] private float bonusLossChance;

        [SerializeField, Range(0f, 1f)] private float gearDamageChance;
        [SerializeField, Range(0f, 1f)] private float lootLossChance;
        [SerializeField] private FailureDeath death;

        public FailureStep()
        {
        }

        public FailureStep(float stress, float lightWound, float heavyWound, float maim, float bonusLoss, float gearDamage, float lootLoss, FailureDeath death)
        {
            this.stress = stress;
            lightWoundChance = lightWound;
            heavyWoundChance = heavyWound;
            maimChance = maim;
            bonusLossChance = bonusLoss;
            gearDamageChance = gearDamage;
            lootLossChance = lootLoss;
            this.death = death;
        }

        public float Stress => stress;
        public float LightWoundChance => lightWoundChance;
        public float HeavyWoundChance => heavyWoundChance;
        public float MaimChance => maimChance;
        public float BonusLossChance => bonusLossChance;
        public float GearDamageChance => gearDamageChance;
        public float LootLossChance => lootLossChance;
        public FailureDeath Death => death;
    }
}
