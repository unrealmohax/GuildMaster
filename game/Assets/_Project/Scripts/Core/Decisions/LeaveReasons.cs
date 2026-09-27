using System;
using System.Collections.Generic;
using System.Text;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>Почему человек ушёл сам: главные мотивы и состояние. Значения записаны в событиях — новые только в конец.</summary>
    public enum LeaveCause
    {
        /// <summary>Ни один мотив не выделяется: «ничто его здесь не держит».</summary>
        LowLoyalty,

        /// <summary>Деньги при кошельке меньше расходов на неделю.</summary>
        EmptyWallet,

        /// <summary>Деньги: мало платят (Жадный).</summary>
        LowPay,

        /// <summary>Слава: здесь не вырасти (Амбициозный).</summary>
        Glory,

        /// <summary>Безопасность от черт или стресса: слишком опасно (трус).</summary>
        Danger,

        /// <summary>Безопасность от лёгкой раны: рана ещё не зажила.</summary>
        Wounded,

        /// <summary>Товарищи: не с кем здесь быть (Командный).</summary>
        Companions,

        /// <summary>Отдых: устал.</summary>
        Tired,

        /// <summary>Утешение при ранах: тяжело после ранения.</summary>
        HardAfterWound,

        /// <summary>Утешение после недавнего срыва: тяжело после срыва.</summary>
        HardAfterBreakdown,

        /// <summary>Утешение без явного повода: тяжело на душе.</summary>
        HeavyHeart,
    }

    /// <summary>
    /// Причины ухода: до <c>maxReasons</c> мотивов, у которых вес (черты × состояние) выше веса по умолчанию, от большего
    /// к меньшему; какой текст у мотива — по состоянию (пустой кошелёк, рана, недавний срыв). Нет выделяющегося мотива —
    /// <see cref="LeaveCause.LowLoyalty"/>. Тексты — шаблоны ключей <c>reason.*</c> в данных: условие «черта раскрыта»
    /// выбирает строку, которая называет черту; скрытая черта не называется. Случайных чисел не тратит.
    /// </summary>
    public static class LeaveReasons
    {
        private const float WeightEpsilon = 1e-4f;

        /// <summary>Ключ шаблона текста причины.</summary>
        public static string KeyOf(LeaveCause cause)
        {
            string name = cause.ToString();
            return "reason." + char.ToLowerInvariant(name[0]) + name.Substring(1);
        }

        /// <summary>Ключи всех причин — их шаблоны обязаны быть в данных.</summary>
        public static IReadOnlyList<string> Keys { get; } = BuildKeys();

        public static List<LeaveCause> Of(Adventurer adventurer, DataRegistry data, long nowHours, Calendar calendar)
        {
            MotiveWeights weights = Motives.Weigh(adventurer, data, inTavern: false);
            DecisionsBalance decisions = data.Balance.Decisions;

            var motives = new List<Motive>();
            for (int i = 0; i < Vocabulary.MotiveCount; i++)
            {
                if (weights[(Motive)i] > decisions.DefaultMotiveWeight + WeightEpsilon) motives.Add((Motive)i);
            }
            motives.Sort((a, b) => weights[b] != weights[a] ? weights[b].CompareTo(weights[a]) : a.CompareTo(b));

            var causes = new List<LeaveCause>();
            foreach (Motive motive in motives)
            {
                if (causes.Count >= decisions.MaxReasons) break;
                LeaveCause cause = CauseOf(motive, adventurer, weights, data, nowHours, calendar);
                if (!causes.Contains(cause)) causes.Add(cause);
            }
            if (causes.Count == 0) causes.Add(LeaveCause.LowLoyalty);
            return causes;
        }

        /// <summary>Главная причина — деньги (пустой кошелёк или мало платят).</summary>
        public static bool IsMoney(LeaveCause cause) => cause == LeaveCause.EmptyWallet || cause == LeaveCause.LowPay;

        private static LeaveCause CauseOf(Motive motive, Adventurer adventurer, MotiveWeights weights, DataRegistry data, long nowHours, Calendar calendar)
        {
            AdventurerState state = adventurer.State;
            float defaultWeight = data.Balance.Decisions.DefaultMotiveWeight;
            switch (motive)
            {
                case Motive.Money:
                    return weights.TryGetFactor(Motive.Money, out _, out _) ? LeaveCause.EmptyWallet : LeaveCause.LowPay;
                case Motive.Glory:
                    return LeaveCause.Glory;
                case Motive.Safety:
                    return weights.TraitWeight(Motive.Safety) <= defaultWeight + WeightEpsilon && state.HasLightWound()
                        ? LeaveCause.Wounded
                        : LeaveCause.Danger;
                case Motive.Companions:
                    return LeaveCause.Companions;
                case Motive.Rest:
                    return LeaveCause.Tired;
                default:
                    if (state.Conditions.Count > 0) return LeaveCause.HardAfterWound;
                    if (state.LastBreakdownAtHours.HasValue
                        && nowHours - state.LastBreakdownAtHours.Value <= calendar.DaysToHours(data.Balance.Decisions.RecentBreakdownDays))
                        return LeaveCause.HardAfterBreakdown;
                    return LeaveCause.HeavyHeart;
            }
        }

        /// <summary>
        /// Текст причин через запятую: «пустой кошелёк, устал». Шаблон — самый конкретный из подходящих (раскрыта ли черта),
        /// первый вариант. Ошибки шаблона — в лог.
        /// </summary>
        public static string Text(SimContext ctx, Adventurer adventurer, IReadOnlyList<LeaveCause> causes)
        {
            var text = new StringBuilder();
            var errors = new List<string>();
            var source = new PersonTextSource(adventurer, ctx.Data);
            foreach (LeaveCause cause in causes)
            {
                if (text.Length > 0) text.Append(", ");
                string key = KeyOf(cause);
                IReadOnlyList<FeedTemplate> templates = ctx.Data.HasDefinitions ? ctx.Data.FeedTemplates(key) : Array.Empty<FeedTemplate>();
                FeedTemplate template = FeedConditions.Select(templates, new FeedConditionContext(null, adventurer, ctx.Data));
                if (template == null || template.Variants.Count == 0)
                {
                    if (ctx.Data.HasDefinitions) ctx.Log.Write(SimLogLevel.Error, "reason {0}: no template", key);
                    text.Append(key);
                    continue;
                }

                errors.Clear();
                text.Append(TextRenderer.Render(template.Variants[0], source, errors));
                foreach (string error in errors) ctx.Log.Write(SimLogLevel.Error, "reason {0}: {1}", key, error);
            }
            return text.ToString();
        }

        private static IReadOnlyList<string> BuildKeys()
        {
            var keys = new List<string>();
            foreach (LeaveCause cause in (LeaveCause[])Enum.GetValues(typeof(LeaveCause))) keys.Add(KeyOf(cause));
            return keys;
        }
    }

    /// <summary>Метка <c>{имя}</c> — этот человек (род для скобок <c>[его|её]@имя</c>).</summary>
    public sealed class PersonTextSource : ITextSource
    {
        private readonly TextValue person;

        public PersonTextSource(Adventurer adventurer, DataRegistry data)
        {
            person = EventTextSource.PersonValue(adventurer, data);
        }

        public bool TryGet(string label, out TextValue value)
        {
            value = label == "имя" ? person : null;
            return value != null;
        }
    }
}
