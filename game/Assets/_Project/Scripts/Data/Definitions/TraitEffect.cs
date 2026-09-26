using System;
using System.Collections.Generic;
using UnityEngine;

namespace GuildMaster.Data
{
    /// <summary>
    /// Один эффект полюса оси или особой черты (ТЗ 02). Какие поля читаются — зависит от <see cref="Kind"/>:
    /// <list type="bullet">
    /// <item><c>MotiveWeight</c> — <see cref="Motive"/>, <see cref="Arrow"/>;</item>
    /// <item><c>StateRate</c> — <see cref="StateStat"/>, <see cref="Direction"/>, <see cref="Arrow"/> или явный множитель в <see cref="Value"/> (если стрелки нет);</item>
    /// <item><c>RiskPerception</c> — доля в <see cref="Value"/> (+0,3 — переоценивает риск);</item>
    /// <item><c>TensionModifier</c> — <see cref="Tension"/>, добавка к шансу в <see cref="Value"/>;</item>
    /// <item><c>PayContentmentSensitivity</c> — множитель в <see cref="Value"/>;</item>
    /// <item><c>PartyPreference</c> — <see cref="PartyPreference"/>;</item>
    /// <item><c>ProfileMultiplier</c> — множитель в <see cref="Value"/> к одному параметру из <see cref="Stats"/> (выбирается при появлении черты).</item>
    /// </list>
    /// У осей сила эффекта растёт от центра к полюсу (формула — ТЗ 04).
    /// </summary>
    [Serializable]
    public sealed class TraitEffect
    {
        [SerializeField] private EffectKind kind;

        [Tooltip("Когда действует")]
        [SerializeField] private EffectCondition condition;

        [Tooltip("Порог условия StressAbove")]
        [SerializeField, Range(0f, 100f)] private float conditionThreshold;

        [SerializeField] private Motive motive;
        [SerializeField] private StateStat stateStat;
        [SerializeField] private RateDirection direction;
        [SerializeField] private Arrow arrow;

        [Tooltip("Число эффекта: доля, добавка к шансу или множитель — см. вид эффекта")]
        [SerializeField, Range(-1f, 3f)] private float value;

        [SerializeField] private TensionKind tension;
        [SerializeField] private PartySizePreference partyPreference;

        [Tooltip("ProfileMultiplier: параметр (или несколько — тогда один выбирается при появлении черты)")]
        [SerializeField] private List<StatId> stats = new List<StatId>();

        public EffectKind Kind => kind;
        public EffectCondition Condition => condition;
        public float ConditionThreshold => conditionThreshold;
        public Motive Motive => motive;
        public StateStat StateStat => stateStat;
        public RateDirection Direction => direction;
        public Arrow Arrow => arrow;
        public float Value => value;
        public TensionKind Tension => tension;
        public PartySizePreference PartyPreference => partyPreference;
        public IReadOnlyList<StatId> Stats => stats;
    }
}
