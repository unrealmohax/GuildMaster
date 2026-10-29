namespace GuildMaster.Core
{
    /// <summary>
    /// Кризисы по календарю за такт (после поселения, перед казной; свой поток случайности — броски мора и нападения при
    /// проигранном набеге, генерация заказа набега).
    /// <list type="number">
    /// <item>Итоги набега по событиям такта, опубликованным до системы: задание по заказу набега вернулось или заказ снят по сроку.</item>
    /// <item>Новый год (и первый такт игры) — расписание кризисов на год (<see cref="CrisisService.PlanYear"/>).</item>
    /// <item>Каждый час — по расписанию: объявить (за <c>warningDays</c>), начать, кончить зиму и мор.</item>
    /// <item>В 00:00, пока идёт мор, — сутки мора.</item>
    /// </list>
    /// Без определений или с закрытой гильдией — ничего.
    /// </summary>
    public sealed class CrisisSystem : ISimSystem
    {
        public string Name => nameof(CrisisSystem);

        public void Tick(SimContext ctx)
        {
            if (!ctx.Data.HasDefinitions || ctx.World.Treasury.IsClosed) return;

            ApplyEvents(ctx);

            CrisisBook book = ctx.World.Crises;
            GameTime time = ctx.World.Time;
            if (time.Year > book.PlannedYear) CrisisService.PlanYear(ctx, time.Year);

            long now = time.TotalHours;
            var all = book.All;
            for (int i = 0; i < all.Count; i++)
            {
                Crisis crisis = all[i];
                if (crisis.Status == CrisisStatus.Planned && crisis.Kind != CrisisKind.Plague && now >= crisis.WarnAtHours && now < crisis.StartsAtHours)
                    CrisisService.Warn(ctx, crisis);
                if ((crisis.Status == CrisisStatus.Planned || crisis.Status == CrisisStatus.Warned) && now >= crisis.StartsAtHours)
                    CrisisService.Start(ctx, crisis);
                if (crisis.IsActive && (crisis.Kind == CrisisKind.Winter || crisis.Kind == CrisisKind.Plague) && now >= crisis.EndsAtHours)
                    CrisisService.End(ctx, crisis);
            }

            if (time.Hour == 0 && book.TryGetActive(CrisisKind.Plague, out Crisis plague) && now > plague.StartsAtHours)
                CrisisService.PlagueDay(ctx, plague);
        }

        /// <summary>События такта до этой системы: вернулось задание по заказу набега или заказ набега снят по сроку.</summary>
        private static void ApplyEvents(SimContext ctx)
        {
            var events = ctx.Events.Events;
            int count = events.Count;
            for (int i = 0; i < count; i++)
            {
                SimEvent simEvent = events[i];
                if (simEvent.Type != SimEventType.QuestReturned && simEvent.Type != SimEventType.OrderExpired) continue;
                if (!simEvent.TryGet("order", out int orderId) || !ctx.World.Crises.TryGetByOrder(orderId, out Crisis crisis)) continue;
                bool won = simEvent.Type == SimEventType.QuestReturned && simEvent.TryGet("done", out bool done) && done;
                CrisisService.RaidOutcome(ctx, crisis, won);
            }
        }
    }
}
