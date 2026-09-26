namespace GuildMaster.Core
{
    /// <summary>
    /// Типы событий симуляции. Системы добавляют свои типы в своих ТЗ (08–13).
    /// </summary>
    public enum SimEventType
    {
        /// <summary>Отладочное событие: тесты, отладочная панель (ТЗ 16).</summary>
        Debug,

        // Время (ТЗ 03)
        HourStarted,

        /// <summary>Новые сутки, 00:00.</summary>
        DayStarted,

        MorningStarted,

        /// <summary>Начало дневной фазы (09:00), не путать с <see cref="DayStarted"/>.</summary>
        DaytimeStarted,

        EveningStarted,
        NightStarted,
        MonthStarted,

        /// <summary>Игрок включил или выключил вид автопаузы (ТЗ 03).</summary>
        AutopauseChanged,
    }
}
