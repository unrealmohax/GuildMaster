using System;
using System.Collections.Generic;
using UnityEngine;

namespace GuildMaster.Data
{
    /// <summary>
    /// Тип задания. Значения осей по рангам — <see cref="RanksBalance"/>;
    /// оси, которых нет в шаблоне, получают минимальный порог требований.
    /// </summary>
    [CreateAssetMenu(menuName = "GuildMaster/Quest Type", fileName = "QuestType")]
    public sealed class QuestTypeDefinition : Definition
    {
        [Tooltip("Главные оси профиля")]
        [SerializeField] private List<StatId> mainAxes = new List<StatId>();

        [Tooltip("Второстепенные оси профиля")]
        [SerializeField] private List<StatId> secondaryAxes = new List<StatId>();

        [Tooltip("Длительность раунда, часов")]
        [SerializeField, Min(1)] private int roundHours = 1;

        [Tooltip("Потолок размера группы: больше — раунд провален. 0 — нет потолка")]
        [SerializeField, Min(0)] private int maxPartySizeCeiling;

        [Tooltip("Потолки по осям: значение группы выше — раунд провален (в прототипе пусто)")]
        [SerializeField] private List<StatCeiling> statCeilings = new List<StatCeiling>();

        [Tooltip("Вес типа при генерации заказа")]
        [SerializeField, Min(0f)] private float generationWeight = 1f;

        [Tooltip("Ключ строк ленты: раунд удался")]
        [SerializeField] private string roundSuccessFeedKey;

        [Tooltip("Ключ строк ленты: раунд провален")]
        [SerializeField] private string roundFailFeedKey;

        public IReadOnlyList<StatId> MainAxes => mainAxes;
        public IReadOnlyList<StatId> SecondaryAxes => secondaryAxes;
        public int RoundHours => roundHours;
        public int MaxPartySizeCeiling => maxPartySizeCeiling;
        public IReadOnlyList<StatCeiling> StatCeilings => statCeilings;
        public float GenerationWeight => generationWeight;
        public string RoundSuccessFeedKey => roundSuccessFeedKey;
        public string RoundFailFeedKey => roundFailFeedKey;
    }

    /// <summary>Скрытый предел по оси для каждого ранга G–C. 0 — нет предела на этом ранге.</summary>
    [Serializable]
    public sealed class StatCeiling
    {
        [SerializeField] private StatId stat;
        [SerializeField] private int[] perRank = new int[Vocabulary.RankCount];

        public StatId Stat => stat;
        public int For(GuildRank rank) => perRank[(int)rank];
    }
}
