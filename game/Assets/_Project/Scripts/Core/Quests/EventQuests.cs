using System;
using System.Collections.Generic;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Событийное задание из находки, мимо которой прошли. Заказ ждёт игрока: игрок назначает ранг (от него — награда и кто
    /// может взять) или отказывается; без ответа <c>playerResponseDays</c> дней — отклонено. Настоящий профиль — от истинного
    /// ранга находки, скрыт. Награду платит гильдия, комиссии нет.
    /// </summary>
    public static class EventQuests
    {
        /// <summary>Создать событийное задание: [В], автопауза «Важный заказ». Нет типа событийного задания в данных — нет и задания.</summary>
        public static Order Create(SimContext ctx, QuestRun run, DiscoveryDefinition discovery, QuestDiscovery found)
        {
            QuestTypeDefinition type = discovery.EventQuestType;
            if (type == null) return null;

            DataRegistry data = ctx.Data;
            OrdersBalance balance = data.Balance.Orders;
            OrderBoard board = ctx.World.Orders;
            long now = ctx.World.Time.TotalHours;

            var order = new Order(board.NextId(), type.Id, run.Rank, run.Distance)
            {
                IsEventQuest = true,
                SourceRank = run.Rank,
                TrueRank = found.TrueRank,
                Place = run.Place,
                ArrivedAtHours = now,
                DescriptionAccuracy = 1f,
                Status = OrderStatus.AwaitingPlayer,
                AnswerDueAtHours = now + ctx.Calendar.DaysToHours(balance.PlayerResponseDays),
            };
            float[] profile = OrderGenerator.BuildProfile(ctx.Rng, data, type, found.TrueRank, run.Distance);
            for (int i = 0; i < profile.Length; i++) order.SetRequirement((StatId)i, profile[i]);
            order.PartySizeCeiling = type.MaxPartySizeCeiling;
            foreach (StatCeiling ceiling in type.StatCeilings)
            {
                if (ceiling != null) order.SetStatCeiling(ceiling.Stat, ceiling.For(found.TrueRank));
            }
            order.BoardDays = ctx.Rng.RangeInclusive(balance.BoardDays.Min, balance.BoardDays.Max);

            var errors = new List<string>();
            order.Description = TextRenderer.Render(discovery.EventQuestTitle, new OrderTextSource(order), errors);
            foreach (string error in errors) ctx.Log.Write(SimLogLevel.Error, "event quest #{0} description: {1}", order.Id, error);

            board.AddOpen(order);
            OrderSystem.WriteGenerated(ctx, "event", order);
            OrderSystem.Publish(ctx, SimEventType.EventQuestAwaitingPlayer, order, EventImportance.Important)
                .With("trueRank", found.TrueRank)
                .With("answerDueAt", order.AnswerDueAtHours);
            return order;
        }
    }

    /// <summary>
    /// Ответ игрока на событийное задание: назначить ранг G–C — награда по рангу (как у обычного заказа), заказ на доске с этого
    /// часа; <c>null</c> — отказ, заказ в архиве. Заказ не ждёт ответа — ничего.
    /// </summary>
    public sealed class AnswerEventQuestCommand : ICommand
    {
        public AnswerEventQuestCommand(int orderId, GuildRank? rank)
        {
            OrderId = orderId;
            Rank = rank;
        }

        public int OrderId { get; }

        /// <summary>Назначенный ранг; <c>null</c> — отказ.</summary>
        public GuildRank? Rank { get; }

        public void Apply(SimContext ctx)
        {
            OrderBoard board = ctx.World.Orders;
            if (!board.TryGetOpen(OrderId, out Order order) || !order.IsEventQuest || order.Status != OrderStatus.AwaitingPlayer) return;

            if (!Rank.HasValue)
            {
                OrderSystem.Decline(ctx, order, OrderDeclinedBy.Player);
                OrderSystem.Publish(ctx, SimEventType.EventQuestAnswered, order).With("declined", true).With("noAnswer", false);
                return;
            }

            order.Rank = Rank.Value;
            order.Reward = OrderGenerator.Reward(ctx.Rng, ctx.Data, order.Rank, order.Distance);
            OrderSystem.Post(ctx, order);
            board.Totals = board.Totals.AddPosted();
            OrderSystem.Publish(ctx, SimEventType.EventQuestAnswered, order).With("declined", false).With("noAnswer", false);
            OrderSystem.Publish(ctx, SimEventType.OrderPosted, order)
                .With("by", "player")
                .With("expiresAt", order.ExpiresAtHours)
                .With("surcharge", order.Surcharge);
        }
    }
}
