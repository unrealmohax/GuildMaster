using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace GuildMaster.Core
{
    /// <summary>
    /// Внутреннее событие симуляции. Создаётся только через <see cref="EventBus.Publish"/>: шина сама ставит
    /// время и имя системы-источника. Полезные данные — пары «ключ → значение» в порядке добавления.
    /// </summary>
    public sealed class SimEvent
    {
        private static readonly int[] NoParticipants = Array.Empty<int>();

        private readonly List<KeyValuePair<string, object>> payload = new List<KeyValuePair<string, object>>();

        internal SimEvent(SimEventType type, EventImportance importance, long timeHours, string source, int[] participants)
        {
            Type = type;
            Importance = importance;
            TimeHours = timeHours;
            Source = source;
            Participants = participants == null || participants.Length == 0
                ? NoParticipants
                : (int[])participants.Clone();
        }

        public SimEventType Type { get; }
        public EventImportance Importance { get; }
        public long TimeHours { get; }

        /// <summary>Имя системы, опубликовавшей событие.</summary>
        public string Source { get; }

        /// <summary>Id участников (людей, заданий, групп).</summary>
        public IReadOnlyList<int> Participants { get; }

        public IReadOnlyList<KeyValuePair<string, object>> Payload => payload;

        /// <summary>Добавить значение. Ключи уникальны.</summary>
        public SimEvent With(string key, object value)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("Payload key is required", nameof(key));
            foreach (KeyValuePair<string, object> pair in payload)
            {
                if (pair.Key == key) throw new ArgumentException($"Payload key '{key}' is already set", nameof(key));
            }
            payload.Add(new KeyValuePair<string, object>(key, value));
            return this;
        }

        public bool TryGet<T>(string key, out T value)
        {
            foreach (KeyValuePair<string, object> pair in payload)
            {
                if (pair.Key == key && pair.Value is T typed)
                {
                    value = typed;
                    return true;
                }
            }
            value = default;
            return false;
        }

        /// <summary>
        /// Строка для лога, не зависящая от культуры: «[1.1.1 07:00] [TimeSystem] [Normal] HourStarted ids=3,5 key=value».
        /// </summary>
        public string ToLogLine(Calendar calendar)
        {
            var line = new StringBuilder(64);
            line.Append('[').Append(calendar.At(TimeHours)).Append("] [")
                .Append(Source).Append("] [")
                .Append(Importance).Append("] ")
                .Append(Type);

            if (Participants.Count > 0)
            {
                line.Append(" ids=");
                for (int i = 0; i < Participants.Count; i++)
                {
                    if (i > 0) line.Append(',');
                    line.Append(Participants[i].ToString(CultureInfo.InvariantCulture));
                }
            }

            foreach (KeyValuePair<string, object> pair in payload)
            {
                line.Append(' ').Append(pair.Key).Append('=').Append(FormatValue(pair.Value));
            }
            return line.ToString();
        }

        private static string FormatValue(object value)
        {
            switch (value)
            {
                case null: return "null";
                case float f: return f.ToString("R", CultureInfo.InvariantCulture);
                case double d: return d.ToString("R", CultureInfo.InvariantCulture);
                case IFormattable formattable: return formattable.ToString(null, CultureInfo.InvariantCulture);
                default: return value.ToString();
            }
        }
    }
}
