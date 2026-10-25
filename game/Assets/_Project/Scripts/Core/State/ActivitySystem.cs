using System;
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
    /// Расходы занятия — раз в сутки, в первый его час: запой — <c>bingeDrinkPerDay</c> кружек; Лазарет — <c>infirmary</c>, в казну —
    /// сколько заплачено (недостача — недополученный доход гильдии). Вечер в городе (начало ночи, не на задании, не в Лазарете, без
    /// запоя в этот день) — выпивка: <c>eveningDrinks</c> кружек, у Пьяницы <c>drunkardEveningDrinks</c>, и ещё кружка за каждые
    /// <c>drinkStressStep</c> стресса выше <c>drinkStressThreshold</c>; обычный пьёт только сверх резерва на еду, Пьяница — из всего
    /// кошелька. Оплаченные кружки — в доход таверны (<see cref="TreasuryService"/>).
    /// Тренировка — занятие на <c>trainingHoursPerDay</c> часов, плата <c>training</c> в казну при начале; после него человек
    /// решает снова. Утром Пьяница может пропустить день (10%, при стрессе выше 50 — 20%); человек не в духе (довольство ниже
    /// <c>sulkContentment</c>) — с шансом <see cref="StateRules.SulkSkipChance"/> (растёт с недовольством)
    /// провести день без работы: заказов не берёт, свободное время выбирает сам.
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
            bool night = hour == ctx.Data.Balance.Time.NightHour;

            foreach (Adventurer adventurer in ctx.World.Adventurers.Active)
            {
                AdventurerState state = adventurer.State;
                if (state.IsOnQuest()) continue;

                if (state.Breakdown != BreakdownKind.None && now >= state.BreakdownEndsAtHours)
                {
                    state.Breakdown = BreakdownKind.None;
                    state.BreakdownEndsAtHours = 0;
                }

                if (morning)
                {
                    TrySkipDay(ctx, adventurer);
                    TrySulk(ctx, adventurer);
                }

                Activity previous = state.Activity;
                Activity next = Schedule(state, phase, now);
                if (next == Activity.Training) next = Train(ctx, adventurer, previous, now);
                state.Activity = next;
                state.PlannedActivity = null;
                PayForActivity(ctx, adventurer);
                if (night) DrinkEvening(ctx, adventurer);
            }
        }

        /// <summary>Вечер в городе кончился — выпивка (см. описание системы).</summary>
        private static void DrinkEvening(SimContext ctx, Adventurer adventurer)
        {
            AdventurerState state = adventurer.State;
            if (state.InInfirmary || state.DrankToday) return;

            ExpensesBalance expenses = ctx.Data.Balance.Expenses;
            bool drunkard = TraitRules.FindWithHook(adventurer, TraitHook.DrunkardSkipsDay, ctx.Data) != null;
            IntRange range = drunkard ? expenses.DrunkardEveningDrinks : expenses.EveningDrinks;
            int wanted = ctx.Rng.RangeInclusive(range.Min, range.Max)
                + (int)Math.Floor(Math.Max(0f, state.Stress - expenses.DrinkStressThreshold) / expenses.DrinkStressStep);
            int budget = drunkard ? state.Wallet : Math.Max(0, state.Wallet - FoodRules.Reserve(adventurer, ctx.Data.Balance));
            int price = ctx.Rng.RangeInclusive(expenses.Drink.Min, expenses.Drink.Max);
            int portions = price > 0 ? Math.Min(wanted, budget / price) : wanted;
            state.DrankToday = true;
            if (portions <= 0) return;

            WalletService.Pay(ctx, adventurer, portions * price);
            TreasuryService.CountTavernDrink(ctx, portions);
            if (ctx.Log.IsOn(SimLogLevel.Debug))
            {
                ctx.Log.Begin(SimLogLevel.Debug).Append("drink #").Append(adventurer.Id).Append(' ').Append(adventurer.Name).Append(": ")
                    .Append(portions).Append(" of ").Append(wanted).Append(" (wallet=").Append(state.Wallet).Append(')');
                ctx.Log.Commit();
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

        /// <summary>
        /// Не в духе утром: шанс <see cref="StateRules.SulkSkipChance"/> на день без работы — до начала ночи заказов не берёт (запрет модели решений).
        /// Кто и так не работает сегодня (срыв, Лазарет, тяжёлая рана, Пьяница пропускает день), не бросает.
        /// </summary>
        private static void TrySulk(SimContext ctx, Adventurer adventurer)
        {
            AdventurerState state = adventurer.State;
            StateBalance balance = ctx.Data.Balance.State;
            long now = ctx.World.Time.TotalHours;
            if (!StateRules.IsSulking(state, balance) || state.Breakdown != BreakdownKind.None || state.InInfirmary
                || state.IsLaidUpByWound() || now < state.SkipsDayUntilHours) return;
            if (!ctx.RollChance(StateRules.SulkSkipChance(state.Contentment, balance), "sulk-day", adventurer, "contentment", state.Contentment)) return;

            GameTime time = ctx.World.Time;
            state.SulksUntilHours = time.TotalHours - time.Hour + ctx.Data.Balance.Time.NightHour;
            ctx.Events.Publish(SimEventType.SulkingDay, EventImportance.Normal, adventurer.Id)
                .With("contentment", state.Contentment);
        }

        private static void PayForActivity(SimContext ctx, Adventurer adventurer)
        {
            AdventurerState state = adventurer.State;
            ExpensesBalance expenses = ctx.Data.Balance.Expenses;

            switch (state.Activity)
            {
                case Activity.Binge:
                    if (!state.DrankToday)
                    {
                        state.DrankToday = true; // запой — в таверне
                        int price = ctx.Rng.RangeInclusive(expenses.Drink.Min, expenses.Drink.Max);
                        int wanted = WalletService.Coins(ctx.Data.Balance.State.BingeDrinkPerDay);
                        int portions = price > 0 ? Math.Min(wanted, state.Wallet / price) : wanted;
                        if (portions > 0)
                        {
                            WalletService.Pay(ctx, adventurer, portions * price);
                            TreasuryService.CountTavernDrink(ctx, portions);
                        }
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

        /// <summary>Тянет в таверну выпить: стресс выше порога или Пьяница.</summary>
        public static bool WantsToDrink(Adventurer adventurer, DataRegistry data) =>
            adventurer.State.Stress > data.Balance.Expenses.DrinkStressThreshold
            || TraitRules.FindWithHook(adventurer, TraitHook.DrunkardSkipsDay, data) != null;
    }
}
