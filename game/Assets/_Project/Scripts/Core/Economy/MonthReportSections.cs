using System;
using System.Collections.Generic;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Период, за который собирается отчёт: от прошлого отчёта (у первого — от начала игры) до текущего часа
    /// включительно. Всё, что попало в прошлый отчёт, в этот не попадает (<see cref="WasReported"/>): так в отчёт
    /// месяца идёт и то, что случилось в час отчёта, до него. Записи журнала — по индексам.
    /// </summary>
    public sealed class ReportPeriod
    {
        internal ReportPeriod(WorldState world, DataRegistry data, Calendar calendar, MonthReport previous)
        {
            World = world;
            Data = data;
            Calendar = calendar;
            Previous = previous;
            FromHours = previous?.ToHours ?? calendar.StartTotalHours;
            ToHours = world.Time.TotalHours;
            LedgerFrom = previous?.LedgerTo ?? 0;
            LedgerTo = world.Treasury.Ledger.Count;
            MoneyAtStart = previous?.MoneyAtEnd ?? world.Treasury.StartMoney;
            MoneyAtEnd = world.Treasury.Money;
            ReputationAtStart = previous?.ReputationAtEnd ?? world.Guild.StartReputation;
            ReputationAtEnd = world.Guild.Reputation;
            OrdersAtStart = previous?.OrdersAtEnd ?? default;
            OrdersAtEnd = world.Orders.Totals;
        }

        public WorldState World { get; }
        public DataRegistry Data { get; }
        public Calendar Calendar { get; }

        /// <summary>Прошлый отчёт; <c>null</c> — это первый.</summary>
        public MonthReport Previous { get; }

        public long FromHours { get; }
        public long ToHours { get; }
        public int LedgerFrom { get; }
        public int LedgerTo { get; }
        public int MoneyAtStart { get; }
        public int MoneyAtEnd { get; }
        public float ReputationAtStart { get; }
        public float ReputationAtEnd { get; }

        /// <summary>Счётчики заказов на начало периода (у первого отчёта — нули) и сейчас; за период — их разница.</summary>
        public OrderTotals OrdersAtStart { get; }

        public OrderTotals OrdersAtEnd { get; }

        /// <summary>
        /// Час попадает в период. Час начала первого периода — начало игры: стартовый состав — не новость месяца.
        /// </summary>
        public bool Contains(long hours) => hours <= ToHours && (hours > FromHours || (hours == FromHours && Previous != null));

        /// <summary>Строка с этим человеком и подробностью уже была в прошлом отчёте.</summary>
        public bool WasReported(string lineKey, int personId, string detail) => Previous != null && Previous.HasItem(lineKey, personId, detail);
    }

    /// <summary>Раздел отчёта: ключ заголовка, ключи всех подписей строк (их тексты проверяет валидатор) и сборка строк.</summary>
    public sealed class MonthReportSection
    {
        public MonthReportSection(string titleKey, IReadOnlyList<string> lineKeys, Func<ReportPeriod, List<ReportLine>> build)
        {
            TitleKey = titleKey;
            LineKeys = lineKeys;
            Build = build;
        }

        public string TitleKey { get; }
        public IReadOnlyList<string> LineKeys { get; }
        public Func<ReportPeriod, List<ReportLine>> Build { get; }
    }

    /// <summary>
    /// Разделы отчёта месяца по порядку. Новый раздел — строка в <see cref="Default"/>; новая строка раздела — в его сборке
    /// и ключ подписи в его списке. Названия статей журнала — ключи <see cref="LedgerCategory.TextKey"/>.
    /// <list type="bullet">
    /// <item>«Деньги»: казна на начало, доходы всего и по статьям, расходы всего и по статьям, казна на конец — из журнала.</item>
    /// <item>«Люди»: кто пришёл, кто ушёл (погиб, пропал, изгнан), кто сбежал с задания, что раскрылось — из людей в гильдии
    /// и в архиве.</item>
    /// <item>«Заказы»: сколько пришло, повешено на доску, отклонено Регистратором и игроком, снято по сроку — из счётчиков.</item>
    /// <item>«Задания»: сколько заказов взято, выполнено, не выполнено — из счётчиков (уровни результата игроку не видны).</item>
    /// <item>«Постройки»: что построено за месяц, что строится, что в очереди.</item>
    /// <item>«Персонал»: кто нанят, кто ушёл сам и кто уволен за месяц, кому гильдия должна жалованье.</item>
    /// <item>«Распоряжения»: какие действовали за месяц (сколько дней и сколько стоили) и расходы на них всего.</item>
    /// <item>«Репутация»: было → стало.</item>
    /// </list>
    /// </summary>
    public static class MonthReportSections
    {
        public const string MoneyTitle = "report.money";
        public const string MoneyStart = "report.money.start";
        public const string MoneyIncome = "report.money.income";
        public const string MoneyExpense = "report.money.expense";
        public const string MoneyEnd = "report.money.end";

        public const string PeopleTitle = "report.people";
        public const string PeopleJoined = "report.people.joined";
        public const string PeopleLeft = "report.people.left";
        public const string PeopleDied = "report.people.died";
        public const string PeopleDisappeared = "report.people.disappeared";
        public const string PeopleExpelled = "report.people.expelled";
        public const string PeopleRevealed = "report.people.revealed";
        public const string PeopleFled = "report.people.fled";

        public const string OrdersTitle = "report.orders";
        public const string OrdersArrived = "report.orders.arrived";
        public const string OrdersPosted = "report.orders.posted";
        public const string OrdersDeclinedByRegistrar = "report.orders.declinedByRegistrar";
        public const string OrdersDeclinedByPlayer = "report.orders.declinedByPlayer";
        public const string OrdersExpired = "report.orders.expired";

        public const string QuestsTitle = "report.quests";
        public const string QuestsTaken = "report.quests.taken";
        public const string QuestsDone = "report.quests.done";
        public const string QuestsFailed = "report.quests.failed";

        public const string BuildingsTitle = "report.buildings";
        public const string BuildingsReady = "report.buildings.ready";
        public const string BuildingsUnderConstruction = "report.buildings.underConstruction";
        public const string BuildingsQueued = "report.buildings.queued";

        public const string StaffTitle = "report.staff";
        public const string StaffHired = "report.staff.hired";
        public const string StaffQuit = "report.staff.quit";
        public const string StaffDismissed = "report.staff.dismissed";
        public const string StaffUnpaid = "report.staff.unpaid";

        public const string DecreesTitle = "report.decrees";
        public const string DecreesActed = "report.decrees.acted";
        public const string DecreesCost = "report.decrees.cost";

        /// <summary>Распоряжение в строке «Действовали»: метки <c>{распоряжение}</c> и <c>{число}</c> (дни).</summary>
        public const string DecreeItem = "report.decrees.item";

        /// <summary>Подробность строки-постройки: id в строке — id постройки.</summary>
        public const string BuildingDetail = "building";

        /// <summary>Подробность строки-сотрудника: id в строке — id сотрудника.</summary>
        public const string StaffDetail = "staff";

        public const string ReputationTitle = "report.reputation";
        public const string ReputationChange = "report.reputation.change";

        /// <summary>Слово для раскрытой нейтральной оси: «Риск: уравновешенность».</summary>
        public const string AxisBalanced = "report.axis.balanced";

        public static IReadOnlyList<MonthReportSection> Default { get; } = new[]
        {
            new MonthReportSection(MoneyTitle, new[] { MoneyStart, MoneyIncome, MoneyExpense, MoneyEnd }, BuildMoney),
            new MonthReportSection(PeopleTitle,
                new[] { PeopleJoined, PeopleLeft, PeopleDied, PeopleDisappeared, PeopleExpelled, PeopleFled, PeopleRevealed, AxisBalanced }, BuildPeople),
            new MonthReportSection(OrdersTitle,
                new[] { OrdersArrived, OrdersPosted, OrdersDeclinedByRegistrar, OrdersDeclinedByPlayer, OrdersExpired }, BuildOrders),
            new MonthReportSection(QuestsTitle, new[] { QuestsTaken, QuestsDone, QuestsFailed }, BuildQuests),
            new MonthReportSection(BuildingsTitle, new[] { BuildingsReady, BuildingsUnderConstruction, BuildingsQueued }, BuildBuildings),
            new MonthReportSection(StaffTitle, new[] { StaffHired, StaffQuit, StaffDismissed, StaffUnpaid }, BuildStaff),
            new MonthReportSection(DecreesTitle, new[] { DecreesActed, DecreesCost, DecreeItem }, BuildDecrees),
            new MonthReportSection(ReputationTitle, new[] { ReputationChange }, BuildReputation),
        };

        /// <summary>Все ключи текстов отчёта: заголовки, подписи строк, названия статей журнала.</summary>
        public static IReadOnlyList<string> TextKeys { get; } = BuildTextKeys();

        /// <summary>Подробность распоряжения: <c>decree:{id}</c>.</summary>
        public static string DecreeDetail(string decreeId) => "decree:" + decreeId;

        /// <summary>Подробность раскрытой черты: <c>trait:{id}</c>.</summary>
        public static string TraitDetail(string traitId) => "trait:" + traitId;

        /// <summary>Подробность раскрытой оси: <c>axis:{ось}:{Negative|Positive|Balanced}</c>.</summary>
        public static string AxisDetail(AxisId axis, AxisPole? pole) => "axis:" + axis + ":" + (pole.HasValue ? pole.Value.ToString() : "Balanced");

        /// <summary>Доходы и расходы по статьям за период: сумма со знаком по каждой статье в порядке появления.</summary>
        public static List<KeyValuePair<LedgerCategory, int>> Totals(IReadOnlyList<LedgerEntry> ledger, int from, int to)
        {
            var totals = new List<KeyValuePair<LedgerCategory, int>>();
            for (int i = from; i < to; i++)
            {
                LedgerEntry entry = ledger[i];
                int index = totals.FindIndex(pair => pair.Key == entry.Category);
                if (index < 0) totals.Add(new KeyValuePair<LedgerCategory, int>(entry.Category, entry.Amount));
                else totals[index] = new KeyValuePair<LedgerCategory, int>(entry.Category, totals[index].Value + entry.Amount);
            }
            return totals;
        }

        private static List<ReportLine> BuildMoney(ReportPeriod period)
        {
            List<KeyValuePair<LedgerCategory, int>> totals = Totals(period.World.Treasury.Ledger, period.LedgerFrom, period.LedgerTo);
            int income = 0;
            int expense = 0;
            foreach (KeyValuePair<LedgerCategory, int> pair in totals)
            {
                if (pair.Key.Flow == LedgerFlow.Income) income += pair.Value;
                else expense += pair.Value;
            }

            var lines = new List<ReportLine> { ReportLine.Money(MoneyStart, period.MoneyAtStart, signed: false) };
            lines.Add(ReportLine.Money(MoneyIncome, income, signed: true));
            AddCategories(lines, totals, LedgerFlow.Income);
            lines.Add(ReportLine.Money(MoneyExpense, expense, signed: true));
            AddCategories(lines, totals, LedgerFlow.Expense);
            lines.Add(ReportLine.Money(MoneyEnd, period.MoneyAtEnd, signed: false));
            return lines;
        }

        private static void AddCategories(List<ReportLine> lines, List<KeyValuePair<LedgerCategory, int>> totals, LedgerFlow flow)
        {
            foreach (KeyValuePair<LedgerCategory, int> pair in totals)
            {
                if (pair.Key.Flow == flow) lines.Add(ReportLine.Money(pair.Key.TextKey, pair.Value, signed: true));
            }
        }

        private static List<ReportLine> BuildPeople(ReportPeriod period)
        {
            var everyone = new List<Adventurer>(period.World.Adventurers.Active);
            everyone.AddRange(period.World.Adventurers.Archive);

            var lines = new List<ReportLine>();
            AddPeople(lines, PeopleJoined, Pick(period, PeopleJoined, everyone, a => period.Contains(a.JoinedAtHours), a => a.JoinedAtHours));
            AddLeft(lines, period, PeopleLeft, LeaveReason.Left);
            AddLeft(lines, period, PeopleDied, LeaveReason.Died);
            AddLeft(lines, period, PeopleDisappeared, LeaveReason.Disappeared);
            AddLeft(lines, period, PeopleExpelled, LeaveReason.Expelled);
            AddPeople(lines, PeopleFled, Pick(period, PeopleFled, everyone, a => a.LastFledAtHours > 0 && period.Contains(a.LastFledAtHours), a => a.LastFledAtHours));
            AddPeople(lines, PeopleRevealed, Revealed(period, everyone));
            return lines;
        }

        private static List<ReportLine> BuildOrders(ReportPeriod period)
        {
            OrderTotals from = period.OrdersAtStart;
            OrderTotals to = period.OrdersAtEnd;
            return new List<ReportLine>
            {
                ReportLine.Number(OrdersArrived, to.Arrived - from.Arrived),
                ReportLine.Number(OrdersPosted, to.Posted - from.Posted),
                ReportLine.Number(OrdersDeclinedByRegistrar, to.DeclinedByRegistrar - from.DeclinedByRegistrar),
                ReportLine.Number(OrdersDeclinedByPlayer, to.DeclinedByPlayer - from.DeclinedByPlayer),
                ReportLine.Number(OrdersExpired, to.Expired - from.Expired),
            };
        }

        private static List<ReportLine> BuildQuests(ReportPeriod period)
        {
            OrderTotals from = period.OrdersAtStart;
            OrderTotals to = period.OrdersAtEnd;
            return new List<ReportLine>
            {
                ReportLine.Number(QuestsTaken, to.Taken - from.Taken),
                ReportLine.Number(QuestsDone, to.Done - from.Done),
                ReportLine.Number(QuestsFailed, to.Failed - from.Failed),
            };
        }

        /// <summary>Постройки: готовые за период (кроме стартовых), строится сейчас, в очереди — по порядку очереди.</summary>
        private static List<ReportLine> BuildBuildings(ReportPeriod period)
        {
            BuildingBook book = period.World.Buildings;
            var ready = new List<ReportItem>();
            foreach (Building building in book.All)
            {
                if (building.IsReady && building.PaidCost > 0 && period.Contains(building.ConstructionEndsAtHours)
                    && !period.WasReported(BuildingsReady, building.Id, BuildingDetail))
                    ready.Add(new ReportItem(building.Id, BuildingDetail));
            }

            var lines = new List<ReportLine>();
            AddPeople(lines, BuildingsReady, ready);
            if (book.Current != null)
                lines.Add(ReportLine.People(BuildingsUnderConstruction, new[] { new ReportItem(book.Current.Id, BuildingDetail) }));
            var queued = new List<ReportItem>();
            foreach (Building building in book.Queue) queued.Add(new ReportItem(building.Id, BuildingDetail));
            AddPeople(lines, BuildingsQueued, queued);
            return lines;
        }

        /// <summary>Персонал: нанятые за период, ушедшие сами и уволенные, у кого долг по зарплате сейчас.</summary>
        private static List<ReportLine> BuildStaff(ReportPeriod period)
        {
            StaffRoster staff = period.World.Staff;
            var everyone = new List<StaffMember>(staff.Members);
            everyone.AddRange(staff.Former);

            var hired = new List<ReportItem>();
            var quit = new List<ReportItem>();
            var dismissed = new List<ReportItem>();
            var unpaid = new List<ReportItem>();
            foreach (StaffMember member in everyone)
            {
                if (period.Contains(member.HiredAtHours) && !period.WasReported(StaffHired, member.Id, StaffDetail))
                    hired.Add(new ReportItem(member.Id, StaffDetail));
                if (member.LeaveReason == StaffLeaveReason.Quit && period.Contains(member.LeftAtHours)) quit.Add(new ReportItem(member.Id, StaffDetail));
                if (member.LeaveReason == StaffLeaveReason.Dismissed && period.Contains(member.LeftAtHours)) dismissed.Add(new ReportItem(member.Id, StaffDetail));
                if (member.LeaveReason == StaffLeaveReason.None && member.UnpaidSalary > 0) unpaid.Add(new ReportItem(member.Id, StaffDetail));
            }

            var lines = new List<ReportLine>();
            AddPeople(lines, StaffHired, hired);
            AddPeople(lines, StaffQuit, quit);
            AddPeople(lines, StaffDismissed, dismissed);
            AddPeople(lines, StaffUnpaid, unpaid);
            return lines;
        }

        private static List<ReportLine> BuildReputation(ReportPeriod period) =>
            new List<ReportLine> { ReportLine.Change(ReputationChange, period.ReputationAtStart, period.ReputationAtEnd) };

        private static void AddLeft(List<ReportLine> lines, ReportPeriod period, string key, LeaveReason reason) =>
            AddPeople(lines, key, Pick(period, key, period.World.Adventurers.Archive,
                a => a.LeaveReason == reason && period.Contains(a.LeftAtHours), a => a.LeftAtHours));

        /// <summary>
        /// Распоряжения, действовавшие за период: дни (неполный день — как день) и расходы по статье «Распоряжения» с его id
        /// в комментарии; затем расходы на распоряжения всего. Ничего не действовало и не стоило — раздел пуст.
        /// </summary>
        private static List<ReportLine> BuildDecrees(ReportPeriod period)
        {
            var lines = new List<ReportLine>();
            if (!period.Data.HasDefinitions) return lines;

            IReadOnlyList<LedgerEntry> ledger = period.World.Treasury.Ledger;
            int total = 0;
            var costs = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = period.LedgerFrom; i < period.LedgerTo; i++)
            {
                LedgerEntry entry = ledger[i];
                if (entry.Category != LedgerCategories.Decrees) continue;
                total += entry.Amount;
                string id = entry.Comment ?? string.Empty;
                costs[id] = (costs.TryGetValue(id, out int sum) ? sum : 0) + entry.Amount;
            }

            int hoursPerDay = period.Calendar.HoursPerDay;
            var items = new List<ReportItem>();
            foreach (DecreeDefinition decree in period.Data.All<DecreeDefinition>())
            {
                long hours = period.World.Decrees.GetActiveHours(decree.Id, period.FromHours, period.ToHours);
                costs.TryGetValue(decree.Id, out int cost);
                if (hours <= 0 && cost == 0) continue;
                int days = (int)((hours + hoursPerDay - 1) / hoursPerDay);
                items.Add(new ReportItem(0, DecreeDetail(decree.Id), days, cost));
            }

            AddPeople(lines, DecreesActed, items);
            if (items.Count > 0 || total != 0) lines.Add(ReportLine.Money(DecreesCost, total, signed: true));
            return lines;
        }

        private static void AddPeople(List<ReportLine> lines, string key, List<ReportItem> items)
        {
            if (items.Count > 0) lines.Add(ReportLine.People(key, items));
        }

        private static List<ReportItem> Pick(ReportPeriod period, string key, IReadOnlyList<Adventurer> people, Func<Adventurer, bool> fits,
            Func<Adventurer, long> time)
        {
            var picked = new List<Adventurer>();
            foreach (Adventurer adventurer in people)
            {
                if (fits(adventurer) && !period.WasReported(key, adventurer.Id, string.Empty)) picked.Add(adventurer);
            }
            picked.Sort((a, b) => time(a) != time(b) ? time(a).CompareTo(time(b)) : a.Id.CompareTo(b.Id));

            var items = new List<ReportItem>(picked.Count);
            foreach (Adventurer adventurer in picked) items.Add(new ReportItem(adventurer.Id));
            return items;
        }

        /// <summary>Раскрытые за период черты и оси, по времени раскрытия.</summary>
        private static List<ReportItem> Revealed(ReportPeriod period, List<Adventurer> everyone)
        {
            var found = new List<KeyValuePair<long, ReportItem>>();
            AdventurersBalance balance = period.Data.Balance.Adventurers;
            foreach (Adventurer adventurer in everyone)
            {
                foreach (TraitInstance trait in adventurer.Traits)
                {
                    if (trait.Revealed && period.Contains(trait.RevealedAtHours))
                        AddRevealed(found, period, adventurer, TraitDetail(trait.TraitId), trait.RevealedAtHours);
                }
                for (int i = 0; i < Vocabulary.AxisCount; i++)
                {
                    var axis = (AxisId)i;
                    long at = adventurer.GetAxisRevealedAtHours(axis);
                    if (!adventurer.IsAxisRevealed(axis) || !period.Contains(at)) continue;

                    float value = adventurer.GetAxis(axis);
                    AxisPole? pole = AxisMath.IsNeutral(value, balance) ? (AxisPole?)null : AxisMath.PoleOf(value);
                    AddRevealed(found, period, adventurer, AxisDetail(axis, pole), at);
                }
            }

            // Сортировка устойчивая: при равном времени — порядок обхода (люди по порядку, черты, затем оси).
            var ordered = new List<ReportItem>(found.Count);
            var indices = new List<int>(found.Count);
            for (int i = 0; i < found.Count; i++) indices.Add(i);
            indices.Sort((a, b) => found[a].Key != found[b].Key ? found[a].Key.CompareTo(found[b].Key) : a.CompareTo(b));
            foreach (int index in indices) ordered.Add(found[index].Value);
            return ordered;
        }

        private static void AddRevealed(List<KeyValuePair<long, ReportItem>> found, ReportPeriod period, Adventurer adventurer, string detail, long at)
        {
            if (period.WasReported(PeopleRevealed, adventurer.Id, detail)) return;
            found.Add(new KeyValuePair<long, ReportItem>(at, new ReportItem(adventurer.Id, detail)));
        }

        private static IReadOnlyList<string> BuildTextKeys()
        {
            var keys = new List<string>();
            foreach (MonthReportSection section in Default)
            {
                keys.Add(section.TitleKey);
                keys.AddRange(section.LineKeys);
            }
            foreach (LedgerCategory category in LedgerCategories.All) keys.Add(category.TextKey);
            return keys;
        }
    }
}
