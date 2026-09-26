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
        DayStarted,
        MorningStarted,
        EveningStarted,
        NightStarted,
        MonthStarted,
    }
}
