using System.Collections.Generic;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Задание на повышение — экзамен: показывает, может ли человек брать заказы следующего ранга. Появляется в начале утра
    /// у того, кто готов (<see cref="GuildRanks.IsReadyForPromotion"/>) и ещё не держит экзамена: личный заказ следующего
    /// ранга, тип и профиль — как у обычного заказа этого ранга, награды нет. Висит на доске, пока владелец его не возьмёт;
    /// по сроку не снимается, правила Регистратора и счётчики заказов его не касаются. Экзамен человека, ушедшего из гильдии,
    /// снимается.
    /// </summary>
    public static class PromotionOrders
    {
        public static void Offer(SimContext ctx)
        {
            RemoveOrphaned(ctx);

            RanksBalance ranks = ctx.Data.Balance.Ranks;
            long now = ctx.World.Time.TotalHours;
            var errors = new List<string>();
            foreach (Adventurer adventurer in ctx.World.Adventurers.Active)
            {
                if (!GuildRanks.IsReadyForPromotion(adventurer, ranks, now)) continue;
                if (ctx.World.Orders.TryGetPromotion(adventurer.Id, out _)) continue;

                errors.Clear();
                Order order = OrderGenerator.Generate(ctx.Rng, ctx.Data, ctx.World.Guild.Reputation, ctx.World.Orders.NextId(), now,
                    adventurer.GuildRank + 1, errors);
                foreach (string error in errors) ctx.Log.Write(SimLogLevel.Error, "promotion #{0} description: {1}", order.Id, error);

                order.IsPromotion = true;
                order.OwnerId = adventurer.Id;
                order.IsImportant = false;
                order.Reward = 0;
                order.Status = OrderStatus.OnBoard;
                order.PostedAtHours = now;
                order.ExpiresAtHours = long.MaxValue;
                ctx.World.Orders.AddOpen(order);
                OrderSystem.WriteGenerated(ctx, "promotion", order);
                OrderSystem.Publish(ctx, SimEventType.PromotionOffered, order, EventImportance.Normal, adventurer.Id);
            }
        }

        private static void RemoveOrphaned(SimContext ctx)
        {
            OrderBoard board = ctx.World.Orders;
            var orphaned = new List<Order>();
            foreach (Order order in board.Open)
            {
                if (order.IsPromotion && !ctx.World.Adventurers.IsActive(order.OwnerId)) orphaned.Add(order);
            }
            foreach (Order order in orphaned)
            {
                order.Status = OrderStatus.Expired;
                order.ClosedAtHours = ctx.World.Time.TotalHours;
                board.Close(order, ctx.Data.Balance.Orders.ClosedOrdersLimit);
            }
        }
    }
}
