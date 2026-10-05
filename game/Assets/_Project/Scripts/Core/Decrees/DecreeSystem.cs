using System.Collections.Generic;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Распоряжения в такте: каждый час — снятие тех, чей срок вышел (без штрафа). Эффекты распоряжений считают сами системы,
    /// которых они касаются (<see cref="DecreeRules"/>). Бросков не тратит. Включает и выключает их игрок — <see cref="ToggleDecreeCommand"/>.
    /// </summary>
    public sealed class DecreeSystem : ISimSystem
    {
        public string Name => nameof(DecreeSystem);

        public void Tick(SimContext ctx)
        {
            DecreeBook book = ctx.World.Decrees;
            if (book.Active.Count == 0) return;

            long now = ctx.World.Time.TotalHours;
            foreach (ActiveDecree active in new List<ActiveDecree>(book.Active))
            {
                if (active.IsPermanent || now < active.EndsAtHours) continue;
                DecreeService.Expire(ctx, ctx.Data.Get<DecreeDefinition>(active.DecreeId), active);
            }
        }
    }
}
