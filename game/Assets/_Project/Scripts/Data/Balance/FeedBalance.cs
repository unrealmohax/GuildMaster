using System;
using UnityEngine;

namespace GuildMaster.Data
{
    /// <summary>Раздел <c>Feed</c>: ленты событий.</summary>
    [Serializable]
    public sealed class FeedBalance
    {
        [Tooltip("Лента гильдии хранит столько последних строк; старые выбрасываются")]
        [SerializeField, Min(1)] private int guildFeedLimit = 500;

        public int GuildFeedLimit => guildFeedLimit;
    }
}
