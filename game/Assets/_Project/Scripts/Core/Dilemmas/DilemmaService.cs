using System;
using System.Collections.Generic;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Обращения: открыть (эффекты появления — раскрытия), ответить вариантом (игрок или по сроку — все эффекты варианта, видимые
    /// и отложенные), снять без последствий. Деньги — через казну: заём — «Займы авантюристам», прочие расходы — «Обращения»,
    /// штраф — «Штрафы». Эффекты «всем в таверне вечером» откладываются до ближайших итогов вечера (<see cref="ApplyFeast"/>).
    /// </summary>
    public static class DilemmaService
    {
        /// <summary>
        /// Открыть обращение: срок ответа — <c>responseDays</c> суток, перезарядка считается отсюда; эффекты появления;
        /// событие <see cref="SimEventType.DilemmaArrived"/> и строка лога, что человек подал обращение.
        /// </summary>
        internal static Dilemma Open(SimContext ctx, DilemmaDefinition definition, Action<Dilemma> setup)
        {
            DilemmaBook book = ctx.World.Dilemmas;
            long now = ctx.World.Time.TotalHours;
            var dilemma = new Dilemma(book.NextId(), definition.Id, definition.Trigger, now,
                now + ctx.Calendar.DaysToHours(ctx.Data.Balance.Dilemmas.ResponseDays));
            setup(dilemma);
            book.Add(dilemma);

            LogRequest(ctx, dilemma);
            foreach (DilemmaEffect effect in definition.ArrivalEffects) Apply(ctx, dilemma, effect);

            SimEvent arrived = ctx.Events.Publish(SimEventType.DilemmaArrived, EventImportance.Notable, Participants(dilemma));
            Describe(ctx, arrived, dilemma);
            return dilemma;
        }

        /// <summary>
        /// Ответ вариантом <paramref name="optionIndex"/>: все эффекты варианта, обращение закрыто, событие со строкой варианта.
        /// Игрок не может выбрать скрытый вариант или вариант, на который не хватает денег, — тогда ничего не происходит.
        /// </summary>
        internal static bool Answer(SimContext ctx, Dilemma dilemma, int optionIndex, bool timedOut)
        {
            if (!dilemma.IsOpen) return false;
            DilemmaOption option = DilemmaRules.OptionOf(ctx.Data, dilemma, optionIndex);
            if (option == null) return false;
            if (!timedOut && !DilemmaRules.IsAvailable(ctx.World, ctx.Data, dilemma, optionIndex)) return false;

            int amount = DilemmaRules.OptionCost(ctx.World, ctx.Data, dilemma, optionIndex);
            int[] participants = Participants(dilemma);
            bool deferred = false;
            foreach (DilemmaEffect effect in option.Effects)
            {
                if (effect.Target == DilemmaTarget.AllInTavern) deferred = true;
                else Apply(ctx, dilemma, effect);
            }
            if (deferred)
            {
                ctx.World.Dilemmas.PendingFeastId = dilemma.Id;
                ctx.World.Dilemmas.PendingFeastOption = optionIndex;
            }

            dilemma.Status = timedOut ? DilemmaStatus.TimedOut : DilemmaStatus.Answered;
            dilemma.OptionIndex = optionIndex;
            dilemma.ClosedAtHours = ctx.World.Time.TotalHours;
            ctx.World.Dilemmas.Close(dilemma);

            ctx.Log.Write(SimLogLevel.Info, "dilemma #{0} {1}: option {2}{3}", dilemma.Id, dilemma.DefinitionId, optionIndex,
                timedOut ? " (no answer)" : string.Empty);
            SimEvent answered = ctx.Events.Publish(SimEventType.DilemmaAnswered, EventImportance.Normal, participants)
                .With("option", optionIndex)
                .With("timedOut", timedOut)
                .With("feedKey", option.AnswerFeedKey)
                .With("amount", amount);
            Describe(ctx, answered, dilemma);
            return true;
        }

        /// <summary>Снять обращение без последствий (тот, кого оно касалось, ушёл из гильдии или погиб).</summary>
        internal static void Withdraw(SimContext ctx, Dilemma dilemma)
        {
            if (!dilemma.IsOpen) return;
            dilemma.Status = DilemmaStatus.Withdrawn;
            dilemma.ClosedAtHours = ctx.World.Time.TotalHours;
            ctx.World.Dilemmas.Close(dilemma);
            ctx.Log.Write(SimLogLevel.Info, "dilemma #{0} {1}: withdrawn", dilemma.Id, dilemma.DefinitionId);
            SimEvent withdrawn = ctx.Events.Publish(SimEventType.DilemmaWithdrawn, EventImportance.Normal, Participants(dilemma));
            Describe(ctx, withdrawn, dilemma);
        }

        /// <summary>
        /// Итоги вечера: если ждёт праздник, его эффекты «всем в таверне» — тем, кто провёл в таверне хотя бы час вечера
        /// (<paramref name="there"/>); отношения — каждой паре. Праздник проходит один раз.
        /// </summary>
        internal static void ApplyFeast(SimContext ctx, List<Adventurer> there)
        {
            DilemmaBook book = ctx.World.Dilemmas;
            if (book.PendingFeastId == 0) return;
            int feastId = book.PendingFeastId;
            int optionIndex = book.PendingFeastOption;
            book.PendingFeastId = 0;
            book.PendingFeastOption = -1;
            if (!book.TryGetDilemma(feastId, out Dilemma dilemma)) return;
            DilemmaOption option = DilemmaRules.OptionOf(ctx.Data, dilemma, optionIndex);
            if (option == null) return;

            ctx.Log.Write(SimLogLevel.Info, "dilemma #{0} feast: {1} people in tavern", dilemma.Id, there.Count);
            foreach (DilemmaEffect effect in option.Effects)
            {
                if (effect.Target != DilemmaTarget.AllInTavern) continue;
                if (effect.Kind == DilemmaEffectKind.Relation)
                {
                    for (int i = 0; i < there.Count; i++)
                    {
                        for (int j = i + 1; j < there.Count; j++) RelationService.Change(ctx, there[i].Id, there[j].Id, effect.Value);
                    }
                    continue;
                }
                foreach (Adventurer adventurer in there) ApplyToPerson(ctx, dilemma, effect, adventurer, 0);
            }
        }

        // ---------- Эффекты ----------

        private static void Apply(SimContext ctx, Dilemma dilemma, DilemmaEffect effect)
        {
            switch (effect.Kind)
            {
                case DilemmaEffectKind.Treasury:
                    int coins = WalletService.Coins(Math.Abs(effect.Value));
                    if (effect.Value < 0f) TreasuryService.Debit(ctx, LedgerCategories.Dilemmas, coins, dilemma.DefinitionId, dilemma.SubjectId);
                    else TreasuryService.Credit(ctx, LedgerCategories.Fines, coins, dilemma.DefinitionId, dilemma.SubjectId);
                    return;
                case DilemmaEffectKind.Relation:
                    ApplyRelation(ctx, dilemma, effect);
                    return;
                case DilemmaEffectKind.ReturnSkimmedLoot:
                    ReturnSkimmedLoot(ctx, dilemma);
                    return;
                case DilemmaEffectKind.ContentmentIfRecentDeath:
                    if (!DilemmaRules.HadRecentDeath(ctx.World, ctx.Calendar, dilemma.ArrivedAtHours, ctx.Data.Balance.Dilemmas.FeastRecentDeathDays)) return;
                    foreach (Adventurer adventurer in new List<Adventurer>(ctx.World.Adventurers.Active))
                        StateService.ChangeContentment(ctx, adventurer, effect.Value);
                    return;
            }

            List<Adventurer> targets = Targets(ctx, dilemma, effect.Target);
            foreach (Adventurer adventurer in targets) ApplyToPerson(ctx, dilemma, effect, adventurer, OtherOf(dilemma, adventurer, effect.Target));
        }

        private static void ApplyToPerson(SimContext ctx, Dilemma dilemma, DilemmaEffect effect, Adventurer adventurer, int otherId)
        {
            long now = ctx.World.Time.TotalHours;
            switch (effect.Kind)
            {
                case DilemmaEffectKind.Contentment:
                    StateService.ChangeContentment(ctx, adventurer, effect.Value);
                    break;
                case DilemmaEffectKind.Loyalty:
                    StateService.ChangeLoyalty(ctx, adventurer, effect.Value);
                    break;
                case DilemmaEffectKind.Stress:
                    StateService.AddStress(ctx, adventurer, effect.Value);
                    break;
                case DilemmaEffectKind.Fatigue:
                    StateService.AddFatigue(ctx, adventurer, effect.Value);
                    break;
                case DilemmaEffectKind.WalletToTreasury:
                    int fine = WalletService.Take(adventurer, WalletService.Share(adventurer.State.Wallet, effect.Value));
                    TreasuryService.Credit(ctx, LedgerCategories.Fines, fine, dilemma.DefinitionId, adventurer.Id);
                    break;
                case DilemmaEffectKind.GiveLoan:
                    int loan = WalletService.Coins(dilemma.Amount * effect.Value);
                    if (loan > 0 && TreasuryService.Debit(ctx, LedgerCategories.Loans, loan, dilemma.DefinitionId, adventurer.Id))
                        WalletService.TakeLoan(ctx, adventurer, loan);
                    break;
                case DilemmaEffectKind.PayWeekAndTreatment:
                    int pay = DilemmaRules.WeekAndTreatment(ctx.World, ctx.Data, adventurer);
                    if (pay > 0 && TreasuryService.Debit(ctx, LedgerCategories.Dilemmas, pay, dilemma.DefinitionId, adventurer.Id))
                        WalletService.Give(adventurer, pay);
                    break;
                case DilemmaEffectKind.RevealAxisPole:
                    RevealService.TryRevealAxisByDilemma(ctx, adventurer, effect.Axis, effect.Pole);
                    break;
                case DilemmaEffectKind.RevealTrait:
                    if (effect.Trait != null) RevealService.TryRevealTraitByDilemma(ctx, adventurer, effect.Trait.Id);
                    break;
                case DilemmaEffectKind.SetMemoryFlag:
                    adventurer.Memory.Set(effect.Flag, now, otherId);
                    break;
                case DilemmaEffectKind.Expel:
                    AdventurerLifecycle.Retire(ctx, adventurer, LeaveReason.Expelled);
                    break;
                case DilemmaEffectKind.AllowQuestWhileWounded:
                    if (!adventurer.State.HasHeavyWound()) break;
                    adventurer.State.WoundedQuestProfile = effect.Value;
                    adventurer.State.InInfirmary = false;
                    break;
                case DilemmaEffectKind.AllowSameParty:
                    if (otherId == 0) break;
                    adventurer.Memory.Remove(MemoryFlag.LoversSeparated);
                    adventurer.Memory.Set(MemoryFlag.LoversTogether, now, otherId, effect.Value);
                    break;
                case DilemmaEffectKind.ForbidSameParty:
                    if (otherId == 0) break;
                    adventurer.Memory.Remove(MemoryFlag.LoversTogether);
                    adventurer.Memory.Set(MemoryFlag.LoversSeparated, now, otherId);
                    if (ctx.World.Adventurers.TryGetActive(otherId, out Adventurer other)) PartyService.Separate(ctx, adventurer, other);
                    break;
            }
        }

        /// <summary>
        /// Отношения: пары «обратился — второй участник», брошенных к беглецу, всех в гильдии попарно.
        /// </summary>
        private static void ApplyRelation(SimContext ctx, Dilemma dilemma, DilemmaEffect effect)
        {
            switch (effect.Target)
            {
                case DilemmaTarget.Subject:
                case DilemmaTarget.Partner:
                case DilemmaTarget.SubjectAndPartner:
                    if (dilemma.SubjectId != 0 && dilemma.PartnerId != 0) RelationService.Change(ctx, dilemma.SubjectId, dilemma.PartnerId, effect.Value);
                    return;
                case DilemmaTarget.Abandoned:
                    foreach (Adventurer abandoned in Targets(ctx, dilemma, DilemmaTarget.Abandoned))
                        RelationService.Change(ctx, abandoned.Id, dilemma.SubjectId, effect.Value);
                    return;
                case DilemmaTarget.AllAdventurers:
                    IReadOnlyList<Adventurer> everyone = ctx.World.Adventurers.Active;
                    for (int i = 0; i < everyone.Count; i++)
                    {
                        for (int j = i + 1; j < everyone.Count; j++) RelationService.Change(ctx, everyone[i].Id, everyone[j].Id, effect.Value);
                    }
                    return;
            }
        }

        /// <summary>
        /// Утаенное возвращается в общий делёж: если на задании заметили, что обратившийся утаил часть трофеев, он отдаёт из кошелька
        /// сколько может (не больше утаенного), и это делится поровну между остальными вернувшимися, кто ещё в гильдии.
        /// </summary>
        private static void ReturnSkimmedLoot(SimContext ctx, Dilemma dilemma)
        {
            if (!ctx.World.Quests.TryGetRun(dilemma.QuestRunId, out QuestRun run)) return;
            if (!run.SkimCaught || run.SkimmerId != dilemma.SubjectId) return;
            if (!ctx.World.Adventurers.TryGetActive(dilemma.SubjectId, out Adventurer skimmer)) return;

            var others = new List<Adventurer>();
            foreach (int id in run.Departed)
            {
                if (id == skimmer.Id || QuestParty.Contains(run.Fled, id) || QuestParty.Contains(run.Dead, id)) continue;
                if (ctx.World.Adventurers.TryGetActive(id, out Adventurer other)) others.Add(other);
            }
            if (others.Count == 0) return;

            int returned = WalletService.Take(skimmer, run.SkimmedAmount);
            int[] shares = QuestSettlement.Split(returned, others, equal: true);
            for (int i = 0; i < others.Count; i++)
            {
                if (shares[i] > 0) WalletService.ReceiveIncome(ctx, others[i], shares[i], IncomeKind.Loot);
            }
            ctx.Log.Write(SimLogLevel.Debug, "dilemma #{0}: #{1} returned {2} skimmed coins", dilemma.Id, skimmer.Id, returned);
        }

        /// <summary>Кого касается эффект: только те, кто сейчас в гильдии.</summary>
        private static List<Adventurer> Targets(SimContext ctx, Dilemma dilemma, DilemmaTarget target)
        {
            var targets = new List<Adventurer>();
            switch (target)
            {
                case DilemmaTarget.Subject:
                    AddActive(ctx, targets, dilemma.SubjectId);
                    break;
                case DilemmaTarget.Partner:
                    AddActive(ctx, targets, dilemma.PartnerId);
                    break;
                case DilemmaTarget.SubjectAndPartner:
                    AddActive(ctx, targets, dilemma.SubjectId);
                    AddActive(ctx, targets, dilemma.PartnerId);
                    break;
                case DilemmaTarget.Abandoned:
                    foreach (int id in dilemma.Abandoned) AddActive(ctx, targets, id);
                    break;
                case DilemmaTarget.AllAdventurers:
                    targets.AddRange(ctx.World.Adventurers.Active);
                    break;
            }
            return targets;
        }

        private static void AddActive(SimContext ctx, List<Adventurer> list, int id)
        {
            if (id != 0 && ctx.World.Adventurers.TryGetActive(id, out Adventurer adventurer)) list.Add(adventurer);
        }

        /// <summary>С кем связан флаг памяти: у пары «обратился — второй участник» — другой из пары.</summary>
        private static int OtherOf(Dilemma dilemma, Adventurer adventurer, DilemmaTarget target)
        {
            if (target != DilemmaTarget.SubjectAndPartner) return 0;
            return adventurer.Id == dilemma.SubjectId ? dilemma.PartnerId : dilemma.SubjectId;
        }

        private static int[] Participants(Dilemma dilemma)
        {
            if (dilemma.SubjectId == 0) return Array.Empty<int>();
            return dilemma.PartnerId != 0 ? new[] { dilemma.SubjectId, dilemma.PartnerId } : new[] { dilemma.SubjectId };
        }

        /// <summary>Данные события обращения: id, дилемма, триггер; у обращения персонала — сотрудник для <c>{имя}</c>.</summary>
        private static void Describe(SimContext ctx, SimEvent simEvent, Dilemma dilemma)
        {
            simEvent.With("dilemma", dilemma.Id).With("definition", dilemma.DefinitionId).With("trigger", dilemma.Trigger);
            if (dilemma.StaffId == 0) return;
            foreach (StaffMember member in ctx.World.Staff.Members)
            {
                if (member.Id != dilemma.StaffId) continue;
                simEvent.With("staffId", member.Id).With("staff", TextValue.Person(member.Name, ctx.Data.NameForms(member.Name), member.Gender));
            }
        }

        /// <summary>Строка лога, что человек подал обращение (как решение модели решений, но запущенное триггером).</summary>
        private static void LogRequest(SimContext ctx, Dilemma dilemma)
        {
            SimLogger log = ctx.Log;
            if (!log.IsOn(SimLogLevel.Info)) return;
            var line = log.Begin(SimLogLevel.Info);
            line.Append("decide ");
            if (ctx.World.Adventurers.TryGetActive(dilemma.SubjectId, out Adventurer subject)) AdventurerLog.AppendName(line, subject);
            else line.Append("staff #").Append(dilemma.StaffId);
            line.Append(" request: ").Append(dilemma.Trigger).Append(" #").Append(dilemma.Id);
            if (dilemma.PartnerId != 0) line.Append(" with #").Append(dilemma.PartnerId);
            log.Commit();
        }
    }
}
