using System;
using System.Collections.Generic;
using UnityEngine;

namespace GuildMaster.Data
{
    /// <summary>Ось характера и эффекты двух её полюсов (trait-effects.md, ТЗ 04).</summary>
    [CreateAssetMenu(menuName = "GuildMaster/Axis", fileName = "Axis")]
    public sealed class AxisDefinition : Definition
    {
        [SerializeField] private AxisId axis;
        [SerializeField] private AxisPoleDefinition negativePole = new AxisPoleDefinition();
        [SerializeField] private AxisPoleDefinition positivePole = new AxisPoleDefinition();

        public AxisId Axis => axis;
        public AxisPoleDefinition NegativePole => negativePole;
        public AxisPoleDefinition PositivePole => positivePole;

        public AxisPoleDefinition Pole(AxisPole pole) => pole == AxisPole.Negative ? negativePole : positivePole;
    }

    [Serializable]
    public sealed class AxisPoleDefinition
    {
        [Tooltip("Название полюса для мужчины («Трус»)")]
        [SerializeField] private string name;

        [Tooltip("Название полюса для женщины («Трусиха»)")]
        [SerializeField] private string nameFemale;

        [SerializeField] private List<TraitEffect> effects = new List<TraitEffect>();

        [Tooltip("Когда полюс раскрывается (ТЗ 04)")]
        [SerializeField] private RevealTrigger revealTrigger;

        [Tooltip("Ключ строки раскрытия в FeedTemplateSet")]
        [SerializeField] private string revealFeedKey;

        public string Name => name;
        public string NameFemale => nameFemale;
        public IReadOnlyList<TraitEffect> Effects => effects;
        public RevealTrigger RevealTrigger => revealTrigger;
        public string RevealFeedKey => revealFeedKey;
    }
}
