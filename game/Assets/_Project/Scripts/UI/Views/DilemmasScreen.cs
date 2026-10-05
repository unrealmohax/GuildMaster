using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GuildMaster.UI
{
    /// <summary>
    /// Экран «Обращения»: открытые обращения (заголовок, от кого, сколько осталось до ответа), под ними — последние закрытые
    /// (чем кончились). Клик по строке — карточка обращения поверх экрана (<see cref="DilemmaWindow"/>).
    /// </summary>
    public sealed class DilemmasScreen : ScreenView
    {
        private readonly DilemmasModel model;
        private readonly RectTransform list;
        private readonly TextMeshProUGUI openHeading;
        private readonly TextMeshProUGUI none;
        private readonly TextMeshProUGUI closedHeading;
        private readonly List<RowView> openRows = new List<RowView>();
        private readonly List<RowView> closedRows = new List<RowView>();
        private readonly RectTransform openBlock;
        private readonly RectTransform closedBlock;

        public DilemmasScreen(UiContext context, RectTransform parent, DilemmasModel model) : base(context)
        {
            this.model = model;
            Root = UiFactory.Stretch(UiFactory.Node(parent, "Dilemmas"));
            Factory.Vertical(Root, 8).childForceExpandHeight = false;
            list = Factory.Scroll(Root, "List", out ScrollRect scroll, 6);
            UiFactory.Size(scroll, flexWidth: 1, flexHeight: 1);

            openHeading = Factory.Heading(list, UiStrings.DilemmasOpen);
            none = Factory.Label(list, UiStrings.DilemmasNone, Theme.FontSize, Theme.TextDim);
            UiFactory.Size(none, height: Theme.RowHeight);
            openBlock = UiFactory.Node(list, "Open");
            Factory.Vertical(openBlock, 2);
            closedHeading = Factory.Heading(list, UiStrings.DilemmasClosed);
            closedBlock = UiFactory.Node(list, "Closed");
            Factory.Vertical(closedBlock, 2);
        }

        public override ScreenId Id => ScreenId.Dilemmas;
        public override string Title => UiStrings.DilemmasScreen;

        public DilemmasModel Model => model;

        public override void Refresh()
        {
            model.Refresh(Client);
            Fill(openRows, openBlock, model.Open, true);
            Fill(closedRows, closedBlock, model.Closed, false);
            none.gameObject.SetActive(model.Open.Count == 0);
            openHeading.gameObject.SetActive(true);
            closedHeading.gameObject.SetActive(model.Closed.Count > 0);
        }

        private void Fill(List<RowView> views, RectTransform parent, List<DilemmaRow> rows, bool open)
        {
            while (views.Count < rows.Count) views.Add(new RowView(this, parent));
            for (int i = 0; i < views.Count; i++) views[i].Refresh(i < rows.Count ? rows[i] : null, open, model.IsSent(i < rows.Count ? rows[i].Id : 0));
        }

        private void OpenCard(int id) => Context.OpenPopup(new Popup(PopupKind.Dilemma, id));

        private sealed class RowView
        {
            private readonly Button button;
            private readonly TextMeshProUGUI title;
            private readonly TextMeshProUGUI from;
            private readonly TextMeshProUGUI remaining;
            private readonly DilemmasScreen screen;
            private int id;

            public RowView(DilemmasScreen screen, RectTransform parent)
            {
                this.screen = screen;
                UiFactory factory = screen.Factory;
                UiTheme theme = screen.Theme;
                button = factory.RowButton(parent, "Row", () => screen.OpenCard(id));
                factory.Horizontal(button, 16, 6);
                UiFactory.Size(button, height: theme.RowHeight + 8);
                title = factory.Label(button.transform, string.Empty, theme.FontSize, theme.Accent);
                UiFactory.Size(title, 420);
                from = factory.Label(button.transform, string.Empty);
                UiFactory.Size(from, flexWidth: 1);
                remaining = factory.Label(button.transform, string.Empty, theme.FontSize, theme.TextDim, align: TextAlignmentOptions.MidlineRight);
                UiFactory.Size(remaining, 520);
            }

            public void Refresh(DilemmaRow row, bool open, bool sent)
            {
                button.gameObject.SetActive(row != null);
                if (row == null) return;
                id = row.Id;
                UiTheme theme = screen.Theme;
                title.text = row.Title;
                title.color = open ? theme.Accent : theme.TextDim;
                from.text = row.From;
                from.color = open ? theme.Text : theme.TextDim;
                remaining.text = sent ? UiStrings.DilemmaSent : row.Remaining;
            }
        }
    }

    /// <summary>
    /// Карточка обращения поверх экрана: от кого, заголовок, текст со ссылками, до трёх вариантов — кнопка и под ней видимые
    /// последствия; вариант, на который в казне не хватает денег, неактивен. Ответ — через <see cref="DilemmasModel.Answer"/>.
    /// </summary>
    public sealed class DilemmaWindow : ModalWindow
    {
        private const int MaxChoices = 3;

        private readonly DilemmasModel model;
        private readonly TextMeshProUGUI from;
        private readonly TextMeshProUGUI body;
        private readonly TextMeshProUGUI deadline;
        private readonly List<(Button Button, TextMeshProUGUI Text, TextMeshProUGUI Consequences)> choices =
            new List<(Button, TextMeshProUGUI, TextMeshProUGUI)>();
        private readonly int[] choiceIndex = new int[MaxChoices];

        public DilemmaWindow(UiContext context, RectTransform parent, DilemmasModel model) : base(context, parent, "Dilemma", 1100, 760)
        {
            this.model = model;
            Factory.Vertical(Body, 10);
            from = Factory.Label(Body, string.Empty, Theme.FontSizeLarge, Theme.TextDim);
            UiFactory.Size(from, height: Theme.RowHeight + 4);
            body = Factory.LinkLabel(Body, Context.OnLink, Theme.FontSizeLarge);
            body.alignment = TextAlignmentOptions.TopLeft;
            UiFactory.Size(body, flexHeight: 1);

            for (int i = 0; i < MaxChoices; i++)
            {
                int slot = i;
                RectTransform block = UiFactory.Node(Body, "Choice" + i);
                Factory.Vertical(block, 2);
                UiFactory.Size(block, height: Theme.RowHeight * 2 + 26);
                Button button = Factory.Button(block, string.Empty, () => Choose(slot), height: Theme.RowHeight + 10, fontSize: Theme.FontSizeLarge);
                TextMeshProUGUI text = button.GetComponentInChildren<TextMeshProUGUI>();
                TextMeshProUGUI consequences = Factory.Label(block, string.Empty, Theme.FontSize, Theme.TextDim, wrap: true);
                UiFactory.Size(consequences, height: Theme.RowHeight);
                choices.Add((button, text, consequences));
            }
            deadline = Factory.Label(Body, string.Empty, Theme.FontSizeSmall, Theme.TextDim);
            UiFactory.Size(deadline, height: Theme.RowHeight);
        }

        public int DilemmaId { get; set; }

        /// <summary>Карточка последнего обновления (для проверки).</summary>
        public DilemmaCard Card { get; private set; }

        public override void Refresh()
        {
            Card = model.Card(Client, DilemmaId, UiTheme.ToHex(Theme.Accent));
            if (Card == null)
            {
                TitleLabel.text = UiStrings.DilemmaGone;
                from.text = body.text = deadline.text = string.Empty;
                foreach (var choice in choices) choice.Button.transform.parent.gameObject.SetActive(false);
                return;
            }

            TitleLabel.text = Card.Title;
            from.text = string.Format(UiStrings.DilemmaFromFormat, Card.From);
            body.text = Card.Body;
            deadline.text = Card.IsOpen ? (Card.Sent ? UiStrings.DilemmaSent : Card.Deadline) : Card.Outcome;
            for (int i = 0; i < choices.Count; i++)
            {
                var view = choices[i];
                bool shown = i < Card.Choices.Count;
                view.Button.transform.parent.gameObject.SetActive(shown);
                if (!shown) continue;
                DilemmaChoice choice = Card.Choices[i];
                choiceIndex[i] = choice.Index;
                view.Text.text = choice.Text;
                view.Button.interactable = choice.Available;
                view.Consequences.text = choice.Unaffordable ? choice.Consequences + " · " + UiStrings.DilemmaUnaffordable : choice.Consequences;
                view.Consequences.color = choice.Unaffordable ? Theme.Danger : Theme.TextDim;
            }
        }

        private void Choose(int slot)
        {
            if (Card == null || slot >= Card.Choices.Count || !Card.Choices[slot].Available) return;
            if (model.Answer(Client, DilemmaId, choiceIndex[slot])) Context.Navigator.Close(this);
        }
    }
}
