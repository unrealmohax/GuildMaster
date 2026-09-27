using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace GuildMaster.Core
{
    /// <summary>
    /// Лог симуляции. Строка: <c>[Год.Месяц.День ЧЧ:00] [Система] [Уровень] текст</c>, время — игровое, система — та, что
    /// сейчас работает (ставит <see cref="Simulation"/>). Пишет в <see cref="TextWriter"/> (файл прогона) и, если задан,
    /// в обработчик консоли. Конец строки — всегда <c>\n</c>, числа — без зависимости от культуры: одно зерно — один и тот же лог.
    /// <para>
    /// Выключенный уровень не строит строку: простые строки — перегрузки <see cref="Write{T0}"/> с аргументами (формат только
    /// при включённом уровне), сложные — под проверкой <see cref="IsOn"/> через <see cref="Begin"/> / <see cref="Commit"/>.
    /// </para>
    /// Случайных чисел логгер не тратит и мир не меняет. Один логгер — одна симуляция.
    /// </summary>
    public sealed class SimLogger
    {
        /// <summary>Логгер, который ничего не пишет: симуляция без лога.</summary>
        public static readonly SimLogger Disabled = new SimLogger();

        private readonly TextWriter writer;
        private readonly Action<SimLogLevel, string> console;
        private readonly int maxLevel;
        private readonly StringBuilder line = new StringBuilder(256);

        private Calendar calendar;
        private WorldState world;
        private long cachedHours = -1;
        private string cachedTime = string.Empty;
        private bool lineOpen;
        private SimLogLevel lineLevel;

        /// <param name="level">Самый подробный уровень, который пишется.</param>
        /// <param name="writer">Куда писать строки; <c>null</c> — только в консоль. Логгер его не закрывает.</param>
        /// <param name="console">Копия каждой строки (консоль Unity); <c>null</c> — не копировать.</param>
        public SimLogger(SimLogLevel level, TextWriter writer, Action<SimLogLevel, string> console = null)
        {
            Level = level;
            maxLevel = (int)level;
            this.writer = writer;
            this.console = console;
        }

        private SimLogger()
        {
            Level = SimLogLevel.Error;
            maxLevel = -1;
        }

        /// <summary>Самый подробный уровень, который пишется. У <see cref="Disabled"/> не пишется ни один.</summary>
        public SimLogLevel Level { get; }

        /// <summary>Сколько строк записано.</summary>
        public long LinesWritten { get; private set; }

        /// <summary>Имя системы для подписи строк; ставит симуляция перед каждым шагом такта.</summary>
        internal string Source { get; set; } = string.Empty;

        /// <summary>Пишется ли этот уровень. Строить сложную строку — только под этой проверкой.</summary>
        public bool IsOn(SimLogLevel level) => (int)level <= maxLevel;

        public void Write(SimLogLevel level, string text)
        {
            if (!IsOn(level)) return;
            Emit(level, Source, text);
        }

        public void Write<T0>(SimLogLevel level, string format, T0 arg0)
        {
            if (!IsOn(level)) return;
            Emit(level, Source, string.Format(CultureInfo.InvariantCulture, format, arg0));
        }

        public void Write<T0, T1>(SimLogLevel level, string format, T0 arg0, T1 arg1)
        {
            if (!IsOn(level)) return;
            Emit(level, Source, string.Format(CultureInfo.InvariantCulture, format, arg0, arg1));
        }

        public void Write<T0, T1, T2>(SimLogLevel level, string format, T0 arg0, T1 arg1, T2 arg2)
        {
            if (!IsOn(level)) return;
            Emit(level, Source, string.Format(CultureInfo.InvariantCulture, format, arg0, arg1, arg2));
        }

        public void Write<T0, T1, T2, T3>(SimLogLevel level, string format, T0 arg0, T1 arg1, T2 arg2, T3 arg3)
        {
            if (!IsOn(level)) return;
            Emit(level, Source, string.Format(CultureInfo.InvariantCulture, format, arg0, arg1, arg2, arg3));
        }

        /// <summary>Строка от имени того, кто не система такта (прогон, бот).</summary>
        public void WriteFrom(string source, SimLogLevel level, string text)
        {
            if (!IsOn(level)) return;
            Emit(level, source, text);
        }

        /// <summary>
        /// Начать сложную строку: возвращает построитель с уже записанной подписью, текст дописывает вызывающий,
        /// затем <see cref="Commit"/>. Вызывать только под <see cref="IsOn"/>.
        /// </summary>
        public StringBuilder Begin(SimLogLevel level)
        {
            if (!IsOn(level)) throw new InvalidOperationException($"Log level {level} is off: check IsOn before building a line");
            if (lineOpen) throw new InvalidOperationException("Previous log line is not committed");

            lineOpen = true;
            lineLevel = level;
            line.Clear();
            AppendPrefix(line, level, Source);
            return line;
        }

        /// <summary>Записать строку, начатую <see cref="Begin"/>.</summary>
        public void Commit()
        {
            if (!lineOpen) throw new InvalidOperationException("No log line to commit");
            lineOpen = false;
            Output(lineLevel, line.ToString());
        }

        /// <summary>Привязать к миру симуляции: время для подписи строк. Один логгер — одна симуляция.</summary>
        internal void Bind(Calendar simulationCalendar, WorldState simulationWorld)
        {
            if (maxLevel < 0) return;
            if (world != null && world != simulationWorld) throw new InvalidOperationException("Logger is already bound to another simulation");
            calendar = simulationCalendar;
            world = simulationWorld;
        }

        /// <summary>Строка события: подпись — время и система события, текст — <see cref="SimEvent.AppendLogText"/>.</summary>
        internal void WriteEvent(SimLogLevel level, SimEvent simEvent)
        {
            if (!IsOn(level)) return;
            if (lineOpen) throw new InvalidOperationException("Log line is being built: commit it first");
            line.Clear();
            AppendPrefix(line, level, simEvent.Source);
            simEvent.AppendLogText(line);
            Output(level, line.ToString());
        }

        private void Emit(SimLogLevel level, string source, string text)
        {
            if (lineOpen) throw new InvalidOperationException("Log line is being built: commit it first");
            line.Clear();
            AppendPrefix(line, level, source);
            line.Append(text);
            Output(level, line.ToString());
        }

        private void AppendPrefix(StringBuilder builder, SimLogLevel level, string source)
        {
            builder.Append('[').Append(TimeText()).Append("] [").Append(source).Append("] [").Append(LevelName(level)).Append("] ");
        }

        private void Output(SimLogLevel level, string text)
        {
            if (writer != null)
            {
                writer.Write(text);
                writer.Write('\n');
            }
            console?.Invoke(level, text);
            LinesWritten++;
        }

        private string TimeText()
        {
            if (world == null) return "-";
            long hours = world.Time.TotalHours;
            if (hours != cachedHours)
            {
                cachedHours = hours;
                cachedTime = calendar.At(hours).ToString();
            }
            return cachedTime;
        }

        private static string LevelName(SimLogLevel level)
        {
            switch (level)
            {
                case SimLogLevel.Error: return "Error";
                case SimLogLevel.Info: return "Info";
                case SimLogLevel.Debug: return "Debug";
                default: return "Trace";
            }
        }
    }
}
