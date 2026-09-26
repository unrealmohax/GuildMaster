using System.Collections.Generic;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Шаг 6 такта: показатели состояния.
    /// <list type="bullet">
    /// <item>Каждый час — усталость и стресс по занятию × эффекты черт.</item>
    /// <item>Раз в сутки (00:00), по каждому в гильдии: расходы на жизнь за прошедшие сутки (еда, жильё); довольство — к цели
    /// на 1; лояльность — к довольству на 0,1 (ось «Верность»); учёт суток сбрасывается; срыв при стрессе выше 80
    /// (шанс 10%, вид — по чертам); конец спада «Потерявшего товарища».</item>
    /// <item>В начале месяца — проверка ухода: лояльность ниже 25 — шанс 20% (Семейный × 1,5). Уход — [В], автопауза.</item>
    /// </list>
    /// Люди на задании в суточных срывах и проверке ухода не участвуют.
    /// </summary>
    public sealed class StateSystem : ISimSystem
    {
        public string Name => nameof(StateSystem);

        public void Tick(SimContext ctx)
        {
            IReadOnlyList<Adventurer> active = ctx.World.Adventurers.Active;
            foreach (Adventurer adventurer in active)
            {
                StateService.ApplyHour(ctx, adventurer, PartyContext.None); // вне задания
            }

            GameTime time = ctx.World.Time;
            if (time.Hour != 0) return;

            // Казны пока нет: комиссия гильдии — по умолчанию.
            float commission = ctx.Data.Balance.Economy.DefaultCommission;
            foreach (Adventurer adventurer in new List<Adventurer>(active))
            {
                WalletService.PayDaily(ctx, adventurer);
                StateService.MoveContentment(ctx, adventurer, StateRules.ContentmentTarget(adventurer, commission, ctx.Data));
                StateService.MoveLoyalty(ctx, adventurer);
                ResetDay(adventurer.State);
                TryBreakdown(ctx, adventurer);
                TryEndGrieving(ctx, adventurer);
            }

            if (time.Day == 1) CheckLeaving(ctx);
        }

        private static void ResetDay(AdventurerState state)
        {
            state.AteInTavernToday = false;
            state.DrankToday = false;
            state.PaidInfirmaryToday = false;
        }

        private static void TryBreakdown(SimContext ctx, Adventurer adventurer)
        {
            AdventurerState state = adventurer.State;
            StateBalance balance = ctx.Data.Balance.State;
            if (state.Stress <= balance.BreakdownThreshold || state.Breakdown != BreakdownKind.None || state.IsOnQuest()) return;
            if (!ctx.Rng.Chance(balance.BreakdownChancePerDay)) return;

            StateService.StartBreakdown(ctx, adventurer, StateService.BreakdownKindOf(adventurer, ctx.Data));
        }

        /// <summary>
        /// Потерявший товарища: через <c>grievingDays</c> — раскрытие («после спада»), затем «сломался» (стресс +30) или
        /// «ожесточился» (Хладнокровие +5), и черта снимается.
        /// </summary>
        private static void TryEndGrieving(SimContext ctx, Adventurer adventurer)
        {
            TraitInstance grieving = TraitRules.FindWithHook(adventurer, TraitHook.GrievingOutcome, ctx.Data);
            TraitsBalance traits = ctx.Data.Balance.Traits;
            if (grieving == null || ctx.World.Time.TotalHours - grieving.AcquiredAtHours < ctx.Calendar.DaysToHours(traits.GrievingDays)) return;

            RevealService.TryRevealTrait(ctx, adventurer, grieving.TraitId, RevealTrigger.GrievingAfterDecline);
            bool broke = ctx.Rng.Chance(traits.GrievingBreakChance);
            if (broke) StateService.AddStress(ctx, adventurer, traits.GrievingBreakStress);
            else Growth.AddBonus(ctx, adventurer, StatId.Composure, traits.GrievingHardenComposure);

            SimEvent ended = grieving.PartnerId != 0
                ? ctx.Events.Publish(SimEventType.GrievingEnded, EventImportance.Notable, adventurer.Id, grieving.PartnerId)
                : ctx.Events.Publish(SimEventType.GrievingEnded, EventImportance.Notable, adventurer.Id);
            ended.With("outcome", broke ? "broke" : "hardened");
            TraitService.TryRemove(ctx, adventurer, grieving.TraitId);
        }

        /// <summary>Ежемесячная проверка ухода. Причина — «низкая лояльность».</summary>
        private static void CheckLeaving(SimContext ctx)
        {
            StateBalance balance = ctx.Data.Balance.State;
            foreach (Adventurer adventurer in new List<Adventurer>(ctx.World.Adventurers.Active))
            {
                if (adventurer.State.Loyalty >= balance.LeaveCheckThreshold || adventurer.State.IsOnQuest()) continue;

                float chance = balance.LeaveChance;
                if (TraitRules.FindWithHook(adventurer, TraitHook.FamilyLeaveChance, ctx.Data) != null)
                    chance *= ctx.Data.Balance.Traits.FamilyLeaveChanceMultiplier;
                if (!ctx.Rng.Chance(chance)) continue;

                float loyalty = adventurer.State.Loyalty;
                AdventurerLifecycle.Retire(ctx, adventurer, LeaveReason.Left);
                ctx.Events.Publish(SimEventType.AdventurerLeft, EventImportance.Important, adventurer.Id)
                    .With("cause", LeaveCause.LowLoyalty)
                    .With("loyalty", loyalty);
            }
        }
    }

    /// <summary>Почему человек ушёл сам.</summary>
    public enum LeaveCause
    {
        LowLoyalty,
    }
}
