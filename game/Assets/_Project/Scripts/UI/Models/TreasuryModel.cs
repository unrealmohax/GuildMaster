using System;
using System.Collections.Generic;
using GuildMaster.Core;
using GuildMaster.Data;

namespace GuildMaster.UI
{
    /// <summary>Запись журнала казны для таблицы.</summary>
    public sealed class LedgerRow
    {
        public string Time;
        public string Category;
        public int Amount;
        public string Comment;
        public int BalanceAfter;
    }

    /// <summary>
    /// Экран «Казна»: казна сейчас, комиссия, банкротство, журнал операций за выбранный месяц с фильтром по статьям (новые
    /// сверху, не больше <see cref="Limit"/>) и список прошедших месяцев с отчётами.
    /// </summary>
    public sealed class TreasuryModel
    {
        /// <summary>Выбранные статьи (id); пусто — все.</summary>
        public HashSet<string> Categories { get; } = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>Месяц журнала: 0 — текущий, 1 — последний прошедший, дальше — старше.</summary>
        public int PeriodIndex { get; set; }

        /// <summary>Сколько записей показывать.</summary>
        public int Limit { get; set; } = 300;

        public List<string> PeriodNames { get; } = new List<string>();
        public List<LedgerRow> Rows { get; } = new List<LedgerRow>();

        /// <summary>Сколько записей за месяц подходит под фильтр (показано не больше <see cref="Limit"/>).</summary>
        public int Matched { get; private set; }

        public int Money { get; private set; }
        public string Commission { get; private set; } = string.Empty;
        public string MonthSoFar { get; private set; } = string.Empty;
        public string Bankruptcy { get; private set; } = string.Empty;

        /// <summary>Казна в минусе, банкротство или гильдия закрыта — показывать сигналом.</summary>
        public bool BankruptcyAlarm { get; private set; }

        public bool IsShown(LedgerCategory category) => Categories.Count == 0 || Categories.Contains(category.Id);

        /// <summary>Включить или выключить статью в фильтре; выключили все — снова все.</summary>
        public void ToggleCategory(string id)
        {
            if (!Categories.Remove(id)) Categories.Add(id);
        }

        public void ShowAllCategories() => Categories.Clear();

        public void Refresh(ISimulationClient client)
        {
            WorldState world = client.World;
            Treasury treasury = world.Treasury;
            Money = treasury.Money;
            Commission = string.Format(UiStrings.CommissionFormat, UiFormat.Percent(treasury.Commission));
            FillBankruptcy(client);

            IReadOnlyList<MonthReport> reports = world.Reports.Reports;
            PeriodNames.Clear();
            PeriodNames.Add(UiStrings.CurrentMonth);
            for (int i = reports.Count - 1; i >= 0; i--) PeriodNames.Add(ReportName(reports[i]));
            PeriodIndex = Math.Max(0, Math.Min(PeriodIndex, PeriodNames.Count - 1));

            (int currentFrom, int currentTo) = Period(world, 0);
            int income = 0;
            int expense = 0;
            for (int i = currentFrom; i < currentTo; i++)
            {
                int amount = treasury.Ledger[i].Amount;
                if (amount >= 0) income += amount;
                else expense -= amount;
            }
            MonthSoFar = string.Format(UiStrings.MonthSoFarFormat, UiFormat.Money(income), UiFormat.Money(expense));

            (int from, int to) = Period(world, PeriodIndex);
            Rows.Clear();
            Matched = 0;
            for (int i = to - 1; i >= from; i--)
            {
                LedgerEntry entry = treasury.Ledger[i];
                if (!IsShown(entry.Category)) continue;
                Matched++;
                if (Rows.Count >= Limit) continue;
                Rows.Add(new LedgerRow
                {
                    Time = UiFormat.Stamp(client.Calendar.At(entry.TimeHours)),
                    Category = CategoryName(client.Data, entry.Category),
                    Amount = entry.Amount,
                    Comment = Describe(entry, client),
                    BalanceAfter = entry.BalanceAfter,
                });
            }
        }

        /// <summary>Записи журнала месяца <paramref name="index"/>: с какой по какую (не включая).</summary>
        public static (int From, int To) Period(WorldState world, int index)
        {
            IReadOnlyList<MonthReport> reports = world.Reports.Reports;
            if (index <= 0 || index > reports.Count)
                return (reports.Count > 0 ? reports[reports.Count - 1].LedgerTo : 0, world.Treasury.Ledger.Count);
            MonthReport report = reports[reports.Count - index];
            return (report.LedgerFrom, report.LedgerTo);
        }

