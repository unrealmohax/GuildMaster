using System.Collections.Generic;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>Флаг памяти: что было, когда, с кем (второй человек; 0 — ни с кем) и число, если флагу оно нужно.</summary>
    public sealed class MemoryEntry
    {
        internal MemoryEntry(MemoryFlag flag, long atHours, int otherId, float value)
        {
            Flag = flag;
            AtHours = atHours;
            OtherId = otherId;
            Value = value;
        }

        public MemoryFlag Flag { get; }
        public long AtHours { get; internal set; }

        /// <summary>С кем связан флаг (Влюблённые — партнёр); 0 — ни с кем.</summary>
        public int OtherId { get; }

        /// <summary>Число флага (прибавка к оценке напарника); у большинства флагов — 0.</summary>
        public float Value { get; internal set; }
    }

    /// <summary>
    /// Память человека: флаги того, что с ним было (брал в долг, сбежал, прощён, наказан…). Цепочки событий читают их,
    /// чтобы прошлое влияло на будущее. Один флаг на пару «флаг + второй человек»; повторная запись обновляет время и число.
    /// </summary>
    public sealed class AdventurerMemory
    {
        private readonly List<MemoryEntry> entries = new List<MemoryEntry>();

        public IReadOnlyList<MemoryEntry> Entries => entries;

        public bool HasFlag(MemoryFlag flag) => TryGetFlag(flag, out _);

        public bool HasFlag(MemoryFlag flag, int otherId) => TryGetFlag(flag, otherId, out _);

        /// <summary>Первый флаг этого вида, с кем бы он ни был.</summary>
        public bool TryGetFlag(MemoryFlag flag, out MemoryEntry entry)
        {
            foreach (MemoryEntry item in entries)
            {
                if (item.Flag != flag) continue;
                entry = item;
                return true;
            }
            entry = null;
            return false;
        }

        public bool TryGetFlag(MemoryFlag flag, int otherId, out MemoryEntry entry)
        {
            foreach (MemoryEntry item in entries)
            {
                if (item.Flag != flag || item.OtherId != otherId) continue;
                entry = item;
                return true;
            }
            entry = null;
            return false;
        }

        internal void Set(MemoryFlag flag, long atHours, int otherId = 0, float value = 0f)
        {
            if (TryGetFlag(flag, otherId, out MemoryEntry existing))
            {
                existing.AtHours = atHours;
                existing.Value = value;
                return;
            }
            entries.Add(new MemoryEntry(flag, atHours, otherId, value));
        }

        internal bool Remove(MemoryFlag flag) => entries.RemoveAll(e => e.Flag == flag) > 0;
    }
}
