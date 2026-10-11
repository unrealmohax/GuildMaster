using System;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Расчёт ожидаемого задания по ритму дня, без бросков: сколько суток займёт путь с ночлегами и сколько приёмов пищи
    /// попадёт в задание. Задержки от проваленных раундов не учитываются — их заранее не знает никто.
    /// <para>
    /// Задание из пути туда, одного раунда типа и пути назад идёт ходовыми часами; в часы ночлега путь и раунды стоят.
    /// Приём пищи считается, если в его час человек ещё на задании: в час, когда группа дошла домой, он ест уже в городе.
    /// </para>
    /// </summary>
    public static class TravelPlan
    {
        /// <summary>Ходовых часов всего задания: путь туда, один раунд, путь назад.</summary>
        public static int WorkHours(int travelHours, int roundHours) => 2 * Math.Max(1, travelHours) + Math.Max(1, roundHours);

        /// <summary>Сколько календарных часов займёт <paramref name="activeHours"/> ходовых, начатых в час суток <paramref name="startHour"/>.</summary>
        public static int CalendarHours(TimeBalance time, int startHour, int activeHours)
        {
            int hoursPerDay = Math.Max(1, time.HoursPerDay);
            int hour = Mod(startHour, hoursPerDay);
            int elapsed = 0;
            for (int left = activeHours; left > 0; elapsed++)
            {
                if (!IsCamp(time, hour)) left--;
                hour = (hour + 1) % hoursPerDay;
            }
            return elapsed;
        }

        /// <summary>
        /// Сколько приёмов пищи придётся на <paramref name="activeHours"/> ходовых часов задания, начатых в час суток
        /// <paramref name="startHour"/>: часы приёма, в которые человек ещё в пути или на месте (с ночлегами).
        /// </summary>
        public static int MealsDuring(TimeBalance time, FoodBalance food, int startHour, int activeHours)
        {
            int hoursPerDay = Math.Max(1, time.HoursPerDay);
            int hour = Mod(startHour, hoursPerDay);
            int meals = 0;
            for (int left = activeHours; ; )
            {
                if (!IsCamp(time, hour)) left--;
                if (left <= 0) break;
                if (food.IsMealHour(hour)) meals++;
                hour = (hour + 1) % hoursPerDay;
            }
            return meals;
        }

        /// <summary>Приёмов пищи за ожидаемое задание по заказу, если выйти в час суток <paramref name="startHour"/>.</summary>
        public static int ExpectedMeals(Order order, DataRegistry data, int startHour) =>
            MealsDuring(data.Balance.Time, data.Balance.Food, startHour, WorkHours(order.TravelHours, RoundHours(order.TypeId, data)));

        /// <summary>
        /// Приёмов пищи за ожидаемое задание при выходе в начале утра — так люди прикидывают цену пайков, выбирая заказ.
        /// Считается один раз на заказ.
        /// </summary>
        public static int ExpectedMeals(Order order, DataRegistry data)
        {
            if (order.ExpectedMealsCache < 0) order.ExpectedMealsCache = ExpectedMeals(order, data, data.Balance.Time.MorningHour);
            return order.ExpectedMealsCache;
        }

        /// <summary>Длительность раунда типа задания, ходовых часов (не меньше часа; тип неизвестен — час).</summary>
        public static int RoundHours(string typeId, DataRegistry data) =>
            data.TryGet(typeId, out QuestTypeDefinition type) ? Math.Max(1, type.RoundHours) : 1;

        private static bool IsCamp(TimeBalance time, int hour) => hour >= time.CampHour || hour < time.MorningHour;

        private static int Mod(int value, int divisor) => ((value % divisor) + divisor) % divisor;
    }
}
