using System.Collections.Generic;

namespace GuildMaster.Core
{
    /// <summary>
    /// Группа: под задание (собрал инициатор на один заказ, распадается после задания) или постоянная (с названием,
    /// ходит вместе, делит награду поровну). Часть мира: снаружи Core только чтение.
    /// </summary>
    public sealed class Party
    {
        private readonly List<int> memberIds = new List<int>();
        private readonly Dictionary<int, int> questsTogether = new Dictionary<int, int>();
        private readonly Dictionary<int, int> guestSuccesses = new Dictionary<int, int>();

        internal Party(int id, bool isPermanent, int initiatorId)
        {
            Id = id;
            IsPermanent = isPermanent;
            InitiatorId = initiatorId;
        }

        public int Id { get; }

        public bool IsPermanent { get; }

        /// <summary>Название постоянной группы («Серые волки»); у группы под задание — пусто.</summary>
        public string Name { get; internal set; } = string.Empty;

        /// <summary>Члены, по порядку вступления (у группы под задание инициатор — первый).</summary>
        public IReadOnlyList<int> MemberIds => memberIds;

        /// <summary>Кто собрал группу под задание; у постоянной — кто первым в ней был.</summary>
        public int InitiatorId { get; internal set; }

        /// <summary>Под какой заказ (у постоянной — текущий); 0 — ни под какой.</summary>
        public int OrderId { get; internal set; }

        /// <summary>Совместных заданий постоянной группы.</summary>
        public int JointQuests { get; internal set; }

        /// <summary>Из них выполнено.</summary>
        public int JointSuccesses { get; internal set; }

        /// <summary>Когда постоянная группа сложилась.</summary>
        public long FormedAtHours { get; internal set; }

        /// <summary>Сутки, когда постоянная группа уже решала, куда идти; −1 — ещё не решала.</summary>
        internal long DecidedDay { get; set; } = -1;

        public bool HasMember(int id) => memberIds.Contains(id);

        /// <summary>Сколько заданий человек сходил с группой, будучи её членом.</summary>
        public int GetQuestsTogether(int id) => questsTogether.TryGetValue(id, out int count) ? count : 0;

        /// <summary>Сколько успешных заданий гость сходил с постоянной группой.</summary>
        public int GetGuestSuccesses(int id) => guestSuccesses.TryGetValue(id, out int count) ? count : 0;

        internal void AddMember(int id)
        {
            if (memberIds.Contains(id)) return;
            memberIds.Add(id);
            questsTogether[id] = 0;
            guestSuccesses.Remove(id);
        }

        internal bool RemoveMember(int id)
        {
            questsTogether.Remove(id);
            return memberIds.Remove(id);
        }

        internal void AddQuestTogether(int id) => questsTogether[id] = GetQuestsTogether(id) + 1;

        internal int AddGuestSuccess(int id) => guestSuccesses[id] = GetGuestSuccesses(id) + 1;
    }

    /// <summary>
    /// Часть мира: группы. Идущие и собранные под задание, постоянные — по порядку появления; счётчики постоянных групп за игру;
    /// занятые названия постоянных групп.
    /// </summary>
    public sealed class PartyBook
    {
        private readonly List<Party> active = new List<Party>();
        private readonly HashSet<string> usedNames = new HashSet<string>();
        private int nextId = 1;

        /// <summary>Группы сейчас: под задание (пока идёт задание) и постоянные.</summary>
        public IReadOnlyList<Party> Active => active;

        /// <summary>Постоянных групп сложилось за игру.</summary>
        public int PermanentFormed { get; internal set; }

        /// <summary>Постоянных групп распалось за игру.</summary>
        public int PermanentDisbanded { get; internal set; }

        /// <summary>Постоянных групп сейчас.</summary>
        public int GetPermanentCount()
        {
            int count = 0;
            foreach (Party party in active)
            {
                if (party.IsPermanent) count++;
            }
            return count;
        }

        public bool TryGetParty(int id, out Party party)
        {
            foreach (Party candidate in active)
            {
                if (candidate.Id != id) continue;
                party = candidate;
                return true;
            }
            party = null;
            return false;
        }

        /// <summary>Название постоянной группы уже занято (было у какой-то группы за игру).</summary>
        public bool IsNameUsed(string name) => usedNames.Contains(name);

        internal Party Create(bool permanent, int initiatorId)
        {
            var party = new Party(nextId++, permanent, initiatorId);
            active.Add(party);
            return party;
        }

        internal void Remove(Party party) => active.Remove(party);

        internal void UseName(string name) => usedNames.Add(name);
    }
}
