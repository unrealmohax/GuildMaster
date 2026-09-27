using System;
using System.Collections.Generic;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Моменты напряжения: после проваленного раунда, провала события в пути или в пещере. По каждому присутствующему,
    /// по порядку выхода:
    /// <list type="number">
    /// <item>Ветеран войны раскрывается первым моментом напряжения;</item>
    /// <item>стресс выше <c>breakdownThreshold</c> — срыв: как паника, своя строка;</item>
    /// <item>бегство — шанс из эффектов черт (трус на крайнем полюсе, Бывший дезертир при стрессе): уходит один, беглец;</item>
    /// <item>паника — max(0, (стресс − Хладнокровие + сдвиг) / делитель) + эффекты черт (трус — по силе оси, Железные нервы,
    /// Ветеран — минус): профиль × 0,5 в следующем раунде, раскрывает труса;</item>
    /// <item>бросок вперёд — безрассудный на крайнем полюсе: вклад × 1,3 в следующем раунде, первым получает рану.</item>
    /// </list>
    /// Кто-то запаниковал — у Железных нервов, кто не дрогнул, строка «держится» и раскрытие. Геройство — в момент тяжёлой
    /// раны товарища (<see cref="TryHero"/>). Беглец один — задание кончается отступлением.
    /// </summary>
    public static class QuestTension
    {
        public static void Moment(SimContext ctx, QuestRun run)
        {
            DataRegistry data = ctx.Data;
            var panicked = new List<Adventurer>();
            var calm = new List<Adventurer>();

            foreach (Adventurer member in QuestParty.Present(ctx.World, run))
            {
                if (!QuestParty.Contains(run.Members, member.Id)) continue; // уже сбежал в этом моменте

                TraitInstance veteran = TraitRules.FindWithHook(member, TraitHook.VeteranBinge, data);
                if (veteran != null) RevealService.TryRevealTrait(ctx, member, veteran.TraitId, RevealTrigger.VeteranFirstTension);

                if (member.State.Stress > data.Balance.State.BreakdownThreshold)
                {
                    run.SetPanicked(member.Id);
                    panicked.Add(member);
                    QuestParty.Publish(ctx, SimEventType.TensionMoment, run, EventImportance.Important, member.Id).With("kind", "breakdown");
                    continue;
                }

                float flee = Math.Min(1f, TraitChance(member, TensionKind.Flee, data));
                if (flee > 0f && ctx.RollChance(flee, "flee", member, "stress", member.State.Stress))
                {
                    Flee(ctx, run, member);
                    continue;
                }

                float panic = PanicChance(member, data);
                if (panic > 0f && ctx.RollChance(panic, "panic", member, "stress", member.State.Stress))
                {
                    run.SetPanicked(member.Id);
                    panicked.Add(member);
                    QuestParty.Publish(ctx, SimEventType.TensionMoment, run, EventImportance.Notable, member.Id).With("kind", "panic");
                    RevealService.TryRevealAxis(ctx, member, AxisId.Risk, RevealTrigger.CowardPanicOrFlee);
                    continue;
                }

                float rush = Math.Min(1f, TraitChance(member, TensionKind.Rush, data));
                if (rush > 0f && ctx.RollChance(rush, "rush", member))
                {
                    run.SetRushing(member.Id);
                    QuestParty.Publish(ctx, SimEventType.TensionMoment, run, EventImportance.Notable, member.Id).With("kind", "rush");
                    RevealService.TryRevealAxis(ctx, member, AxisId.Risk, RevealTrigger.RecklessRush);
                }
                calm.Add(member);
            }

            if (panicked.Count == 0) return;
            foreach (Adventurer member in calm)
            {
                TraitInstance ironNerves = TraitRules.FindWithHook(member, TraitHook.IronNervesComradeDeath, data);
                if (ironNerves == null || !QuestParty.Contains(run.Members, member.Id)) continue;
                QuestParty.Publish(ctx, SimEventType.TensionMoment, run, EventImportance.Normal, member.Id).With("kind", "hold");
                RevealService.TryRevealTrait(ctx, member, ironNerves.TraitId, RevealTrigger.IronNervesHeld);
            }
        }

        /// <summary>Шанс паники: max(0, (стресс − Хладнокровие + сдвиг) / делитель) + эффекты черт паники, в пределах 0..1.</summary>
        public static float PanicChance(Adventurer adventurer, DataRegistry data)
        {
            TensionBalance tension = data.Balance.Tension;
            float composure = AdventurerStats.Effective(adventurer, StatId.Composure, data);
            float basis = Math.Max(0f, (adventurer.State.Stress - composure + tension.PanicOffset) / tension.PanicDivisor);
            return Math.Max(0f, Math.Min(1f, basis + TraitChance(adventurer, TensionKind.Panic, data)));
        }

        /// <summary>
        /// Добавка черт к шансу реакции (<see cref="EffectKind.TensionModifier"/>). У осей: условие «крайний полюс» — вся
        /// добавка на крайнем полюсе; «всегда» — добавка × сила оси. У особых черт — вся добавка. Условие «стресс выше» —
        /// только выше порога.
        /// </summary>
        public static float TraitChance(Adventurer adventurer, TensionKind kind, DataRegistry data)
        {
            float sum = 0f;
            AdventurersBalance people = data.Balance.Adventurers;
            for (int i = 0; i < Vocabulary.AxisCount; i++)
            {
                float value = adventurer.GetAxis((AxisId)i);
                AxisPole pole = AxisMath.PoleOf(value);
                foreach (TraitEffect effect in data.Axis((AxisId)i).Pole(pole).Effects)
                {
                    if (effect.Kind != EffectKind.TensionModifier || effect.Tension != kind) continue;
                    switch (effect.Condition)
                    {
                        case EffectCondition.ExtremePole:
                            if (AxisMath.IsOnExtremePole(value, pole, people)) sum += effect.Value;
                            break;
                        case EffectCondition.StressAbove:
                            if (adventurer.State.Stress > effect.ConditionThreshold) sum += effect.Value * AxisMath.Strength(value, pole);
                            break;
                        case EffectCondition.Always:
                            sum += effect.Value * AxisMath.Strength(value, pole);
                            break;
                    }
                }
            }
            foreach (TraitInstance trait in adventurer.Traits)
            {
                foreach (TraitEffect effect in data.Get<SpecialTraitDefinition>(trait.TraitId).Effects)
                {
                    if (effect.Kind != EffectKind.TensionModifier || effect.Tension != kind) continue;
                    if (effect.Condition == EffectCondition.StressAbove && adventurer.State.Stress <= effect.ConditionThreshold) continue;
                    if (effect.Condition != EffectCondition.Always && effect.Condition != EffectCondition.StressAbove) continue;
                    sum += effect.Value;
                }
            }
            return sum;
        }

        /// <summary>
        /// Товарищ должен получить тяжёлую рану: кто из остальных с Хладнокровием от <c>heroMinComposure</c> и осью «Люди» от
        /// <c>heroMinPeopleAxis</c> (по порядку выхода, бросок <c>heroChance</c> каждому до первого успеха) принимает её на себя;
        /// отношения с товарищем + <c>heroRelation</c>. Возвращает, кто получит рану.
        /// </summary>
        public static Adventurer TryHero(SimContext ctx, QuestRun run, List<Adventurer> present, Adventurer target)
        {
            TensionBalance tension = ctx.Data.Balance.Tension;
            foreach (Adventurer member in present)
            {
                if (member.Id == target.Id) continue;
                if (AdventurerStats.Effective(member, StatId.Composure, ctx.Data) < tension.HeroMinComposure) continue;
                if (member.GetAxis(AxisId.People) < tension.HeroMinPeopleAxis) continue;
                if (!ctx.RollChance(tension.HeroChance, "hero", member)) continue;

                RelationService.Change(ctx, member.Id, target.Id, tension.HeroRelation);
                QuestParty.Publish(ctx, SimEventType.TensionMoment, run, EventImportance.Important, member.Id, target.Id).With("kind", "hero");
                return member;
            }
            return target;
        }

        /// <summary>
        /// Бегство: человек уходит из группы и идёт домой один. Брошенным — стресс и отношения к беглецу хуже; раскрываются
        /// трус и Бывший дезертир. Бросок: вернётся в гильдию через 1–3 дня или исчезнет. Был один — задание кончается
        /// отступлением.
        /// </summary>
        public static void Flee(SimContext ctx, QuestRun run, Adventurer fugitive)
        {
            DataRegistry data = ctx.Data;
            TensionBalance tension = data.Balance.Tension;
            run.RemoveMember(fugitive.Id);
            run.AddFled(fugitive.Id);

            List<Adventurer> abandoned = QuestParty.Present(ctx.World, run);
            PartyContext context = QuestParty.ContextOf(abandoned.Count);
            foreach (Adventurer member in abandoned)
            {
                StateService.AddStress(ctx, member, tension.AbandonedStress, context);
                RelationService.Change(ctx, member.Id, fugitive.Id, data.Balance.Adventurers.FleeAbandonedRelation);
            }

            RevealService.TryRevealAxis(ctx, fugitive, AxisId.Risk, RevealTrigger.CowardPanicOrFlee);
            TraitInstance deserter = FindDeserter(fugitive, data);
            if (deserter != null) RevealService.TryRevealTrait(ctx, fugitive, deserter.TraitId, RevealTrigger.DeserterFled);

            bool returns = ctx.RollChance(tension.FugitiveReturnChance, "fugitive-returns", fugitive);
            int days = ctx.Rng.RangeInclusive(tension.FugitiveReturnDays.Min, tension.FugitiveReturnDays.Max);
            fugitive.LastFledAtHours = ctx.World.Time.TotalHours;
            QuestSystem.AddStraggler(ctx, run, fugitive, StragglerKind.Fugitive, ctx.World.Time.TotalHours + ctx.Calendar.DaysToHours(days), returns);

            QuestParty.Publish(ctx, SimEventType.AdventurerFled, run, EventImportance.Important, fugitive.Id)
                .With("reason", QuestReasons.Fled(ctx, fugitive))
                .With("returns", returns)
                .With("days", days);

            if (run.Members.Count == 0) run.Retreated = true;
        }

        /// <summary>Бывший дезертир — черта с эффектом бегства (кроме осей).</summary>
        private static TraitInstance FindDeserter(Adventurer adventurer, DataRegistry data)
        {
            foreach (TraitInstance trait in adventurer.Traits)
            {
                foreach (TraitEffect effect in data.Get<SpecialTraitDefinition>(trait.TraitId).Effects)
                {
                    if (effect.Kind == EffectKind.TensionModifier && effect.Tension == TensionKind.Flee) return trait;
                }
            }
            return null;
        }
    }
}
