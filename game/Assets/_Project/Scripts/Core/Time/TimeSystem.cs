using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Шаг 2 такта: сдвигает время на час и объявляет начало часа, суток, месяца и фаз дня.
    /// Сутки (<see cref="SimEventType.DayStarted"/>) начинаются в 00:00, дневная фаза
    /// (<see cref="SimEventType.DaytimeStarted"/>) — в час <see cref="TimeBalance.DayHour"/>.
    /// </summary>
    public sealed class TimeSystem : ISimSystem
    {
        public string Name => nameof(TimeSystem);

        public void Tick(SimContext ctx)
        {
            GameTime time = ctx.Calendar.At(ctx.World.Time.TotalHours + 1);
            ctx.World.Time = time;

            TimeBalance settings = ctx.Data.Balance.Time;
            EventBus events = ctx.Events;

            events.Publish(SimEventType.HourStarted);
            if (time.Hour == 0)
            {
                events.Publish(SimEventType.DayStarted);
                if (time.Day == 1) events.Publish(SimEventType.MonthStarted);
            }
            if (time.Hour == settings.MorningHour) events.Publish(SimEventType.MorningStarted);
            if (time.Hour == settings.DayHour) events.Publish(SimEventType.DaytimeStarted);
            if (time.Hour == settings.EveningHour) events.Publish(SimEventType.EveningStarted);
            if (time.Hour == settings.NightHour) events.Publish(SimEventType.NightStarted);
        }
    }
}
