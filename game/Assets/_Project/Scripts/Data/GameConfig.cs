using System.Collections.Generic;
using UnityEngine;

namespace GuildMaster.Data
{
    /// <summary>
    /// Корневой ассет данных: ссылается на все остальные определения. <c>DataRegistry</c> строится из него при старте.
    /// Новое определение попадает в игру, только когда его добавили сюда.
    /// </summary>
    [CreateAssetMenu(menuName = "GuildMaster/Game Config", fileName = "GameConfig")]
    public sealed class GameConfig : ScriptableObject
    {
        [SerializeField] private BalanceSettings balance;
        [SerializeField] private StatCatalog statCatalog;
        [SerializeField] private List<AxisDefinition> axes = new List<AxisDefinition>();
        [SerializeField] private List<SpecialTraitDefinition> specialTraits = new List<SpecialTraitDefinition>();
        [SerializeField] private List<ArchetypeDefinition> archetypes = new List<ArchetypeDefinition>();
        [SerializeField] private List<QuestTypeDefinition> questTypes = new List<QuestTypeDefinition>();
        [SerializeField] private List<RandomEventDefinition> randomEvents = new List<RandomEventDefinition>();
        [SerializeField] private List<DiscoveryDefinition> discoveries = new List<DiscoveryDefinition>();
        [SerializeField] private List<BuildingDefinition> buildings = new List<BuildingDefinition>();
        [SerializeField] private List<StaffRoleDefinition> staffRoles = new List<StaffRoleDefinition>();
        [SerializeField] private List<DecreeDefinition> decrees = new List<DecreeDefinition>();
        [SerializeField] private List<DilemmaDefinition> dilemmas = new List<DilemmaDefinition>();
        [SerializeField] private FeedTemplateSet feedTemplates;
        [SerializeField] private NameList nameList;
        [SerializeField] private OrderTextTemplates orderTexts;

        public BalanceSettings Balance => balance;
        public StatCatalog StatCatalog => statCatalog;
        public IReadOnlyList<AxisDefinition> Axes => axes;
        public IReadOnlyList<SpecialTraitDefinition> SpecialTraits => specialTraits;
        public IReadOnlyList<ArchetypeDefinition> Archetypes => archetypes;
        public IReadOnlyList<QuestTypeDefinition> QuestTypes => questTypes;
        public IReadOnlyList<RandomEventDefinition> RandomEvents => randomEvents;
        public IReadOnlyList<DiscoveryDefinition> Discoveries => discoveries;
        public IReadOnlyList<BuildingDefinition> Buildings => buildings;
        public IReadOnlyList<StaffRoleDefinition> StaffRoles => staffRoles;
        public IReadOnlyList<DecreeDefinition> Decrees => decrees;
        public IReadOnlyList<DilemmaDefinition> Dilemmas => dilemmas;
        public FeedTemplateSet FeedTemplates => feedTemplates;
        public NameList NameList => nameList;
        public OrderTextTemplates OrderTexts => orderTexts;

        /// <summary>Все определения с id, для реестра и валидатора.</summary>
        public IEnumerable<Definition> AllDefinitions()
        {
            foreach (AxisDefinition d in axes) yield return d;
            foreach (SpecialTraitDefinition d in specialTraits) yield return d;
            foreach (ArchetypeDefinition d in archetypes) yield return d;
            foreach (QuestTypeDefinition d in questTypes) yield return d;
            foreach (RandomEventDefinition d in randomEvents) yield return d;
            foreach (DiscoveryDefinition d in discoveries) yield return d;
            foreach (BuildingDefinition d in buildings) yield return d;
            foreach (StaffRoleDefinition d in staffRoles) yield return d;
            foreach (DecreeDefinition d in decrees) yield return d;
            foreach (DilemmaDefinition d in dilemmas) yield return d;
        }
    }
}
