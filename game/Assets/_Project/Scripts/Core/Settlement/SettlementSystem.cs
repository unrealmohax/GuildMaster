using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Поселение за такт (после обращений, перед казной). По событиям этого такта:
    /// <list type="bullet">
    /// <item>задание вернулось (кроме экзаменов, событийных заданий и набега — его итог решает кризис): выполнено — + угроза или
    /// другой заказ × ранг; не выполнено —
    /// − × ранг, погибли все ушедшие — ещё − <c>partyLost</c>;</item>
    /// <item>заказ-угроза снят по сроку, отклонён Регистратором или игроком (в том числе важный без ответа) — − × ранг.</item>
    /// </list>
    /// В 00:00 — суточный спад (зима и Дозорная вышка — <see cref="CrisisRules.DailyDecay"/>) и доход таверны от жителей; в начале
    /// месяца — население по уровню; в час ночи в осаде — бросок нападения (один бросок за ночь, диапазоны — только при нападении;
    /// с Частоколом — только при Безопасности ниже его порога, иначе без броска). Каждый час — проверка покидания: население 0 или
    /// Безопасность на нуле <c>abandonDays</c> суток — гильдия закрыта.
    /// Население за готовую постройку гильдии прибавляет сама постройка (<see cref="SettlementService.BuildingReady"/>).
    /// </summary>
    public sealed class SettlementSystem : ISimSystem
    {
        public string Name => nameof(SettlementSystem);

        public void Tick(SimContext ctx)
        {
            if (!ctx.Data.HasDefinitions || ctx.World.Treasury.IsClosed) return;

            ApplyEvents(ctx);

            GameTime time = ctx.World.Time;
            SettlementBalance balance = ctx.Data.Balance.Settlement;
            if (time.Hour == 0)
            {
                SettlementService.ChangeSecurity(ctx, -CrisisRules.DailyDecay(ctx.World, ctx.Data), SettlementReasons.Night);
                SettlementService.SettleLocalsTavern(ctx);
                if (time.Day == 1) SettlementService.MonthlyPopulation(ctx);
            }

            if (time.Hour == ctx.Data.Balance.Time.NightHour && ctx.World.Settlement.Level == SettlementLevel.Siege
                && CrisisRules.AttackPossible(ctx.World, ctx.Data)
                && ctx.RollChance(balance.AttackChancePerNight, "settlement-attack", null, "security", ctx.World.Settlement.Security))
                SettlementService.Attack(ctx);

            SettlementService.CheckAbandoned(ctx);
        }

        /// <summary>События такта, опубликованные до этой системы (свои события системы в список не попадают — счёт до начала).</summary>
        private static void ApplyEvents(SimContext ctx)
        {
            var events = ctx.Events.Events;
            int count = events.Count;
            for (int i = 0; i < count; i++)
            {
                SimEvent simEvent = events[i];
                switch (simEvent.Type)
                {
                    case SimEventType.QuestReturned:
                        QuestReturned(ctx, simEvent);
                        break;
                    case SimEventType.OrderExpired:
                        ThreatMissed(ctx, simEvent, SettlementReasons.Expired);
                        break;
                    case SimEventType.OrderDeclinedByRegistrar:
                        ThreatMissed(ctx, simEvent, SettlementReasons.DeclinedByRegistrar);
                        break;
                    case SimEventType.OrderDeclinedByPlayer:
                        bool noAnswer = simEvent.TryGet("noAnswer", out bool flag) && flag;
                        ThreatMissed(ctx, simEvent, noAnswer ? SettlementReasons.NoAnswer : SettlementReasons.DeclinedByPlayer);
                        break;
                }
            }
        }

        private static void QuestReturned(SimContext ctx, SimEvent simEvent)
        {
            if (simEvent.TryGet("promotion", out bool promotion) && promotion) return;
            if (!simEvent.TryGet("order", out int orderId) || !ctx.World.Orders.TryGetOrder(orderId, out Order order)) return;
            if (order.IsEventQuest || order.IsPromotion || order.IsRaid) return;
            if (!simEvent.TryGet("quest", out int questId) || !ctx.World.Quests.TryGetRun(questId, out QuestRun run)) return;

            SettlementBalance balance = ctx.Data.Balance.Settlement;
            int rank = SettlementRules.RankFactor(order.Rank);
            bool threat = SettlementRules.IsThreat(ctx.Data, order.TypeId);
            if (run.Completed)
            {
                if (threat) ctx.World.Settlement.Totals = ctx.World.Settlement.Totals.AddThreatDone();
                SettlementService.ChangeSecurity(ctx, (threat ? balance.ThreatDonePerRank : balance.OtherDonePerRank) * rank,
                    threat ? SettlementReasons.ThreatDone : SettlementReasons.OtherDone, order);
                return;
            }

            SettlementService.ChangeSecurity(ctx, -balance.FailedPerRank * rank, SettlementReasons.Failed, order);
            if (run.Departed.Count > 0 && run.Dead.Count == run.Departed.Count)
                SettlementService.ChangeSecurity(ctx, -balance.PartyLost, SettlementReasons.PartyLost, order);
        }

        private static void ThreatMissed(SimContext ctx, SimEvent simEvent, string reason)
        {
            if (!simEvent.TryGet("order", out int orderId) || !ctx.World.Orders.TryGetOrder(orderId, out Order order)) return;
            if (order.IsEventQuest || order.IsPromotion || order.IsRaid || !SettlementRules.IsThreat(ctx.Data, order.TypeId)) return;

            ctx.World.Settlement.Totals = ctx.World.Settlement.Totals.AddThreatMissed();
            SettlementService.ChangeSecurity(ctx, -ctx.Data.Balance.Settlement.ThreatMissedPerRank * SettlementRules.RankFactor(order.Rank),
                reason, order);
        }
    }
}
