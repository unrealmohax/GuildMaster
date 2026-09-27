using System;
using System.Collections.Generic;

namespace GuildMaster.Core
{
    /// <summary>
    /// Ленты событий (часть мира): лента гильдии — последние строки, не больше лимита из баланса; старые выбрасываются.
    /// Помнит последний вариант строки каждого ключа, чтобы он не повторился подряд, и сколько строк каждой важности
    /// добавлено за игру.
    /// </summary>
    public sealed class FeedState
    {
        private static readonly int ImportanceCount = Enum.GetValues(typeof(EventImportance)).Length;

        private readonly List<FeedEntry> guild = new List<FeedEntry>();
        private readonly Dictionary<string, string> lastVariants = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly long[] added = new long[ImportanceCount];

        /// <summary>Лента гильдии, от старых строк к новым.</summary>
        public IReadOnlyList<FeedEntry> Guild => guild;

        /// <summary>Сколько строк этой важности добавлено за игру (включая выброшенные по лимиту).</summary>
        public long GetAddedCount(EventImportance importance) => added[(int)importance];

        /// <summary>Последний использованный вариант строки этого ключа (шаблон до подстановки).</summary>
        public bool TryGetLastVariant(string key, out string variant) => lastVariants.TryGetValue(key, out variant);

        internal void AddGuild(FeedEntry entry, int limit)
        {
            guild.Add(entry);
            added[(int)entry.Importance]++;
            int excess = guild.Count - Math.Max(1, limit);
            if (excess > 0) guild.RemoveRange(0, excess);
        }

        internal void SetLastVariant(string key, string variant) => lastVariants[key] = variant;
    }
}
