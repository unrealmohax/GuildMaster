using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Ранг гильдии (ТЗ 04 → «Ранг гильдии»): очки, порог <c>pointsToNext</c>, готовность к повышению, повышение.
    /// Очки за задания и само задание на повышение — ТЗ 09.
    /// </summary>
    public static class GuildRanks
    {
        /// <summary>Последний ранг прототипа (C): у него нет порога повышения.</summary>
        public static bool IsTopRank(GuildRank rank, RanksBalance ranks) => ranks.For(rank).PointsToNext <= 0;

        /// <summary>Сколько очков нужно для повышения с текущего ранга; 0 — повышаться некуда.</summary>
        public static int PointsToNext(Adventurer adventurer, RanksBalance ranks) => ranks.For(adventurer.GuildRank).PointsToNext;

        /// <summary>
        /// Можно выдать задание на повышение: ранг не последний, очков хватает и прошёл <see cref="Adventurer.PromotionReadyAtHours"/>.
        /// </summary>
        public static bool IsReadyForPromotion(Adventurer adventurer, RanksBalance ranks, long nowHours)
        {
            int needed = PointsToNext(adventurer, ranks);
            return needed > 0 && adventurer.RankPoints >= needed && nowHours >= adventurer.PromotionReadyAtHours;
        }

        /// <summary>Авантюрист берёт заказы своего ранга и ниже.</summary>
        public static bool CanTakeOrder(Adventurer adventurer, GuildRank orderRank) => orderRank <= adventurer.GuildRank;

        public static void AddPoints(SimContext ctx, Adventurer adventurer, float points)
        {
            if (points <= 0f) return;
            adventurer.RankPoints += points;
        }

        /// <summary>Задание на повышение выполнено: ранг +1, ❔ очки обнуляются. На последнем ранге — false.</summary>
        public static bool Promote(SimContext ctx, Adventurer adventurer)
        {
            RanksBalance ranks = ctx.Data.Balance.Ranks;
            if (IsTopRank(adventurer.GuildRank, ranks)) return false;

            GuildRank from = adventurer.GuildRank;
            adventurer.GuildRank = from + 1;
            adventurer.RankPoints = 0f;
            ctx.Events.Publish(SimEventType.RankPromoted, EventImportance.Notable, adventurer.Id)
                .With("from", from)
                .With("to", adventurer.GuildRank);
            return true;
        }

        /// <summary>Провал задания на повышение: следующая попытка через <c>promotionRetryDays</c> (30).</summary>
        public static void FailPromotion(SimContext ctx, Adventurer adventurer)
        {
            adventurer.PromotionReadyAtHours = ctx.World.Time.TotalHours + ctx.Calendar.DaysToHours(ctx.Data.Balance.Ranks.PromotionRetryDays);
        }
    }
}
