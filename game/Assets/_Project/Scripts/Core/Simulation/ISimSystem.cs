namespace GuildMaster.Core
{
    /// <summary>Система симуляции: один шаг такта.</summary>
    public interface ISimSystem
    {
        /// <summary>Уникальное имя: по нему — поток случайных чисел и подпись в логе. Не менять без причины.</summary>
        string Name { get; }

        void Tick(SimContext ctx);
    }
}
