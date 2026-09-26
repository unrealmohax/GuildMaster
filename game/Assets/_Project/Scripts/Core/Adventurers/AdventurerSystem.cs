using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Шаг 13 такта, перед <see cref="RecruitSystem"/> (решение 2026-09-26). Раз в сутки, в 00:00: пересчёт архетипа
    /// у всех в гильдии и раскрытие нейтральных осей через <c>neutralRevealDays</c> в гильдии («уравновешен»).
    /// Случайных чисел не тратит.
    /// </summary>
    public sealed class AdventurerSystem : ISimSystem
    {
        public string Name => nameof(AdventurerSystem);

        public void Tick(SimContext ctx)
        {
            if (ctx.World.Time.Hour != 0) return;

            long now = ctx.World.Time.TotalHours;
            long neutralRevealHours = ctx.Calendar.DaysToHours(ctx.Data.Balance.Adventurers.NeutralRevealDays);

            foreach (Adventurer adventurer in ctx.World.Adventurers.Active)
            {
                ArchetypeService.Recalculate(ctx, adventurer);

                if (now - adventurer.JoinedAtHours < neutralRevealHours) continue;
                for (int axis = 0; axis < Vocabulary.AxisCount; axis++)
                {
                    RevealService.TryRevealBalanced(ctx, adventurer, (AxisId)axis);
                }
            }
        }
    }
}
