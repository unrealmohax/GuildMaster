using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Раны. Ранить — <see cref="Wound"/> (задания, драка при срыве), увечье — <see cref="Maim"/>.
    /// Срок раны — поток вызывающей системы. Лечение и осложнения — <see cref="HealthSystem"/>.
    /// </summary>
    public static class HealthService
    {
        /// <summary>
        /// Ранить: своя рана — стресс +10 (лёгкая) / +20 (тяжёлая) × черты. Несколько лёгких ран не складываются —
        /// остаётся самая длинная. Вторая тяжёлая при уже тяжёлой становится увечьем (черта «Калека», если её можно дать).
        /// Событие: лёгкая — [З], тяжёлая — [В]. Возвращает рану, которая теперь у человека.
        /// </summary>
        public static Condition Wound(SimContext ctx, Adventurer adventurer, ConditionKind kind, PartyContext party = PartyContext.None)
        {
            HealthBalance health = ctx.Data.Balance.Health;
            StateBalance state = ctx.Data.Balance.State;
            AdventurerState adventurerState = adventurer.State;
            long now = ctx.World.Time.TotalHours;

            IntRange range = kind == ConditionKind.LightWound ? health.LightWoundDays : health.HeavyWoundDays;
            int days = ctx.Rng.RangeInclusive(range.Min, range.Max);
            bool maimed = false;

            Condition condition;
            if (adventurerState.TryGetCondition(kind, out Condition existing))
            {
                condition = existing;
                if (kind == ConditionKind.LightWound)
                {
                    if (days > existing.RemainingDays)
                    {
                        existing.Days = days;
                        existing.RemainingDays = days;
                    }
                }
                else
                {
                    maimed = Maim(ctx, adventurer);
                }
            }
            else
            {
                condition = new Condition(kind, days, now);
                adventurerState.AddCondition(condition);
            }

            StateService.AddStress(ctx, adventurer, kind == ConditionKind.LightWound ? state.OwnLightWoundStress : state.OwnHeavyWoundStress, party);

            SimEvent wounded = ctx.Events.Publish(SimEventType.AdventurerWounded,
                    kind == ConditionKind.LightWound ? EventImportance.Notable : EventImportance.Important, adventurer.Id)
                .With("kind", kind)
                .With("days", days);
            if (maimed) wounded.With("maimed", true);
            return condition;
        }

        /// <summary>Увечье: черта «Калека» (одна характеристика × 0,7 навсегда; какая — поток вызывающего). false — дать нельзя.</summary>
        public static bool Maim(SimContext ctx, Adventurer adventurer)
        {
            SpecialTraitDefinition maimed = FindMaimedTrait(ctx.Data);
            return maimed != null && TraitService.TryAcquire(ctx, adventurer, maimed.Id);
        }

        /// <summary>Черта увечья — постоянная (<see cref="TraitRules.IsPermanent"/>: «Калека»).</summary>
        public static SpecialTraitDefinition FindMaimedTrait(DataRegistry data)
        {
            foreach (SpecialTraitDefinition trait in data.All<SpecialTraitDefinition>())
            {
                if (TraitRules.IsPermanent(trait)) return trait;
            }
            return null;
        }
    }
}
