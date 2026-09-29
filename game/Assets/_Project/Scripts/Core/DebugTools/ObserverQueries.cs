using System;
using System.Collections.Generic;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Вопросы к миру для интерфейса: считают из готового состояния, ничего не меняют и случайность не тратят.
    /// </summary>
    public static class ObserverQueries
    {
        /// <summary>
        /// Когда группа вернётся (в <see cref="GameTime.TotalHours"/>): только на обратном пути, с ночлегами; иначе — нет
        /// (на месте число раундов не известно заранее).
        /// </summary>
        public static bool TryGetReturnAtHours(QuestRun run, WorldState world, DayRhythm rhythm, out long atHours)
        {
            atHours = 0;
            if (run == null || run.Phase != QuestPhase.TravelBack) return false;
            atHours = rhythm.MarchEnd(world.Time.TotalHours, Math.Max(0, run.PhaseHoursLeft));
            return true;
        }

        /// <summary>Доля пройденной фазы задания, 0..1.</summary>
        public static float PhaseProgress(QuestRun run)
        {
            if (run == null || run.PhaseHours <= 0) return run != null && run.Phase == QuestPhase.Returned ? 1f : 0f;
            return Math.Max(0f, Math.Min(1f, 1f - (float)run.PhaseHoursLeft / run.PhaseHours));
        }
    }

    /// <summary>
    /// Скрытое, которое видно только в отладке («Раскрыть всё»): профиль заказа и шанс раунда идущего задания. Игровые экраны
    /// это не показывают.
    /// </summary>
    public static class DebugQueries
    {
        /// <summary>Копия скрытого профиля требований заказа (по осям <see cref="StatId"/>).</summary>
        public static float[] OrderProfile(Order order) => order?.Profile != null ? (float[])order.Profile.Clone() : Array.Empty<float>();

        /// <summary>
        /// Шанс раунда задания сейчас: перекрытие профиля группы с требованиями плюс синергии, без случайности. Нет заказа или
        /// никого на задании — 0.
        /// </summary>
        public static float RoundChance(QuestRun run, WorldState world, DataRegistry data)
        {
            if (run == null || !world.Orders.TryGetOrder(run.OrderId, out Order order)) return 0f;
            List<Adventurer> present = QuestParty.Present(world, run);
            if (present.Count == 0) return 0f;

            float[] group = QuestMath.GroupProfile(present, data, run.Panicked, run.Rushing);
            if (QuestMath.CeilingHit(order, present.Count, group)) return 0f;
            float overlap = QuestMath.Overlap(data.Stats.RadarOrder, order.Profile, group);
            float synergy = QuestMath.Synergy(present, world.Relations, data);
            return Math.Max(0f, Math.Min(1f, overlap + synergy));
        }
    }
}
