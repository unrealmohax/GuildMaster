using System;
using System.Diagnostics;
using GuildMaster.Core;
using GuildMaster.Data;

namespace GuildMaster.Debugging
{
    /// <summary>
    /// Прогон симуляции без интерфейса, так быстро, как получится. Лог, сводка и бот игрока — ТЗ 16.
    /// </summary>
    public static class HeadlessRun
    {
        public readonly struct Result
        {
            public Result(uint seed, long ticks, long events, GameTime finalTime, double elapsedSeconds)
            {
                Seed = seed;
                Ticks = ticks;
                Events = events;
                FinalTime = finalTime;
                ElapsedSeconds = elapsedSeconds;
            }

            public uint Seed { get; }
            public long Ticks { get; }
            public long Events { get; }
            public GameTime FinalTime { get; }
            public double ElapsedSeconds { get; }
        }

        public static Result Run(GameConfig config, uint seed, int days)
        {
            if (days < 0) throw new ArgumentOutOfRangeException(nameof(days));

            Simulation simulation = Simulation.CreateDefault(DataRegistry.FromConfig(config), seed);
            long events = 0;
            simulation.TickCompleted += tickEvents => events += tickEvents.Count;

            long ticks = simulation.Calendar.DaysToHours(days);
            Stopwatch stopwatch = Stopwatch.StartNew();
            for (long i = 0; i < ticks; i++)
            {
                simulation.Tick();
            }
            stopwatch.Stop();

            return new Result(seed, ticks, events, simulation.World.Time, stopwatch.Elapsed.TotalSeconds);
        }

        public static uint NewRandomSeed() => unchecked((uint)Guid.NewGuid().GetHashCode());
    }
}
