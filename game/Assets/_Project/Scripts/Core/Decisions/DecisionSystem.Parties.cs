using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Группы в модели решений. Всё решается в одном часу, выход — в следующем, как у заказа в одиночку.
    /// <list type="bullet">
    /// <item><b>Собрать группу</b> (<see cref="DecisionActionKind.SeekParty"/>): утром и когда освободился к каждому заказу,
    /// который можно взять, — ещё вариант «с группой». Предполагаемая группа (<see cref="PartyMath.Presume"/>): он и лучшие
    /// свободные кандидаты по оценке напарника, пока это повышает его ценность. Вариант есть, если в ней хоть кто-то, кроме
    /// него.</item>
    /// <item>Выбран — заказ снят с доски, инициатор зовёт по одному лучшего по оценке напарника, пока новый человек повышает его
    /// ценность и в группе меньше <c>maxPartySize</c>; отказал — следующего. Собрал не меньше приемлемого минимума
    /// (<see cref="PresumedParty.MinimumAbove"/> лучшего другого варианта) — группа идёт. Нет — идёт один, если это лучше отдыха,
    /// иначе заказ снова на доске, а Командный, отвергший одиночку, раскрывается. Распоряжение «только группой» на ранг заказа:
    /// минимум — двое, одному идти нельзя (отказ без раскрытия).</item>
    /// <item><b>Приглашение</b>: зовут любого свободного, кто может брать задания, не взял заказ и не в собираемой группе (решение
    /// этого часа пересматривается). Он сравнивает «этот заказ с этой группой» (<see cref="DecisionActionKind.JoinParty"/>)
    /// со своими вариантами точки — обычный выбор с броском. Отказ — событие с причиной (<see cref="PartyReasons"/>); взял
    /// вместо этого заказ один — раскрывается Одиночка.</item>
    /// <item><b>Постоянная группа</b> решает утром раньше всех: лидер (<see cref="QuestParty.Leader"/> среди свободных членов)
    /// сравнивает заказы по средней по членам ценности «всей группой» и среднюю ценность отдыха. Выбран заказ — каждый член
    /// решает как приглашённый (может выйти на день), затем лидер зовёт гостей, пока растёт средняя ценность. Идут, если их
    /// хотя бы двое.</item>
    /// </list>
    /// Лог: <c>Info</c> — сбор, приглашения (<c>decide … invite #N</c>), итог; <c>Debug</c> — почему остановился; <c>Trace</c> —
    /// предполагаемые группы и оценки напарников.
    /// </summary>
    public sealed partial class DecisionSystem
    {
        /// <summary>Предполагаемые группы решения, которое идёт сейчас: заказ → группа.</summary>
        private readonly Dictionary<int, PresumedParty> presumed = new Dictionary<int, PresumedParty>();

        private static DecisionPoint MorningPoint
        {
            get
            {
                foreach (DecisionPoint point in DecisionPoints.Default)
                {
                    if (point.Kind == DecisionPointKind.Morning) return point;
                }
                throw new InvalidOperationException("No morning decision point");
            }
        }

        // ---------- Кого можно звать ----------

        /// <summary>Свободен, может брать задания, не взял заказ и не в собираемой группе.</summary>
        private static bool CanInvite(SimContext ctx, Adventurer person, long now)
        {
            AdventurerState state = person.State;
            return DecisionPoints.CanDecide(state, now) && StateRules.CanTakeQuests(state, ctx.Data.Balance.State)
                && state.PlannedOrderId == 0 && person.PartyId == 0;
        }

        /// <summary>
        /// Кого можно позвать на этот заказ: свободные нужного ранга, кроме уже позванных, тех, кто в группе, и тех, кому гильдия
        /// запретила ходить в одной группе с инициатором или с кем-то из группы.
        /// </summary>
        private static List<Adventurer> Invitable(SimContext ctx, Adventurer initiator, Order order, ICollection<int> exclude,
            IReadOnlyList<Adventurer> group)
        {
            long now = ctx.World.Time.TotalHours;
            var pool = new List<Adventurer>();
            foreach (Adventurer person in ctx.World.Adventurers.Active)
            {
                if (person.Id == initiator.Id || (exclude != null && exclude.Contains(person.Id)) || Contains(group, person)) continue;
                if (!CanInvite(ctx, person, now) || !GuildRanks.CanTakeOrder(person, order.Rank)) continue;
                if (IsSeparated(person, initiator, group)) continue;
                pool.Add(person);
            }
            return pool;
        }

        private static bool IsSeparated(Adventurer person, Adventurer initiator, IReadOnlyList<Adventurer> group)
        {
            if (person.Memory.Entries.Count == 0 && initiator.Memory.Entries.Count == 0 && (group == null || group.Count == 0)) return false;
            if (DilemmaRules.AreSeparated(person, initiator)) return true;
            if (group == null) return false;
            foreach (Adventurer member in group)
            {
                if (DilemmaRules.AreSeparated(person, member)) return true;
            }
            return false;
        }

        private static bool Contains(IReadOnlyList<Adventurer> group, Adventurer person)
        {
            if (group == null) return false;
            foreach (Adventurer member in group)
            {
                if (member.Id == person.Id) return true;
            }
            return false;
        }

        // ---------- Вариант «собрать группу» ----------

        /// <summary>К каждому разрешённому заказу (не экзамену) — вариант «собрать группу», если предполагаемая группа не из одного.</summary>
        private void AddSeekParty(DecisionScope scope, Adventurer adventurer, List<DecisionAction> actions)
        {
            SimContext ctx = scope.Ctx;
            presumed.Clear();
            DataRegistry data = ctx.Data;
            RelationBook relations = ctx.World.Relations;
            float commission = ctx.World.Treasury.Commission;
            List<Adventurer> pool = null;
            int count = actions.Count;
            for (int i = 0; i < count; i++)
            {
                DecisionAction take = actions[i];
                if (take.Kind != DecisionActionKind.TakeOrder || !ctx.World.Orders.TryGetOrder(take.OrderId, out Order order) || order.IsPromotion)
                    continue;
                pool = pool ?? Invitable(ctx, adventurer, order, null, null);
                List<Adventurer> candidates = pool.FindAll(c => GuildRanks.CanTakeOrder(c, order.Rank));
                if (candidates.Count == 0) continue;

                var lens = new PartyLens(adventurer, order, false, commission, relations, data, scope.Profiles, scope.SafetyMultiplier);
                PresumedParty party = PartyMath.Presume(lens, candidates);
                if (party.Members.Count < 2) continue;
                presumed[order.Id] = party;
                actions.Add(new DecisionAction(DecisionActionKind.SeekParty, Activity.Resting, false,
                    (sc, a, scores) => lens.Scores(party.Members, scores), order.Id));
                if (ctx.Log.IsOn(SimLogLevel.Trace)) WritePresumed(ctx.Log, adventurer, order, party);
            }
        }

        // ---------- Сбор группы под задание ----------

        private void Gather(DecisionScope scope, Adventurer initiator, DecisionPoint point, Option chosen, List<Option> all,
            List<DecisionAction> allowedActions)
        {
            SimContext ctx = scope.Ctx;
            DataRegistry data = ctx.Data;
            if (!presumed.TryGetValue(chosen.Action.OrderId, out PresumedParty plan) || !ctx.World.Orders.TryGetOpen(chosen.Action.OrderId, out Order order))
                return;

            float bestOther = float.NegativeInfinity;
            float solo = float.NegativeInfinity;
            float rest = float.NegativeInfinity;
            foreach (Option option in all)
            {
                if (option.Action == chosen.Action) continue;
                bestOther = Math.Max(bestOther, option.Value);
                if (option.Action.Kind == DecisionActionKind.TakeOrder && option.Action.OrderId == order.Id) solo = option.Value;
                if (option.Action.Kind == DecisionActionKind.Rest) rest = option.Value;
            }
            bool soloBanned = DecreeRules.IsSoloBanned(ctx.World, data, order);
            int minimum = plan.MinimumAbove(bestOther);
            if (soloBanned) minimum = Math.Max(minimum, 2);

            OrderChoice.Reserve(ctx, order, initiator);
            Party party = PartyService.StartGathering(ctx, initiator, order);
            if (ctx.Log.IsOn(SimLogLevel.Info)) WriteSeek(ctx.Log, party, initiator, order, plan, minimum, bestOther);

            var group = new List<Adventurer> { initiator };
            var lens = new PartyLens(initiator, order, false, ctx.World.Treasury.Commission, ctx.World.Relations, data, scope.Profiles,
                scope.SafetyMultiplier);
            InviteWhileValueGrows(scope, lens, party, group, false, point, null, lens.Value);

            if (group.Count >= minimum)
            {
                OrderChoice.Commit(ctx, initiator, order, allowedActions, group.Count);
                PublishGathered(ctx, party, order, group);
                return;
            }

            var invited = group.GetRange(1, group.Count - 1);
            PartyService.Release(ctx, party, invited);
            foreach (Adventurer person in invited) person.State.PlannedActivity = null;
            initiator.PartyId = 0;
            ctx.World.Parties.Remove(party);

            bool goesAlone = !soloBanned && solo > rest;
            ctx.Log.Write(SimLogLevel.Info, "party #{0} not gathered: {1} of {2}, {3}", party.Id, group.Count, minimum, goesAlone ? "goes alone" : "gives up");
            if (goesAlone)
            {
                OrderChoice.Commit(ctx, initiator, order, allowedActions, 1);
            }
            else
            {
                OrderChoice.Release(ctx, order);
                if (!soloBanned) RevealService.TryRevealAxis(ctx, initiator, AxisId.People, RevealTrigger.TeamRefusedSolo);
            }
            SimEvent notGathered = ctx.Events.Publish(SimEventType.PartyNotGathered, EventImportance.Normal, initiator.Id)
                .With("order", order.Id).With("solo", goesAlone);
            if (soloBanned) notGathered.With("soloBanned", true);
        }

        /// <summary>
        /// Звать по одному лучшего по оценке напарника (глазами <paramref name="leader"/>), пока новый человек повышает
        /// <paramref name="value"/> группы и в ней меньше <c>maxPartySize</c>. Отказал — следующего.
        /// </summary>
        private void InviteWhileValueGrows(DecisionScope scope, PartyLens lens, Party party, List<Adventurer> group,
            bool permanent, DecisionPoint point, ICollection<int> exclude, Func<List<Adventurer>, float> value)
        {
            SimContext ctx = scope.Ctx;
            Adventurer leader = lens.Viewer;
            Order order = lens.Order;
            DataRegistry data = ctx.Data;
            var refused = exclude != null ? new HashSet<int>(exclude) : new HashSet<int>();
            float current = value(group);
            Action<Adventurer, float, float> trace = null;
            if (ctx.Log.IsOn(SimLogLevel.Trace)) trace = (candidate, gain, score) => WritePartner(ctx.Log, leader, order, candidate, gain, score);

            while (group.Count < data.Balance.Rounds.MaxPartySize)
            {
                List<Adventurer> pool = Invitable(ctx, leader, order, refused, group);
                if (pool.Count == 0) break;
                Adventurer candidate = PartyMath.BestPartner(lens, group, pool, trace);
                group.Add(candidate);
                float next = value(group);
                if (next <= current)
                {
                    group.RemoveAt(group.Count - 1);
                    ctx.Log.Write(SimLogLevel.Debug, "party #{0} stops: #{1} would not add value ({2} <= {3})", party.Id, candidate.Id,
                        AdventurerLog.Number(next), AdventurerLog.Number(current));
                    break;
                }
                if (Invite(scope, candidate, leader, party, order, group, permanent, point))
                {
                    current = next;
                    continue;
                }
                group.RemoveAt(group.Count - 1);
                refused.Add(candidate.Id);
            }
        }

        /// <summary>
        /// Приглашение: <paramref name="invitee"/> сравнивает «этот заказ с группой <paramref name="group"/>» со своими вариантами
        /// точки. <c>true</c> — согласился (он в группе и выходит с ней). Отказ — его выбор действует, событие с причиной.
        /// </summary>
        private bool Invite(DecisionScope scope, Adventurer invitee, Adventurer inviter, Party party, Order order, IReadOnlyList<Adventurer> group,
            bool permanent, DecisionPoint point)
        {
            SimContext ctx = scope.Ctx;
            DataRegistry data = ctx.Data;
            decidedThisHour.Add(invitee.Id);

            var snapshot = new List<Adventurer>(group);
            float commission = ctx.World.Treasury.Commission;
            RelationBook relations = ctx.World.Relations;
            var lens = new PartyLens(invitee, order, permanent, commission, relations, data, scope.Profiles, scope.SafetyMultiplier);
            var join = new DecisionAction(DecisionActionKind.JoinParty, Activity.Resting, false,
                (sc, a, scores) => lens.Scores(snapshot, scores), order.Id);

            var actions = new List<DecisionAction>(point.Options);
            actions.AddRange(OrderChoice.Actions(ctx, invitee));
            actions.Add(join);
            var permitted = new List<DecisionAction>();
            FilterBans(ctx, invitee, actions, permitted);
            if (!permitted.Contains(join)) return false;

            var choices = new List<Option>();
            Evaluate(scope, invitee, permitted, choices);
            Option invitation = choices.Find(o => o.Action == join);
            string label = "invite #" + inviter.Id.ToString(CultureInfo.InvariantCulture);
            if (ctx.Log.IsOn(SimLogLevel.Trace)) WriteScores(ctx.Log, invitee, label, choices);

            if (!Choose(ctx, invitee, choices, out Option chosen, out bool best))
            {
                invitee.State.PlannedActivity = Activity.Resting;
                return false;
            }
            invitee.State.PlannedActivity = chosen.Action.Activity;
            if (ctx.Log.IsOn(SimLogLevel.Info)) WriteDecision(ctx.Log, invitee, label, chosen, best, choices, data.Balance.Decisions.MaxReasons);

            if (chosen.Action == join)
            {
                invitee.PartyId = party.Id;
                invitee.State.PlannedOrderId = order.Id;
                invitee.IdleOrderDays = 0;
                return true;
            }

            bool alone = chosen.Action.Kind == DecisionActionKind.TakeOrder;
            if (alone)
            {
                OrderChoice.Take(ctx, invitee, chosen.Action, permitted);
                RevealService.TryRevealAxis(ctx, invitee, AxisId.People, RevealTrigger.LonerWentSolo);
            }
            InvitationRefusal cause = PartyReasons.Of(chosen.Weights, chosen.Scores, invitation.Weights, invitation.Scores);
            ctx.Events.Publish(SimEventType.InvitationDeclined, EventImportance.Normal, invitee.Id, inviter.Id)
                .With("order", order.Id)
                .With("cause", cause.ToString())
                .With("reason", PartyReasons.Text(ctx, invitee, cause))
                .With("solo", alone)
                .With("party", PartyService.NameOf(ctx, party))
                .With("partyId", party.Id);
            if (point.Kind == DecisionPointKind.Morning) invitee.State.MorningDecisionDay = DecisionPoints.Today(ctx);
            return false;
        }

        private static void PublishGathered(SimContext ctx, Party party, Order order, List<Adventurer> group)
        {
            if (ctx.Log.IsOn(SimLogLevel.Info))
            {
                StringBuilder line = ctx.Log.Begin(SimLogLevel.Info);
                line.Append("party #").Append(party.Id.ToString(CultureInfo.InvariantCulture)).Append(" gathered for #")
                    .Append(order.Id.ToString(CultureInfo.InvariantCulture)).Append(':');
                foreach (Adventurer member in group) AdventurerLog.AppendName(line.Append(' '), member);
                ctx.Log.Commit();
            }
            ctx.Events.Publish(SimEventType.PartyGathered, EventImportance.Normal, QuestSystem.Ids(group))
                .With("order", order.Id)
                .With("count", group.Count - 2)
                .With("total", group.Count)
                .With("permanent", party.IsPermanent)
                .With("party", PartyService.NameOf(ctx, party))
                .With("partyId", party.Id);
        }

        // ---------- Постоянные группы ----------

        /// <summary>Утром, раньше всех: каждая постоянная группа, у которой свободны хотя бы двое, решает, идти ли вместе.</summary>
        private void PermanentPartiesDecide(DecisionScope scope)
        {
            SimContext ctx = scope.Ctx;
            if (!ctx.Rhythm.CanStartQuest(ctx.World.Time.Hour + 1)) return;
            long today = DecisionPoints.Today(ctx);
            long now = ctx.World.Time.TotalHours;
            foreach (Party party in new List<Party>(ctx.World.Parties.Active))
            {
                if (!party.IsPermanent || party.DecidedDay == today) continue;
                var free = new List<Adventurer>();
                foreach (Adventurer member in PartyService.Members(ctx, party))
                {
                    if (CanInvite(ctx, member, now) && member.State.MorningDecisionDay != today) free.Add(member);
                }
                if (free.Count < 2) continue;
                party.DecidedDay = today;
                PermanentPartyDecide(scope, party, free);
            }
        }

        private void PermanentPartyDecide(DecisionScope scope, Party party, List<Adventurer> free)
        {
            SimContext ctx = scope.Ctx;
            DataRegistry data = ctx.Data;
            RelationBook relations = ctx.World.Relations;
            float commission = ctx.World.Treasury.Commission;
            Adventurer leader = QuestParty.Leader(free, data);

            var choices = new List<PartyOption>();
            float restSum = 0f;
            foreach (Adventurer member in free)
                restSum += PartyMath.Value(member, DecisionActions.Scores(scope, member, DecisionActions.Rest), data);
            choices.Add(new PartyOption(null, null, restSum / free.Count, 0));

            foreach (Order order in new List<Order>(ctx.World.Orders.Open))
            {
                if (order.Status != OrderStatus.OnBoard || order.IsPromotion) continue;
                var probe = new DecisionAction(DecisionActionKind.JoinParty, Activity.Resting, false, null, order.Id);
                if (FindBan(ctx, leader, probe) != null) continue;
                var group = new List<Adventurer> { leader };
                foreach (Adventurer member in free)
                {
                    if (member.Id != leader.Id && FindBan(ctx, member, probe) == null) group.Add(member);
                }
                if (group.Count < 2) continue;
                choices.Add(new PartyOption(order, group, PartyMath.MeanGroupValue(order, group, true, commission, relations, data, scope.Profiles,
                    scope.SafetyMultiplier),
                    choices.Count));
            }
            if (choices.Count == 1) return;

            choices.Sort((a, b) => a.Value != b.Value ? b.Value.CompareTo(a.Value) : a.Order.CompareTo(b.Order));
            bool best = ctx.RollChance(data.Balance.Decisions.BestChoiceChance, "decision-best", leader);
            PartyOption chosen = best ? choices[0] : choices[1];
            if (ctx.Log.IsOn(SimLogLevel.Info)) WritePartyDecision(ctx.Log, party, leader, chosen, best, choices);
            if (chosen.Target == null) return;

            Order target = chosen.Target;
            OrderChoice.Reserve(ctx, target, leader);
            party.OrderId = target.Id;
            party.InitiatorId = leader.Id;
            leader.PartyId = party.Id;
            decidedThisHour.Add(leader.Id);

            DecisionPoint point = MorningPoint;
            var going = new List<Adventurer> { leader };
            var outside = new HashSet<int>(party.MemberIds);
            foreach (Adventurer member in chosen.Group)
            {
                if (member.Id != leader.Id && Invite(scope, member, leader, party, target, chosen.Group, true, point)) going.Add(member);
            }
            var leaderLens = new PartyLens(leader, target, true, commission, relations, data, scope.Profiles, scope.SafetyMultiplier);
            InviteWhileValueGrows(scope, leaderLens, party, going, true, point, outside,
                members => PartyMath.MeanGroupValue(target, members, true, commission, relations, data, scope.Profiles,
                    scope.SafetyMultiplier));

            if (going.Count < 2)
            {
                ctx.Log.Write(SimLogLevel.Info, "party #{0} {1}: nobody else goes today", party.Id, party.Name);
                OrderChoice.Release(ctx, target);
                PartyService.Release(ctx, party, going);
                decidedThisHour.Remove(leader.Id);
                return;
            }

            long today = DecisionPoints.Today(ctx);
            foreach (Adventurer member in going)
            {
                member.State.MorningDecisionDay = today;
                member.State.PlannedActivity = Activity.Resting;
                member.PartyId = party.Id;
            }
            OrderChoice.Commit(ctx, leader, target, Array.Empty<DecisionAction>(), going.Count);
            PublishGathered(ctx, party, target, going);
        }

        /// <summary>Вариант постоянной группы: заказ (с кем из свободных) или отдых (<see cref="Target"/> — <c>null</c>).</summary>
        private readonly struct PartyOption
        {
            public PartyOption(Order target, List<Adventurer> group, float value, int order)
            {
                Target = target;
                Group = group;
                Value = value;
                Order = order;
            }

            public Order Target { get; }
            public List<Adventurer> Group { get; }
            public float Value { get; }
            public int Order { get; }

            public string Label => Target == null ? "Rest" : "PartyOrder#" + Target.Id.ToString(CultureInfo.InvariantCulture);
        }

        // ---------- Лог ----------

        /// <summary>«party-presume #3 Имя #12: #5 Имя, #7 Имя values=0.8,1.1,1.2».</summary>
        private static void WritePresumed(SimLogger log, Adventurer initiator, Order order, PresumedParty party)
        {
            StringBuilder line = log.Begin(SimLogLevel.Trace);
            AdventurerLog.AppendName(line.Append("party-presume "), initiator).Append(" #").Append(order.Id.ToString(CultureInfo.InvariantCulture)).Append(':');
            for (int i = 1; i < party.Members.Count; i++) AdventurerLog.AppendName(line.Append(' '), party.Members[i]);
            line.Append(" values=");
            for (int i = 0; i < party.Values.Count; i++) line.Append(i > 0 ? "," : string.Empty).Append(AdventurerLog.Number(party.Values[i]));
            log.Commit();
        }

        /// <summary>«partner-eval #3 Имя #12: #5 Имя gain=0.12 score=0.61».</summary>
        private static void WritePartner(SimLogger log, Adventurer initiator, Order order, Adventurer candidate, float gain, float score)
        {
            StringBuilder line = log.Begin(SimLogLevel.Trace);
            AdventurerLog.AppendName(line.Append("partner-eval "), initiator).Append(" #").Append(order.Id.ToString(CultureInfo.InvariantCulture)).Append(": ");
            AdventurerLog.AppendName(line, candidate).Append(" gain=").Append(AdventurerLog.Number(gain)).Append(" score=").Append(AdventurerLog.Number(score));
            log.Commit();
        }

        /// <summary>«party #4 seek: #3 Имя #12 presumed #5 Имя, #7 Имя min=2 value=1.2 other=0.9».</summary>
        private static void WriteSeek(SimLogger log, Party party, Adventurer initiator, Order order, PresumedParty plan, int minimum, float bestOther)
        {
            StringBuilder line = log.Begin(SimLogLevel.Info);
            line.Append("party #").Append(party.Id.ToString(CultureInfo.InvariantCulture)).Append(" seek: ");
            AdventurerLog.AppendName(line, initiator).Append(" #").Append(order.Id.ToString(CultureInfo.InvariantCulture)).Append(" presumed");
            for (int i = 1; i < plan.Members.Count; i++) AdventurerLog.AppendName(line.Append(' '), plan.Members[i]);
            line.Append(" min=").Append(minimum.ToString(CultureInfo.InvariantCulture))
                .Append(" value=").Append(AdventurerLog.Number(plan.Value))
                .Append(" other=").Append(float.IsNegativeInfinity(bestOther) ? "-" : AdventurerLog.Number(bestOther));
            log.Commit();
        }

        /// <summary>«party #4 Серые волки decide (#3 Имя): PartyOrder#12 1.2 (best; Rest 0.9)».</summary>
        private static void WritePartyDecision(SimLogger log, Party party, Adventurer leader, PartyOption chosen, bool best, List<PartyOption> all)
        {
            StringBuilder line = log.Begin(SimLogLevel.Info);
            line.Append("party #").Append(party.Id.ToString(CultureInfo.InvariantCulture)).Append(' ').Append(party.Name).Append(" decide (");
            AdventurerLog.AppendName(line, leader).Append("): ").Append(chosen.Label).Append(' ').Append(AdventurerLog.Number(chosen.Value))
                .Append(" (").Append(best ? "best" : "second");
            foreach (PartyOption other in all)
            {
                if (other.Order == chosen.Order) continue;
                line.Append("; ").Append(other.Label).Append(' ').Append(AdventurerLog.Number(other.Value));
            }
            line.Append(')');
            log.Commit();
        }
    }
}
