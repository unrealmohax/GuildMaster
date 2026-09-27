namespace GuildMaster.Core
{
    /// <summary>
    /// Уровень строки лога симуляции. Логгер пишет строки своего уровня и всех, что выше по списку:
    /// на <see cref="Info"/> — ошибки и важное, на <see cref="Trace"/> — всё.
    /// </summary>
    public enum SimLogLevel
    {
        /// <summary>Ошибки прогона.</summary>
        Error,

        /// <summary>Важное: события симуляции, решения, деньги.</summary>
        Info,

        /// <summary>Все броски: шанс, выпавшее число, итог; генерация людей со скрытым.</summary>
        Debug,

        /// <summary>Промежуточные расчёты и показатели каждого человека раз в сутки.</summary>
        Trace,
    }
}
