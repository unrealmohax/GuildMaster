using System;
using System.Collections.Generic;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Раунд на месте задания и его последствия.
    /// <list type="bullet">
    /// <item>Шанс = перекрытие профиля группы и требований заказа + синергии, в пределах 0..1; сработал потолок — раунд провален
    /// сам. Паника и бросок вперёд действуют в этом раунде и снимаются.</item>
    /// <item>Успех — задание выполнено, путь назад. Провал — ступень лестницы провалов (<see cref="RoundsBalance.FailureLadder"/>):
    /// стресс, время, бонус, раны, снаряжение, добыча, гибель на 4-м, гибель всех на 5-м; затем момент напряжения и решение
    /// группы: продолжить (ещё раунд) или отступить.</item>
    /// <item>Экзамен на повышение — один раунд без синергий, потолков, лестницы и решений: сдал — ранг +1, нет — повтор
    /// через <c>promotionRetryDays</c>.</item>
    /// </list>
    /// Броски — поток системы заданий, в порядке: раунд, бонус, рана, увечье, геройство, Лекарь, снаряжение (и ремонт),
    /// добыча, гибель (спасение Лекарем), момент напряжения по каждому, решение группы.
    /// </summary>
    public static class QuestRounds
    {
        public static void Resolve(SimContext ctx, QuestRun run)
        {
            List<Adventurer> present = QuestParty.Present(ctx.World, run);
            if (!ctx.World.Orders.TryGetOrder(run.OrderId, out Order order)) throw new InvalidOperationException($"Quest {run.Id}: no order {run.OrderId}");

            if (run.IsPromotion)
            {
                Exam(ctx, run, order, present);
                return;
            }

            float[] group = QuestMath.GroupProfile(present, ctx.Data, run.Panicked, run.Rushing);
            run.ClearRoundEffects();
            float overlap = QuestMath.Overlap(ctx.Data.Stats.RadarOrder, order.Profile, group);
            var pairs = new List<PairSynergy>();
            float synergy = QuestMath.Synergy(present, ctx.World.Relations, ctx.Data, pairs);
            float chance = Math.Max(0f, Math.Min(1f, overlap + synergy));
            bool ceiling = QuestMath.CeilingHit(order, present.Count, group);
            int[] ids = QuestSystem.Ids(present);

            if (run.Round == 1) PublishSynergies(ctx, run, pairs);
            ctx.Log.Write(SimLogLevel.Debug, "quest #{0} round {1}: overlap={2} synergy={3}", run.Id, run.Round,
                AdventurerLog.Number(overlap), AdventurerLog.Number(synergy));

            bool success;
            if (ceiling)
            {
                success = false;
                QuestParty.Publish(ctx, SimEventType.CeilingTriggered, run, EventImportance.Notable, ids);
            }
            else
            {
                success = ctx.RollChance(chance, "round", QuestParty.Leader(present, ctx.Data), "overlap", overlap);
            }

            if (success)
            {
                QuestParty.Publish(ctx, SimEventType.RoundSuccess, run, EventImportance.Normal, ids)
                    .With("round", run.Round).With("chance", chance).With("overlap", overlap);
                run.Completed = true;
                QuestSystem.StartReturn(ctx, run);
                return;
            }

            run.FailedRounds++;
            QuestParty.Publish(ctx, SimEventType.RoundFail, run, EventImportance.Notable, ids)
                .With("round", run.Round).With("failed", run.FailedRounds).With("chance", chance).With("overlap", overlap);
            ApplyFailure(ctx, run, present);
            if (!QuestSystem.IsActive(ctx, run))
            {
                QuestSettlement.Finish(ctx, run);
                return;
            }

            QuestTension.Moment(ctx, run);
            if (!QuestSystem.IsActive(ctx, run))
            {
                QuestSettlement.Finish(ctx, run);
                return;
            }

            if (QuestChoices.DecideAfterFailure(ctx, run, order))
            {
                run.Round++;
                run.StartPhaseHours(QuestSystem.RoundHours(ctx, run) * Math.Max(1, ctx.Data.Balance.Rounds.ExtraRoundsPerFailure));
            }
            else
            {
                run.Retreated = true;
                QuestSystem.StartReturn(ctx, run);
            }
        }

        private static void Exam(SimContext ctx, QuestRun run, Order order, List<Adventurer> present)
        {
            Adventurer candidate = present[0];
            float overlap = QuestMath.Overlap(ctx.Data.Stats.RadarOrder, order.Profile, QuestMath.GroupProfile(present, ctx.Data));
            bool passed = ctx.RollChance(overlap, "exam", candidate, "overlap", overlap);
            run.Completed = passed;
            if (passed) GuildRanks.Promote(ctx, candidate);
            else GuildRanks.FailPromotion(ctx, candidate);
            QuestParty.Publish(ctx, SimEventType.PromotionExam, run, EventImportance.Notable, candidate.Id)
                .With("passed", passed).With("chance", overlap);
            QuestSystem.StartReturn(ctx, run);
        }

        private static void PublishSynergies(SimContext ctx, QuestRun run, List<PairSynergy> pairs)
        {
            var shown = new HashSet<long>();
            foreach (PairSynergy pair in pairs)
            {
                string kind = pair.Kind == "longPartners" ? "friends" : pair.Kind;
                if (kind == "dislike") continue; // своей строки нет
                long key = ((long)pair.A << 32) | (uint)pair.B;
                if (!shown.Add(key)) continue;
                QuestParty.Publish(ctx, SimEventType.Synergy, run, kind == "rivals" ? EventImportance.Notable : EventImportance.Normal, pair.A, pair.B)
                    .With("kind", kind);
            }
        }

        /// <summary>Ступень лестницы провалов для номера провала (с 1).</summary>
        public static FailureStep StepFor(RoundsBalance rounds, int failedRounds)
        {
            IReadOnlyList<FailureStep> ladder = rounds.FailureLadder;
            return ladder[Math.Max(0, Math.Min(failedRounds, ladder.Count) - 1)];
        }

        private static void ApplyFailure(SimContext ctx, QuestRun run, List<Adventurer> present)
        {
            FailureStep step = StepFor(ctx.Data.Balance.Rounds, run.FailedRounds);
            int[] ids = QuestSystem.Ids(present);

            StressEvents.RoundFailed(ctx, present, run.FailedRounds);
            QuestParty.Publish(ctx, SimEventType.QuestLoss, run, EventImportance.Normal, ids).With("kind", "time");
            if (step.Stress > 0f) QuestParty.Publish(ctx, SimEventType.QuestLoss, run, EventImportance.Normal, ids).With("kind", "stress");

            if (!run.BonusLost && step.BonusLossChance > 0f && ctx.RollChance(step.BonusLossChance, "bonus-loss", null, "failed", run.FailedRounds))
            {
                run.BonusLost = true;
                QuestParty.Publish(ctx, SimEventType.QuestLoss, run, EventImportance.Notable, ids).With("kind", "bonus");
            }

            Adventurer hit = null;
            if (step.HeavyWoundChance > 0f || step.LightWoundChance > 0f)
            {
                float roll = ctx.Rng.NextFloat();
                ConditionKind? kind = roll < step.HeavyWoundChance ? ConditionKind.HeavyWound
                    : roll < step.HeavyWoundChance + step.LightWoundChance ? ConditionKind.LightWound
                    : (ConditionKind?)null;
                ctx.Log.Write(SimLogLevel.Debug, "roll wound quest #{0} rolled={1} => {2}", run.Id, AdventurerLog.Number(roll), kind?.ToString() ?? "none");
                if (kind.HasValue) hit = Wound(ctx, run, kind.Value, fromShield: true);
            }
            if (step.MaimChance > 0f && QuestSystem.IsActive(ctx, run))
            {
                Adventurer target = hit ?? QuestParty.Shield(QuestParty.Present(ctx.World, run), run, ctx.Data);
                if (target != null && ctx.RollChance(step.MaimChance, "maim", target)) Maim(ctx, run, target);
            }

            if (step.GearDamageChance > 0f)
            {
                Adventurer owner = hit ?? QuestParty.Shield(QuestParty.Present(ctx.World, run), run, ctx.Data);
                if (owner != null && ctx.RollChance(step.GearDamageChance, "gear-damage", owner))
                {
                    IntRange repair = ctx.Data.Balance.Expenses.GearRepair;
                    int cost = ctx.Rng.RangeInclusive(repair.Min, repair.Max);
                    int paid = WalletService.Pay(ctx, owner, cost);
                    QuestParty.Publish(ctx, SimEventType.QuestLoss, run, EventImportance.Notable, owner.Id)
                        .With("kind", "gear").With("amount", cost).With("paid", paid);
                }
            }

            if (!run.LootLost && step.LootLossChance > 0f && ctx.RollChance(step.LootLossChance, "loot-loss", null, "failed", run.FailedRounds))
            {
                run.LootLost = true;
                QuestParty.Publish(ctx, SimEventType.QuestLoss, run, EventImportance.Notable, ids).With("kind", "loot");
            }

            switch (step.Death)
            {
                case FailureDeath.MostWounded:
                    DieMostWounded(ctx, run);
                    break;
                case FailureDeath.Everyone:
                    List<Adventurer> all = QuestParty.Present(ctx.World, run);
                    foreach (Adventurer member in all) Kill(ctx, run, member, everyone: true);
                    break;
            }
        }

        /// <summary>
        /// Ранить на задании того, кто принимает удар (<see cref="QuestParty.Shield"/>). Тяжёлую рану может принять на себя
        /// герой; Лекарь в группе — шанс, что рана на ступень легче. Возвращает раненого (или <c>null</c>, если рана не случилась).
        /// </summary>
        public static Adventurer Wound(SimContext ctx, QuestRun run, ConditionKind kind, bool fromShield, Adventurer target = null)
        {
            List<Adventurer> present = QuestParty.Present(ctx.World, run);
            if (present.Count == 0) return null;
            target = target ?? QuestParty.Shield(present, run, ctx.Data);
            if (target == null) return null;

            if (kind == ConditionKind.HeavyWound) target = QuestTension.TryHero(ctx, run, present, target);

            Adventurer medic = FieldMedic(present, target, ctx.Data);
            if (medic != null && ctx.RollChance(ctx.Data.Balance.Health.FieldMedicChance, "field-medic", medic))
            {
                if (kind == ConditionKind.LightWound)
                {
                    ctx.Log.Write(SimLogLevel.Debug, "quest #{0}: medic #{1} kept #{2} unhurt", run.Id, medic.Id, target.Id);
                    return null;
                }
                kind = ConditionKind.LightWound;
            }

            if (fromShield && present.Count > 1)
                QuestParty.Publish(ctx, SimEventType.QuestWound, run, EventImportance.Notable, target.Id).With("kind", "shield").With("shield", target.Id);

            PartyContext context = QuestParty.ContextOf(present.Count);
            HealthService.Wound(ctx, target, kind, context);
            bool maimed = LastWoundMaimed(ctx);
            run.HadWounds = true;
            StressEvents.ComradeWounded(ctx, present, target);

            Adventurer partner = null;
            foreach (Adventurer member in present)
            {
                if (member.Id != target.Id) { partner = member; break; }
            }
            SimEvent line = partner != null
                ? QuestParty.Publish(ctx, SimEventType.QuestWound, run, Importance(kind), target.Id, partner.Id)
                : QuestParty.Publish(ctx, SimEventType.QuestWound, run, Importance(kind), target.Id);
            line.With("kind", kind == ConditionKind.HeavyWound ? "heavy" : "light");
            if (maimed) PublishMaimed(ctx, run, target, present);
            return target;
        }

        /// <summary>Увечье с лестницы провалов: черта «Калека» (если её можно дать) и строка.</summary>
        private static void Maim(SimContext ctx, QuestRun run, Adventurer target)
        {
            if (!HealthService.Maim(ctx, target)) return;
            run.HadWounds = true;
            PublishMaimed(ctx, run, target, QuestParty.Present(ctx.World, run));
        }

        private static void PublishMaimed(SimContext ctx, QuestRun run, Adventurer target, List<Adventurer> present)
        {
            Adventurer medic = QuestParty.Best(present, StatId.Medicine, ctx.Data, target.Id) ?? target;
            QuestParty.Publish(ctx, SimEventType.QuestWound, run, EventImportance.Important, target.Id)
                .With("kind", "maimed").With("medic", medic.Id);
        }

        /// <summary>Вторая тяжёлая стала увечьем: последнее событие раны с <c>maimed</c>.</summary>
        private static bool LastWoundMaimed(SimContext ctx)
        {
            IReadOnlyList<SimEvent> events = ctx.Events.Events;
            for (int i = events.Count - 1; i >= 0; i--)
            {
                if (events[i].Type != SimEventType.AdventurerWounded) continue;
                return events[i].TryGet("maimed", out bool maimed) && maimed;
            }
            return false;
        }

        /// <summary>Лекарь в группе, кроме самого раненого: человек, чей архетип держит Медицину основным параметром.</summary>
        private static Adventurer FieldMedic(List<Adventurer> present, Adventurer wounded, DataRegistry data)
        {
            foreach (Adventurer member in present)
            {
                if (member.Id == wounded.Id || !data.TryGet(member.ArchetypeId, out ArchetypeDefinition archetype)) continue;
                foreach (StatId stat in archetype.MainStats)
                {
                    if (stat == StatId.Medicine) return member;
                }
            }
            return null;
        }

        private static EventImportance Importance(ConditionKind kind) =>
            kind == ConditionKind.HeavyWound ? EventImportance.Important : EventImportance.Notable;

        /// <summary>
        /// Гибель самого тяжело раненого. Лекарь спасает с шансом min(<c>medicSaveMax</c>, Σ Медицины остальных /
        /// <c>medicSaveDivisor</c>) — умиравший становится Калекой.
        /// </summary>
        private static void DieMostWounded(SimContext ctx, QuestRun run)
        {
            List<Adventurer> present = QuestParty.Present(ctx.World, run);
            Adventurer victim = QuestParty.MostWounded(present, ctx.Data);
            if (victim == null) return;

            RoundsBalance rounds = ctx.Data.Balance.Rounds;
            float medicine = 0f;
            foreach (Adventurer member in present)
            {
                if (member.Id != victim.Id) medicine += AdventurerStats.Effective(member, StatId.Medicine, ctx.Data);
            }
            float saveChance = Math.Min(rounds.MedicSaveMax, medicine / rounds.MedicSaveDivisor);
            if (saveChance > 0f && ctx.RollChance(saveChance, "medic-save", victim, "medicine", medicine))
            {
                Adventurer medic = QuestParty.Best(present, StatId.Medicine, ctx.Data, victim.Id);
                HealthService.Maim(ctx, victim);
                run.HadWounds = true;
                QuestParty.Publish(ctx, SimEventType.MedicSaved, run, EventImportance.Important, victim.Id).With("medic", medic.Id);
                return;
            }
            Kill(ctx, run, victim, everyone: false);
        }

        /// <summary>
        /// Человек погиб на задании: уходит из группы и из гильдии (архив), стресс и «Потерявший товарища» у остальных,
        /// репутация −<c>deathReputation</c>, событие [В] с автопаузой.
        /// </summary>
        public static void Kill(SimContext ctx, QuestRun run, Adventurer victim, bool everyone)
        {
            run.RemoveMember(victim.Id);
            run.AddDead(victim.Id);
            AdventurerState state = victim.State;
            state.QuestRunId = 0;
            state.QuestParty = PartyContext.None;

            QuestParty.Publish(ctx, SimEventType.AdventurerDied, run, EventImportance.Important, victim.Id).With("all", everyone);
            AdventurerLifecycle.Retire(ctx, victim, LeaveReason.Died);
            StressEvents.AdventurerDied(ctx, victim);
            ReputationService.Change(ctx, ctx.Data.Balance.Guild.DeathReputation, "death");
        }
    }
}
