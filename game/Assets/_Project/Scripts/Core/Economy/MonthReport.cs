using System.Collections.Generic;

namespace GuildMaster.Core
{
    /// <summary>
    /// Отчёт за прошедший месяц: период, казна на начало и конец, записи журнала за период, репутация и счётчики заказов
    /// на конец (с них начинается следующий отчёт) и разделы. Раздел — заголовок
    /// и строки (<see cref="MonthReportSections"/>). Тексты — ключи шаблонов; строками их собирает <see cref="MonthReportText"/>.
    /// </summary>
    public sealed class MonthReport
    {
        internal MonthReport(int year, int month, long fromHours, long toHours, int moneyAtStart, int moneyAtEnd, int ledgerFrom, int ledgerTo,
            int income, int expense, float reputationAtEnd, OrderTotals ordersAtEnd, IReadOnlyList<ReportSection> sections)
        {
            Year = year;
            Month = month;
            FromHours = fromHours;
            ToHours = toHours;
            MoneyAtStart = moneyAtStart;
            MoneyAtEnd = moneyAtEnd;
            LedgerFrom = ledgerFrom;
            LedgerTo = ledgerTo;
            Income = income;
            Expense = expense;
            ReputationAtEnd = reputationAtEnd;
            OrdersAtEnd = ordersAtEnd;
            Sections = sections;
        }

        /// <summary>Год и месяц, за который отчёт.</summary>
        public int Year { get; }

        public int Month { get; }

        /// <summary>Начало периода — час прошлого отчёта (у первого — начало игры).</summary>
        public long FromHours { get; }

        /// <summary>Час, когда отчёт составлен.</summary>
        public long ToHours { get; }

        public int MoneyAtStart { get; }
        public int MoneyAtEnd { get; }

        /// <summary>Записи журнала казны за период: индексы с <see cref="LedgerFrom"/> до <see cref="LedgerTo"/> (не включая).</summary>
        public int LedgerFrom { get; }

        public int LedgerTo { get; }

        /// <summary>Доходы за период.</summary>
        public int Income { get; }

        /// <summary>Расходы за период (положительное число).</summary>
        public int Expense { get; }

        /// <summary>Репутация гильдии, когда отчёт составлен.</summary>
        public float ReputationAtEnd { get; }

        /// <summary>Счётчики заказов за игру, когда отчёт составлен.</summary>
        public OrderTotals OrdersAtEnd { get; }

        public IReadOnlyList<ReportSection> Sections { get; }

        public bool TryGetSection(string titleKey, out ReportSection section)
        {
            foreach (ReportSection candidate in Sections)
            {
                if (candidate.TitleKey == titleKey)
                {
                    section = candidate;
                    return true;
                }
            }
            section = null;
            return false;
        }

        /// <summary>Есть ли в отчёте строка <paramref name="lineKey"/> с этим человеком и подробностью.</summary>
        public bool HasItem(string lineKey, int personId, string detail)
        {
            foreach (ReportSection section in Sections)
            {
                foreach (ReportLine line in section.Lines)
                {
                    if (line.TextKey != lineKey) continue;
                    foreach (ReportItem item in line.Items)
                    {
                        if (item.PersonId == personId && item.Detail == detail) return true;
                    }
                }
            }
            return false;
        }
    }

    /// <summary>Раздел отчёта: ключ заголовка и строки.</summary>
    public sealed class ReportSection
    {
        internal ReportSection(string titleKey, IReadOnlyList<ReportLine> lines)
        {
            TitleKey = titleKey;
            Lines = lines;
        }

        public string TitleKey { get; }
        public IReadOnlyList<ReportLine> Lines { get; }

        public bool TryGetLine(string textKey, out ReportLine line)
        {
            foreach (ReportLine candidate in Lines)
            {
                if (candidate.TextKey == textKey)
                {
                    line = candidate;
                    return true;
                }
            }
            line = null;
            return false;
        }
    }

    /// <summary>
    /// Строка отчёта: ключ подписи и значение — сумма или число (<see cref="HasAmount"/>; со знаком, если <see cref="Signed"/>),
    /// изменение «было → стало» (<see cref="HasChange"/>) или список людей (<see cref="Items"/>).
    /// </summary>
    public sealed class ReportLine
    {
        private static readonly ReportItem[] NoItems = new ReportItem[0];

        private ReportLine(string textKey, bool hasAmount, int amount, bool signed, IReadOnlyList<ReportItem> items)
        {
            TextKey = textKey;
            HasAmount = hasAmount;
            Amount = amount;
            Signed = signed;
            Items = items ?? NoItems;
        }

        public string TextKey { get; }
        public bool HasAmount { get; }
        public int Amount { get; }

        /// <summary>Сумма пишется со знаком: доходы «+», расходы «−».</summary>
        public bool Signed { get; }

        /// <summary>Значение — изменение: <see cref="From"/> → <see cref="To"/>.</summary>
        public bool HasChange { get; private set; }

        public float From { get; private set; }
        public float To { get; private set; }

        public IReadOnlyList<ReportItem> Items { get; }

        public static ReportLine Money(string textKey, int amount, bool signed) => new ReportLine(textKey, true, amount, signed, null);

        /// <summary>Строка-счёт: «Пришло: 45».</summary>
        public static ReportLine Number(string textKey, int count) => new ReportLine(textKey, true, count, false, null);

        /// <summary>Строка-изменение: «Репутация: 5 → 7».</summary>
        public static ReportLine Change(string textKey, float from, float to) =>
            new ReportLine(textKey, false, 0, false, null) { HasChange = true, From = from, To = to };

        public static ReportLine People(string textKey, IReadOnlyList<ReportItem> items) => new ReportLine(textKey, false, 0, false, items);
    }

    /// <summary>
    /// Человек в строке отчёта и подробность: у раскрытий — что раскрылось (<see cref="MonthReportSections.TraitDetail"/>,
    /// <see cref="MonthReportSections.AxisDetail"/>); без подробности — пустая строка.
    /// </summary>
    public sealed class ReportItem
    {
        public ReportItem(int personId, string detail = "")
        {
            PersonId = personId;
            Detail = detail ?? string.Empty;
        }

        public int PersonId { get; }
        public string Detail { get; }
    }

    /// <summary>История отчётов месяца (часть мира), от старых к новым.</summary>
    public sealed class MonthReportHistory
    {
        private readonly List<MonthReport> reports = new List<MonthReport>();

        internal MonthReportHistory()
        {
        }

        public IReadOnlyList<MonthReport> Reports => reports;

        /// <summary>Последний отчёт; <c>null</c> — отчётов ещё не было.</summary>
        public MonthReport GetLast() => reports.Count > 0 ? reports[reports.Count - 1] : null;

        internal void Add(MonthReport report) => reports.Add(report);
    }
}
