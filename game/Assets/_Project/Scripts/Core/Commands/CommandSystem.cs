namespace GuildMaster.Core
{
    /// <summary>Шаг 1 такта: применить команды игрока из очереди в порядке отправки.</summary>
    public sealed class CommandSystem : ISimSystem
    {
        public const string SystemName = nameof(CommandSystem);

        public string Name => SystemName;

        public void Tick(SimContext ctx) => ApplyPending(ctx);

        internal static void ApplyPending(SimContext ctx)
        {
            foreach (ICommand command in ctx.Commands.TakeAll())
            {
                command.Apply(ctx);
            }
        }
    }
}
