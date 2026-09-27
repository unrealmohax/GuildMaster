using System;
using System.Collections.Generic;
using System.Text;
using GuildMaster.Core;
using GuildMaster.Data;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GuildMaster.Tests
{
    /// <summary>Данные для симуляции без ассетов: BalanceSettings со значениями по умолчанию.</summary>
    internal sealed class TestData : IDisposable
    {
        public TestData()
        {
            Balance = ScriptableObject.CreateInstance<BalanceSettings>();
            Registry = new DataRegistry(Balance);
        }

        public BalanceSettings Balance { get; }
        public DataRegistry Registry { get; }

        public void Dispose() => Object.DestroyImmediate(Balance);
    }

    /// <summary>Система-шумелка: тратит свой поток случайных чисел и пишет результаты в события.</summary>
    internal sealed class NoiseSystem : ISimSystem
    {
        private readonly bool issueIds;
        private static readonly string[] Words = { "wolf", "bandit", "mill", "cave", "road" };
        private static readonly float[] Weights = { 5f, 1f, 0f, 2f, 0.5f };

        public NoiseSystem(string name, bool issueIds = true)
        {
            Name = name;
            this.issueIds = issueIds;
        }

        public string Name { get; }

        public void Tick(SimContext ctx)
        {
            Rng rng = ctx.Rng;
            if (!rng.Chance(0.3f)) return;

            SimEvent simEvent = issueIds
                ? ctx.Events.Publish(SimEventType.Debug, EventImportance.Notable, ctx.World.Ids.Next())
                : ctx.Events.Publish(SimEventType.Debug);
            simEvent
                .With("roll", rng.NextFloat())
                .With("range", rng.Range(0, 100))
                .With("normal", rng.Normal(0f, 45f))
                .With("pick", rng.Pick(Words))
                .With("weighted", rng.PickWeighted(Words, Weights));
        }
    }

    /// <summary>Команда, которая оставляет след в логе и тратит поток команд.</summary>
    internal sealed class TraceCommand : ICommand
    {
        private readonly string label;

        public TraceCommand(string label)
        {
            this.label = label;
        }

        public void Apply(SimContext ctx)
        {
            ctx.Events.Publish(SimEventType.Debug, EventImportance.Important, ctx.World.Ids.Next())
                .With("command", label)
                .With("roll", ctx.Rng.Range(0, 1000));
        }
    }

    /// <summary>Команда-обёртка: выполнить действие с контекстом такта — вызвать службу Core так, как её вызовет система.</summary>
    internal sealed class ActionCommand : ICommand
    {
        private readonly Action<SimContext> action;

        public ActionCommand(Action<SimContext> action)
        {
            this.action = action;
        }

        public void Apply(SimContext ctx) => action(ctx);
    }

    internal static class SimulationRun
    {
        /// <summary>Выполнить действие сразу, как команду на паузе. События — в <c>simulation.Events</c> до следующего такта.</summary>
        public static void Do(Simulation simulation, Action<SimContext> action)
        {
            simulation.Send(new ActionCommand(action));
            simulation.ApplyCommandsNow();
        }

        public static void Days(Simulation simulation, int days)
        {
            long ticks = simulation.Calendar.DaysToHours(days);
            for (long i = 0; i < ticks; i++) simulation.Tick();
        }

        /// <summary>Все события прогона.</summary>
        public static List<SimEvent> Collect(Simulation simulation, Action<Simulation> run)
        {
            var events = new List<SimEvent>();
            void Append(IReadOnlyList<SimEvent> tickEvents) => events.AddRange(tickEvents);

            simulation.TickCompleted += Append;
            try
            {
                run(simulation);
            }
            finally
            {
                simulation.TickCompleted -= Append;
            }
            return events;
        }
    }

    internal static class SimulationLog
    {
        /// <summary>Все события прогона строками лога.</summary>
        public static string Record(Simulation simulation, Action<Simulation> run)
        {
            var log = new StringBuilder();
            void Append(IReadOnlyList<SimEvent> events)
            {
                foreach (SimEvent simEvent in events) log.Append(simEvent.ToLogLine(simulation.Calendar)).Append('\n');
            }

            simulation.TickCompleted += Append;
            try
            {
                run(simulation);
            }
            finally
            {
                simulation.TickCompleted -= Append;
            }
            return log.ToString();
        }
    }
}
