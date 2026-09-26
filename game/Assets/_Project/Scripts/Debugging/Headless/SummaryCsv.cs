using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace GuildMaster.Debugging
{
    /// <summary>
    /// Сводка в CSV: запятая между полями, точка в числах, конец строки <c>\n</c>. Таблицы идут одна за другой
    /// через пустую строку, у каждой своя строка заголовков: прогон, по месяцам (с итогом), по людям.
    /// </summary>
    public static class SummaryCsv
    {
        public static string Write(RunSummary summary)
        {
            var csv = new StringBuilder();
            Row(csv, "Зерно", "Бот", "Дней");
            Row(csv, Number(summary.Seed), summary.BotName, Number(summary.Days));
            csv.Append('\n');

            var header = new List<string> { "Месяц", "Дней" };
            foreach (MonthColumn column in summary.MonthColumns) header.Add(column.Name);
            Row(csv, header);
            foreach (MonthRow month in summary.Months)
            {
                var row = new List<string> { month.Label, Number(month.Days) };
                for (int c = 0; c < summary.MonthColumns.Count; c++) row.Add(Format(month.Values[c], summary.MonthColumns[c].Format));
                Row(csv, row);
            }
            var totals = new List<string> { "Итого", Number(summary.Days) };
            for (int c = 0; c < summary.MonthColumns.Count; c++) totals.Add(Format(summary.Totals[c], summary.MonthColumns[c].Format));
            Row(csv, totals);
            csv.Append('\n');

            var peopleHeader = new List<string>();
            foreach (PersonColumn column in summary.PersonColumns) peopleHeader.Add(column.Name);
            Row(csv, peopleHeader);
            foreach (string[] person in summary.People) Row(csv, person);
            return csv.ToString();
        }

        /// <summary>Несколько прогонов: по каждому столбцу — среднее, разброс, минимум, максимум; затем итог каждого прогона.</summary>
        public static string Write(SummaryAggregate aggregate)
        {
            RunSummary first = aggregate.Runs[0];
            var csv = new StringBuilder();
            Row(csv, "Прогонов", "Зёрна", "Бот", "Дней");
            Row(csv, Number(aggregate.Runs.Count), SeedList(aggregate.Runs), first.BotName, Number(first.Days));
            csv.Append('\n');

            var header = new List<string> { "Месяц" };
            foreach (MonthColumn column in aggregate.Columns)
            {
                header.Add(column.Name + " ср");
                header.Add(column.Name + " разброс");
                header.Add(column.Name + " мин");
                header.Add(column.Name + " макс");
            }
            Row(csv, header);
            for (int m = 0; m < aggregate.Months.Count; m++) Row(csv, StatRow(aggregate.MonthLabels[m], aggregate.Months[m]));
            Row(csv, StatRow("Итого", aggregate.Totals));
            csv.Append('\n');

            var runsHeader = new List<string> { "Зерно" };
            foreach (MonthColumn column in aggregate.Columns) runsHeader.Add(column.Name);
            Row(csv, runsHeader);
            foreach (RunSummary run in aggregate.Runs)
            {
                var row = new List<string> { Number(run.Seed) };
                for (int c = 0; c < run.MonthColumns.Count; c++) row.Add(Format(run.Totals[c], run.MonthColumns[c].Format));
                Row(csv, row);
            }
            return csv.ToString();
        }

        /// <summary>Число для таблиц сводки: <c>0.##</c>, без зависимости от культуры.</summary>
        public static string Format(double value, string format = "0.##") => value.ToString(format, CultureInfo.InvariantCulture);

        private static List<string> StatRow(string label, IReadOnlyList<SummaryStat> stats)
        {
            var row = new List<string> { label };
            foreach (SummaryStat stat in stats)
            {
                row.Add(Format(stat.Mean));
                row.Add(Format(stat.Deviation));
                row.Add(Format(stat.Min));
                row.Add(Format(stat.Max));
            }
            return row;
        }

        private static string SeedList(IReadOnlyList<RunSummary> runs)
        {
            var seeds = new StringBuilder();
            foreach (RunSummary run in runs)
            {
                if (seeds.Length > 0) seeds.Append(' ');
                seeds.Append(Number(run.Seed));
            }
            return seeds.ToString();
        }

        private static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);

        private static void Row(StringBuilder csv, params string[] fields) => Row(csv, (IReadOnlyList<string>)fields);

        private static void Row(StringBuilder csv, IReadOnlyList<string> fields)
        {
            for (int i = 0; i < fields.Count; i++)
            {
                if (i > 0) csv.Append(',');
                Field(csv, fields[i] ?? string.Empty);
            }
            csv.Append('\n');
        }

        private static void Field(StringBuilder csv, string value)
        {
            if (value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0)
            {
                csv.Append(value);
                return;
            }
            csv.Append('"').Append(value.Replace("\"", "\"\"")).Append('"');
        }
    }
}
