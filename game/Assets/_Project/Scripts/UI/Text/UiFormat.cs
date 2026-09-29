using System.Globalization;
using GuildMaster.Core;
using GuildMaster.Data;
using Calendar = GuildMaster.Core.Calendar;

namespace GuildMaster.UI
{
    /// <summary>Числа, даты и сроки словами для экранов.</summary>
    public static class UiFormat
    {
        private static readonly NumberFormatInfo Thousands = new NumberFormatInfo { NumberGroupSeparator = " ", NumberGroupSizes = new[] { 3 } };

        /// <summary>«День 12, месяц 1, год 1 · 14:00».</summary>
        public static string Date(GameTime time) =>
            string.Format(CultureInfo.InvariantCulture, UiStrings.DateFormat, time.Day, time.Month, time.Year, time.Hour);

        /// <summary>Короткая метка времени для строк ленты: «12.1 14:00».</summary>
        public static string Stamp(GameTime time) =>
            string.Format(CultureInfo.InvariantCulture, "{0}.{1} {2:00}:00", time.Day, time.Month, time.Hour);

        public static string Phase(DayPhase phase)
        {
            int index = (int)phase;
            return index >= 0 && index < UiStrings.DayPhases.Length ? UiStrings.DayPhases[index] : phase.ToString();
        }

        /// <summary>Деньги с разделителем тысяч: «12 480», «−350».</summary>
        public static string Money(long amount) =>
            (amount < 0 ? "−" : string.Empty) + System.Math.Abs(amount).ToString("#,0", Thousands);

        public static string Signed(long amount) => amount > 0 ? "+" + Money(amount) : Money(amount);

        /// <summary>
        /// Срок словами по календарю: «3 мес. 12 дн.», «4 дн. 6 ч», «5 ч». Меньше часа — «0 ч»; отрицательный — как ноль.
        /// </summary>
        public static string Duration(long hours, Calendar calendar)
        {
            if (hours < 0) hours = 0;
            long months = hours / calendar.HoursPerMonth;
            long rest = hours % calendar.HoursPerMonth;
            long days = rest / calendar.HoursPerDay;
            long h = rest % calendar.HoursPerDay;

            if (months > 0) return days > 0 ? $"{months} мес. {days} дн." : $"{months} мес.";
            if (days > 0) return h > 0 ? $"{days} дн. {h} ч" : $"{days} дн.";
            return $"{h} ч";
        }

        /// <summary>Целые дни до срока, округление вверх (для «ещё 3 дн.»).</summary>
        public static int DaysLeft(long untilHours, long nowHours, Calendar calendar)
        {
            long left = untilHours - nowHours;
            if (left <= 0) return 0;
            return (int)((left + calendar.HoursPerDay - 1) / calendar.HoursPerDay);
        }

        public static string Number(float value) => value.ToString("0", CultureInfo.InvariantCulture);

        public static string Rank(GuildRank rank) => rank.ToString();

        public static string Percent(float fraction) => (fraction * 100f).ToString("0", CultureInfo.InvariantCulture) + "%";
    }
}
