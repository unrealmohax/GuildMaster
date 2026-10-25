using System.Collections.Generic;

namespace GuildMaster.Core
{
    /// <summary>Почему гильдия закрыта (поражение).</summary>
    public enum ClosedCause
    {
        /// <summary>Банкротство: срок на исправление истёк, а казна в минусе.</summary>
        Bankruptcy,

        /// <summary>Поселение покинуто жителями: гильдии некого защищать.</summary>
        SettlementAbandoned,
    }

    /// <summary>
    /// Общий путь закрытия гильдии — по любой причине. Закрытая гильдия (<see cref="Treasury.IsClosed"/>) останавливает
    /// симуляцию: такты и команды больше ничего не делают.
    /// </summary>
    public static class GuildClosure
    {
        /// <summary>
        /// Закрыть гильдию: флаг, час и причина в казне, событие <see cref="SimEventType.GuildClosed"/> [В] с итогами — сколько
        /// прожили, сколько погибло и ушло, деньги, причина (<c>cause</c>); участники — лучшие люди. Уже закрыта — ничего.
        /// </summary>
        public static void Close(SimContext ctx, ClosedCause cause)
        {
            Treasury treasury = ctx.World.Treasury;
            if (treasury.IsClosed) return;

            long now = ctx.World.Time.TotalHours;
            treasury.IsClosed = true;
            treasury.ClosedAtHours = now;
            treasury.ClosedCause = cause;

            AdventurerRoster roster = ctx.World.Adventurers;
            int died = 0;
            int left = 0;
            foreach (Adventurer adventurer in roster.Archive)
            {
                if (adventurer.LeaveReason == LeaveReason.Died) died++;
                else left++;
            }

            int days = (int)((now - ctx.Calendar.StartTotalHours) / ctx.Calendar.HoursPerDay);
            SimEvent closed = ctx.Events.Publish(SimEventType.GuildClosed, EventImportance.Important,
                    BestPeople(roster, ctx.Data.Balance.Economy.ClosedBestPeople).ToArray())
                .With("days", days)
                .With("died", died)
                .With("left", left)
                .With("money", treasury.Money)
                .With("cause", cause);
            closed.WithSettlement(ctx.World.Settlement);
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
