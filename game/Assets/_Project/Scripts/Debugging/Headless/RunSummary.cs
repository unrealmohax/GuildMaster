using System;
using System.Collections.Generic;
using GuildMaster.Core;

namespace GuildMaster.Debugging
{
    /// <summary>Как столбец месяца сворачивается в итог прогона.</summary>
    public enum SummaryTotal
    {
        /// <summary>Сумма за все месяцы (счётчики событий).</summary>
        Sum,

        /// <summary>Значение последнего месяца (состояние на конец прогона).</summary>
        Last,

        /// <summary>Среднее по месяцам.</summary>
        Mean,
    }

    /// <summary>Что видит столбец месяца: мир на конец месяца и события, случившиеся за месяц.</summary>
    public sealed class MonthRecord
    {
        private readonly Dictionary<SimEventType, int> counts;

        internal MonthRecord(int year, int month, int days, ISimulationClient game, Dictionary<SimEventType, int> counts)
        {
            Year = year;
            Month = month;
            Days = days;
            Game = game;
            this.counts = counts;
        }

        public int Year { get; }
        public int Month { get; }

        /// <summary>Сколько суток месяца попало в прогон.</summary>
        public int Days { get; }

        public ISimulationClient Game { get; }
        public WorldState World => Game.World;

        /// <summary>Сколько событий этого типа было за месяц.</summary>
        public int Count(SimEventType type) => counts.TryGetValue(type, out int count) ? count : 0;
    }

    /// <summary>Что видит столбец человека: сам человек (в гильдии или в архиве) и события, где он первый участник.</summary>
    public sealed class PersonRecord
    {
        internal PersonRecord(Adventurer adventurer, ISimulationClient game, IReadOnlyList<SimEvent> events)
        {
            Adventurer = adventurer;
            Game = game;
            Events = events;
        }

        public Adventurer Adventurer { get; }
        public ISimulationClient Game { get; }

        /// <summary>События прогона, где человек — первый участник, по порядку.</summary>
        public IReadOnlyList<SimEvent> Events { get; }

        public int Count(SimEventType type)
        {
            int count = 0;
            foreach (SimEvent simEvent in Events)
            {
                if (simEvent.Type == type) count++;
            }
            return count;
        }
    }

    /// <summary>Столбец сводки по месяцам: имя, значение за месяц, свёртка в итог, формат числа.</summary>
    public sealed class MonthColumn
    {
        public MonthColumn(string name, Func<MonthRecord, double> value, SummaryTotal total, string format = "0.##")
        {
            Name = name;
            Value = value;
            Total = total;
            Format = format;
        }

        public string Name { get; }
        public Func<MonthRecord, double> Value { get; }
        public SummaryTotal Total { get; }
        public string Format { get; }
    }

    /// <summary>Столбец сводки по людям: имя и значение на конец прогона строкой.</summary>
    public sealed class PersonColumn
    {
        public PersonColumn(string name, Func<PersonRecord, string> value)
        {
            Name = name;
            Value = value;
        }

        public string Name { get; }
        public Func<PersonRecord, string> Value { get; }
    }

    /// <summary>Строка сводки за календарный месяц.</summary>
    public sealed class MonthRow
    {
        internal MonthRow(int year, int month, int days, double[] values)
        {
            Year = year;
            Month = month;
            Days = days;
            Values = values;
        }

        public int Year { get; }
        public int Month { get; }
        public int Days { get; }

        /// <summary>Значения по столбцам <see cref="RunSummary.MonthColumns"/>.</summary>
        public IReadOnlyList<double> Values { get; }

        public string Label => Year + "." + Month;
    }

    /// <summary>Сводка одного прогона: по месяцам, итог, по людям.</summary>
    public sealed class RunSummary
    {
        internal RunSummary(uint seed, string botName, int days, IReadOnlyList<MonthColumn> monthColumns, List<MonthRow> months,
            IReadOnlyList<PersonColumn> personColumns, List<string[]> people)
        {
            Seed = seed;
            BotName = botName;
            Days = days;
            MonthColumns = monthColumns;
            Months = months;
            PersonColumns = personColumns;
            People = people;
            Totals = ComputeTotals(monthColumns, months);
        }

        public uint Seed { get; }
        public string BotName { get; }
        public int Days { get; }
        public IReadOnlyList<MonthColumn> MonthColumns { get; }
        public IReadOnlyList<MonthRow> Months { get; }

        /// <summary>Итог прогона по столбцам месяцев (<see cref="MonthColumn.Total"/>); месяцев нет — нули.</summary>
        public IReadOnlyList<double> Totals { get; }

        public IReadOnlyList<PersonColumn> PersonColumns { get; }

