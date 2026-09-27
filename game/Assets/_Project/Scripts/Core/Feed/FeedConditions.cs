using System;
using System.Collections.Generic;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>Что видит условие шаблона: событие, его первый участник (может не быть) и данные.</summary>
    public readonly struct FeedConditionContext
    {
        public FeedConditionContext(SimEvent simEvent, Adventurer subject, DataRegistry data)
        {
            Event = simEvent;
            Subject = subject;
            Data = data;
        }

        public SimEvent Event { get; }
        public Adventurer Subject { get; }
        public DataRegistry Data { get; }
    }

    /// <summary>
    /// Условия шаблонов ленты: вид условия → проверка по событию. Новое условие — значение
    /// <see cref="FeedConditionKind"/> и строка в <see cref="Checks"/>. Условие без проверки не выполняется.
    /// <list type="bullet">
    /// <item><c>Archetype</c> — архетип первого участника;</item>
    /// <item><c>RevealedAxisPole</c>, <c>RevealedTrait</c> — черта первого участника уже раскрыта: строка, которая называет
    /// черту прямо, не выдаёт скрытое;</item>
    /// <item><c>MaimedStat</c> — какая характеристика снижена у Калеки (данные события <c>stat</c>).</item>
    /// </list>
    /// </summary>
    public static class FeedConditions
    {
        private static readonly Dictionary<FeedConditionKind, Func<FeedCondition, FeedConditionContext, bool>> Checks =
            new Dictionary<FeedConditionKind, Func<FeedCondition, FeedConditionContext, bool>>
            {
                { FeedConditionKind.Archetype, IsArchetype },
                { FeedConditionKind.RevealedAxisPole, IsAxisPoleRevealed },
                { FeedConditionKind.RevealedTrait, IsTraitRevealed },
                { FeedConditionKind.MaimedStat, IsMaimedStat },
            };

        /// <summary>У условия этого вида есть проверка.</summary>
        public static bool CanCheck(FeedConditionKind kind) => Checks.ContainsKey(kind);

        public static bool Holds(FeedCondition condition, FeedConditionContext context) =>
            condition != null && Checks.TryGetValue(condition.Kind, out Func<FeedCondition, FeedConditionContext, bool> check) && check(condition, context);

        /// <summary>Все условия шаблона выполнены.</summary>
        public static bool AllHold(FeedTemplate template, FeedConditionContext context)
        {
            foreach (FeedCondition condition in template.Conditions)
            {
                if (!Holds(condition, context)) return false;
            }
            return true;
        }

        /// <summary>
        /// Шаблон, у которого выполнены все условия; из подходящих — самый конкретный (больше условий), при равенстве —
        /// первый по порядку в данных. Нет подходящего — <c>null</c>.
        /// </summary>
        public static FeedTemplate Select(IReadOnlyList<FeedTemplate> templates, FeedConditionContext context)
        {
            FeedTemplate best = null;
            foreach (FeedTemplate template in templates)
            {
                if (template == null || !AllHold(template, context)) continue;
                if (best == null || template.Conditions.Count > best.Conditions.Count) best = template;
            }
            return best;
        }

        private static bool IsArchetype(FeedCondition condition, FeedConditionContext context) =>
            context.Subject != null && condition.Archetype != null && context.Subject.ArchetypeId == condition.Archetype.Id;

        private static bool IsAxisPoleRevealed(FeedCondition condition, FeedConditionContext context)
        {
            Adventurer subject = context.Subject;
            if (subject == null || !subject.IsAxisRevealed(condition.Axis)) return false;
            float value = subject.GetAxis(condition.Axis);
            return !AxisMath.IsNeutral(value, context.Data.Balance.Adventurers) && AxisMath.PoleOf(value) == condition.Pole;
        }

        private static bool IsTraitRevealed(FeedCondition condition, FeedConditionContext context) =>
            context.Subject != null && condition.Trait != null
            && context.Subject.TryGetTrait(condition.Trait.Id, out TraitInstance trait) && trait.Revealed;

        private static bool IsMaimedStat(FeedCondition condition, FeedConditionContext context) =>
            context.Event.TryGet("stat", out StatId stat) && stat == condition.Stat;
    }
}
