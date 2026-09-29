using System;
using System.Collections.Generic;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// События в пути и находки: один раунд со своим профилем (<see cref="QuestMath.EncounterProfile"/>) и тем же расчётом
    /// перекрытия.
    /// <list type="bullet">
    /// <item>Событие в пути (засада, звери) — одно из определений наугад. Провал: стресс всем, исход (рана одному наугад),
    /// момент напряжения. Тяжело раненный на пути туда поворачивает назад один («не дошёл»); если он был один — задание
    /// кончается отступлением.</item>
    /// <item>Находка (пещера) — бросок за задание при выходе, в случайный час пути туда. Замечает лучший по параметру
    /// находки; истинный ранг — ранг задания со сдвигом. Решающий решает: исследовать (один раунд: успех — пусто или тайник,
    /// провал — исход, момент напряжения, решение группы) или пройти мимо (по возвращении — рассказать Регистратору).</item>
    /// </list>
    /// </summary>
    public static class QuestEncounters
    {
        public static void TravelEvent(SimContext ctx, QuestRun run)
        {
            IReadOnlyList<RandomEventDefinition> events = ctx.Data.All<RandomEventDefinition>();
            if (events.Count == 0) return;

            RandomEventDefinition definition = ctx.Rng.Pick(events);
            List<Adventurer> present = QuestParty.Present(ctx.World, run);
            GuildRank rank = QuestMath.ShiftRank(run.Rank, ctx.Rng.RangeInclusive(definition.RankOffset.Min, definition.RankOffset.Max));
            float[] profile = QuestMath.EncounterProfile(ctx.Rng, ctx.Data, definition, rank);
            NounForms enemy = definition.Enemies.Count > 0 ? ctx.Rng.Pick(definition.Enemies) : run.Enemy;
            Adventurer first = present[0];

            QuestParty.Publish(ctx, SimEventType.TravelEvent, run, EventImportance.Notable, enemy, first.Id)
                .With("event", definition.Id).With("kind", "start");

            bool success = Round(ctx, run, present, profile, "travel-event");
            if (success)
            {
                QuestParty.Publish(ctx, SimEventType.TravelEvent, run, EventImportance.Normal, enemy, QuestSystem.Ids(present))
                    .With("event", definition.Id).With("kind", "success");
                return;
            }

            PartyContext context = QuestParty.ContextOf(present.Count);
            foreach (Adventurer member in present) StateService.AddStress(ctx, member, ctx.Data.Balance.Travel.EventFailStress, context);

            OutcomeChance outcome = PickOutcome(ctx, definition.FailureOutcomes);
            Adventurer victim = ctx.Rng.Pick(present);
            if (outcome != null && (outcome.Kind == OutcomeKind.LightWound || outcome.Kind == OutcomeKind.HeavyWound))
            {
                ConditionKind kind = outcome.Kind == OutcomeKind.HeavyWound ? ConditionKind.HeavyWound : ConditionKind.LightWound;
                Adventurer wounded = QuestRounds.Wound(ctx, run, kind, fromShield: false, target: victim);
                if (wounded != null && kind == ConditionKind.HeavyWound && wounded.State.HasHeavyWound())
                {
                    QuestParty.Publish(ctx, SimEventType.TravelEvent, run, EventImportance.Notable, enemy, wounded.Id)
                        .With("event", definition.Id).With("kind", "fail");
                    if (outcome.TurnsBack && run.Phase == QuestPhase.TravelOut) TurnBack(ctx, run, wounded);
                }
            }

            if (QuestSystem.IsActive(ctx, run) && !run.Retreated) QuestTension.Moment(ctx, run);
            AfterFlight(ctx, run);
        }

        /// <summary>
        /// Тяжело раненный на пути туда поворачивает назад: один — задание кончается отступлением (путь назад — столько же,
        /// сколько прошли); в группе — идёт домой один и дойдёт за пройденные ходовые часы.
        /// </summary>
        private static void TurnBack(SimContext ctx, QuestRun run, Adventurer wounded)
        {
            if (run.Members.Count == 1)
            {
                run.Retreated = true;
                QuestSystem.StartReturn(ctx, run);
                run.StartPhaseHours(Math.Max(1, run.OutHoursDone));
                return;
            }

            run.RemoveMember(wounded.Id);
            run.AddTurnedBack(wounded.Id);
            long due = ctx.Rhythm.MarchEnd(ctx.World.Time.TotalHours + 1, Math.Max(1, run.OutHoursDone));
            QuestSystem.AddStraggler(ctx, run, wounded, StragglerKind.TurnedBack, due, true);
        }

        /// <summary>Бросок находки при выходе: шанс за задание, ходовой час пути туда, какая находка.</summary>
        internal static void PlanDiscovery(SimContext ctx, QuestRun run)
        {
            IReadOnlyList<DiscoveryDefinition> discoveries = ctx.Data.All<DiscoveryDefinition>();
            if (discoveries.Count == 0) return;
            if (!ctx.RollChance(ctx.Data.Balance.Travel.DiscoveryChance, "discovery", null, "quest", run.Id)) return;

            int hour = ctx.Rng.RangeInclusive(1, run.PhaseHoursLeft);
            DiscoveryDefinition definition = discoveries.Count == 1 ? discoveries[0] : ctx.Rng.Pick(discoveries);
            run.Discovery = new QuestDiscovery(definition.Id, hour);
        }

        public static void Discover(SimContext ctx, QuestRun run)
        {
            QuestDiscovery discovery = run.Discovery;
            DiscoveryDefinition definition = ctx.Data.Get<DiscoveryDefinition>(discovery.DiscoveryId);
            if (!ctx.World.Orders.TryGetOrder(run.OrderId, out Order order)) return;
            List<Adventurer> present = QuestParty.Present(ctx.World, run);

            Adventurer spotter = QuestParty.Best(present, definition.SpotterStat, ctx.Data);
            discovery.SpotterId = spotter.Id;
            discovery.TrueRank = QuestMath.ShiftRank(run.Rank, ctx.Rng.RangeInclusive(definition.RankOffset.Min, definition.RankOffset.Max));
            discovery.State = DiscoveryState.Found;
            float[] profile = QuestMath.EncounterProfile(ctx.Rng, ctx.Data, definition, discovery.TrueRank);
            Publish(ctx, run, definition, "found", EventImportance.Notable, spotter.Id).With("trueRank", discovery.TrueRank);

            if (!QuestChoices.DecideExplore(ctx, run, definition, profile, order.Reward))
            {
                discovery.State = DiscoveryState.Skipped;
                Publish(ctx, run, definition, "skip", EventImportance.Normal, QuestSystem.Ids(present));
                return;
            }

            discovery.State = DiscoveryState.Explored;
            Adventurer leader = QuestParty.Leader(present, ctx.Data);
            Publish(ctx, run, definition, "explore", EventImportance.Notable, leader.Id);

            if (Round(ctx, run, present, profile, "discovery"))
            {
                OutcomeChance success = PickOutcome(ctx, definition.SuccessOutcomes);
                if (success != null && success.Kind == OutcomeKind.Loot)
                {
                    float share = ctx.Rng.Range(success.LootShareOfReward.Min, success.LootShareOfReward.Max);
                    discovery.Loot = (int)Math.Round(order.Reward * share, MidpointRounding.AwayFromZero);
                    Publish(ctx, run, definition, "loot", EventImportance.Notable, QuestSystem.Ids(present)).With("amount", discovery.Loot);
                }
                else
                {
                    Publish(ctx, run, definition, "empty", EventImportance.Notable, QuestSystem.Ids(present));
                }
                return;
            }

            Publish(ctx, run, definition, "fail", EventImportance.Important, QuestSystem.Ids(present));
            PartyContext context = QuestParty.ContextOf(present.Count);
            foreach (Adventurer member in present) StateService.AddStress(ctx, member, ctx.Data.Balance.Travel.EventFailStress, context);
            ApplyOutcome(ctx, run, PickOutcome(ctx, definition.FailureOutcomes));

            if (QuestSystem.IsActive(ctx, run)) QuestTension.Moment(ctx, run);
            AfterFlight(ctx, run);
            if (!QuestSystem.IsActive(ctx, run))
            {
                QuestSettlement.Finish(ctx, run);
                return;
            }
            if (!run.Retreated && !QuestChoices.DecideAfterFailure(ctx, run, order))
            {
                run.Retreated = true;
                QuestSystem.StartReturn(ctx, run);
                run.StartPhaseHours(Math.Max(1, run.OutHoursDone));
            }
        }

        /// <summary>Исход провала в пещере: тяжёлая рана одному, гибель одного или лёгкие раны нескольким (кто принимает удар).</summary>
        private static void ApplyOutcome(SimContext ctx, QuestRun run, OutcomeChance outcome)
        {
            if (outcome == null) return;
            switch (outcome.Kind)
            {
                case OutcomeKind.HeavyWound:
                case OutcomeKind.LightWound:
                    ConditionKind kind = outcome.Kind == OutcomeKind.HeavyWound ? ConditionKind.HeavyWound : ConditionKind.LightWound;
                    var hurt = new List<int>();
                    for (int i = 0; i < Math.Max(1, outcome.Count); i++)
                    {
                        List<Adventurer> present = QuestParty.Present(ctx.World, run);
                        present.RemoveAll(a => hurt.Contains(a.Id));
                        if (present.Count == 0) break;
                        Adventurer target = QuestParty.Shield(present, run, ctx.Data);
                        hurt.Add(target.Id);
                        QuestRounds.Wound(ctx, run, kind, fromShield: false, target: target);
                    }
                    break;
                case OutcomeKind.Death:
                    List<Adventurer> all = QuestParty.Present(ctx.World, run);
                    if (all.Count > 0) QuestRounds.Kill(ctx, run, ctx.Rng.Pick(all), everyone: all.Count == 1);
                    break;
            }
        }

        /// <summary>Один раунд события или находки: перекрытие группы и профиля + синергии; строка в лог.</summary>
        private static bool Round(SimContext ctx, QuestRun run, List<Adventurer> present, float[] profile, string roll)
        {
            float overlap = QuestMath.Overlap(ctx.Data.Stats.RadarOrder, profile, QuestMath.GroupProfile(present, ctx.Data, run.Panicked, run.Rushing));
            float chance = Math.Max(0f, Math.Min(1f, overlap + QuestMath.Synergy(present, ctx.World.Relations, ctx.Data)));
            return ctx.RollChance(chance, roll, QuestParty.Leader(present, ctx.Data), "overlap", overlap);
        }

        /// <summary>Все сбежали — задание кончается отступлением.</summary>
        private static void AfterFlight(SimContext ctx, QuestRun run)
        {
            if (run.Members.Count == 0) run.Retreated = true;
        }

        private static OutcomeChance PickOutcome(SimContext ctx, IReadOnlyList<OutcomeChance> outcomes)
        {
            if (outcomes.Count == 0) return null;
            var weights = new float[outcomes.Count];
            for (int i = 0; i < weights.Length; i++) weights[i] = outcomes[i].Chance;
            return outcomes[ctx.Rng.PickWeightedIndex(weights)];
        }

        private static SimEvent Publish(SimContext ctx, QuestRun run, DiscoveryDefinition definition, string kind, EventImportance importance,
            params int[] participants) =>
            QuestParty.Publish(ctx, SimEventType.Discovery, run, importance, participants)
                .With("discovery", definition.Id).With("kind", kind);
    }
}
