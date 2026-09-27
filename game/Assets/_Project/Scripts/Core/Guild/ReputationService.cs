using System;

namespace GuildMaster.Core
{
    /// <summary>
    /// Единственный путь изменить репутацию гильдии: на Δ с причиной. Значение обрезается до 0..<c>maxReputation</c>;
    /// изменение — событие <see cref="SimEventType.ReputationChanged"/> (в лог, без строки ленты). Без изменения — ничего.
    /// </summary>
    public static class ReputationService
    {
        /// <summary>Изменить репутацию на <paramref name="delta"/>. Возвращает, на сколько она изменилась на самом деле.</summary>
        /// <param name="reason">Причина для лога: «quest», «death»…</param>
        public static float Change(SimContext ctx, float delta, string reason)
        {
            GuildState guild = ctx.World.Guild;
            float from = guild.Reputation;
            float to = Math.Min(ctx.Data.Balance.Guild.MaxReputation, Math.Max(0f, from + delta));
            if (to == from) return 0f;

            guild.Reputation = to;
            ctx.Events.Publish(SimEventType.ReputationChanged)
                .With("from", from)
                .With("to", to)
                .With("reason", reason ?? string.Empty);
            return to - from;
        }
    }
}
