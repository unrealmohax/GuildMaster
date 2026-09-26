namespace GuildMaster.Core
{
    /// <summary>
    /// Всё текущее состояние мира. Снаружи Core — только чтение: сеттеры internal, менять мир можно
    /// только системами и командами.
    /// Казна, репутация, авантюристы, персонал, постройки, заказы, задания, группы, распоряжения, обращения
    /// и ленты добавляются в своих ТЗ (04–14) по тому же правилу.
    /// </summary>
    public sealed class WorldState
    {
        public GameTime Time { get; internal set; }

        public IdGenerator Ids { get; } = new IdGenerator();
    }
}
