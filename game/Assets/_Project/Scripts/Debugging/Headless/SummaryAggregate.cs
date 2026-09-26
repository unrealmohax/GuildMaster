using System;
using System.Collections.Generic;

namespace GuildMaster.Debugging
{
    /// <summary>Статистика значения по прогонам: среднее, разброс (стандартное отклонение выборки), минимум, максимум.</summary>
    public readonly struct SummaryStat
    {
        public SummaryStat(double mean, double deviation, double min, double max)
        {
            Mean = mean;
            Deviation = deviation;
            Min = min;
            Max = max;
        }

        public double Mean { get; }

        /// <summary>Стандартное отклонение выборки (n − 1); у одного прогона — 0.</summary>
        public double Deviation { get; }

        public double Min { get; }
        public double Max { get; }

        public static SummaryStat Of(IReadOnlyList<double> values)
        {
            if (values == null || values.Count == 0) throw new ArgumentException("No values", nameof(values));

            double sum = 0, min = double.MaxValue, max = double.MinValue;
            foreach (double value in values)
            {
                sum += value;
                min = Math.Min(min, value);
                max = Math.Max(max, value);
            }
            double mean = sum / values.Count;
            if (values.Count < 2) return new SummaryStat(mean, 0, min, max);

            double squares = 0;
            foreach (double value in values) squares += (value - mean) * (value - mean);
            return new SummaryStat(mean, Math.Sqrt(squares / (values.Count - 1)), min, max);
        }
    }

    /// <summary>
    /// Сводка нескольких прогонов с одним ботом и числом дней: по каждому месяцу и итогу — статистика каждого столбца.
    /// Месяцы сопоставляются по порядку; в свёртку идут месяцы, которые есть во всех прогонах.
    /// </summary>
    public sealed class SummaryAggregate
    {
        private SummaryAggregate(IReadOnlyList<RunSummary> runs, List<string> labels, List<SummaryStat[]> months, SummaryStat[] totals)
        {
            Runs = runs;
            MonthLabels = labels;
            Months = months;
            Totals = totals;
        }

        public IReadOnlyList<RunSummary> Runs { get; }
        public IReadOnlyList<MonthColumn> Columns => Runs[0].MonthColumns;
        public IReadOnlyList<string> MonthLabels { get; }

        /// <summary>По месяцам — статистика по столбцам <see cref="Columns"/>.</summary>
        public IReadOnlyList<SummaryStat[]> Months { get; }

        public IReadOnlyList<SummaryStat> Totals { get; }

        public static SummaryAggregate Of(IReadOnlyList<RunSummary> runs)
        {
            if (runs == null || runs.Count == 0) throw new ArgumentException("No runs", nameof(runs));

            int columns = runs[0].MonthColumns.Count;
            int monthCount = int.MaxValue;
            foreach (RunSummary run in runs)
            {
                if (run.MonthColumns.Count != columns) throw new ArgumentException("Runs have different summary columns", nameof(runs));
                monthCount = Math.Min(monthCount, run.Months.Count);
            }

            var labels = new List<string>(monthCount);
            var months = new List<SummaryStat[]>(monthCount);
            var values = new double[runs.Count];
            for (int m = 0; m < monthCount; m++)
            {
                labels.Add(runs[0].Months[m].Label);
                var stats = new SummaryStat[columns];
                for (int c = 0; c < columns; c++)
                {
                    for (int r = 0; r < runs.Count; r++) values[r] = runs[r].Months[m].Values[c];
                    stats[c] = SummaryStat.Of(values);
                }
                months.Add(stats);
            }

            var totals = new SummaryStat[columns];
            for (int c = 0; c < columns; c++)
            {
                for (int r = 0; r < runs.Count; r++) values[r] = runs[r].Totals[c];
                totals[c] = SummaryStat.Of(values);
            }
            return new SummaryAggregate(runs, labels, months, totals);
        }
    }
}
