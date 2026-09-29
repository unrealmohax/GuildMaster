using System.Collections.Generic;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Шаг 11 такта: казна гильдии.
    /// <list type="bullet">
    /// <item>В 00:00 — доход таверны за прошедшие сутки (<see cref="TreasuryService.SettleTavern"/>).</item>
    /// <item>Каждый час — банкротство: казна в минусе <c>bankruptcyStartDays</c> суток подряд — начало банкротства ([В],
    /// автопауза) и срок <c>bankruptcyMonths</c> месяцев; казна не в минусе — банкротство снято; срок истёк, а казна
    /// в минусе — гильдия закрыта (<see cref="SimEventType.GuildClosed"/>), дальше симуляция не идёт.</item>
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
                    Close(ctx);
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

        /// <summary>Гильдия закрыта: итоги — сколько прожили, сколько погибло и ушло, лучшие люди (участники события).</summary>
        private static void Close(SimContext ctx)
        {
            Treasury treasury = ctx.World.Treasury;
            long now = ctx.World.Time.TotalHours;
            treasury.IsClosed = true;
            treasury.ClosedAtHours = now;

            AdventurerRoster roster = ctx.World.Adventurers;
            int died = 0;
            int left = 0;
            foreach (Adventurer adventurer in roster.Archive)
            {
                if (adventurer.LeaveReason == LeaveReason.Died) died++;
                else left++;
            }

            int days = (int)((now - ctx.Calendar.StartTotalHours) / ctx.Calendar.HoursPerDay);
            ctx.Events.Publish(SimEventType.GuildClosed, EventImportance.Important, BestPeople(roster, ctx.Data.Balance.Economy.ClosedBestPeople).ToArray())
                .With("days", days)
                .With("died", died)
                .With("left", left)
                .With("money", treasury.Money);
        }

        /// <summary>
        /// Лучшие люди за игру (в гильдии и в архиве, кроме погибших): больше выполненных заданий, затем выше ранг гильдии
        /// и оценка лучшей роли. Только чтение — этим же правилом итоги закрытия показывает интерфейс.
        /// </summary>
        public static List<int> BestPeople(AdventurerRoster roster, int count)
        {
            var people = new List<Adventurer>(roster.Active);
            foreach (Adventurer adventurer in roster.Archive)
            {
                if (adventurer.LeaveReason != LeaveReason.Died) people.Add(adventurer);
            }
            people.Sort((a, b) =>
            {
                int byQuests = b.QuestsCompleted.CompareTo(a.QuestsCompleted);
                if (byQuests != 0) return byQuests;
                int byRank = b.GuildRank.CompareTo(a.GuildRank);
                if (byRank != 0) return byRank;
                int byPower = b.PowerScore.CompareTo(a.PowerScore);
                return byPower != 0 ? byPower : a.Id.CompareTo(b.Id);
            });

            var best = new List<int>(count);
            for (int i = 0; i < people.Count && i < count; i++) best.Add(people[i].Id);
            return best;
        }
    }
}
