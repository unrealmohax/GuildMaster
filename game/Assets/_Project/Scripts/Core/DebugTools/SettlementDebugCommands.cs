namespace GuildMaster.Core
{
    /// <summary>Безопасность поселения ± (обрезается пределами; уровень и отсчёт покидания пересчитываются).</summary>
    public sealed class DebugSecurityCommand : ICommand
    {
        public DebugSecurityCommand(float delta)
        {
            Delta = delta;
        }

        public float Delta { get; }

        public void Apply(SimContext ctx)
        {
            if (SettlementService.ChangeSecurity(ctx, Delta, SettlementReasons.Debug) == 0f) return;
            DebugAction.Publish(ctx, "security").With("value", ctx.World.Settlement.Security);
        }
    }

    /// <summary>Нападение на поселение сейчас, при любом уровне: жители, стресс людей в гильдии, ущерб казне.</summary>
    public sealed class DebugAttackCommand : ICommand
    {
        public void Apply(SimContext ctx)
        {
            SettlementService.Attack(ctx);
            DebugAction.Publish(ctx, "attack");
        }
    }
}
