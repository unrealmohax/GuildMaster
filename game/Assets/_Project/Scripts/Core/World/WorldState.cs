namespace GuildMaster.Core
{
    /// <summary>
    /// Всё текущее состояние мира. Снаружи Core — только чтение: сеттеры internal, менять мир можно
    /// только системами и командами.
    /// Обращения добавляются по тому же правилу.
    /// </summary>
    public sealed class WorldState
    {
        public GameTime Time { get; internal set; }

        public IdGenerator Ids { get; } = new IdGenerator();

        /// <summary>Переключатели автопаузы и её последняя причина.</summary>
        public AutopauseState Autopause { get; } = new AutopauseState();

        /// <summary>Авантюристы: активные, архив, кандидаты.</summary>
        public AdventurerRoster Adventurers { get; } = new AdventurerRoster();

        /// <summary>Отношения между людьми.</summary>
        public RelationBook Relations { get; } = new RelationBook();

        /// <summary>Ленты событий: лента гильдии.</summary>
        public FeedState Feed { get; } = new FeedState();

        /// <summary>Казна гильдии: деньги, журнал, комиссия, банкротство.</summary>
        public Treasury Treasury { get; } = new Treasury();

        /// <summary>Отчёты месяца.</summary>
        public MonthReportHistory Reports { get; } = new MonthReportHistory();

        /// <summary>Гильдия как целое: репутация.</summary>
        public GuildState Guild { get; } = new GuildState();

        /// <summary>Заказы: доска, ждущие игрока, в работе, архив, правила Регистратора.</summary>
        public OrderBoard Orders { get; } = new OrderBoard();

        /// <summary>Задания: идущие, недавно законченные (с лентой задания), люди, идущие домой одни.</summary>
        public QuestBook Quests { get; } = new QuestBook();

        /// <summary>Группы: под задание и постоянные.</summary>
        public PartyBook Parties { get; } = new PartyBook();

        /// <summary>Постройки гильдии: готовые, стройка, очередь.</summary>
        public BuildingBook Buildings { get; } = new BuildingBook();

        /// <summary>Персонал: сотрудники, бывшие сотрудники, кандидаты на вакансии.</summary>
        public StaffRoster Staff { get; } = new StaffRoster();

        /// <summary>Распоряжения: включённые, их область и срок.</summary>
        public DecreeBook Decrees { get; } = new DecreeBook();

        /// <summary>Обращения: открытые, закрытые, перезарядки, отложенный праздник.</summary>
        public DilemmaBook Dilemmas { get; } = new DilemmaBook();
    }
}
