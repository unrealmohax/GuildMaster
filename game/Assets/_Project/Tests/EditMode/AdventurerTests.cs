using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GuildMaster.Core;
using GuildMaster.Data;
using NUnit.Framework;

namespace GuildMaster.Tests
{
    /// <summary>Генерация авантюристов (ТЗ 04 → «Генерация»).</summary>
    public sealed class AdventurerGenerationTests
    {
        private const int People = 1000;

        private PeopleData data;

        [SetUp]
        public void SetUp() => data = new PeopleData();

        [TearDown]
        public void TearDown() => data.Dispose();

        [Test]
        public void Generate1000_StatsInLevelRanges_ArchetypeMatchesType_SharesCloseToChances()
        {
            List<GeneratedAdventurer> people = GeneratePeople(20260926u);
            AdventurersBalance balance = data.Balance.Adventurers;

            foreach (GeneratedAdventurer generated in people)
            {
                for (int i = 0; i < Vocabulary.StatCount; i++)
                {
                    var stat = (StatId)i;
                    IntRange range = AllowedRange(generated, stat, balance);
                    float value = generated.Adventurer.GetStat(stat);
                    Assert.That(value, Is.InRange((float)Math.Max(range.Min, balance.NaturalMinimum), (float)range.Max),
                        $"{generated.Type.Id} {generated.Level} {stat}");
                }
                if (generated.Type.Kind == ArchetypeKind.Novice) Assert.AreEqual(AdventurerLevel.Weak, generated.Level);
                Assert.IsNotEmpty(generated.Adventurer.ArchetypeId, "у каждого читается архетип");
            }

            var report = new StringBuilder();
            IReadOnlyList<ArchetypeDefinition> archetypes = data.Registry.All<ArchetypeDefinition>();
            float totalWeight = archetypes.Sum(a => a.GenerationWeight);
            foreach (ArchetypeDefinition type in archetypes)
            {
                List<GeneratedAdventurer> ofType = people.Where(p => p.Type == type).ToList();
                float match = ofType.Count(p => p.Adventurer.ArchetypeId == type.Id) / (float)ofType.Count;
                float typeShare = ofType.Count / (float)People;
                float archetypeShare = people.Count(p => p.Adventurer.ArchetypeId == type.Id) / (float)People;
                float chance = type.GenerationWeight / totalWeight;
                report.AppendLine($"{type.Id}: chance {chance:P0}, type {typeShare:P1}, archetype {archetypeShare:P1}, match {match:P1}");

                // Решение 2026-09-26: роли ≈ 99,8%, Мастер — 100%, Новичок ≈ 95%.
                float minMatch = type.Kind == ArchetypeKind.JackOfAllTrades ? 1f : type.Kind == ArchetypeKind.Role ? 0.98f : 0.9f;
                Assert.That(match, Is.GreaterThanOrEqualTo(minMatch), $"{type.Id}: рассчитанный архетип совпадает с типом\n{report}");
                Assert.That(typeShare, Is.EqualTo(chance).Within(0.05f), $"{type.Id}: доля типа\n{report}");
                Assert.That(archetypeShare, Is.EqualTo(chance).Within(0.05f), $"{type.Id}: доля архетипа\n{report}");
            }
            TestContext.WriteLine(report.ToString());
        }

        [Test]
        public void Generate_AxesInRange_TraitsFollowRules_WalletRankHousing()
        {
            AdventurersBalance balance = data.Balance.Adventurers;
            foreach (GeneratedAdventurer generated in GeneratePeople(7u))
            {
                Adventurer adventurer = generated.Adventurer;
                foreach (float axis in adventurer.Axes) Assert.That(axis, Is.InRange(-100f, 100f));

                Assert.That(adventurer.Traits.Count, Is.InRange(balance.BirthTraitsLater.Min, balance.BirthTraitsLater.Max));
                foreach (TraitInstance trait in adventurer.Traits)
                {
                    var definition = data.Registry.Get<SpecialTraitDefinition>(trait.TraitId);
                    Assert.IsFalse(definition.IsAcquired, "при генерации — только черты «от рождения»");
                    Assert.IsFalse(definition.RequiresPartner, "в пустой гильдии черта с партнёром перебрасывается");
                    Assert.IsFalse(trait.Revealed, "черты скрыты");
                }
                Assert.AreEqual(adventurer.Traits.Count, adventurer.Traits.Select(t => data.Registry.Get<SpecialTraitDefinition>(t.TraitId).Category).Distinct().Count());

                Assert.That(adventurer.State.Wallet, Is.InRange(balance.Wallet.Min, balance.Wallet.Max));
                Assert.That(adventurer.Age, Is.InRange(balance.Age.Min, balance.Age.Max));
                Assert.AreEqual(GuildRank.G, adventurer.GuildRank);
                Assert.AreEqual(Housing.City, adventurer.Housing);
                Assert.IsFalse(adventurer.RevealedAxes.Any(r => r));
            }
        }

