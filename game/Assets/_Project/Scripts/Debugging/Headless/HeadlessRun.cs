using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using GuildMaster.Core;
using GuildMaster.Data;

namespace GuildMaster.Debugging
{
    /// <summary>
    /// Прогон симуляции без интерфейса, так быстро, как получится. Перед каждым тактом ходит бот игрока — отдаёт команды
    /// через очередь, как игрок; его команды пишутся в лог (<see cref="BotSource"/>) и в сценарий, которым игру можно повторить.
    /// Во время прогона собирается сводка (<see cref="SummaryRecorder"/>). Если гильдия закрылась (игра проиграна), прогон
    /// кончается раньше срока.
    /// </summary>
    public static class HeadlessRun
    {
        public const string RunSource = "HeadlessRun";
        public const string BotSource = "Bot";

        /// <summary>Итог одного прогона.</summary>
        public sealed class Result
        {
            internal Result(Simulation simulation, long ticks, long events, double elapsedSeconds, RunSummary summary, ScenarioRecorder scenario)
            {
                Simulation = simulation;
                Ticks = ticks;
                Events = events;
                ElapsedSeconds = elapsedSeconds;
                Summary = summary;
                Scenario = scenario;
            }

            public Simulation Simulation { get; }
            public uint Seed => Simulation.MasterSeed;
            public long Ticks { get; }
            public long Events { get; }
            public GameTime FinalTime => Simulation.World.Time;

            /// <summary>Гильдия закрылась — прогон кончился раньше срока.</summary>
            public bool GuildClosed => Simulation.IsFinished;
            public double ElapsedSeconds { get; }
            public long LogLines => Simulation.Log.LinesWritten;
            public RunSummary Summary { get; }

            /// <summary>Команды бота текстом сценария (<see cref="ScenarioScript"/>).</summary>
            public ScenarioRecorder Scenario { get; }
        }

        /// <summary>
        /// Один прогон на <paramref name="days"/> суток. Лог — в <paramref name="log"/> (<c>null</c> — без лога); первой строкой —
        /// параметры прогона. Исключение в симуляции пишется в лог уровнем <see cref="SimLogLevel.Error"/> и пробрасывается.
        /// </summary>
        public static Result Run(DataRegistry data, uint seed, int days, PlayerBot bot, SimLogger log = null)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (bot == null) throw new ArgumentNullException(nameof(bot));
            if (days < 0) throw new ArgumentOutOfRangeException(nameof(days));

            log = log ?? SimLogger.Disabled;
            log.WriteFrom(RunSource, SimLogLevel.Info, string.Format(CultureInfo.InvariantCulture,
                "run seed={0} days={1} bot={2} level={3}", seed, days, bot.Name, log.Level));

            Stopwatch stopwatch = Stopwatch.StartNew();
            Simulation simulation = null;
            try
            {
                simulation = Simulation.CreateDefault(data, seed, log);
                long events = 0;
                simulation.TickCompleted += tickEvents => events += tickEvents.Count;

                var scenario = new ScenarioRecorder(seed, bot.Name);
                var turn = new BotTurn(simulation, command => SendFromBot(simulation, scenario, command));

                long ticks = simulation.Calendar.DaysToHours(days);
                using (var summary = new SummaryRecorder(simulation))
                {
                    for (long i = 0; i < ticks && !simulation.IsFinished; i++)
                    {
                        turn.Tick = simulation.TicksDone;
                        bot.Act(turn);
                        simulation.Tick();
                    }
                    stopwatch.Stop();
                    ticks = simulation.TicksDone;
                    if (simulation.IsFinished)
                        log.WriteFrom(RunSource, SimLogLevel.Info, string.Format(CultureInfo.InvariantCulture,
                            "guild closed at {0} after {1} ticks", simulation.World.Time, ticks));
                    return new Result(simulation, ticks, events, stopwatch.Elapsed.TotalSeconds, summary.Finish(bot.Name, days), scenario);
                }
            }
            catch (Exception e)
            {
                log.WriteFrom(RunSource, SimLogLevel.Error, e.ToString().Replace("\r\n", "\n").Replace('\n', ' '));
                throw;
            }
        }

        private static void SendFromBot(Simulation simulation, ScenarioRecorder scenario, ICommand command)
        {
            string text = scenario.Record(simulation.TicksDone, command);
            simulation.Log.WriteFrom(BotSource, SimLogLevel.Info, "send " + (text ?? command.GetType().Name));
            simulation.Send(command);
        }

