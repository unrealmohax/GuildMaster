using System.Collections.Generic;
using System.Text;
using GuildMaster.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GuildMaster.UI
{
    /// <summary>
    /// Экран «Задания»: слева — идущие и недавно вернувшиеся, справа — выбранное: заказ, участники, полоска фазы,
    /// проваленные раунды (5 делений), лента задания.
    /// </summary>
    public sealed class QuestsScreen : ScreenView
    {
        private readonly QuestsModel model = new QuestsModel();
        private readonly TableView active;
        private readonly TableView recent;
        private readonly TextMeshProUGUI title;
        private readonly TextMeshProUGUI order;
        private readonly TextMeshProUGUI members;
        private readonly TextMeshProUGUI phase;
        private readonly BarView phaseBar;
        private readonly Image[] failures = new Image[QuestsModel.FailureSteps];
        private readonly TextMeshProUGUI hidden;
        private readonly FeedView feed;

        public QuestsScreen(UiContext context, RectTransform parent) : base(context)
        {
            Root = UiFactory.Stretch(UiFactory.Node(parent, "Quests"));
            Factory.Horizontal(Root, Theme.Padding, 0).childForceExpandHeight = true;

            RectTransform lists = UiFactory.Node(Root, "Lists");
            Factory.Vertical(lists, 8);
            UiFactory.Size(lists, flexWidth: 1, flexHeight: 1);
            Factory.Heading(lists, UiStrings.ActiveQuests);
            active = new TableView(Factory, lists, Columns(UiStrings.ColActivity), Select);
            UiFactory.Size(active.Root, flexHeight: 2);
            Factory.Heading(lists, UiStrings.RecentQuests);
            recent = new TableView(Factory, lists, Columns(UiStrings.ColState), Select);
            UiFactory.Size(recent.Root, flexHeight: 1);

            Image detail = Factory.Panel(Root, "Detail", Theme.Panel);
            Factory.Vertical(detail, 8, Theme.Padding * 1.5f);
            UiFactory.Size(detail, 720, flexWidth: 0, flexHeight: 1);
            title = Factory.Label(detail.transform, string.Empty, Theme.FontSizeLarge, Theme.Accent, wrap: true);
            order = Factory.LinkLabel(detail.transform, Context.OnLink, Theme.FontSizeSmall);
            members = Factory.LinkLabel(detail.transform, Context.OnLink);
            phase = Factory.Label(detail.transform, string.Empty, Theme.FontSizeSmall, Theme.TextDim);
            phaseBar = new BarView(Factory, detail.transform, height: 16);

            RectTransform failureRow = UiFactory.Node(detail.transform, "Failures");
            Factory.Horizontal(failureRow, 6);
            UiFactory.Size(failureRow, height: 24);
            TextMeshProUGUI failureLabel = Factory.Label(failureRow, UiStrings.FailedRounds, Theme.FontSizeSmall, Theme.TextDim);
            UiFactory.Size(failureLabel, 230);
            for (int i = 0; i < failures.Length; i++)
            {
                failures[i] = Factory.Panel(failureRow, "Step" + i, Theme.BarBack);
                UiFactory.Size(failures[i], 40, 18);
            }

            hidden = Factory.Label(detail.transform, string.Empty, Theme.FontSizeSmall, Theme.Danger, wrap: true);
            Factory.Heading(detail.transform, UiStrings.QuestFeed);
            feed = new FeedView(Factory, detail.transform, Context.OnLink);
            UiFactory.Size(feed.Scroll, flexHeight: 1);
        }

        public override ScreenId Id => ScreenId.Quests;
        public override string Title => UiStrings.Quests;

        public QuestsModel Model => model;

        private static List<TableColumn> Columns(string last) => new List<TableColumn>
        {
            new TableColumn { Title = UiStrings.ColType, Width = 140 },
            new TableColumn { Title = UiStrings.ColPlace, Width = -1 },
            new TableColumn { Title = UiStrings.QuestMembers, Width = 260 },
            new TableColumn { Title = last, Width = 200 },
        };

        private void Select(int id)
        {
            model.SelectedId = id;
            Context.Navigator.LastQuestId = id;
            Refresh();
        }

        public override void Select(Destination destination)
        {
            if (destination.SelectId == 0) return;
            model.SelectedId = destination.SelectId;
            Context.Navigator.LastQuestId = destination.SelectId;
        }

        public override void Refresh()
        {
            model.RevealAll = Context.RevealAll;
            model.Refresh(Client);
            Fill(active, model.Active, finished: false);
            Fill(recent, model.Recent, finished: true);

            title.text = model.DetailTitle;
            order.text = model.DetailOrderId != 0
                ? $"{LinkCodec.Wrap(UiStrings.QuestOrder, new TextLink(TextLinkKind.Order, model.DetailOrderId), UiTheme.ToHex(Theme.Text))}: {LinkCodec.Escape(model.DetailOrder)}"
                : string.Empty;
            members.text = MembersText();
            phase.text = model.Selected != null ? model.DetailPhase : string.Empty;
            phaseBar.Root.gameObject.SetActive(model.Selected != null);
            phaseBar.Set(model.DetailProgress, string.Empty);
            for (int i = 0; i < failures.Length; i++) failures[i].color = i < model.DetailFailedRounds ? Theme.Danger : Theme.BarBack;
            hidden.text = model.DetailHidden;

            if (model.Selected != null) feed.Show(model.Selected.Log, Client.Calendar);
            else feed.Clear();
        }

        private void Fill(TableView table, List<QuestRow> rows, bool finished)
        {
            table.SetSelected(model.SelectedId);
            table.SetRowCount(rows.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                QuestRow row = rows[i];
                table.SetRowId(i, row.Id);
                table.SetText(i, 0, row.Type);
                table.SetText(i, 1, row.Place);
                table.SetText(i, 2, row.Party);
                if (finished) table.SetText(i, 3, row.Outcome, row.Success ? (Color?)null : Theme.Danger);
                else table.SetText(i, 3, row.Phase, Theme.Accent);
            }
        }

        private string MembersText()
        {
            if (model.Selected == null) return string.Empty;
            string text = UiTheme.ToHex(Theme.Text);
            string dim = UiTheme.ToHex(Theme.TextDim);
            string danger = UiTheme.ToHex(Theme.Danger);
            string accent = UiTheme.ToHex(Theme.Accent);
            var builder = new StringBuilder($"<color={dim}>{UiStrings.QuestMembers}</color>\n");
            foreach (QuestMemberRow row in model.Members)
            {
                string wound = row.WoundLevel == 2 ? $" <color={danger}>■</color>" : row.WoundLevel == 1 ? $" <color={accent}>■</color>" : string.Empty;
                string status = string.IsNullOrEmpty(row.Status) ? string.Empty : $" <color={(row.Dead ? danger : dim)}>— {row.Status}</color>";
                builder.Append(LinkCodec.Wrap(row.Name, new TextLink(TextLinkKind.Adventurer, row.Id), text)).Append(wound).Append(status).Append('\n');
            }
            return builder.ToString().TrimEnd('\n');
        }
    }
}
