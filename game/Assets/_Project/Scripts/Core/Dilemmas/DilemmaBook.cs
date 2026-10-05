using System;
using System.Collections.Generic;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>Чем кончилось обращение.</summary>
    public enum DilemmaStatus
    {
        /// <summary>Ждёт ответа игрока.</summary>
        Open,

        /// <summary>Игрок ответил.</summary>
        Answered,

        /// <summary>Срок вышел — сработал вариант по умолчанию.</summary>
        TimedOut,

        /// <summary>Снято без последствий: тот, кого оно касалось, ушёл из гильдии или погиб.</summary>
        Withdrawn,
    }

    /// <summary>
    /// Обращение: какая дилемма, кто обратился и кого ещё касается, что подставляется в текст, срок ответа и чем кончилось.
    /// </summary>
    public sealed class Dilemma
    {
        private readonly List<int> abandoned = new List<int>();

        internal Dilemma(int id, string definitionId, DilemmaTrigger trigger, long arrivedAtHours, long deadlineAtHours)
        {
            Id = id;
            DefinitionId = definitionId ?? throw new ArgumentNullException(nameof(definitionId));
            Trigger = trigger;
            ArrivedAtHours = arrivedAtHours;
            DeadlineAtHours = deadlineAtHours;
            OptionIndex = -1;
        }

        public int Id { get; }

        /// <summary>Id <see cref="DilemmaDefinition"/>.</summary>
        public string DefinitionId { get; }

        public DilemmaTrigger Trigger { get; }

        /// <summary>Кто обратился (<c>{имя}</c>; в ссоре из-за добычи — жадный или утаивший). 0 — обращение персонала.</summary>
        public int SubjectId { get; internal set; }

        /// <summary>Второй участник (<c>{напарник}</c>); 0 — нет.</summary>
        public int PartnerId { get; internal set; }

        /// <summary>Сотрудник, который обратился (обращение персонала); 0 — нет.</summary>
        public int StaffId { get; internal set; }

        /// <summary>Задание, после которого обращение (беглец, ссора из-за добычи); 0 — нет.</summary>
        public int QuestRunId { get; internal set; }

        /// <summary>Место задания для <c>{место}</c>; <c>null</c> — нет.</summary>
        public NounForms Place { get; internal set; }

        /// <summary>Запрошенная сумма (просьба в долг) для <c>{сумма}</c> в тексте; 0 — нет.</summary>
        public int Amount { get; internal set; }

        /// <summary>Текст <c>{причина}</c>; пусто — нет.</summary>
        public string Reason { get; internal set; } = string.Empty;

        /// <summary>Кого бросил беглец (для «Беглец вернулся»).</summary>
        public IReadOnlyList<int> Abandoned => abandoned;

        public long ArrivedAtHours { get; }

        /// <summary>До какого часа ждёт ответа; в этот час срабатывает вариант по умолчанию.</summary>
        public long DeadlineAtHours { get; }

        public DilemmaStatus Status { get; internal set; }

        public bool IsOpen => Status == DilemmaStatus.Open;

        /// <summary>Выбранный вариант (ответ или по умолчанию); −1 — открыто или снято.</summary>
        public int OptionIndex { get; internal set; }

        /// <summary>Когда закрыто; 0 — открыто.</summary>
        public long ClosedAtHours { get; internal set; }

        /// <summary>Касается человека: обратился сам или второй участник.</summary>
        public bool IsAbout(int adventurerId) => adventurerId != 0 && (SubjectId == adventurerId || PartnerId == adventurerId);

        internal void SetAbandoned(IEnumerable<int> ids)
        {
            abandoned.Clear();
            abandoned.AddRange(ids);
        }
    }

    /// <summary>
    /// Часть мира: обращения. Открытые — по порядку появления; закрытые — все с начала игры (для отчёта месяца, сводки и истории
    /// ответов); когда какая дилемма приходила к человеку (перезарядка); отложенный праздник в таверне.
    /// </summary>
    public sealed class DilemmaBook
    {
        private readonly List<Dilemma> open = new List<Dilemma>();
        private readonly List<Dilemma> closed = new List<Dilemma>();
        private readonly Dictionary<(string, int), long> lastArrivals = new Dictionary<(string, int), long>();
        private int nextId = 1;

        /// <summary>Ждут ответа, по порядку появления.</summary>
        public IReadOnlyList<Dilemma> Open => open;

        /// <summary>Закрытые с начала игры, по порядку закрытия.</summary>
        public IReadOnlyList<Dilemma> Closed => closed;

        /// <summary>Сколько обращений пришло за игру.</summary>
        public int ArrivedCount => nextId - 1;

        /// <summary>Праздник, который пройдёт в ближайшие итоги вечера: обращение; 0 — не ждём.</summary>
        public int PendingFeastId { get; internal set; }

        /// <summary>Вариант праздника (индекс в вариантах дилеммы).</summary>
        public int PendingFeastOption { get; internal set; } = -1;

        public bool TryGetDilemma(int id, out Dilemma dilemma)
        {
            foreach (Dilemma item in open)
            {
                if (item.Id != id) continue;
                dilemma = item;
                return true;
            }
            foreach (Dilemma item in closed)
            {
                if (item.Id != id) continue;
                dilemma = item;
                return true;
            }
            dilemma = null;
            return false;
        }

        public bool TryGetOpen(int id, out Dilemma dilemma)
        {
            foreach (Dilemma item in open)
            {
                if (item.Id != id) continue;
                dilemma = item;
                return true;
            }
            dilemma = null;
            return false;
        }

        /// <summary>Ждёт ли ответа обращение с этим триггером, которое касается человека (обратился сам или второй участник).</summary>
        public bool IsAwaiting(DilemmaTrigger trigger, int adventurerId)
        {
            foreach (Dilemma item in open)
            {
                if (item.Trigger == trigger && item.IsAbout(adventurerId)) return true;
            }
            return false;
        }

        /// <summary>Есть ли открытое обращение этой дилеммы (для обращений на всю гильдию).</summary>
        public bool IsOpen(string definitionId)
        {
            foreach (Dilemma item in open)
            {
                if (string.Equals(item.DefinitionId, definitionId, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        /// <summary>Когда эта дилемма последний раз приходила к человеку (0 — к гильдии); <c>false</c> — не приходила.</summary>
        public bool TryGetLastArrival(string definitionId, int adventurerId, out long atHours) =>
            lastArrivals.TryGetValue((definitionId, adventurerId), out atHours);

        internal int NextId() => nextId++;

        internal void Add(Dilemma dilemma)
        {
            open.Add(dilemma);
            lastArrivals[(dilemma.DefinitionId, dilemma.SubjectId)] = dilemma.ArrivedAtHours;
            if (dilemma.PartnerId != 0) lastArrivals[(dilemma.DefinitionId, dilemma.PartnerId)] = dilemma.ArrivedAtHours;
        }

        internal void Close(Dilemma dilemma)
        {
            if (open.Remove(dilemma)) closed.Add(dilemma);
        }
    }
}
