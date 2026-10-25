using System;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>Чистые правила поселения: уровень по Безопасности, сдвиги за заказы, население за месяц, доход таверны.</summary>
    public static class SettlementRules
    {
        /// <summary>Уровень по Безопасности: от порога <c>calmFrom</c> — спокойно, <c>normalFrom</c> — норма, <c>alarmFrom</c> — тревога, ниже — осада.</summary>
        public static SettlementLevel LevelOf(float security, SettlementBalance balance)
        {
            if (security >= balance.CalmFrom) return SettlementLevel.Calm;
            if (security >= balance.NormalFrom) return SettlementLevel.Normal;
            if (security >= balance.AlarmFrom) return SettlementLevel.Alarm;
            return SettlementLevel.Siege;
        }

        /// <summary>Номер ранга для сдвигов «× ранг»: G = 1 … C = 5.</summary>
        public static int RankFactor(GuildRank rank) => (int)rank + 1;

        /// <summary>Заказ этого типа — угроза поселению. Неизвестный тип — не угроза.</summary>
        public static bool IsThreat(DataRegistry data, string questTypeId) =>
            questTypeId != null && data.TryGet(questTypeId, out QuestTypeDefinition type) && type.IsThreat;

        /// <summary>
        /// Изменение населения в начале месяца: спокойно — + <c>calmGrowth</c>, норма — 0, тревога — − <c>alarmDecline</c>,
        /// осада — − <c>siegeDecline</c> от населения; ненулевая доля округляется от нуля (не меньше одного человека).
        /// </summary>
        public static int MonthlyPopulationChange(int population, SettlementLevel level, SettlementBalance balance)
        {
            float share;
            switch (level)
            {
                case SettlementLevel.Calm: share = balance.CalmGrowth; break;
                case SettlementLevel.Alarm: share = -balance.AlarmDecline; break;
                case SettlementLevel.Siege: share = -balance.SiegeDecline; break;
                default: share = 0f; break;
            }
            if (share == 0f || population <= 0) return 0;

            // Допуск — на погрешность float: 80 × 0,05 не должно стать 4,0000001 и округлиться до 5.
            double exact = population * (double)share;
            return exact > 0 ? (int)Math.Ceiling(exact - 1e-4) : -(int)Math.Ceiling(-exact - 1e-4);
        }

        /// <summary>Доход таверны от жителей за сутки (без Сухого закона): население × <c>localsTavernIncome</c>, в осаде × <c>siegeTavernMultiplier</c>.</summary>
        public static double LocalsTavernIncome(SettlementState settlement, SettlementBalance balance)
        {
            double income = settlement.Population * (double)balance.LocalsTavernIncome;
            return settlement.Level == SettlementLevel.Siege ? income * balance.SiegeTavernMultiplier : income;
        }

        /// <summary>Сколько суток осталось до покидания; <c>-1</c> — отсчёта нет. Неполные сутки считаются целыми.</summary>
        public static int DaysUntilAbandoned(SettlementState settlement, long nowHours, Calendar calendar, SettlementBalance balance)
        {
            if (!settlement.ZeroSecuritySinceHours.HasValue) return -1;
            long endsAt = settlement.ZeroSecuritySinceHours.Value + calendar.DaysToHours(balance.AbandonDays);
            long left = Math.Max(0, endsAt - nowHours);
            return (int)((left + calendar.HoursPerDay - 1) / calendar.HoursPerDay);
        }
    }
}
