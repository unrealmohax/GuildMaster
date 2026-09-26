using System;
using System.Collections.Generic;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Рост параметров (ТЗ 04 → «Рост», numbers.md → Рост). Функции для следующих систем: кто и когда тренируется — ТЗ 06,
    /// опыт заданий — ТЗ 09. После любого изменения архетип пересчитывается сразу.
    /// <list type="bullet">
    /// <item>Замедление к 99: прибавка × <c>(1 − значение/100)</c> (для значения от 99 — как у 99).</item>
    /// <item>Характеристика растёт в <c>characteristicSlowdown</c> (3) раза медленнее навыка.</item>
    /// <item>От 99 (<c>statCap</c>) и выше любая прибавка × <c>above99Multiplier</c> (0,1): рост почти останавливается.</item>
    /// <item>Хладнокровие и Слаженность на дворе не тренируются и опыта задания не получают — у них свои правила.</item>
    /// </list>
    /// </summary>
    public static class Growth
    {
        /// <summary>Тренируется ли параметр на дворе (Хладнокровие и Слаженность — нет).</summary>
        public static bool IsTrainable(StatId stat) => stat != StatId.Composure && stat != StatId.Cohesion;

        /// <summary>Прибавка за <paramref name="days"/> дней тренировки к параметру со значением <paramref name="value"/>.</summary>
        public static float TrainingGain(StatId stat, float value, float days, BalanceSettings balance)
        {
            if (days <= 0f || !IsTrainable(stat)) return 0f;
            return Slowed(stat, value, balance.Growth.TrainSkillPerDay * days, balance);
        }

        /// <summary>Прибавка опыта задания к параметру: +0,5 (успех) / +0,3 (провал) с замедлением.</summary>
        public static float QuestExperienceGain(StatId stat, float value, bool success, BalanceSettings balance)
        {
            if (!IsTrainable(stat)) return 0f;
            GrowthBalance growth = balance.Growth;
            return Slowed(stat, value, success ? growth.QuestExperienceSuccess : growth.QuestExperienceFailure, balance);
        }

        /// <summary>
        /// Тренировка на дворе: <paramref name="hours"/> часов (<c>trainingHoursPerDay</c> = 1 день, меньше — пропорционально).
        /// Возвращает прибавку (0, если параметр не тренируется).
        /// </summary>
        public static float Train(SimContext ctx, Adventurer adventurer, StatId stat, float hours)
        {
            GrowthBalance growth = ctx.Data.Balance.Growth;
            float gain = TrainingGain(stat, adventurer.GetStat(stat), hours / growth.TrainingHoursPerDay, ctx.Data.Balance);
            return Add(ctx, adventurer, stat, gain);
        }

        /// <summary>
        /// ❔ Что тренирует человек на дворе: основной параметр текущего архетипа (случайно один из двух);
        /// у Новичка и Мастера на все руки — один из двух самых высоких. Хладнокровие и Слаженность не тренируются.
        /// Случайность — из потока системы, которая вызывает (ТЗ 06).
        /// </summary>
        public static StatId PickTrainingStat(Adventurer adventurer, DataRegistry data, Rng rng)
        {
            var options = new List<StatId>(2);
            if (data.TryGet(adventurer.ArchetypeId, out ArchetypeDefinition archetype) && archetype.Kind == ArchetypeKind.Role)
            {
                foreach (StatId stat in archetype.MainStats)
                {
                    if (IsTrainable(stat)) options.Add(stat);
                }
            }
            if (options.Count == 0) options.AddRange(TwoHighestTrainable(adventurer, data));
            return rng.Pick(options);
        }

        /// <summary>
        /// Опыт задания по осям <paramref name="axes"/> (какие оси — решает ТЗ 09). <paramref name="multiplier"/> — например,
        /// Соперник на совместном задании × <c>rivalExperienceMultiplier</c>.
        /// </summary>
        public static void ApplyQuestExperience(SimContext ctx, Adventurer adventurer, IReadOnlyList<StatId> axes, bool success, float multiplier = 1f)
        {
            bool changed = false;
            foreach (StatId stat in axes)
            {
                float gain = QuestExperienceGain(stat, adventurer.GetStat(stat), success, ctx.Data.Balance) * multiplier;
                changed |= AddRaw(ctx, adventurer, stat, gain);
            }
            if (changed) ArchetypeService.Recalculate(ctx, adventurer);
        }

        /// <summary>Хладнокровие: + <c>composurePerHardQuest</c> за задание, где был провал раунда.</summary>
        public static float ApplyHardQuestComposure(SimContext ctx, Adventurer adventurer) =>
            Add(ctx, adventurer, StatId.Composure, Capped(adventurer.GetStat(StatId.Composure), ctx.Data.Balance.Growth.ComposurePerHardQuest, ctx.Data.Balance));

        /// <summary>Слаженность: + <c>cohesionPerGroupQuest</c> за групповое задание, + <c>cohesionPerPermanentPartyQuest</c> — постоянной группой.</summary>
        public static float ApplyGroupQuestCohesion(SimContext ctx, Adventurer adventurer, bool permanentParty)
        {
            GrowthBalance growth = ctx.Data.Balance.Growth;
            float amount = permanentParty ? growth.CohesionPerPermanentPartyQuest : growth.CohesionPerGroupQuest;
            return Add(ctx, adventurer, StatId.Cohesion, Capped(adventurer.GetStat(StatId.Cohesion), amount, ctx.Data.Balance));
        }

        /// <summary>
        /// Разовая прибавка к базовому параметру (Проверенный: Слаженность +5; «ожесточился»: Хладнокровие +5).
        /// Без замедления; не ниже естественного минимума.
        /// </summary>
        public static float AddBonus(SimContext ctx, Adventurer adventurer, StatId stat, float amount) => Add(ctx, adventurer, stat, amount);

        private static float Add(SimContext ctx, Adventurer adventurer, StatId stat, float amount)
        {
            float before = adventurer.GetStat(stat);
            if (!AddRaw(ctx, adventurer, stat, amount)) return 0f;
            ArchetypeService.Recalculate(ctx, adventurer);
            return adventurer.GetStat(stat) - before;
        }

        private static bool AddRaw(SimContext ctx, Adventurer adventurer, StatId stat, float amount)
        {
            if (amount == 0f) return false;
            float value = Math.Max(adventurer.GetStat(stat) + amount, ctx.Data.Balance.Adventurers.NaturalMinimum);
            adventurer.SetStat(stat, value);
            return true;
        }

        private static float Slowed(StatId stat, float value, float amount, BalanceSettings balance)
        {
            int cap = balance.Adventurers.StatCap;
            float gain = amount * (1f - Math.Min(value, cap) / 100f);
            if (Vocabulary.IsCharacteristic(stat)) gain /= balance.Growth.CharacteristicSlowdown;
            return Capped(value, gain, balance);
        }

        private static float Capped(float value, float gain, BalanceSettings balance) =>
            value >= balance.Adventurers.StatCap ? gain * balance.Growth.Above99Multiplier : gain;

        private static IEnumerable<StatId> TwoHighestTrainable(Adventurer adventurer, DataRegistry data)
        {
            StatId first = StatId.Strength, second = StatId.Strength;
            float firstValue = float.MinValue, secondValue = float.MinValue;
            for (int i = 0; i < Vocabulary.StatCount; i++)
            {
                var stat = (StatId)i;
                if (!IsTrainable(stat)) continue;

                float value = AdventurerStats.Permanent(adventurer, stat, data);
                if (value > firstValue)
                {
                    second = first;
                    secondValue = firstValue;
                    first = stat;
                    firstValue = value;
                }
                else if (value > secondValue)
                {
                    second = stat;
                    secondValue = value;
                }
            }
            yield return first;
            yield return second;
        }
    }
}