        /// <summary>Люди, когда-либо бывшие в гильдии, по Id: значения по <see cref="PersonColumns"/>.</summary>
        public IReadOnlyList<string[]> People { get; }

        private static double[] ComputeTotals(IReadOnlyList<MonthColumn> columns, List<MonthRow> months)
        {
            var totals = new double[columns.Count];
            if (months.Count == 0) return totals;

            for (int c = 0; c < columns.Count; c++)
            {
                switch (columns[c].Total)
                {
                    case SummaryTotal.Last:
                        totals[c] = months[months.Count - 1].Values[c];
                        break;
                    default:
                        double sum = 0;
                        foreach (MonthRow month in months) sum += month.Values[c];
                        totals[c] = columns[c].Total == SummaryTotal.Mean ? sum / months.Count : sum;
                        break;
                }
            }
            return totals;
        }
    }

    /// <summary>
    /// Сбор сводки во время прогона. Строка — календарный месяц: закрывается после последнего часа месяца, значения
    /// столбцов — по миру в этот момент и событиям месяца. Последний неполный месяц идёт в сводку, если в нём прошли
    /// целые сутки; остаток короче суток отбрасывается.
    /// </summary>
    public sealed class SummaryRecorder : IDisposable
    {
        private readonly Simulation simulation;
        private readonly IReadOnlyList<MonthColumn> monthColumns;
        private readonly IReadOnlyList<PersonColumn> personColumns;
        private readonly List<MonthRow> months = new List<MonthRow>();
        private readonly Dictionary<SimEventType, int> monthCounts = new Dictionary<SimEventType, int>();
        private readonly Dictionary<int, List<SimEvent>> personEvents = new Dictionary<int, List<SimEvent>>();
        private long monthTicks;

        public SummaryRecorder(Simulation simulation, IReadOnlyList<MonthColumn> monthColumns = null, IReadOnlyList<PersonColumn> personColumns = null)
        {
            this.simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
            this.monthColumns = monthColumns ?? SummaryColumns.Monthly;
            this.personColumns = personColumns ?? SummaryColumns.People;
            simulation.TickCompleted += OnTick;
        }

        public void Dispose() => simulation.TickCompleted -= OnTick;

        /// <summary>Закрыть прогон: последний месяц (если в нём прошли целые сутки) и таблица людей.</summary>
        public RunSummary Finish(string botName, int days)
        {
            Dispose();
            if (monthTicks >= simulation.Calendar.HoursPerDay) CloseMonth();

            var adventurers = new List<Adventurer>(simulation.World.Adventurers.Active);
            adventurers.AddRange(simulation.World.Adventurers.Archive);
            adventurers.Sort((a, b) => a.Id.CompareTo(b.Id));

            var people = new List<string[]>(adventurers.Count);
            foreach (Adventurer adventurer in adventurers)
            {
                IReadOnlyList<SimEvent> events = personEvents.TryGetValue(adventurer.Id, out List<SimEvent> list) ? list : (IReadOnlyList<SimEvent>)Array.Empty<SimEvent>();
                var record = new PersonRecord(adventurer, simulation, events);
                var row = new string[personColumns.Count];
                for (int c = 0; c < row.Length; c++) row[c] = personColumns[c].Value(record);
                people.Add(row);
            }

            return new RunSummary(simulation.MasterSeed, botName, days, monthColumns, months, personColumns, people);
        }

        private void OnTick(IReadOnlyList<SimEvent> events)
        {
            monthTicks++;
            foreach (SimEvent simEvent in events)
            {
                monthCounts.TryGetValue(simEvent.Type, out int count);
                monthCounts[simEvent.Type] = count + 1;

                if (simEvent.Participants.Count == 0) continue;
                int id = simEvent.Participants[0];
                if (!personEvents.TryGetValue(id, out List<SimEvent> list)) personEvents[id] = list = new List<SimEvent>();
                list.Add(simEvent);
            }

            GameTime time = simulation.World.Time;
            Calendar calendar = simulation.Calendar;
            if (time.Day == calendar.DaysPerMonth && time.Hour == calendar.HoursPerDay - 1) CloseMonth();
        }

        private void CloseMonth()
        {
            GameTime time = simulation.World.Time;
            int hoursPerDay = simulation.Calendar.HoursPerDay;
            int days = (int)((monthTicks + hoursPerDay / 2) / hoursPerDay); // первый месяц начинается не с полуночи
            var record = new MonthRecord(time.Year, time.Month, days, simulation, monthCounts);
            var values = new double[monthColumns.Count];
            for (int c = 0; c < values.Length; c++) values[c] = monthColumns[c].Value(record);

            months.Add(new MonthRow(time.Year, time.Month, days, values));
            monthCounts.Clear();
            monthTicks = 0;
        }
    }
}
