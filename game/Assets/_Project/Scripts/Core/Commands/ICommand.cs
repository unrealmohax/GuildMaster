namespace GuildMaster.Core
{
    /// <summary>
    /// Действие игрока. Интерфейс не меняет <see cref="WorldState"/> напрямую — только отправляет команду;
    /// команда применяется в начале следующего такта, а на паузе — сразу (<see cref="Simulation.ApplyCommandsNow"/>).
    /// </summary>
    public interface ICommand
    {
        /// <summary>Применить к миру. Случайность — только из <see cref="SimContext.Rng"/> (поток команд).</summary>
        void Apply(SimContext ctx);
    }
}
