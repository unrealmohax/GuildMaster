using System.Collections.Generic;
using GuildMaster.Core;
using GuildMaster.Data;

namespace GuildMaster.UI
{
    /// <summary>Строка окна автопаузы: текст события (как в ленте, со ссылками) и куда ведёт «Перейти».</summary>
    public sealed class AutopauseLine
    {
        public AutopauseKind Kind;
        public string Text;
        public IReadOnlyList<TextSpan> Spans;
        public Destination Destination;
    }

    /// <summary>
    /// Окно автопаузы: события, из-за которых встало время (<see cref="AutopauseState.Triggers"/>). Текст — строка ленты этого
    /// события (та же, что игрок увидит в ленте), а если её нет — первый вариант шаблона.
    /// </summary>
    public static class AutopauseModel
    {
        public static List<AutopauseLine> Build(ISimulationClient client)
        {
            var lines = new List<AutopauseLine>();
            foreach (AutopauseTrigger trigger in client.World.Autopause.Triggers)
            {
                SimEvent simEvent = trigger.Event;
                var line = new AutopauseLine { Kind = trigger.Kind, Destination = DestinationOf(simEvent, client) };
                FeedEntry entry = FindEntry(simEvent, client);
                if (entry != null)
                {
                    line.Text = entry.Text;
                    line.Spans = entry.Spans;
                }
                else
                {
                    var spans = new List<TextSpan>();
                    line.Text = Render(simEvent, client, spans);
                    line.Spans = spans;
                }
                lines.Add(line);
            }
            return lines;
        }

        /// <summary>Первый участник — его карточка; иначе задание, заказ, сотрудник из данных события.</summary>
        public static Destination DestinationOf(SimEvent simEvent, ISimulationClient client)
        {
            if (simEvent.Participants.Count > 0)
            {
                Destination person = LinkRouter.Resolve(new TextLink(TextLinkKind.Adventurer, simEvent.Participants[0]), client);
                if (person.IsValid) return person;
            }
            if (simEvent.TryGet("quest", out int quest))
            {
                Destination run = LinkRouter.Resolve(new TextLink(TextLinkKind.Quest, quest), client);
                if (run.IsValid) return run;
            }
            if (simEvent.TryGet("order", out int order))
            {
                Destination board = LinkRouter.Resolve(new TextLink(TextLinkKind.Order, order), client);
                if (board.IsValid) return board;
            }
            if (simEvent.TryGet("staffId", out int staff)) return LinkRouter.Resolve(new TextLink(TextLinkKind.Staff, staff), client);
            return Destination.None;
        }

        private static FeedEntry FindEntry(SimEvent simEvent, ISimulationClient client)
        {
            string key = FeedKeys.Of(simEvent, client.World, client.Data);
            string guildLine = FeedKeys.GuildLineOf(simEvent);
            if (key == null && guildLine == null) return null;

            IReadOnlyList<FeedEntry> guild = client.World.Feed.Guild;
            for (int i = guild.Count - 1; i >= 0 && guild[i].TimeHours >= simEvent.TimeHours; i--)
            {
                FeedEntry entry = guild[i];
                if (entry.TimeHours == simEvent.TimeHours && (entry.TemplateKey == key || entry.TemplateKey == guildLine)) return entry;
            }

            if (simEvent.TryGet("quest", out int runId) && client.World.Quests.TryGetRun(runId, out QuestRun run))
            {
                for (int i = run.Log.Count - 1; i >= 0; i--)
                {
                    if (run.Log[i].TimeHours == simEvent.TimeHours && run.Log[i].TemplateKey == key) return run.Log[i];
                }
            }
            return null;
        }

        private static string Render(SimEvent simEvent, ISimulationClient client, List<TextSpan> spans)
        {
            string key = FeedKeys.Of(simEvent, client.World, client.Data) ?? FeedKeys.GuildLineOf(simEvent);
            if (key == null || !client.Data.HasDefinitions) return simEvent.Type.ToString();
            IReadOnlyList<FeedTemplate> templates = client.Data.FeedTemplates(key);
            if (templates.Count == 0 || templates[0].Variants.Count == 0) return simEvent.Type.ToString();
            return TextRenderer.Render(templates[0].Variants[0], new EventTextSource(simEvent, client.World, client.Data), null, spans);
        }
    }
}
