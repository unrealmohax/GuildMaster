using System;
using System.Collections.Generic;
using UnityEngine;

namespace GuildMaster.Data
{
    /// <summary>
    /// Раздел <c>Time</c>: календарь и ритм дня (numbers.md → Время, ТЗ 03).
    /// </summary>
    [Serializable]
    public sealed class TimeBalance
    {
        [Tooltip("Часов в сутках")]
        [SerializeField, Min(1)] private int hoursPerDay = 24;

        [Tooltip("Дней в месяце")]
        [SerializeField, Min(1)] private int daysPerMonth = 30;

        [Tooltip("Месяцев в году")]
        [SerializeField, Min(1)] private int monthsPerYear = 12;

        [Tooltip("Час старта игры (день 1 месяца 1 года 1)")]
        [SerializeField, Min(0)] private int startHour = 6;

        [Tooltip("Начало утра")]
        [SerializeField, Min(0)] private int morningHour = 6;

        [Tooltip("Начало дня (конец утра)")]
        [SerializeField, Min(0)] private int dayHour = 9;

        [Tooltip("Начало вечера")]
        [SerializeField, Min(0)] private int eveningHour = 18;

        [Tooltip("Начало ночи")]
        [SerializeField, Min(0)] private int nightHour = 22;

        [Tooltip("С этого часа группы в пути ночуют (до начала утра)")]
        [SerializeField, Min(0)] private int campHour = 20;

        [Tooltip("После этого часа новые задания не начинаются")]
        [SerializeField, Min(0)] private int latestDepartureHour = 14;

        [Tooltip("Секунд реального времени на игровой час при скорости ×1")]
        [SerializeField, Min(0.01f)] private float realSecondsPerHour = 1f;

        [Tooltip("Скорости игры (множители к ×1)")]
        [SerializeField] private List<int> speedMultipliers = new List<int> { 1, 2, 4 };

        [Tooltip("Отладочная скорость (только отладочная сборка)")]
        [SerializeField, Min(1)] private int debugSpeedMultiplier = 50;

        [Tooltip("Не больше тактов за кадр, если кадр подвис")]
        [SerializeField, Min(1)] private int maxTicksPerFrame = 10;

        public int HoursPerDay => hoursPerDay;
        public int DaysPerMonth => daysPerMonth;
        public int MonthsPerYear => monthsPerYear;
        public int StartHour => startHour;
        public int MorningHour => morningHour;
        public int DayHour => dayHour;
        public int EveningHour => eveningHour;
        public int NightHour => nightHour;
        public int CampHour => campHour;
        public int LatestDepartureHour => latestDepartureHour;
        public float RealSecondsPerHour => realSecondsPerHour;
        public IReadOnlyList<int> SpeedMultipliers => speedMultipliers;
        public int DebugSpeedMultiplier => debugSpeedMultiplier;
        public int MaxTicksPerFrame => maxTicksPerFrame;
    }
}
