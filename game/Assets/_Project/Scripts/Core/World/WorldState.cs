namespace GuildMaster.Core
{
    /// <summary>
    /// Всё текущее состояние мира. Снаружи Core — только чтение: сеттеры internal, менять мир можно
    /// только системами и командами.
    /// Казна, репутация, персонал, постройки, заказы, задания, группы, распоряжения, обращения
    /// и ленты добавляются по тому же правилу.
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
    }
}
