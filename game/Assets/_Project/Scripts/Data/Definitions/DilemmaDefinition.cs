using System;
using System.Collections.Generic;
using UnityEngine;

namespace GuildMaster.Data
{
    /// <summary>
    /// Обращение-дилемма (dilemmas.md, ТЗ 13). Числа вариантов — в эффектах вариантов; срок ответа, лимит открытых,
    /// час проверки и числа триггеров — <see cref="DilemmasBalance"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "GuildMaster/Dilemma", fileName = "Dilemma")]
    public sealed class DilemmaDefinition : Definition
    {
        [Tooltip("Номер дилеммы в dilemmas.md")]
        [SerializeField, Min(1)] private int number = 1;

        [SerializeField] private DilemmaSource source;

        [Tooltip("Код триггера (ТЗ 13)")]
        [SerializeField] private DilemmaTrigger trigger;

        [Tooltip("Перезарядка, дней. 0 — общая по умолчанию из BalanceSettings")]
        [SerializeField, Min(0)] private int cooldownDays;

        [Tooltip("Перезарядка на всю гильдию, а не на человека")]
        [SerializeField] private bool cooldownPerGuild;

        [Tooltip("Не больше одного раза на пару (Влюблённые)")]
        [SerializeField] private bool oncePerPair;

        [Tooltip("Текст с подстановками")]
        [SerializeField, TextArea] private string bodyTemplate;

        [Tooltip("Варианты {причина}; с чертой — только если черта есть у {имя}")]
        [SerializeField] private List<DilemmaReason> reasons = new List<DilemmaReason>();

        [Tooltip("Эффекты при появлении обращения (раскрытие черты самой дилеммой)")]
        [SerializeField] private List<DilemmaEffect> arrivalEffects = new List<DilemmaEffect>();

        [SerializeField] private List<DilemmaOption> options = new List<DilemmaOption>();

        [Tooltip("Какой вариант срабатывает без ответа (индекс в options)")]
        [SerializeField, Min(0)] private int timeoutOption;

        public int Number => number;
        public string Title => DisplayName;
        public DilemmaSource Source => source;
        public DilemmaTrigger Trigger => trigger;
        public int CooldownDays => cooldownDays;
        public bool CooldownPerGuild => cooldownPerGuild;
        public bool OncePerPair => oncePerPair;
        public string BodyTemplate => bodyTemplate;
        public IReadOnlyList<DilemmaReason> Reasons => reasons;
        public IReadOnlyList<DilemmaEffect> ArrivalEffects => arrivalEffects;
        public IReadOnlyList<DilemmaOption> Options => options;
        public int TimeoutOption => timeoutOption;
    }

    [Serializable]
    public sealed class DilemmaReason
    {
        [SerializeField] private string text;

        [Tooltip("Только если у {имя} есть эта черта (пусто — нейтральная причина)")]
        [SerializeField, OptionalReference] private SpecialTraitDefinition requiresTrait;

        public string Text => text;
        public SpecialTraitDefinition RequiresTrait => requiresTrait;
    }

    [Serializable]
    public sealed class DilemmaOption
    {
        [SerializeField] private string text;

        [Tooltip("Ближайшие последствия, которые видит игрок")]
        [SerializeField] private string visibleConsequencesText;

        [Tooltip("Все эффекты, видимые и отложенные")]
        [SerializeField] private List<DilemmaEffect> effects = new List<DilemmaEffect>();

        [Tooltip("Вариант-отказ")]
        [SerializeField] private bool isRefusal;

        [Tooltip("Игрок может выбрать. Выключено — исход только «без ответа» (Ссора из-за добычи)")]
        [SerializeField] private bool playerSelectable = true;

        [Tooltip("Ключ строки ленты об ответе")]
        [SerializeField] private string answerFeedKey;

        public string Text => text;
        public string VisibleConsequencesText => visibleConsequencesText;
        public IReadOnlyList<DilemmaEffect> Effects => effects;
        public bool IsRefusal => isRefusal;
        public bool PlayerSelectable => playerSelectable;
        public string AnswerFeedKey => answerFeedKey;
    }

    /// <summary>Эффект дилеммы: вид, кого касается, число. Для раскрытия — ось и полюс или черта; для флага — флаг.</summary>
    [Serializable]
    public sealed class DilemmaEffect
    {
        [SerializeField] private DilemmaEffectKind kind;
        [SerializeField] private DilemmaTarget target;

        [Tooltip("Сдвиг, сумма, доля или множитель — по виду эффекта")]
        [SerializeField] private float value;

        [SerializeField] private MemoryFlag flag;
        [SerializeField] private AxisId axis;
        [SerializeField] private AxisPole pole;
        [SerializeField, OptionalReference] private SpecialTraitDefinition trait;

        public DilemmaEffectKind Kind => kind;
        public DilemmaTarget Target => target;
        public float Value => value;
        public MemoryFlag Flag => flag;
        public AxisId Axis => axis;
        public AxisPole Pole => pole;
        public SpecialTraitDefinition Trait => trait;
    }
}
