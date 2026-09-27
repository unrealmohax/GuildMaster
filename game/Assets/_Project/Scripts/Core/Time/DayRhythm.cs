using System;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Ритм дня: фаза по часу, ночлег групп в пути и допустимое время выхода на задание.
    /// Часы фаз читаются из <see cref="TimeBalance"/> при каждом вызове, поэтому правка чисел действует сразу.
    /// <para>
    /// Час H — такт, в котором мир стоит на H:00; он тратит час от H:00 до H+1:00. «Час ночлега» — такт, в котором
    /// группа в пути стоит; «час выхода» — такт, в котором задание может начаться.
    /// </para>
    /// </summary>
    public sealed class DayRhythm
    {
        private readonly Calendar calendar;
        private readonly TimeBalance time;

        public DayRhythm(Calendar calendar, TimeBalance time)
        {
            this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
            this.time = time ?? throw new ArgumentNullException(nameof(time));

            if (!(0 <= time.MorningHour && time.MorningHour < time.DayHour && time.DayHour < time.EveningHour
                  && time.EveningHour < time.NightHour && time.NightHour < calendar.HoursPerDay))
                throw new ArgumentException("Day phases must go in order: morning < day < evening < night within the day", nameof(time));
            if (!(time.MorningHour <= time.LatestDepartureHour && time.LatestDepartureHour < time.CampHour && time.CampHour < calendar.HoursPerDay))
                throw new ArgumentException("Expected morning <= latest departure < camp hour within the day", nameof(time));
        }

        public DayPhase PhaseAt(GameTime at) => PhaseAt(at.Hour);

        public DayPhase PhaseAt(int hour)
        {
            if (hour >= time.NightHour || hour < time.MorningHour) return DayPhase.Night;
            if (hour >= time.EveningHour) return DayPhase.Evening;
            if (hour >= time.DayHour) return DayPhase.Day;
            return DayPhase.Morning;
        }

        /// <summary>Группа в пути ночует: с <see cref="TimeBalance.CampHour"/> до начала утра. Раунды заданий тоже стоят.</summary>
        public bool IsCampHour(GameTime at) => IsCampHour(at.Hour);

        public bool IsCampHour(int hour) => hour >= time.CampHour || hour < time.MorningHour;

        /// <summary>Задание может начаться: с начала утра до <see cref="TimeBalance.LatestDepartureHour"/> включительно.</summary>
        public bool CanStartQuest(GameTime at) => CanStartQuest(at.Hour);

        public bool CanStartQuest(int hour) => hour >= time.MorningHour && hour <= time.LatestDepartureHour;

        /// <summary>Ближайший час (в <see cref="GameTime.TotalHours"/>), не раньше данного, когда задание может начаться.</summary>
        public long NextQuestStart(long totalHours) =>
            CanStartQuest(HourOf(totalHours)) ? totalHours : totalHours + HoursUntilMorning(totalHours);

        /// <summary>Когда группа снова пойдёт: данный час, если это не час ночлега, иначе ближайшее утро.</summary>
        public long CampEnd(long totalHours) =>
            IsCampHour(HourOf(totalHours)) ? totalHours + HoursUntilMorning(totalHours) : totalHours;

        /// <summary>
        /// Конец отрезка в <paramref name="activeHours"/> часов пути (или раундов), начатого в <paramref name="start"/>,
        /// с остановками на ночлег. Пример при ночлеге с 20:00 и утре с 06:00: 19:00 + 2 часа → 07:00 следующих суток.
        /// </summary>
        public long MarchEnd(long start, int activeHours)
        {
            if (activeHours < 0) throw new ArgumentOutOfRangeException(nameof(activeHours));

            long at = start;
            for (int left = activeHours; left > 0; left--)
            {
                at = CampEnd(at) + 1;
            }
            return at;
        }

        private int HourOf(long totalHours) => (int)(totalHours % calendar.HoursPerDay);

        private int HoursUntilMorning(long totalHours) =>
            (time.MorningHour - HourOf(totalHours) + calendar.HoursPerDay) % calendar.HoursPerDay;
    }
}
