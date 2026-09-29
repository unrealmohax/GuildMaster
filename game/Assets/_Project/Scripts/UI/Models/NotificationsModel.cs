using System.Collections.Generic;
using GuildMaster.Core;

namespace GuildMaster.UI
{
    /// <summary>Уведомление: текст и куда ведёт клик. <see cref="ReportIndex"/> ≥ 0 — открыть отчёт месяца с этим номером.</summary>
    public sealed class Notification
    {
        public string Text;
        public Destination Destination;
        public int ReportIndex = -1;
    }

    /// <summary>Вид уведомлений. Новый вид — класс с этим интерфейсом в <see cref="NotificationsModel.Sources"/>.</summary>
    public interface INotificationSource
    {
        void Collect(ISimulationClient client, NotificationsModel model, List<Notification> items);
    }

    /// <summary>
    /// Колокольчик верхней панели: что ждёт внимания игрока, по состоянию мира — важные заказы и событийные задания без ответа,
    /// кандидаты в авантюристы и на вакансии, непрочитанный отчёт месяца. Уведомление пропадает само, когда пропал его повод.
    /// </summary>
    public sealed class NotificationsModel
    {
        public static readonly List<INotificationSource> Sources = new List<INotificationSource>
        {
            new ImportantOrders(),
            new AdventurerCandidates(),
            new StaffCandidates(),
            new MonthReports(),
        };

        public List<Notification> Items { get; } = new List<Notification>();

        /// <summary>Сколько отчётов месяца игрок уже открыл (по порядку истории).</summary>
        public int ReportsSeen { get; set; }

        public void Refresh(ISimulationClient client)
        {
            Items.Clear();
            foreach (INotificationSource source in Sources) source.Collect(client, this, Items);
        }

        private sealed class ImportantOrders : INotificationSource
        {
            public void Collect(ISimulationClient client, NotificationsModel model, List<Notification> items)
            {
                foreach (Order order in client.World.Orders.Open)
                {
                    if (order.Status != OrderStatus.AwaitingPlayer) continue;
                    string format = order.IsEventQuest ? UiStrings.NotifyEventQuestFormat : UiStrings.NotifyImportantFormat;
                    items.Add(new Notification
                    {
                        Text = string.Format(format, BoardModel.TypeName(client.Data, order.TypeId), UiFormat.Rank(order.Rank), order.Client?.Nominative),
                        Destination = Destination.ToScreen(ScreenId.Board, order.Id),
                    });
                }
            }
        }

        private sealed class AdventurerCandidates : INotificationSource
        {
            public void Collect(ISimulationClient client, NotificationsModel model, List<Notification> items)
            {
                foreach (Candidate candidate in client.World.Adventurers.Candidates)
                {
                    items.Add(new Notification
                    {
                        Text = string.Format(UiStrings.NotifyCandidateFormat, candidate.Adventurer.Name, PersonText.Archetype(candidate.Adventurer, client.Data)),
                        Destination = Destination.Card(candidate.Adventurer.Id),
                    });
                }
            }
        }

        private sealed class StaffCandidates : INotificationSource
        {
            public void Collect(ISimulationClient client, NotificationsModel model, List<Notification> items)
            {
                foreach (StaffCandidate candidate in client.World.Staff.Candidates)
                {
                    items.Add(new Notification
                    {
                        Text = string.Format(UiStrings.NotifyStaffCandidateFormat, StaffModel.RoleName(client.Data, candidate.RoleId), candidate.Name),
                        Destination = Destination.ToGuild(GuildTab.Staff, candidate.Id),
                    });
                }
            }
        }

        private sealed class MonthReports : INotificationSource
        {
            public void Collect(ISimulationClient client, NotificationsModel model, List<Notification> items)
            {
                IReadOnlyList<MonthReport> reports = client.World.Reports.Reports;
                for (int i = model.ReportsSeen; i < reports.Count; i++)
                {
                    items.Add(new Notification
                    {
                        Text = string.Format(UiStrings.NotifyReportFormat, reports[i].Month, reports[i].Year),
                        ReportIndex = i,
                    });
                }
            }
        }
    }
}