        /// <summary>
        /// К чему относится запись — словами: заказ (тип, ранг, заказчик), человек, сотрудник, постройка, место находки, порции
        /// в таверне. Не нашли — пусто.
        /// </summary>
        public static string Describe(LedgerEntry entry, ISimulationClient client)
        {
            WorldState world = client.World;
            DataRegistry data = client.Data;
            LedgerCategory category = entry.Category;
            if (category == LedgerCategories.Commission || category == LedgerCategories.Surcharges || category == LedgerCategories.EventQuests)
            {
                return world.Orders.TryGetOrder(entry.RelatedId, out Order order)
                    ? $"{BoardModel.TypeName(data, order.TypeId)} {UiFormat.Rank(order.Rank)}, {order.Client?.Nominative ?? order.Place?.Nominative}"
                    : string.Empty;
            }
            if (category == LedgerCategories.Discoveries)
                return world.Quests.TryGetRun(entry.RelatedId, out QuestRun run) ? run.Place?.Nominative ?? string.Empty : string.Empty;
            if (category == LedgerCategories.Salaries)
            {
                if (!world.Staff.TryGetKnown(entry.RelatedId, out StaffMember member)) return string.Empty;
                string text = $"{member.Name}, {StaffModel.RoleName(data, member.RoleId)}";
                return entry.Comment.StartsWith("debt", StringComparison.Ordinal) ? text + UiStrings.LedgerSalaryDebt : text;
            }
            if (category == LedgerCategories.DebtRepayment || category == LedgerCategories.Dormitory
                || category == LedgerCategories.Infirmary || category == LedgerCategories.TrainingYard)
            {
                Adventurer person = EventTextSource.FindPerson(world, entry.RelatedId);
                return person != null ? person.Name : string.Empty;
            }
            if (category == LedgerCategories.Construction || category == LedgerCategories.Upkeep)
                return data.TryGet(entry.Comment, out BuildingDefinition building) ? building.DisplayName : string.Empty;
            if (category == LedgerCategories.Tavern) return TavernPortions(entry.Comment);
            return string.Empty;
        }

        /// <summary>«food=2 drinks=1» → «еда 2, выпивка 1».</summary>
        private static string TavernPortions(string comment)
        {
            int food = 0;
            int drinks = 0;
            foreach (string part in comment.Split(' '))
            {
                string[] pair = part.Split('=');
                if (pair.Length != 2 || !int.TryParse(pair[1], out int value)) continue;
                if (pair[0] == "food") food = value;
                else if (pair[0] == "drinks") drinks = value;
            }
            return string.Format(UiStrings.LedgerTavernFormat, food, drinks);
        }

        public static string ReportName(MonthReport report) => string.Format(UiStrings.ReportMonthFormat, report.Month, report.Year);

        public static string CategoryName(DataRegistry data, LedgerCategory category) => MonthReportText.Label(data, category.TextKey);

        private void FillBankruptcy(ISimulationClient client)
        {
            WorldState world = client.World;
            Treasury treasury = world.Treasury;
            Bankruptcy bankruptcy = treasury.Bankruptcy;
            long now = world.Time.TotalHours;
            BankruptcyAlarm = true;
            if (treasury.IsClosed)
            {
                Bankruptcy = UiStrings.GuildClosed;
            }
            else if (bankruptcy.Active)
            {
                Bankruptcy = string.Format(UiStrings.BankruptcyActiveFormat, UiFormat.Duration(bankruptcy.EndsAtHours - now, client.Calendar));
            }
            else if (bankruptcy.NegativeSinceHours.HasValue)
            {
                long since = bankruptcy.NegativeSinceHours.Value;
                long starts = since + client.Calendar.DaysToHours(client.Data.Balance.Economy.BankruptcyStartDays);
                Bankruptcy = string.Format(UiStrings.BankruptcyNegativeFormat, UiFormat.Stamp(client.Calendar.At(since)),
                    UiFormat.Duration(starts - now, client.Calendar));
            }
            else
            {
                Bankruptcy = UiStrings.BankruptcyNone;
                BankruptcyAlarm = false;
            }
        }
    }

    /// <summary>Итоги закрытой гильдии: сколько продержалась, казна, погибшие и ушедшие, заказы, лучшие люди.</summary>
    public sealed class DefeatModel
    {
        public string Days { get; private set; } = string.Empty;
        public string Money { get; private set; } = string.Empty;
        public string People { get; private set; } = string.Empty;
        public string Orders { get; private set; } = string.Empty;

        /// <summary>Лучшие люди за игру — тем же правилом, что в событии закрытия гильдии.</summary>
        public List<(int Id, string Name)> Best { get; } = new List<(int, string)>();

        public static DefeatModel Build(ISimulationClient client)
        {
            WorldState world = client.World;
            Treasury treasury = world.Treasury;
            Calendar calendar = client.Calendar;
            long end = treasury.IsClosed ? treasury.ClosedAtHours : world.Time.TotalHours;
            long days = (end - calendar.StartTotalHours) / calendar.HoursPerDay;

            int died = 0;
            int left = 0;
            foreach (Adventurer adventurer in world.Adventurers.Archive)
            {
                if (adventurer.LeaveReason == LeaveReason.Died) died++;
                else left++;
            }

            var model = new DefeatModel
            {
                Days = string.Format(UiStrings.DefeatDaysFormat, days, UiFormat.Date(calendar.At(end))),
                Money = string.Format(UiStrings.DefeatMoneyFormat, UiFormat.Money(treasury.Money)),
                People = string.Format(UiStrings.DefeatPeopleFormat, died, left, world.Adventurers.Active.Count),
                Orders = string.Format(UiStrings.DefeatOrdersFormat, world.Orders.Totals.Done, world.Orders.Totals.Failed),
            };
            foreach (int id in EconomySystem.BestPeople(world.Adventurers, client.Data.Balance.Economy.ClosedBestPeople))
            {
                if (world.Adventurers.TryGetKnown(id, out Adventurer person)) model.Best.Add((id, person.Name));
            }
            return model;
        }

        /// <summary>Новое случайное зерно для новой игры — не такое, как у этой.</summary>
        public static uint NewSeed(uint current)
        {
            uint seed;
            do
            {
                seed = unchecked((uint)Guid.NewGuid().GetHashCode());
            } while (seed == current);
            return seed;
        }
    }
}
