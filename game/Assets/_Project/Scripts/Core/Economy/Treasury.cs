using System.Collections.Generic;

namespace GuildMaster.Core
{
    /// <summary>
    /// Казна гильдии (часть мира): деньги, журнал операций, комиссия, банкротство. Менять — только
    /// <see cref="TreasuryService"/> (деньги и журнал), командой <see cref="SetCommissionCommand"/> (комиссия)
    /// и <see cref="EconomySystem"/> (банкротство, закрытие гильдии).
    /// Инвариант: <see cref="StartMoney"/> + сумма журнала = <see cref="Money"/>.
    /// </summary>
    public sealed class Treasury
    {
        private readonly List<LedgerEntry> ledger = new List<LedgerEntry>();

        internal Treasury()
        {
        }

        /// <summary>Деньги гильдии; могут быть отрицательными.</summary>
        public int Money { get; internal set; }

        /// <summary>Деньги на старте игры — начальный остаток, не запись журнала.</summary>
        public int StartMoney { get; private set; }

        /// <summary>Журнал операций, от старых к новым.</summary>
        public IReadOnlyList<LedgerEntry> Ledger => ledger;

        /// <summary>Доля гильдии с наград заказов, 0..1.</summary>
        public float Commission { get; internal set; }

        public Bankruptcy Bankruptcy { get; } = new Bankruptcy();

        /// <summary>Гильдия закрыта (поражение): симуляция остановлена.</summary>
        public bool IsClosed { get; internal set; }

        public long ClosedAtHours { get; internal set; }

        /// <summary>Порций в таверне (еда и выпивка) с прошлого расчёта дохода таверны.</summary>
        internal int TavernFood { get; set; }

        internal int TavernDrinks { get; set; }

        /// <summary>Дробный остаток дохода таверны, ещё не зачисленный в целых монетах.</summary>
        internal double TavernRemainder { get; set; }

        /// <summary>Старт игры: деньги, комиссия; стартовый минус считается с начала игры.</summary>
        internal void Initialize(int startMoney, float commission, long nowHours)
        {
            StartMoney = startMoney;
            Money = startMoney;
            Commission = commission;
            Bankruptcy.NegativeSinceHours = startMoney < 0 ? nowHours : (long?)null;
        }

        internal void Add(LedgerEntry entry) => ledger.Add(entry);
    }

    /// <summary>
    /// Банкротство: с какого часа казна в минусе, идёт ли банкротство и когда истекает срок на исправление.
    /// </summary>
    public sealed class Bankruptcy
    {
        internal Bankruptcy()
        {
        }

        /// <summary>С какого часа казна отрицательна; <c>null</c> — казна не в минусе.</summary>
        public long? NegativeSinceHours { get; internal set; }

        public bool Active { get; internal set; }

        public long StartedAtHours { get; internal set; }

        /// <summary>Срок на исправление: казна в минусе в этот час — гильдия закрывается.</summary>
        public long EndsAtHours { get; internal set; }
    }

    /// <summary>Операция журнала казны. Сумма со знаком: доход — плюс, расход — минус.</summary>
    public sealed class LedgerEntry
    {
        internal LedgerEntry(long timeHours, LedgerCategory category, int amount, string comment, int relatedId, int balanceAfter)
        {
            TimeHours = timeHours;
            Category = category;
            Amount = amount;
            Comment = comment ?? string.Empty;
            RelatedId = relatedId;
            BalanceAfter = balanceAfter;
        }

        public long TimeHours { get; }
        public LedgerCategory Category { get; }
        public int Amount { get; }
        public string Comment { get; }

        /// <summary>Связанный объект: человек, заказ, постройка…; 0 — нет.</summary>
        public int RelatedId { get; }

        /// <summary>Казна после операции.</summary>
        public int BalanceAfter { get; }
    }
}
