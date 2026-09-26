using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Шаг 5 такта: занятия людей в гильдии и их расходы. До ТЗ 06 занятие задаёт расписание-заглушка по фазам дня
    /// (решение 2026-09-26), первое подходящее:
    /// <list type="number">
    /// <item>на задании — не трогает (ТЗ 09);</item>
    /// <item>срыв: запой — <see cref="Activity.Binge"/>, «сел и не смог подняться» — <see cref="Activity.Resting"/>, весь срок;</item>
    /// <item>ночь — <see cref="Activity.Sleeping"/>;</item>
    /// <item>койка в Лазарете — <see cref="Activity.Infirmary"/>; тяжёлая рана — <see cref="Activity.Resting"/>;</item>
    /// <item>Пьяница, пропускающий день, — <see cref="Activity.Tavern"/> с утра;</item>
    /// <item>вечер — <see cref="Activity.Tavern"/>, утро и день — <see cref="Activity.Resting"/>.</item>
    /// </list>
    /// Расходы занятия — раз в сутки, в первый его час: таверна — выпивка 2–5 (❔ если стресс выше 30 или Пьяница) и еда
    /// в таверне (если кошелёк не меньше расходов на неделю; платится в 00:00); запой — выпивка <c>bingeDrinkPerDay</c>;
    /// Лазарет — <c>infirmary</c>. Утром Пьяница может пропустить день (❔ 10%, при стрессе выше 50 — 20%).
    /// </summary>
    public sealed class ActivitySystem : ISimSystem
    {
        public string Name => nameof(ActivitySystem);

        public void Tick(SimContext ctx)
        {
            long now = ctx.World.Time.TotalHours;
            int hour = ctx.World.Time.Hour;
            DayPhase phase = ctx.Rhythm.PhaseAt(hour);
            bool morning = hour == ctx.Data.Balance.Time.MorningHour;

            foreach (Adventurer adventurer in ctx.World.Adventurers.Active)
            {
                AdventurerState state = adventurer.State;
                if (state.IsOnQuest()) continue;

                if (state.Breakdown != BreakdownKind.None && now >= state.BreakdownEndsAtHours)
                {
                    state.Breakdown = BreakdownKind.None;
                    state.BreakdownEndsAtHours = 0;
                }

                if (morning) TrySkipDay(ctx, adventurer);

                state.Activity = Schedule(ctx, state, phase, now);
                PayForActivity(ctx, adventurer);
            }
        }

        private static Activity Schedule(SimContext ctx, AdventurerState state, DayPhase phase, long now)
        {
            if (state.Breakdown == BreakdownKind.Binge) return Activity.Binge;
            if (state.Breakdown == BreakdownKind.Collapse) return Activity.Resting;
            if (phase == DayPhase.Night) return Activity.Sleeping;
            if (state.InInfirmary) return Activity.Infirmary;
            if (state.HasHeavyWound()) return Activity.Resting;
            if (now < state.SkipsDayUntilHours) return Activity.Tavern;
            return phase == DayPhase.Evening ? Activity.Tavern : Activity.Resting;
        }

        /// <summary>Пьяница утром: шанс провести день в таверне. Раскрывает черту («Пропущенный день»).</summary>
        private static void TrySkipDay(SimContext ctx, Adventurer adventurer)
        {
            AdventurerState state = adventurer.State;
            TraitInstance drunkard = TraitRules.FindWithHook(adventurer, TraitHook.DrunkardSkipsDay, ctx.Data);
            if (drunkard == null || state.Breakdown != BreakdownKind.None || state.InInfirmary || state.HasHeavyWound()) return;

            TraitsBalance traits = ctx.Data.Balance.Traits;
            float chance = state.Stress > traits.DrunkardStressThreshold ? traits.DrunkardSkipChanceStressed : traits.DrunkardSkipChance;
            if (!ctx.Rng.Chance(chance)) return;

            GameTime time = ctx.World.Time;
            state.SkipsDayUntilHours = time.TotalHours - time.Hour + ctx.Data.Balance.Time.NightHour;
            ctx.Events.Publish(SimEventType.DrunkardSkippedDay, EventImportance.Normal, adventurer.Id);
            RevealService.TryRevealTrait(ctx, adventurer, drunkard.TraitId, RevealTrigger.DrunkardSkippedDay);
        }

        private static void PayForActivity(SimContext ctx, Adventurer adventurer)
        {
            AdventurerState state = adventurer.State;
            ExpensesBalance expenses = ctx.Data.Balance.Expenses;

            switch (state.Activity)
            {
                case Activity.Tavern:
                    if (!state.AteInTavernToday && state.Wallet >= WalletService.WeeklyExpenses(adventurer, expenses))
                        state.AteInTavernToday = true;
                    if (!state.DrankToday && WantsToDrink(ctx, adventurer))
                    {
                        state.DrankToday = true;
                        WalletService.Pay(ctx, adventurer, ctx.Rng.RangeInclusive(expenses.Drink.Min, expenses.Drink.Max));
                    }
                    break;
                case Activity.Binge:
                    if (!state.DrankToday)
                    {
                        state.DrankToday = true;
                        WalletService.Pay(ctx, adventurer, WalletService.Coins(ctx.Data.Balance.State.BingeDrinkPerDay));
                    }
                    break;
                case Activity.Infirmary:
                    if (!state.PaidInfirmaryToday)
                    {
                        state.PaidInfirmaryToday = true;
                        WalletService.Pay(ctx, adventurer, WalletService.Coins(expenses.Infirmary)); // ❔ ТЗ 10: недостачу покрывает гильдия
                    }
                    break;
            }
        }

        private static bool WantsToDrink(SimContext ctx, Adventurer adventurer) =>
            adventurer.State.Stress > ctx.Data.Balance.Expenses.DrinkStressThreshold
            || TraitRules.FindWithHook(adventurer, TraitHook.DrunkardSkipsDay, ctx.Data) != null;
    }
}
