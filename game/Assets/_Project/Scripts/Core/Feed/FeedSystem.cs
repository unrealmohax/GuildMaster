using System.Collections.Generic;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Шаг такта перед автопаузой: превращает события такта в строки ленты. Событие → ключ (<see cref="FeedKeys"/>) →
    /// шаблон с выполненными условиями, самый конкретный (<see cref="FeedConditions"/>) → случайный вариант, кроме
    /// последнего использованного у этого ключа → подстановка (<see cref="TextRenderer"/>, значения — <see cref="EventTextSource"/>)
    /// → лента гильдии и лог (<see cref="SimLogLevel.Info"/>). Ошибки шаблона — в лог (<see cref="SimLogLevel.Error"/>),
    /// строка всё равно пишется. Случайность — свой поток, чужие броски не сдвигаются.
    /// Реестр только из чисел строк не даёт.
    /// </summary>
    public sealed class FeedSystem : ISimSystem
    {
        private readonly List<string> errors = new List<string>();

        public string Name => nameof(FeedSystem);

        public void Tick(SimContext ctx)
        {
            if (!ctx.Data.HasDefinitions) return;

            IReadOnlyList<SimEvent> events = ctx.Events.Events;
            int count = events.Count;
            for (int i = 0; i < count; i++)
            {
                Write(ctx, events[i]);
            }
        }

        private void Write(SimContext ctx, SimEvent simEvent)
        {
            string key = FeedKeys.Of(simEvent, ctx.World, ctx.Data);
            if (key == null) return;

            IReadOnlyList<FeedTemplate> templates = ctx.Data.FeedTemplates(key);
            if (templates.Count == 0)
            {
                ctx.Log.Write(SimLogLevel.Error, "feed {0}: no templates for {1}", key, simEvent.Type);
                return;
            }

            Adventurer subject = simEvent.Participants.Count > 0 ? EventTextSource.FindPerson(ctx.World, simEvent.Participants[0]) : null;
            FeedTemplate template = FeedConditions.Select(templates, new FeedConditionContext(simEvent, subject, ctx.Data));
            if (template == null)
            {
                ctx.Log.Write(SimLogLevel.Error, "feed {0}: no template fits {1}", key, simEvent.Type);
                return;
            }
            if (template.Variants.Count == 0)
            {
                ctx.Log.Write(SimLogLevel.Error, "feed {0}: template has no variants", key);
                return;
            }

            string variant = PickVariant(ctx, key, template.Variants);
            errors.Clear();
            string text = TextRenderer.Render(variant, new EventTextSource(simEvent, ctx.World, ctx.Data), errors);
            foreach (string error in errors) ctx.Log.Write(SimLogLevel.Error, "feed {0}: {1}", key, error);

            if (template.Feed != FeedKind.Guild)
            {
                ctx.Log.Write(SimLogLevel.Error, "feed {0}: template is for the {1} feed, event {2} has no such feed", key, template.Feed, simEvent.Type);
                return;
            }

            EventImportance importance = ToImportance(template.Importance);
            var links = new int[simEvent.Participants.Count];
            for (int i = 0; i < links.Length; i++) links[i] = simEvent.Participants[i];

            ctx.World.Feed.AddGuild(new FeedEntry(simEvent.TimeHours, template.Feed, importance, text, links, key), ctx.Data.Balance.Feed.GuildFeedLimit);
            ctx.Log.Write(SimLogLevel.Info, "feed {0} {1}: {2}", Mark(importance), key, text);
        }

        /// <summary>Случайный вариант, кроме последнего использованного у ключа. Один вариант — без броска.</summary>
        private static string PickVariant(SimContext ctx, string key, IReadOnlyList<string> variants)
        {
            string chosen;
            if (variants.Count == 1)
            {
                chosen = variants[0];
            }
            else
            {
                ctx.World.Feed.TryGetLastVariant(key, out string last);
                int excluded = last != null ? IndexOf(variants, last) : -1;
                int index = ctx.Rng.Range(0, excluded >= 0 ? variants.Count - 1 : variants.Count);
                if (excluded >= 0 && index >= excluded) index++;
                chosen = variants[index];
            }
            ctx.World.Feed.SetLastVariant(key, chosen);
            return chosen;
        }

        private static int IndexOf(IReadOnlyList<string> variants, string variant)
        {
            for (int i = 0; i < variants.Count; i++)
            {
                if (variants[i] == variant) return i;
            }
            return -1;
        }

        public static EventImportance ToImportance(FeedImportance importance)
        {
            switch (importance)
            {
                case FeedImportance.Notable: return EventImportance.Notable;
                case FeedImportance.Important: return EventImportance.Important;
                default: return EventImportance.Normal;
            }
        }

        /// <summary>Пометка важности в логе: [О], [З], [В].</summary>
        public static string Mark(EventImportance importance)
        {
            switch (importance)
            {
                case EventImportance.Notable: return "[З]";
                case EventImportance.Important: return "[В]";
                default: return "[О]";
            }
        }
    }
}
