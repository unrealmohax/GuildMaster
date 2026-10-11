using System.Collections.Generic;

namespace GuildMaster.Core
{
    /// <summary>
    /// Семья за кадром — перед <see cref="StateSystem"/>, чтобы Семейный платил семье раньше, чем за жильё.
    /// <list type="bullet">
    /// <item>Каждый час: вернувшийся с задания узнаёт вести, которые его ждали.</item>
    /// <item>В 00:00 каждых <c>paymentIntervalDays</c> суток от начала игры: плата семье и нужда, у больных — бросок смерти.</item>
    /// <item>В начале месяца: бросок болезни у каждого здорового родного.</item>
    /// <item>В 00:00: стресс нужды, конец траура.</item>
    /// </list>
    /// Свой поток случайности: броски семьи не сдвигают других систем. Нет родных — бросков нет.
    /// </summary>
    public sealed class FamilySystem : ISimSystem
    {
        public string Name => nameof(FamilySystem);

        public void Tick(SimContext ctx)
        {
            IReadOnlyList<Adventurer> active = ctx.World.Adventurers.Active;
            foreach (Adventurer adventurer in active)
            {
                if (!adventurer.State.IsOnQuest()) FamilyService.DeliverNews(ctx, adventurer);
            }

            GameTime time = ctx.World.Time;
            if (time.Hour != 0) return;

            bool payday = IsPayday(ctx);
            bool monthStarted = time.Day == 1;
            foreach (Adventurer adventurer in new List<Adventurer>(active))
            {
                if (payday)
                {
                    bool paidInFull = FamilyService.PayWeek(ctx, adventurer);
                    FamilyService.WeekOfIllness(ctx, adventurer, paidInFull);
                }
                if (monthStarted) FamilyService.RollIllness(ctx, adventurer);
                FamilyService.ApplyDay(ctx, adventurer);
            }
        }

        /// <summary>Сегодня день платы: прошло кратное <c>paymentIntervalDays</c> число суток от дня начала игры (не сам день начала).</summary>
        public static bool IsPayday(SimContext ctx)
        {
            int hoursPerDay = ctx.Calendar.HoursPerDay;
            long days = ctx.World.Time.TotalHours / hoursPerDay - ctx.Calendar.StartTotalHours / hoursPerDay;
            return days > 0 && days % ctx.Data.Balance.Family.PaymentIntervalDays == 0;
        }
    }
}
