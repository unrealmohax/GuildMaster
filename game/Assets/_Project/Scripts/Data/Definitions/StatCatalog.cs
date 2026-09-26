using System;
using System.Collections.Generic;
using UnityEngine;

namespace GuildMaster.Data
{
    /// <summary>Описание 14 параметров и порядок осей диаграммы задания (adventurers.md, quests.md).</summary>
    [CreateAssetMenu(menuName = "GuildMaster/Stat Catalog", fileName = "StatCatalog")]
    public sealed class StatCatalog : ScriptableObject
    {
        [Tooltip("По записи на каждый StatId")]
        [SerializeField] private List<StatInfo> stats = new List<StatInfo>();

        [Tooltip("13 осей диаграммы по кругу, без Слаженности")]
        [SerializeField] private List<StatId> radarOrder = new List<StatId>();

        public IReadOnlyList<StatInfo> Stats => stats;
        public IReadOnlyList<StatId> RadarOrder => radarOrder;

        public StatInfo Get(StatId stat)
        {
            foreach (StatInfo info in stats)
            {
                if (info.Stat == stat) return info;
            }
            throw new KeyNotFoundException($"{name}: no entry for {stat}");
        }
    }

    [Serializable]
    public sealed class StatInfo
    {
        [SerializeField] private StatId stat;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;

        [Tooltip("Навык (а не характеристика)")]
        [SerializeField] private bool isSkill;

        public StatId Stat => stat;
        public string DisplayName => displayName;
        public string Description => description;
        public bool IsSkill => isSkill;
    }
}
