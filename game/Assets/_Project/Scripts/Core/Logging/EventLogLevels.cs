using System.Collections.Generic;

namespace GuildMaster.Core
{
    /// <summary>
    /// На каком уровне лога пишется событие симуляции. По умолчанию — <see cref="SimLogLevel.Info"/>: все события
    /// видны в обычном логе. Частые служебные события времени, которые только отмечают ход часов, опущены ниже, чтобы
    /// лог на <see cref="SimLogLevel.Info"/> читался: начало суток — <see cref="SimLogLevel.Debug"/> (разделитель дней
    /// среди бросков), часы и фазы дня — <see cref="SimLogLevel.Trace"/>. Новое частое событие — одна строка в таблице.
    /// </summary>
    public static class EventLogLevels
    {
        private static readonly Dictionary<SimEventType, SimLogLevel> Levels = new Dictionary<SimEventType, SimLogLevel>
        {
            { SimEventType.HourStarted, SimLogLevel.Trace },
            { SimEventType.DayStarted, SimLogLevel.Debug },
            { SimEventType.MorningStarted, SimLogLevel.Trace },
            { SimEventType.DaytimeStarted, SimLogLevel.Trace },
            { SimEventType.EveningStarted, SimLogLevel.Trace },
            { SimEventType.NightStarted, SimLogLevel.Trace },
        };

        public static SimLogLevel Of(SimEventType type) => Levels.TryGetValue(type, out SimLogLevel level) ? level : SimLogLevel.Info;
    }
}
