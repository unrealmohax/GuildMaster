using System;
using System.Collections.Generic;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>Сгенерированный человек и тип, по которому он сгенерирован (архетип в игре всё равно считается из параметров).</summary>
    internal readonly struct GeneratedAdventurer
    {
        public GeneratedAdventurer(Adventurer adventurer, ArchetypeDefinition type, AdventurerLevel level)
        {
            Adventurer = adventurer;
            Type = type;
            Level = level;
        }

        public Adventurer Adventurer { get; }
        public ArchetypeDefinition Type { get; }
        public AdventurerLevel Level { get; }
    }

    /// <summary>
    /// Генерация авантюриста: пол, имя, возраст → тип → уровень → параметры →
    /// оси → черты → кошелёк, ранг, жильё → архетип → стартовое состояние. Случайность — только переданный поток
    /// той системы, что генерирует (<see cref="RecruitSystem"/>, <see cref="StartScenario"/>). Порядок бросков не менять без причины — сдвинутся составы.
    /// </summary>
    internal static class AdventurerGenerator
    {
        /// <param name="atStart">Стартовый человек: только слабый и средний уровень, черты раздаёт сценарий старта.</param>
        /// <param name="namesInUse">Имена, которые уже заняты: по возможности не повторять.</param>
        public static GeneratedAdventurer Generate(Rng rng, DataRegistry data, WorldState world, int id, bool atStart,
            ICollection<string> namesInUse)
        {
            AdventurersBalance balance = data.Balance.Adventurers;
            var adventurer = new Adventurer(id);
            long now = world.Time.TotalHours;

            // 1. Пол, имя, возраст.
            adventurer.Gender = rng.Chance(balance.MaleChance) ? Gender.Male : Gender.Female;
            adventurer.Name = PickName(rng, data.Names, adventurer.Gender, namesInUse);
            adventurer.Age = rng.RangeInclusive(balance.Age.Min, balance.Age.Max);

            // 2. Тип — по весам определений (временные шансы).
            ArchetypeDefinition type = PickType(rng, data);

            // 3. Уровень: у Новичка всегда слабый; на старте — не выше startMaxLevel.
            GenerationLevel level = PickLevel(rng, balance, type, atStart ? balance.StartMaxLevel : AdventurerLevel.Strong);

            // 4. Параметры.
            GenerateStats(rng, balance, type, level, adventurer);

            // 5. Оси: нормальное распределение, обрезка до ±100.
            for (int i = 0; i < Vocabulary.AxisCount; i++)
            {
                adventurer.SetAxis((AxisId)i, AxisMath.Clamp(rng.Normal(balance.AxisMean, balance.AxisStdDev)));
            }

            // 6. Особые черты «от рождения»: у стартовых их раздаёт StartScenario.
            if (!atStart)
            {
                int count = rng.RangeInclusive(balance.BirthTraitsLater.Min, balance.BirthTraitsLater.Max);
                for (int i = 0; i < count; i++) AddBirthTrait(rng, data, world, adventurer, now, allowPartner: true);
            }

            // 7. Кошелёк, ранг, жильё: Общежития на старте нет — город.
            adventurer.State.Wallet = rng.RangeInclusive(balance.Wallet.Min, balance.Wallet.Max);
            adventurer.GuildRank = GuildRank.G;
            adventurer.Housing = Housing.City;
            adventurer.JoinedAtHours = now;

            // 8. Архетип и ранг характеристик.
            ArchetypeService.Initialize(adventurer, data);

            // 9. Состояние: стресс и лояльность — последними бросками генератора, броски шагов 1–7 не сдвигаются.
            StateService.InitializeNew(rng, adventurer.State, data.Balance.State);
            return new GeneratedAdventurer(adventurer, type, level.Level);
        }

        /// <summary>
        /// Одна случайная черта «от рождения», которую можно добавить по правилам сочетания. Черта с партнёром — только если
        /// в гильдии есть подходящий партнёр, иначе перебросить. false — подходящей черты нет.
        /// </summary>
        public static bool AddBirthTrait(Rng rng, DataRegistry data, WorldState world, Adventurer adventurer, long now, bool allowPartner)
        {
            var pool = new List<SpecialTraitDefinition>();
            foreach (SpecialTraitDefinition trait in TraitRules.BirthTraits(data))
            {
                if ((allowPartner || !trait.RequiresPartner) && TraitRules.CanAdd(adventurer, trait, data)) pool.Add(trait);
            }

            while (pool.Count > 0)
            {
                SpecialTraitDefinition trait = rng.Pick(pool);
                if (!trait.RequiresPartner)
                {
                    TraitService.AddAtGeneration(adventurer, trait, now, 0);
                    return true;
                }

                var partners = new List<Adventurer>();
                foreach (Adventurer candidate in world.Adventurers.Active)
                {
                    if (candidate.Id != adventurer.Id && TraitRules.CanAdd(candidate, trait, data)) partners.Add(candidate);
                }
                if (partners.Count > 0)
                {
                    TraitService.AddAtGeneration(adventurer, trait, now, rng.Pick(partners).Id);
                    return true;
                }
                pool.Remove(trait);
            }
            return false;
        }

        /// <summary>Ранг F на старте: основные и вспомогательные параметры типа +<c>rankFBoost</c>, у Мастера на все руки — все 14.</summary>
        public static void ApplyRankF(DataRegistry data, GeneratedAdventurer generated)
        {
            AdventurersBalance balance = data.Balance.Adventurers;
            Adventurer adventurer = generated.Adventurer;
            adventurer.GuildRank = GuildRank.F;

            for (int i = 0; i < Vocabulary.StatCount; i++)
            {
                var stat = (StatId)i;
                bool boosted = generated.Type.Kind == ArchetypeKind.JackOfAllTrades
                               || Contains(generated.Type.MainStats, stat) || Contains(generated.Type.SecondaryStats, stat);
                if (boosted) adventurer.SetStat(stat, Math.Min(adventurer.GetStat(stat) + balance.RankFBoost, balance.StatCap));
            }
            ArchetypeService.Initialize(adventurer, data);
        }

        /// <summary>Оси заново, как при генерации (разнообразие стартовой шестёрки).</summary>
        public static void RerollAxes(Rng rng, AdventurersBalance balance, Adventurer adventurer)
        {
            for (int i = 0; i < Vocabulary.AxisCount; i++)
            {
                adventurer.SetAxis((AxisId)i, AxisMath.Clamp(rng.Normal(balance.AxisMean, balance.AxisStdDev)));
            }
        }

        /// <summary>Имя из списка имён по полу: свободное от <paramref name="namesInUse"/>, а если свободных нет — любое.</summary>
        internal static string PickName(Rng rng, NameList names, Gender gender, ICollection<string> namesInUse)
        {
            if (names == null) throw new InvalidOperationException("GameConfig has no NameList");
            IReadOnlyList<NounForms> list = gender == Gender.Male ? names.MaleNames : names.FemaleNames;
            if (list.Count == 0) throw new InvalidOperationException($"NameList has no {gender} names");

            var free = new List<string>(list.Count);
            foreach (NounForms name in list)
            {
                if (!namesInUse.Contains(name.Nominative)) free.Add(name.Nominative);
            }
            return free.Count > 0 ? rng.Pick(free) : rng.Pick(list).Nominative;
        }

        private static ArchetypeDefinition PickType(Rng rng, DataRegistry data)
        {
            IReadOnlyList<ArchetypeDefinition> archetypes = data.All<ArchetypeDefinition>();
            var weights = new float[archetypes.Count];
            for (int i = 0; i < weights.Length; i++) weights[i] = archetypes[i].GenerationWeight;
            return rng.PickWeighted(archetypes, weights);
        }

        private static GenerationLevel PickLevel(Rng rng, AdventurersBalance balance, ArchetypeDefinition type, AdventurerLevel maxLevel)
        {
            if (type.Kind == ArchetypeKind.Novice) return FindLevel(balance, AdventurerLevel.Weak);

            var levels = new List<GenerationLevel>();
            var weights = new List<float>();
            foreach (GenerationLevel level in balance.Levels)
            {
                if (level.Level > maxLevel) continue;
                levels.Add(level);
                weights.Add(level.Weight);
            }
            return rng.PickWeighted(levels, weights);
        }

        private static GenerationLevel FindLevel(AdventurersBalance balance, AdventurerLevel wanted)
        {
            foreach (GenerationLevel level in balance.Levels)
            {
                if (level.Level == wanted) return level;
            }
            throw new InvalidOperationException($"Adventurers balance has no level {wanted}");
        }

        /// <summary>
        /// Параметры по типу и уровню: у роли основные и вспомогательные выше прочих; у Мастера на все
        /// руки все 14 из своего диапазона; у Новичка всё низкое, 1–2 параметра (не Слаженность) — чуть выше.
        /// Слаженность 20–60 у всех, кроме Щита (как вспомогательный) и Мастера. Естественный минимум — 10.
        /// </summary>
        private static void GenerateStats(Rng rng, AdventurersBalance balance, ArchetypeDefinition type, GenerationLevel level, Adventurer adventurer)
        {
            for (int i = 0; i < Vocabulary.StatCount; i++)
            {
                var stat = (StatId)i;
                IntRange range;
                switch (type.Kind)
                {
                    case ArchetypeKind.JackOfAllTrades:
                        range = level.JackOfAllTrades;
                        break;
                    case ArchetypeKind.Novice:
                        range = stat == StatId.Cohesion ? balance.Cohesion
                            : Vocabulary.IsCharacteristic(stat) ? balance.NoviceCharacteristics
                            : balance.NoviceSkills;
                        break;
                    default:
                        range = Contains(type.MainStats, stat) ? level.MainStats
                            : Contains(type.SecondaryStats, stat) ? level.SecondaryStats
                            : stat == StatId.Cohesion ? balance.Cohesion
                            : Vocabulary.IsCharacteristic(stat) ? level.Characteristics
                            : level.Skills;
                        break;
                }
                adventurer.SetStat(stat, Roll(rng, range, balance));
            }

            if (type.Kind != ArchetypeKind.Novice) return;

            var peakable = new List<StatId>(Vocabulary.StatCount);
            for (int i = 0; i < Vocabulary.StatCount; i++)
            {
                if ((StatId)i != StatId.Cohesion) peakable.Add((StatId)i);
            }
            int peaks = rng.RangeInclusive(balance.NovicePeakCount.Min, balance.NovicePeakCount.Max);
            for (int p = 0; p < peaks && peakable.Count > 0; p++)
            {
                StatId stat = peakable[rng.Range(0, peakable.Count)];
                peakable.Remove(stat);
                adventurer.SetStat(stat, Math.Max(adventurer.GetStat(stat), Roll(rng, balance.NovicePeak, balance)));
            }
        }

        private static float Roll(Rng rng, IntRange range, AdventurersBalance balance)
        {
            int value = rng.RangeInclusive(range.Min, range.Max);
            return Math.Min(Math.Max(value, balance.NaturalMinimum), balance.StatCap);
        }

        private static bool Contains(IReadOnlyList<StatId> stats, StatId stat)
        {
            for (int i = 0; i < stats.Count; i++)
            {
                if (stats[i] == stat) return true;
            }
            return false;
        }
    }
}
