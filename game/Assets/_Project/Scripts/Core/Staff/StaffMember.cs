using System;
using System.Collections.Generic;

namespace GuildMaster.Core
{
    /// <summary>Почему сотрудник больше не служит гильдии.</summary>
    public enum StaffLeaveReason
    {
        None,

        /// <summary>Уволен игроком.</summary>
        Dismissed,

        /// <summary>Ушёл сам: не дождался жалованья.</summary>
        Quit,
    }

    /// <summary>
    /// Сотрудник гильдии: живой человек с именем, должность, уровень возможностей и зарплата. Черт у персонала нет.
    /// Долг по зарплате — сумма невыплаченного и сколько платёжных сроков он висит.
    /// </summary>
    public sealed class StaffMember
    {
        internal StaffMember(int id, string name, Gender gender, string roleId, int level, int salary, long hiredAtHours)
        {
            Id = id;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Gender = gender;
            RoleId = roleId ?? throw new ArgumentNullException(nameof(roleId));
            Level = level;
            Salary = salary;
            HiredAtHours = hiredAtHours;
        }

        public int Id { get; }
        public string Name { get; }
        public Gender Gender { get; }
        public string RoleId { get; }

        /// <summary>Уровень возможностей: от него эффект должности.</summary>
        public int Level { get; internal set; }

        /// <summary>Зарплата в месяц, о которой договорились.</summary>
        public int Salary { get; internal set; }

        /// <summary>Сколько гильдия должна по зарплате; 0 — долга нет.</summary>
        public int UnpaidSalary { get; internal set; }

        /// <summary>Сколько платёжных сроков подряд зарплата не выплачена (долг ещё висит).</summary>
        public int UnpaidMonths { get; internal set; }

        public long HiredAtHours { get; }
        public long LeftAtHours { get; internal set; }
        public StaffLeaveReason LeaveReason { get; internal set; }
    }

    /// <summary>Кандидат в персонал: ждёт предложения о зарплате до <see cref="ExpiresAtHours"/>.</summary>
    public sealed class StaffCandidate
    {
        internal StaffCandidate(int id, string name, Gender gender, string roleId, int level, int askedSalary, long arrivedAtHours, long expiresAtHours)
        {
            Id = id;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Gender = gender;
            RoleId = roleId ?? throw new ArgumentNullException(nameof(roleId));
            Level = level;
            AskedSalary = askedSalary;
            ArrivedAtHours = arrivedAtHours;
            ExpiresAtHours = expiresAtHours;
        }

        public int Id { get; }
        public string Name { get; }
        public Gender Gender { get; }
        public string RoleId { get; }
        public int Level { get; }

        /// <summary>Сколько просит в месяц.</summary>
        public int AskedSalary { get; }

        public long ArrivedAtHours { get; }
        public long ExpiresAtHours { get; }
    }

    /// <summary>
    /// Персонал гильдии: кто служит (в порядке найма), кто ушёл, кандидаты на вакансии. У сотрудников и кандидатов — свой счёт
    /// id, отдельный от людей и заказов. Здесь же — когда на вакансию придут следующие кандидаты.
    /// </summary>
    public sealed class StaffRoster
    {
        private readonly List<StaffMember> members = new List<StaffMember>();
        private readonly List<StaffMember> former = new List<StaffMember>();
        private readonly List<StaffCandidate> candidates = new List<StaffCandidate>();
        private readonly Dictionary<string, long> nextCandidatesAtHours = new Dictionary<string, long>(StringComparer.Ordinal);
        private int nextId = 1;

        internal StaffRoster()
        {
        }

        /// <summary>Служат гильдии, в порядке найма.</summary>
        public IReadOnlyList<StaffMember> Members => members;

        /// <summary>Ушедшие и уволенные, в порядке ухода.</summary>
        public IReadOnlyList<StaffMember> Former => former;

        /// <summary>Кандидаты на вакансии, в порядке прихода.</summary>
        public IReadOnlyList<StaffCandidate> Candidates => candidates;

        public bool TryGetMember(int id, out StaffMember member) => TryFind(members, id, out member);

        /// <summary>Сотрудник — служащий или бывший.</summary>
        public bool TryGetKnown(int id, out StaffMember member) => TryFind(members, id, out member) || TryFind(former, id, out member);

        /// <summary>Кто служит на этой должности.</summary>
        public bool TryGetByRole(string roleId, out StaffMember member)
        {
            foreach (StaffMember entry in members)
            {
                if (entry.RoleId == roleId)
                {
                    member = entry;
                    return true;
                }
            }
            member = null;
            return false;
        }

        public bool HasRole(string roleId) => TryGetByRole(roleId, out _);

        public bool TryGetCandidate(int id, out StaffCandidate candidate)
        {
            foreach (StaffCandidate entry in candidates)
            {
                if (entry.Id == id)
                {
                    candidate = entry;
                    return true;
                }
            }
            candidate = null;
            return false;
        }

        /// <summary>Когда на вакансию этой должности придут следующие кандидаты; нет записи — вакансия ещё не ждёт.</summary>
        public bool TryGetNextCandidatesAt(string roleId, out long hours) => nextCandidatesAtHours.TryGetValue(roleId, out hours);

        internal int NextId() => nextId++;

        internal void AddMember(StaffMember member) => members.Add(member);

        internal void MoveToFormer(StaffMember member)
        {
            if (!members.Remove(member)) throw new InvalidOperationException($"Staff member {member.Id} is not serving");
            former.Add(member);
        }

        internal void AddCandidate(StaffCandidate candidate) => candidates.Add(candidate);

        internal void RemoveCandidate(StaffCandidate candidate) => candidates.Remove(candidate);

        internal void SetNextCandidatesAt(string roleId, long hours) => nextCandidatesAtHours[roleId] = hours;

        internal void ClearNextCandidatesAt(string roleId) => nextCandidatesAtHours.Remove(roleId);

        private static bool TryFind(List<StaffMember> list, int id, out StaffMember member)
        {
            foreach (StaffMember entry in list)
            {
                if (entry.Id == id)
                {
                    member = entry;
                    return true;
                }
            }
            member = null;
            return false;
        }
    }
}
