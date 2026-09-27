using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using GuildMaster.Core;
using GuildMaster.Data;
using GuildMaster.Debugging;
using NUnit.Framework;

namespace GuildMaster.Tests
{
    /// <summary>Логгер симуляции: формат строк, уровни, выключенный уровень, лог не влияет на мир.</summary>
    public sealed class SimLoggerTests
    {
        private TestData data;

        [SetUp]
        public void SetUp() => data = new TestData();

        [TearDown]
        public void TearDown() => data.Dispose();

        [Test]
        public void Line_HasGameTimeSystemLevelAndText()
        {
            var writer = new StringWriter();
            var log = new SimLogger(SimLogLevel.Debug, writer);
            List<ISimSystem> systems = SimulationSystems.CreateDefault();
            systems.Add(new LambdaSystem("Probe", ctx => ctx.Log.Write(SimLogLevel.Debug, "value={0} name={1}", 1.5f, "x")));
            var simulation = new Simulation(data.Registry, 1u, systems, log);

            simulation.Tick();

            StringAssert.Contains("[1.1.1 07:00] [Probe] [Debug] value=1.5 name=x\n", writer.ToString());
            Assert.AreEqual(writer.ToString().Split('\n').Length - 1, log.LinesWritten);
        }

        [Test]
        public void OffLevel_DoesNotFormatArguments()
        {
            var writer = new StringWriter();
            var log = new SimLogger(SimLogLevel.Info, writer);
            var argument = new CountingArgument();

            log.Write(SimLogLevel.Debug, "{0}", argument);
            log.Write(SimLogLevel.Trace, "{0} {1}", argument, argument);
            Assert.Throws<InvalidOperationException>(() => log.Begin(SimLogLevel.Trace));

            Assert.AreEqual(0, argument.Formatted);
            Assert.AreEqual(string.Empty, writer.ToString());
            Assert.IsTrue(log.IsOn(SimLogLevel.Error));
            Assert.IsTrue(log.IsOn(SimLogLevel.Info));
            Assert.IsFalse(log.IsOn(SimLogLevel.Debug));
            Assert.IsFalse(SimLogger.Disabled.IsOn(SimLogLevel.Error));

            log.Write(SimLogLevel.Info, "{0}", argument);
            Assert.AreEqual(1, argument.Formatted);
        }

        [Test]
        public void Events_ClockEventsBelowInfo_OthersAtInfo()
        {
            string Run(SimLogLevel level)
            {
                var writer = new StringWriter();
                SimulationRun.Days(Simulation.CreateDefault(data.Registry, 1u, new SimLogger(level, writer)), 31);
                return writer.ToString();
            }

            string info = Run(SimLogLevel.Info);
            StringAssert.Contains("[1.2.1 00:00] [TimeSystem] [Info] MonthStarted", info);
            StringAssert.DoesNotContain("HourStarted", info);
            StringAssert.DoesNotContain("DayStarted", info);

            string debug = Run(SimLogLevel.Debug);
            StringAssert.Contains("[1.1.2 00:00] [TimeSystem] [Debug] DayStarted", debug);
            StringAssert.DoesNotContain("HourStarted", debug);

            string trace = Run(SimLogLevel.Trace);
            StringAssert.Contains("[1.1.1 07:00] [TimeSystem] [Trace] HourStarted", trace);
            StringAssert.Contains("[Trace] EveningStarted", trace);
        }

        [Test]
        public void EventLine_FollowsRollsOfItsSystem_WithImportanceAndPayload()
        {
            var writer = new StringWriter();
            List<ISimSystem> systems = SimulationSystems.CreateDefault();
            systems.Insert(1, new LambdaSystem("Probe", ctx =>
            {
                ctx.RollChance(1f, "probe");
                ctx.Events.Publish(SimEventType.Debug, EventImportance.Important, 7).With("x", 2);
            }));
            var simulation = new Simulation(data.Registry, 1u, systems, new SimLogger(SimLogLevel.Debug, writer));

            simulation.Tick();

            string[] lines = writer.ToString().Split('\n');
            Assert.That(lines[0], Does.Match(@"^\[1\.1\.1 06:00\] \[Probe\] \[Debug\] roll probe chance=1 rolled=0\.\d{4} => yes$"));
            Assert.AreEqual("[1.1.1 06:00] [Probe] [Info] Debug (Important) ids=7 x=2", lines[1]);
        }

