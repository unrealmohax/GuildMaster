using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using GuildMaster.Core;

namespace GuildMaster.Debugging
{
    /// <summary>
    /// Как команда записывается в сценарий: слово, тип команды, разбор аргументов и запись обратно.
    /// </summary>
    public sealed class ScenarioCommandFormat
    {
        public ScenarioCommandFormat(string keyword, Type commandType, Func<string[], ICommand> parse, Func<ICommand, string> format)
        {
            Keyword = keyword;
            CommandType = commandType;
            Parse = parse;
            Format = format;
        }

        public string Keyword { get; }
        public Type CommandType { get; }

        /// <summary>Аргументы после слова команды → команда. Ошибка — <see cref="FormatException"/>.</summary>
        public Func<string[], ICommand> Parse { get; }

        /// <summary>Команда → аргументы строкой (без слова и такта).</summary>
        public Func<ICommand, string> Format { get; }
    }

    /// <summary>
    /// Команды игрока, которые умеет сценарий. Новая команда игрока — строка в <see cref="Formats"/>.
    /// </summary>
    public static class ScenarioCommands
    {
        public static readonly IReadOnlyList<ScenarioCommandFormat> Formats = new[]
        {
            new ScenarioCommandFormat("accept", typeof(AcceptCandidateCommand),
                args => new AcceptCandidateCommand(ParseInt(args, 0)),
                command => Int(((AcceptCandidateCommand)command).CandidateId)),
            new ScenarioCommandFormat("reject", typeof(RejectCandidateCommand),
                args => new RejectCandidateCommand(ParseInt(args, 0)),
                command => Int(((RejectCandidateCommand)command).CandidateId)),
            new ScenarioCommandFormat("autopause", typeof(SetAutopauseCommand),
                args => new SetAutopauseCommand(ParseEnum<AutopauseKind>(args, 0), ParseOnOff(args, 1)),
                command =>
                {
                    var autopause = (SetAutopauseCommand)command;
                    return autopause.Kind + (autopause.Enabled ? " on" : " off");
                }),
        };

        public static ICommand Parse(string keyword, string[] args)
        {
            foreach (ScenarioCommandFormat format in Formats)
            {
                if (format.Keyword == keyword) return format.Parse(args);
            }
            throw new FormatException($"Unknown command '{keyword}'");
        }

        /// <summary>Команда строкой «слово аргументы»; false — такую команду сценарий не умеет.</summary>
        public static bool TryFormat(ICommand command, out string text)
        {
            foreach (ScenarioCommandFormat format in Formats)
            {
                if (format.CommandType != command.GetType()) continue;
                string args = format.Format(command);
                text = args.Length > 0 ? format.Keyword + " " + args : format.Keyword;
                return true;
            }
            text = null;
            return false;
        }

        private static string Int(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static string Arg(string[] args, int index) =>
            index < args.Length ? args[index] : throw new FormatException($"Argument {index + 1} is missing");

        private static int ParseInt(string[] args, int index) =>
            int.TryParse(Arg(args, index), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
                ? value
                : throw new FormatException($"'{args[index]}' is not an integer");

        private static T ParseEnum<T>(string[] args, int index) where T : struct =>
            Enum.TryParse(Arg(args, index), false, out T value) && Enum.IsDefined(typeof(T), value)
                ? value
                : throw new FormatException($"'{args[index]}' is not a {typeof(T).Name}");

        private static bool ParseOnOff(string[] args, int index)
        {
            switch (Arg(args, index))
            {
                case "on": return true;
                case "off": return false;
                default: throw new FormatException($"'{args[index]}' must be on or off");
            }
        }
    }

    /// <summary>
    /// Сценарий: команды игрока по тактам — для повторения конкретной игры. Текстовый формат, строка на команду:
    /// <c>такт слово аргументы</c>, например <c>52 accept 7</c>. Такт — сколько тактов сделано к моменту отправки (с нуля):
    /// команда применится в начале этого такта. Такты не убывают. Пустые строки и строки с <c>#</c> — комментарии;
    /// <c># seed=N</c> — зерно, с которым сценарий записан (ид кандидатов зависят от него).
    /// </summary>
    public sealed class ScenarioScript
    {
        public readonly struct Entry
        {
            public Entry(long tick, ICommand command)
            {
                Tick = tick;
                Command = command;
            }

            public long Tick { get; }
            public ICommand Command { get; }
        }

        private readonly List<Entry> entries;

        public ScenarioScript(IEnumerable<Entry> entries, uint? seed = null)
        {
            this.entries = new List<Entry>(entries);
            for (int i = 1; i < this.entries.Count; i++)
            {
                if (this.entries[i].Tick < this.entries[i - 1].Tick) throw new ArgumentException("Scenario ticks must not decrease", nameof(entries));
            }
            Seed = seed;
        }

        public IReadOnlyList<Entry> Entries => entries;

        /// <summary>Зерно из заголовка <c># seed=N</c>; <c>null</c> — не указано.</summary>
        public uint? Seed { get; }

        /// <summary>Разобрать текст сценария. Ошибка — <see cref="FormatException"/> с номером строки.</summary>
        public static ScenarioScript Parse(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));

            var parsed = new List<Entry>();
            uint? seed = null;
            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0) continue;
                if (line[0] == '#')
                {
                    seed = seed ?? ParseSeed(line);
                    continue;
                }

