using System;
using System.Collections.Generic;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Игрок задаёт правила Регистратора: какие типы брать (<c>null</c> — все), до какого ранга, с какой награды.
    /// Действуют на заказы, пришедшие после изменения. Те же правила — без события.
    /// </summary>
    public sealed class SetRegistrarRulesCommand : ICommand
    {
        public SetRegistrarRulesCommand(IEnumerable<string> allowedTypes, GuildRank maxRank, int minReward)
        {
            Rules = new RegistrarRules(allowedTypes, maxRank, minReward);
        }

        public SetRegistrarRulesCommand(RegistrarRules rules)
        {
            Rules = rules ?? throw new ArgumentNullException(nameof(rules));
        }

        public RegistrarRules Rules { get; }

        public void Apply(SimContext ctx)
        {
            OrderBoard board = ctx.World.Orders;
            if (board.Rules.IsSameAs(Rules)) return;

            RegistrarRules from = board.Rules;
            board.Rules = Rules;
            ctx.Events.Publish(SimEventType.RegistrarRulesChanged)
                .With("from", from.ToString())
                .With("to", Rules.ToString());
        }
    }

    /// <summary>
    /// Ответ игрока на важный заказ: принять — заказ на доске с этого часа (срок на доске считается от принятия), можно сразу
    /// назначить доплату; отклонить — заказ в архиве. Заказ не ждёт ответа (нет такого, уже отклонён) или это событийное задание
    /// (у него свой ответ — ранг) — ничего.
    /// </summary>
    public sealed class AnswerImportantOrderCommand : ICommand
    {
        public AnswerImportantOrderCommand(int orderId, bool accept, int surcharge = 0)
        {
            OrderId = orderId;
            Accept = accept;
            Surcharge = surcharge;
        }

        public int OrderId { get; }
        public bool Accept { get; }

        /// <summary>Доплата при принятии; меньше нуля — 0.</summary>
        public int Surcharge { get; }

        public void Apply(SimContext ctx)
        {
            OrderBoard board = ctx.World.Orders;
            if (!board.TryGetOpen(OrderId, out Order order) || order.Status != OrderStatus.AwaitingPlayer || order.IsEventQuest) return;

            if (!Accept)
            {
                OrderSystem.Decline(ctx, order, OrderDeclinedBy.Player);
                OrderSystem.Publish(ctx, SimEventType.OrderDeclinedByPlayer, order).With("noAnswer", false);
                return;
            }

            order.Surcharge = Math.Max(0, Surcharge);
            OrderSystem.Post(ctx, order);
            board.Totals = board.Totals.AddPosted();
            OrderSystem.Publish(ctx, SimEventType.OrderPosted, order)
                .With("by", "player")
                .With("expiresAt", order.ExpiresAtHours)
                .With("surcharge", order.Surcharge);
        }
    }

    /// <summary>
    /// Доплата гильдии к заказу на доске — обещание, из казны сейчас не списывается. Целое не меньше 0; 0 снимает доплату.
    /// Заказ не на доске или та же сумма — ничего.
    /// </summary>
    public sealed class SetSurchargeCommand : ICommand
    {
        public SetSurchargeCommand(int orderId, int amount)
        {
            OrderId = orderId;
            Amount = amount;
        }

        public int OrderId { get; }
        public int Amount { get; }

        public void Apply(SimContext ctx)
        {
            if (!ctx.World.Orders.TryGetOpen(OrderId, out Order order) || order.Status != OrderStatus.OnBoard) return;

            int value = Math.Max(0, Amount);
            if (order.Surcharge == value) return;

            int from = order.Surcharge;
            order.Surcharge = value;
            ctx.Events.Publish(SimEventType.SurchargeSet)
                .With("order", order.Id)
                .With("from", from)
                .With("to", value);
        }
    }
}
