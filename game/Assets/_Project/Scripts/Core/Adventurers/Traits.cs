using System;
using System.Collections.Generic;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Правила сочетания особых черт (ТЗ 04 → «Особые черты: правила»): 0–<c>maxSpecialTraits</c> черт, не больше одной
    /// из категории (строго, решение 2026-09-26), без несовместимых пар. Чистые проверки, мир не меняют.
    /// </summary>
    public static class TraitRules
    {
        /// <summary>
        /// Постоянная черта (Калека): приобретённая, снижает параметр навсегда (эффект <c>ProfileMultiplier</c>).
        /// Не вытесняется другими приобретёнными, сама их не вытесняет, правило категорий на неё не действует (решение 2026-09-26).
        /// </summary>
        public static bool IsPermanent(SpecialTraitDefinition trait)
        {
            if (!trait.IsAcquired) return false;
            foreach (TraitEffect effect in trait.Effects)
            {
                if (effect.Kind == EffectKind.ProfileMultiplier) return true;
            }
            return false;
        }

        /// <summary>Приобретённая черта, которую вытеснит новая <paramref name="trait"/>; null — никакую.</summary>
        public static TraitInstance FindReplaced(Adventurer adventurer, SpecialTraitDefinition trait, DataRegistry data)
        {
            if (!trait.IsAcquired || IsPermanent(trait)) return null;
            foreach (TraitInstance instance in adventurer.Traits)
            {
                SpecialTraitDefinition existing = data.Get<SpecialTraitDefinition>(instance.TraitId);
                if (existing.IsAcquired && !IsPermanent(existing)) return instance;
            }
            return null;
        }

        /// <summary>
        /// Можно ли добавить черту, не считая <paramref name="ignored"/> (черту, которую новая вытеснит).
        /// Партнёра не проверяет — это делает вызывающий.
        /// </summary>
        public static bool CanAdd(Adventurer adventurer, SpecialTraitDefinition trait, DataRegistry data, TraitInstance ignored = null)
        {
            int count = 0;
            foreach (TraitInstance instance in adventurer.Traits)
            {
                if (instance == ignored) continue;
                if (instance.TraitId == trait.Id) return false;
                count++;

                SpecialTraitDefinition existing = data.Get<SpecialTraitDefinition>(instance.TraitId);
                if (existing.Category == trait.Category && !IsPermanent(existing) && !IsPermanent(trait)) return false;
                if (Contains(trait.IncompatibleWith, existing) || Contains(existing.IncompatibleWith, trait)) return false;
            }
            return count < data.Balance.Adventurers.MaxSpecialTraits;
        }

        /// <summary>Черты «от рождения» (не приобретённые) в порядке GameConfig.</summary>
        public static List<SpecialTraitDefinition> BirthTraits(DataRegistry data)
        {
            var result = new List<SpecialTraitDefinition>();
            foreach (SpecialTraitDefinition trait in data.All<SpecialTraitDefinition>())
            {
                if (!trait.IsAcquired) result.Add(trait);
            }
            return result;
        }

        /// <summary>Первая черта с кодовым правилом <paramref name="hook"/> (Соперник, Влюблённый…); null — нет такой.</summary>
        public static SpecialTraitDefinition FindByHook(DataRegistry data, TraitHook hook)
        {
            foreach (SpecialTraitDefinition trait in data.All<SpecialTraitDefinition>())
            {
                if (trait.HasHook(hook)) return trait;
            }
            return null;
        }

        /// <summary>Черта человека с кодовым правилом <paramref name="hook"/> (Пьяница, Семейный…); null — нет такой.</summary>
        public static TraitInstance FindWithHook(Adventurer adventurer, TraitHook hook, DataRegistry data)
        {
            foreach (TraitInstance instance in adventurer.Traits)
            {
                if (data.Get<SpecialTraitDefinition>(instance.TraitId).HasHook(hook)) return instance;
            }
            return null;
        }

        private static bool Contains(IReadOnlyList<SpecialTraitDefinition> list, SpecialTraitDefinition trait)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == trait || (list[i] != null && list[i].Id == trait.Id)) return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Появление и снятие особых черт в игре (ТЗ 04). Когда черта появляется (увечье, гибель друга, проверка месяца) —
    /// решают системы ТЗ 05, 09; их эффекты в чужих системах (паника, бегство, таверна, стресс, лояльность) — там же.
    /// Здесь: правила сочетания и замены, партнёр, выбор параметра Калеки, раскрытие «сразу при появлении»,
    /// лояльность и Слаженность Проверенного.
    /// </summary>
    public static class TraitService
    {
        /// <summary>
        /// Дать человеку черту. Новая приобретённая вытесняет старую приобретённую (кроме Калеки).
        /// <paramref name="partnerId"/> — для черт с партнёром (Соперник, Влюблённый) и Потерявшего товарища (погибший).
        /// <paramref name="stat"/> — параметр Калеки; не задан — случайный из списка эффекта (поток вызывающей системы).
        /// false — черту дать нельзя (уже есть, категория, несовместимость, лимит).
        /// </summary>
        public static bool TryAcquire(SimContext ctx, Adventurer adventurer, string traitId, int partnerId = 0, StatId? stat = null)
        {
            SpecialTraitDefinition trait = ctx.Data.Get<SpecialTraitDefinition>(traitId);
            if (trait.RequiresPartner && partnerId == 0) throw new ArgumentException($"Trait {traitId} requires a partner", nameof(partnerId));

            TraitInstance replaced = TraitRules.FindReplaced(adventurer, trait, ctx.Data);
            if (!TraitRules.CanAdd(adventurer, trait, ctx.Data, replaced)) return false;

            StatId? affected = ChooseAffectedStat(trait, stat, ctx.Rng);
            if (replaced != null) adventurer.RemoveTrait(replaced);

            var instance = new TraitInstance(trait.Id, ctx.World.Time.TotalHours, partnerId, affected);
            adventurer.AddTrait(instance);

            SimEvent acquired = ctx.Events.Publish(SimEventType.TraitAcquired, EventImportance.Normal, Participants(adventurer, partnerId))
                .With("trait", trait.Id);
            if (replaced != null) acquired.With("replaced", replaced.TraitId);
            if (affected.HasValue) acquired.With("stat", affected.Value);

            if (trait.RevealTrigger == RevealTrigger.OnAcquire) RevealService.Reveal(ctx, adventurer, instance, trait);

            // ❔ Проверенный: лояльность +10 (ТЗ 05), Слаженность +5.
            if (trait.HasHook(TraitHook.TestedOnAcquire))
            {
                StateService.ChangeLoyalty(ctx, adventurer, ctx.Data.Balance.Traits.TestedLoyaltyBonus);
                Growth.AddBonus(ctx, adventurer, StatId.Cohesion, ctx.Data.Balance.Traits.TestedCohesionBonus);
            }

            if (affected.HasValue || (replaced?.AffectedStat.HasValue ?? false)) ArchetypeService.Recalculate(ctx, adventurer);
            return true;
        }

        /// <summary>Снять черту (например, когда пройдёт срок Потерявшего товарища — ТЗ 05). false — черты нет.</summary>
        public static bool TryRemove(SimContext ctx, Adventurer adventurer, string traitId)
        {
            if (!adventurer.TryGetTrait(traitId, out TraitInstance instance)) return false;
            adventurer.RemoveTrait(instance);
            if (instance.AffectedStat.HasValue) ArchetypeService.Recalculate(ctx, adventurer);
            return true;
        }

        /// <summary>Черта при генерации: без событий и раскрытия (всё скрыто). Правила сочетания проверяет генератор.</summary>
        internal static TraitInstance AddAtGeneration(Adventurer adventurer, SpecialTraitDefinition trait, long nowHours, int partnerId)
        {
            var instance = new TraitInstance(trait.Id, nowHours, partnerId, null);
            adventurer.AddTrait(instance);
            return instance;
        }

        private static StatId? ChooseAffectedStat(SpecialTraitDefinition trait, StatId? requested, Rng rng)
        {
            foreach (TraitEffect effect in trait.Effects)
            {
                if (effect.Kind != EffectKind.ProfileMultiplier || effect.Stats.Count == 0) continue;
                return requested ?? rng.Pick(effect.Stats);
            }
            return null;
        }

        private static int[] Participants(Adventurer adventurer, int partnerId) =>
            partnerId != 0 ? new[] { adventurer.Id, partnerId } : new[] { adventurer.Id };
    }
}