        private List<GeneratedAdventurer> GeneratePeople(uint seed)
        {
            var rng = new Rng(seed);
            var world = new WorldState();
            var names = new HashSet<string>();
            var people = new List<GeneratedAdventurer>(People);
            for (int i = 0; i < People; i++)
            {
                people.Add(AdventurerGenerator.Generate(rng, data.Registry, world, i + 1, atStart: false, names));
            }
            return people;
        }

        private static IntRange AllowedRange(GeneratedAdventurer generated, StatId stat, AdventurersBalance balance)
        {
            ArchetypeDefinition type = generated.Type;
            GenerationLevel level = balance.Levels.First(l => l.Level == generated.Level);
            bool characteristic = Vocabulary.IsCharacteristic(stat);
            switch (type.Kind)
            {
                case ArchetypeKind.JackOfAllTrades:
                    return level.JackOfAllTrades;
                case ArchetypeKind.Novice:
                    if (stat == StatId.Cohesion) return balance.Cohesion;
                    IntRange low = characteristic ? balance.NoviceCharacteristics : balance.NoviceSkills;
                    return new IntRange(low.Min, Math.Max(low.Max, balance.NovicePeak.Max));
                default:
                    if (type.MainStats.Contains(stat)) return level.MainStats;
                    if (type.SecondaryStats.Contains(stat)) return level.SecondaryStats;
                    if (stat == StatId.Cohesion) return balance.Cohesion;
                    return characteristic ? level.Characteristics : level.Skills;
            }
        }
    }

    /// <summary>Архетип и ранг характеристик (ТЗ 04, пример Щита из adventurers.md).</summary>
    public sealed class ArchetypeTests
    {
        private PeopleData data;

        [SetUp]
        public void SetUp() => data = new PeopleData();

        [TearDown]
        public void TearDown() => data.Dispose();

        /// <summary>Щит из adventurers.md: Выносливость 85, Сила 70, Хладнокровие 60, Слаженность 55, Реакция 30, остальные 25.</summary>
        private static float[] ShieldExample()
        {
            float[] values = Enumerable.Repeat(25f, Vocabulary.StatCount).ToArray();
            values[(int)StatId.Endurance] = 85f;
            values[(int)StatId.Strength] = 70f;
            values[(int)StatId.Composure] = 60f;
            values[(int)StatId.Cohesion] = 55f;
            values[(int)StatId.Reaction] = 30f;
            return values;
        }

        [Test]
        public void ShieldExample_Gives68_AsShield()
        {
            ArchetypeResult result = ArchetypeCalculator.Evaluate(ShieldExample(), data.Registry);

            Assert.AreEqual("Shield", result.Archetype.Id);
            Assert.That(result.PowerScore, Is.EqualTo(68f).Within(0.5f));
            Assert.That(result.Mean, Is.EqualTo(37.5f).Within(0.01f));

            RoleScore fighter = ArchetypeCalculator.Profile(ShieldExample(), data.Registry).Single(r => r.Role.Id == "Fighter");
            Assert.That(fighter.Score, Is.EqualTo(49f).Within(0.5f), "как Боец ≈ 49");
        }

        [Test]
        public void EvenProfile_IsJackOfAllTrades_ScoredByMean()
        {
            float[] values = Enumerable.Repeat(50f, Vocabulary.StatCount).ToArray();
            ArchetypeResult result = ArchetypeCalculator.Evaluate(values, data.Registry);

            Assert.AreEqual("JackOfAllTrades", result.Archetype.Id);
            Assert.That(result.PowerScore, Is.EqualTo(50f).Within(0.001f));
        }

        [Test]
        public void NoviceThreshold_Boundary()
        {
            float[] values = Enumerable.Repeat(10f, Vocabulary.StatCount).ToArray();
            values[(int)StatId.Endurance] = 40f;
            values[(int)StatId.Strength] = 40f;
            float best = ArchetypeCalculator.Evaluate(values, data.Registry).BestRoleScore;

            data.Set("adventurers.noviceThreshold", best);
            Assert.AreEqual("Shield", ArchetypeCalculator.Evaluate(values, data.Registry).Archetype.Id, "лучшая = порог — уже не Новичок");

            data.Set("adventurers.noviceThreshold", best + 0.01f);
            ArchetypeResult novice = ArchetypeCalculator.Evaluate(values, data.Registry);
            Assert.AreEqual("Novice", novice.Archetype.Id, "лучшая ниже порога — Новичок");
            Assert.AreEqual(best, novice.PowerScore, "у Новичка ранг — лучшая оценка");
        }

