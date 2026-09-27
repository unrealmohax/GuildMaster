using System;
using System.Collections.Generic;

namespace GuildMaster.Core
{
    /// <summary>
    /// Часть мира: люди гильдии. Активные, архив ушедших и погибших (для отчёта месяца и лент), кандидаты,
    /// ждущие ответа игрока. Меняется только системами и командами Core.
    /// </summary>
    public sealed class AdventurerRoster
    {
        private readonly List<Adventurer> active = new List<Adventurer>();
        private readonly List<Adventurer> archive = new List<Adventurer>();
        private readonly List<Candidate> candidates = new List<Candidate>();

        internal AdventurerRoster()
        {
        }

        /// <summary>В гильдии, в порядке вступления.</summary>
        public IReadOnlyList<Adventurer> Active => active;

        /// <summary>Ушедшие, погибшие, пропавшие, изгнанные — в порядке ухода.</summary>
        public IReadOnlyList<Adventurer> Archive => archive;

        /// <summary>Кандидаты в авантюристы, ждущие ответа, в порядке прихода.</summary>
        public IReadOnlyList<Candidate> Candidates => candidates;

        /// <summary>Сколько людей считается в лимите гильдии: активные и ожидающие кандидаты.</summary>
        public int HeadCount => active.Count + candidates.Count;

        public bool IsActive(int id) => TryGetActive(id, out _);

        public bool TryGetActive(int id, out Adventurer adventurer) => TryFind(active, id, out adventurer);

        public Adventurer GetActive(int id) =>
            TryGetActive(id, out Adventurer adventurer) ? adventurer : throw new KeyNotFoundException($"No active adventurer {id}");

        /// <summary>Активный или из архива — для строк о давно ушедших.</summary>
        public bool TryGetKnown(int id, out Adventurer adventurer) =>
            TryFind(active, id, out adventurer) || TryFind(archive, id, out adventurer);

        public bool TryGetCandidate(int id, out Candidate candidate)
        {
            foreach (Candidate entry in candidates)
            {
                if (entry.Adventurer.Id == id)
                {
                    candidate = entry;
                    return true;
                }
            }
            candidate = null;
            return false;
        }

        internal void AddActive(Adventurer adventurer)
        {
            if (adventurer == null) throw new ArgumentNullException(nameof(adventurer));
            if (IsActive(adventurer.Id)) throw new InvalidOperationException($"Adventurer {adventurer.Id} is already active");
            active.Add(adventurer);
        }

        internal void MoveToArchive(Adventurer adventurer)
        {
            if (!active.Remove(adventurer)) throw new InvalidOperationException($"Adventurer {adventurer.Id} is not active");
            archive.Add(adventurer);
        }

        internal void AddCandidate(Candidate candidate) => candidates.Add(candidate ?? throw new ArgumentNullException(nameof(candidate)));

        internal bool RemoveCandidate(Candidate candidate) => candidates.Remove(candidate);

        private static bool TryFind(List<Adventurer> list, int id, out Adventurer adventurer)
        {
            foreach (Adventurer entry in list)
            {
                if (entry.Id == id)
                {
                    adventurer = entry;
                    return true;
                }
            }
            adventurer = null;
            return false;
        }
    }

    /// <summary>
    /// Кандидат в авантюристы: уже сгенерированный человек, ждущий ответа игрока.
    /// Черты скрыты, как у всех. Без ответа уходит в <see cref="ExpiresAtHours"/>.
    /// </summary>
    public sealed class Candidate
    {
        internal Candidate(Adventurer adventurer, long arrivedAtHours, long expiresAtHours)
        {
            Adventurer = adventurer ?? throw new ArgumentNullException(nameof(adventurer));
            ArrivedAtHours = arrivedAtHours;
            ExpiresAtHours = expiresAtHours;
        }

        public Adventurer Adventurer { get; }
        public long ArrivedAtHours { get; }
        public long ExpiresAtHours { get; }
    }
}
