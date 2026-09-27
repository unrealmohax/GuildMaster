using System;
using System.Collections.Generic;
using UnityEngine;

namespace GuildMaster.Data
{
    /// <summary>Раздел <c>Ranks</c>: требования, награды и очки по рангам G–C.</summary>
    [Serializable]
    public sealed class RanksBalance
    {
        [Tooltip("По строке на ранг, по порядку G, F, E, D, C")]
        [SerializeField] private List<RankEntry> ranks = new List<RankEntry>
        {
            new RankEntry(GuildRank.G, new IntRange(20, 30), new IntRange(10, 15), new IntRange(30, 60), 1, 10),
            new RankEntry(GuildRank.F, new IntRange(30, 45), new IntRange(15, 25), new IntRange(70, 130), 2, 25),
            new RankEntry(GuildRank.E, new IntRange(45, 65), new IntRange(25, 35), new IntRange(140, 260), 3, 50),
            new RankEntry(GuildRank.D, new IntRange(65, 90), new IntRange(35, 50), new IntRange(280, 520), 4, 90),
            new RankEntry(GuildRank.C, new IntRange(90, 130), new IntRange(50, 70), new IntRange(550, 1000), 5, 0),
        };

        [Tooltip("Провал задания на повышение — повтор через, дней")]
        [SerializeField, Min(0)] private int promotionRetryDays = 30;

        [Tooltip("Блестящий успех: очки ранга ×")]
        [SerializeField, Min(0f)] private float brilliantPointsMultiplier = 2f;

        [Tooltip("Частичный успех: очки ранга ×")]
        [SerializeField, Min(0f)] private float partialPointsMultiplier = 0.5f;

        public IReadOnlyList<RankEntry> Ranks => ranks;
        public int PromotionRetryDays => promotionRetryDays;
        public float BrilliantPointsMultiplier => brilliantPointsMultiplier;
        public float PartialPointsMultiplier => partialPointsMultiplier;

        public RankEntry For(GuildRank rank) => ranks[(int)rank];
    }

    [Serializable]
    public sealed class RankEntry
    {
        [SerializeField] private GuildRank rank;

        [Tooltip("Требования по главным осям")]
        [SerializeField] private IntRange mainAxes;

        [Tooltip("Требования по второстепенным осям")]
        [SerializeField] private IntRange secondaryAxes;

        [Tooltip("Награда заказа, монет")]
        [SerializeField] private IntRange reward;

        [Tooltip("Очки ранга за успех")]
        [SerializeField, Min(0)] private int rankPoints;

        [Tooltip("Очков для повышения до следующего ранга (0 — последний ранг прототипа)")]
        [SerializeField, Min(0)] private int pointsToNext;

        public RankEntry()
        {
        }

        public RankEntry(GuildRank rank, IntRange mainAxes, IntRange secondaryAxes, IntRange reward, int rankPoints, int pointsToNext)
        {
            this.rank = rank;
            this.mainAxes = mainAxes;
            this.secondaryAxes = secondaryAxes;
            this.reward = reward;
            this.rankPoints = rankPoints;
            this.pointsToNext = pointsToNext;
        }

        public GuildRank Rank => rank;
        public IntRange MainAxes => mainAxes;
        public IntRange SecondaryAxes => secondaryAxes;
        public IntRange Reward => reward;
        public int RankPoints => rankPoints;
        public int PointsToNext => pointsToNext;
    }
}