        [Test]
        public void JackOfAllTradesThreshold_Boundary()
        {
            float[] values = ShieldExample();
            ArchetypeResult shield = ArchetypeCalculator.Evaluate(values, data.Registry);
            float gap = shield.BestRoleScore - shield.Mean;

            data.Set("adventurers.jackOfAllTradesThreshold", gap);
            Assert.AreEqual("Shield", ArchetypeCalculator.Evaluate(values, data.Registry).Archetype.Id, "разница = порог — ещё роль");

            data.Set("adventurers.jackOfAllTradesThreshold", gap + 0.01f);
            ArchetypeResult jack = ArchetypeCalculator.Evaluate(values, data.Registry);
            Assert.AreEqual("JackOfAllTrades", jack.Archetype.Id, "разница меньше порога — Мастер на все руки");
            Assert.AreEqual(shield.Mean, jack.PowerScore, "у Мастера ранг — среднее всех 14");
        }

        [Test]
        public void Maimed_CountsInArchetype_ButNotBelowNaturalMinimum()
        {
            Simulation simulation = Simulation.CreateDefault(data.Registry, 3u);
            Adventurer adventurer = simulation.World.Adventurers.Active[0];
            float before = 0f;

            SimulationRun.Do(simulation, ctx =>
            {
                foreach (TraitInstance trait in adventurer.Traits.ToList()) TraitService.TryRemove(ctx, adventurer, trait.TraitId);
                for (int i = 0; i < Vocabulary.StatCount; i++) adventurer.SetStat((StatId)i, 12f);
                adventurer.SetStat(StatId.Strength, 80f);
                adventurer.SetStat(StatId.Endurance, 80f);
                ArchetypeService.Recalculate(ctx, adventurer);
                before = adventurer.PowerScore;
                Assert.IsTrue(TraitService.TryAcquire(ctx, adventurer, "Maimed", stat: StatId.Strength));
            });

            Assert.AreEqual("Shield", adventurer.ArchetypeId);
            Assert.AreEqual(80f, adventurer.GetStat(StatId.Strength), "базовое значение не меняется");
            Assert.That(AdventurerStats.Permanent(adventurer, StatId.Strength, data.Registry), Is.EqualTo(56f).Within(0.001f), "Калека −30%");
            Assert.That(AdventurerStats.Effective(adventurer, StatId.Strength, data.Registry), Is.EqualTo(56f).Within(0.001f));
            Assert.That(adventurer.PowerScore, Is.EqualTo(before - 0.7f * 24f / 2f).Within(0.01f), "ранг характеристик считается с Калекой");

            SimulationRun.Do(simulation, ctx =>
            {
                Assert.IsTrue(TraitService.TryRemove(ctx, adventurer, "Maimed"));
                Assert.IsTrue(TraitService.TryAcquire(ctx, adventurer, "Maimed", stat: StatId.Agility));
            });
            Assert.AreEqual(10f, AdventurerStats.Permanent(adventurer, StatId.Agility, data.Registry),
                "12 × 0,7 = 8,4, но Калека не опускает ниже естественного минимума 10");
            Assert.AreEqual(before - 0.1f * 2f / 10f, adventurer.PowerScore, 0.001f, "Сила снова целая, Ловкость у Щита — «остальные»");
        }
    }

    /// <summary>Стартовая шестёрка (ТЗ 04 → «Старт», решения 2026-09-26).</summary>
    public sealed class StartScenarioTests
    {
        private PeopleData data;

        [SetUp]
        public void SetUp() => data = new PeopleData();

        [TearDown]
        public void TearDown() => data.Dispose();

