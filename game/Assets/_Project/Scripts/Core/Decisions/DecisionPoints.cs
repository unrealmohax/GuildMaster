using System;
using System.Collections.Generic;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    public enum DecisionPointKind
    {
        /// <summary>Начало ночи: сон, без выбора.</summary>
        Night,

        /// <summary>Начало вечера: таверна или отдых.</summary>
        Evening,

        /// <summary>Утро, раз в сутки: что делать сегодня.</summary>
        Morning,

        /// <summary>Человек освободился (кончился срыв, зажила рана, вступил в гильдию): что делать дальше, если не ночь.</summary>
        Freed,
    }

    /// <summary>Точка решения: когда наступает для свободного человека и какие варианты в ней есть.</summary>
    public sealed class DecisionPoint
    {
        public DecisionPoint(DecisionPointKind kind, Func<SimContext, Adventurer, bool> isDue, IReadOnlyList<DecisionAction> options,
            Action<SimContext, Adventurer> onDecided = null)
        {
            Kind = kind;
            IsDue = isDue;
            Options = options;
            OnDecided = onDecided;
        }

        public DecisionPointKind Kind { get; }

        /// <summary>Точка наступила для этого человека (он уже свободен).</summary>
        public Func<SimContext, Adventurer, bool> IsDue { get; }

        public IReadOnlyList<DecisionAction> Options { get; }

        /// <summary>Что отметить после решения (утро — «сегодня уже решал»).</summary>
        public Action<SimContext, Adventurer> OnDecided { get; }
    }

    /// <summary>
    /// Точки решения по порядку: в час срабатывает первая наступившая. Новая точка — строка в <see cref="Default"/>.
    /// <list type="bullet">
    /// <item>Ночь — в час начала ночи, сон без выбора.</item>
    /// <item>Вечер — в час начала вечера.</item>
    /// <item>Утро — в первый утренний час, когда человек свободен, раз в сутки (обычно в начале утра; в первый день игры
    /// и после срыва — позже).</item>
    /// <item>Освободился — в прошлом часу человек не был свободен, а сейчас свободен и не ночь; сюда же — только что
    /// вступивший.</item>
    /// </list>
    /// </summary>
    public static class DecisionPoints
    {
        private static readonly DecisionAction[] RestOrTavern = { DecisionActions.Rest, DecisionActions.Tavern };

        public static IReadOnlyList<DecisionPoint> Default { get; } = new[]
        {
            new DecisionPoint(DecisionPointKind.Night,
                (ctx, a) => ctx.World.Time.Hour == ctx.Data.Balance.Time.NightHour,
                new[] { DecisionActions.Sleep }),
            new DecisionPoint(DecisionPointKind.Evening,
                (ctx, a) => ctx.World.Time.Hour == ctx.Data.Balance.Time.EveningHour,
                RestOrTavern),
            new DecisionPoint(DecisionPointKind.Morning,
                (ctx, a) => ctx.Rhythm.PhaseAt(ctx.World.Time.Hour) == DayPhase.Morning && a.State.MorningDecisionDay != Today(ctx),
                RestOrTavern,
                (ctx, a) => a.State.MorningDecisionDay = Today(ctx)),
            new DecisionPoint(DecisionPointKind.Freed,
                (ctx, a) => !a.State.WasFreeLastHour && ctx.Rhythm.PhaseAt(ctx.World.Time.Hour) != DayPhase.Night,
                RestOrTavern),
        };

        /// <summary>Номер суток от начала календаря.</summary>
        public static long Today(SimContext ctx) => ctx.World.Time.TotalHours / ctx.Calendar.HoursPerDay;

        /// <summary>
        /// Человек сам решает, чем заняться: не на задании, не в запое и не «сел и не смог подняться», не в Лазарете,
        /// без тяжёлой раны, не пропускает день (Пьяница). Отказ от заданий решениям о свободном времени не мешает.
        /// </summary>
        public static bool CanDecide(AdventurerState state, long now) =>
            !state.IsOnQuest()
            && state.Breakdown != BreakdownKind.Binge && state.Breakdown != BreakdownKind.Collapse
            && !state.InInfirmary && !state.HasHeavyWound()
            && now >= state.SkipsDayUntilHours;
    }

    /// <summary>Запрет варианта: правило и причина для лога. Новый запрет — строка в <see cref="DecisionBans.Default"/>.</summary>
    public sealed class DecisionBan
    {
        public DecisionBan(string reason, Func<SimContext, Adventurer, DecisionAction, bool> applies)
        {
            Reason = reason;
            Applies = applies;
        }

        /// <summary>Причина запрета для лога.</summary>
        public string Reason { get; }

        /// <summary>Вариант запрещён этому человеку сейчас.</summary>
        public Func<SimContext, Adventurer, DecisionAction, bool> Applies { get; }
    }

    /// <summary>Запреты вариантов. Утром и днём таверна — только Пьянице или при стрессе выше <c>daytimeTavernStress</c>.</summary>
    public static class DecisionBans
    {
        public static IReadOnlyList<DecisionBan> Default { get; } = new[]
        {
            new DecisionBan("tavern in daytime: not a drunkard, stress not above daytimeTavernStress", IsDaytimeTavern),
        };

        private static bool IsDaytimeTavern(SimContext ctx, Adventurer adventurer, DecisionAction action)
        {
            if (action.Kind != DecisionActionKind.Tavern) return false;
            DayPhase phase = ctx.Rhythm.PhaseAt(ctx.World.Time.Hour);
            if (phase != DayPhase.Morning && phase != DayPhase.Day) return false;
            if (TraitRules.FindWithHook(adventurer, TraitHook.DrunkardSkipsDay, ctx.Data) != null) return false;
            return adventurer.State.Stress <= ctx.Data.Balance.Decisions.DaytimeTavernStress;
        }
    }
}
