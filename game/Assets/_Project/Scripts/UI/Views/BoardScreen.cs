using System.Collections.Generic;
using System.Text;
using GuildMaster.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GuildMaster.UI
{
    /// <summary>Экран «Доска» (просмотр): слева — заказы, справа — выбранный заказ с описанием и намёками.</summary>
    public sealed class BoardScreen : ScreenView
    {
        private readonly BoardModel model = new BoardModel();
        private readonly TableView table;
        private readonly TextMeshProUGUI title;
        private readonly TextMeshProUGUI facts;
        private readonly TextMeshProUGUI description;
        private readonly TextMeshProUGUI people;
        private readonly TextMeshProUGUI hidden;

        public BoardScreen(UiContext context, RectTransform parent) : base(context)
        {
            Root = UiFactory.Stretch(UiFactory.Node(parent, "Board"));
            Factory.Horizontal(Root, Theme.Padding, 0).childForceExpandHeight = true;

            var columns = new List<TableColumn>
            {
                new TableColumn { Title = UiStrings.ColType, Width = 130 },
                new TableColumn { Title = UiStrings.ColRank, Width = 56 },
                new TableColumn { Title = UiStrings.ColClient, Width = -1 },
                new TableColumn { Title = UiStrings.ColDistance, Width = 90 },
                new TableColumn { Title = UiStrings.ColReward, Width = 90 },
                new TableColumn { Title = UiStrings.ColSurcharge, Width = 90 },
                new TableColumn { Title = UiStrings.ColTimeLeft, Width = 170 },
                new TableColumn { Title = UiStrings.ColTakenBy, Width = 170 },
            };
            table = new TableView(Factory, Root, columns, id =>
            {
                model.SelectedId = id;
                Refresh();
            });

            Image detail = Factory.Panel(Root, "Detail", Theme.Panel);
            Factory.Vertical(detail, 10, Theme.Padding * 1.5f);
            UiFactory.Size(detail, 600, flexWidth: 0, flexHeight: 1);
            title = Factory.Label(detail.transform, string.Empty, Theme.FontSizeLarge, Theme.Accent, wrap: true);
            facts = Factory.Label(detail.transform, string.Empty, Theme.FontSizeSmall, Theme.TextDim, wrap: true);
            description = Factory.Label(detail.transform, string.Empty, Theme.FontSize, wrap: true);
            people = Factory.LinkLabel(detail.transform, Context.OnLink);
            hidden = Factory.Label(detail.transform, string.Empty, Theme.FontSizeSmall, Theme.Danger, wrap: true);
        }

        public override ScreenId Id => ScreenId.Board;
        public override string Title => UiStrings.Board;

        public BoardModel Model => model;

        public override void Select(Destination destination)
        {
            if (destination.SelectId != 0) model.SelectedId = destination.SelectId;
        }

        public override void Refresh()
        {
            model.RevealAll = Context.RevealAll;
            model.Refresh(Client);

            table.SetSelected(model.SelectedId);
            table.SetRowCount(model.Rows.Count);
            for (int i = 0; i < model.Rows.Count; i++)
            {
                OrderRow row = model.Rows[i];
                table.SetRowId(i, row.Id);
                string marks = string.IsNullOrEmpty(row.Marks) ? string.Empty : $" <color={UiTheme.ToHex(Theme.Accent)}>♦</color>";
                table.SetText(i, 0, row.Type + marks, row.InWork ? Theme.TextDim : (Color?)null);
                table.SetText(i, 1, UiFormat.Rank(row.Rank));
                table.SetText(i, 2, row.Client);
                table.SetText(i, 3, row.Distance);
                table.SetText(i, 4, UiFormat.Money(row.Reward));
                table.SetText(i, 5, row.Surcharge > 0 ? UiFormat.Money(row.Surcharge) : "—", row.Surcharge > 0 ? (Color?)null : Theme.TextDim);
                table.SetText(i, 6, row.TimeLeft, row.InWork ? Theme.TextDim : (Color?)null);
                table.SetText(i, 7, Names(row.TakenBy));
            }

            title.text = model.DetailTitle;
            facts.text = model.Selected != null && !string.IsNullOrEmpty(model.Selected.Marks)
                ? $"{model.DetailFacts}\n<color={UiTheme.ToHex(Theme.Accent)}>{model.Selected.Marks}</color>"
                : model.DetailFacts;
            description.text = LinkCodec.Escape(model.DetailDescription);
            people.text = TakenText(model.Selected);
            hidden.text = model.DetailHidden;
        }

        private static string Names(List<(int Id, string Name)> people)
        {
            var names = new List<string>(people.Count);
            foreach ((int _, string name) in people) names.Add(name);
            return string.Join(", ", names);
        }

        /// <summary>Кто взял и ссылка на задание — кликабельно.</summary>
        private string TakenText(OrderRow row)
        {
            if (row == null || (row.TakenBy.Count == 0 && row.QuestRunId == 0)) return string.Empty;
            string text = UiTheme.ToHex(Theme.Text);
            var builder = new StringBuilder($"<color={UiTheme.ToHex(Theme.TextDim)}>{UiStrings.ColTakenBy}:</color> ");
            for (int i = 0; i < row.TakenBy.Count; i++)
            {
                if (i > 0) builder.Append(", ");
                builder.Append(LinkCodec.Wrap(row.TakenBy[i].Name, new TextLink(TextLinkKind.Adventurer, row.TakenBy[i].Id), text));
            }
            if (row.QuestRunId != 0 && Client.World.Quests.TryGetRun(row.QuestRunId, out _))
                builder.Append("\n").Append(LinkCodec.Wrap(UiStrings.OrderQuestLink + " →", new TextLink(TextLinkKind.Quest, row.QuestRunId), UiTheme.ToHex(Theme.Accent)));
            return builder.ToString();
        }
    }
}
