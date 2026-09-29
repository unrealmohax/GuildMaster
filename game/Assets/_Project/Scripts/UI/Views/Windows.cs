using System.Collections.Generic;
using System.Globalization;
using System.Text;
using GuildMaster.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GuildMaster.UI
{
    /// <summary>Окно по центру поверх затемнения: заголовок, кнопка «Закрыть», тело с вертикальной раскладкой.</summary>
    public abstract class ModalWindow : WindowView
    {
        protected ModalWindow(UiContext context, RectTransform parent, string name, float width, float height, bool shade = true)
            : base(context)
        {
            Image overlay = Factory.Panel(parent, name, shade ? Theme.OverlayShade : Color.clear, blocksClicks: shade);
            Root = UiFactory.Stretch(overlay.rectTransform);

            Image frame = Factory.Panel(overlay.transform, "Frame", Theme.Panel, blocksClicks: true);
            UiFactory.Centered(frame.rectTransform, width, height);
            Frame = frame.rectTransform;
            Factory.Vertical(frame, 8, Theme.Padding * 1.5f);

            RectTransform header = UiFactory.Node(frame.transform, "Header");
            Factory.Horizontal(header, 8);
            UiFactory.Size(header, height: Theme.FontSizeTitle + 12);
            TitleLabel = Factory.Title(header, string.Empty);
            UiFactory.Size(TitleLabel, flexWidth: 1);
            Factory.Button(header, UiStrings.Close, () => Context.Navigator.Close(this), 140);

            Body = UiFactory.Node(frame.transform, "Body");
            UiFactory.Size(Body, flexWidth: 1, flexHeight: 1);
            Root.gameObject.SetActive(false);
        }

        protected RectTransform Frame { get; }
        protected TextMeshProUGUI TitleLabel { get; }
        protected RectTransform Body { get; }
    }

    /// <summary>Карточка авантюриста поверх экрана: два столбца, всё видимое игроку (<see cref="CardModel"/>).</summary>
    public sealed class CardWindow : ModalWindow
    {
        private readonly TextMeshProUGUI subtitle;
        private readonly TextMeshProUGUI rankLine;
        private readonly BarView rankBar;
        private readonly TextMeshProUGUI left;
        private readonly BarView fatigue;
        private readonly BarView stress;
        private readonly BarView contentment;
        private readonly TextMeshProUGUI right;

        public CardWindow(UiContext context, RectTransform parent) : base(context, parent, "Card", 1400, 940)
        {
            Factory.Vertical(Body, 6);

            subtitle = Factory.Label(Body, string.Empty, Theme.FontSizeLarge, Theme.TextDim);
            RectTransform rankRow = UiFactory.Node(Body, "Rank");
            Factory.Horizontal(rankRow, 12);
            UiFactory.Size(rankRow, height: Theme.RowHeight);
            rankLine = Factory.Label(rankRow, string.Empty);
            UiFactory.Size(rankLine, 560);
            rankBar = new BarView(Factory, rankRow, 360);

            RectTransform columns = UiFactory.Node(Body, "Columns");
            Factory.Horizontal(columns, 30).childForceExpandHeight = true;
            UiFactory.Size(columns, flexWidth: 1, flexHeight: 1);

            RectTransform leftContent = Factory.Scroll(columns, "Left", out ScrollRect leftScroll, 6);
            UiFactory.Size(leftScroll, flexWidth: 1, flexHeight: 1);
            left = Factory.Label(leftContent, string.Empty, wrap: true);

            RectTransform rightContent = Factory.Scroll(columns, "Right", out ScrollRect rightScroll, 6);
            UiFactory.Size(rightScroll, flexWidth: 1, flexHeight: 1);
            Factory.Heading(rightContent, UiStrings.State);
            fatigue = StateBar(rightContent, UiStrings.ColFatigue);
            stress = StateBar(rightContent, UiStrings.ColStress);
            contentment = StateBar(rightContent, UiStrings.ColContentment);
            right = Factory.LinkLabel(rightContent, Context.OnLink);
        }

        public int AdventurerId { get; private set; }

        public CardModel Model { get; private set; }

        public void SetPerson(int id) => AdventurerId = id;

        private BarView StateBar(Transform parent, string name)
        {
            RectTransform row = UiFactory.Node(parent, name);
            Factory.Horizontal(row, 10);
            UiFactory.Size(row, height: Theme.RowHeight);
            TextMeshProUGUI label = Factory.Label(row, name, Theme.FontSize, Theme.TextDim);
            UiFactory.Size(label, 180);
            return new BarView(Factory, row, 360, 22);
        }

        public override void Refresh()
        {
            CardModel model = CardModel.Build(Client, AdventurerId, Context.RevealAll);
            Model = model;
            if (!model.Found)
            {
                TitleLabel.text = UiStrings.Unknown;
                return;
            }

            string accent = UiTheme.ToHex(Theme.Accent);
            string dim = UiTheme.ToHex(Theme.TextDim);
            string text = UiTheme.ToHex(Theme.Text);

            TitleLabel.text = model.Name;
            subtitle.text = model.Subtitle;
            rankLine.text = $"{model.RankLine} · {model.Power}";
            rankBar.Set(model.RankProgress, UiFormat.Percent(model.RankProgress));

            var l = new StringBuilder();
            l.Append($"<color={accent}>{UiStrings.RoleProfile}</color>\n{model.RoleProfile}\n\n");
            l.Append($"<color={accent}>{UiStrings.Characteristics}</color>\n");
            AppendValues(l, model.Characteristics, dim);
            l.Append($"\n<color={accent}>{UiStrings.Skills}</color>\n");
            AppendValues(l, model.Skills, dim);
            l.Append($"\n<color={accent}>{UiStrings.Character}</color>\n");
            foreach ((string axis, string value) in model.Axes)
            {
                string color = value == UiStrings.Unknown ? dim : text;
                l.Append($"<color={dim}>{axis}</color><pos=45%><color={color}>{value}</color>\n");
            }
            l.Append($"\n<color={accent}>{UiStrings.Traits}</color>\n");
            l.Append(model.Traits.Count == 0 ? $"<color={dim}>{UiStrings.NoTraits}</color>" : string.Join(", ", model.Traits));
            left.text = l.ToString();

            fatigue.Set(model.Fatigue / 100f, UiFormat.Number(model.Fatigue), model.Fatigue > 80 ? Theme.Danger : Theme.Accent);
            stress.Set(model.Stress / 100f, UiFormat.Number(model.Stress), model.Stress > 80 ? Theme.Danger : Theme.Accent);
            contentment.Set(model.Contentment / 100f, UiFormat.Number(model.Contentment));

            var r = new StringBuilder();
            r.Append($"<color={dim}>{UiStrings.ColLoyalty}</color><pos=26%>{model.Loyalty}\n\n");
            r.Append($"<color={accent}>{UiStrings.Health}</color>\n{model.Health}\n\n");
            r.Append($"<color={accent}>{UiStrings.Money}</color>\n{model.Money}\n\n");
            r.Append($"<color={accent}>{UiStrings.Relations}</color>\n");
            if (model.Relations.Count == 0) r.Append($"<color={dim}>{UiStrings.NoRelations}</color>");
            for (int i = 0; i < model.Relations.Count; i++)
            {
                RelationRow relation = model.Relations[i];
                if (i > 0) r.Append(", ");
                r.Append(LinkCodec.Wrap(relation.Name, new TextLink(TextLinkKind.Adventurer, relation.Id), text)).Append($" <color={dim}>({relation.Label})</color>");
            }
            r.Append($"\n\n<color={accent}>{UiStrings.Now}</color>\n{LinkCodec.Escape(model.Now)}\n\n");
            r.Append($"<color={accent}>{UiStrings.History}</color>\n{model.History}\n");
            if (model.LastLines.Count > 0)
            {
                r.Append($"<color={dim}>{UiStrings.LastLines}</color>\n");
                foreach (FeedEntry entry in model.LastLines)
                {
                    r.Append($"<size=85%><color={dim}>{UiFormat.Stamp(Client.Calendar.At(entry.TimeHours))}</color>  ")
                        .Append(FeedFormatter.WithLinks(entry.Text, entry.Spans, text)).Append("</size>\n");
                }
            }
            right.text = r.ToString().TrimEnd('\n');
        }

        private static void AppendValues(StringBuilder builder, List<NamedValue> values, string dim)
        {
            foreach (NamedValue value in values)
                builder.Append($"<color={dim}>{value.Name}</color><pos=45%>{value.Value.ToString("0.#", CultureInfo.InvariantCulture)}\n");
        }
    }

    /// <summary>Окно «Время»: календарь (дата, до начала месяца, ближайшие события) и переключатели автопаузы.</summary>
    public sealed class TimeWindow : ModalWindow
    {
        private readonly CalendarModel model = new CalendarModel();
        private readonly TextMeshProUGUI today;
        private readonly RectTransform upcoming;
        private readonly List<Button> itemButtons = new List<Button>();
        private readonly Dictionary<AutopauseKind, Button> toggles = new Dictionary<AutopauseKind, Button>();

        public TimeWindow(UiContext context, RectTransform parent) : base(context, parent, "Time", 1100, 800)
        {
            TitleLabel.text = UiStrings.Calendar;
            Factory.Horizontal(Body, 30).childForceExpandHeight = true;

            RectTransform calendar = UiFactory.Node(Body, "Calendar");
            Factory.Vertical(calendar, 8);
            UiFactory.Size(calendar, flexWidth: 1, flexHeight: 1);
            today = Factory.Label(calendar, string.Empty, wrap: true);
            Factory.Heading(calendar, UiStrings.Upcoming);
            upcoming = Factory.Scroll(calendar, "Upcoming", out ScrollRect scroll, 2);
            UiFactory.Size(scroll, flexHeight: 1);

            RectTransform autopause = UiFactory.Node(Body, "Autopause");
            Factory.Vertical(autopause, 6);
            UiFactory.Size(autopause, 380, flexHeight: 1);
            Factory.Heading(autopause, UiStrings.AutopauseSettings);
            foreach (AutopauseKind kind in System.Enum.GetValues(typeof(AutopauseKind)))
            {
                AutopauseKind captured = kind;
                toggles[kind] = Factory.Button(autopause, UiStrings.AutopauseKindName(kind), () =>
                    Client.Send(new SetAutopauseCommand(captured, !Client.World.Autopause.IsEnabled(captured))), fontSize: Theme.FontSizeSmall);
            }
        }

        public CalendarModel Model => model;

        public override void Refresh()
        {
            model.Refresh(Client);
            today.text = $"{UiStrings.Today}: {model.Today}\n<color={UiTheme.ToHex(Theme.TextDim)}>{model.UntilMonth}</color>";

            int count = System.Math.Max(1, model.Items.Count);
            while (itemButtons.Count < count)
            {
                int index = itemButtons.Count;
                Button button = Factory.RowButton(upcoming, "Item", () => OnItem(index));
                UiFactory.Size(button, height: Theme.RowHeight + 22);
                TextMeshProUGUI label = Factory.Label(button.transform, string.Empty, Theme.FontSizeSmall, wrap: true);
                UiFactory.Stretch(label.rectTransform, 8, 0, 8, 0);
                itemButtons.Add(button);
            }
            for (int i = 0; i < itemButtons.Count; i++)
            {
                itemButtons[i].gameObject.SetActive(i < count);
                if (i >= count) continue;
                TextMeshProUGUI label = itemButtons[i].GetComponentInChildren<TextMeshProUGUI>();
                label.text = model.Items.Count == 0
                    ? $"<color={UiTheme.ToHex(Theme.TextDim)}>{UiStrings.NothingUpcoming}</color>"
                    : $"<color={UiTheme.ToHex(Theme.TextDim)}>{model.Items[i].When}</color>\n{LinkCodec.Escape(model.Items[i].Text)}";
            }

            foreach (KeyValuePair<AutopauseKind, Button> pair in toggles)
                Factory.SetButtonColors(pair.Value, Client.World.Autopause.IsEnabled(pair.Key));
        }

        private void OnItem(int index)
        {
            if (index < model.Items.Count) Context.Navigator.Go(model.Items[index].Destination);
        }
    }

    /// <summary>Окно автопаузы: строки событий, «Перейти» к объекту каждого и «Продолжить» (снять паузу).</summary>
    public sealed class AutopauseWindow : ModalWindow
    {
        private readonly RectTransform lines;
        private readonly List<(TextMeshProUGUI Text, Button GoTo)> rows = new List<(TextMeshProUGUI, Button)>();
        private List<AutopauseLine> model = new List<AutopauseLine>();

        public AutopauseWindow(UiContext context, RectTransform parent) : base(context, parent, "Autopause", 1000, 520)
        {
            TitleLabel.text = UiStrings.AutopauseTitle;
            Factory.Vertical(Body, 10);
            lines = Factory.Scroll(Body, "Lines", out ScrollRect scroll, 8);
            UiFactory.Size(scroll, flexHeight: 1);
            Button resume = Factory.Button(Body, UiStrings.Continue, () =>
            {
                Context.Navigator.Close(this);
                Context.Clock?.Resume();
            }, height: Theme.RowHeight + 10, fontSize: Theme.FontSizeLarge);
            Factory.SetButtonColors(resume, true);
        }

        /// <summary>Событие первой строки, по которому окно открыто: другое событие — новая автопауза.</summary>
        public SimEvent ShownFor { get; private set; }

        public IReadOnlyList<AutopauseLine> Lines => model;

        public void ShowFor(SimEvent first) => ShownFor = first;

        public override void Refresh()
        {
            // Строки — снимок на момент паузы; следующий такт очистит причины в мире, окно их помнит.
            if (Client.World.Autopause.Triggers.Count > 0 && Client.World.Autopause.Triggers[0].Event == ShownFor)
                model = AutopauseModel.Build(Client);

            while (rows.Count < model.Count)
            {
                RectTransform row = UiFactory.Node(lines, "Line");
                Factory.Horizontal(row, 12);
                TextMeshProUGUI text = Factory.LinkLabel(row, Context.OnLink);
                UiFactory.Size(text, flexWidth: 1);
                int index = rows.Count;
                Button goTo = Factory.Button(row, UiStrings.GoTo, () => OnGoTo(index), 150);
                rows.Add((text, goTo));
            }
            for (int i = 0; i < rows.Count; i++)
            {
                bool show = i < model.Count;
                rows[i].Text.transform.parent.gameObject.SetActive(show);
                if (!show) continue;
                AutopauseLine line = model[i];
                rows[i].Text.text = $"<color={UiTheme.ToHex(Theme.TextDim)}>{UiStrings.AutopauseKindName(line.Kind)}</color>\n" +
                                    FeedFormatter.WithLinks(line.Text, line.Spans, UiTheme.ToHex(Theme.Accent));
                rows[i].GoTo.interactable = line.Destination.IsValid;
            }
        }

        private void OnGoTo(int index)
        {
            if (index >= model.Count) return;
            Destination destination = model[index].Destination;
            Context.Navigator.Close(this);
            Context.Navigator.Go(destination);
        }
    }

    /// <summary>Список уведомлений под колокольчиком: клик — к объекту уведомления.</summary>
    public sealed class NotificationsWindow : WindowView
    {
        private readonly NotificationsModel model;
        private readonly RectTransform list;
        private readonly List<Button> buttons = new List<Button>();
        private readonly System.Action<int> openReport;

        public NotificationsWindow(UiContext context, RectTransform parent, NotificationsModel model, System.Action<int> openReport)
            : base(context)
        {
            this.model = model;
            this.openReport = openReport;
            Image overlay = Factory.Panel(parent, "Notifications", Color.clear, blocksClicks: true);
            Root = UiFactory.Stretch(overlay.rectTransform);
            overlay.gameObject.AddComponent<Button>().onClick.AddListener(() => Context.Navigator.Close(this));

            Image frame = Factory.Panel(overlay.transform, "Frame", Theme.PanelAlt, blocksClicks: true);
            RectTransform rect = frame.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(1, 1);
            rect.sizeDelta = new Vector2(720, 520);
            rect.anchoredPosition = new Vector2(-16, -Theme.TopBarHeight - 4);
            list = Factory.Scroll(frame.transform, "List", out ScrollRect scroll, 2);
            UiFactory.Stretch((RectTransform)scroll.transform, 6, 6, 6, 6);
            Root.gameObject.SetActive(false);
        }

        public override void Refresh()
        {
            model.Refresh(Client);
            int count = System.Math.Max(1, model.Items.Count);
            while (buttons.Count < count)
            {
                int index = buttons.Count;
                Button button = Factory.RowButton(list, "Item", () => OnClick(index));
                UiFactory.Size(button, height: Theme.RowHeight + 8);
                TextMeshProUGUI label = Factory.Label(button.transform, string.Empty, Theme.FontSizeSmall);
                UiFactory.Stretch(label.rectTransform, 10, 0, 10, 0);
                buttons.Add(button);
            }
            for (int i = 0; i < buttons.Count; i++)
            {
                buttons[i].gameObject.SetActive(i < count);
                if (i >= count) continue;
                buttons[i].GetComponentInChildren<TextMeshProUGUI>().text = model.Items.Count == 0
                    ? $"<color={UiTheme.ToHex(Theme.TextDim)}>{UiStrings.NoNotifications}</color>"
                    : LinkCodec.Escape(model.Items[i].Text);
            }
        }

        private void OnClick(int index)
        {
            if (index >= model.Items.Count) return;
            Notification item = model.Items[index];
            Context.Navigator.Close(this);
            if (item.ReportIndex >= 0) openReport(item.ReportIndex);
            else Context.Navigator.Go(item.Destination);
        }
    }

    /// <summary>Отчёт месяца — текстом по разделам (просмотр).</summary>
    public sealed class ReportWindow : ModalWindow
    {
        private readonly TextMeshProUGUI text;

        public ReportWindow(UiContext context, RectTransform parent) : base(context, parent, "Report", 1000, 880)
        {
            Factory.Vertical(Body, 4);
            RectTransform content = Factory.Scroll(Body, "Report", out ScrollRect scroll, 4);
            UiFactory.Size(scroll, flexHeight: 1);
            text = Factory.Label(content, string.Empty, wrap: true);
        }

        public int ReportIndex { get; set; }

        public override void Refresh()
        {
            IReadOnlyList<MonthReport> reports = Client.World.Reports.Reports;
            if (ReportIndex < 0 || ReportIndex >= reports.Count) return;
            MonthReport report = reports[ReportIndex];
            TitleLabel.text = $"{UiStrings.MonthReport}: {report.Month}.{report.Year}";
            var builder = new StringBuilder();
            foreach (string line in MonthReportText.Lines(report, Client.World, Client.Data))
            {
                bool heading = !line.Contains(":");
                builder.Append(heading ? $"\n<color={UiTheme.ToHex(Theme.Accent)}>{LinkCodec.Escape(line)}</color>\n" : LinkCodec.Escape(line) + "\n");
            }
            text.text = builder.ToString().Trim('\n');
        }
    }
}