                try
                {
                    string[] parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 2) throw new FormatException("Expected 'tick command arguments'");
                    if (!long.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out long tick) || tick < 0)
                        throw new FormatException($"'{parts[0]}' is not a tick number");
                    if (parsed.Count > 0 && tick < parsed[parsed.Count - 1].Tick)
                        throw new FormatException($"Tick {tick} is before the previous command's tick {parsed[parsed.Count - 1].Tick}");

                    var args = new string[parts.Length - 2];
                    Array.Copy(parts, 2, args, 0, args.Length);
                    parsed.Add(new Entry(tick, ScenarioCommands.Parse(parts[1], args)));
                }
                catch (FormatException e)
                {
                    throw new FormatException($"Scenario line {i + 1}: {e.Message}", e);
                }
            }
            return new ScenarioScript(parsed, seed);
        }

        private static uint? ParseSeed(string comment)
        {
            string body = comment.TrimStart('#').Trim();
            const string prefix = "seed=";
            if (!body.StartsWith(prefix, StringComparison.Ordinal)) return null;
            return uint.TryParse(body.Substring(prefix.Length).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out uint seed) ? seed : (uint?)null;
        }
    }

    /// <summary>
    /// Запись команд бота в текст сценария. Команду, которую сценарий не умеет, пишет комментарием и считает в
    /// <see cref="Unrecorded"/> — такой сценарий игру не повторит.
    /// </summary>
    public sealed class ScenarioRecorder
    {
        private readonly StringBuilder text = new StringBuilder();

        public ScenarioRecorder(uint seed, string botName)
        {
            text.Append("# seed=").Append(seed.ToString(CultureInfo.InvariantCulture)).Append('\n');
            text.Append("# bot: ").Append(botName).Append('\n');
            text.Append("# tick command arguments\n");
        }

        public int Recorded { get; private set; }
        public int Unrecorded { get; private set; }

        /// <summary>Команда строкой сценария без такта; <c>null</c> — сценарий её не умеет.</summary>
        public string Record(long tick, ICommand command)
        {
            string tickText = tick.ToString(CultureInfo.InvariantCulture);
            if (ScenarioCommands.TryFormat(command, out string commandText))
            {
                text.Append(tickText).Append(' ').Append(commandText).Append('\n');
                Recorded++;
                return commandText;
            }

            text.Append("# ").Append(tickText).Append(" unsupported ").Append(command.GetType().Name).Append('\n');
            Unrecorded++;
            return null;
        }

        public override string ToString() => text.ToString();
    }

    /// <summary>Правило бота «Сценарий»: в свой такт отправляет команды сценария по порядку.</summary>
    public sealed class ScenarioRule : IBotRule
    {
        private readonly ScenarioScript script;
        private int next;

        public ScenarioRule(ScenarioScript script)
        {
            this.script = script ?? throw new ArgumentNullException(nameof(script));
        }

        public void Act(BotTurn turn)
        {
            IReadOnlyList<ScenarioScript.Entry> entries = script.Entries;
            while (next < entries.Count && entries[next].Tick <= turn.Tick)
            {
                turn.Send(entries[next].Command);
                next++;
            }
        }
    }
}
