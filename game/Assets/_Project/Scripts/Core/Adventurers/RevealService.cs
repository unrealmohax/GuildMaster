using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Раскрытие скрытых черт. Системы вызывают
    /// <see cref="TryRevealAxis"/> и <see cref="TryRevealTrait"/> в своих местах со своим триггером; раскрывается,
    /// только если триггер совпадает с триггером полюса или черты из данных. Раскрытие — важное событие с автопаузой
    /// (<see cref="AutopauseKind.TraitRevealed"/>). Нейтральная ось раскрывается сама — <see cref="AdventurerSystem"/>.
    /// </summary>
    public static class RevealService
    {
        /// <summary>
        /// Полюс оси впервые реально повлиял на исход. Раскрывает, если ось ещё скрыта, не нейтральна (|значение| ≥ 30)
        /// и <paramref name="trigger"/> — триггер полюса, к которому склоняется человек.
        /// </summary>
        public static bool TryRevealAxis(SimContext ctx, Adventurer adventurer, AxisId axis, RevealTrigger trigger)
        {
            if (adventurer.IsAxisRevealed(axis)) return false;

            float value = adventurer.GetAxis(axis);
            AdventurersBalance balance = ctx.Data.Balance.Adventurers;
            if (AxisMath.IsNeutral(value, balance)) return false;

            AxisPole pole = AxisMath.PoleOf(value);
            AxisPoleDefinition poleDefinition = ctx.Data.Axis(axis).Pole(pole);
            if (trigger == RevealTrigger.None || poleDefinition.RevealTrigger != trigger) return false;

            adventurer.SetAxisRevealed(axis);
            ctx.Events.Publish(SimEventType.AxisRevealed, EventImportance.Important, adventurer.Id)
                .With("axis", axis)
                .With("pole", pole)
                .With("extreme", AxisMath.IsExtreme(value, balance))
                .With("feedKey", poleDefinition.RevealFeedKey);
            return true;
        }

        /// <summary>Черта проявилась. Раскрывает, если черта есть, скрыта и <paramref name="trigger"/> — её триггер из данных.</summary>
        public static bool TryRevealTrait(SimContext ctx, Adventurer adventurer, string traitId, RevealTrigger trigger)
        {
            if (!adventurer.TryGetTrait(traitId, out TraitInstance instance) || instance.Revealed) return false;

            SpecialTraitDefinition trait = ctx.Data.Get<SpecialTraitDefinition>(traitId);
            if (trigger == RevealTrigger.None || trait.RevealTrigger != trigger) return false;

            Reveal(ctx, adventurer, instance, trait);
            return true;
        }

        /// <summary>Нейтральная ось раскрывается как «уравновешен»: заметное событие без автопаузы.</summary>
        internal static bool TryRevealBalanced(SimContext ctx, Adventurer adventurer, AxisId axis)
        {
            if (adventurer.IsAxisRevealed(axis) || !AxisMath.IsNeutral(adventurer.GetAxis(axis), ctx.Data.Balance.Adventurers)) return false;

            adventurer.SetAxisRevealed(axis);
            ctx.Events.Publish(SimEventType.AxisBalanced, EventImportance.Notable, adventurer.Id)
                .With("axis", axis);
            return true;
        }

        internal static void Reveal(SimContext ctx, Adventurer adventurer, TraitInstance instance, SpecialTraitDefinition trait)
        {
            instance.Revealed = true;
            SimEvent revealed = instance.PartnerId != 0
                ? ctx.Events.Publish(SimEventType.TraitRevealed, EventImportance.Important, adventurer.Id, instance.PartnerId)
                : ctx.Events.Publish(SimEventType.TraitRevealed, EventImportance.Important, adventurer.Id);
            revealed.With("trait", trait.Id).With("feedKey", trait.RevealFeedKey);
            if (instance.AffectedStat.HasValue) revealed.With("stat", instance.AffectedStat.Value);
        }
    }
}
