namespace GuildMaster.Core
{
    /// <summary>То, с чем работает система в такте.</summary>
    public sealed class SimContext
    {
        internal SimContext(WorldState world, DataRegistry data, Calendar calendar, DayRhythm rhythm, EventBus events, CommandQueue commands,
            SimLogger log, RngService streams)
        {
            Streams = streams;
            World = world;
            Data = data;
            Calendar = calendar;
            Rhythm = rhythm;
            Events = events;
            Commands = commands;
            Log = log;
        }

        public WorldState World { get; }
        public DataRegistry Data { get; }
        public Calendar Calendar { get; }

        /// <summary>Фазы дня, ночлег в пути, время выхода на задание.</summary>
        public DayRhythm Rhythm { get; }

        public EventBus Events { get; }

        /// <summary>Лог симуляции; строки подписаны текущей системой. Без лога — <see cref="SimLogger.Disabled"/>.</summary>
        public SimLogger Log { get; }

        /// <summary>Поток случайных чисел текущей системы.</summary>
        public Rng Rng { get; internal set; }

        /// <summary>Имя текущей системы.</summary>
        public string CurrentSystem { get; internal set; }

        internal CommandQueue Commands { get; }

        /// <summary>
        /// Все потоки случайных чисел: для бросков, которые не должны сдвигать поток текущей системы (например, название
        /// постоянной группы — свой поток, кто бы её ни создал).
        /// </summary>
        internal RngService Streams { get; }

        internal bool PauseRequested { get; set; }

        /// <summary>Поставить игру на паузу после такта (AutopauseSystem).</summary>
        public void RequestPause() => PauseRequested = true;

        /// <summary>
        /// Бросок шанса потоком текущей системы — то же, что <see cref="GuildMaster.Core.Rng.Chance"/> (одно число), и строка в лог
        /// (<see cref="SimLogLevel.Debug"/>): что бросали, за кого, показатель, от которого зависел шанс, шанс, выпавшее число, итог.
        /// </summary>
        /// <param name="roll">Что бросали: «breakdown», «leave»…</param>
        /// <param name="subject">Чей бросок; <c>null</c> — ничей (приход кандидата).</param>
        /// <param name="factor">Имя показателя для лога (<c>stress</c>); <c>null</c> — без него.</param>
        public bool RollChance(float probability, string roll, Adventurer subject = null, string factor = null, float factorValue = 0f)
        {
            float rolled = Rng.NextFloat();
            bool success = rolled < probability;
            if (Log.IsOn(SimLogLevel.Debug)) AdventurerLog.WriteRoll(Log, roll, subject, probability, rolled, success, factor, factorValue);
            return success;
        }
    }
}
