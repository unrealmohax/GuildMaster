using System;
using System.Collections.Generic;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Изменения распоряжений: включить, сменить область и срок, выключить (игроком или по сроку), а также платежи гильдии
    /// по распоряжениям. Бросков не тратит.
    /// </summary>
    public static class DecreeService
    {
        /// <summary>
        /// Включить: срок — от текущего часа. Строка «Объявлено», у Сухого закона — своя; её участник — первый в гильдии,
        /// у кого черта Пьяницы уже раскрыта (скрытая черта строкой не выдаётся), если такой есть.
        /// </summary>
        internal static ActiveDecree Enable(SimContext ctx, DecreeDefinition decree, List<GuildRank> ranks, DecreeDuration duration)
        {
            var active = new ActiveDecree(decree.Id);
            SetTerm(ctx, active, ranks, duration);
            ctx.World.Decrees.Add(active, ctx.World.Time.TotalHours);
            ctx.Log.Write(SimLogLevel.Info, "decree {0} enabled: {1}", decree.Id, Describe(active));
            bool prohibition = decree.Effect == DecreeEffect.Prohibition;
            Adventurer witness = prohibition ? KnownDrunkard(ctx) : null;
            Publish(ctx, SimEventType.DecreeEnabled, prohibition ? EventImportance.Notable : EventImportance.Normal, decree,
                    witness != null ? new[] { witness.Id } : Array.Empty<int>())
                .With("ranks", RanksText(active))
                .With("duration", duration)
                .With("endsAt", active.EndsAtHours);
            return active;
        }

        /// <summary>Сменить область и срок включённого: срок — заново от текущего часа, без строки и без штрафа.</summary>
        internal static void Change(SimContext ctx, DecreeDefinition decree, ActiveDecree active, List<GuildRank> ranks, DecreeDuration duration)
        {
            SetTerm(ctx, active, ranks, duration);
            ctx.Log.Write(SimLogLevel.Info, "decree {0} changed: {1}", decree.Id, Describe(active));
            Publish(ctx, SimEventType.DecreeChanged, EventImportance.Normal, decree)
                .With("ranks", RanksText(active))
                .With("duration", duration)
                .With("endsAt", active.EndsAtHours);
        }

        /// <summary>
        /// Игрок выключил распоряжение. Льгота: довольство <c>benefitCancelContentment</c> у всех, кого она касалась
        /// (<see cref="DecreeRules.IsAffectedByBenefit"/>).
        /// </summary>
        internal static void Revoke(SimContext ctx, DecreeDefinition decree, ActiveDecree active)
        {
            ctx.World.Decrees.Remove(active, ctx.World.Time.TotalHours);
            int affected = 0;
            if (decree.IsBenefit)
            {
                float penalty = ctx.Data.Balance.Decrees.BenefitCancelContentment;
                foreach (Adventurer adventurer in ctx.World.Adventurers.Active)
                {
                    if (!DecreeRules.IsAffectedByBenefit(decree, adventurer, ctx.World, ctx.Data)) continue;
                    StateService.ChangeContentment(ctx, adventurer, penalty);
                    affected++;
                }
            }
            ctx.Log.Write(SimLogLevel.Info, "decree {0} revoked by player: benefit={1} affected={2}", decree.Id, decree.IsBenefit, affected);
            Publish(ctx, SimEventType.DecreeRevoked, decree.IsBenefit ? EventImportance.Notable : EventImportance.Normal, decree)
                .With("benefit", decree.IsBenefit)
                .With("affected", affected);
        }

        /// <summary>Срок вышел: распоряжение выключается само, без штрафа.</summary>
        internal static void Expire(SimContext ctx, DecreeDefinition decree, ActiveDecree active)
        {
            ctx.World.Decrees.Remove(active, ctx.World.Time.TotalHours);
            ctx.Log.Write(SimLogLevel.Info, "decree {0} expired", decree.Id);
            Publish(ctx, SimEventType.DecreeExpired, EventImportance.Normal, decree);
        }

        /// <summary>Гильдия платит суточные еду и жильё новичка: <c>newcomerDailyCost</c>, даже в минус.</summary>
        internal static void PayLiving(SimContext ctx, Adventurer adventurer)
        {
            DecreeDefinition decree = DecreeRules.Find(ctx.Data, DecreeEffect.FreeLodgingForNewcomers);
            TreasuryService.Debit(ctx, LedgerCategories.Decrees, WalletService.Coins(ctx.Data.Balance.Decrees.NewcomerDailyCost), decree.Id,
                adventurer.Id);
        }

        /// <summary>Компенсация за рану, если распоряжение включено: казна → кошелёк раненого.</summary>
        internal static void CompensateWound(SimContext ctx, Adventurer adventurer, ConditionKind kind, bool maimed)
        {
            if (!DecreeRules.TryGetOn(ctx.World, ctx.Data, DecreeEffect.InjuryCompensation, out ActiveDecree active)) return;

            int amount = DecreeRules.Compensation(kind, maimed, ctx.Data.Balance.Decrees);
            if (amount <= 0) return;
            TreasuryService.Debit(ctx, LedgerCategories.Decrees, amount, active.DecreeId, adventurer.Id);
            WalletService.ReceiveIncome(ctx, adventurer, amount, IncomeKind.Compensation);
            ctx.Events.Publish(SimEventType.InjuryCompensated, EventImportance.Normal, adventurer.Id)
                .With("decreeId", active.DecreeId)
                .With("amount", amount)
                .With("kind", kind)
                .With("maimed", maimed);
        }

        /// <summary>Область и срок от текущего часа.</summary>
        private static void SetTerm(SimContext ctx, ActiveDecree active, List<GuildRank> ranks, DecreeDuration duration)
        {
            long now = ctx.World.Time.TotalHours;
            int days = DecreeRules.Days(duration, ctx.Data.Balance.Decrees);
            active.SetRanks(ranks);
            active.Duration = duration;
            active.TermStartedAtHours = now;
            active.EndsAtHours = duration == DecreeDuration.Permanent ? 0 : now + ctx.Calendar.DaysToHours(days);
        }

        private static SimEvent Publish(SimContext ctx, SimEventType type, EventImportance importance, DecreeDefinition decree,
            params int[] participants) =>
            ctx.Events.Publish(type, importance, participants)
                .With("decree", decree.NameForms)
                .With("decreeId", decree.Id);

        private static Adventurer KnownDrunkard(SimContext ctx)
        {
            foreach (Adventurer adventurer in ctx.World.Adventurers.Active)
            {
                TraitInstance drunkard = TraitRules.FindWithHook(adventurer, TraitHook.DrunkardSkipsDay, ctx.Data);
                if (drunkard != null && drunkard.Revealed) return adventurer;
            }
            return null;
        }

        private static string Describe(ActiveDecree active) =>
            "ranks=" + RanksText(active) + " duration=" + active.Duration + " endsAt=" + active.EndsAtHours;

        private static string RanksText(ActiveDecree active)
        {
            if (active.Ranks.Count == 0) return "-";
            var parts = new string[active.Ranks.Count];
            for (int i = 0; i < parts.Length; i++) parts[i] = active.Ranks[i].ToString();
            return string.Join(",", parts);
        }
    }
}