        private sealed class CountingArgument
        {
            public int Formatted { get; private set; }

            public override string ToString()
            {
                Formatted++;
                return "counted";
            }
        }
    }

    /// <summary>Лог с людьми: не влияет на мир, объясняет срывы и уходы, одинаков при одном зерне.</summary>
    public sealed class SimulationLogTests
    {
        private PeopleData data;

        [SetUp]
        public void SetUp() => data = new PeopleData();

        [TearDown]
        public void TearDown() => data.Dispose();

        [Test]
        public void Logging_DoesNotChangeTheWorld()
        {
            (string events, string people) Run(SimLogger log)
            {
                Simulation simulation = Simulation.CreateDefault(data.Registry, 77u, log);
                string events = SimulationLog.Record(simulation, sim =>
                {
                    for (int day = 0; day < 120; day++)
                    {
                        SimulationRun.Days(sim, 1);
                        foreach (Candidate candidate in sim.World.Adventurers.Candidates.ToList())
                            sim.Send(new AcceptCandidateCommand(candidate.Adventurer.Id));
                    }
                });
                return (events, PeopleDump.Of(simulation));
            }

            (string events, string people) silent = Run(null);
            (string events, string people) traced = Run(new SimLogger(SimLogLevel.Trace, new StringWriter()));

            Assert.AreEqual(silent.events, traced.events);
            Assert.AreEqual(silent.people, traced.people);
        }

        [Test]
        public void SameSeed_SameBot_SameLog()
        {
            string Run() => HeadlessLog(2026u, 180, PlayerBots.Simple(), SimLogLevel.Trace).log;

            string first = Run();
            Assert.AreEqual(first, Run());

            StringAssert.Contains("[HeadlessRun] [Info] run seed=2026 days=180 bot=Простой level=Trace", first);
            StringAssert.Contains("[StartScenario] [Debug] start #1 ", first);
            StringAssert.Contains("[RecruitSystem] [Debug] roll candidate chance=", first);
            StringAssert.Contains("[RecruitSystem] [Debug] candidate #", first);
            StringAssert.Contains("[RecruitSystem] [Info] CandidateArrived ids=", first);
            StringAssert.Contains("[Bot] [Info] send accept ", first);
            StringAssert.Contains("[CommandSystem] [Info] AdventurerJoined ids=", first);
            StringAssert.Contains("[StateSystem] [Trace] day #1 ", first);
            Assert.That(first, Does.Match(@"\| axes Risk=[+-]\d"));
            Assert.That(first, Does.Match(@"\| traits [^\n]*\(hidden\)"));

            string otherSeed = HeadlessLog(2027u, 180, PlayerBots.Simple(), SimLogLevel.Trace).log;
            Assert.AreNotEqual(first, otherSeed);
        }

        [Test]
        public void Scenario_ReplaysTheGame()
        {
            (string log, HeadlessRun.Result result) simple = HeadlessLog(31u, 180, PlayerBots.Simple(), SimLogLevel.Debug);
            Assert.That(simple.result.Scenario.Recorded, Is.GreaterThan(0));
            Assert.AreEqual(0, simple.result.Scenario.Unrecorded);

            ScenarioScript script = ScenarioScript.Parse(simple.result.Scenario.ToString());
            Assert.AreEqual(31u, script.Seed);
            (string log, HeadlessRun.Result result) replay = HeadlessLog(31u, 180, PlayerBots.Scenario(script), SimLogLevel.Debug);

            Assert.AreEqual(WithoutRunHeader(simple.log), WithoutRunHeader(replay.log));
            Assert.AreEqual(PeopleDump.Of(simple.result.Simulation), PeopleDump.Of(replay.result.Simulation));
            Assert.AreEqual(simple.result.Scenario.ToString().Replace("bot: Простой", "bot: Сценарий"), replay.result.Scenario.ToString());

            (string log, HeadlessRun.Result result) passive = HeadlessLog(31u, 180, PlayerBots.Passive(), SimLogLevel.Debug);
            Assert.AreNotEqual(WithoutRunHeader(simple.log), WithoutRunHeader(passive.log), "без команд игра другая");
        }

