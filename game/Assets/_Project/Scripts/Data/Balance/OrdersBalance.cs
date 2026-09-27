using System;
using System.Collections.Generic;
using UnityEngine;

namespace GuildMaster.Data
{
    /// <summary>Раздел <c>Orders</c>: поток заказов, доска, Регистратор.</summary>
    [Serializable]
    public sealed class OrdersBalance
    {
        [Tooltip("Заказов в день: база")]
        [SerializeField, Min(0f)] private float baseOrdersPerDay = 1f;

        [Tooltip("Заказов в день: + репутация / это число (дробная часть — шанс ещё одного)")]
        [SerializeField, Min(1f)] private float reputationPerExtraOrder = 10f;

        [Tooltip("Срок на доске, дней")]
        [SerializeField] private IntRange boardDays = new IntRange(3, 7);

        [Tooltip("Шанс «сложного не по времени» заказа")]
        [SerializeField, Range(0f, 1f)] private float hardEarlyChance = 0.05f;

        [Tooltip("«Сложный не по времени»: ранг выше выпавшего на столько (не выше C)")]
        [SerializeField, Min(0)] private int hardEarlyRankBonus = 2;

        [Tooltip("Смесь рангов по репутации: строка действует от своей репутации до следующей")]
        [SerializeField] private List<RankMixEntry> rankMix = new List<RankMixEntry>
        {
            new RankMixEntry(0, 0.85f, 0.15f, 0f, 0f, 0f),
            new RankMixEntry(20, 0.2f, 0.5f, 0.3f, 0f, 0f),
            new RankMixEntry(40, 0f, 0.1f, 0.5f, 0.4f, 0f),
            new RankMixEntry(60, 0f, 0f, 0.2f, 0.5f, 0.3f),
        };

        [Tooltip("Доля дальних заказов")]
        [SerializeField, Range(0f, 1f)] private float farChance = 0.3f;

        [Tooltip("Минимальный порог требований по всем осям заказа")]
        [SerializeField, Min(0)] private int requirementFloor = 10;

        [Tooltip("Случайный множитель каждой оси профиля")]
        [SerializeField] private FloatRange axisVariance = new FloatRange(0.8f, 1.2f);

        [Tooltip("Далеко: награда ×")]
        [SerializeField, Min(1f)] private float farRewardMultiplier = 1.5f;

        [Tooltip("Награда округляется до")]
        [SerializeField, Min(1)] private int rewardRounding = 5;

        [Tooltip("Дополнительная награда (бонус за чистое выполнение) — доля награды")]
        [SerializeField, Range(0f, 1f)] private float bonusRewardShare = 0.25f;

        [Tooltip("Добыча — доля награды")]
        [SerializeField] private FloatRange lootShare = new FloatRange(0f, 0.3f);

        [Tooltip("Намёков в описании: на столько самых больших осей")]
        [SerializeField] private IntRange hintCount = new IntRange(1, 2);

        [Tooltip("Точность описания: скрытый множитель, насколько описание недооценивает или переоценивает задание")]
        [SerializeField] private FloatRange descriptionAccuracy = new FloatRange(0.9f, 1.1f);

        [Tooltip("Важный заказ: награда от")]
        [SerializeField, Min(0)] private int importantRewardThreshold = 250;

        [Tooltip("Важный заказ без ответа игрока отклоняется через, дней")]
        [SerializeField, Min(1)] private int playerResponseDays = 2;

        [Tooltip("Заказов на доске на старте игры")]
        [SerializeField, Min(0)] private int startOrders = 3;

        [Tooltip("Ранг стартовых заказов")]
        [SerializeField] private GuildRank startOrderRank = GuildRank.G;

        [Tooltip("Закрытых заказов (отклонённых, снятых) хранится не больше; старые выбрасываются")]
        [SerializeField, Min(1)] private int closedOrdersLimit = 200;

        public float BaseOrdersPerDay => baseOrdersPerDay;
        public float ReputationPerExtraOrder => reputationPerExtraOrder;
        public IntRange BoardDays => boardDays;
        public float HardEarlyChance => hardEarlyChance;
        public int HardEarlyRankBonus => hardEarlyRankBonus;
        public IReadOnlyList<RankMixEntry> RankMix => rankMix;
        public float FarChance => farChance;
        public int RequirementFloor => requirementFloor;
        public FloatRange AxisVariance => axisVariance;
        public float FarRewardMultiplier => farRewardMultiplier;
        public int RewardRounding => rewardRounding;
        public float BonusRewardShare => bonusRewardShare;
        public FloatRange LootShare => lootShare;
        public IntRange HintCount => hintCount;
        public FloatRange DescriptionAccuracy => descriptionAccuracy;
        public int ImportantRewardThreshold => importantRewardThreshold;
        public int PlayerResponseDays => playerResponseDays;
        public int StartOrders => startOrders;
        public GuildRank StartOrderRank => startOrderRank;
        public int ClosedOrdersLimit => closedOrdersLimit;
    }

    /// <summary>Строка смеси рангов: с этой репутации — такие веса рангов G–C.</summary>
    [Serializable]
    public sealed class RankMixEntry
    {
        [SerializeField, Range(0, 100)] private int fromReputation;

        [Tooltip("Веса рангов G, F, E, D, C")]
        [SerializeField] private float[] weights = new float[Vocabulary.RankCount];

        public RankMixEntry()
        {
        }

        public RankMixEntry(int fromReputation, params float[] weights)
        {
            this.fromReputation = fromReputation;
            this.weights = weights;
        }

        public int FromReputation => fromReputation;
        public IReadOnlyList<float> Weights => weights;
    }
}
