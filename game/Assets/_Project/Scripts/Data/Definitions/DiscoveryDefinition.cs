using UnityEngine;

namespace GuildMaster.Data
{
    /// <summary>Находка: пещера. Шанс находки — <see cref="TravelBalance"/>.</summary>
    [CreateAssetMenu(menuName = "GuildMaster/Discovery", fileName = "Discovery")]
    public sealed class DiscoveryDefinition : EncounterDefinition
    {
        [Tooltip("Кто замечает находку: лучший по этому параметру")]
        [SerializeField] private StatId spotterStat = StatId.Perception;

        [Tooltip("Награда группе за сообщение Регистратору — доля награды задания")]
        [SerializeField, Range(0f, 1f)] private float reportRewardShare;

        [Tooltip("Название событийного задания для игрока")]
        [SerializeField] private string eventQuestTitle;

        [SerializeField] private string foundFeedKey;
        [SerializeField] private string exploreFeedKey;
        [SerializeField] private string skipFeedKey;
        [SerializeField] private string emptyFeedKey;
        [SerializeField] private string lootFeedKey;
        [SerializeField] private string failFeedKey;
        [SerializeField] private string reportedFeedKey;

        public StatId SpotterStat => spotterStat;
        public float ReportRewardShare => reportRewardShare;
        public string EventQuestTitle => eventQuestTitle;
        public string FoundFeedKey => foundFeedKey;
        public string ExploreFeedKey => exploreFeedKey;
        public string SkipFeedKey => skipFeedKey;
        public string EmptyFeedKey => emptyFeedKey;
        public string LootFeedKey => lootFeedKey;
        public string FailFeedKey => failFeedKey;
        public string ReportedFeedKey => reportedFeedKey;
    }
}