        [Test]
        public void Start_SixPeople_TwoToThreeTraits_OneToTwoPairs_NoCrowdedExtremePole()
        {
            AdventurersBalance balance = data.Balance.Adventurers;
            for (uint seed = 1; seed <= 200; seed++)
            {
                Simulation simulation = Simulation.CreateDefault(data.Registry, seed);
                IReadOnlyList<Adventurer> people = simulation.World.Adventurers.Active;
                string context = $"seed {seed}\n{PeopleDump.Of(simulation)}";

                Assert.AreEqual(6, people.Count, context);
                Assert.That(people.Sum(p => p.Traits.Count), Is.InRange(2, 3), context);
                Assert.That(people.Max(p => p.Traits.Count), Is.LessThanOrEqualTo(1), context);

                int traitPairs = people.Sum(p => p.Traits.Count(t => t.PartnerId != 0)) / 2;
                int friendPairs = simulation.World.Relations.All.Count(r => r.Value >= balance.OldFriendsRelation);
                Assert.That(traitPairs + friendPairs, Is.InRange(1, 2), context);
                foreach (Adventurer adventurer in people)
                {
                    foreach (TraitInstance trait in adventurer.Traits.Where(t => t.PartnerId != 0))
                    {
                        Adventurer partner = simulation.World.Adventurers.GetActive(trait.PartnerId);
                        Assert.IsTrue(partner.TryGetTrait(trait.TraitId, out TraitInstance back) && back.PartnerId == adventurer.Id, context);
                    }
                }

                for (int axis = 0; axis < Vocabulary.AxisCount; axis++)
                {
                    foreach (AxisPole pole in new[] { AxisPole.Negative, AxisPole.Positive })
                    {
                        int onPole = people.Count(p => AxisMath.IsOnExtremePole(p.GetAxis((AxisId)axis), pole, balance));
                        Assert.That(onPole, Is.LessThanOrEqualTo(2), $"{(AxisId)axis} {pole}\n{context}");
                    }
                }

                List<Adventurer> rankF = people.Where(p => p.GuildRank == GuildRank.F).ToList();
                Assert.That(rankF.Count, Is.InRange(1, 2), context);
                Assert.IsTrue(rankF.All(p => p.ArchetypeId != "Novice"), context);
                Assert.IsTrue(people.All(p => p.GuildRank == GuildRank.F || p.GuildRank == GuildRank.G), context);

                foreach (Adventurer adventurer in people)
                {
                    Assert.AreEqual(Housing.City, adventurer.Housing);
                    Assert.AreEqual(simulation.Calendar.StartTotalHours, adventurer.JoinedAtHours);
                    Assert.IsTrue(adventurer.Traits.All(t => !t.Revealed) && adventurer.RevealedAxes.All(r => !r), "всё скрыто");
                    Assert.That(adventurer.Stats.Min(), Is.GreaterThanOrEqualTo(10f));
                }
                Assert.AreEqual(people.Count, people.Select(p => p.Name).Distinct().Count(), "имена не повторяются");
            }
        }

        [Test]
        public void Start_OnlyWeakAndMediumLevels()
        {
            // Сильный уровень даёт основные параметры роли от 70; на старте максимум — средний (до 70) + ранг F (+10).
            for (uint seed = 1; seed <= 100; seed++)
            {
                Simulation simulation = Simulation.CreateDefault(data.Registry, seed);
                foreach (Adventurer adventurer in simulation.World.Adventurers.Active)
                {
                    float cap = adventurer.GuildRank == GuildRank.F ? 80f : 70f;
                    Assert.That(adventurer.Stats.Max(), Is.LessThanOrEqualTo(cap), $"seed {seed}");
                }
            }
        }

        [Test]
        public void SameSeed_SameLineup_DifferentSeed_Different()
        {
            string first = PeopleDump.Of(Simulation.CreateDefault(data.Registry, 42u));
            string second = PeopleDump.Of(Simulation.CreateDefault(data.Registry, 42u));
            string other = PeopleDump.Of(Simulation.CreateDefault(data.Registry, 43u));

            Assert.AreEqual(first, second);
            Assert.AreNotEqual(first, other);
            TestContext.WriteLine(first);
        }

        [Test]
        public void NumbersOnlyRegistry_HasNoPeople()
        {
            using var numbers = new TestData();
            Simulation simulation = Simulation.CreateDefault(numbers.Registry, 1u);
            Assert.IsEmpty(simulation.World.Adventurers.Active);
            Assert.AreEqual(0, simulation.World.Ids.LastIssued);
        }
    }

    /// <summary>Состав гильдии строкой — для сравнения прогонов и вывода в тестах.</summary>
    internal static class PeopleDump
    {
        public static string Of(Simulation simulation)
        {
            var text = new StringBuilder();
            AdventurerRoster roster = simulation.World.Adventurers;
            foreach (Adventurer adventurer in roster.Active) Append(text, "active", adventurer);
            foreach (Candidate candidate in roster.Candidates) Append(text, "candidate", candidate.Adventurer);
            foreach (Adventurer adventurer in roster.Archive) Append(text, "archive", adventurer);
            foreach (Relation relation in simulation.World.Relations.All)
            {
                text.Append(FormattableString.Invariant($"relation {relation.A}-{relation.B} {relation.Value:0.###} x{relation.JointQuests}")).Append('\n');
            }
            return text.ToString();
        }

        private static void Append(StringBuilder text, string list, Adventurer a)
        {
            text.Append(FormattableString.Invariant(
                $"{list} #{a.Id} {a.Name} {a.Gender} {a.Age} {a.GuildRank} {a.ArchetypeId} {a.PowerScore:0.##} wallet {a.State.Wallet}"));
            text.Append(" stats ").Append(string.Join(",", a.Stats.Select(v => v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture))));
            text.Append(" axes ").Append(string.Join(",", a.Axes.Select(v => v.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture))));
            text.Append(" traits ").Append(string.Join(",", a.Traits.Select(t => $"{t.TraitId}{(t.PartnerId != 0 ? "@" + t.PartnerId : "")}{(t.Revealed ? "!" : "")}")));
            text.Append('\n');
        }
    }
}
