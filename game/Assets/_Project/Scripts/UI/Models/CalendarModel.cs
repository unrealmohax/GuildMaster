using System.Collections.Generic;
using GuildMaster.Core;

namespace GuildMaster.UI
{
    /// <summary>Ожидаемое событие календаря: когда, что и куда перейти по клику.</summary>
    public sealed class CalendarItem
    {
        public long AtHours;
        public string When;
        public string Text;
        public Destination Destination;
    }

    /// <summary>Вид событий календаря. Новый вид — класс с этим интерфейсом в <see cref="CalendarModel.Sources"/>.</summary>
    public interface ICalendarSource
    {
        void Collect(ISimulationClient client, List<CalendarItem> items);
    }

    /// <summary>
    /// Окно «Время»: текущая дата, сколько до начала месяца и ближайшие события по порядку — конец стройки, сроки ответа
    /// на важные заказы, возвращение групп (только тех, кто уже на обратном пути: срок на месте заранее не известен), окончание
    /// срока распоряжений, сроки ответа на обращения.
    /// </summary>
    public sealed class CalendarModel
    {
        public static readonly List<ICalendarSource> Sources = new List<ICalendarSource>
        {
            new ConstructionEnds(),
            new OrderAnswersDue(),
            new PartiesReturn(),
            new DecreesEnd(),
            new DilemmaAnswersDue(),
        };

        public string Today { get; private set; } = string.Empty;
        public string UntilMonth { get; private set; } = string.Empty;
        public List<CalendarItem> Items { get; } = new List<CalendarItem>();

        public void Refresh(ISimulationClient client)
        {
            GameTime now = client.World.Time;
            Today = $"{UiFormat.Date(now)} · {UiFormat.Phase(client.Rhythm.PhaseAt(now))}";
            long monthStart = client.Calendar.ToTotalHours(now.Year, now.Month, 1, 0);
            long nextMonth = monthStart + client.Calendar.HoursPerMonth;
            UntilMonth = string.Format(UiStrings.UntilMonthFormat, UiFormat.Duration(nextMonth - now.TotalHours, client.Calendar));

            Items.Clear();
            foreach (ICalendarSource source in Sources) source.Collect(client, Items);
            Items.Sort((a, b) => a.AtHours.CompareTo(b.AtHours));
            foreach (CalendarItem item in Items)
            {
                GameTime at = client.Calendar.At(item.AtHours);
                item.When = $"{UiFormat.Stamp(at)} (через {UiFormat.Duration(item.AtHours - now.TotalHours, client.Calendar)})";
            }
        }

        private sealed class ConstructionEnds : ICalendarSource
        {
            public void Collect(ISimulationClient client, List<CalendarItem> items)
            {
                Building building = client.World.Buildings.Current;
                if (building == null || building.State != BuildingState.UnderConstruction) return;
                string name = client.Data.TryGet(building.DefinitionId, out GuildMaster.Data.BuildingDefinition definition)
                    ? definition.DisplayName
                    : building.DefinitionId;
                items.Add(new CalendarItem
                {
                    AtHours = building.ConstructionEndsAtHours,
                    Text = string.Format(UiStrings.CalendarConstructionFormat, name),
                    Destination = Destination.ToGuild(GuildTab.Buildings, building.Id),
                });
            }
        }

        private sealed class OrderAnswersDue : ICalendarSource
        {
            public void Collect(ISimulationClient client, List<CalendarItem> items)
            {
                foreach (Order order in client.World.Orders.Open)
                {
                    if (order.Status != OrderStatus.AwaitingPlayer || order.AnswerDueAtHours < 0) continue;
                    items.Add(new CalendarItem
                    {
                        AtHours = order.AnswerDueAtHours,
                        Text = string.Format(UiStrings.CalendarAnswerFormat, BoardModel.TypeName(client.Data, order.TypeId), order.Client?.Nominative),
                        Destination = Destination.ToScreen(ScreenId.Board, order.Id),
                    });
                }
            }
        }

        private sealed class DecreesEnd : ICalendarSource
        {
            public void Collect(ISimulationClient client, List<CalendarItem> items)
            {
                foreach (ActiveDecree active in client.World.Decrees.Active)
                {
                    if (active.IsPermanent) continue;
                    string name = client.Data.TryGet(active.DecreeId, out GuildMaster.Data.DecreeDefinition definition)
                        ? definition.DisplayName
                        : active.DecreeId;
                    items.Add(new CalendarItem
                    {
                        AtHours = active.EndsAtHours,
                        Text = string.Format(UiStrings.CalendarDecreeFormat, name),
                        Destination = Destination.ToScreen(ScreenId.Decrees),
                    });
                }
            }
        }

        private sealed class DilemmaAnswersDue : ICalendarSource
        {
            public void Collect(ISimulationClient client, List<CalendarItem> items)
            {
                foreach (Dilemma dilemma in client.World.Dilemmas.Open)
                {
                    items.Add(new CalendarItem
                    {
                        AtHours = dilemma.DeadlineAtHours,
                        Text = string.Format(UiStrings.CalendarDilemmaFormat, DilemmasModel.Title(client.Data, dilemma), DilemmasModel.From(client, dilemma)),
                        Destination = Destination.ToScreen(ScreenId.Dilemmas, dilemma.Id),
                    });
                }
            }
        }

        private sealed class PartiesReturn : ICalendarSource
        {
            public void Collect(ISimulationClient client, List<CalendarItem> items)
            {
                foreach (QuestRun run in client.World.Quests.Active)
                {
                    if (!ObserverQueries.TryGetReturnAtHours(run, client.World, client.Rhythm, out long at)) continue;
                    items.Add(new CalendarItem
                    {
                        AtHours = at,
                        Text = string.Format(UiStrings.CalendarReturnFormat, QuestsModel.PartyText(run, client.World)),
                        Destination = Destination.ToScreen(ScreenId.Quests, run.Id),
                    });
                }
            }
        }
    }
}