        /// <summary>Что задать для прогона в файлы.</summary>
        public sealed class Options
        {
            public uint Seed;
            public int Days = 360;
            public SimLogLevel LogLevel = SimLogLevel.Info;
            public int Runs = 1;

            /// <summary>Бот на каждый прогон заново (у сценария своё состояние).</summary>
            public Func<PlayerBot> Bot = PlayerBots.Simple;

            /// <summary>Копия строк лога в консоль Unity.</summary>
            public bool LogToConsole;

            /// <summary>Папка для лога, сводки и сценария; по умолчанию — <see cref="LogFiles.DefaultFolder"/>.</summary>
            public string Folder;
        }

        /// <summary>Итог прогона в файлы: прогоны, их файлы, свёртка (если прогонов больше одного).</summary>
        public sealed class Batch
        {
            internal Batch(List<Result> results, List<string> files, SummaryAggregate aggregate, bool cancelled)
            {
                Results = results;
                Files = files;
                Aggregate = aggregate;
                Cancelled = cancelled;
            }

            public IReadOnlyList<Result> Results { get; }
            public IReadOnlyList<string> Files { get; }
            public SummaryAggregate Aggregate { get; }
            public bool Cancelled { get; }
        }

        /// <summary>
        /// Прогоны в файлы папки <see cref="Options.Folder"/>. Прогон <c>i</c> идёт с зерном <c>Seed + i</c>. На каждый —
        /// лог <c>sim_{зерно}_{дата}.log</c>, сводка <c>summary_{зерно}.csv</c>, сценарий <c>scenario_{зерно}.txt</c>; при нескольких
        /// прогонах — ещё <c>summary_{зерно}_x{прогонов}.csv</c> со средними и разбросом.
        /// </summary>
        /// <param name="progress">Перед каждым прогоном: (номер, всего) → продолжать ли. <c>null</c> — не спрашивать.</param>
        public static Batch RunToFiles(GameConfig config, Options options, Func<int, int, bool> progress = null)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (options.Runs < 1) throw new ArgumentOutOfRangeException(nameof(options), "At least one run is required");

            DataRegistry data = DataRegistry.FromConfig(config);
            string folder = string.IsNullOrEmpty(options.Folder) ? LogFiles.DefaultFolder : options.Folder;
            Directory.CreateDirectory(folder);

            var results = new List<Result>(options.Runs);
            var files = new List<string>();
            string stamp = LogFiles.Stamp(DateTime.Now);
            bool cancelled = false;
            for (int i = 0; i < options.Runs; i++)
            {
                if (progress != null && !progress(i, options.Runs))
                {
                    cancelled = true;
                    break;
                }

                uint seed = unchecked(options.Seed + (uint)i);
                string logPath = Path.Combine(folder, LogFiles.LogName(seed, stamp));
                Result result;
                using (var writer = new StreamWriter(logPath, false, new UTF8Encoding(false), 1 << 16))
                {
                    var log = new SimLogger(options.LogLevel, writer, options.LogToConsole ? LogFiles.UnityConsole : null);
                    result = Run(data, seed, options.Days, options.Bot(), log);
                }
                results.Add(result);
                files.Add(logPath);
                files.Add(WriteText(Path.Combine(folder, LogFiles.SummaryName(seed)), SummaryCsv.Write(result.Summary), bom: true));
                files.Add(WriteText(Path.Combine(folder, LogFiles.ScenarioName(seed)), result.Scenario.ToString(), bom: false));
            }

            SummaryAggregate aggregate = null;
            if (results.Count > 1)
            {
                var summaries = new List<RunSummary>(results.Count);
                foreach (Result result in results) summaries.Add(result.Summary);
                aggregate = SummaryAggregate.Of(summaries);
                files.Add(WriteText(Path.Combine(folder, LogFiles.AggregateName(options.Seed, results.Count)), SummaryCsv.Write(aggregate), bom: true));
            }
            return new Batch(results, files, aggregate, cancelled);
        }

        public static uint NewRandomSeed() => unchecked((uint)Guid.NewGuid().GetHashCode());

        /// <summary>CSV — с меткой UTF-8 (BOM), чтобы Excel прочёл кириллицу.</summary>
        private static string WriteText(string path, string text, bool bom)
        {
            File.WriteAllText(path, text, new UTF8Encoding(bom));
            return path;
        }
    }
}
