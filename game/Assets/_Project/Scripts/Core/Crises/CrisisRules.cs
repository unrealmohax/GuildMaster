using System;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>Время года по месяцу: год начинается весной, каждое — четверть года.</summary>
    public enum Season
    {
        Spring,
        Summer,
        Autumn,
        Winter,
    }

    /// <summary>
    /// Чистые правила кризисов и проектов поселения для других систем: идёт ли зима или мор, суточный спад Безопасности,
    /// поток заказов, доход таверны от жителей, налог, время года. Мир не меняют, случайных чисел не тратят. Зима и мор действуют,
    /// только пока их кризис идёт (<see cref="CrisisBook"/>): без системы кризисов их нет.
    /// </summary>
    public static class CrisisRules
    {
        /// <summary>Идёт зима.</summary>
        public static bool IsWinter(WorldState world) => world.Crises.IsActive(CrisisKind.Winter);

        /// <summary>Была ли зима в час <paramref name="totalHours"/> (для расчётов за прошедшие сутки).</summary>
        public static bool WasWinterAt(WorldState world, long totalHours)
        {
            foreach (Crisis crisis in world.Crises.All)
            {
                if (crisis.Kind == CrisisKind.Winter && crisis.Status >= CrisisStatus.Active && crisis.StartsAtHours <= totalHours
                    && totalHours < crisis.EndsAtHours)
                    return true;
            }
            return false;
        }

        /// <summary>Идёт мор.</summary>
        public static bool IsPlague(WorldState world) => world.Crises.IsActive(CrisisKind.Plague);

        /// <summary>
        /// Суточный спад Безопасности: <c>dailyDecay</c>, зимой × <c>winterDecayMultiplier</c>, с готовой Дозорной вышкой −
        /// <c>watchtowerDecayReduction</c>; не ниже 0.
        /// </summary>
        public static float DailyDecay(WorldState world, DataRegistry data)
        {
            CrisesBalance crises = data.Balance.Crises;
            float decay = data.Balance.Settlement.DailyDecay;
            if (IsWinter(world)) decay *= crises.WinterDecayMultiplier;
            if (BuildingRules.IsReady(world, data, BuildingFunction.Watchtower)) decay -= crises.WatchtowerDecayReduction;
            return Math.Max(0f, decay);
        }

        /// <summary>Заказов в день: обычный поток + <c>roadOrdersPerDay</c> с готовой Дорогой, зимой всё × <c>winterOrdersMultiplier</c>.</summary>
        public static float OrdersPerDay(WorldState world, DataRegistry data)
        {
            CrisesBalance crises = data.Balance.Crises;
            float perDay = OrderGenerator.OrdersPerDay(data.Balance.Orders, world.Guild.Reputation, world.Settlement.Population);
            if (BuildingRules.IsReady(world, data, BuildingFunction.Road)) perDay += crises.RoadOrdersPerDay;
            if (IsWinter(world)) perDay *= crises.WinterOrdersMultiplier;
            return perDay;
        }

        /// <summary>Доход таверны от жителей во время мора — × <c>plagueTavernMultiplier</c>, иначе × 1.</summary>
        public static float LocalsTavernMultiplier(WorldState world, DataRegistry data) =>
            IsPlague(world) ? data.Balance.Crises.PlagueTavernMultiplier : 1f;

        /// <summary>Прибавка к росту населения в спокойствии: <c>wellGrowth</c> с готовым Колодцем.</summary>
        public static float ExtraCalmGrowth(WorldState world, DataRegistry data) =>
            BuildingRules.IsReady(world, data, BuildingFunction.Well) ? data.Balance.Crises.WellGrowth : 0f;

        /// <summary>
        /// Частокол: нападение в осаде возможно только при Безопасности ниже <c>palisadeAttackBelow</c>; без Частокола — всегда.
        /// </summary>
        public static bool AttackPossible(WorldState world, DataRegistry data) =>
            !BuildingRules.IsReady(world, data, BuildingFunction.Palisade) || world.Settlement.Security < data.Balance.Crises.PalisadeAttackBelow;

        /// <summary>Ущерб и погибшие при нападении: с Частоколом × <c>palisadeDamageMultiplier</c>, до целого от нуля.</summary>
        public static int AttackLoss(int value, WorldState world, DataRegistry data) =>
            BuildingRules.IsReady(world, data, BuildingFunction.Palisade)
                ? (int)Math.Round(value * data.Balance.Crises.PalisadeDamageMultiplier, MidpointRounding.AwayFromZero)
                : value;

        /// <summary>Множитель профиля на задании от Арсенала: 1 + <c>armoryProfilePerLevel</c> × уровень снаряжения.</summary>
        public static float GearMultiplier(int gearLevel, CrisesBalance crises) => 1f + crises.ArmoryProfilePerLevel * gearLevel;

        /// <summary>Налог: <c>taxBase</c> + <c>taxShareOfIncome</c> × доходы, до целого от нуля.</summary>
        public static int Tax(long income, CrisesBalance crises) =>
            crises.TaxBase + (int)Math.Round(Math.Max(0L, income) * (double)crises.TaxShareOfIncome, MidpointRounding.AwayFromZero);

        /// <summary>Доходы гильдии (все статьи доходов журнала) с часа <paramref name="fromHours"/> включительно.</summary>
        public static long IncomeSince(WorldState world, long fromHours)
        {
            long income = 0;
            var ledger = world.Treasury.Ledger;
            for (int i = ledger.Count - 1; i >= 0; i--)
            {
                LedgerEntry entry = ledger[i];
                if (entry.TimeHours < fromHours) break;
                if (entry.Category.Flow == LedgerFlow.Income) income += entry.Amount;
            }
            return income;
        }

        /// <summary>Время года по месяцу (1..<paramref name="monthsPerYear"/>): весна, лето, осень, зима — по четверти года.</summary>
        public static Season SeasonOf(int month, int monthsPerYear)
        {
            int quarter = Math.Max(1, monthsPerYear / 4);
            return (Season)Math.Min(3, Math.Max(0, (month - 1) / quarter));
        }
    }
}
