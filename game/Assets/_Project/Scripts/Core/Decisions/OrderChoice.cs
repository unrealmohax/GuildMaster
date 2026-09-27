using System.Collections.Generic;
using System.Globalization;
using System.Text;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Взять заказ с доски — действие модели решений. По варианту на каждый заказ на доске (чужой экзамен не виден).
    /// Оценки по мотивам — <see cref="QuestChoices.OrderScores"/> от воспринимаемого шанса (<see cref="QuestMath.PerceivedSoloOverlap"/>).
    /// Выбран заказ — он сразу взят (уходит с доски, следующий в этом часу его уже не видит), выход — в следующем часу.
    /// <list type="bullet">
    /// <item>Кошмары: выбран заказ того же типа, после которого появилась черта, — шанс отказа <c>nightmaresRefuseChance</c>:
    /// важное решение с причиной, раскрытие; тогда выбирается лучший из остальных вариантов.</item>
    /// <item>Отказ — были разрешённые заказы, а выбран не заказ: Семейный раскрывается, если у лучшего заказа воспринимаемый
    /// шанс ниже <c>familyRevealPerceivedOverlap</c>; Жадный — если его доля в лучшем заказе ниже средней награды ранга.</item>
    /// <item>Взял: Бескорыстный раскрывается, если доля ниже <c>selflessShareOfAverage</c> средней награды ранга; Амбициозный —
    /// если взял заказ своего ранга, когда были разрешённые ниже, или экзамен в день его появления.</item>
    /// <item>Утром: не взял заказ, хотя мог, был без ран и с усталостью ниже <c>lazyFatigueBelow</c> — +1 день лени; <c>lazyIdleDays</c> подряд —
    /// раскрывается Ленивый.</item>
    /// </list>
    /// </summary>
    public static class OrderChoice
    {
        /// <summary>Варианты «взять заказ» для человека: заказы на доске по порядку (чужие экзамены пропускаются).</summary>
        public static List<DecisionAction> Actions(SimContext ctx, Adventurer adventurer)
        {
            var actions = new List<DecisionAction>();
            foreach (Order order in ctx.World.Orders.Open)
            {
                if (order.Status != OrderStatus.OnBoard) continue;
                if (order.IsPromotion && order.OwnerId != adventurer.Id) continue;
                Order captured = order;
                actions.Add(new DecisionAction(DecisionActionKind.TakeOrder, Activity.Resting, false,
                    (scope, a, scores) => Score(scope.Ctx, a, captured, scores), order.Id));
            }
            return actions;
        }

        private static void Score(SimContext ctx, Adventurer adventurer, Order order, float[] scores)
        {
            float perceived = QuestMath.PerceivedSoloOverlap(adventurer, order, ctx.Data);
            QuestChoices.OrderScores(adventurer, order, perceived, ctx.World.Treasury.Commission, ctx.Data, scores);
            if (ctx.Log.IsOn(SimLogLevel.Trace))
            {
                StringBuilder line = ctx.Log.Begin(SimLogLevel.Trace);
                AdventurerLog.AppendName(line.Append("order-eval "), adventurer)
                    .Append(" #").Append(order.Id.ToString(CultureInfo.InvariantCulture)).Append(' ').Append(order.TypeId).Append(' ').Append(order.Rank)
                    .Append(" reward=").Append(order.Reward.ToString(CultureInfo.InvariantCulture))
                    .Append(" surcharge=").Append(order.Surcharge.ToString(CultureInfo.InvariantCulture))
                    .Append(" perceived=").Append(AdventurerLog.Number(perceived))
                    .Append(" real=").Append(AdventurerLog.Number(QuestMath.RealSoloOverlap(adventurer, order, ctx.Data)));
                ctx.Log.Commit();
            }
        }

        /// <summary>
        /// Кошмары: выбранный заказ того же типа (одному или с группой) — бросок отказа. <c>true</c> — отказался (событие
        /// с причиной, раскрытие).
        /// </summary>
        public static bool RefusesFromNightmares(SimContext ctx, Adventurer adventurer, DecisionAction action)
        {
            if (!action.IsOrder || !ctx.World.Orders.TryGetOrder(action.OrderId, out Order order)) return false;
            TraitInstance nightmares = TraitRules.FindWithHook(adventurer, TraitHook.NightmaresRefuseSimilar, ctx.Data);
            if (nightmares == null || nightmares.SourceQuestTypeId != order.TypeId) return false;
            if (!ctx.RollChance(ctx.Data.Balance.Traits.NightmaresRefuseChance, "nightmares-refuse", adventurer)) return false;

            RevealService.TryRevealTrait(ctx, adventurer, nightmares.TraitId, RevealTrigger.NightmaresRefusedOrder);
            OrderSystem.Publish(ctx, SimEventType.OrderRefused, order, EventImportance.Notable, adventurer.Id)
                .With("reason", QuestReasons.Nightmares(ctx, adventurer));
            return true;
        }

        /// <summary>Выбран заказ: он взят, раскрытия при взятии.</summary>
        public static void Take(SimContext ctx, Adventurer adventurer, DecisionAction action, IReadOnlyList<DecisionAction> allowed)
        {
            if (!ctx.World.Orders.TryGetOpen(action.OrderId, out Order order)) return;
            Reserve(ctx, order, adventurer);
            Commit(ctx, adventurer, order, allowed, 1);
        }

        /// <summary>Снять заказ с доски за человеком (пока группа собирается, другие его не видят).</summary>
        internal static void Reserve(SimContext ctx, Order order, Adventurer taker)
        {
            order.Status = OrderStatus.Taken;
            order.TakenBy = taker.Id;
            ctx.World.Orders.MoveToWork(order);
        }

        /// <summary>Группа не собралась, одному идти не стоит: заказ снова на доске.</summary>
        internal static void Release(SimContext ctx, Order order)
        {
            order.Status = OrderStatus.OnBoard;
            order.TakenBy = 0;
            ctx.World.Orders.ReturnToOpen(order);
        }

        /// <summary>
        /// Заказ взят окончательно (один или с группой из <paramref name="partySize"/> человек): счётчик, выход в следующем часу,
        /// событие, раскрытия при взятии (доля — своя часть награды поровну).
        /// </summary>
        internal static void Commit(SimContext ctx, Adventurer adventurer, Order order, IReadOnlyList<DecisionAction> allowed, int partySize)
        {
            OrderBoard board = ctx.World.Orders;
            if (!order.IsPromotion) board.Totals = board.Totals.AddTaken();
            adventurer.State.PlannedOrderId = order.Id;
            adventurer.IdleOrderDays = 0;
            OrderSystem.Publish(ctx, SimEventType.OrderTaken, order, EventImportance.Normal, adventurer.Id).With("promotion", order.IsPromotion);

            AdventurersBalance people = ctx.Data.Balance.Adventurers;
            float share = QuestChoices.ExpectedShare(order, ctx.World.Treasury.Commission, partySize);
            if (!order.IsPromotion && share < people.SelflessShareOfAverage * AverageReward(ctx.Data, order.Rank))
                RevealService.TryRevealAxis(ctx, adventurer, AxisId.Money, RevealTrigger.SelflessUnderpaidOrder);

            bool topWithLower = !order.IsPromotion && order.Rank == adventurer.GuildRank && HasLowerRank(ctx, allowed, order.Rank);
            bool promotionFirstDay = order.IsPromotion && DecisionPoints.Today(ctx) == order.PostedAtHours / ctx.Calendar.HoursPerDay;
            if (topWithLower || promotionFirstDay) RevealService.TryRevealAxis(ctx, adventurer, AxisId.Work, RevealTrigger.AmbitiousTopOrder);
        }

        /// <summary>
        /// Не взял заказ, хотя мог: раскрытия отказа (Семейный, Жадный) по лучшему из разрешённых заказов и, утром, счёт дней
        /// лени.
        /// </summary>
        public static void Refused(SimContext ctx, Adventurer adventurer, DecisionAction bestOrder, bool morning)
        {
            DataRegistry data = ctx.Data;
            if (ctx.World.Orders.TryGetOrder(bestOrder.OrderId, out Order order))
            {
                float perceived = QuestMath.PerceivedSoloOverlap(adventurer, order, data);
                TraitInstance family = TraitRules.FindWithHook(adventurer, TraitHook.FamilySendsMoneyHome, data);
                if (family != null && perceived < data.Balance.Traits.FamilyRevealPerceivedOverlap)
                    RevealService.TryRevealTrait(ctx, adventurer, family.TraitId, RevealTrigger.FamilyRefusedDanger);

                if (!order.IsPromotion && QuestChoices.ExpectedShare(order, ctx.World.Treasury.Commission, 1) < AverageReward(data, order.Rank))
                    RevealService.TryRevealAxis(ctx, adventurer, AxisId.Money, RevealTrigger.GreedyRefusedPay);
            }

            if (!morning) return;
            AdventurerState state = adventurer.State;
            if (state.Conditions.Count > 0 || state.Fatigue >= data.Balance.Adventurers.LazyFatigueBelow) return;
            adventurer.IdleOrderDays++;
            if (adventurer.IdleOrderDays >= data.Balance.Adventurers.LazyIdleDays)
                RevealService.TryRevealAxis(ctx, adventurer, AxisId.Work, RevealTrigger.LazyIdleDays);
        }

        /// <summary>Средняя награда ранга: середина диапазона наград (без дальности и комиссии).</summary>
        public static float AverageReward(DataRegistry data, GuildRank rank)
        {
            IntRange reward = data.Balance.Ranks.For(rank).Reward;
            return (reward.Min + reward.Max) / 2f;
        }

        private static bool HasLowerRank(SimContext ctx, IReadOnlyList<DecisionAction> allowed, GuildRank rank)
        {
            foreach (DecisionAction action in allowed)
            {
                if (action.Kind == DecisionActionKind.TakeOrder && ctx.World.Orders.TryGetOrder(action.OrderId, out Order other)
                    && !other.IsPromotion && other.Rank < rank)
                    return true;
            }
            return false;
        }
    }
}
