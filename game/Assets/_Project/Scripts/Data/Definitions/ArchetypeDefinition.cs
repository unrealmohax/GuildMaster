using System.Collections.Generic;
using UnityEngine;

namespace GuildMaster.Data
{
    /// <summary>Архетип. Оценка роли и пороги Новичка и Мастера на все руки — <see cref="AdventurersBalance"/>.</summary>
    [CreateAssetMenu(menuName = "GuildMaster/Archetype", fileName = "Archetype")]
    public sealed class ArchetypeDefinition : Definition
    {
        [Tooltip("Название для женщины, если отличается («Разведчица»)")]
        [SerializeField] private string displayNameFemale;

        [SerializeField, TextArea] private string description;
        [SerializeField] private ArchetypeKind kind;

        [Tooltip("Основные параметры (2) — у роли")]
        [SerializeField] private List<StatId> mainStats = new List<StatId>();

        [Tooltip("Вспомогательные параметры (1–2) — у роли")]
        [SerializeField] private List<StatId> secondaryStats = new List<StatId>();

        [Tooltip("Вес типа при генерации авантюриста: Новичок — чаще всех, Мастер на все руки — реже всех")]
        [SerializeField, Min(0f)] private float generationWeight = 1f;

        public string DisplayNameFemale => string.IsNullOrEmpty(displayNameFemale) ? DisplayName : displayNameFemale;
        public string Description => description;
        public ArchetypeKind Kind => kind;
        public IReadOnlyList<StatId> MainStats => mainStats;
        public IReadOnlyList<StatId> SecondaryStats => secondaryStats;
        public float GenerationWeight => generationWeight;
    }
}
