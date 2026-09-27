using System.Collections.Generic;

namespace GuildMaster.Core
{
    /// <summary>
    /// Отчёт месяца: в начале месяца, после всех систем, которые меняют мир в этом такте (доходы и выплаты, уходы,
    /// трудные времена), — отчёт за прошедший месяц (<see cref="MonthReportSections"/>) в историю
    /// (<see cref="WorldState.Reports"/>), событие <see cref="SimEventType.MonthReportReady"/> без автопаузы и строки
    /// отчёта в лог (<see cref="SimLogLevel.Info"/>). Случайности нет.
    /// </summary>
    public sealed class MonthReportSystem : ISimSystem
    {
        public string Name => nameof(MonthReportSystem);

        public void Tick(SimContext ctx)
        {
            GameTime time = ctx.World.Time;
            if (time.Hour != 0 || time.Day != 1) return;

            MonthReport report = Build(ctx.World, ctx.Data, ctx.Calendar);
            ctx.World.Reports.Add(report);

            int died = 0;
            if (report.TryGetSection(MonthReportSections.PeopleTitle, out ReportSection people)
                && people.TryGetLine(MonthReportSections.PeopleDied, out ReportLine diedLine))
                died = diedLine.Items.Count;

            ctx.Events.Publish(SimEventType.MonthReportReady, EventImportance.Notable)
                .With("year", report.Year)
                .With("month", report.Month)
                .With("income", report.Income)
                .With("expense", report.Expense)
                .With("count", died);

            if (!ctx.Log.IsOn(SimLogLevel.Info)) return;
            var errors = new List<string>();
            foreach (string line in MonthReportText.Lines(report, ctx.World, ctx.Data, errors))
                ctx.Log.Write(SimLogLevel.Info, "report {0}.{1}: {2}", report.Year, report.Month, line);
            foreach (string error in errors) ctx.Log.Write(SimLogLevel.Error, "report: {0}", error);
        }

        /// <summary>Собрать отчёт за период с прошлого отчёта до текущего часа.</summary>
        public static MonthReport Build(WorldState world, DataRegistry data, Calendar calendar)
        {
            var period = new ReportPeriod(world, data, calendar, world.Reports.GetLast());
            var sections = new List<ReportSection>(MonthReportSections.Default.Count);
            foreach (MonthReportSection section in MonthReportSections.Default)
                sections.Add(new ReportSection(section.TitleKey, section.Build(period)));

            int income = 0;
            int expense = 0;
            IReadOnlyList<LedgerEntry> ledger = world.Treasury.Ledger;
            for (int i = period.LedgerFrom; i < period.LedgerTo; i++)
            {
                if (ledger[i].Amount >= 0) income += ledger[i].Amount;
                else expense -= ledger[i].Amount;
            }

            GameTime reported = calendar.At(period.ToHours - 1);
            return new MonthReport(reported.Year, reported.Month, period.FromHours, period.ToHours, period.MoneyAtStart, period.MoneyAtEnd,
                period.LedgerFrom, period.LedgerTo, income, expense, sections);
        }
    }
}
