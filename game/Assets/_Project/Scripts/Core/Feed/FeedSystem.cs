using System.Collections.Generic;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Шаг такта перед автопаузой: превращает события такта в строки ленты. Событие → ключ (<see cref="FeedKeys"/>) →
    /// шаблон с выполненными условиями, самый конкретный (<see cref="FeedConditions"/>) → случайный вариант, кроме
    /// последнего использованного у этого ключа → подстановка (<see cref="TextRenderer"/>, значения — <see cref="EventTextSource"/>)
    /// → лента гильдии или лента задания и лог (<see cref="SimLogLevel.Info"/>). Ошибки шаблона — в лог (<see cref="SimLogLevel.Error"/>),
    /// строка всё равно пишется. Случайность — свой поток, чужие броски не сдвигаются.
    /// <para>
    /// Лента задания: шаблон ленты задания — строка в <see cref="QuestRun.Log"/> задания из данных события (<c>quest</c>).
    /// Важные для гильдии (<see cref="FeedKeys.IsGuildWorthy"/>) — ещё и в ленту гильдии; у бегства — своя строка гильдии
    /// (<see cref="FeedKeys.GuildLineOf"/>). Строка гильдии о человеке на задании (раскрытие) — ещё и в ленту его задания.
    /// </para>
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
            if (key != null) Write(ctx, simEvent, key, guildCopy: FeedKeys.IsGuildWorthy(simEvent) && FeedKeys.GuildLineOf(simEvent) == null);
            string guildLine = FeedKeys.GuildLineOf(simEvent);
            if (guildLine != null) Write(ctx, simEvent, guildLine, guildCopy: false);
        }

        private void Write(SimContext ctx, SimEvent simEvent, string key, bool guildCopy)
        {
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

            EventImportance importance = ToImportance(template.Importance);
            var links = new int[simEvent.Participants.Count];
            for (int i = 0; i < links.Length; i++) links[i] = simEvent.Participants[i];
            int guildLimit = ctx.Data.Balance.Feed.GuildFeedLimit;

            if (template.Feed == FeedKind.Quest)
            {
                if (!simEvent.TryGet("quest", out int runId) || !ctx.World.Quests.TryGetRun(runId, out QuestRun run))
                {
                    ctx.Log.Write(SimLogLevel.Error, "feed {0}: template is for the quest feed, event {1} has no quest", key, simEvent.Type);
                    return;
                }
                var entry = new FeedEntry(simEvent.TimeHours, FeedKind.Quest, importance, text, links, key, run.Id);
                ctx.World.Feed.AddQuest(run, entry);
                ctx.Log.Write(SimLogLevel.Info, "feed quest #{0} {1} {2}: {3}", run.Id, Mark(importance), key, text);
                if (guildCopy)
                {
                    ctx.World.Feed.AddGuild(new FeedEntry(simEvent.TimeHours, FeedKind.Guild, importance, text, links, key, run.Id), guildLimit);
                    ctx.Log.Write(SimLogLevel.Info, "feed {0} {1}: {2}", Mark(importance), key, text);
                }
                return;
            }

            int questOfSubject = QuestOfSubject(ctx, simEvent);
            ctx.World.Feed.AddGuild(new FeedEntry(simEvent.TimeHours, template.Feed, importance, text, links, key, questOfSubject), guildLimit);
            ctx.Log.Write(SimLogLevel.Info, "feed {0} {1}: {2}", Mark(importance), key, text);
            if (questOfSubject != 0 && ctx.World.Quests.TryGetRun(questOfSubject, out QuestRun subjectRun))
                ctx.World.Feed.AddQuest(subjectRun, new FeedEntry(simEvent.TimeHours, FeedKind.Quest, importance, text, links, key, subjectRun.Id));
        }

        /// <summary>Раскрытие у человека на задании — строка и в ленте его задания; иначе 0.</summary>
        private static int QuestOfSubject(SimContext ctx, SimEvent simEvent)
        {
            if (simEvent.Type != SimEventType.AxisRevealed && simEvent.Type != SimEventType.TraitRevealed) return 0;
            if (simEvent.Participants.Count == 0 || !ctx.World.Adventurers.TryGetActive(simEvent.Participants[0], out Adventurer subject)) return 0;
            return subject.State.QuestRunId;
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
