using System.Collections.Generic;
using System.Globalization;
using System.Text;
using GuildMaster.Core;
using GuildMaster.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GuildMaster.UI
{
    /// <summary>
    /// Экран «Доска»: сверху — правила Регистратора и комиссия, под ними — заказы, которые ждут решения игрока («Принять» /
    /// «Отклонить», у событийного — ранг), дальше — заказы на доске и в работе; справа — выбранный заказ с описанием, намёками
    /// и доплатой. Всё меняется командами; на паузе результат виден сразу.
    /// </summary>
    public sealed class BoardScreen : ScreenView
    {
        private sealed class AwaitingView
        {
            public RectTransform Root;
            public TextMeshProUGUI Text;
            public TMP_InputField Surcharge;
            public CycleSelector Rank;
            public int OrderId;
            public bool IsEventQuest;
        }

        private static readonly GuildRank[] AllRanks = { GuildRank.G, GuildRank.F, GuildRank.E, GuildRank.D, GuildRank.C };

        private readonly BoardModel model = new BoardModel();
        private readonly RegistrarModel registrar = new RegistrarModel();
        private readonly TableView table;
        private readonly TextMeshProUGUI title;
        private readonly TextMeshProUGUI facts;
        private readonly TextMeshProUGUI description;
        private readonly TextMeshProUGUI people;
        private readonly TextMeshProUGUI hidden;
        private readonly RectTransform surchargeRow;
        private readonly TMP_InputField surchargeInput;
        private readonly TextMeshProUGUI surchargeNote;
        private readonly List<Button> typeButtons = new List<Button>();
        private readonly CycleSelector maxRank;
        private readonly TMP_InputField minReward;
        private readonly Slider commission;
        private readonly SliderReleaseHandler commissionHandle;
        private readonly TextMeshProUGUI commissionLabel;
        private readonly RectTransform awaitingBlock;
        private readonly RectTransform awaitingList;
        private readonly List<AwaitingView> awaitingViews = new List<AwaitingView>();
        private int surchargeFilledFor = -1;

        public BoardScreen(UiContext context, RectTransform parent) : base(context)
        {
            Root = UiFactory.Stretch(UiFactory.Node(parent, "Board"));
            Factory.Horizontal(Root, Theme.Padding, 0).childForceExpandHeight = true;

            RectTransform main = UiFactory.Node(Root, "Main");
            Factory.Vertical(main, 8);
            UiFactory.Size(main, flexWidth: 1, flexHeight: 1);

            // Правила Регистратора и комиссия.
            Image rules = Factory.Panel(main, "Rules", Theme.Panel);
            Factory.Vertical(rules, 6, Theme.Padding);
            RectTransform typesRow = Row(rules.transform, "Types");
            TextMeshProUGUI registrarTitle = Factory.Label(typesRow, UiStrings.Registrar, Theme.FontSizeLarge, Theme.Accent);
            UiFactory.Size(registrarTitle, 170);
            Dim(typesRow, UiStrings.RegistrarTypes, 130);
            foreach (QuestTypeDefinition type in RegistrarModel.RegistrarTypes(Client.Data))
            {
                string id = type.Id;
                typeButtons.Add(Factory.Button(typesRow, type.DisplayName, () => OnTypeToggle(id), 150, fontSize: Theme.FontSizeSmall));
            }

            RectTransform limitsRow = Row(rules.transform, "Limits");
            Dim(limitsRow, UiStrings.RegistrarMaxRank, 150);
            maxRank = new CycleSelector(Factory, limitsRow, 140);
            var rankNames = new List<string>();
            foreach (GuildRank rank in AllRanks) rankNames.Add(UiFormat.Rank(rank));
            maxRank.SetValues(rankNames);
            maxRank.Changed += index =>
            {
                registrar.SetMaxRank(Client, AllRanks[index]);
                Refresh();
            };
            Dim(limitsRow, UiStrings.RegistrarMinReward, 150);
            minReward = Factory.Input(limitsRow, "0", 110, TMP_InputField.ContentType.IntegerNumber);
            minReward.onEndEdit.AddListener(OnMinReward);
            UiFactory.Size(UiFactory.Node(limitsRow, "Gap"), 10, Theme.RowHeight);
            TextMeshProUGUI commissionTitle = Factory.Label(limitsRow, UiStrings.Commission, Theme.FontSizeLarge, Theme.Accent);
            UiFactory.Size(commissionTitle, 120);
            commission = Factory.Slider(limitsRow, 220, 0, 50, value =>
            {
                RegistrarModel.SetCommissionPercent(Client, Mathf.RoundToInt(value));
                Refresh();
            });
            commission.TryGetComponent(out commissionHandle);
            commissionLabel = Factory.Label(limitsRow, string.Empty, Theme.FontSize);
            UiFactory.Size(commissionLabel, 70);
            commission.onValueChanged.AddListener(value => commissionLabel.text = Mathf.RoundToInt(value) + "%");
            TextMeshProUGUI note = Factory.Label(rules.transform, UiStrings.RulesNote, Theme.FontSizeSmall, Theme.TextDim, wrap: true);
            UiFactory.Size(note, height: Theme.FontSizeSmall + 8);

            // Ждут решения.
            awaitingBlock = UiFactory.Node(main, "Awaiting");
            Factory.Vertical(awaitingBlock, 4);
            Factory.Heading(awaitingBlock, UiStrings.AwaitingDecision);
            awaitingList = UiFactory.Node(awaitingBlock, "List");
            Factory.Vertical(awaitingList, 4);

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
            table = new TableView(Factory, main, columns, Select);

            Image detail = Factory.Panel(Root, "Detail", Theme.Panel);
            Factory.Vertical(detail, 10, Theme.Padding * 1.5f);
            UiFactory.Size(detail, 600, flexWidth: 0, flexHeight: 1);
            title = Factory.Label(detail.transform, string.Empty, Theme.FontSizeLarge, Theme.Accent, wrap: true);
            facts = Factory.Label(detail.transform, string.Empty, Theme.FontSizeSmall, Theme.TextDim, wrap: true);
            description = Factory.Label(detail.transform, string.Empty, Theme.FontSize, wrap: true);
            people = Factory.LinkLabel(detail.transform, Context.OnLink);

            surchargeRow = Row(detail.transform, "Surcharge");
            surchargeInput = Factory.Input(surchargeRow, UiStrings.SurchargeHint, 140, TMP_InputField.ContentType.IntegerNumber);
            surchargeInput.onSubmit.AddListener(_ => ApplySurcharge());
            Factory.Button(surchargeRow, UiStrings.SetSurcharge, ApplySurcharge, 260);
            surchargeNote = Factory.Label(detail.transform, UiStrings.SurchargeNote, Theme.FontSizeSmall, Theme.TextDim, wrap: true);
            hidden = Factory.Label(detail.transform, string.Empty, Theme.FontSizeSmall, Theme.Danger, wrap: true);
        }

        public override ScreenId Id => ScreenId.Board;
        public override string Title => UiStrings.Board;

        public BoardModel Model => model;
        public RegistrarModel Registrar => registrar;

        public override void Select(Destination destination)
        {
            if (destination.SelectId != 0) model.SelectedId = destination.SelectId;
        }

        /// <summary>Доплата выбранного заказа из поля ввода (для проверки и Enter в поле).</summary>
        public void ApplySurcharge()
        {
            if (!model.CanSetSurcharge || !UiFormat.TryParseAmount(surchargeInput.text, out int amount)) return;
            BoardActions.SetSurcharge(Client, model.SelectedId, amount);
        }

        /// <summary>Поле доплаты выбранного заказа (для проверки).</summary>
        public TMP_InputField SurchargeField => surchargeInput;

        public override void Refresh()
        {
            model.RevealAll = Context.RevealAll;
            model.Refresh(Client);
            registrar.Refresh(Client);

            RefreshRules();
            RefreshAwaiting();

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

            surchargeRow.gameObject.SetActive(model.CanSetSurcharge);
            surchargeNote.gameObject.SetActive(model.CanSetSurcharge);
            if (model.CanSetSurcharge && surchargeFilledFor != model.SelectedId && !surchargeInput.isFocused)
            {
                surchargeInput.text = model.SelectedSurcharge > 0 ? model.SelectedSurcharge.ToString(CultureInfo.InvariantCulture) : string.Empty;
                surchargeFilledFor = model.SelectedId;
            }
        }

        private void Select(int id)
        {
            model.SelectedId = id;
            Refresh();
        }

        // ---------- Регистратор и комиссия ----------

        private void RefreshRules()
        {
            for (int i = 0; i < typeButtons.Count && i < registrar.Types.Count; i++)
                Factory.SetButtonColors(typeButtons[i], registrar.Types[i].Allowed);
            maxRank.SetIndex(System.Array.IndexOf(AllRanks, registrar.MaxRank));
            if (!minReward.isFocused) minReward.text = registrar.MinReward.ToString(CultureInfo.InvariantCulture);

            commission.minValue = registrar.CommissionMinPercent;
            commission.maxValue = registrar.CommissionMaxPercent;
            if (commissionHandle == null || !commissionHandle.IsHeld)
            {
                commission.SetValueWithoutNotify(registrar.CommissionPercent);
                commissionLabel.text = registrar.CommissionPercent + "%";
            }
        }

        private void OnTypeToggle(string typeId)
        {
            foreach (RegistrarTypeRow row in registrar.Types)
            {
                if (row.Id == typeId) registrar.SetTypeAllowed(Client, typeId, !row.Allowed);
            }
            Refresh();
        }

        private void OnMinReward(string text)
        {
            if (!UiFormat.TryParseAmount(text, out int amount) || amount == registrar.MinReward) return;
            registrar.SetMinReward(Client, amount);
            Refresh();
        }

        // ---------- Ждут решения ----------

        private void RefreshAwaiting()
        {
            awaitingBlock.gameObject.SetActive(model.Awaiting.Count > 0);
            while (awaitingViews.Count < model.Awaiting.Count) awaitingViews.Add(CreateAwaiting());
            for (int i = 0; i < awaitingViews.Count; i++)
            {
                AwaitingView view = awaitingViews[i];
                bool show = i < model.Awaiting.Count;
                view.Root.gameObject.SetActive(show);
                if (!show) continue;

                AwaitingRow row = model.Awaiting[i];
                bool fresh = view.OrderId != row.Id;
                view.OrderId = row.Id;
                view.IsEventQuest = row.IsEventQuest;
                view.Text.text = (row.Id == model.SelectedId ? "► " : string.Empty) + LinkCodec.Escape(row.Text);
                view.Surcharge.gameObject.SetActive(!row.IsEventQuest);
                view.Rank.Root.gameObject.SetActive(row.IsEventQuest);
                if (fresh)
                {
                    view.Surcharge.text = string.Empty;
                    view.Rank.SetIndex(System.Array.IndexOf(BoardModel.EventQuestRanks, row.SourceRank));
                }
            }
        }

        private AwaitingView CreateAwaiting()
        {
            var view = new AwaitingView();
            Image panel = Factory.Panel(awaitingList, "Order", Theme.PanelAlt);
            view.Root = panel.rectTransform;
            HorizontalLayoutGroup layout = Factory.Horizontal(panel, 8);
            layout.padding = new RectOffset(8, 8, 4, 4);
            UiFactory.Size(panel, height: Theme.RowHeight + 8);

            Button textButton = Factory.RowButton(panel.transform, "Text", () => Select(view.OrderId));
            ColorBlock colors = textButton.colors;
            colors.normalColor = Theme.PanelAlt;
            textButton.colors = colors;
            view.Text = Factory.Label(textButton.transform, string.Empty, Theme.FontSizeSmall);
            UiFactory.Stretch(view.Text.rectTransform, 6, 0, 6, 0);
            UiFactory.Size(textButton, 100, Theme.RowHeight, flexWidth: 1);

            view.Surcharge = Factory.Input(panel.transform, UiStrings.SurchargeHint, 150, TMP_InputField.ContentType.IntegerNumber);
            view.Rank = new CycleSelector(Factory, panel.transform, 150);
            var ranks = new List<string>();
            foreach (GuildRank rank in BoardModel.EventQuestRanks) ranks.Add($"{UiStrings.EventRankHint} {UiFormat.Rank(rank)}");
            view.Rank.SetValues(ranks);

            Button accept = Factory.Button(panel.transform, UiStrings.Accept, () => Answer(view, true), 140, fontSize: Theme.FontSizeSmall);
            Factory.SetButtonColors(accept, true);
            Factory.Button(panel.transform, UiStrings.Decline, () => Answer(view, false), 140, fontSize: Theme.FontSizeSmall);
            return view;
        }

        private void Answer(AwaitingView view, bool accept)
        {
            if (view.IsEventQuest)
            {
                BoardActions.AnswerEventQuest(Client, view.OrderId, accept ? BoardModel.EventQuestRanks[view.Rank.Index] : (GuildRank?)null);
            }
            else
            {
                if (!UiFormat.TryParseAmount(view.Surcharge.text, out int surcharge)) surcharge = 0;
                BoardActions.AnswerImportant(Client, view.OrderId, accept, surcharge);
            }
            Refresh();
        }

        /// <summary>Ответить на заказ из «Ждут решения», как кнопкой: принять (доплата и ранг — из строки) или отклонить.</summary>
        public void AnswerAwaiting(int orderId, bool accept)
        {
            foreach (AwaitingView view in awaitingViews)
            {
                if (view.OrderId == orderId && view.Root.gameObject.activeSelf) Answer(view, accept);
            }
        }

        // ---------- Помощники ----------

        private RectTransform Row(Transform parent, string name)
        {
            RectTransform row = UiFactory.Node(parent, name);
            Factory.Horizontal(row, 8);
            UiFactory.Size(row, height: Theme.RowHeight);
            return row;
        }

        private void Dim(Transform parent, string text, float width)
        {
            TextMeshProUGUI label = Factory.Label(parent, text, Theme.FontSizeSmall, Theme.TextDim);
            UiFactory.Size(label, width);
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
