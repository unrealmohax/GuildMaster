using System;
using System.Collections.Generic;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Шаг 7 такта: раз в сутки (00:00) — койки Лазарета, лечение, осложнения.
    /// <list type="bullet">
    /// <item>Койки: тяжёлые раны в приоритете, при равенстве — кто раньше ранен. Лечит только Лазарет с Лекарем
    /// (<see cref="IInfirmary"/>; построек пока нет — <see cref="NoInfirmary"/>). Люди на задании коек не занимают.</item>
    /// <item>У каждой раны срок уменьшается на 1 × множитель: в Лазарете — 1 / 0,7 × скорость Лекаря, без него — 1 / 1,5.</item>
    /// <item>Тяжёлая рана без Лазарета — один бросок на осложнение (в первые сутки лечения): +7 дней, стресс +10.</item>
    /// </list>
    /// </summary>
    public sealed class HealthSystem : ISimSystem
    {
        /// <summary>Остаток срока, который считается нулём (накопление дробных шагов в float).</summary>
        private const float HealedEpsilon = 1e-3f;

        private readonly IInfirmary infirmary;

        public HealthSystem() : this(NoInfirmary.Instance)
        {
        }

        /// <summary>Свой запрос к Лазарету — для тестов и для построек.</summary>
        public HealthSystem(IInfirmary infirmary)
        {
            this.infirmary = infirmary ?? throw new ArgumentNullException(nameof(infirmary));
        }

        public string Name => nameof(HealthSystem);

        public void Tick(SimContext ctx)
        {
            if (ctx.World.Time.Hour != 0) return;

            AssignBeds(ctx);

            HealthBalance health = ctx.Data.Balance.Health;
            float speed = infirmary.HealingSpeed(ctx);
            foreach (Adventurer adventurer in ctx.World.Adventurers.Active)
            {
                AdventurerState state = adventurer.State;
                if (state.Conditions.Count == 0) continue;

                float step = state.InInfirmary ? speed / health.InfirmaryTimeMultiplier : 1f / health.NoInfirmaryTimeMultiplier;
                foreach (Condition condition in new List<Condition>(state.Conditions))
                {
                    if (condition.Kind == ConditionKind.HeavyWound && !condition.ComplicationChecked)
                    {
                        condition.ComplicationChecked = true;
                        if (!state.InInfirmary && ctx.Rng.Chance(health.ComplicationChance)) Complicate(ctx, adventurer, condition);
                    }

                    condition.RemainingDays -= step;
                    if (condition.RemainingDays > HealedEpsilon) continue;

                    state.RemoveCondition(condition);
                    ctx.Events.Publish(SimEventType.WoundHealed, EventImportance.Normal, adventurer.Id)
                        .With("kind", condition.Kind);
                }
                if (state.Conditions.Count == 0) state.InInfirmary = false;
            }
        }

        /// <summary>Раздать койки на сутки: тяжёлые раны первыми, при равенстве — кто раньше ранен, затем по Id.</summary>
        private void AssignBeds(SimContext ctx)
        {
            var wounded = new List<Adventurer>();
            foreach (Adventurer adventurer in ctx.World.Adventurers.Active)
            {
                adventurer.State.InInfirmary = false;
                if (adventurer.State.Conditions.Count > 0 && !adventurer.State.IsOnQuest()) wounded.Add(adventurer);
            }

            int beds = infirmary.HasMedic(ctx) ? infirmary.Beds(ctx) : 0;
            if (beds <= 0 || wounded.Count == 0) return;

            wounded.Sort((a, b) =>
            {
                int bySeverity = b.State.HasHeavyWound().CompareTo(a.State.HasHeavyWound());
                if (bySeverity != 0) return bySeverity;
                int byTime = FirstWoundedAt(a).CompareTo(FirstWoundedAt(b));
                return byTime != 0 ? byTime : a.Id.CompareTo(b.Id);
            });
            for (int i = 0; i < wounded.Count && i < beds; i++) wounded[i].State.InInfirmary = true;
        }

        /// <summary>Когда получена самая тяжёлая из ран человека (при нескольких — самая ранняя).</summary>
        private static long FirstWoundedAt(Adventurer adventurer)
        {
            AdventurerState state = adventurer.State;
            ConditionKind worst = state.HasHeavyWound() ? ConditionKind.HeavyWound : ConditionKind.LightWound;
            long first = long.MaxValue;
            foreach (Condition condition in state.Conditions)
            {
                if (condition.Kind == worst) first = Math.Min(first, condition.InflictedAtHours);
            }
            return first;
        }

        private static void Complicate(SimContext ctx, Adventurer adventurer, Condition condition)
        {
            HealthBalance health = ctx.Data.Balance.Health;
            condition.IsComplicated = true;
            condition.Days += health.ComplicationExtraDays;
            condition.RemainingDays += health.ComplicationExtraDays;
            StateService.AddStress(ctx, adventurer, health.ComplicationStress);

            ctx.Events.Publish(SimEventType.WoundComplicated, EventImportance.Notable, adventurer.Id)
                .With("extraDays", health.ComplicationExtraDays);
        }
    }
}
