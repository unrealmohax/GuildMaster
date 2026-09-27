using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using GuildMaster.Core;
using GuildMaster.Debugging;
using NUnit.Framework;

namespace GuildMaster.Tests
{
    /// <summary>Прогон без интерфейса: боты, сводка, файлы, скорость.</summary>
    public sealed class HeadlessRunTests
    {
        private PeopleData data;

        [SetUp]
        public void SetUp() => data = new PeopleData();

        [TearDown]
        public void TearDown() => data.Dispose();

        [Test]
        public void PassiveBot_SendsNothing_CandidatesLeave()
        {
            HeadlessRun.Result result = HeadlessRun.Run(data.Registry, 5u, 90, PlayerBots.Passive());

            Assert.AreEqual(0, result.Scenario.Recorded);
            Assert.That(Total(result.Summary, "Кандидатов"), Is.GreaterThan(0));
            Assert.AreEqual(0, Total(result.Summary, "Пришло"));
            Assert.AreEqual(result.Simulation.World.Adventurers.Active.Count + result.Simulation.World.Adventurers.Archive.Count,
                data.Balance.Adventurers.StartAdventurers);
        }

        [Test]
        public void SimpleBot_AcceptsEveryCandidate()
        {
            HeadlessRun.Result result = HeadlessRun.Run(data.Registry, 5u, 90, PlayerBots.Simple());
            RunSummary summary = result.Summary;

            Assert.That(Total(summary, "Пришло"), Is.GreaterThan(0));
            Assert.AreEqual(Total(summary, "Кандидатов"), Total(summary, "Пришло"), 1, "каждый пришедший кандидат принят (последний может ждать хода бота)");
            // Команды кандидатам — «такт accept id»; у ответа на важный заказ слово другое («answer»).
            int accepts = result.Scenario.ToString().Split('\n').Count(line => line.Split(' ').Length > 1 && line.Split(' ')[1] == "accept");
            Assert.AreEqual(accepts, (int)Total(summary, "Пришло"), 1);
        }

        [Test]
        public void Year_WithLogAndSummary_UnderOneMinute()
        {
            var writer = new StringWriter();
            HeadlessRun.Result result = HeadlessRun.Run(data.Registry, 360u, 360, PlayerBots.Simple(), new SimLogger(SimLogLevel.Info, writer));

            Assert.AreEqual("2.1.1 06:00", result.FinalTime.ToString());
            Assert.That(result.ElapsedSeconds, Is.LessThan(60));
            Assert.That(result.LogLines, Is.GreaterThan(0));
            Assert.AreEqual(12, result.Summary.Months.Count, "12 календарных месяцев; остаток последнего меньше суток не считается");
            Assert.IsTrue(result.Summary.Months.All(m => m.Days == 30));
            Assert.AreEqual("1.1", result.Summary.Months[0].Label);
            Assert.AreEqual("1.12", result.Summary.Months[11].Label);
            TestContext.WriteLine($"360 days, Info: {result.ElapsedSeconds * 1000:0} ms, {result.LogLines} lines, {writer.ToString().Length} chars");
        }

        [Test]
        public void InfoRun_IsNoticeablyFasterThanTrace()
        {
            // Цена лога — время сверх прогона без лога. Замеры идут по кругу (без лога, Info, Trace), каждый — после сборки
            // мусора, от каждого — лучший из восьми: так дрейф редактора не влияет на сравнение. На Info строки Trace
            // не строятся, поэтому его цена — малая доля цены Trace.
            SimLogLevel?[] levels = { null, SimLogLevel.Info, SimLogLevel.Trace };
            var best = new double[levels.Length];
            var lines = new long[levels.Length];
            for (int i = 0; i < best.Length; i++) best[i] = double.MaxValue;

            string path = Path.GetTempFileName();
            try
            {
                for (int round = 0; round < 9; round++)
                {
                    for (int i = 0; i < levels.Length; i++)
                    {
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                        using (var writer = new StreamWriter(path, false))
                        {
                            SimLogger log = levels[i].HasValue ? new SimLogger(levels[i].Value, writer) : null;
                            HeadlessRun.Result result = HeadlessRun.Run(data.Registry, 360u, 360, PlayerBots.Simple(), log);
                            if (round > 0) best[i] = Math.Min(best[i], result.ElapsedSeconds); // первый круг — прогрев
                            lines[i] = result.LogLines;
                        }
                    }
                }
            }
            finally
            {
                File.Delete(path);
            }

            double none = best[0], info = best[1], trace = best[2];
            string numbers = $"no log {none * 1000:0} ms; Info {info * 1000:0} ms, {lines[1]} lines; Trace {trace * 1000:0} ms, {lines[2]} lines";
            TestContext.WriteLine(numbers);

            // На Info — решения людей (по строке на решение, в том числе ответы на приглашения в группу, сбор групп), на Trace —
            // ещё и оценки каждого варианта и напарника: разница в разы.
            Assert.That(lines[2], Is.GreaterThan(lines[1] * 3), numbers);
            Assert.That(info, Is.LessThan(trace), numbers);
            Assert.That(info - none, Is.LessThan((trace - none) / 2), "на Info строки Trace не строятся: " + numbers);
        }

