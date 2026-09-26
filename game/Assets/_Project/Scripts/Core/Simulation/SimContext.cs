namespace GuildMaster.Core
{
    /// <summary>То, с чем работает система в такте.</summary>
    public sealed class SimContext
    {
        internal SimContext(WorldState world, DataRegistry data, Calendar calendar, DayRhythm rhythm, EventBus events, CommandQueue commands)
        {
            World = world;
            Data = data;
            Calendar = calendar;
            Rhythm = rhythm;
            Events = events;
            Commands = commands;
        }

        public WorldState World { get; }
        public DataRegistry Data { get; }
        public Calendar Calendar { get; }

        /// <summary>Фазы дня, ночлег в пути, время выхода на задание (ТЗ 03).</summary>
        public DayRhythm Rhythm { get; }

        public EventBus Events { get; }

        /// <summary>Поток случайных чисел текущей системы.</summary>
        public Rng Rng { get; internal set; }

        /// <summary>Имя текущей системы.</summary>
        public string CurrentSystem { get; internal set; }

        internal CommandQueue Commands { get; }

        internal bool PauseRequested { get; set; }

        /// <summary>Поставить игру на паузу после такта (AutopauseSystem, ТЗ 03).</summary>
        public void RequestPause() => PauseRequested = true;
    }
}
