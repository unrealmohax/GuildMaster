using System;
using System.Collections.Generic;
using System.Text;
using GuildMaster.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GuildMaster.UI
{
    /// <summary>
    /// Экран «Гильдия»: вкладки Люди, Группы, Постройки, Персонал и лента гильдии справа (всегда на этом экране). На вкладке
    /// «Постройки» — заказать стройку, порядок очереди (перетаскиванием или ▲▼), снять из очереди; на «Персонале» — вакансии,
    /// предложение зарплаты кандидатам, увольнение. Всё — командами.
    /// </summary>
    public sealed class GuildScreen : ScreenView
    {
        private static readonly PeopleFilter[] Filters =
        {
            PeopleFilter.All, PeopleFilter.OnQuest, PeopleFilter.Resting, PeopleFilter.Tavern, PeopleFilter.Training,
            PeopleFilter.Infirmary, PeopleFilter.Breakdown,
        };

        private static readonly string[] FilterNames =
        {
            UiStrings.FilterAll, UiStrings.FilterOnQuest, UiStrings.FilterResting, UiStrings.FilterTavern, UiStrings.FilterTraining,
            UiStrings.FilterInfirmary, UiStrings.FilterBreakdown,
        };

        private readonly Dictionary<GuildTab, Button> tabButtons = new Dictionary<GuildTab, Button>();
        private readonly Dictionary<GuildTab, RectTransform> tabPages = new Dictionary<GuildTab, RectTransform>();
        private readonly List<Button> filterButtons = new List<Button>();
        private readonly PeopleModel people = new PeopleModel();
        private readonly FeedView feed;

        private TableView peopleTable;
        private TextMeshProUGUI candidates;
        private RectTransform partiesContent;
        private readonly List<TextMeshProUGUI> partyLabels = new List<TextMeshProUGUI>();
        private TableView buildingsTable;
        private List<BuildingRow> buildingRows = new List<BuildingRow>();
        private Button buildButton;
        private RectTransform queueList;
        private TextMeshProUGUI queueEmpty;
        private readonly List<(RectTransform Root, TextMeshProUGUI Text)> queueViews = new List<(RectTransform, TextMeshProUGUI)>();
        private List<QueueRow> queueRows = new List<QueueRow>();
        private TableView staffTable;
        private TextMeshProUGUI staffSelected;
        private Button dismissButton;
        private int dismissArmedFor;
        private TextMeshProUGUI vacancies;
        private RectTransform staffCandidateList;
        private readonly List<StaffCandidateView> staffCandidateViews = new List<StaffCandidateView>();

        private sealed class StaffCandidateView
        {
            public RectTransform Root;
            public TextMeshProUGUI Text;
            public TMP_InputField Offer;
            public Button OfferButton;
            public Button RejectButton;
            public int CandidateId;
        }

        private GuildTab tab = GuildTab.People;
        private int selectedBuilding;
        private int selectedStaff;
        private int selectedParty;

        public GuildScreen(UiContext context, RectTransform parent) : base(context)
        {
            Root = UiFactory.Stretch(UiFactory.Node(parent, "Guild"));
            Factory.Horizontal(Root, Theme.Padding, 0, false).childForceExpandHeight = true;

            RectTransform main = UiFactory.Node(Root, "Main");
            Factory.Vertical(main, 8);
            UiFactory.Size(main, flexWidth: 1, flexHeight: 1);

            RectTransform tabs = UiFactory.Node(main, "Tabs");
            Factory.Horizontal(tabs, 6);
            UiFactory.Size(tabs, height: Theme.RowHeight + 8);
            AddTab(tabs, GuildTab.People, UiStrings.People);
            AddTab(tabs, GuildTab.Parties, UiStrings.Parties);
            AddTab(tabs, GuildTab.Buildings, UiStrings.Buildings);
            AddTab(tabs, GuildTab.Staff, UiStrings.Staff);

            RectTransform pages = UiFactory.Node(main, "Pages");
            UiFactory.Size(pages, flexWidth: 1, flexHeight: 1);
            BuildPeople(Page(pages, GuildTab.People));
            BuildParties(Page(pages, GuildTab.Parties));
            BuildBuildings(Page(pages, GuildTab.Buildings));
            BuildStaff(Page(pages, GuildTab.Staff));

            RectTransform side = UiFactory.Node(Root, "FeedPanel");
            Factory.Vertical(side, 6);
            UiFactory.Size(side, Theme.FeedWidth, flexWidth: 0, flexHeight: 1);
            Factory.Heading(side, UiStrings.GuildFeed);
            feed = new FeedView(Factory, side, Context.OnLink);
            UiFactory.Size(feed.Scroll, flexHeight: 1);

            ShowTab(GuildTab.People);
        }

        public override ScreenId Id => ScreenId.Guild;
        public override string Title => UiStrings.Guild;

        public GuildTab Tab => tab;

        public PeopleModel People => people;

        public override void Select(Destination destination)
        {
            ShowTab(destination.Tab);
            switch (destination.Tab)
            {
                case GuildTab.Buildings: selectedBuilding = destination.SelectId; break;
                case GuildTab.Staff: selectedStaff = destination.SelectId; break;
                case GuildTab.Parties: selectedParty = destination.SelectId; break;
            }
        }

        public override void Refresh()
        {
            feed.Show(Client.World.Feed.Guild, Client.Calendar);
            switch (tab)
            {
                case GuildTab.People: RefreshPeople(); break;
                case GuildTab.Parties: RefreshParties(); break;
                case GuildTab.Buildings: RefreshBuildings(); break;
                case GuildTab.Staff: RefreshStaff(); break;
            }
        }

        // ---------- Вкладки ----------

        private void AddTab(Transform parent, GuildTab id, string title)
        {
            tabButtons[id] = Factory.Button(parent, title, () =>
            {
                ShowTab(id);
                Refresh();
            }, 170);
        }

        private RectTransform Page(RectTransform parent, GuildTab id)
        {
            RectTransform page = UiFactory.Stretch(UiFactory.Node(parent, id.ToString()));
            Factory.Vertical(page, 6);
            tabPages[id] = page;
            return page;
        }

        private void ShowTab(GuildTab id)
        {
            tab = id;
            foreach (KeyValuePair<GuildTab, RectTransform> pair in tabPages) pair.Value.gameObject.SetActive(pair.Key == id);
            foreach (KeyValuePair<GuildTab, Button> pair in tabButtons) Factory.SetButtonColors(pair.Value, pair.Key == id);
        }

        // ---------- Люди ----------

        private void BuildPeople(RectTransform page)
        {
            RectTransform filters = UiFactory.Node(page, "Filters");
            Factory.Horizontal(filters, 4);
            UiFactory.Size(filters, height: Theme.RowHeight);
            for (int i = 0; i < Filters.Length; i++)
            {
                PeopleFilter filter = Filters[i];
                filterButtons.Add(Factory.Button(filters, FilterNames[i], () =>
                {
                    people.Filter = filter;
                    RefreshPeople();
                }, 150, fontSize: Theme.FontSizeSmall));
            }

            var columns = new List<TableColumn>
            {
                Column(UiStrings.ColName, 130, PeopleColumn.Name),
                Column(UiStrings.ColArchetype, 150, PeopleColumn.Archetype),
                Column(UiStrings.ColRank, 50, PeopleColumn.Rank),
                Column(UiStrings.ColActivity, -1, PeopleColumn.Activity),
                Column(UiStrings.ColWounds, 56, PeopleColumn.Wounds),
                Column(UiStrings.ColFatigue, 92, PeopleColumn.Fatigue, bar: true),
                Column(UiStrings.ColStress, 92, PeopleColumn.Stress, bar: true),
                Column(UiStrings.ColContentment, 92, PeopleColumn.Contentment, bar: true),
                Column(UiStrings.ColLoyalty, 200, PeopleColumn.Loyalty),
                Column(UiStrings.ColWallet, 74, PeopleColumn.Wallet),
            };
            peopleTable = new TableView(Factory, page, columns, id => Context.Navigator.Go(Destination.Card(id)));

            candidates = Factory.LinkLabel(page, Context.OnLink, Theme.FontSizeSmall);
        }

        private TableColumn Column(string title, float width, PeopleColumn sort, bool bar = false) => new TableColumn
        {
            Title = title,
            Width = width,
            Bar = bar,
            OnHeader = () =>
            {
                people.SortBy(sort);
                RefreshPeople();
            },
        };

        private void RefreshPeople()
        {
            people.Refresh(Client);
            for (int i = 0; i < filterButtons.Count; i++)
            {
                Factory.SetButtonColors(filterButtons[i], people.Filter == Filters[i]);
                filterButtons[i].GetComponentInChildren<TextMeshProUGUI>().text = $"{FilterNames[i]} {people.Counts[(int)Filters[i]]}";
            }

            peopleTable.SetRowCount(people.Rows.Count);
            for (int i = 0; i < people.Rows.Count; i++)
            {
                PersonRow row = people.Rows[i];
                peopleTable.SetRowId(i, row.Id);
                peopleTable.SetText(i, 0, row.Name);
                peopleTable.SetText(i, 1, row.Archetype);
                peopleTable.SetText(i, 2, UiFormat.Rank(row.Rank));
                peopleTable.SetText(i, 3, row.Activity, row.Group == PeopleFilter.OnQuest ? Theme.Accent : (Color?)null);
                peopleTable.SetText(i, 4, WoundBadges(row.WoundLevel, row.Maimed));
                peopleTable.SetBar(i, 5, row.Fatigue / 100f, UiFormat.Number(row.Fatigue), row.Fatigue > 80 ? Theme.Danger : Theme.Accent);
                peopleTable.SetBar(i, 6, row.Stress / 100f, UiFormat.Number(row.Stress), row.Stress > 80 ? Theme.Danger : Theme.Accent);
                peopleTable.SetBar(i, 7, row.Contentment / 100f, UiFormat.Number(row.Contentment));
                peopleTable.SetText(i, 8, row.LoyaltyWord);
                peopleTable.SetText(i, 9, UiFormat.Money(row.Wallet));
            }

            if (people.Candidates.Count == 0)
            {
                candidates.text = string.Empty;
                return;
            }
            var text = new StringBuilder($"<color={UiTheme.ToHex(Theme.Accent)}>{UiStrings.Candidates}</color>\n");
            foreach (CandidateRow candidate in people.Candidates)
            {
                text.Append(LinkCodec.Wrap(candidate.Name, new TextLink(TextLinkKind.Adventurer, candidate.Id), UiTheme.ToHex(Theme.Text)))
                    .Append(" — ").Append(candidate.Archetype).Append(", ").Append(candidate.Waits).Append('\n');
            }
            candidates.text = text.ToString().TrimEnd('\n');
        }

        /// <summary>Значки ран: ■ акцентом — лёгкая, ■ красным — тяжёлая, «К» — калека.</summary>
        private string WoundBadges(int level, bool maimed)
        {
            string badge = level == 2 ? $"<color={UiTheme.ToHex(Theme.Danger)}>■</color>"
                : level == 1 ? $"<color={UiTheme.ToHex(Theme.Accent)}>■</color>"
                : string.Empty;
            return maimed ? badge + $" <color={UiTheme.ToHex(Theme.TextDim)}>К</color>" : badge;
        }

        // ---------- Группы ----------

        private void BuildParties(RectTransform page)
        {
            partiesContent = Factory.Scroll(page, "Parties", out ScrollRect scroll, 10);
            UiFactory.Size(scroll, flexWidth: 1, flexHeight: 1);
        }

        private void RefreshParties()
        {
            List<PartyRow> rows = PartiesModel.Build(Client);
            int count = Math.Max(1, rows.Count);
            while (partyLabels.Count < count)
            {
                TextMeshProUGUI label = Factory.LinkLabel(partiesContent, Context.OnLink);
                partyLabels.Add(label);
            }
            for (int i = 0; i < partyLabels.Count; i++) partyLabels[i].gameObject.SetActive(i < count);

            if (rows.Count == 0)
            {
                partyLabels[0].text = $"<color={UiTheme.ToHex(Theme.TextDim)}>{UiStrings.NoParties}</color>";
                return;
            }

            string accent = UiTheme.ToHex(Theme.Accent);
            string text = UiTheme.ToHex(Theme.Text);
            string dim = UiTheme.ToHex(Theme.TextDim);
            for (int i = 0; i < rows.Count; i++)
            {
                PartyRow row = rows[i];
                var builder = new StringBuilder();
                string marker = row.Id == selectedParty ? "► " : string.Empty;
                builder.Append($"<size=120%><color={accent}>{marker}{LinkCodec.Escape(row.Name)}</color></size>");
                if (row.OnQuest) builder.Append($"  <color={dim}>{UiStrings.PartyOnQuest}</color>");
                builder.Append('\n');
                for (int m = 0; m < row.Members.Count; m++)
                {
                    if (m > 0) builder.Append(", ");
                    builder.Append(LinkCodec.Wrap(row.Members[m].Name, new TextLink(TextLinkKind.Adventurer, row.Members[m].Id), text));
                }
                builder.Append($"\n<color={dim}>{row.Stats}</color>");
                partyLabels[i].text = builder.ToString();
            }
        }

        // ---------- Постройки ----------

        private void BuildBuildings(RectTransform page)
        {
            var columns = new List<TableColumn>
            {
                new TableColumn { Title = UiStrings.ColBuilding, Width = -1 },
                new TableColumn { Title = UiStrings.ColState, Width = 320 },
                new TableColumn { Title = UiStrings.ColCapacity, Width = 110 },
                new TableColumn { Title = UiStrings.ColCost, Width = 120 },
                new TableColumn { Title = UiStrings.ColBuildTime, Width = 100 },
            };
            buildingsTable = new TableView(Factory, page, columns, id =>
            {
                selectedBuilding = id;
                RefreshBuildings();
            });

            buildButton = Factory.Button(page, UiStrings.SelectBuilding, OrderSelectedBuilding, 620);

            RectTransform queueHeader = UiFactory.Node(page, "QueueHeader");
            Factory.Horizontal(queueHeader, 12);
            UiFactory.Size(queueHeader, height: Theme.FontSizeLarge + 10);
            TextMeshProUGUI queueTitle = Factory.Label(queueHeader, UiStrings.BuildQueue, Theme.FontSizeLarge, Theme.Accent);
            UiFactory.Size(queueTitle, 260);
            TextMeshProUGUI hint = Factory.Label(queueHeader, UiStrings.QueueHint, Theme.FontSizeSmall, Theme.TextDim);
            UiFactory.Size(hint, flexWidth: 1);

            queueList = UiFactory.Node(page, "Queue");
            Factory.Vertical(queueList, 4);
            queueEmpty = Factory.Label(page, UiStrings.QueueEmpty, Theme.FontSizeSmall, Theme.TextDim);
            UiFactory.Size(queueEmpty, height: Theme.RowHeight);
        }

        /// <summary>Выбранная строка «Построек» (id постройки или отрицательный номер не начатой).</summary>
        public int SelectedBuilding
        {
            get => selectedBuilding;
            set => selectedBuilding = value;
        }

        /// <summary>Строки вкладки «Постройки» после последней перерисовки.</summary>
        public IReadOnlyList<BuildingRow> BuildingRows => buildingRows;

        /// <summary>Заказать выбранную постройку, если её можно заказать.</summary>
        public void OrderSelectedBuilding()
        {
            BuildingRow row = SelectedBuildingRow();
            if (row == null || !row.CanOrder) return;
            BuildingActions.Order(Client, row.DefinitionId);
            Refresh();
        }

        private BuildingRow SelectedBuildingRow()
        {
            foreach (BuildingRow row in buildingRows)
            {
                if (row.RowId == selectedBuilding) return row;
            }
            return null;
        }

        private void RefreshBuildings()
        {
            buildingRows = BuildingsModel.Build(Client);
            buildingsTable.SetSelected(selectedBuilding);
            buildingsTable.SetRowCount(buildingRows.Count);
            for (int i = 0; i < buildingRows.Count; i++)
            {
                BuildingRow row = buildingRows[i];
                buildingsTable.SetRowId(i, row.RowId);
                buildingsTable.SetText(i, 0, row.Name, row.Id == 0 ? Theme.TextDim : (Color?)null);
                buildingsTable.SetText(i, 1, row.State, row.Ready ? (Color?)null : row.Id != 0 ? Theme.Accent : Theme.TextDim);
                buildingsTable.SetText(i, 2, row.Capacity);
                buildingsTable.SetText(i, 3, row.Ready ? "—" : UiFormat.Money(row.Cost), Theme.TextDim);
                buildingsTable.SetText(i, 4, row.Ready ? "—" : string.Format(UiStrings.DaysFormat, row.BuildDays), Theme.TextDim);
            }

            BuildingRow selected = SelectedBuildingRow();
            bool canOrder = selected != null && selected.CanOrder;
            buildButton.interactable = canOrder;
            Factory.SetButtonColors(buildButton, canOrder);
            buildButton.GetComponentInChildren<TextMeshProUGUI>().text = canOrder
                ? string.Format(UiStrings.BuildFormat, selected.Name, UiFormat.Money(selected.Cost), selected.BuildDays)
                : selected != null ? selected.Name + " — " + selected.State : UiStrings.SelectBuilding;

            RefreshQueue();
        }

        private void RefreshQueue()
        {
            queueRows = BuildingActions.Queue(Client);
            queueEmpty.gameObject.SetActive(queueRows.Count == 0);
            while (queueViews.Count < queueRows.Count) queueViews.Add(CreateQueueRow(queueViews.Count));
            for (int i = 0; i < queueViews.Count; i++)
            {
                bool show = i < queueRows.Count;
                queueViews[i].Root.gameObject.SetActive(show);
                if (show) queueViews[i].Text.text = $"{i + 1}. {queueRows[i].Name} <color={UiTheme.ToHex(Theme.TextDim)}>— {queueRows[i].Details}</color>";
            }
        }

        private (RectTransform, TextMeshProUGUI) CreateQueueRow(int index)
        {
            Image panel = Factory.Panel(queueList, "Queued", Theme.PanelAlt, blocksClicks: true);
            HorizontalLayoutGroup layout = Factory.Horizontal(panel, 6);
            layout.padding = new RectOffset(10, 6, 2, 2);
            UiFactory.Size(panel, height: Theme.RowHeight + 4);
            panel.gameObject.AddComponent<DragReorder>().Dropped = (from, to) => MoveQueued(from, to);

            TextMeshProUGUI text = Factory.Label(panel.transform, string.Empty);
            UiFactory.Size(text, flexWidth: 1);
            Factory.Button(panel.transform, UiStrings.Up, () => MoveQueued(index, index - 1), 44);
            Factory.Button(panel.transform, UiStrings.Down, () => MoveQueued(index, index + 1), 44);
            Factory.Button(panel.transform, UiStrings.RemoveFromQueue, () =>
            {
                if (index < queueRows.Count) BuildingActions.Cancel(Client, queueRows[index].Id);
                Refresh();
            }, 120, fontSize: Theme.FontSizeSmall);
            return (panel.rectTransform, text);
        }

        /// <summary>Переставить стройку очереди с места <paramref name="from"/> на <paramref name="to"/>.</summary>
        public void MoveQueued(int from, int to)
        {
            if (from < 0 || from >= queueRows.Count) return;
            BuildingActions.MoveTo(Client, queueRows[from].Id, to);
            Refresh();
        }

        // ---------- Персонал ----------

        private void BuildStaff(RectTransform page)
        {
            var columns = new List<TableColumn>
            {
                new TableColumn { Title = UiStrings.ColName, Width = 220 },
                new TableColumn { Title = UiStrings.ColRole, Width = -1 },
                new TableColumn { Title = UiStrings.ColLevel, Width = 110 },
                new TableColumn { Title = UiStrings.ColSalary, Width = 130 },
                new TableColumn { Title = UiStrings.ColSalaryDebt, Width = 130 },
            };
            staffTable = new TableView(Factory, page, columns, id =>
            {
                selectedStaff = id;
                RefreshStaff();
            });

            RectTransform actions = UiFactory.Node(page, "Actions");
            Factory.Horizontal(actions, 12);
            UiFactory.Size(actions, height: Theme.RowHeight);
            staffSelected = Factory.Label(actions, string.Empty, Theme.FontSize, Theme.TextDim);
            UiFactory.Size(staffSelected, 420);
            dismissButton = Factory.Button(actions, UiStrings.Dismiss, OnDismiss, 240);

            Factory.Heading(page, UiStrings.Vacancies);
            vacancies = Factory.Label(page, string.Empty, Theme.FontSizeSmall, wrap: true);

            Factory.Heading(page, UiStrings.StaffCandidates);
            staffCandidateList = UiFactory.Node(page, "Candidates");
            Factory.Vertical(staffCandidateList, 4);
        }

        /// <summary>Выбранный сотрудник на «Персонале» (id).</summary>
        public int SelectedStaff
        {
            get => selectedStaff;
            set => selectedStaff = value;
        }

        /// <summary>«Уволить»: первое нажатие спрашивает «Точно уволить?», второе — увольняет.</summary>
        public void OnDismiss()
        {
            if (!Client.World.Staff.TryGetMember(selectedStaff, out _)) return;
            if (dismissArmedFor != selectedStaff)
            {
                dismissArmedFor = selectedStaff;
                RefreshStaff();
                return;
            }
            dismissArmedFor = 0;
            StaffModel.Dismiss(Client, selectedStaff);
            Refresh();
        }

        private void RefreshStaff()
        {
            List<StaffRow> rows = StaffModel.Members(Client);
            staffTable.SetSelected(selectedStaff);
            staffTable.SetRowCount(rows.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                StaffRow row = rows[i];
                staffTable.SetRowId(i, row.Id);
                staffTable.SetText(i, 0, row.Name);
                staffTable.SetText(i, 1, row.Role);
                staffTable.SetText(i, 2, row.Level.ToString());
                staffTable.SetText(i, 3, UiFormat.Money(row.Salary));
                staffTable.SetText(i, 4, row.Unpaid > 0 ? UiFormat.Money(row.Unpaid) : "—", row.Unpaid > 0 ? Theme.Danger : Theme.TextDim);
            }

            bool member = Client.World.Staff.TryGetMember(selectedStaff, out StaffMember selected);
            if (dismissArmedFor != 0 && dismissArmedFor != selectedStaff) dismissArmedFor = 0;
            staffSelected.text = member ? $"{selected.Name}, {StaffModel.RoleName(Client.Data, selected.RoleId)}" : UiStrings.SelectStaff;
            dismissButton.interactable = member;
            bool armed = member && dismissArmedFor == selectedStaff;
            dismissButton.GetComponentInChildren<TextMeshProUGUI>().text = armed ? UiStrings.DismissConfirm : UiStrings.Dismiss;
            Factory.SetButtonColors(dismissButton, armed);

            List<string> vacancyLines = StaffModel.Vacancies(Client);
            vacancies.text = vacancyLines.Count == 0
                ? $"<color={UiTheme.ToHex(Theme.TextDim)}>{UiStrings.NoVacancies}</color>"
                : string.Join("\n", vacancyLines);

            RefreshStaffCandidates();
        }

        private void RefreshStaffCandidates()
        {
            List<StaffCandidateRow> rows = StaffModel.Candidates(Client, Context.Notifications?.RejectedStaffCandidates);
            while (staffCandidateViews.Count < rows.Count) staffCandidateViews.Add(CreateStaffCandidate());
            string dim = UiTheme.ToHex(Theme.TextDim);
            for (int i = 0; i < staffCandidateViews.Count; i++)
            {
                StaffCandidateView view = staffCandidateViews[i];
                bool show = i < rows.Count;
                view.Root.gameObject.SetActive(show);
                if (!show) continue;

                StaffCandidateRow row = rows[i];
                bool fresh = view.CandidateId != row.Id;
                view.CandidateId = row.Id;
                string marker = row.Id == selectedStaff ? "► " : string.Empty;
                string line = string.Format(UiStrings.StaffCandidateRowFormat, row.Role, row.Name, row.Level, UiFormat.Money(row.AskedSalary), row.Waits);
                view.Text.text = row.Rejected ? $"<color={dim}>{line} · {UiStrings.Rejected}</color>" : marker + line;
                view.Offer.gameObject.SetActive(!row.Rejected);
                view.OfferButton.gameObject.SetActive(!row.Rejected);
                view.RejectButton.gameObject.SetActive(!row.Rejected);
                if (fresh && !view.Offer.isFocused) view.Offer.text = row.AskedSalary.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        private StaffCandidateView CreateStaffCandidate()
        {
            var view = new StaffCandidateView();
            Image panel = Factory.Panel(staffCandidateList, "Candidate", Theme.PanelAlt);
            view.Root = panel.rectTransform;
            HorizontalLayoutGroup layout = Factory.Horizontal(panel, 8);
            layout.padding = new RectOffset(10, 6, 2, 2);
            UiFactory.Size(panel, height: Theme.RowHeight + 4);

            view.Text = Factory.Label(panel.transform, string.Empty, Theme.FontSizeSmall);
            UiFactory.Size(view.Text, flexWidth: 1);
            view.Offer = Factory.Input(panel.transform, UiStrings.SalaryHint, 130, TMP_InputField.ContentType.IntegerNumber);
            view.OfferButton = Factory.Button(panel.transform, UiStrings.Offer, () => OfferSalary(view), 170, fontSize: Theme.FontSizeSmall);
            Factory.SetButtonColors(view.OfferButton, true);
            view.RejectButton = Factory.Button(panel.transform, UiStrings.Reject, () =>
            {
                Context.Notifications?.RejectStaffCandidate(view.CandidateId);
                Refresh();
            }, 140, fontSize: Theme.FontSizeSmall);
            return view;
        }

        private void OfferSalary(StaffCandidateView view)
        {
            if (!UiFormat.TryParseAmount(view.Offer.text, out int amount)) return;
            StaffModel.Offer(Client, view.CandidateId, amount);
            Refresh();
        }
    }
}
