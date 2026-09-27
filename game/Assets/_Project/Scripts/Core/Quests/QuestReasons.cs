using System;
using System.Collections.Generic;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Причины важных решений на заданиях — текст для игрока (<c>{причина}</c>): бегство и отказ от заказа из-за Кошмаров.
    /// Шаблон — самый конкретный из подходящих (раскрыта ли черта: скрытая не называется), первый вариант; <c>{имя}</c> —
    /// сам человек. Ключи — <see cref="Keys"/> (валидатор данных требует шаблоны).
    /// </summary>
    public static class QuestReasons
    {
        public const string FledKey = "reason.fled";
        public const string NightmaresKey = "reason.nightmares";

        public static IReadOnlyList<string> Keys { get; } = new[] { FledKey, NightmaresKey };

        /// <summary>Почему сбежал: «не выдержал», у раскрытого труса — «трус» и т.п.</summary>
        public static string Fled(SimContext ctx, Adventurer adventurer) => Text(ctx, adventurer, FledKey);

        /// <summary>Почему отказался от заказа: похоже на задание, после которого снятся кошмары.</summary>
        public static string Nightmares(SimContext ctx, Adventurer adventurer) => Text(ctx, adventurer, NightmaresKey);

        private static string Text(SimContext ctx, Adventurer adventurer, string key)
        {
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
    }
}