        [Test]
        public void Summary_PartialLastMonth_CountsWhenAtLeastOneDay()
        {
            HeadlessRun.Result result = HeadlessRun.Run(data.Registry, 8u, 35, PlayerBots.Simple());

            Assert.AreEqual(2, result.Summary.Months.Count);
            Assert.AreEqual(30, result.Summary.Months[0].Days);
            Assert.AreEqual(5, result.Summary.Months[1].Days);
        }

        [Test]
        public void Summary_People_AndTotals()
        {
            HeadlessRun.Result result = HeadlessRun.Run(data.Registry, 360u, 360, PlayerBots.Simple());
            RunSummary summary = result.Summary;
            AdventurerRoster roster = result.Simulation.World.Adventurers;

            Assert.AreEqual(roster.Active.Count + roster.Archive.Count, summary.People.Count);
            int inGuild = Column(summary, "В гильдии");
            Assert.AreEqual(roster.Active.Count, summary.People.Count(p => p[inGuild] == "да"));
            Assert.AreEqual(summary.Months.Last().Values[MonthColumn(summary, "Людей")], Total(summary, "Людей"));
            Assert.AreEqual(summary.Months.Sum(m => m.Values[MonthColumn(summary, "Срывов")]), Total(summary, "Срывов"));

            int revealed = Column(summary, "Раскрыто");
            Assert.IsTrue(summary.People.Any(p => p[revealed].Contains(" 1.")), "у кого-то за год что-то раскрылось, с датой");

            string csv = SummaryCsv.Write(summary);
            StringAssert.StartsWith("Зерно,Бот,Дней\n360,Простой,360\n\nМесяц,Дней,Людей,", csv);
            StringAssert.Contains("\nИтого,360,", csv);
            StringAssert.Contains("\nId,Имя,Архетип,Ранг,Ран,Выполнено,Не выполнено,Жив,Постоянная группа,В гильдии,Раскрыто\n", csv);
        }

        [Test]
        public void SummaryStat_MeanDeviationRange()
        {
            SummaryStat stat = SummaryStat.Of(new[] { 1.0, 2.0, 3.0, 4.0 });
            Assert.AreEqual(2.5, stat.Mean, 1e-9);
            Assert.AreEqual(Math.Sqrt(5.0 / 3.0), stat.Deviation, 1e-9);
            Assert.AreEqual(1.0, stat.Min);
            Assert.AreEqual(4.0, stat.Max);
            Assert.AreEqual(0.0, SummaryStat.Of(new[] { 7.0 }).Deviation);
        }

        [Test]
        public void Aggregate_OfSeveralRuns()
        {
            var runs = new List<RunSummary>();
            for (uint seed = 1; seed <= 3; seed++) runs.Add(HeadlessRun.Run(data.Registry, seed, 60, PlayerBots.Simple()).Summary);

            SummaryAggregate aggregate = SummaryAggregate.Of(runs);
            int people = MonthColumn(runs[0], "Людей");

            Assert.AreEqual(2, aggregate.Months.Count);
            Assert.AreEqual(runs.Average(r => r.Months[1].Values[people]), aggregate.Months[1][people].Mean, 1e-9);
            Assert.AreEqual(runs.Average(r => r.Totals[people]), aggregate.Totals[people].Mean, 1e-9);
            Assert.AreEqual(runs.Max(r => r.Totals[people]), aggregate.Totals[people].Max);

            string csv = SummaryCsv.Write(aggregate);
            StringAssert.StartsWith("Прогонов,Зёрна,Бот,Дней\n3,1 2 3,Простой,60\n", csv);
            StringAssert.Contains("Людей ср,Людей разброс,Людей мин,Людей макс", csv);
        }

