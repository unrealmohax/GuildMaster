using System;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Перевод <see cref="GameTime.TotalHours"/> в год, месяц, день и час по настройкам <see cref="TimeBalance"/>.
    /// </summary>
    public sealed class Calendar
    {
        public Calendar(TimeBalance time)
        {
            if (time == null) throw new ArgumentNullException(nameof(time));
            if (time.HoursPerDay < 1 || time.DaysPerMonth < 1 || time.MonthsPerYear < 1)
                throw new ArgumentException("Calendar sizes must be positive", nameof(time));
            if (time.StartHour < 0 || time.StartHour >= time.HoursPerDay)
                throw new ArgumentException($"Start hour {time.StartHour} is outside the day", nameof(time));

            HoursPerDay = time.HoursPerDay;
            DaysPerMonth = time.DaysPerMonth;
            MonthsPerYear = time.MonthsPerYear;
            StartTotalHours = time.StartHour;
        }

        public int HoursPerDay { get; }
        public int DaysPerMonth { get; }
        public int MonthsPerYear { get; }
        public long HoursPerMonth => (long)HoursPerDay * DaysPerMonth;
        public long HoursPerYear => HoursPerMonth * MonthsPerYear;

        /// <summary>Момент старта игры: год 1, месяц 1, день 1, час старта из настроек.</summary>
        public long StartTotalHours { get; }

        public GameTime At(long totalHours)
        {
            if (totalHours < 0) throw new ArgumentOutOfRangeException(nameof(totalHours), "Time before year 1");

            long year = totalHours / HoursPerYear;
            long rest = totalHours % HoursPerYear;
            long month = rest / HoursPerMonth;
            rest %= HoursPerMonth;
            long day = rest / HoursPerDay;
            long hour = rest % HoursPerDay;
            return new GameTime(totalHours, (int)year + 1, (int)month + 1, (int)day + 1, (int)hour);
        }

        public long ToTotalHours(int year, int month, int day, int hour)
        {
            if (year < 1) throw new ArgumentOutOfRangeException(nameof(year));
            if (month < 1 || month > MonthsPerYear) throw new ArgumentOutOfRangeException(nameof(month));
            if (day < 1 || day > DaysPerMonth) throw new ArgumentOutOfRangeException(nameof(day));
            if (hour < 0 || hour >= HoursPerDay) throw new ArgumentOutOfRangeException(nameof(hour));
            return (year - 1) * HoursPerYear + (month - 1) * HoursPerMonth + (long)(day - 1) * HoursPerDay + hour;
        }

        public long DaysToHours(int days) => (long)days * HoursPerDay;
    }
}
