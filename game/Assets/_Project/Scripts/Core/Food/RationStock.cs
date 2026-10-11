using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Запас походных пайков: дешёвые и обычные. Личный — у человека, общий — у постоянной группы. Пайки не портятся.
    /// Часть мира: снаружи Core только чтение.
    /// </summary>
    public sealed class RationStock
    {
        private readonly int[] counts = new int[Vocabulary.RationTierCount];

        /// <summary>Сколько пайков этого типа.</summary>
        public int GetCount(RationTier tier) => counts[(int)tier];

        public int Cheap => counts[(int)RationTier.Cheap];
        public int Regular => counts[(int)RationTier.Regular];
        public int Total => Cheap + Regular;
        public bool IsEmpty => Total == 0;

        internal void Add(RationTier tier, int count)
        {
            if (count > 0) counts[(int)tier] += count;
        }

        /// <summary>Взять один паёк: сначала дешёвый, потом обычный. <c>null</c> — запас пуст.</summary>
        internal RationTier? TakeOne()
        {
            for (int i = 0; i < counts.Length; i++)
            {
                if (counts[i] <= 0) continue;
                counts[i]--;
                return (RationTier)i;
            }
            return null;
        }

        /// <summary>Убрать <paramref name="count"/> пайков (сначала дешёвые); вернуть, сколько убрано.</summary>
        internal int Remove(int count)
        {
            int removed = 0;
            for (int i = 0; i < counts.Length && removed < count; i++)
            {
                int take = System.Math.Min(counts[i], count - removed);
                counts[i] -= take;
                removed += take;
            }
            return removed;
        }

        /// <summary>Переложить весь запас в <paramref name="target"/>.</summary>
        internal void MoveAllTo(RationStock target)
        {
            for (int i = 0; i < counts.Length; i++)
            {
                target.counts[i] += counts[i];
                counts[i] = 0;
            }
        }

        /// <summary>Переложить в <paramref name="target"/> долю 1/<paramref name="parts"/> каждого типа (вниз).</summary>
        internal void MoveShareTo(RationStock target, int parts)
        {
            if (parts < 1) return;
            for (int i = 0; i < counts.Length; i++)
            {
                int share = counts[i] / parts;
                target.counts[i] += share;
                counts[i] -= share;
            }
        }

        internal void Clear()
        {
            for (int i = 0; i < counts.Length; i++) counts[i] = 0;
        }
    }
}
