using System.Collections.Generic;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Чистые правила распоряжений: что включено и как это меняет числа других систем. Распоряжение находится по правилу
    /// <see cref="DecreeEffect"/>, а не по id. Выключенное распоряжение возвращает нейтральное значение (× 1, + 0), поэтому
    /// вызывающие системы считают так же, как без распоряжений.
    /// </summary>
    public static class DecreeRules
    {
        /// <summary>Определение с этим правилом; нет — <c>null</c>.</summary>
        public static DecreeDefinition Find(DataRegistry data, DecreeEffect effect)
        {
            if (!data.HasDefinitions) return null;
            foreach (DecreeDefinition decree in data.All<DecreeDefinition>())
            {
                if (decree.Effect == effect) return decree;
            }
            return null;
        }

        /// <summary>Распоряжение с этим правилом включено.</summary>
        public static bool IsOn(WorldState world, DataRegistry data, DecreeEffect effect) => TryGetOn(world, data, effect, out _);

        public static bool TryGetOn(WorldState world, DataRegistry data, DecreeEffect effect, out ActiveDecree active)
        {
            active = null;
            if (world.Decrees.Active.Count == 0) return false;
            DecreeDefinition decree = Find(data, effect);
            return decree != null && world.Decrees.TryGetActive(decree.Id, out active);
        }

        /// <summary>Срок в днях: недели и месяца — из баланса, бессрочно — 0.</summary>
        public static int Days(DecreeDuration duration, DecreesBalance balance)
        {
            switch (duration)
            {
                case DecreeDuration.Week: return balance.WeekDurationDays;
                case DecreeDuration.Month: return balance.MonthDurationDays;
                default: return 0;
            }
        }

        /// <summary>Ранги области без повторов, от G к C. У распоряжений без области — пусто.</summary>
        public static List<GuildRank> NormalizeRanks(DecreeDefinition decree, IEnumerable<GuildRank> ranks)
        {
            var result = new List<GuildRank>();
            if (decree.ScopeKind != DecreeScopeKind.Ranks || ranks == null) return result;
            foreach (GuildRank rank in ranks)
            {
                if (!result.Contains(rank)) result.Add(rank);
            }
            result.Sort();
            return result;
        }

        // ---------- Еда и жильё для новичков ----------

        /// <summary>Новичок: в гильдии меньше <c>newcomerPeriodDays</c> дней.</summary>
        public static bool IsNewcomer(Adventurer adventurer, WorldState world, DataRegistry data) =>
            world.Time.TotalHours - adventurer.JoinedAtHours < (long)data.Balance.Decrees.NewcomerPeriodDays * data.Balance.Time.HoursPerDay;

        /// <summary>Еду и жильё этого человека сегодня платит гильдия.</summary>
        public static bool GuildPaysLiving(Adventurer adventurer, WorldState world, DataRegistry data) =>
            IsOn(world, data, DecreeEffect.FreeLodgingForNewcomers) && IsNewcomer(adventurer, world, data);

        /// <summary>Множитель шанса кандидата: × <c>newcomerInfluxMultiplier</c>, пока гильдия кормит новичков.</summary>
        public static float CandidateChanceMultiplier(WorldState world, DataRegistry data) =>
            IsOn(world, data, DecreeEffect.FreeLodgingForNewcomers) ? data.Balance.Decrees.NewcomerInfluxMultiplier : 1f;

        /// <summary>Сдвиг оси Труд у нового кандидата («халявщики»), пока гильдия кормит новичков.</summary>
        public static float CandidateWorkShift(WorldState world, DataRegistry data) =>
            IsOn(world, data, DecreeEffect.FreeLodgingForNewcomers) ? data.Balance.Decrees.FreeloaderWorkShift : 0f;

        // ---------- Только группой ----------

        /// <summary>Заказ этого ранга нельзя взять одному. Экзамен на ранг не затрагивается.</summary>
        public static bool IsSoloBanned(WorldState world, DataRegistry data, Order order) =>
            !order.IsPromotion && TryGetOn(world, data, DecreeEffect.GroupOnlyFromRank, out ActiveDecree active) && active.HasRank(order.Rank);

        // ---------- Компенсация за ранение ----------

        /// <summary>Сколько гильдия платит за рану; увечье — вместо платы за тяжёлую.</summary>
        public static int Compensation(ConditionKind kind, bool maimed, DecreesBalance balance)
        {
            if (maimed) return balance.CompensationMaimed;
            return kind == ConditionKind.LightWound ? balance.CompensationLightWound : balance.CompensationHeavyWound;
        }

        /// <summary>Множитель мотива «Безопасность» в вариантах с заказом.</summary>
        public static float OrderSafetyMultiplier(WorldState world, DataRegistry data) =>
            IsOn(world, data, DecreeEffect.InjuryCompensation) ? data.Balance.Decrees.CompensationSafetyMultiplier : 1f;

        // ---------- Сухой закон ----------

        /// <summary>Множитель дохода таверны.</summary>
        public static float TavernIncomeMultiplier(WorldState world, DataRegistry data) =>
            IsOn(world, data, DecreeEffect.Prohibition) ? data.Balance.Decrees.ProhibitionTavernIncomeMultiplier : 1f;

        /// <summary>Множитель снятия стресса в таверне: при Сухом законе × <c>prohibitionStressReliefMultiplier</c>, у Пьяницы — 0.</summary>
        public static float TavernStressReliefMultiplier(Adventurer adventurer, WorldState world, DataRegistry data)
        {
            if (!IsOn(world, data, DecreeEffect.Prohibition)) return 1f;
            return IsDrunkard(adventurer, data) ? 0f : data.Balance.Decrees.ProhibitionStressReliefMultiplier;
        }

        /// <summary>Шанс Пьяницы пропустить день: при Сухом законе — <c>prohibitionDrunkardSkipChance</c>, иначе <paramref name="usual"/>.</summary>
        public static float DrunkardSkipChance(WorldState world, DataRegistry data, float usual) =>
            IsOn(world, data, DecreeEffect.Prohibition) ? data.Balance.Decrees.ProhibitionDrunkardSkipChance : usual;

        // ---------- Довольство и лояльность ----------

        /// <summary>
        /// Вклад распоряжений в цель довольства: одиночки (ось Люди не выше <c>groupOnlyLonerAxis</c>) — «Только группой»,
        /// Пьяница — Сухой закон. Скрытые черты и оси действуют так же, как раскрытые.
        /// </summary>
        public static float ContentmentTerm(Adventurer adventurer, WorldState world, DataRegistry data)
        {
            if (world == null || world.Decrees.Active.Count == 0) return 0f;
            DecreesBalance balance = data.Balance.Decrees;
            float term = 0f;
            if (IsOn(world, data, DecreeEffect.GroupOnlyFromRank) && adventurer.GetAxis(AxisId.People) <= balance.GroupOnlyLonerAxis)
                term += balance.GroupOnlyLonerContentment;
            if (IsOn(world, data, DecreeEffect.Prohibition) && IsDrunkard(adventurer, data))
                term += balance.ProhibitionDrunkardContentment;
            return term;
        }

        /// <summary>Сдвиг лояльности в сутки от распоряжений.</summary>
        public static float DailyLoyalty(WorldState world, DataRegistry data) =>
            IsOn(world, data, DecreeEffect.InjuryCompensation) ? data.Balance.Decrees.CompensationLoyaltyPerDay : 0f;

        /// <summary>Кого касалась льгота — их задевает её отмена: кормёжка — текущих новичков, остальные — всех.</summary>
        public static bool IsAffectedByBenefit(DecreeDefinition decree, Adventurer adventurer, WorldState world, DataRegistry data) =>
            decree.Effect != DecreeEffect.FreeLodgingForNewcomers || IsNewcomer(adventurer, world, data);

        private static bool IsDrunkard(Adventurer adventurer, DataRegistry data) =>
            TraitRules.FindWithHook(adventurer, TraitHook.DrunkardSkipsDay, data) != null;
    }
}
