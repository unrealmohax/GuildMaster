using System;
using System.Collections.Generic;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>Включённое распоряжение: область (ранги), срок и когда он кончится.</summary>
    public sealed class ActiveDecree
    {
        private readonly List<GuildRank> ranks = new List<GuildRank>();

        internal ActiveDecree(string decreeId)
        {
            DecreeId = decreeId ?? throw new ArgumentNullException(nameof(decreeId));
        }

        public string DecreeId { get; }

        /// <summary>Область: ранги заказов. У распоряжений без области — пусто.</summary>
        public IReadOnlyList<GuildRank> Ranks => ranks;

        public DecreeDuration Duration { get; internal set; }

        /// <summary>С какого часа отсчитывается текущий срок (включение или последняя смена области и срока).</summary>
        public long TermStartedAtHours { get; internal set; }

        /// <summary>Когда распоряжение выключится само; у бессрочного — 0.</summary>
        public long EndsAtHours { get; internal set; }

        public bool IsPermanent => Duration == DecreeDuration.Permanent;

        public bool HasRank(GuildRank rank) => ranks.Contains(rank);

        internal void SetRanks(IEnumerable<GuildRank> value)
        {
            ranks.Clear();
            ranks.AddRange(value);
        }
    }

    /// <summary>Отрезок, когда распоряжение действовало: от включения до выключения (игроком или по сроку).</summary>
    public sealed class DecreeSpan
    {
        internal DecreeSpan(string decreeId, long startedAtHours)
        {
            DecreeId = decreeId;
            StartedAtHours = startedAtHours;
            EndedAtHours = -1;
        }

        public string DecreeId { get; }
        public long StartedAtHours { get; }

        /// <summary>Когда выключено; действует до сих пор — −1.</summary>
        public long EndedAtHours { get; internal set; }

        public bool IsOpen => EndedAtHours < 0;
    }

    /// <summary>Распоряжения гильдии: включённые сейчас и когда каждое действовало.</summary>
    public sealed class DecreeBook
    {
        private readonly List<ActiveDecree> active = new List<ActiveDecree>();
        private readonly List<DecreeSpan> history = new List<DecreeSpan>();

        /// <summary>Включённые распоряжения в порядке включения.</summary>
        public IReadOnlyList<ActiveDecree> Active => active;

        /// <summary>Отрезки действия всех распоряжений с начала игры, в порядке включения.</summary>
        public IReadOnlyList<DecreeSpan> History => history;

        public bool IsActive(string decreeId) => TryGetActive(decreeId, out _);

        public bool TryGetActive(string decreeId, out ActiveDecree decree)
        {
            foreach (ActiveDecree item in active)
            {
                if (string.Equals(item.DecreeId, decreeId, StringComparison.Ordinal))
                {
                    decree = item;
                    return true;
                }
            }
            decree = null;
            return false;
        }

        /// <summary>
        /// Сколько часов из отрезка (<paramref name="fromHours"/>, <paramref name="toHours"/>] распоряжение действовало.
        /// Действующее сейчас считается до <paramref name="toHours"/>.
        /// </summary>
        public long GetActiveHours(string decreeId, long fromHours, long toHours)
        {
            long total = 0;
            foreach (DecreeSpan span in history)
            {
                if (!string.Equals(span.DecreeId, decreeId, StringComparison.Ordinal)) continue;
                long end = span.IsOpen ? toHours : Math.Min(span.EndedAtHours, toHours);
                long start = Math.Max(span.StartedAtHours, fromHours);
                if (end > start) total += end - start;
            }
            return total;
        }

        internal void Add(ActiveDecree decree, long now)
        {
            active.Add(decree);
            history.Add(new DecreeSpan(decree.DecreeId, now));
        }

        internal void Remove(ActiveDecree decree, long now)
        {
            active.Remove(decree);
            for (int i = history.Count - 1; i >= 0; i--)
            {
                if (history[i].IsOpen && string.Equals(history[i].DecreeId, decree.DecreeId, StringComparison.Ordinal))
                {
                    history[i].EndedAtHours = now;
                    break;
                }
            }
        }
    }
}
