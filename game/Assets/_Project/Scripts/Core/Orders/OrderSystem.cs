using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Шаг 3 такта: заказы. Каждый такт — снять с доски заказы, чей срок вышел (<see cref="SimEventType.OrderExpired"/>), и
    /// отклонить важные, на которые игрок не ответил вовремя. Утром (начало утра) — новые заказы: число по репутации
    /// (дробная часть — шанс ещё одного, бросок всегда), каждый — <see cref="OrderGenerator"/>. Важный — игроку
    /// (<see cref="OrderStatus.AwaitingPlayer"/>, событие [В]); обычный — Регистратору: подходит под правила — на доску,
    /// нет — отклонён. За утро — по событию со счётом повешенных и отклонённых Регистратором (строки ленты).
    /// Стартовые заказы — <see cref="ApplyStart"/>, своим потоком. Реестр только из чисел — заказов нет.
    /// </summary>
    public sealed class OrderSystem : ISimSystem
    {
        /// <summary>Поток случайных чисел стартовых заказов.</summary>
        public const string StartStreamName = "OrderStart";

        private readonly List<string> errors = new List<string>();

        public string Name => nameof(OrderSystem);

        public void Tick(SimContext ctx)
        {
            if (!ctx.Data.HasDefinitions) return;

            CloseOverdue(ctx);
            if (ctx.World.Time.Hour == ctx.Data.Balance.Time.MorningHour) Morning(ctx);
        }

        /// <summary>
        /// Стартовые заказы: <c>startOrders</c> заказов ранга <c>startOrderRank</c> сразу на доске, без Регистратора и событий —
        /// стартовое состояние не новость. В счётчики заказов не входят.
        /// </summary>
        public static void ApplyStart(SimContext ctx)
        {
            if (!ctx.Data.HasDefinitions) return;

            OrdersBalance balance = ctx.Data.Balance.Orders;
            var errors = new List<string>();
            for (int i = 0; i < balance.StartOrders; i++)
            {
                Order order = New(ctx, balance.StartOrderRank, errors);
                order.IsImportant = false;
                Post(ctx, order);
                ctx.World.Orders.AddOpen(order);
                WriteGenerated(ctx, "start", order);
            }
        }

        private void Morning(SimContext ctx)
        {
            OrdersBalance balance = ctx.Data.Balance.Orders;
            OrderBoard board = ctx.World.Orders;
            float perDay = OrderGenerator.OrdersPerDay(balance, ctx.World.Guild.Reputation);
            int count = (int)Math.Floor(perDay);
            if (ctx.RollChance(perDay - count, "extra-order", null, "reputation", ctx.World.Guild.Reputation)) count++;

            int posted = 0;
            int declined = 0;
            for (int i = 0; i < count; i++)
            {
                Order order = New(ctx, null, errors);
                board.Totals = board.Totals.AddArrived();
                WriteGenerated(ctx, "new", order);
                Publish(ctx, SimEventType.OrderArrived, order)
                    .With("important", order.IsImportant)
                    .With("hardEarly", order.IsHardEarly);

                if (order.IsImportant)
                {
                    order.Status = OrderStatus.AwaitingPlayer;
                    order.AnswerDueAtHours = ctx.World.Time.TotalHours + ctx.Calendar.DaysToHours(balance.PlayerResponseDays);
                    board.AddOpen(order);
                    Publish(ctx, SimEventType.OrderAwaitingPlayer, order, EventImportance.Important)
                        .With("answerDueAt", order.AnswerDueAtHours);
                }
                else if (board.Rules.IsAccepted(order))
                {
                    Post(ctx, order);
                    board.AddOpen(order);
                    board.Totals = board.Totals.AddPosted();
                    posted++;
                    Publish(ctx, SimEventType.OrderPosted, order)
                        .With("by", "registrar")
                        .With("expiresAt", order.ExpiresAtHours);
                }
                else
                {
                    Decline(ctx, order, OrderDeclinedBy.Registrar);
                    declined++;
                    Publish(ctx, SimEventType.OrderDeclinedByRegistrar, order)
                        .With("rules", board.Rules.ToString());
                }
            }

            if (posted > 0) ctx.Events.Publish(SimEventType.NewOrdersPosted).With("count", posted);
            if (declined > 0) ctx.Events.Publish(SimEventType.RegistrarDeclinedOrders).With("count", declined);
        }

        /// <summary>Снять заказы с вышедшим сроком и отклонить важные без ответа.</summary>
        private static void CloseOverdue(SimContext ctx)
        {
            OrderBoard board = ctx.World.Orders;
            long now = ctx.World.Time.TotalHours;
            var overdue = new List<Order>();
            foreach (Order order in board.Open)
            {
                if ((order.Status == OrderStatus.OnBoard && now >= order.ExpiresAtHours)
                    || (order.Status == OrderStatus.AwaitingPlayer && now >= order.AnswerDueAtHours))
                    overdue.Add(order);
            }

            foreach (Order order in overdue)
            {
                if (order.Status == OrderStatus.OnBoard)
                {
                    order.Status = OrderStatus.Expired;
                    order.ClosedAtHours = now;
                    board.Close(order, ctx.Data.Balance.Orders.ClosedOrdersLimit);
                    board.Totals = board.Totals.AddExpired();
                    Publish(ctx, SimEventType.OrderExpired, order).With("surcharge", order.Surcharge);
                }
                else if (order.IsEventQuest)
                {
                    Decline(ctx, order, OrderDeclinedBy.NoAnswer);
                    Publish(ctx, SimEventType.EventQuestAnswered, order).With("declined", true).With("noAnswer", true);
                }
                else
                {
                    Decline(ctx, order, OrderDeclinedBy.NoAnswer);
                    Publish(ctx, SimEventType.OrderDeclinedByPlayer, order).With("noAnswer", true);
                }
            }
        }

        private static Order New(SimContext ctx, GuildRank? fixedRank, List<string> errors)
        {
            errors.Clear();
            Order order = OrderGenerator.Generate(ctx.Rng, ctx.Data, ctx.World.Guild.Reputation, ctx.World.Orders.NextId(),
                ctx.World.Time.TotalHours, fixedRank, errors);
            foreach (string error in errors) ctx.Log.Write(SimLogLevel.Error, "order #{0} description: {1}", order.Id, error);
            return order;
        }

        /// <summary>Повесить на доску сейчас: срок считается с этого часа.</summary>
        internal static void Post(SimContext ctx, Order order)
        {
            long now = ctx.World.Time.TotalHours;
            order.Status = OrderStatus.OnBoard;
            order.PostedAtHours = now;
            order.ExpiresAtHours = now + ctx.Calendar.DaysToHours(order.BoardDays);
            order.AnswerDueAtHours = -1;
        }

        /// <summary>Отклонить заказ и положить в архив; счётчики — по тому, кто отклонил.</summary>
        internal static void Decline(SimContext ctx, Order order, OrderDeclinedBy by)
        {
            OrderBoard board = ctx.World.Orders;
            order.Status = OrderStatus.Declined;
            order.DeclinedBy = by;
            order.ClosedAtHours = ctx.World.Time.TotalHours;
            order.AnswerDueAtHours = -1;
            board.Close(order, ctx.Data.Balance.Orders.ClosedOrdersLimit);
            board.Totals = by == OrderDeclinedBy.Registrar ? board.Totals.AddDeclinedByRegistrar() : board.Totals.AddDeclinedByPlayer();
        }

        /// <summary>
        /// Событие о заказе: id заказа, тип, ранг, расстояние, награда и названия для ленты (<c>client</c>, <c>place</c>,
        /// <c>enemy</c>, <c>cargo</c>). Id заказа — в данных, не в участниках: участники — люди.
        /// </summary>
        internal static SimEvent Publish(SimContext ctx, SimEventType type, Order order, EventImportance importance = EventImportance.Normal,
            params int[] participants)
        {
            SimEvent simEvent = ctx.Events.Publish(type, importance, participants)
                .With("order", order.Id)
                .With("questType", order.TypeId)
                .With("rank", order.Rank)
                .With("distance", order.Distance)
                .With("reward", order.Reward)
                .With("place", order.Place);
            if (order.Client != null) simEvent.With("client", order.Client);
            if (order.Enemy != null) simEvent.With("enemy", order.Enemy);
            if (order.Cargo != null) simEvent.With("cargo", order.Cargo);
            return simEvent;
        }

        /// <summary>
        /// В лог (<see cref="SimLogLevel.Debug"/>) — заказ целиком, со скрытым: «order new #4 Hunt F near reward=95 board=5d
        /// important=no hardEarly=no accuracy=0.97 profile Str=16.2 … hints=Perception,Knowledge: Мельник просит…».
        /// </summary>
        internal static void WriteGenerated(SimContext ctx, string what, Order order)
        {
            SimLogger log = ctx.Log;
            if (!log.IsOn(SimLogLevel.Debug)) return;

            StringBuilder line = log.Begin(SimLogLevel.Debug);
            line.Append("order ").Append(what).Append(" #").Append(order.Id.ToString(CultureInfo.InvariantCulture))
                .Append(' ').Append(order.TypeId).Append(' ').Append(order.Rank)
                .Append(' ').Append(order.Distance == OrderDistance.Far ? "far" : "near")
                .Append(" reward=").Append(order.Reward.ToString(CultureInfo.InvariantCulture))
                .Append(" board=").Append(order.BoardDays.ToString(CultureInfo.InvariantCulture)).Append('d')
                .Append(" important=").Append(order.IsImportant ? "yes" : "no")
                .Append(" hardEarly=").Append(order.IsHardEarly ? "yes" : "no")
                .Append(" accuracy=").Append(order.DescriptionAccuracy.ToString("0.00", CultureInfo.InvariantCulture))
                .Append(" profile");
            for (int i = 0; i < Vocabulary.StatCount; i++)
            {
                var stat = (StatId)i;
                if (!Vocabulary.IsDiagramAxis(stat)) continue;
                line.Append(' ').Append(stat).Append('=').Append(order.Requirement(stat).ToString("0.#", CultureInfo.InvariantCulture));
            }
            if (order.PartySizeCeiling > 0) line.Append(" partyCeiling=").Append(order.PartySizeCeiling.ToString(CultureInfo.InvariantCulture));
            for (int i = 0; i < Vocabulary.StatCount; i++)
            {
                int ceiling = order.StatCeiling((StatId)i);
                if (ceiling > 0) line.Append(" ceiling ").Append((StatId)i).Append('=').Append(ceiling.ToString(CultureInfo.InvariantCulture));
            }
            line.Append(" hints=");
            for (int i = 0; i < order.HintStats.Count; i++)
            {
                if (i > 0) line.Append(',');
                line.Append(order.HintStats[i]);
            }
            line.Append(": ").Append(order.Description);
            log.Commit();
        }
    }
}
