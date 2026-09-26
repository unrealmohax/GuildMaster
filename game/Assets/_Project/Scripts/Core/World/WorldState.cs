namespace GuildMaster.Core
{
    /// <summary>
    /// Всё текущее состояние мира. Снаружи Core — только чтение: сеттеры internal, менять мир можно
    /// только системами и командами.
    /// Казна, репутация, персонал, постройки, заказы, задания, группы, распоряжения, обращения
    /// и ленты добавляются в своих ТЗ (05–14) по тому же правилу.
    /// </summary>
    public sealed class WorldState
    {
        public GameTime Time { get; internal set; }

        public IdGenerator Ids { get; } = new IdGenerator();

        /// <summary>Переключатели автопаузы и её последняя причина (ТЗ 03).</summary>
        public AutopauseState Autopause { get; } = new AutopauseState();

        /// <summary>Авантюристы: активные, архив, кандидаты (ТЗ 04).</summary>
        public AdventurerRoster Adventurers { get; } = new AdventurerRoster();

        /// <summary>Отношения между людьми (ТЗ 04).</summary>
        public RelationBook Relations { get; } = new RelationBook();
    }
}
