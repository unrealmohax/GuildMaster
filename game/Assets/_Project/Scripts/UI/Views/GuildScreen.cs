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
    /// Экран «Гильдия»: вкладки Люди, Группы, Постройки, Персонал и лента гильдии справа (всегда на этом экране).
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
        private TableView staffTable;
        private TextMeshProUGUI staffCandidates;

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
                new TableColumn { Title = UiStrings.ColState, Width = 360 },
                new TableColumn { Title = UiStrings.ColCapacity, Width = 140 },
            };
            buildingsTable = new TableView(Factory, page, columns, id =>
            {
                selectedBuilding = id;
                buildingsTable.SetSelected(id);
            });
        }

        private void RefreshBuildings()
        {
            buildingRows = BuildingsModel.Build(Client);
            buildingsTable.SetSelected(selectedBuilding);
            buildingsTable.SetRowCount(buildingRows.Count);
            for (int i = 0; i < buildingRows.Count; i++)
            {
                BuildingRow row = buildingRows[i];
                buildingsTable.SetRowId(i, row.Id);
                buildingsTable.SetText(i, 0, row.Name, row.Id == 0 ? Theme.TextDim : (Color?)null);
                buildingsTable.SetText(i, 1, row.State, row.Ready ? (Color?)null : row.Id != 0 ? Theme.Accent : Theme.TextDim);
                buildingsTable.SetText(i, 2, row.Capacity);
            }
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
                staffTable.SetSelected(id);
            });
            staffCandidates = Factory.Label(page, string.Empty, Theme.FontSizeSmall, wrap: true);
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

            List<StaffCandidateRow> candidateRows = StaffModel.Candidates(Client);
            if (candidateRows.Count == 0)
            {
                staffCandidates.text = string.Empty;
                return;
            }
            var text = new StringBuilder($"<color={UiTheme.ToHex(Theme.Accent)}>{UiStrings.StaffCandidates}</color>\n");
            foreach (StaffCandidateRow row in candidateRows)
            {
                string marker = row.Id == selectedStaff ? "► " : string.Empty;
                text.Append($"{marker}{row.Role}: {row.Name} — {row.Details}\n");
            }
            staffCandidates.text = text.ToString().TrimEnd('\n');
        }
    }
}
