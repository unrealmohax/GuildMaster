namespace GuildMaster.Core
{
    /// <summary>Включить или выключить вид автопаузы (настройки на экране «Время», ТЗ 15).</summary>
    public sealed class SetAutopauseCommand : ICommand
    {
        public SetAutopauseCommand(AutopauseKind kind, bool enabled)
        {
            Kind = kind;
            Enabled = enabled;
        }

        public AutopauseKind Kind { get; }
        public bool Enabled { get; }

        public void Apply(SimContext ctx)
        {
            AutopauseState autopause = ctx.World.Autopause;
            if (autopause.IsEnabled(Kind) == Enabled) return;

            autopause.SetEnabled(Kind, Enabled);
            ctx.Events.Publish(SimEventType.AutopauseChanged)
                .With("kind", Kind)
                .With("enabled", Enabled);
        }
    }
}
