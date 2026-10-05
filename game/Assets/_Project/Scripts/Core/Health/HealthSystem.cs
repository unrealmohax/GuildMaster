using System;
using System.Collections.Generic;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Шаг 7 такта: койки Лазарета (каждый час) и лечение (раз в сутки, 00:00).
    /// <list type="bullet">
    /// <item>Койка держится до выздоровления. Лечит только Лазарет с Лекарем (<see cref="IInfirmary"/>): Лекаря нет или
    /// коек стало меньше — лишние больные выходят (остаются тяжёлые, при равенстве — кто раньше ранен).</item>
    /// <item>Тяжело раненого (он сам не решает; кроме того, кому гильдия разрешила ходить с раной на задания) кладут на свободную койку в тот же час: тяжёлые в очереди — кто раньше ранен,
    /// затем по Id. Легко раненый ложится сам — решением (<see cref="HealthService.Admit"/>). Тяжёлый не вытесняет лёгкого,
    /// а ждёт свободную койку. Люди на задании коек не занимают.</item>
    /// <item>У каждой раны срок уменьшается на 1 × множитель: в Лазарете — 1 / 0,7 × скорость Лекаря, без него — 1 / 1,5.</item>
    /// <item>Зажила тяжёлая рана — разрешение ходить с ней на задания снимается.</item>
    /// <item>Тяжёлая рана без Лазарета — один бросок на осложнение (в первые сутки лечения): +7 дней, стресс +10.</item>
    /// </list>
    /// </summary>
    public sealed class HealthSystem : ISimSystem
    {
        /// <summary>Остаток срока, который считается нулём (накопление дробных шагов в float).</summary>
        private const float HealedEpsilon = 1e-3f;

        private readonly IInfirmary infirmary;
        private readonly List<Adventurer> patients = new List<Adventurer>();
        private readonly List<Adventurer> waiting = new List<Adventurer>();

        public HealthSystem() : this(BuildingInfirmary.Instance)
        {
        }

        /// <summary>Свой запрос к Лазарету — для тестов.</summary>
        public HealthSystem(IInfirmary infirmary)
        {
            this.infirmary = infirmary ?? throw new ArgumentNullException(nameof(infirmary));
        }

        public string Name => nameof(HealthSystem);

        public void Tick(SimContext ctx)
        {
            UpdateBeds(ctx);
            if (ctx.World.Time.Hour != 0) return;

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
                        if (!state.InInfirmary && ctx.RollChance(health.ComplicationChance, "complication", adventurer)) Complicate(ctx, adventurer, condition);
                    }

                    condition.RemainingDays -= step;
                    if (condition.RemainingDays > HealedEpsilon) continue;

                    state.RemoveCondition(condition);
                    if (condition.Kind == ConditionKind.HeavyWound) state.WoundedQuestProfile = 0f;
                    ctx.Events.Publish(SimEventType.WoundHealed, EventImportance.Normal, adventurer.Id)
                        .With("kind", condition.Kind)
                        .With("infirmary", state.InInfirmary);
                }
                if (state.Conditions.Count == 0) state.InInfirmary = false;
            }
        }

        /// <summary>Свободных коек сейчас (Лазарет без Лекаря коек не даёт).</summary>
        public static int FreeBeds(SimContext ctx, IInfirmary infirmary)
        {
            int beds = infirmary.HasMedic(ctx) ? infirmary.Beds(ctx) : 0;
            return beds - BuildingRules.Patients(ctx.World);
        }

        /// <summary>
        /// Коек меньше, чем больных (нет Лекаря, нет Лазарета), — лишние выходят; есть свободные — ложатся тяжело раненые.
        /// </summary>
        private void UpdateBeds(SimContext ctx)
        {
            int beds = infirmary.HasMedic(ctx) ? infirmary.Beds(ctx) : 0;
            patients.Clear();
            waiting.Clear();
            foreach (Adventurer adventurer in ctx.World.Adventurers.Active)
            {
                AdventurerState state = adventurer.State;
                if (state.InInfirmary) patients.Add(adventurer);
                else if (beds > 0 && state.IsLaidUpByWound() && !state.IsOnQuest()) waiting.Add(adventurer);
            }

            int occupied = patients.Count;
            if (occupied > beds)
            {
                patients.Sort(ByPriority);
                for (int i = beds; i < patients.Count; i++) patients[i].State.InInfirmary = false;
                occupied = beds;
            }

            if (waiting.Count == 0 || occupied >= beds) return;
            waiting.Sort(ByPriority);
            for (int i = 0; i < waiting.Count && occupied < beds; i++, occupied++) HealthService.Admit(ctx, waiting[i], self: false);
        }

        /// <summary>Очередь на койку: тяжёлые раны первыми, при равенстве — кто раньше ранен, затем по Id.</summary>
        private static int ByPriority(Adventurer a, Adventurer b)
        {
            int bySeverity = b.State.HasHeavyWound().CompareTo(a.State.HasHeavyWound());
            if (bySeverity != 0) return bySeverity;
            int byTime = FirstWoundedAt(a).CompareTo(FirstWoundedAt(b));
            return byTime != 0 ? byTime : a.Id.CompareTo(b.Id);
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