        [Test]
        public void RunToFiles_WritesLogSummaryScenarioAndAggregate()
        {
            string folder = Path.Combine(Path.GetTempPath(), "GuildMasterHeadless_" + Guid.NewGuid().ToString("N"));
            using (var game = new GameData())
            {
                try
                {
                    var options = new HeadlessRun.Options { Seed = 40u, Days = 30, Runs = 2, Bot = PlayerBots.Simple, Folder = folder };
                    HeadlessRun.Batch batch = HeadlessRun.RunToFiles(game.Config, options);

                    Assert.AreEqual(2, batch.Results.Count);
                    Assert.AreEqual(41u, batch.Results[1].Seed);
                    Assert.IsNotNull(batch.Aggregate);
                    foreach (string file in batch.Files) Assert.IsTrue(File.Exists(file), file);

                    string[] names = batch.Files.Select(Path.GetFileName).ToArray();
                    Assert.That(names, Has.Some.Match(@"^sim_40_\d{8}_\d{6}\.log$"));
                    CollectionAssert.IsSubsetOf(new[] { "summary_40.csv", "scenario_40.txt", "summary_41.csv", "summary_40_x2.csv" }, names);

                    string log = File.ReadAllText(batch.Files.First(f => Path.GetFileName(f).StartsWith("sim_40_")));
                    StringAssert.StartsWith("[-] [HeadlessRun] [Info] run seed=40 days=30 bot=Простой level=Info\n", log);
                    ScenarioScript script = ScenarioScript.Parse(File.ReadAllText(Path.Combine(folder, "scenario_40.txt")));
                    Assert.AreEqual(40u, script.Seed);
                    Assert.AreEqual(batch.Results[0].Scenario.Recorded, script.Entries.Count);
                }
                finally
                {
                    if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
                }
            }
        }

        private static int MonthColumn(RunSummary summary, string name) => summary.MonthColumns.ToList().FindIndex(c => c.Name == name);

        private static int Column(RunSummary summary, string name) => summary.PersonColumns.ToList().FindIndex(c => c.Name == name);

        private static double Total(RunSummary summary, string name) => summary.Totals[MonthColumn(summary, name)];
    }

    /// <summary>Сценарий команд: разбор, запись, ошибки.</summary>
    public sealed class ScenarioScriptTests
    {
        [Test]
        public void Parse_ReadsCommandsCommentsAndSeed()
        {
            ScenarioScript script = ScenarioScript.Parse("# seed=77\n# comment\n\n5 accept 12\r\n5 reject 13\n  40\tautopause TraitRevealed off\n");

            Assert.AreEqual(77u, script.Seed);
            Assert.AreEqual(3, script.Entries.Count);
            Assert.AreEqual(5, script.Entries[0].Tick);
            Assert.AreEqual(12, ((AcceptCandidateCommand)script.Entries[0].Command).CandidateId);
            Assert.AreEqual(13, ((RejectCandidateCommand)script.Entries[1].Command).CandidateId);
            var autopause = (SetAutopauseCommand)script.Entries[2].Command;
            Assert.AreEqual(40, script.Entries[2].Tick);
            Assert.AreEqual(AutopauseKind.TraitRevealed, autopause.Kind);
            Assert.IsFalse(autopause.Enabled);
        }

        [Test]
        public void Recorder_WritesWhatParseReads()
        {
            var recorder = new ScenarioRecorder(9u, "Простой");
            recorder.Record(3, new AcceptCandidateCommand(4));
            recorder.Record(3, new SetAutopauseCommand(AutopauseKind.MemberLeftGuild, true));
            recorder.Record(8, new ActionCommand(_ => { }));

            Assert.AreEqual(2, recorder.Recorded);
            Assert.AreEqual(1, recorder.Unrecorded);
            StringAssert.Contains("3 accept 4\n3 autopause MemberLeftGuild on\n# 8 unsupported ActionCommand\n", recorder.ToString());

            ScenarioScript script = ScenarioScript.Parse(recorder.ToString());
            Assert.AreEqual(9u, script.Seed);
            Assert.AreEqual(2, script.Entries.Count);
        }

        [TestCase("1 accept 2\nfly 3", "line 2")]
        [TestCase("1 dance 2", "Unknown command 'dance'")]
        [TestCase("1 accept", "Argument 1 is missing")]
        [TestCase("1 accept x", "'x' is not an integer")]
        [TestCase("1 autopause Nothing on", "is not a AutopauseKind")]
        [TestCase("1 autopause TraitRevealed maybe", "must be on or off")]
        [TestCase("9 accept 1\n3 accept 2", "before the previous")]
        [TestCase("-1 accept 1", "is not a tick number")]
        public void Parse_Errors_NameTheLine(string text, string message)
        {
            var error = Assert.Throws<FormatException>(() => ScenarioScript.Parse(text));
            StringAssert.Contains(message, error.Message);
            StringAssert.StartsWith("Scenario line ", error.Message);
        }

        [Test]
        public void ScenarioRule_SendsInItsTick()
        {
            var sent = new List<(long tick, ICommand command)>();
            var turn = new BotTurn(null, command => sent.Add((-1, command)));
            var rule = new ScenarioRule(ScenarioScript.Parse("0 accept 1\n2 accept 2\n2 accept 3\n5 accept 4"));

            for (long tick = 0; tick < 5; tick++)
            {
                turn.Tick = tick;
                int before = sent.Count;
                rule.Act(turn);
                for (int i = before; i < sent.Count; i++) sent[i] = (tick, sent[i].command);
            }

            CollectionAssert.AreEqual(new long[] { 0, 2, 2 }, sent.Select(s => s.tick).ToArray());
        }
    }
}
