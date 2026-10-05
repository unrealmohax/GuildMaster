using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Шаг 5 такта: занятия людей в гильдии и их расходы. Занятие — первое подходящее:
    /// <list type="number">
    /// <item>на задании — не трогает;</item>
    /// <item>срыв: запой — <see cref="Activity.Binge"/>, «сел и не смог подняться» — <see cref="Activity.Resting"/>, весь срок;</item>
    /// <item>ночь — <see cref="Activity.Sleeping"/>;</item>
    /// <item>койка в Лазарете — <see cref="Activity.Infirmary"/>; тяжёлая рана — <see cref="Activity.Resting"/>;</item>
    /// <item>Пьяница, пропускающий день, — <see cref="Activity.Tavern"/> с утра;</item>
    /// <item>иначе человек свободен: занятие, выбранное решением в прошлом часу (<see cref="AdventurerState.PlannedActivity"/>),
    /// иначе прежнее свободное занятие (отдых, таверна, тренировка); после сна, срыва, Лазарета — <see cref="Activity.Resting"/>.</item>
    /// </list>
    /// Выбранное решением занятие ставится только здесь, поэтому за час решения показатели считаются по прежнему занятию.
    /// Расходы занятия — раз в сутки, в первый его час: таверна — выпивка 2–5 (если стресс выше 30 или Пьяница) и еда
    /// в таверне (если кошелёк не меньше расходов на неделю; платится в 00:00); запой — выпивка <c>bingeDrinkPerDay</c>;
    /// Лазарет — <c>infirmary</c>, в казну — сколько заплачено (недостача — недополученный доход гильдии). Выпивка в таверне
    /// и в запое, за которую заплачено хоть что-то, — порция в доход таверны (<see cref="TreasuryService"/>).
    /// Тренировка — занятие на <c>trainingHoursPerDay</c> часов, плата <c>training</c> в казну при начале; после него человек
    /// решает снова. Утром Пьяница может пропустить день (10%, при стрессе выше 50 — 20%).
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

                Activity previous = state.Activity;
                Activity next = Schedule(state, phase, now);
                if (next == Activity.Training) next = Train(ctx, adventurer, previous, now);
                state.Activity = next;
                state.PlannedActivity = null;
                PayForActivity(ctx, adventurer);
            }
        }

        private static Activity Schedule(AdventurerState state, DayPhase phase, long now)
        {
            if (state.Breakdown == BreakdownKind.Binge) return Activity.Binge;
            if (state.Breakdown == BreakdownKind.Collapse) return Activity.Resting;
            if (phase == DayPhase.Night) return Activity.Sleeping;
            if (state.InInfirmary) return Activity.Infirmary;
            if (state.IsLaidUpByWound()) return Activity.Resting;
            if (now < state.SkipsDayUntilHours) return Activity.Tavern;
            if (state.PlannedActivity.HasValue) return state.PlannedActivity.Value;
            return IsFreeTime(state.Activity) ? state.Activity : Activity.Resting;
        }

        /// <summary>
        /// Тренировка на дворе: выбрал её — начинается занятие на <c>trainingHoursPerDay</c> часов (плата за день — в казну,
        /// второй раз за сутки нельзя); занятие кончилось — человек отдыхает и в этом часу решает снова, как освободившийся.
        /// </summary>
        private static Activity Train(SimContext ctx, Adventurer adventurer, Activity previous, long now)
        {
            AdventurerState state = adventurer.State;
            bool chosenNow = state.PlannedActivity == Activity.Training;
            if (previous == Activity.Training && !chosenNow)
            {
                if (now < state.TrainingEndsAtHours) return Activity.Training;
                state.TrainingStat = null;
                state.WasFreeLastHour = false;
                return Activity.Resting;
            }

            state.TrainingEndsAtHours = now + ctx.Data.Balance.Growth.TrainingHoursPerDay;
            state.TrainingStat = null;
            state.TrainedOnDay = DecisionPoints.Today(ctx);
            int paid = WalletService.Pay(ctx, adventurer, WalletService.Coins(ctx.Data.Balance.Expenses.Training));
            TreasuryService.Credit(ctx, LedgerCategories.TrainingYard, paid, "training", adventurer.Id);
            ctx.Events.Publish(SimEventType.TrainingStarted, EventImportance.Normal, adventurer.Id);
            return Activity.Training;
        }

        /// <summary>Занятие свободного человека, которое держится до следующего решения.</summary>
        public static bool IsFreeTime(Activity activity) =>
            activity == Activity.Resting || activity == Activity.Tavern || activity == Activity.Training;

        /// <summary>Пьяница утром: шанс провести день в таверне (при Сухом законе — свой). Раскрывает черту («Пропущенный день»).</summary>
        private static void TrySkipDay(SimContext ctx, Adventurer adventurer)
        {
            AdventurerState state = adventurer.State;
            TraitInstance drunkard = TraitRules.FindWithHook(adventurer, TraitHook.DrunkardSkipsDay, ctx.Data);
            if (drunkard == null || state.Breakdown != BreakdownKind.None || state.InInfirmary || state.HasHeavyWound()) return;

            TraitsBalance traits = ctx.Data.Balance.Traits;
            float chance = state.Stress > traits.DrunkardStressThreshold ? traits.DrunkardSkipChanceStressed : traits.DrunkardSkipChance;
            chance = DecreeRules.DrunkardSkipChance(ctx.World, ctx.Data, chance);
            if (!ctx.RollChance(chance, "drunkard-skip-day", adventurer, "stress", state.Stress)) return;

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
                    if (!state.DrankToday && WantsToDrink(adventurer, ctx.Data))
                    {
                        state.DrankToday = true;
                        if (WalletService.Pay(ctx, adventurer, ctx.Rng.RangeInclusive(expenses.Drink.Min, expenses.Drink.Max)) > 0)
                            TreasuryService.CountTavernDrink(ctx);
                    }
                    break;
                case Activity.Binge:
                    if (!state.DrankToday)
                    {
                        state.DrankToday = true; // запой — в таверне
                        if (WalletService.Pay(ctx, adventurer, WalletService.Coins(ctx.Data.Balance.State.BingeDrinkPerDay)) > 0)
                            TreasuryService.CountTavernDrink(ctx);
                    }
                    break;
                case Activity.Infirmary:
                    if (!state.PaidInfirmaryToday)
                    {
                        state.PaidInfirmaryToday = true;
                        int paid = WalletService.Pay(ctx, adventurer, WalletService.Coins(expenses.Infirmary)); // недостача — недополученный доход
                        TreasuryService.Credit(ctx, LedgerCategories.Infirmary, paid, "infirmary", adventurer.Id);
                    }
                    break;
            }
        }

        /// <summary>Человек в таверне пьёт: стресс выше порога или Пьяница.</summary>
        public static bool WantsToDrink(Adventurer adventurer, DataRegistry data) =>
            adventurer.State.Stress > data.Balance.Expenses.DrinkStressThreshold
            || TraitRules.FindWithHook(adventurer, TraitHook.DrunkardSkipsDay, data) != null;
    }
}
