using System;
using System.Collections.Generic;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Трудные времена — гильдия в банкротстве или кто-то ушёл из гильдии за прошедший месяц (включая проверку ухода
    /// в начале этого месяца). Проверяются в начале месяца; в трудные времена:
    /// <list type="bullet">
    /// <item>у каждого, кто в гильдии не меньше <c>testedMinDays</c> суток и с лояльностью не ниже <c>testedMinLoyalty</c>,
    /// появляется черта «Проверенный» (её эффект при появлении — <see cref="TraitService"/>);</item>
    /// <item>кто остался при лояльности ниже <c>loyalStayLoyaltyBelow</c>, раскрывается как Преданный, если он Преданный.</item>
    /// </list>
    /// </summary>
    public static class HardTimes
    {
        /// <summary>
        /// Трудные ли сейчас времена. <paramref name="leftCount"/> — сколько ушли из гильдии за месяц до этого часа
        /// (не раньше начала игры).
        /// </summary>
        public static bool IsNow(WorldState world, Calendar calendar, out int leftCount)
        {
            long now = world.Time.TotalHours;
            long from = Math.Max(calendar.StartTotalHours, now - calendar.HoursPerMonth);
            leftCount = 0;
            foreach (Adventurer adventurer in world.Adventurers.Archive)
            {
                if (adventurer.LeaveReason == LeaveReason.Left && adventurer.LeftAtHours > from && adventurer.LeftAtHours <= now) leftCount++;
            }
            return world.Treasury.Bankruptcy.Active || leftCount > 0;
        }

        internal static void Apply(SimContext ctx)
        {
            if (!IsNow(ctx.World, ctx.Calendar, out int leftCount)) return;

            ctx.Log.Write(SimLogLevel.Info, "hard times: bankruptcy={0} left={1}", ctx.World.Treasury.Bankruptcy.Active, leftCount);

            TraitsBalance traits = ctx.Data.Balance.Traits;
            float loyalBelow = ctx.Data.Balance.Adventurers.LoyalStayLoyaltyBelow;
            long now = ctx.World.Time.TotalHours;
            long testedHours = ctx.Calendar.DaysToHours(traits.TestedMinDays);
            SpecialTraitDefinition tested = ctx.Data.HasDefinitions ? TraitRules.FindByHook(ctx.Data, TraitHook.TestedOnAcquire) : null;

            foreach (Adventurer adventurer in new List<Adventurer>(ctx.World.Adventurers.Active))
            {
                AdventurerState state = adventurer.State;
                if (tested != null && now - adventurer.JoinedAtHours >= testedHours && state.Loyalty >= traits.TestedMinLoyalty)
                    TraitService.TryAcquire(ctx, adventurer, tested.Id);
                if (state.Loyalty < loyalBelow)
                    RevealService.TryRevealAxis(ctx, adventurer, AxisId.Loyalty, RevealTrigger.LoyalStayedInHardTimes);
            }
        }
    }
}
