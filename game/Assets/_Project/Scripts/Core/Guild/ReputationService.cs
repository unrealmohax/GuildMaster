using System;
using System.Globalization;
using System.Text;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Единственный путь изменить репутацию гильдии: на Δ с причиной (<see cref="ReputationReasons"/>), о человеке и заказе.
    /// Значение обрезается до 0..<c>maxReputation</c>; изменение запоминается в <see cref="GuildState.Changes"/> и публикуется
    /// событием <see cref="SimEventType.ReputationChanged"/> (в лог, без строки ленты). Без изменения — ничего.
    /// Здесь же — прибавка за выполненное задание с убывающей отдачей и суточное остывание.
    /// </summary>
    public static class ReputationService
    {
        /// <summary>Изменить репутацию на <paramref name="delta"/>. Возвращает, на сколько она изменилась на самом деле.</summary>
        public static float Change(SimContext ctx, float delta, string reason) => Change(ctx, delta, reason, 0, null);

        /// <summary>
        /// Изменить репутацию на <paramref name="delta"/> по причине, о человеке <paramref name="adventurerId"/> (0 — ни о ком)
        /// и заказе <paramref name="order"/> (может быть <c>null</c>). Возвращает фактическое изменение.
        /// </summary>
        public static float Change(SimContext ctx, float delta, string reason, int adventurerId, Order order)
        {
            GuildState guild = ctx.World.Guild;
            GuildBalance balance = ctx.Data.Balance.Guild;
            float from = guild.Reputation;
            float to = Math.Min(balance.MaxReputation, Math.Max(0f, from + delta));
            if (to == from) return 0f;

            guild.Reputation = to;
            int orderId = order?.Id ?? 0;
            guild.AddChange(new ReputationChange(ctx.World.Time.TotalHours, to - from, reason ?? string.Empty, adventurerId, orderId,
                order?.TypeId, order?.Rank ?? GuildRank.G), balance.RecentReputationChanges);

            SimEvent changed = adventurerId != 0
                ? ctx.Events.Publish(SimEventType.ReputationChanged, EventImportance.Normal, adventurerId)
                : ctx.Events.Publish(SimEventType.ReputationChanged);
            changed.With("from", from)
                .With("to", to)
                .With("reason", reason ?? string.Empty);
            if (orderId != 0) changed.With("order", orderId);
            return to - from;
        }

        /// <summary>
        /// Прибавка за выполненное задание: <c>successReputationPerRank</c> × ранг × (1 − репутация / <c>maxReputation</c>) —
        /// чем выше репутация, тем меньше прибавка. Задание засчитывается для остывания.
        /// </summary>
        public static float QuestDone(SimContext ctx, int rankNumber, Order order)
        {
            GuildState guild = ctx.World.Guild;
            guild.QuestsDoneToday++;
            return Change(ctx, QuestDoneDelta(ctx.Data.Balance.Guild, rankNumber, guild.Reputation), ReputationReasons.QuestDone, 0, order);
        }

        /// <summary>Прибавка за задание ранга <paramref name="rankNumber"/> при репутации <paramref name="reputation"/>.</summary>
        public static float QuestDoneDelta(GuildBalance balance, int rankNumber, float reputation) =>
            balance.SuccessReputationPerRank * rankNumber * Math.Max(0f, 1f - reputation / balance.MaxReputation);

        /// <summary>
        /// Полночь: если с прошлой полуночи гильдия не выполнила ни одного задания (экзамены не в счёт) — репутация остывает
        /// на <c>reputationDecayPerDay</c>. Счёт заданий начинается заново. Гильдия закрыта — ничего.
        /// </summary>
        internal static void Midnight(SimContext ctx)
        {
            GuildState guild = ctx.World.Guild;
            int done = guild.QuestsDoneToday;
            guild.QuestsDoneToday = 0;
            if (ctx.World.Treasury.IsClosed) return;

            if (ctx.Log.IsOn(SimLogLevel.Debug))
            {
                StringBuilder line = ctx.Log.Begin(SimLogLevel.Debug);
                line.Append("reputation day quests=").Append(done.ToString(CultureInfo.InvariantCulture))
                    .Append(" reputation=").Append(guild.Reputation.ToString("0.00", CultureInfo.InvariantCulture));
                ctx.Log.Commit();
            }
            if (done > 0) return;
            Change(ctx, -ctx.Data.Balance.Guild.ReputationDecayPerDay, ReputationReasons.Decay);
        }
    }
}
