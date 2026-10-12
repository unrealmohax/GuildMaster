using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Единственный путь изменить поселение. Безопасность — <see cref="ChangeSecurity"/> (обрезка 0..<c>maxSecurity</c>, запись
    /// в последние изменения, событие <see cref="SimEventType.SecurityChanged"/> без строки ленты, пересчёт уровня и отсчёта
    /// покидания); население — <see cref="ChangePopulation"/> (не ниже 0, событие <see cref="SimEventType.PopulationChanged"/>).
    /// Здесь же — нападение, рост за готовую постройку, доход таверны от жителей и проверка покидания.
    /// </summary>
    public static class SettlementService
    {
        /// <summary>Старт игры: Безопасность и население из баланса, уровень по Безопасности. Название выбирает сценарий старта.</summary>
        internal static void Initialize(SettlementState settlement, SettlementBalance balance)
        {
            settlement.Initialize(balance.StartSecurity, balance.StartPopulation, SettlementRules.LevelOf(balance.StartSecurity, balance));
        }

        /// <summary>Название поселения — один бросок из списка названий; список пуст — без броска и без названия.</summary>
        internal static void ChooseName(SimContext ctx)
        {
            IReadOnlyList<NounForms> names = ctx.Data.Names?.SettlementNames;
            if (names == null || names.Count == 0) return;
            ctx.World.Settlement.Name = names[ctx.Rng.Range(0, names.Count)];
            ctx.Log.Write(SimLogLevel.Debug, "settlement name={0}", ctx.World.Settlement.Name.Nominative);
        }

        /// <summary>
        /// Изменить Безопасность на <paramref name="delta"/> по причине (<see cref="SettlementReasons"/>), о заказе
        /// <paramref name="order"/> (может быть <c>null</c>). Возвращает фактическое изменение; без изменения — ничего.
        /// </summary>
        public static float ChangeSecurity(SimContext ctx, float delta, string reason, Order order = null) =>
            ChangeSecurity(ctx, delta, reason, order?.Id ?? 0, order?.TypeId, order?.Rank ?? GuildRank.G);

        public static float ChangeSecurity(SimContext ctx, float delta, string reason, int orderId, string questTypeId, GuildRank rank)
        {
            SettlementState settlement = ctx.World.Settlement;
            SettlementBalance balance = ctx.Data.Balance.Settlement;
            float from = settlement.Security;
            float to = Math.Min(balance.MaxSecurity, Math.Max(0f, from + delta));
            if (to == from) return 0f;

            settlement.Security = to;
            long now = ctx.World.Time.TotalHours;
            settlement.AddChange(new SettlementChange(now, to - from, reason, orderId, questTypeId, rank), balance.RecentChanges);

            if (ctx.Log.IsOn(SimLogLevel.Debug))
            {
                StringBuilder line = ctx.Log.Begin(SimLogLevel.Debug);
                line.Append("security ").Append(from.ToString("0.0", CultureInfo.InvariantCulture))
                    .Append(" -> ").Append(to.ToString("0.0", CultureInfo.InvariantCulture)).Append(' ').Append(reason);
                if (orderId != 0) line.Append(" #").Append(orderId.ToString(CultureInfo.InvariantCulture)).Append(' ').Append(questTypeId).Append(' ').Append(rank);
                ctx.Log.Commit();
            }

            SimEvent changed = ctx.Events.Publish(SimEventType.SecurityChanged)
                .With("from", from)
                .With("to", to)
                .With("reason", reason ?? string.Empty);
            if (orderId != 0) changed.With("order", orderId);

            UpdateLevel(ctx);
            UpdateZeroCountdown(ctx, now);
            return to - from;
        }

        /// <summary>Изменить население на <paramref name="delta"/> (не ниже 0). Возвращает фактическое изменение.</summary>
        public static int ChangePopulation(SimContext ctx, int delta, string reason)
        {
            SettlementState settlement = ctx.World.Settlement;
            int from = settlement.Population;
            int to = Math.Max(0, from + delta);
            if (to == from) return 0;

            settlement.Population = to;
            ctx.Log.Write(SimLogLevel.Debug, "population {0} -> {1} {2}", from, to, reason);
            ctx.Events.Publish(SimEventType.PopulationChanged)
                .With("from", from)
                .With("to", to)
                .With("count", Math.Abs(to - from))
                .With("total", to)
                .With("reason", reason ?? string.Empty)
                .WithSettlement(settlement);
            return to - from;
        }

        /// <summary>Постройка гильдии стала готовой: в поселении прибавляются её работники и их семьи.</summary>
        internal static void BuildingReady(SimContext ctx)
        {
            ChangePopulation(ctx, ctx.Data.Balance.Settlement.PopulationPerBuilding, SettlementReasons.Building);
        }

        /// <summary>
        /// Население в начале месяца по уровню: спокойно — растёт, норма — держится, тревога и осада — убывает. Доля округляется
        /// от нуля: ненулевая доля даёт не меньше одного человека.
        /// </summary>
        internal static void MonthlyPopulation(SimContext ctx)
        {
            SettlementState settlement = ctx.World.Settlement;
            int delta = SettlementRules.MonthlyPopulationChange(settlement.Population, settlement.Level, ctx.Data.Balance.Settlement);
            ChangePopulation(ctx, delta, SettlementReasons.Month);
        }

        /// <summary>
        /// Нападение на поселение: гибнут жители (бросок диапазона), каждому человеку гильдии, кто не на задании, — стресс
        /// (с множителями черт), казне — ущерб (бросок диапазона, обязательный расход, может увести в минус). Событие
        /// <see cref="SimEventType.SettlementAttacked"/> [В] — участники — люди, получившие стресс.
        /// </summary>
        internal static void Attack(SimContext ctx)
        {
            SettlementState settlement = ctx.World.Settlement;
            SettlementBalance balance = ctx.Data.Balance.Settlement;
            int killed = ctx.Rng.RangeInclusive(balance.AttackKilled.Min, balance.AttackKilled.Max);
            int damage = ctx.Rng.RangeInclusive(balance.AttackDamage.Min, balance.AttackDamage.Max);

            int actualKilled = -ChangePopulation(ctx, -killed, SettlementReasons.Attack);

            var hit = new List<int>();
            foreach (Adventurer adventurer in ctx.World.Adventurers.Active)
            {
                if (adventurer.State.IsOnQuest()) continue;
                StateService.AddStress(ctx, adventurer, balance.AttackStress);
                hit.Add(adventurer.Id);
            }

            TreasuryService.Debit(ctx, LedgerCategories.AttackDamage, damage, "attack");
            settlement.Totals = settlement.Totals.AddAttack(damage, actualKilled);

            ctx.Events.Publish(SimEventType.SettlementAttacked, EventImportance.Important, hit.ToArray())
                .With("killed", actualKilled)
                .With("count", actualKilled)
                .With("amount", damage)
                .WithSettlement(settlement);
        }

        /// <summary>
        /// Доход таверны от жителей за прошедшие сутки: население × <c>localsTavernIncome</c> × множитель уровня × Сухой закон.
        /// Зачисляются целые монеты, дробный остаток переносится на следующие сутки.
        /// </summary>
        internal static void SettleLocalsTavern(SimContext ctx)
        {
            SettlementState settlement = ctx.World.Settlement;
            double income = SettlementRules.LocalsTavernIncome(settlement, ctx.Data.Balance.Settlement)
                * DecreeRules.TavernIncomeMultiplier(ctx.World, ctx.Data);
            double total = settlement.TavernRemainder + income;
            int coins = (int)Math.Floor(total + 1e-9);
            TreasuryService.Credit(ctx, LedgerCategories.TavernLocals, coins,
                string.Format(CultureInfo.InvariantCulture, "population={0}", settlement.Population));
            settlement.TavernRemainder = Math.Max(0, total - coins);
        }

        /// <summary>
        /// Поселение покинуто: население 0 или Безопасность на нуле <c>abandonDays</c> суток подряд. Тогда гильдия закрывается.
        /// Возвращает, закрыта ли.
        /// </summary>
        internal static bool CheckAbandoned(SimContext ctx)
        {
            SettlementState settlement = ctx.World.Settlement;
            long now = ctx.World.Time.TotalHours;
            bool empty = settlement.Population <= 0;
            bool zeroTooLong = settlement.ZeroSecuritySinceHours.HasValue
                && now - settlement.ZeroSecuritySinceHours.Value >= ctx.Calendar.DaysToHours(ctx.Data.Balance.Settlement.AbandonDays);
            if (!empty && !zeroTooLong) return false;

            ctx.Log.Write(SimLogLevel.Info, "settlement abandoned: population={0} zeroSince={1}", settlement.Population,
                settlement.ZeroSecuritySinceHours ?? -1);
            GuildClosure.Close(ctx, ClosedCause.SettlementAbandoned);
            return true;
        }

        /// <summary>Сменился уровень — событие: вниз в тревогу или осаду — [В] с автопаузой, иначе — [З].</summary>
        private static void UpdateLevel(SimContext ctx)
        {
            SettlementState settlement = ctx.World.Settlement;
            SettlementLevel from = settlement.Level;
            SettlementLevel to = SettlementRules.LevelOf(settlement.Security, ctx.Data.Balance.Settlement);
            if (to == from) return;

            settlement.Level = to;
            bool down = to > from;
            bool danger = down && to >= SettlementLevel.Alarm;
            ctx.Events.Publish(danger ? SimEventType.SettlementLevelDropped : SimEventType.SettlementLevelChanged,
                    danger ? EventImportance.Important : EventImportance.Notable)
                .With("from", from)
                .With("to", to)
                .With("down", down)
                .WithSettlement(settlement);
        }

        /// <summary>Безопасность дошла до нуля — начат отсчёт покидания; поднялась выше нуля — отсчёт снят.</summary>
        private static void UpdateZeroCountdown(SimContext ctx, long now)
        {
            SettlementState settlement = ctx.World.Settlement;
            int days = ctx.Data.Balance.Settlement.AbandonDays;
            if (settlement.Security <= 0f && !settlement.ZeroSecuritySinceHours.HasValue)
            {
                settlement.ZeroSecuritySinceHours = now;
                ctx.Events.Publish(SimEventType.SettlementAbandoning, EventImportance.Important)
                    .With("count", days)
                    .With("endsAt", now + ctx.Calendar.DaysToHours(days))
                    .WithSettlement(settlement);
            }
            else if (settlement.Security > 0f && settlement.ZeroSecuritySinceHours.HasValue)
            {
                settlement.ZeroSecuritySinceHours = null;
                ctx.Events.Publish(SimEventType.SettlementAbandoningLifted, EventImportance.Notable)
                    .WithSettlement(settlement);
            }
        }
    }

    internal static class SettlementEventExtensions
    {
        /// <summary>Название поселения в данные события (<c>settlement</c>) — для строк ленты; без названия — ничего.</summary>
        internal static SimEvent WithSettlement(this SimEvent simEvent, SettlementState settlement) =>
            settlement.Name != null ? simEvent.With("settlement", settlement.Name) : simEvent;
    }
}
