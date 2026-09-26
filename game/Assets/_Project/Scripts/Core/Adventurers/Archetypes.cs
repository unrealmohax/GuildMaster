using System;
using System.Collections.Generic;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>Итог расчёта архетипа.</summary>
    public readonly struct ArchetypeResult
    {
        public ArchetypeResult(ArchetypeDefinition archetype, float powerScore, ArchetypeDefinition bestRole, float bestRoleScore, float mean)
        {
            Archetype = archetype;
            PowerScore = powerScore;
            BestRole = bestRole;
            BestRoleScore = bestRoleScore;
            Mean = mean;
        }

        public ArchetypeDefinition Archetype { get; }

        /// <summary>Ранг характеристик.</summary>
        public float PowerScore { get; }

        /// <summary>Роль с лучшей оценкой (даже если архетип — Новичок или Мастер на все руки).</summary>
        public ArchetypeDefinition BestRole { get; }

        public float BestRoleScore { get; }

        /// <summary>Среднее всех 14 параметров.</summary>
        public float Mean { get; }
    }

    /// <summary>Оценка одной роли — строка профиля в карточке («Щит 68, Боец 49, …»).</summary>
    public readonly struct RoleScore
    {
        public RoleScore(ArchetypeDefinition role, float score)
        {
            Role = role;
            Score = score;
        }

        public ArchetypeDefinition Role { get; }
        public float Score { get; }
    }

    /// <summary>
    /// Архетип и ранг характеристик по параметрам:
    /// <c>оценка роли = 0,7 × среднее(основные) + 0,2 × среднее(вспомогательные) + 0,1 × среднее(остальные)</c>;
    /// лучшая &lt; <c>noviceThreshold</c> → Новичок; лучшая − среднее(все 14) &lt; <c>jackOfAllTradesThreshold</c> →
    /// Мастер на все руки (ранг = среднее); иначе — лучшая роль. Роли — архетипы вида <see cref="ArchetypeKind.Role"/>
    /// в порядке GameConfig; при равных оценках берётся первая. Чистые функции, мир не меняют.
    /// </summary>
    public static class ArchetypeCalculator
    {
        public static float RoleScoreOf(IReadOnlyList<float> values, ArchetypeDefinition role, AdventurersBalance balance)
        {
            if (values.Count != Vocabulary.StatCount) throw new ArgumentException($"Expected {Vocabulary.StatCount} stat values", nameof(values));

            float mainSum = 0f, secondarySum = 0f, restSum = 0f;
            int mainCount = 0, secondaryCount = 0, restCount = 0;
            for (int i = 0; i < values.Count; i++)
            {
                var stat = (StatId)i;
                if (Contains(role.MainStats, stat))
                {
                    mainSum += values[i];
                    mainCount++;
                }
                else if (Contains(role.SecondaryStats, stat))
                {
                    secondarySum += values[i];
                    secondaryCount++;
                }
                else
                {
                    restSum += values[i];
                    restCount++;
                }
            }

            return balance.RoleMainWeight * Average(mainSum, mainCount)
                   + balance.RoleSecondaryWeight * Average(secondarySum, secondaryCount)
                   + balance.RoleRestWeight * Average(restSum, restCount);
        }

        /// <summary>Оценки всех ролей в порядке GameConfig.</summary>
        public static RoleScore[] Profile(IReadOnlyList<float> values, DataRegistry data)
        {
            var scores = new List<RoleScore>();
            foreach (ArchetypeDefinition archetype in data.All<ArchetypeDefinition>())
            {
                if (archetype.Kind == ArchetypeKind.Role)
                    scores.Add(new RoleScore(archetype, RoleScoreOf(values, archetype, data.Balance.Adventurers)));
            }
            return scores.ToArray();
        }

        public static ArchetypeResult Evaluate(IReadOnlyList<float> values, DataRegistry data)
        {
            AdventurersBalance balance = data.Balance.Adventurers;
            ArchetypeDefinition novice = null, jack = null, bestRole = null;
            float bestScore = float.MinValue;

            foreach (ArchetypeDefinition archetype in data.All<ArchetypeDefinition>())
            {
                switch (archetype.Kind)
                {
                    case ArchetypeKind.Novice:
                        novice = novice ?? archetype;
                        break;
                    case ArchetypeKind.JackOfAllTrades:
                        jack = jack ?? archetype;
                        break;
                    default:
                        float score = RoleScoreOf(values, archetype, balance);
                        if (score > bestScore)
                        {
                            bestScore = score;
                            bestRole = archetype;
                        }
                        break;
                }
            }

            if (bestRole == null || novice == null || jack == null)
                throw new InvalidOperationException("Archetypes need at least one role, Novice and JackOfAllTrades");

            float sum = 0f;
            for (int i = 0; i < values.Count; i++) sum += values[i];
            float mean = sum / values.Count;

            if (bestScore < balance.NoviceThreshold) return new ArchetypeResult(novice, bestScore, bestRole, bestScore, mean);
            if (bestScore - mean < balance.JackOfAllTradesThreshold) return new ArchetypeResult(jack, mean, bestRole, bestScore, mean);
            return new ArchetypeResult(bestRole, bestScore, bestRole, bestScore, mean);
        }

        /// <summary>Архетип человека: параметры с постоянными модификаторами (Калека учитывается, рана и усталость — нет).</summary>
        public static ArchetypeResult Evaluate(Adventurer adventurer, DataRegistry data) =>
            Evaluate(AdventurerStats.PermanentProfile(adventurer, data), data);

        private static bool Contains(IReadOnlyList<StatId> stats, StatId stat)
        {
            for (int i = 0; i < stats.Count; i++)
            {
                if (stats[i] == stat) return true;
            }
            return false;
        }

        private static float Average(float sum, int count) => count == 0 ? 0f : sum / count;
    }

    /// <summary>
    /// Пересчёт архетипа человека: раз в сутки (<see cref="AdventurerSystem"/>) и сразу после изменения параметров.
    /// Смена архетипа — событие <see cref="SimEventType.ArchetypeChanged"/> без автопаузы.
    /// </summary>
    public static class ArchetypeService
    {
        /// <summary>Пересчитать; true — архетип сменился.</summary>
        public static bool Recalculate(SimContext ctx, Adventurer adventurer)
        {
            ArchetypeResult result = ArchetypeCalculator.Evaluate(adventurer, ctx.Data);
            adventurer.PowerScore = result.PowerScore;

            string previous = adventurer.ArchetypeId;
            if (previous == result.Archetype.Id) return false;

            adventurer.ArchetypeId = result.Archetype.Id;
            ctx.Events.Publish(SimEventType.ArchetypeChanged, EventImportance.Normal, adventurer.Id)
                .With("from", previous)
                .With("to", result.Archetype.Id);
            return true;
        }

        /// <summary>Первый расчёт при генерации — без события.</summary>
        internal static void Initialize(Adventurer adventurer, DataRegistry data)
        {
            ArchetypeResult result = ArchetypeCalculator.Evaluate(adventurer, data);
            adventurer.ArchetypeId = result.Archetype.Id;
            adventurer.PowerScore = result.PowerScore;
        }
    }
}
