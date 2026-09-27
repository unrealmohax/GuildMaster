using System;
using System.Collections.Generic;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>Почему человек отказался идти с группой — мотив, который больше всего перевесил. Значения пишутся в события: новые — в конец.</summary>
    public enum InvitationRefusal
    {
        /// <summary>Ни один мотив не перевесил: выбор со второй попытки — «не захотел».</summary>
        Whim,

        /// <summary>Деньги: доля в группе мала.</summary>
        Money,

        /// <summary>Слава: заказ не по нему.</summary>
        Glory,

        /// <summary>Безопасность: слишком опасно.</summary>
        Danger,

        /// <summary>Товарищи у одиночки: не хочет идти с группой.</summary>
        Loner,

        /// <summary>Отдых: устал.</summary>
        Tired,

        /// <summary>Утешение: тяжело на душе.</summary>
        Comfort,
    }

    /// <summary>
    /// Причина отказа от приглашения — важное решение, причину видит игрок (<c>{причина}</c>). Причина — мотив с наибольшим
    /// перевесом выбранного варианта над приглашением (вес × оценка); текст — шаблон <c>reason.invite.*</c>, самый конкретный
    /// (раскрытая черта называется, скрытая — нет), первый вариант. Случайных чисел не тратит.
    /// </summary>
    public static class PartyReasons
    {
        public static string KeyOf(InvitationRefusal cause)
        {
            string name = cause.ToString();
            return "reason.invite." + char.ToLowerInvariant(name[0]) + name.Substring(1);
        }

        /// <summary>Ключи всех причин — их шаблоны обязаны быть в данных.</summary>
        public static IReadOnlyList<string> Keys { get; } = BuildKeys();

        /// <summary>
        /// Главная причина: мотив, где выбранный вариант больше всего перевешивает приглашение. Нет перевеса — <see cref="InvitationRefusal.Whim"/>.
        /// </summary>
        public static InvitationRefusal Of(MotiveWeights chosenWeights, float[] chosenScores, MotiveWeights inviteWeights, float[] inviteScores)
        {
            Motive best = Motive.Money;
            float bestGap = 0f;
            for (int m = 0; m < chosenScores.Length; m++)
            {
                float gap = chosenWeights[(Motive)m] * chosenScores[m] - inviteWeights[(Motive)m] * inviteScores[m];
                if (gap <= bestGap) continue;
                best = (Motive)m;
                bestGap = gap;
            }
            if (bestGap <= 0f) return InvitationRefusal.Whim;
            switch (best)
            {
                case Motive.Money: return InvitationRefusal.Money;
                case Motive.Glory: return InvitationRefusal.Glory;
                case Motive.Safety: return InvitationRefusal.Danger;
                case Motive.Companions: return InvitationRefusal.Loner;
                case Motive.Rest: return InvitationRefusal.Tired;
                default: return InvitationRefusal.Comfort;
            }
        }

        /// <summary>Текст причины для строки ленты.</summary>
        public static string Text(SimContext ctx, Adventurer adventurer, InvitationRefusal cause)
        {
            string key = KeyOf(cause);
            IReadOnlyList<FeedTemplate> templates = ctx.Data.HasDefinitions ? ctx.Data.FeedTemplates(key) : Array.Empty<FeedTemplate>();
            FeedTemplate template = FeedConditions.Select(templates, new FeedConditionContext(null, adventurer, ctx.Data));
            if (template == null || template.Variants.Count == 0)
            {
                if (ctx.Data.HasDefinitions) ctx.Log.Write(SimLogLevel.Error, "reason {0}: no template", key);
                return key;
            }

            var errors = new List<string>();
            string text = TextRenderer.Render(template.Variants[0], new PersonTextSource(adventurer, ctx.Data), errors);
            foreach (string error in errors) ctx.Log.Write(SimLogLevel.Error, "reason {0}: {1}", key, error);
            return text;
        }

        private static IReadOnlyList<string> BuildKeys()
        {
            var keys = new List<string>();
            foreach (InvitationRefusal cause in (InvitationRefusal[])Enum.GetValues(typeof(InvitationRefusal))) keys.Add(KeyOf(cause));
            return keys;
        }
    }
}
