using System.Collections.Generic;

namespace GuildMaster.Core
{
    /// <summary>Кризис по календарю.</summary>
    public enum CrisisKind
    {
        /// <summary>Набег: заказ «Отразить набег» на доске; не выполнен — нападение на поселение.</summary>
        Raid,

        /// <summary>Налог лорда: списывается из казны в конце квартала.</summary>
        Tax,

        /// <summary>Зима: еда, жильё и спад Безопасности дороже, заказов меньше.</summary>
        Winter,

        /// <summary>Мор: убывает население, таверна теряет жителей. Приходит без предупреждения.</summary>
        Plague,
    }

    /// <summary>Стадия кризиса.</summary>
    public enum CrisisStatus
    {
        /// <summary>Запланирован, о нём ещё не объявлено.</summary>
        Planned,

        /// <summary>Объявлено заранее: ждёт начала.</summary>
        Warned,

        /// <summary>Идёт.</summary>
        Active,

        /// <summary>Кончился.</summary>
        Over,
    }

    /// <summary>Чем кончился кризис.</summary>
    public enum CrisisOutcome
    {
        None,

        /// <summary>Набег отбит.</summary>
        Won,

        /// <summary>Набег не отбит: заказ снят по сроку или задание не выполнено.</summary>
        Lost,

        /// <summary>Налог списан, казна осталась не в минусе.</summary>
        Paid,

        /// <summary>Налог увёл казну в минус: лорд недоволен.</summary>
        Unpaid,

        /// <summary>Зима или мор прошли.</summary>
        Passed,
    }

    /// <summary>
    /// Один кризис: вид, год, когда объявить, начать и кончить, стадия, итог. У налога — сумма (в объявлении — по доходам на тот
    /// момент, при списании — окончательная), у набега — заказ.
    /// </summary>
    public sealed class Crisis
    {
        internal Crisis(int id, CrisisKind kind, int year, long warnAtHours, long startsAtHours, long endsAtHours)
        {
            Id = id;
            Kind = kind;
            Year = year;
            WarnAtHours = warnAtHours;
            StartsAtHours = startsAtHours;
            EndsAtHours = endsAtHours;
        }

        public int Id { get; }
        public CrisisKind Kind { get; }

        /// <summary>Год игры, к которому кризис относится (налог за последний квартал года списывается уже в следующем).</summary>
        public int Year { get; }

        /// <summary>Когда объявить; у мора объявления нет.</summary>
        public long WarnAtHours { get; }

        /// <summary>Когда начинается: набег — заказ на доске, налог — списание, зима и мор — начало.</summary>
        public long StartsAtHours { get; internal set; }

        /// <summary>Когда кончается: зима и мор — конец, набег — срок заказа на доске, налог — час списания.</summary>
        public long EndsAtHours { get; internal set; }

        public CrisisStatus Status { get; internal set; }
        public CrisisOutcome Outcome { get; internal set; }

        /// <summary>Налог: сумма (объявленная или списанная).</summary>
        public int Amount { get; internal set; }

        /// <summary>Набег: заказ «Отразить набег»; 0 — ещё нет.</summary>
        public int OrderId { get; internal set; }

        /// <summary>Мор: сколько недельных убылей населения уже было.</summary>
        public int WeeksApplied { get; internal set; }

        public bool IsActive => Status == CrisisStatus.Active;
        public bool IsOver => Status == CrisisStatus.Over;

        /// <summary>О кризисе знают (объявлен, идёт или кончился). Запланированный мор не виден до начала.</summary>
        public bool IsKnown => Status != CrisisStatus.Planned;
    }

    /// <summary>Итоги кризисов за игру — для отчёта и сводки.</summary>
    public readonly struct CrisisTotals
    {
        public CrisisTotals(int raidsWon, int raidsLost, int taxPaid, int taxesUnpaid, int plagueDays)
        {
            RaidsWon = raidsWon;
            RaidsLost = raidsLost;
            TaxPaid = taxPaid;
            TaxesUnpaid = taxesUnpaid;
            PlagueDays = plagueDays;
        }

        public int RaidsWon { get; }
        public int RaidsLost { get; }

        /// <summary>Сколько налога списано за игру.</summary>
        public int TaxPaid { get; }

        /// <summary>Сколько раз налог увёл казну в минус.</summary>
        public int TaxesUnpaid { get; }

        /// <summary>Сколько суток шёл мор.</summary>
        public int PlagueDays { get; }

        internal CrisisTotals AddRaid(bool won) => new CrisisTotals(RaidsWon + (won ? 1 : 0), RaidsLost + (won ? 0 : 1), TaxPaid, TaxesUnpaid, PlagueDays);
        internal CrisisTotals AddTax(int amount, bool unpaid) => new CrisisTotals(RaidsWon, RaidsLost, TaxPaid + amount, TaxesUnpaid + (unpaid ? 1 : 0), PlagueDays);
        internal CrisisTotals AddPlagueDay() => new CrisisTotals(RaidsWon, RaidsLost, TaxPaid, TaxesUnpaid, PlagueDays + 1);
    }

    /// <summary>
    /// Кризисы по календарю: запланированные на год вперёд, идущие и прошедшие (в порядке начала), до какого года расписание
    /// составлено, итоги. Меняет только <see cref="CrisisService"/>.
    /// </summary>
    public sealed class CrisisBook
    {
        private readonly List<Crisis> all = new List<Crisis>();
        private int nextId = 1;

        internal CrisisBook()
        {
        }

        /// <summary>Все кризисы: прошедшие, идущие и запланированные — в порядке начала.</summary>
        public IReadOnlyList<Crisis> All => all;

        /// <summary>На какой год уже составлено расписание; 0 — ни на какой.</summary>
        public int PlannedYear { get; internal set; }

        public CrisisTotals Totals { get; internal set; }

        /// <summary>Идёт кризис этого вида.</summary>
        public bool IsActive(CrisisKind kind) => TryGetActive(kind, out _);

        public bool TryGetActive(CrisisKind kind, out Crisis crisis)
        {
            foreach (Crisis entry in all)
            {
                if (entry.Kind == kind && entry.IsActive)
                {
                    crisis = entry;
                    return true;
                }
            }
            crisis = null;
            return false;
        }

        /// <summary>Кризис по заказу набега.</summary>
        public bool TryGetByOrder(int orderId, out Crisis crisis)
        {
            foreach (Crisis entry in all)
            {
                if (entry.Kind == CrisisKind.Raid && entry.OrderId == orderId && orderId != 0)
                {
                    crisis = entry;
                    return true;
                }
            }
            crisis = null;
            return false;
        }

        /// <summary>Ближайший объявленный кризис, который ещё не начался; нет — <c>null</c>.</summary>
        public Crisis GetNextWarned()
        {
            Crisis next = null;
            foreach (Crisis entry in all)
            {
                if (entry.Status == CrisisStatus.Warned && (next == null || entry.StartsAtHours < next.StartsAtHours)) next = entry;
            }
            return next;
        }

        internal Crisis Add(CrisisKind kind, int year, long warnAtHours, long startsAtHours, long endsAtHours)
        {
            var crisis = new Crisis(nextId++, kind, year, warnAtHours, startsAtHours, endsAtHours);
            int at = all.Count;
            while (at > 0 && all[at - 1].StartsAtHours > startsAtHours) at--;
            all.Insert(at, crisis);
            return crisis;
        }
    }
}
