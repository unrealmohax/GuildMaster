using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Шаг 11 такта: казна гильдии.
    /// <list type="bullet">
    /// <item>В 00:00 — доход таверны за прошедшие сутки (<see cref="TreasuryService.SettleTavern"/>).</item>
    /// <item>Каждый час — банкротство: казна в минусе <c>bankruptcyStartDays</c> суток подряд — начало банкротства ([В],
    /// автопауза) и срок <c>bankruptcyMonths</c> месяцев; казна не в минусе — банкротство снято; срок истёк, а казна
    /// в минусе — гильдия закрыта (<see cref="GuildClosure"/>), дальше симуляция не идёт.</item>
    /// <item>В начале месяца — трудные времена (<see cref="HardTimes"/>).</item>
    /// </list>
    /// </summary>
    public sealed class EconomySystem : ISimSystem
    {
        public string Name => nameof(EconomySystem);

        public void Tick(SimContext ctx)
        {
            Treasury treasury = ctx.World.Treasury;
            if (treasury.IsClosed) return;

            GameTime time = ctx.World.Time;
            if (time.Hour == 0) TreasuryService.SettleTavern(ctx);

            UpdateBankruptcy(ctx);
            if (treasury.IsClosed) return;

            if (time.Hour == 0 && time.Day == 1) HardTimes.Apply(ctx);
        }

        private static void UpdateBankruptcy(SimContext ctx)
        {
            Treasury treasury = ctx.World.Treasury;
            Bankruptcy bankruptcy = treasury.Bankruptcy;
            EconomyBalance economy = ctx.Data.Balance.Economy;
            long now = ctx.World.Time.TotalHours;

            if (bankruptcy.Active)
            {
                // Казна выходила в плюс (минус начался заново после начала банкротства) — тоже выход из долговой ямы.
                bool recovered = treasury.Money >= 0
                    || !bankruptcy.NegativeSinceHours.HasValue
                    || bankruptcy.NegativeSinceHours.Value > bankruptcy.StartedAtHours;
                if (recovered)
                {
                    bankruptcy.Active = false;
                    ctx.Events.Publish(SimEventType.BankruptcyLifted, EventImportance.Notable).With("money", treasury.Money);
                }
                else if (now >= bankruptcy.EndsAtHours)
                {
                    GuildClosure.Close(ctx, ClosedCause.Bankruptcy);
                    return;
                }
            }

            if (!bankruptcy.Active && bankruptcy.NegativeSinceHours.HasValue
                && now - bankruptcy.NegativeSinceHours.Value >= ctx.Calendar.DaysToHours(economy.BankruptcyStartDays))
            {
                bankruptcy.Active = true;
                bankruptcy.StartedAtHours = now;
                bankruptcy.EndsAtHours = now + ctx.Calendar.HoursPerMonth * economy.BankruptcyMonths;
                ctx.Events.Publish(SimEventType.BankruptcyStarted, EventImportance.Important)
                    .With("money", treasury.Money)
                    .With("endsAt", bankruptcy.EndsAtHours);
            }
        }
    }
}
