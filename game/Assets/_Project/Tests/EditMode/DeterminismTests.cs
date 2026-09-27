using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using GuildMaster.Core;
using NUnit.Framework;

namespace GuildMaster.Tests
{
    /// <summary>
    /// Детерминизм: то же зерно + те же команды в те же такты = тот же мир (побайтно одинаковый лог).
    /// </summary>
    public sealed class DeterminismTests
    {
        private const int Days = 60;

        [Test]
        public void SameSeed_SameCommands_GiveByteIdenticalLog()
        {
            using var data = new TestData();
            string first = RunWithCommands(data, 20260926u);
            string second = RunWithCommands(data, 20260926u);

            Assert.That(CountLines(first, "] Debug "), Is.GreaterThan(Days * 24 / 5), "log must contain random outcomes");
            Assert.That(CountLines(first, "command="), Is.EqualTo(CommandTicks().Count() * 2));
            Assert.AreEqual(Encoding.UTF8.GetBytes(first), Encoding.UTF8.GetBytes(second));
        }

        [Test]
        public void DifferentSeed_GivesDifferentLog()
        {
            using var data = new TestData();
            Assert.AreNotEqual(RunWithCommands(data, 1u), RunWithCommands(data, 2u));
        }

        [Test]
        public void CommandsAppliedOnPause_GiveSameWorldAsAtNextTick()
        {
            using var data = new TestData();
            string atTick = RunWithCommands(data, 77u, applyOnPause: false);
            string onPause = RunWithCommands(data, 77u, applyOnPause: true);
            Assert.AreEqual(atTick, onPause);
        }

        [Test]
        public void NewSystemWithOwnRandomness_DoesNotChangeOtherSystems()
        {
            using var data = new TestData();

            string Run(bool withExtraSystem)
            {
                var systems = SimulationSystems.CreateDefault();
                if (withExtraSystem) systems.Add(new NoiseSystem("Newcomer", issueIds: false));
                systems.Add(new NoiseSystem("Veteran", issueIds: false));
                var simulation = new Simulation(data.Registry, 5u, systems);
                string log = SimulationLog.Record(simulation, sim => TickDays(sim, Days));
                return string.Join("\n", log.Split('\n').Where(line => line.Contains("[Veteran]")));
            }

            string alone = Run(withExtraSystem: false);
            Assert.IsNotEmpty(alone);
            Assert.AreEqual(alone, Run(withExtraSystem: true));
        }

        [Test]
        public void AutopauseSystem_DoesNotShiftOtherSystems()
        {
            using var data = new TestData();

            string Run(bool withAutopause)
            {
                var systems = withAutopause
                    ? SimulationSystems.CreateDefault()
                    : new List<ISimSystem> { new CommandSystem(), new TimeSystem() };
                Assert.AreEqual(withAutopause, systems.Any(s => s is AutopauseSystem));
                systems.Add(new NoiseSystem("Noise"));
                var simulation = new Simulation(data.Registry, 9u, systems);
                string log = SimulationLog.Record(simulation, sim =>
                {
                    sim.Send(new TraceCommand("first"));
                    TickDays(sim, Days);
                });
                // Отчёт месяца пишется и в пустом мире; сравниваются время, команды и шум.
                return string.Join("\n", log.Split('\n').Where(line => !line.Contains("[MonthReportSystem]")));
            }

            string without = Run(withAutopause: false);
            Assert.That(CountLines(without, "] Debug "), Is.GreaterThan(Days * 24 / 5));
            Assert.AreEqual(without, Run(withAutopause: true));
        }

        [Test]
        public void HeadlessYear_RunsUnderOneMinute()
        {
            using var data = new TestData();
            var systems = SimulationSystems.CreateDefault();
            systems.Add(new NoiseSystem("Noise"));
            var simulation = new Simulation(data.Registry, 360u, systems);

            Stopwatch stopwatch = Stopwatch.StartNew();
            string log = SimulationLog.Record(simulation, sim => TickDays(sim, 360));
            stopwatch.Stop();

            Assert.AreEqual("2.1.1 06:00", simulation.World.Time.ToString());
            Assert.IsNotEmpty(log);
            Assert.That(stopwatch.Elapsed.TotalSeconds, Is.LessThan(60));
            TestContext.WriteLine($"360 days: {simulation.TicksDone} ticks in {stopwatch.Elapsed.TotalMilliseconds:0} ms");
        }

        private static string RunWithCommands(TestData data, uint seed, bool applyOnPause = false)
        {
            var systems = SimulationSystems.CreateDefault();
            systems.Add(new NoiseSystem("Noise"));
            var simulation = new Simulation(data.Registry, seed, systems);
            var commandTicks = new HashSet<int>(CommandTicks());

            return SimulationLog.Record(simulation, sim =>
            {
                int ticks = (int)sim.Calendar.DaysToHours(Days);
                for (int tick = 0; tick < ticks; tick++)
                {
                    if (commandTicks.Contains(tick))
                    {
                        sim.Send(new TraceCommand($"t{tick}a"));
                        sim.Send(new TraceCommand($"t{tick}b"));
                        if (applyOnPause) sim.ApplyCommandsNow();
                    }
                    sim.Tick();
                }
            });
        }

        private static IEnumerable<int> CommandTicks() => Enumerable.Range(0, Days).Select(day => day * 24 + 3);

        private static void TickDays(Simulation simulation, int days)
        {
            long ticks = simulation.Calendar.DaysToHours(days);
            for (long i = 0; i < ticks; i++) simulation.Tick();
        }

        private static int CountLines(string log, string fragment) =>
            log.Split('\n').Count(line => line.Contains(fragment));
    }
}