        [Test]
        public void Log_ExplainsBreakdown()
        {
            var writer = new StringWriter();
            // Стресс держится на 95 до StateSystem каждый час: суточная проверка срыва видит его выше порога.
            var stressor = new LambdaSystem("Stressor", ctx =>
            {
                foreach (Adventurer adventurer in ctx.World.Adventurers.Active) adventurer.State.Stress = 95f;
            });
            using (var world = new StateWorld(seed: 11u, beforeState: stressor, log: new SimLogger(SimLogLevel.Trace, writer)))
            {
                world.AddMany(10);
                world.Days(5);
            }

            string log = writer.ToString();
            Match breakdown = Regex.Match(log, @"\[StateSystem\] \[Info\] Breakdown \(Important\) ids=(\d+)");
            Assert.IsTrue(breakdown.Success, "за 5 суток при стрессе 95 кто-то сорвался");

            string id = breakdown.Groups[1].Value;
            string before = log.Substring(0, breakdown.Index);
            string lastDay = before.Split('\n').Last(line => line.Contains($"[Trace] day #{id} "));
            string lastRoll = before.Split('\n').Last(line => line.Contains($"roll breakdown #{id} "));
            StringAssert.Contains("stress=", lastDay);
            Assert.That(lastRoll, Does.Match(@"stress=\d+(\.\d+)? chance=0\.1 rolled=0\.\d{4} => yes$"));
        }

        [Test]
        public void Log_ExplainsLeaving()
        {
            var writer = new StringWriter();
            using (var world = new StateWorld(seed: 12u, log: new SimLogger(SimLogLevel.Debug, writer)))
            {
                List<Adventurer> people = world.AddMany(20);
                world.Do(ctx => people.ForEach(a => StateService.ChangeLoyalty(ctx, a, -40f)));
                world.Days(31);
            }

            string log = writer.ToString();
            Match left = Regex.Match(log, @"\[StateSystem\] \[Info\] AdventurerLeft \(Important\) ids=(\d+) cause=\w+ causes=\S+ loyalty=[\d.]+ reason=\S");
            Assert.IsTrue(left.Success, "в начале месяца кто-то из 20 с лояльностью 10 ушёл");

            string id = left.Groups[1].Value;
            string roll = log.Substring(0, left.Index).Split('\n').Last(line => line.Contains($"roll leave #{id} "));
            Assert.That(roll, Does.Match(@"loyalty=\d+(\.\d+)? chance=0\.2 rolled=0\.\d{4} => yes$"));
            StringAssert.Contains("=> no", string.Join("\n", Regex.Matches(log, @"[^\n]*roll leave[^\n]*").Cast<Match>().Select(m => m.Value)),
                "броски тех, кто остался, тоже в логе");
        }

        private (string log, HeadlessRun.Result result) HeadlessLog(uint seed, int days, PlayerBot bot, SimLogLevel level)
        {
            var writer = new StringWriter();
            HeadlessRun.Result result = HeadlessRun.Run(data.Registry, seed, days, bot, new SimLogger(level, writer));
            return (writer.ToString(), result);
        }

        private static string WithoutRunHeader(string log) =>
            string.Join("\n", log.Split('\n').Where(line => !line.Contains("[" + HeadlessRun.RunSource + "]")));
    }
}
