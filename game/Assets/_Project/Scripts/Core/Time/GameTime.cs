using System;
using System.Globalization;

namespace GuildMaster.Core
{
    /// <summary>
    /// Момент игрового времени. <see cref="TotalHours"/> — часы от 00:00 дня 1 месяца 1 года 1;
    /// все сроки в симуляции хранятся в нём. Календарные поля считает <see cref="Calendar"/>.
    /// </summary>
    public readonly struct GameTime : IEquatable<GameTime>
    {
        public GameTime(long totalHours, int year, int month, int day, int hour)
        {
            TotalHours = totalHours;
            Year = year;
            Month = month;
            Day = day;
            Hour = hour;
        }

        public long TotalHours { get; }
        public int Year { get; }
        public int Month { get; }
        public int Day { get; }
        public int Hour { get; }

        public bool Equals(GameTime other) => TotalHours == other.TotalHours;
        public override bool Equals(object obj) => obj is GameTime other && Equals(other);
        public override int GetHashCode() => TotalHours.GetHashCode();
        public static bool operator ==(GameTime a, GameTime b) => a.Equals(b);
        public static bool operator !=(GameTime a, GameTime b) => !a.Equals(b);

        /// <summary>Формат лога: «Год.Месяц.День ЧЧ:00».</summary>
        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture, "{0}.{1}.{2} {3:00}:00", Year, Month, Day, Hour);
    }
}
