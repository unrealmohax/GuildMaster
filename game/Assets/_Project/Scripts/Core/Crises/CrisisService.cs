using System;
using System.Collections.Generic;
using System.Globalization;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Единственный путь изменить кризисы (<see cref="CrisisBook"/>): расписание на год, объявление, начало, итог и конец.
    /// <list type="bullet">
    /// <item><b>Расписание</b> на год составляется в первом такте года: набег (1-го числа <c>raidMonth</c> утром), налог (в конце каждого
    /// месяца из <c>taxMonths</c> — в 00:00 первого числа следующего), зима (с 1-го числа первого зимнего месяца до 1-го числа
    /// месяца после последнего) и мор (бросок <c>plagueChance</c>, затем месяц — бросок; начало — 1-е число, уже прошедшее — сейчас).</item>
    /// <item><b>Объявление</b> — за <c>warningDays</c> до начала (у мора нет); налог объявляется по доходам квартала на этот момент.</item>
    /// <item><b>Набег</b>: заказ «Отразить набег» (тип с флагом набега, близко, ранг по году) сразу на доске на <c>raidDays</c>,
    /// награда <c>raidReward</c> × номер ранга. Задание выполнено — отбит: Безопасность + <c>raidWonSecurity</c>. Заказ снят по сроку
    /// или задание не выполнено — проигран: Безопасность − <c>raidLostSecurity</c>, нападение без броска, население −
    /// <c>raidLostPopulationShare</c>.</item>
    /// <item><b>Налог</b>: <c>taxBase</c> + <c>taxShareOfIncome</c> × доходы гильдии за три прошедших месяца, обязательный расход; казна ушла
    /// в минус — репутация − <c>taxUnpaidReputation</c> и Безопасность − <c>taxUnpaidSecurity</c>.</item>
    /// <item><b>Мор</b>: каждые 7 суток — население − <c>plaguePopulationPerWeek</c> (с готовым Лазаретом и нанятым Лекарем —
    /// − <c>plagueWithHealerPerWeek</c>).</item>
    /// </list>
    /// Случайность — только у мора (два броска в расписании) и у нападения при проигранном набеге; заказ набега генерируется
    /// потоком вызывающей системы.
    /// </summary>
    public static class CrisisService
    {
        /// <summary>Составить расписание кризисов на год <paramref name="year"/>.</summary>
        internal static void PlanYear(SimContext ctx, int year)
        {
            CrisisBook book = ctx.World.Crises;
            CrisesBalance crises = ctx.Data.Balance.Crises;
            Calendar calendar = ctx.Calendar;
            long warning = calendar.DaysToHours(crises.WarningDays);
            book.PlannedYear = year;

            // Заказ набега приходит утром, как все заказы.
            long raid = MonthStart(calendar, year, crises.RaidMonth) + ctx.Data.Balance.Time.MorningHour;
            Crisis raidCrisis = book.Add(CrisisKind.Raid, year, raid - warning, raid, raid);
            Log(ctx, "plan", raidCrisis);

            foreach (int month in crises.TaxMonths)
            {
                long charge = MonthStart(calendar, year, month + 1);
                Log(ctx, "plan", book.Add(CrisisKind.Tax, year, charge - warning, charge, charge));
            }

            IReadOnlyList<int> winter = crises.WinterMonths;
            if (winter.Count > 0)
            {
                long start = MonthStart(calendar, year, winter[0]);
                long end = MonthStart(calendar, year, winter[winter.Count - 1] + 1);
                Log(ctx, "plan", book.Add(CrisisKind.Winter, year, start - warning, start, end));
            }

            if (!ctx.RollChance(crises.PlagueChance, "plague", null, "year", year)) return;
            int plagueMonth = ctx.Rng.RangeInclusive(1, calendar.MonthsPerYear);
            long plague = Math.Max(MonthStart(calendar, year, plagueMonth), ctx.World.Time.TotalHours);
            Log(ctx, "plan", book.Add(CrisisKind.Plague, year, plague, plague, plague + calendar.DaysToHours(crises.PlagueDays)));
        }

        /// <summary>00:00 первого числа месяца <paramref name="month"/> года <paramref name="year"/>; месяц за концом года — следующий год.</summary>
        internal static long MonthStart(Calendar calendar, int year, int month)
        {
            int index = month - 1;
            int y = year + Math.Max(0, index) / calendar.MonthsPerYear;
            int m = index % calendar.MonthsPerYear + 1;
            return calendar.ToTotalHours(y, m, 1, 0);
        }

        /// <summary>Объявить кризис: [В] с автопаузой.</summary>
        internal static void Warn(SimContext ctx, Crisis crisis)
        {
            crisis.Status = CrisisStatus.Warned;
            long now = ctx.World.Time.TotalHours;
            SimEvent warned = Publish(ctx, SimEventType.CrisisWarning, EventImportance.Important, crisis)
                .With("startsAt", crisis.StartsAtHours)
                .With("count", (int)((crisis.StartsAtHours - now + ctx.Calendar.HoursPerDay - 1) / ctx.Calendar.HoursPerDay));
            if (crisis.Kind == CrisisKind.Tax)
            {
                crisis.Amount = CrisisRules.Tax(CrisisRules.IncomeSince(ctx.World, QuarterStart(ctx, crisis)), ctx.Data.Balance.Crises);
                warned.With("amount", crisis.Amount);
            }
            if (crisis.Kind == CrisisKind.Raid) warned.With("rank", ctx.Data.Balance.Crises.RaidRankFor(crisis.Year));
            Log(ctx, "warn", crisis);
        }

        /// <summary>Кризис наступил: набег — заказ на доске, налог — списание, зима и мор — начались. [В] с автопаузой.</summary>
        internal static void Start(SimContext ctx, Crisis crisis)
        {
            crisis.Status = CrisisStatus.Active;
            switch (crisis.Kind)
            {
                case CrisisKind.Raid:
                    StartRaid(ctx, crisis);
                    break;
                case CrisisKind.Tax:
                    ChargeTax(ctx, crisis);
                    break;
                default:
                    Publish(ctx, SimEventType.CrisisStarted, EventImportance.Important, crisis).With("endsAt", crisis.EndsAtHours);
                    Log(ctx, "start", crisis);
                    break;
            }
        }

        /// <summary>Зима или мор кончились по сроку. [З].</summary>
        internal static void End(SimContext ctx, Crisis crisis)
        {
            crisis.Status = CrisisStatus.Over;
            crisis.Outcome = CrisisOutcome.Passed;
            Publish(ctx, SimEventType.CrisisEnded, EventImportance.Notable, crisis).With("outcome", crisis.Outcome);
            Log(ctx, "end", crisis);
        }

        /// <summary>Итог набега: задание по его заказу вернулось (выполнено — отбит) или заказ снят по сроку (проигран).</summary>
        internal static void RaidOutcome(SimContext ctx, Crisis crisis, bool won)
        {
            if (crisis.IsOver) return;
            CrisesBalance crises = ctx.Data.Balance.Crises;
            crisis.Status = CrisisStatus.Over;
            crisis.Outcome = won ? CrisisOutcome.Won : CrisisOutcome.Lost;
            crisis.EndsAtHours = ctx.World.Time.TotalHours;
            ctx.World.Crises.Totals = ctx.World.Crises.Totals.AddRaid(won);
            Log(ctx, won ? "won" : "lost", crisis);

            if (won)
            {
                SettlementService.ChangeSecurity(ctx, crises.RaidWonSecurity, SettlementReasons.RaidWon, RaidOrder(ctx, crisis));
                Publish(ctx, SimEventType.CrisisEnded, EventImportance.Important, crisis)
                    .With("outcome", crisis.Outcome)
                    .With("order", crisis.OrderId);
                return;
            }

            SettlementService.ChangeSecurity(ctx, -crises.RaidLostSecurity, SettlementReasons.RaidLost, RaidOrder(ctx, crisis));
            SettlementService.Attack(ctx);
            int population = ctx.World.Settlement.Population;
            int fled = (int)Math.Ceiling(population * (double)crises.RaidLostPopulationShare - 1e-4);
            int actual = -SettlementService.ChangePopulation(ctx, -fled, SettlementReasons.RaidFled);
            Publish(ctx, SimEventType.CrisisEnded, EventImportance.Important, crisis)
                .With("outcome", crisis.Outcome)
                .With("order", crisis.OrderId)
                .With("count", actual);
        }

        /// <summary>Сутки мора: в итоги; каждые 7 суток от начала — убыль населения.</summary>
        internal static void PlagueDay(SimContext ctx, Crisis crisis)
        {
            ctx.World.Crises.Totals = ctx.World.Crises.Totals.AddPlagueDay();
            long week = ctx.Calendar.DaysToHours(7);
            int weeks = (int)((ctx.World.Time.TotalHours - crisis.StartsAtHours) / week);
            if (weeks <= crisis.WeeksApplied) return;

            crisis.WeeksApplied = weeks;
            CrisesBalance crises = ctx.Data.Balance.Crises;
            bool healer = BuildingRules.IsReady(ctx.World, ctx.Data, BuildingFunction.Infirmary)
                          && StaffRules.TryGetWithEffect(ctx.World, ctx.Data, StaffLevelEffect.HealingSpeed, out _);
            SettlementService.ChangePopulation(ctx, -(healer ? crises.PlagueWithHealerPerWeek : crises.PlaguePopulationPerWeek), SettlementReasons.Plague);
        }

        private static void StartRaid(SimContext ctx, Crisis crisis)
        {
            CrisesBalance crises = ctx.Data.Balance.Crises;
            QuestTypeDefinition type = RaidType(ctx.Data);
            GuildRank rank = crises.RaidRankFor(crisis.Year);
            if (type == null)
            {
                RaidOutcome(ctx, crisis, won: false);
                return;
            }

            OrderBoard board = ctx.World.Orders;
            var errors = new List<string>();
            Order order = OrderGenerator.Generate(ctx.Rng, ctx.Data, ctx.World.Guild.Reputation, board.NextId(), ctx.World.Time.TotalHours, rank,
                errors, type, OrderDistance.Near);
            foreach (string error in errors) ctx.Log.Write(SimLogLevel.Error, "raid order #{0} description: {1}", order.Id, error);
            order.IsRaid = true;
            order.IsImportant = false;
            order.IsHardEarly = false;
            order.Reward = crises.RaidReward * Vocabulary.Number(rank);
            order.BoardDays = crises.RaidDays;
            OrderSystem.Post(ctx, order);
            board.AddOpen(order);
            board.Totals = board.Totals.AddArrived().AddPosted();
            OrderSystem.WriteGenerated(ctx, "raid", order);

            crisis.OrderId = order.Id;
            crisis.EndsAtHours = order.ExpiresAtHours;
            OrderSystem.Publish(ctx, SimEventType.OrderArrived, order)
                .With("important", false)
                .With("hardEarly", false);
            OrderSystem.Publish(ctx, SimEventType.OrderPosted, order)
                .With("by", "raid")
                .With("expiresAt", order.ExpiresAtHours);
            Publish(ctx, SimEventType.CrisisStarted, EventImportance.Important, crisis)
                .With("order", order.Id)
                .With("rank", rank)
                .With("reward", order.Reward)
                .With("endsAt", order.ExpiresAtHours);
            Log(ctx, "start", crisis);
        }

        private static void ChargeTax(SimContext ctx, Crisis crisis)
        {
            CrisesBalance crises = ctx.Data.Balance.Crises;
            crisis.Amount = CrisisRules.Tax(CrisisRules.IncomeSince(ctx.World, QuarterStart(ctx, crisis)), crises);
            TreasuryService.Debit(ctx, LedgerCategories.Tax, crisis.Amount, crisis.Id.ToString(CultureInfo.InvariantCulture));
            bool unpaid = ctx.World.Treasury.Money < 0;
            crisis.Status = CrisisStatus.Over;
            crisis.Outcome = unpaid ? CrisisOutcome.Unpaid : CrisisOutcome.Paid;
            ctx.World.Crises.Totals = ctx.World.Crises.Totals.AddTax(crisis.Amount, unpaid);
            Log(ctx, "charge", crisis);

            Publish(ctx, SimEventType.CrisisStarted, EventImportance.Important, crisis)
                .With("amount", crisis.Amount)
                .With("unpaid", unpaid)
                .With("money", ctx.World.Treasury.Money);
            if (!unpaid) return;
            ReputationService.Change(ctx, -crises.TaxUnpaidReputation, ReputationReasons.TaxUnpaid);
            SettlementService.ChangeSecurity(ctx, -crises.TaxUnpaidSecurity, SettlementReasons.TaxUnpaid);
        }

        /// <summary>Начало квартала налога: за три месяца до списания (не раньше начала игры).</summary>
        private static long QuarterStart(SimContext ctx, Crisis crisis) =>
            Math.Max(0L, crisis.StartsAtHours - 3 * ctx.Calendar.HoursPerMonth);

        private static QuestTypeDefinition RaidType(DataRegistry data)
        {
            foreach (QuestTypeDefinition type in data.All<QuestTypeDefinition>())
            {
                if (type.IsRaid) return type;
            }
            return null;
        }

        private static Order RaidOrder(SimContext ctx, Crisis crisis) =>
            ctx.World.Orders.TryGetOrder(crisis.OrderId, out Order order) ? order : null;

        private static SimEvent Publish(SimContext ctx, SimEventType type, EventImportance importance, Crisis crisis) =>
            ctx.Events.Publish(type, importance)
                .With("crisis", crisis.Id)
                .With("kind", crisis.Kind)
                .WithSettlement(ctx.World.Settlement);

        private static void Log(SimContext ctx, string what, Crisis crisis) =>
            ctx.Log.Write(SimLogLevel.Debug, "crisis {0} #{1} {2} amount={3}", what, crisis.Id, crisis.Kind, crisis.Amount);
    }
}
