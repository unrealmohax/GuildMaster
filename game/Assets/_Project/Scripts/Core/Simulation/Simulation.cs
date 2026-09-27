using System;
using System.Collections.Generic;

namespace GuildMaster.Core
{
    /// <summary>
    /// Точка входа симуляции. <see cref="Tick"/> — один игровой час: системы по порядку, затем очистка событий.
    /// Работает без сцены и без интерфейса. То же зерно + те же команды в те же такты = тот же мир.
    /// При создании готовит стартовое состояние (казна; <see cref="StartScenario"/>: стартовые авантюристы).
    /// Когда гильдия закрыта (<see cref="IsFinished"/>), такты больше ничего не делают.
    /// Лог (<see cref="SimLogger"/>) получает события сразу после шага системы, которая их опубликовала, — вслед за её
    /// бросками; уровень события — <see cref="EventLogLevels"/>.
    /// </summary>
    public sealed class Simulation : ISimulationClient
    {
        private readonly List<ISimSystem> systems;
        private readonly SimContext context;
        private bool isRunning;
        private int loggedEvents;

        /// <param name="log">Лог симуляции; <c>null</c> — без лога. Логгер привязывается к этой симуляции.</param>
        public Simulation(DataRegistry data, uint masterSeed, IEnumerable<ISimSystem> systems, SimLogger log = null)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            if (systems == null) throw new ArgumentNullException(nameof(systems));

            this.systems = new List<ISimSystem>(systems);
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (ISimSystem system in this.systems)
            {
                if (system == null) throw new ArgumentException("System list contains null", nameof(systems));
                if (string.IsNullOrEmpty(system.Name)) throw new ArgumentException($"{system.GetType().Name} has no name", nameof(systems));
                if (!names.Add(system.Name)) throw new ArgumentException($"Duplicate system name '{system.Name}'", nameof(systems));
            }

            Calendar = new Calendar(data.Balance.Time);
            Rhythm = new DayRhythm(Calendar, data.Balance.Time);
            Rng = new RngService(masterSeed);
            World = new WorldState { Time = Calendar.At(Calendar.StartTotalHours) };
            World.Treasury.Initialize(data.Balance.Guild.StartMoney, data.Balance.Economy.DefaultCommission, World.Time.TotalHours);
            Events = new EventBus(World);
            Commands = new CommandQueue();
            Log = log ?? SimLogger.Disabled;
            Log.Bind(Calendar, World);
            context = new SimContext(World, Data, Calendar, Rhythm, Events, Commands, Log);

            // Стартовое состояние мира — до первого такта, своим потоком случайных чисел.
            Run(StartScenario.StreamName, StartScenario.Apply);
        }

        public static Simulation CreateDefault(DataRegistry data, uint masterSeed, SimLogger log = null) =>
            new Simulation(data, masterSeed, SimulationSystems.CreateDefault(), log);

        public WorldState World { get; }
        public DataRegistry Data { get; }
        public Calendar Calendar { get; }
        public DayRhythm Rhythm { get; }
        public RngService Rng { get; }
        public EventBus Events { get; }
        public CommandQueue Commands { get; }
        public SimLogger Log { get; }
        public IReadOnlyList<ISimSystem> Systems => systems;
        public uint MasterSeed => Rng.MasterSeed;

        /// <summary>Игра окончена — гильдия закрыта: такты и команды больше ничего не делают.</summary>
        public bool IsFinished => World.Treasury.IsClosed;

        /// <summary>Сколько тактов прошло с начала игры.</summary>
        public long TicksDone { get; private set; }

        /// <summary>В последнем такте система попросила паузу (автопауза). Сбрасывается <see cref="ConsumePauseRequest"/>.</summary>
        public bool PauseRequested => context.PauseRequested;

        /// <summary>
        /// События завершённого такта — перед очисткой шины. Для лога и прогона без интерфейса.
        /// </summary>
        public event Action<IReadOnlyList<SimEvent>> TickCompleted;

        public event Action StateChanged;

        public void Send(ICommand command) => Commands.Enqueue(command);

        public void Tick()
        {
            if (IsFinished) return;

            BeginRun();
            try
            {
                foreach (ISimSystem system in systems)
                {
                    Run(system.Name, system.Tick);
                }
                TicksDone++;
                TickCompleted?.Invoke(Events.Events);
                Events.Clear();
                loggedEvents = 0;
            }
            finally
            {
                isRunning = false;
            }
            StateChanged?.Invoke();
        }

        /// <summary>
        /// На паузе: применить команды сразу, не дожидаясь такта. Тот же поток случайных чисел, что у CommandSystem,
        /// поэтому результат совпадает с применением в начале следующего такта. События команд уходят в лог
        /// вместе со следующим тактом.
        /// </summary>
        public void ApplyCommandsNow()
        {
            if (Commands.Count == 0 || IsFinished) return;

            BeginRun();
            try
            {
                Run(CommandSystem.SystemName, CommandSystem.ApplyPending);
            }
            finally
            {
                isRunning = false;
            }
            StateChanged?.Invoke();
        }

        public bool ConsumePauseRequest()
        {
            bool requested = context.PauseRequested;
            context.PauseRequested = false;
            return requested;
        }

        private void BeginRun()
        {
            if (isRunning) throw new InvalidOperationException("Simulation is already running a tick");
            isRunning = true;
        }

        private void Run(string systemName, Action<SimContext> step)
        {
            context.CurrentSystem = systemName;
            context.Rng = Rng.Stream(systemName);
            Events.CurrentSource = systemName;
            Log.Source = systemName;
            step(context);
            LogNewEvents();
        }

        /// <summary>В лог — события, опубликованные с прошлой записи (их данные уже дописаны).</summary>
        private void LogNewEvents()
        {
            IReadOnlyList<SimEvent> events = Events.Events;
            for (; loggedEvents < events.Count; loggedEvents++)
            {
                SimEvent simEvent = events[loggedEvents];
                SimLogLevel level = EventLogLevels.Of(simEvent.Type);
                if (Log.IsOn(level)) Log.WriteEvent(level, simEvent);
            }
        }
    }
}
