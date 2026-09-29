using System.Collections.Generic;
using GuildMaster.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GuildMaster.UI
{
    /// <summary>
    /// Экран «Казна»: казна сейчас, комиссия, банкротство; журнал операций за месяц с фильтром по статьям; справа — отчёт
    /// любого прошедшего месяца целиком.
    /// </summary>
    public sealed class TreasuryScreen : ScreenView
    {
        private readonly TreasuryModel model = new TreasuryModel();
        private readonly TextMeshProUGUI summary;
        private readonly TextMeshProUGUI bankruptcy;
        private readonly CycleSelector period;
        private readonly TextMeshProUGUI shown;
        private readonly Button allButton;
        private readonly List<(LedgerCategory Category, Button Button)> filters = new List<(LedgerCategory, Button)>();
        private readonly TableView ledger;
        private readonly CycleSelector reportSelector;
        private readonly TextMeshProUGUI reportText;

        // Выбор месяца — номер отчёта в истории (−1 — текущий месяц / последний отчёт), чтобы новый отчёт не сдвигал выбор.
        private int periodReport = -1;
        private int shownReport = -1;
        private readonly List<string> periodValues = new List<string>();
        private readonly List<string> reportValues = new List<string>();

        public TreasuryScreen(UiContext context, RectTransform parent) : base(context)
        {
            model.Limit = Theme.LedgerRowsShown;
            Root = UiFactory.Stretch(UiFactory.Node(parent, "Treasury"));
            Factory.Horizontal(Root, Theme.Padding, 0).childForceExpandHeight = true;

            RectTransform main = UiFactory.Node(Root, "Main");
            Factory.Vertical(main, 8);
            UiFactory.Size(main, flexWidth: 1, flexHeight: 1);

            Image now = Factory.Panel(main, "Now", Theme.Panel);
            Factory.Vertical(now, 4, Theme.Padding);
            Factory.Heading(now.transform, UiStrings.TreasuryNow);
            summary = Factory.Label(now.transform, string.Empty, Theme.FontSizeLarge);
            UiFactory.Size(summary, height: Theme.FontSizeLarge + 10);
            bankruptcy = Factory.Label(now.transform, string.Empty, Theme.FontSize, wrap: true);
            UiFactory.Size(bankruptcy, height: Theme.RowHeight);

            RectTransform ledgerHeader = UiFactory.Node(main, "LedgerHeader");
            Factory.Horizontal(ledgerHeader, 12);
            UiFactory.Size(ledgerHeader, height: Theme.RowHeight);
            TextMeshProUGUI title = Factory.Label(ledgerHeader, UiStrings.Ledger, Theme.FontSizeLarge, Theme.Accent);
            UiFactory.Size(title, 260);
            period = new CycleSelector(Factory, ledgerHeader, 260);
            period.Changed += index =>
            {
                IReadOnlyList<MonthReport> reports = Client.World.Reports.Reports;
                periodReport = index == 0 ? -1 : reports.Count - index;
                Refresh();
            };
            shown = Factory.Label(ledgerHeader, string.Empty, Theme.FontSizeSmall, Theme.TextDim);
            UiFactory.Size(shown, flexWidth: 1);

            allButton = FilterRow(main, UiStrings.Incomes, LedgerFlow.Income, withAll: true);
            FilterRow(main, UiStrings.Expenses, LedgerFlow.Expense, withAll: false);

            var columns = new List<TableColumn>
            {
                new TableColumn { Title = UiStrings.ColTime, Width = 110 },
                new TableColumn { Title = UiStrings.ColCategory, Width = 230 },
                new TableColumn { Title = UiStrings.ColAmount, Width = 110 },
                new TableColumn { Title = UiStrings.ColRelated, Width = -1 },
                new TableColumn { Title = UiStrings.ColBalance, Width = 110 },
            };
            ledger = new TableView(Factory, main, columns, null);

            Image reportsPanel = Factory.Panel(Root, "Reports", Theme.Panel);
            Factory.Vertical(reportsPanel, 8, Theme.Padding);
            UiFactory.Size(reportsPanel, 560, flexWidth: 0, flexHeight: 1);
            Factory.Heading(reportsPanel.transform, UiStrings.ReportsHistory);
            reportSelector = new CycleSelector(Factory, reportsPanel.transform, 300);
            reportSelector.Changed += index =>
            {
                shownReport = Client.World.Reports.Reports.Count - 1 - index;
                Refresh();
            };
            RectTransform reportContent = Factory.Scroll(reportsPanel.transform, "Report", out ScrollRect scroll, 4);
            UiFactory.Size(scroll, flexHeight: 1);
            reportText = Factory.Label(reportContent, string.Empty, Theme.FontSizeSmall, wrap: true);
        }

        public override ScreenId Id => ScreenId.Treasury;
        public override string Title => UiStrings.TreasuryScreen;

        public TreasuryModel Model => model;

        /// <summary>Номер показанного отчёта в истории; −1 — отчётов нет.</summary>
        public int ShownReport { get; private set; } = -1;

        /// <summary>Показать журнал месяца отчёта <paramref name="reportIndex"/> (−1 — текущий месяц) и этот отчёт.</summary>
        public void SelectMonth(int reportIndex)
        {
            periodReport = reportIndex;
            shownReport = reportIndex;
        }

        /// <summary>Включить или выключить статью в фильтре журнала.</summary>
        public void ToggleCategory(LedgerCategory category)
        {
            model.ToggleCategory(category.Id);
            Refresh();
        }

        public override void Refresh()
        {
            IReadOnlyList<MonthReport> reports = Client.World.Reports.Reports;
            if (periodReport >= reports.Count) periodReport = -1;
            model.PeriodIndex = periodReport < 0 ? 0 : reports.Count - periodReport;
            model.Refresh(Client);

            summary.text = $"{UiFormat.Money(model.Money)}  <size=80%><color={UiTheme.ToHex(Theme.TextDim)}>{model.Commission} · {model.MonthSoFar}</color></size>";
            summary.color = model.Money < 0 ? Theme.Danger : Theme.Text;
            bankruptcy.text = model.Bankruptcy;
            bankruptcy.color = model.BankruptcyAlarm ? Theme.Danger : Theme.TextDim;

            SetValues(period, periodValues, model.PeriodNames, model.PeriodIndex);
            shown.text = string.Format(UiStrings.LedgerShownFormat, model.Rows.Count, model.Matched);

            Factory.SetButtonColors(allButton, model.Categories.Count == 0);
            foreach ((LedgerCategory category, Button button) in filters)
            {
                Factory.SetButtonColors(button, model.Categories.Contains(category.Id));
            }

            ledger.SetRowCount(model.Rows.Count);
            for (int i = 0; i < model.Rows.Count; i++)
            {
                LedgerRow row = model.Rows[i];
                ledger.SetRowId(i, 0);
                ledger.SetText(i, 0, row.Time, Theme.TextDim);
                ledger.SetText(i, 1, row.Category);
                ledger.SetText(i, 2, UiFormat.Signed(row.Amount), row.Amount < 0 ? Theme.Danger : Theme.Text);
                ledger.SetText(i, 3, LinkCodec.Escape(row.Comment), Theme.TextDim);
                ledger.SetText(i, 4, UiFormat.Money(row.BalanceAfter), row.BalanceAfter < 0 ? Theme.Danger : Theme.Text);
            }

            RefreshReport(reports);
        }

        private void RefreshReport(IReadOnlyList<MonthReport> reports)
        {
            ShownReport = shownReport >= 0 && shownReport < reports.Count ? shownReport : reports.Count - 1;
            var names = new List<string>();
            for (int i = reports.Count - 1; i >= 0; i--) names.Add(TreasuryModel.ReportName(reports[i]));
            SetValues(reportSelector, reportValues, names, reports.Count - 1 - ShownReport);
            reportText.text = ShownReport >= 0
                ? ReportWindow.Render(reports[ShownReport], Client, Theme)
                : $"<color={UiTheme.ToHex(Theme.TextDim)}>{UiStrings.NoReports}</color>";
        }

        /// <summary>Обновить значения выбора, только если список изменился; выбор — из мира.</summary>
        private static void SetValues(CycleSelector selector, List<string> current, List<string> values, int index)
        {
            bool same = current.Count == values.Count;
            for (int i = 0; same && i < values.Count; i++) same = current[i] == values[i];
            if (!same)
            {
                current.Clear();
                current.AddRange(values);
                selector.SetValues(new List<string>(values));
            }
            selector.SetIndex(index);
        }

        private Button FilterRow(RectTransform parent, string title, LedgerFlow flow, bool withAll)
        {
            RectTransform row = UiFactory.Node(parent, title);
            Factory.Horizontal(row, 4);
            UiFactory.Size(row, height: Theme.RowHeight);
            TextMeshProUGUI label = Factory.Label(row, title, Theme.FontSizeSmall, Theme.TextDim);
            UiFactory.Size(label, 96);

            Button all = null;
            if (withAll)
            {
                all = Factory.Button(row, UiStrings.AllCategories, () =>
                {
                    model.ShowAllCategories();
                    Refresh();
                }, 70, fontSize: Theme.FontSizeSmall);
            }
            else
            {
                UiFactory.Size(UiFactory.Node(row, "Gap"), 70, Theme.RowHeight);
            }

            foreach (LedgerCategory category in LedgerCategories.All)
            {
                if (category.Flow != flow) continue;
                bool debugCategory = category == LedgerCategories.DebugIncome || category == LedgerCategories.DebugExpense;
                if (debugCategory && !Context.IsDebugBuild) continue;
                LedgerCategory captured = category;
                Button button = Factory.Button(row, TreasuryModel.CategoryName(Client.Data, category), () => ToggleCategory(captured), 134,
                    fontSize: Theme.FontSizeSmall);
                TextMeshProUGUI caption = button.GetComponentInChildren<TextMeshProUGUI>();
                caption.enableAutoSizing = true;
                caption.fontSizeMin = 11;
                caption.fontSizeMax = Theme.FontSizeSmall;
                filters.Add((category, button));
            }
            return all;
        }
    }
}
