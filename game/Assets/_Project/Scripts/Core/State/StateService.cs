using System;
using System.Collections.Generic;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Изменение показателей состояния. Рост и падение стресса и усталости умножаются на эффекты черт
    /// (<see cref="StateRates"/>); разовые изменения довольства и лояльности (дилеммы; Проверенный) — сразу, минуя цель.
    /// Всё обрезается до 0..100. Суточные сдвиги и срывы делает <see cref="StateSystem"/>.
    /// </summary>
    public static class StateService
    {
        /// <summary>Стресс ± <paramref name="amount"/> × множитель черт роста или падения. Возвращает фактическое изменение.</summary>
        public static float AddStress(SimContext ctx, Adventurer adventurer, float amount, PartyContext party = PartyContext.None)
        {
            AdventurerState state = adventurer.State;
            float before = state.Stress;
            state.Stress = StateRules.Clamp(before + Scaled(adventurer, StateStat.Stress, amount, ctx.Data, party));
            return state.Stress - before;
        }

        /// <summary>Усталость ± <paramref name="amount"/> × множитель черт роста или падения (Кошмары — рост × 1,3).</summary>
        public static float AddFatigue(SimContext ctx, Adventurer adventurer, float amount, PartyContext party = PartyContext.None)
        {
            AdventurerState state = adventurer.State;
            float before = state.Fatigue;
            state.Fatigue = StateRules.Clamp(before + Scaled(adventurer, StateStat.Fatigue, amount, ctx.Data, party));
            return state.Fatigue - before;
        }

        /// <summary>Разовое изменение довольства (дилеммы, отмена распоряжения-льготы): сразу, без множителей и цели.</summary>
        public static float ChangeContentment(SimContext ctx, Adventurer adventurer, float delta)
        {
            AdventurerState state = adventurer.State;
            float before = state.Contentment;
            state.Contentment = StateRules.Clamp(before + delta);
            return state.Contentment - before;
        }

        /// <summary>Разовое изменение лояльности (дилеммы, обещания, Проверенный): сразу, без множителей.</summary>
        public static float ChangeLoyalty(SimContext ctx, Adventurer adventurer, float delta)
        {
            AdventurerState state = adventurer.State;
            float before = state.Loyalty;
            state.Loyalty = StateRules.Clamp(before + delta);
            return state.Loyalty - before;
        }

        /// <summary>
        /// Начать срыв этого вида: стресс <c>stressAfterBreakdown</c> (−30), событие [В] без автопаузы. Запой, отказ и
        /// «сел и не смог подняться» длятся свои дни. Драка — сразу: отношения <c>brawlRelation</c> со случайным человеком
        /// в гильдии (не на задании) и один бросок <c>brawlWoundChance</c> — лёгкая рана у обоих. Случайность — поток вызывающего.
        /// Вызывают <see cref="StateSystem"/> (раз в сутки при стрессе выше 80) и задания (срыв Ветерана после тяжёлого задания).
        /// Ветеран войны раскрывается первым срывом.
        /// </summary>
        public static SimEvent StartBreakdown(SimContext ctx, Adventurer adventurer, BreakdownKind kind)
        {
            if (kind == BreakdownKind.None) throw new ArgumentException("Breakdown kind is required", nameof(kind));

            StateBalance balance = ctx.Data.Balance.State;
            AdventurerState state = adventurer.State;
            int days = kind == BreakdownKind.Binge ? balance.BingeDays
                : kind == BreakdownKind.RefuseQuests ? balance.RefuseQuestsDays
                : kind == BreakdownKind.Collapse ? balance.CollapseDays
                : 0;
            long now = ctx.World.Time.TotalHours;

            if (days > 0)
            {
                state.Breakdown = kind;
                state.BreakdownEndsAtHours = now + ctx.Calendar.DaysToHours(days);
                if (kind == BreakdownKind.Binge || kind == BreakdownKind.Collapse) state.SkipsDayUntilHours = 0;
            }

            state.LastBreakdownAtHours = now;
            float stressBefore = state.Stress;
            state.Stress = StateRules.Clamp(state.Stress + balance.StressAfterBreakdown);

            Adventurer opponent = kind == BreakdownKind.Brawl ? PickOpponent(ctx, adventurer) : null;
            SimEvent simEvent = (opponent != null
                    ? ctx.Events.Publish(SimEventType.Breakdown, EventImportance.Important, adventurer.Id, opponent.Id)
                    : ctx.Events.Publish(SimEventType.Breakdown, EventImportance.Important, adventurer.Id))
                .With("kind", kind)
                .With("days", days)
                .With("stressBefore", stressBefore);

            if (opponent != null)
            {
                RelationService.Change(ctx, adventurer.Id, opponent.Id, balance.BrawlRelation);
                if (ctx.RollChance(balance.BrawlWoundChance, "brawl-wound", adventurer))
                {
                    HealthService.Wound(ctx, adventurer, ConditionKind.LightWound);
                    HealthService.Wound(ctx, opponent, ConditionKind.LightWound);
                }
            }

            TraitInstance veteran = TraitRules.FindWithHook(adventurer, TraitHook.VeteranBinge, ctx.Data);
            if (veteran != null) RevealService.TryRevealTrait(ctx, adventurer, veteran.TraitId, RevealTrigger.VeteranFirstTension);
            return simEvent;
        }

        private static Adventurer PickOpponent(SimContext ctx, Adventurer adventurer)
        {
            var others = new List<Adventurer>();
            foreach (Adventurer other in ctx.World.Adventurers.Active)
            {
                if (other.Id != adventurer.Id && !other.State.IsOnQuest()) others.Add(other);
            }
            return others.Count > 0 ? ctx.Rng.Pick(others) : null;
        }

        /// <summary>
        /// Вид срыва по чертам (первое подходящее): Пьяница или Ветеран — запой; Безрассудный (ось ≥ 30) — драка;
        /// Трус (≤ −30) — отказ от заданий; остальные — «сел и не смог подняться».
        /// </summary>
        public static BreakdownKind BreakdownKindOf(Adventurer adventurer, DataRegistry data)
        {
            if (TraitRules.FindWithHook(adventurer, TraitHook.DrunkardSkipsDay, data) != null
                || TraitRules.FindWithHook(adventurer, TraitHook.VeteranBinge, data) != null)
                return BreakdownKind.Binge;

            float risk = adventurer.GetAxis(AxisId.Risk);
            float threshold = data.Balance.State.BreakdownAxisThreshold;
            if (risk >= threshold) return BreakdownKind.Brawl;
            if (risk <= -threshold) return BreakdownKind.RefuseQuests;
            return BreakdownKind.Collapse;
        }

        /// <summary>Стартовые показатели нового человека: усталость 10, стресс 10–30, довольство 50, лояльность 40–60.</summary>
        internal static void InitializeNew(Rng rng, AdventurerState state, StateBalance balance)
        {
            state.Fatigue = StateRules.Clamp(balance.StartFatigue);
            state.Stress = StateRules.Clamp(rng.RangeInclusive(balance.StartStress.Min, balance.StartStress.Max));
            state.Contentment = StateRules.Clamp(balance.StartContentment);
            state.Loyalty = StateRules.Clamp(rng.RangeInclusive(balance.StartLoyalty.Min, balance.StartLoyalty.Max));
        }

        /// <summary>Час занятия: усталость и стресс по таблице занятий × множители черт.</summary>
        internal static void ApplyHour(SimContext ctx, Adventurer adventurer, PartyContext party)
        {
            StateBalance balance = ctx.Data.Balance.State;
            Activity activity = adventurer.State.Activity;
            AddFatigue(ctx, adventurer, StateRules.FatiguePerHour(activity, balance), party);
            AddStress(ctx, adventurer, StateRules.StressPerHour(activity, balance), party);
        }

        /// <summary>Довольство — на <c>contentmentDailyStep</c> × множитель черт к цели, не проскакивая её.</summary>
        internal static void MoveContentment(SimContext ctx, Adventurer adventurer, float target)
        {
            AdventurerState state = adventurer.State;
            state.Contentment = MoveTowards(adventurer, StateStat.Contentment, state.Contentment, target,
                ctx.Data.Balance.State.ContentmentDailyStep, ctx.Data);
        }

        /// <summary>Лояльность — на <c>loyaltyDailyStep</c> × ось «Верность» к довольству, не проскакивая его.</summary>
        internal static void MoveLoyalty(SimContext ctx, Adventurer adventurer)
        {
            AdventurerState state = adventurer.State;
            state.Loyalty = MoveTowards(adventurer, StateStat.Loyalty, state.Loyalty, state.Contentment,
                ctx.Data.Balance.State.LoyaltyDailyStep, ctx.Data);
        }

        private static float MoveTowards(Adventurer adventurer, StateStat stat, float value, float target, float step, DataRegistry data)
        {
            if (value == target) return value;
            RateDirection direction = target > value ? RateDirection.Growth : RateDirection.Decay;
            float delta = step * StateRates.Multiplier(adventurer, stat, direction, data);
            return StateRules.Clamp(direction == RateDirection.Growth ? Math.Min(target, value + delta) : Math.Max(target, value - delta));
        }

        private static float Scaled(Adventurer adventurer, StateStat stat, float amount, DataRegistry data, PartyContext party)
        {
            if (amount == 0f) return 0f;
            RateDirection direction = amount > 0f ? RateDirection.Growth : RateDirection.Decay;
            return amount * StateRates.Multiplier(adventurer, stat, direction, data, party);
        }
    }
}
