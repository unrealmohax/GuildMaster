using System;
using System.Collections.Generic;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Шаг 4 такта: задания. Каждый час, по порядку:
    /// <list type="number">
    /// <item>в 00:00 — забыть задания, законченные раньше <c>questFeedKeepDays</c> дней (с их лентой); ссоры в постоянных
    /// группах (<see cref="PartyService.CheckQuarrels"/>);</item>
    /// <item>люди, идущие домой одни, — вернулись (повернувший назад, беглец) или исчезли (беглец);</item>
    /// <item>в начале утра — задания на повышение тем, кто готов (<see cref="GuildRanks.IsReadyForPromotion"/>);</item>
    /// <item>выход: кто взял заказ решением в прошлом часу — выходит (<see cref="AdventurerState.PlannedOrderId"/>), группа —
    /// вместе;</item>
    /// <item>каждое идущее задание — ход на час: ночлег, путь (событие в пути, находка), раунд, возвращение.</item>
    /// </list>
    /// Раунды — <see cref="QuestRounds"/>, события в пути и находки — <see cref="QuestEncounters"/>, решения группы —
    /// <see cref="QuestChoices"/>, итоги и выплаты — <see cref="QuestSettlement"/>. Случайность — свой поток.
    /// Реестр только из чисел заданий не даёт.
    /// </summary>
    public sealed class QuestSystem : ISimSystem
    {
        public string Name => nameof(QuestSystem);

        public void Tick(SimContext ctx)
        {
            if (!ctx.Data.HasDefinitions) return;

            GameTime time = ctx.World.Time;
            if (time.Hour == 0)
            {
                ForgetOld(ctx);
                PartyService.CheckQuarrels(ctx);
            }
            Stragglers(ctx);
            if (time.Hour == ctx.Data.Balance.Time.MorningHour) PromotionOrders.Offer(ctx);
            Departures(ctx);

            foreach (QuestRun run in new List<QuestRun>(ctx.World.Quests.Active)) Advance(ctx, run);
        }

        private static void ForgetOld(SimContext ctx)
        {
            long keep = ctx.Calendar.DaysToHours(ctx.Data.Balance.Feed.QuestFeedKeepDays);
            ctx.World.Quests.ForgetFinished(ctx.World.Time.TotalHours - keep);
        }

        // ---------- Идущие домой одни ----------

        private static void Stragglers(SimContext ctx)
        {
            long now = ctx.World.Time.TotalHours;
            QuestBook book = ctx.World.Quests;
            foreach (Straggler straggler in new List<Straggler>(book.Stragglers))
            {
                if (!ctx.World.Adventurers.TryGetActive(straggler.AdventurerId, out Adventurer adventurer))
                {
                    book.RemoveStraggler(straggler);
                    continue;
                }
                if (now < straggler.DueAtHours)
                {
                    SetActivity(ctx, adventurer, Activity.OnQuestTravel);
                    continue;
                }

                book.RemoveStraggler(straggler);
                LeaveQuest(adventurer);
                if (straggler.Kind == StragglerKind.TurnedBack) continue;

                if (straggler.Returns)
                {
                    adventurer.IsDeserter = true;
                    ctx.Events.Publish(SimEventType.DeserterReturned, EventImportance.Important, adventurer.Id)
                        .With("quest", straggler.QuestRunId);
                }
                else
                {
                    AdventurerLifecycle.Retire(ctx, adventurer, LeaveReason.Disappeared);
                    ctx.Events.Publish(SimEventType.AdventurerDisappeared, EventImportance.Important, adventurer.Id)
                        .With("quest", straggler.QuestRunId);
                }
            }
        }

        /// <summary>Человек идёт домой один и дойдёт в <paramref name="dueAtHours"/>; беглец — вернётся или исчезнет.</summary>
        internal static void AddStraggler(SimContext ctx, QuestRun run, Adventurer adventurer, StragglerKind kind, long dueAtHours, bool returns)
        {
            AdventurerState state = adventurer.State;
            state.QuestRunId = 0;
            state.QuestParty = PartyContext.Solo;
            state.Activity = Activity.OnQuestTravel;
            adventurer.PartyId = 0;
            ctx.World.Quests.AddStraggler(new Straggler(adventurer.Id, run.Id, kind, dueAtHours, returns));
        }

        /// <summary>Человек больше не на задании: занятие — отдых, решает сам со следующего часа.</summary>
        internal static void LeaveQuest(Adventurer adventurer)
        {
            AdventurerState state = adventurer.State;
            state.QuestRunId = 0;
            state.QuestParty = PartyContext.None;
            state.Activity = Activity.Resting;
            state.PlannedActivity = null;
            adventurer.PartyId = 0;
        }

        // ---------- Выход ----------

        private static void Departures(SimContext ctx)
        {
            foreach (Adventurer adventurer in new List<Adventurer>(ctx.World.Adventurers.Active))
            {
                if (adventurer.State.PlannedOrderId == 0) continue;
                int orderId = adventurer.State.PlannedOrderId;
                ctx.World.Parties.TryGetParty(adventurer.PartyId, out Party party);
                List<Adventurer> going = party != null && party.OrderId == orderId ? Going(ctx, party, orderId) : new List<Adventurer> { adventurer };
                foreach (Adventurer member in going) member.State.PlannedOrderId = 0;
                if (!ctx.World.Orders.TryGetOrder(orderId, out Order order) || order.Status != OrderStatus.Taken)
                {
                    PartyService.Release(ctx, party, going);
                    continue;
                }

                var ready = new List<Adventurer>(going.Count);
                foreach (Adventurer member in going)
                {
                    AdventurerState state = member.State;
                    if (StateRules.CanTakeQuests(state, ctx.Data.Balance.State) && ctx.Rhythm.CanStartQuest(ctx.World.Time.Hour) && !state.IsOnQuest())
                    {
                        ready.Add(member);
                        continue;
                    }
                    ctx.Log.Write(SimLogLevel.Debug, "quest cancelled: #{0} {1} cannot depart with order #{2}", member.Id, member.Name, order.Id);
                }
                if (ready.Count == 0)
                {
                    order.Status = OrderStatus.OnBoard;
                    order.TakenBy = 0;
                    ctx.World.Orders.ReturnToOpen(order);
                    PartyService.Release(ctx, party, going);
                    continue;
                }
                if (ready.Count < going.Count) PartyService.Release(ctx, null, going.FindAll(m => !ready.Contains(m)));
                Depart(ctx, order, ready, party);
            }
        }

        /// <summary>Кто идёт с группой: взявший заказ первым (инициатор или лидер), затем остальные по порядку в гильдии.</summary>
        private static List<Adventurer> Going(SimContext ctx, Party party, int orderId)
        {
            var going = new List<Adventurer>();
            if (ctx.World.Adventurers.TryGetActive(party.InitiatorId, out Adventurer first) && first.PartyId == party.Id
                && first.State.PlannedOrderId == orderId)
                going.Add(first);
            foreach (Adventurer member in ctx.World.Adventurers.Active)
            {
                if (member.PartyId == party.Id && member.State.PlannedOrderId == orderId && !going.Contains(member)) going.Add(member);
            }
            return going;
        }

        /// <summary>
        /// Начать задание по взятому заказу этой группой: заказ — «идёт», у людей — занятие задания, путь туда
        /// (длительность, событие в пути, находка — броски сразу, в этом порядке), строка «Выход». <paramref name="group"/> —
        /// группа, которая собралась под заказ (идёт один — задание без группы). Соперники в одной группе раскрываются.
        /// </summary>
        internal static QuestRun Depart(SimContext ctx, Order order, IReadOnlyList<Adventurer> party, Party group = null)
        {
            QuestBook book = ctx.World.Quests;
            var run = new QuestRun(book.NextId(), order, ctx.World.Time.TotalHours);
            order.Status = OrderStatus.InProgress;
            order.QuestRunId = run.Id;
            PartyService.Attach(ctx, run, group, party);

            PartyContext context = QuestParty.ContextOf(party.Count);
            foreach (Adventurer member in party)
            {
                run.AddMember(member.Id);
                AdventurerState state = member.State;
                state.QuestRunId = run.Id;
                state.QuestParty = context;
                state.Activity = Activity.OnQuestTravel;
                state.PlannedActivity = null;
                member.IdleOrderDays = 0;
            }
            book.AddActive(run);

            foreach (Adventurer member in party) NoteOverreach(ctx, run, member, order);
            if (party.Count > 1) RevealRivals(ctx, party);
            StartLeg(ctx, run, QuestPhase.TravelOut, eventChanceMultiplier: 1f);
            if (!run.IsPromotion) QuestEncounters.PlanDiscovery(ctx, run);

            QuestParty.Publish(ctx, SimEventType.QuestDeparted, run, EventImportance.Normal, Ids(party));
            return run;
        }

        /// <summary>Соперник, чей соперник-партнёр идёт в той же группе, — первое совместное задание раскрывает черту.</summary>
        private static void RevealRivals(SimContext ctx, IReadOnlyList<Adventurer> party)
        {
            foreach (Adventurer member in party)
            {
                TraitInstance rival = TraitRules.FindWithHook(member, TraitHook.RivalPartner, ctx.Data);
                if (rival == null || rival.Revealed) continue;
                foreach (Adventurer other in party)
                {
                    if (other.Id != rival.PartnerId) continue;
                    RevealService.TryRevealTrait(ctx, member, rival.TraitId, RevealTrigger.RivalFirstClash);
                    break;
                }
            }
        }

        /// <summary>
        /// Хвастун: человек, который видит перекрытие не меньше <c>braggartRevealPerceivedOverlap</c>, а на деле у него меньше
        /// <c>braggartRevealRealOverlap</c>, переоценил себя — раскроется, если задание не удастся. В лог — оба перекрытия.
        /// </summary>
        private static void NoteOverreach(SimContext ctx, QuestRun run, Adventurer member, Order order)
        {
            float perceived = QuestMath.PerceivedSoloOverlap(member, order, ctx.Data);
            float real = QuestMath.RealSoloOverlap(member, order, ctx.Data);
            ctx.Log.Write(SimLogLevel.Debug, "quest #{0} depart #{1}: perceived={2} real={3}", run.Id, member.Id,
                AdventurerLog.Number(perceived), AdventurerLog.Number(real));

            TraitsBalance traits = ctx.Data.Balance.Traits;
            if (perceived >= traits.BraggartRevealPerceivedOverlap && real < traits.BraggartRevealRealOverlap) run.AddOverreached(member.Id);
        }

        /// <summary>
        /// Начать отрезок пути: длительность (близко — <c>nearHours</c>, далеко — бросок <c>farHours</c>), бросок события
        /// в пути (шанс × <paramref name="eventChanceMultiplier"/>) и его ходовой час. На экзамене событий нет.
        /// </summary>
        internal static void StartLeg(SimContext ctx, QuestRun run, QuestPhase phase, float eventChanceMultiplier)
        {
            TravelBalance travel = ctx.Data.Balance.Travel;
            int hours = run.Distance == OrderDistance.Far ? ctx.Rng.RangeInclusive(travel.FarHours.Min, travel.FarHours.Max) : travel.NearHours;
            run.Phase = phase;
            run.PhaseHoursLeft = Math.Max(1, hours);
            run.LegHoursDone = 0;
            run.TravelEventHour = 0;
            if (run.IsPromotion) return;

            float chance = (run.Distance == OrderDistance.Far ? travel.EventChanceFar : travel.EventChanceNear) * eventChanceMultiplier;
            if (ctx.RollChance(Math.Min(1f, chance), "travel-event", null, "quest", run.Id))
                run.TravelEventHour = ctx.Rng.RangeInclusive(1, run.PhaseHoursLeft);
        }

        // ---------- Ход задания ----------

        private static void Advance(SimContext ctx, QuestRun run)
        {
            List<Adventurer> present = QuestParty.Present(ctx.World, run);
            if (present.Count == 0)
            {
                QuestSettlement.Finish(ctx, run);
                return;
            }

            if (ctx.Rhythm.IsCampHour(ctx.World.Time.Hour))
            {
                foreach (Adventurer member in present) SetActivity(ctx, member, Activity.OnQuestCamp);
                if (!run.Camping)
                {
                    run.Camping = true;
                    if (run.Phase != QuestPhase.AtSite) QuestParty.Publish(ctx, SimEventType.TravelProgress, run, EventImportance.Normal, Ids(present));
                }
                return;
            }
            if (run.Camping)
            {
                run.Camping = false;
                QuestParty.Publish(ctx, SimEventType.QuestCamp, run, EventImportance.Normal, Ids(present));
            }

            switch (run.Phase)
            {
                case QuestPhase.TravelOut:
                case QuestPhase.TravelBack:
                    Travel(ctx, run, present);
                    break;
                case QuestPhase.AtSite:
                    foreach (Adventurer member in present) SetActivity(ctx, member, Activity.OnQuestRound);
                    run.PhaseHoursLeft--;
                    if (run.PhaseHoursLeft <= 0) QuestRounds.Resolve(ctx, run);
                    break;
            }
        }

        private static void Travel(SimContext ctx, QuestRun run, List<Adventurer> present)
        {
            foreach (Adventurer member in present) SetActivity(ctx, member, Activity.OnQuestTravel);
            run.LegHoursDone++;
            run.PhaseHoursLeft--;
            if (run.Phase == QuestPhase.TravelOut) run.OutHoursDone = run.LegHoursDone;

            if (run.TravelEventHour == run.LegHoursDone)
            {
                QuestEncounters.TravelEvent(ctx, run);
                if (!IsActive(ctx, run)) return;
            }
            if (run.Phase == QuestPhase.TravelOut && run.Discovery != null && run.Discovery.State == DiscoveryState.None
                && run.Discovery.AtHours == run.LegHoursDone)
            {
                QuestEncounters.Discover(ctx, run);
                if (!IsActive(ctx, run) || run.Phase != QuestPhase.TravelOut) return;
            }

            if (run.PhaseHoursLeft > 0) return;
            if (run.Phase == QuestPhase.TravelOut) Arrive(ctx, run);
            else QuestSettlement.Finish(ctx, run);
        }

        private static void Arrive(SimContext ctx, QuestRun run)
        {
            run.Phase = QuestPhase.AtSite;
            run.Round = 1;
            run.PhaseHoursLeft = RoundHours(ctx, run);
        }

        /// <summary>Длительность раунда — у типа задания (не меньше часа).</summary>
        internal static int RoundHours(SimContext ctx, QuestRun run) =>
            ctx.Data.TryGet(run.TypeId, out QuestTypeDefinition type) ? Math.Max(1, type.RoundHours) : 1;

        /// <summary>
        /// Повернуть к дому: выполнили или отступили. Путь назад — как туда, со своим событием; после отступления шанс
        /// события × <c>eventChanceAfterRetreatMultiplier</c>. Строка «обратный путь» (несут раненых — своя).
        /// </summary>
        internal static void StartReturn(SimContext ctx, QuestRun run)
        {
            float multiplier = run.Retreated ? ctx.Data.Balance.Travel.EventChanceAfterRetreatMultiplier : 1f;
            StartLeg(ctx, run, QuestPhase.TravelBack, multiplier);
            List<Adventurer> present = QuestParty.Present(ctx.World, run);
            bool wounded = false;
            foreach (Adventurer member in present) wounded |= member.State.Conditions.Count > 0;
            QuestParty.Publish(ctx, SimEventType.ReturnTrip, run, EventImportance.Normal, Ids(present))
                .With("kind", wounded ? "hard" : "normal");
        }

        internal static bool IsActive(SimContext ctx, QuestRun run) => run.Phase != QuestPhase.Returned && run.Members.Count > 0;

        private static void SetActivity(SimContext ctx, Adventurer adventurer, Activity activity)
        {
            adventurer.State.Activity = activity;
            adventurer.State.PlannedActivity = null;
        }

        internal static int[] Ids(IReadOnlyList<Adventurer> people)
        {
            var ids = new int[people.Count];
            for (int i = 0; i < ids.Length; i++) ids[i] = people[i].Id;
            return ids;
        }
    }
}
