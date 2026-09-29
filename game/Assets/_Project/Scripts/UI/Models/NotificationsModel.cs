using System.Collections.Generic;
using GuildMaster.Core;

namespace GuildMaster.UI
{
    /// <summary>Окно решения: кандидат в авантюристы, кандидат в персонал, отчёт месяца, поражение.</summary>
    public enum PopupKind
    {
        None,
        AdventurerCandidate,
        StaffCandidate,
        Report,
        Defeat,
    }

    /// <summary>Какое окно решения открыть: вид и id кандидата или номер отчёта в истории.</summary>
    public readonly struct Popup
    {
        public Popup(PopupKind kind, int id = 0)
        {
            Kind = kind;
            Id = id;
        }

        public PopupKind Kind { get; }
        public int Id { get; }
        public bool IsValid => Kind != PopupKind.None;

        public override string ToString() => $"{Kind} {Id}";
    }

    /// <summary>
    /// Уведомление: текст и куда ведёт клик — окно решения (<see cref="Popup"/>) или экран (<see cref="Destination"/>).
    /// <see cref="ReportIndex"/> ≥ 0 — отчёт месяца с этим номером.
    /// </summary>
    public sealed class Notification
    {
        public string Text;
        public Destination Destination;
        public Popup Popup;
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
            new GuildClosed(),
        };

        public List<Notification> Items { get; } = new List<Notification>();

        /// <summary>Сколько отчётов месяца игрок уже открыл (по порядку истории).</summary>
        public int ReportsSeen { get; set; }

        /// <summary>
        /// Кандидаты в персонал, которым игрок отказал: в мире они ждут до своего срока, но уведомлением и окном больше не
        /// приходят. Своей команды отказа у кандидата в персонал нет — отказ живёт только в интерфейсе.
        /// </summary>
        public HashSet<int> RejectedStaffCandidates { get; } = new HashSet<int>();

        /// <summary>Отказать кандидату в персонал (только в интерфейсе).</summary>
        public void RejectStaffCandidate(int candidateId) => RejectedStaffCandidates.Add(candidateId);

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
                        Popup = new Popup(PopupKind.AdventurerCandidate, candidate.Adventurer.Id),
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
                    if (model.RejectedStaffCandidates.Contains(candidate.Id)) continue;
                    items.Add(new Notification
                    {
                        Text = string.Format(UiStrings.NotifyStaffCandidateFormat, StaffModel.RoleName(client.Data, candidate.RoleId), candidate.Name),
                        Destination = Destination.ToGuild(GuildTab.Staff, candidate.Id),
                        Popup = new Popup(PopupKind.StaffCandidate, candidate.Id),
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
                        Popup = new Popup(PopupKind.Report, i),
                    });
                }
            }
        }

        private sealed class GuildClosed : INotificationSource
        {
            public void Collect(ISimulationClient client, NotificationsModel model, List<Notification> items)
            {
                if (!client.IsFinished) return;
                items.Insert(0, new Notification { Text = UiStrings.GuildClosed, Popup = new Popup(PopupKind.Defeat) });
            }
        }
    }

    /// <summary>
    /// Очередь окон решений, которые открываются сами: закрытие гильдии — сразу, поверх всего; остальное — по одному, когда
    /// поверх экрана нет других окон: каждый новый кандидат в авантюристы и в персонал (кроме тех, кому отказали) и каждый
    /// непрочитанный отчёт месяца. Время окна не останавливают. Закрытое окно больше само не приходит — решение ждёт
    /// в уведомлениях.
    /// </summary>
    public sealed class PopupQueue
    {
        private readonly HashSet<int> shownAdventurerCandidates = new HashSet<int>();
        private readonly HashSet<int> shownStaffCandidates = new HashSet<int>();

        public bool DefeatShown { get; private set; }

        /// <summary>Окно закрытия гильдии — не дожидаясь, пока закроют другие окна.</summary>
        public bool TryUrgent(ISimulationClient client, out Popup popup)
        {
            popup = client.IsFinished && !DefeatShown ? new Popup(PopupKind.Defeat) : default;
            return popup.IsValid;
        }

        /// <summary>Следующее окно, которое ещё не открывалось само: кандидат в авантюристы, в персонал, отчёт.</summary>
        public bool TryNext(ISimulationClient client, NotificationsModel notifications, out Popup popup)
        {
            if (TryUrgent(client, out popup)) return true;
            WorldState world = client.World;
            foreach (Candidate candidate in world.Adventurers.Candidates)
            {
                if (shownAdventurerCandidates.Contains(candidate.Adventurer.Id)) continue;
                popup = new Popup(PopupKind.AdventurerCandidate, candidate.Adventurer.Id);
                return true;
            }
            foreach (StaffCandidate candidate in world.Staff.Candidates)
            {
                if (shownStaffCandidates.Contains(candidate.Id) || notifications.RejectedStaffCandidates.Contains(candidate.Id)) continue;
                popup = new Popup(PopupKind.StaffCandidate, candidate.Id);
                return true;
            }
            if (world.Reports.Reports.Count > notifications.ReportsSeen)
            {
                popup = new Popup(PopupKind.Report, notifications.ReportsSeen);
                return true;
            }
            popup = default;
            return false;
        }

        /// <summary>Окно открыто (само или по клику) — само оно больше не придёт.</summary>
        public void MarkShown(Popup popup, NotificationsModel notifications)
        {
            switch (popup.Kind)
            {
                case PopupKind.AdventurerCandidate: shownAdventurerCandidates.Add(popup.Id); break;
                case PopupKind.StaffCandidate: shownStaffCandidates.Add(popup.Id); break;
                case PopupKind.Report: notifications.ReportsSeen = System.Math.Max(notifications.ReportsSeen, popup.Id + 1); break;
                case PopupKind.Defeat: DefeatShown = true; break;
            }
        }
    }
}
