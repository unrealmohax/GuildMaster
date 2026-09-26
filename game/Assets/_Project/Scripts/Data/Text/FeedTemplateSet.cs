using System;
using System.Collections.Generic;
using UnityEngine;

namespace GuildMaster.Data
{
    /// <summary>
    /// Шаблоны строк лент. По ключу может быть несколько шаблонов
    /// с разными условиями; выбирается самый конкретный из подходящих, затем случайный вариант.
    /// </summary>
    [CreateAssetMenu(menuName = "GuildMaster/Feed Template Set", fileName = "FeedTemplates")]
    public sealed class FeedTemplateSet : ScriptableObject
    {
        [SerializeField] private List<FeedTemplate> templates = new List<FeedTemplate>();

        public IReadOnlyList<FeedTemplate> Templates => templates;
    }

    [Serializable]
    public sealed class FeedTemplate
    {
        [Tooltip("Ключ шаблона: по нему система находит строки события")]
        [SerializeField] private string key;

        [SerializeField] private FeedKind feed;
        [SerializeField] private FeedImportance importance;

        [Tooltip("Все условия должны выполниться")]
        [SerializeField] private List<FeedCondition> conditions = new List<FeedCondition>();

        [Tooltip("Варианты строки: подстановки {имя}, {место:р}, род [м|ж]")]
        [SerializeField] private List<string> variants = new List<string>();

        public string Key => key;
        public FeedKind Feed => feed;
        public FeedImportance Importance => importance;
        public IReadOnlyList<FeedCondition> Conditions => conditions;
        public IReadOnlyList<string> Variants => variants;
    }

    /// <summary>Условие шаблона. Какое поле читается — по <see cref="Kind"/>.</summary>
    [Serializable]
    public sealed class FeedCondition
    {
        [SerializeField] private FeedConditionKind kind;
        [SerializeField, OptionalReference] private ArchetypeDefinition archetype;
        [SerializeField, OptionalReference] private QuestTypeDefinition questType;
        [SerializeField] private AxisId axis;
        [SerializeField] private AxisPole pole;
        [SerializeField, OptionalReference] private SpecialTraitDefinition trait;
        [SerializeField] private StatId stat;

        public FeedConditionKind Kind => kind;
        public ArchetypeDefinition Archetype => archetype;
        public QuestTypeDefinition QuestType => questType;
        public AxisId Axis => axis;
        public AxisPole Pole => pole;
        public SpecialTraitDefinition Trait => trait;
        public StatId Stat => stat;
    }
}
