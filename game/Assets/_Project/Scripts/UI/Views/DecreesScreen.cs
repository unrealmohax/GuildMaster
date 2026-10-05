using System.Collections.Generic;
using GuildMaster.Core;
using GuildMaster.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GuildMaster.UI
{
    /// <summary>
    /// Экран «Распоряжения»: карточка на каждое — название и состояние, переключатель, описание, плюс и цена, срок (кнопки)
    /// и сколько осталось, область по рангам (кнопки G–C, у распоряжений с областью), расходы за месяц. Действия — через
    /// <see cref="DecreesModel"/>, командами.
    /// </summary>
    public sealed class DecreesScreen : ScreenView
    {
        private static readonly GuildRank[] AllRanks = { GuildRank.G, GuildRank.F, GuildRank.E, GuildRank.D, GuildRank.C };

        private readonly DecreesModel model = new DecreesModel();
        private readonly RectTransform list;
        private readonly List<CardView> views = new List<CardView>();

        public DecreesScreen(UiContext context, RectTransform parent) : base(context)
        {
            Root = UiFactory.Stretch(UiFactory.Node(parent, "Decrees"));
            Factory.Vertical(Root, 8).childForceExpandHeight = false;
            list = Factory.Scroll(Root, "List", out ScrollRect scroll, 10);
            UiFactory.Size(scroll, flexWidth: 1, flexHeight: 1);
        }

        public override ScreenId Id => ScreenId.Decrees;
        public override string Title => UiStrings.DecreesScreen;

        public DecreesModel Model => model;

        public override void Refresh()
        {
            model.Refresh(Client);
            while (views.Count < model.Cards.Count) views.Add(new CardView(this, model.Cards[views.Count]));
            for (int i = 0; i < views.Count; i++) views[i].Refresh(i < model.Cards.Count ? model.Cards[i] : null);
        }

        private sealed class CardView
        {
            private readonly DecreesScreen screen;
            private readonly string id;
            private readonly Image panel;
            private readonly TextMeshProUGUI state;
            private readonly Button toggle;
            private readonly TextMeshProUGUI toggleText;
            private readonly TextMeshProUGUI remaining;
            private readonly TextMeshProUGUI cost;
            private readonly List<(DecreeDuration Duration, Button Button)> durations = new List<(DecreeDuration, Button)>();
            private readonly List<(GuildRank Rank, Button Button)> ranks = new List<(GuildRank, Button)>();

            public CardView(DecreesScreen screen, DecreeCard card)
            {
                this.screen = screen;
                id = card.Id;
                UiFactory factory = screen.Factory;
                UiTheme theme = screen.Theme;
                float row = theme.RowHeight;

                panel = factory.Panel(screen.list, "Decree_" + card.Id, theme.Panel);
                factory.Vertical(panel, 4, theme.Padding);

                RectTransform header = UiFactory.Node(panel.transform, "Header");
                factory.Horizontal(header, 12);
                UiFactory.Size(header, height: row + 6);
                TextMeshProUGUI name = factory.Label(header, card.Name, theme.FontSizeLarge, theme.Accent);
                UiFactory.Size(name, flexWidth: 1);
                state = factory.Label(header, string.Empty, theme.FontSize, theme.TextDim, align: TextAlignmentOptions.MidlineRight);
                UiFactory.Size(state, 200);
                toggle = factory.Button(header, UiStrings.DecreeEnable, () => screen.Act(m => m.Toggle(screen.Client, id)), 170, row + 6);
                toggleText = toggle.GetComponentInChildren<TextMeshProUGUI>();

                Line(factory, theme, card.Description, theme.Text);
                Line(factory, theme, string.Format(UiStrings.DecreePlusFormat, card.Plus), theme.Text);
                Line(factory, theme, string.Format(UiStrings.DecreePriceFormat, card.Price), theme.Text);

                RectTransform term = UiFactory.Node(panel.transform, "Term");
                factory.Horizontal(term, 8);
                UiFactory.Size(term, height: row);
                UiFactory.Size(factory.Label(term, UiStrings.DecreeTerm, theme.FontSize, theme.TextDim), 160);
                DecreesBalance balance = screen.Client.Data.Balance.Decrees;
                foreach (DecreeDuration duration in card.Durations)
                {
                    DecreeDuration value = duration;
                    Button button = factory.Button(term, DecreesModel.DurationLabel(duration, balance),
                        () => screen.Act(m => m.SetDuration(screen.Client, id, value)), 130, row);
                    durations.Add((duration, button));
                }
                remaining = factory.Label(term, string.Empty, theme.FontSize, theme.TextDim);
                UiFactory.Size(remaining, flexWidth: 1);

                if (card.HasRankScope)
                {
                    RectTransform scope = UiFactory.Node(panel.transform, "Scope");
                    factory.Horizontal(scope, 8);
                    UiFactory.Size(scope, height: row);
                    UiFactory.Size(factory.Label(scope, UiStrings.DecreeScope, theme.FontSize, theme.TextDim), 160);
                    foreach (GuildRank rank in AllRanks)
                    {
                        GuildRank value = rank;
                        Button button = factory.Button(scope, rank.ToString(), () => screen.Act(m => m.ToggleRank(screen.Client, id, value)), 48, row);
                        ranks.Add((rank, button));
                    }
                }

                cost = Line(factory, theme, string.Empty, theme.TextDim, theme.FontSizeSmall);
            }

            public void Refresh(DecreeCard card)
            {
                panel.gameObject.SetActive(card != null);
                if (card == null) return;

                UiFactory factory = screen.Factory;
                state.text = card.IsOn ? UiStrings.DecreeOnState : UiStrings.DecreeOffState;
                state.color = card.IsOn ? screen.Theme.Accent : screen.Theme.TextDim;
                toggleText.text = card.IsOn ? UiStrings.DecreeDisable : UiStrings.DecreeEnable;
                factory.SetButtonColors(toggle, card.IsOn);
                foreach ((DecreeDuration duration, Button button) in durations) factory.SetButtonColors(button, duration == card.Duration);
                foreach ((GuildRank rank, Button button) in ranks) factory.SetButtonColors(button, card.Ranks.Contains(rank));
                remaining.text = card.Remaining;
                string costText = string.Format(UiStrings.DecreeMonthCostFormat, UiFormat.Money(card.MonthCost));
                cost.text = card.IsBenefit ? costText + " · " + UiStrings.DecreeBenefitHint : costText;
            }

            private TextMeshProUGUI Line(UiFactory factory, UiTheme theme, string text, Color color, float size = 0)
            {
                TextMeshProUGUI label = factory.Label(panel.transform, text, size, color);
                UiFactory.Size(label, height: theme.RowHeight);
                return label;
            }
        }

        private void Act(System.Action<DecreesModel> action)
        {
            action(model);
            Refresh();
        }
    }
}
