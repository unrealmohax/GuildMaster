using System;
using System.Collections.Generic;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Часть мира: отношения между людьми (ТЗ 04 → «Отношения»). Пара симметрична: <c>(a, b)</c> и <c>(b, a)</c> — одна запись.
    /// Нет записи — отношения 0 и ни одного совместного задания. Менять — <see cref="RelationService"/>.
    /// </summary>
    public sealed class RelationBook
    {
        private readonly Dictionary<long, Relation> byPair = new Dictionary<long, Relation>();
        private readonly List<Relation> all = new List<Relation>();

        internal RelationBook()
        {
        }

        /// <summary>Все пары в порядке появления.</summary>
        public IReadOnlyList<Relation> All => all;

        public bool TryGetRelation(int a, int b, out Relation relation) => byPair.TryGetValue(Key(a, b), out relation);

        public float GetValue(int a, int b) => TryGetRelation(a, b, out Relation relation) ? relation.Value : 0f;

        public int GetJointQuests(int a, int b) => TryGetRelation(a, b, out Relation relation) ? relation.JointQuests : 0;

        internal Relation GetOrCreate(int a, int b)
        {
            long key = Key(a, b);
            if (!byPair.TryGetValue(key, out Relation relation))
            {
                relation = new Relation(Math.Min(a, b), Math.Max(a, b));
                byPair.Add(key, relation);
                all.Add(relation);
            }
            return relation;
        }

        private static long Key(int a, int b)
        {
            if (a == b) throw new ArgumentException($"Relation of adventurer {a} with oneself");
            int low = Math.Min(a, b);
            int high = Math.Max(a, b);
            return ((long)low << 32) | (uint)high;
        }
    }

    /// <summary>Отношения пары: число −100..+100 и счётчик совместных заданий. <see cref="A"/> &lt; <see cref="B"/>.</summary>
    public sealed class Relation
    {
        internal Relation(int a, int b)
        {
            A = a;
            B = b;
        }

        public int A { get; }
        public int B { get; }
        public float Value { get; internal set; }
        public int JointQuests { get; internal set; }

        public int GetOther(int id) => id == A ? B : id == B ? A : throw new ArgumentException($"{id} is not in relation {A}–{B}");
    }

    /// <summary>Метки отношений (ТЗ 04). Пороги — <see cref="AdventurersBalance"/>.</summary>
    [Flags]
    public enum RelationLabels
    {
        None = 0,

        /// <summary>Отношения ≥ <c>friendsThreshold</c> (40).</summary>
        Friends = 1,

        /// <summary>Отношения ≤ <c>dislikeThreshold</c> (−40).</summary>
        Dislike = 2,

        /// <summary>Совместных заданий ≥ <c>longPartnersQuests</c> (10).</summary>
        LongPartners = 4,
    }
}
