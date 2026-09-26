using System.Collections.Generic;
using System.Linq;
using GuildMaster.Core;
using GuildMaster.Data;
using NUnit.Framework;

namespace GuildMaster.Tests
{
    /// <summary>Рост параметров.</summary>
    public sealed class GrowthTests
    {
        private PeopleData data;
        private Simulation simulation;

        [SetUp]
        public void SetUp()
        {
            data = new PeopleData();
            simulation = Simulation.CreateDefault(data.Registry, 5u);
        }

        [TearDown]
        public void TearDown() => data.Dispose();

        private float Gain(StatId stat, float value) => Growth.TrainingGain(stat, value, 1f, data.Balance);

        [Test]
        public void Training_SlowsTowards99_AndAlmostStopsAbove()
        {
            Assert.That(Gain(StatId.Stealth, 20f), Is.EqualTo(0.3f * 0.8f).Within(1e-6f), "+0,3 × (1 − 20/100)");
            Assert.That(Gain(StatId.Stealth, 80f), Is.EqualTo(0.3f * 0.2f).Within(1e-6f));

            float[] values = { 10f, 30f, 50f, 70f, 90f, 98f, 99f };
            for (int i = 1; i < values.Length; i++)
                Assert.That(Gain(StatId.Stealth, values[i]), Is.LessThan(Gain(StatId.Stealth, values[i - 1])), $"{values[i]}");

            float at99 = Gain(StatId.Stealth, 99f);
            Assert.That(at99, Is.EqualTo(0.3f * 0.01f * 0.1f).Within(1e-7f), "от 99 — × 0,1");
            Assert.AreEqual(at99, Gain(StatId.Stealth, 150f), "выше 99 рост почти стоит и дальше не ускоряется");
            Assert.That(at99 * 360f, Is.LessThan(0.2f), "меньше 0,2 за год ежедневной тренировки");
        }

        [Test]
        public void Characteristics_GrowThreeTimesSlowerThanSkills()
        {
            foreach (float value in new[] { 15f, 40f, 75f, 99f })
            {
                Assert.That(Gain(StatId.Strength, value), Is.EqualTo(Gain(StatId.Marksmanship, value) / 3f).Within(1e-6f));
                Assert.That(Growth.QuestExperienceGain(StatId.Agility, value, true, data.Balance),
                    Is.EqualTo(Growth.QuestExperienceGain(StatId.Knowledge, value, true, data.Balance) / 3f).Within(1e-6f));
            }
        }

        [Test]
        public void QuestExperience_SuccessAndFailure()
        {
            Assert.That(Growth.QuestExperienceGain(StatId.Survival, 40f, true, data.Balance), Is.EqualTo(0.5f * 0.6f).Within(1e-6f));
            Assert.That(Growth.QuestExperienceGain(StatId.Survival, 40f, false, data.Balance), Is.EqualTo(0.3f * 0.6f).Within(1e-6f));
        }

        [Test]
        public void ComposureAndCohesion_NotTrainedOnYard_GrowOnlyByTheirRules()
        {
            Assert.IsFalse(Growth.IsTrainable(StatId.Composure));
            Assert.IsFalse(Growth.IsTrainable(StatId.Cohesion));
            Assert.AreEqual(0f, Gain(StatId.Composure, 20f));
            Assert.AreEqual(0f, Growth.QuestExperienceGain(StatId.Cohesion, 20f, true, data.Balance));

            Adventurer adventurer = simulation.World.Adventurers.Active[0];
            float composure = adventurer.GetStat(StatId.Composure);
            float cohesion = adventurer.GetStat(StatId.Cohesion);
            SimulationRun.Do(simulation, ctx =>
            {
                Growth.ApplyQuestExperience(ctx, adventurer, new[] { StatId.Composure, StatId.Cohesion }, success: true);
                Assert.AreEqual(composure, adventurer.GetStat(StatId.Composure));

                Growth.ApplyHardQuestComposure(ctx, adventurer);
                Growth.ApplyGroupQuestCohesion(ctx, adventurer, permanentParty: false);
                Growth.ApplyGroupQuestCohesion(ctx, adventurer, permanentParty: true);
            });

            Assert.That(adventurer.GetStat(StatId.Composure), Is.EqualTo(composure + 0.3f).Within(1e-5f));
            Assert.That(adventurer.GetStat(StatId.Cohesion), Is.EqualTo(cohesion + 1.5f).Within(1e-5f));
        }

        [Test]
        public void Train_HoursArePartOfDay_AndYearsOfTrainingStayNear99()
        {
            Adventurer adventurer = simulation.World.Adventurers.Active[0];
            SimulationRun.Do(simulation, ctx =>
            {
                adventurer.SetStat(StatId.Stealth, 20f);
                float half = Growth.Train(ctx, adventurer, StatId.Stealth, hours: 4f);
                Assert.That(half, Is.EqualTo(0.3f * 0.8f / 2f).Within(1e-6f), "4 часа из 8 — полдня");

                for (int day = 0; day < 360 * 20; day++) Growth.Train(ctx, adventurer, StatId.Stealth, hours: 8f);
            });

            float value = adventurer.GetStat(StatId.Stealth);
            Assert.That(value, Is.GreaterThan(99f), "за 20 лет навык дорастает до 99");
            Assert.That(value, Is.LessThan(101f), "и выше почти не растёт: ≈ 0,1 за год");
        }

        [Test]
        public void PickTrainingStat_MainStatsOfArchetype_NoviceTwoHighest()
        {
            Adventurer adventurer = simulation.World.Adventurers.Active[0];
            var rng = new Rng(1u);

            SimulationRun.Do(simulation, ctx =>
            {
                for (int i = 0; i < Vocabulary.StatCount; i++) adventurer.SetStat((StatId)i, 20f);
                adventurer.SetStat(StatId.Medicine, 60f);
                adventurer.SetStat(StatId.Composure, 60f);
                ArchetypeService.Recalculate(ctx, adventurer);
            });
            Assert.AreEqual("Medic", adventurer.ArchetypeId);
            var picks = new HashSet<StatId>(Enumerable.Range(0, 50).Select(_ => Growth.PickTrainingStat(adventurer, data.Registry, rng)));
            CollectionAssert.AreEquivalent(new[] { StatId.Medicine }, picks, "у Лекаря Хладнокровие на дворе не тренируется");

            SimulationRun.Do(simulation, ctx =>
            {
                for (int i = 0; i < Vocabulary.StatCount; i++) adventurer.SetStat((StatId)i, 12f);
                adventurer.SetStat(StatId.Knowledge, 22f);
                adventurer.SetStat(StatId.Crafting, 20f);
                adventurer.SetStat(StatId.Cohesion, 40f);
                ArchetypeService.Recalculate(ctx, adventurer);
            });
            Assert.AreEqual("Novice", adventurer.ArchetypeId);
            picks = new HashSet<StatId>(Enumerable.Range(0, 50).Select(_ => Growth.PickTrainingStat(adventurer, data.Registry, rng)));
            CollectionAssert.AreEquivalent(new[] { StatId.Knowledge, StatId.Crafting }, picks);
        }

        [Test]
        public void Growth_RecalculatesArchetype_WithEvent()
        {
            Adventurer adventurer = simulation.World.Adventurers.Active[0];
            SimulationRun.Do(simulation, ctx =>
            {
                for (int i = 0; i < Vocabulary.StatCount; i++) adventurer.SetStat((StatId)i, 12f);
                ArchetypeService.Recalculate(ctx, adventurer);
            });
            Assert.AreEqual("Novice", adventurer.ArchetypeId);

            SimulationRun.Do(simulation, ctx =>
            {
                Growth.AddBonus(ctx, adventurer, StatId.Agility, 50f);
                Growth.AddBonus(ctx, adventurer, StatId.Stealth, 50f);
            });
            IReadOnlyList<SimEvent> events = simulation.Events.Events;

            Assert.AreEqual("Scout", adventurer.ArchetypeId);
            SimEvent changed = events.Last(e => e.Type == SimEventType.ArchetypeChanged);
            Assert.AreEqual(EventImportance.Normal, changed.Importance);
            Assert.IsTrue(changed.TryGet("to", out string to) && to == "Scout");
            Assert.IsFalse(AutopauseRules.Default.ContainsKey(SimEventType.ArchetypeChanged), "смена архетипа — без автопаузы");
        }
    }

    /// <summary>Ранг гильдии.</summary>
    public sealed class GuildRankTests
    {
        private PeopleData data;
        private Simulation simulation;

        [SetUp]
        public void SetUp()
        {
            data = new PeopleData();
            simulation = Simulation.CreateDefault(data.Registry, 5u);
        }

        [TearDown]
        public void TearDown() => data.Dispose();

        [Test]
        public void Points_Threshold_Promotion_Failure()
        {
            Adventurer adventurer = simulation.World.Adventurers.Active.First(a => a.GuildRank == GuildRank.G);
            RanksBalance ranks = data.Balance.Ranks;
            long now = simulation.World.Time.TotalHours;

            SimulationRun.Do(simulation, ctx => GuildRanks.AddPoints(ctx, adventurer, 9.5f));
            Assert.IsFalse(GuildRanks.IsReadyForPromotion(adventurer, ranks, now), "9,5 из 10");

            SimulationRun.Do(simulation, ctx => GuildRanks.AddPoints(ctx, adventurer, 0.5f));
            Assert.IsTrue(GuildRanks.IsReadyForPromotion(adventurer, ranks, now));

            SimulationRun.Do(simulation, ctx => GuildRanks.FailPromotion(ctx, adventurer));
            long retry = now + simulation.Calendar.DaysToHours(30);
            Assert.AreEqual(retry, adventurer.PromotionReadyAtHours);
            Assert.IsFalse(GuildRanks.IsReadyForPromotion(adventurer, ranks, retry - 1), "после провала — через 30 дней");
            Assert.IsTrue(GuildRanks.IsReadyForPromotion(adventurer, ranks, retry));

            SimulationRun.Do(simulation, ctx => Assert.IsTrue(GuildRanks.Promote(ctx, adventurer)));
            Assert.AreEqual(GuildRank.F, adventurer.GuildRank);
            Assert.AreEqual(0f, adventurer.RankPoints, "после повышения очки обнуляются");
            Assert.AreEqual(25, GuildRanks.PointsToNext(adventurer, ranks));
            Assert.AreEqual(EventImportance.Notable, simulation.Events.Events.Last(e => e.Type == SimEventType.RankPromoted).Importance);
        }

        [Test]
        public void TopRank_CannotBePromoted_OrdersOfOwnRankAndBelow()
        {
            Adventurer adventurer = simulation.World.Adventurers.Active[0];
            adventurer.GuildRank = GuildRank.C;
            adventurer.RankPoints = 1000f;

            Assert.IsFalse(GuildRanks.IsReadyForPromotion(adventurer, data.Balance.Ranks, long.MaxValue));
            SimulationRun.Do(simulation, ctx => Assert.IsFalse(GuildRanks.Promote(ctx, adventurer)));

            adventurer.GuildRank = GuildRank.E;
            Assert.IsTrue(GuildRanks.CanTakeOrder(adventurer, GuildRank.G));
            Assert.IsTrue(GuildRanks.CanTakeOrder(adventurer, GuildRank.E));
            Assert.IsFalse(GuildRanks.CanTakeOrder(adventurer, GuildRank.D));
        }
    }

    /// <summary>Отношения.</summary>
    public sealed class RelationTests
    {
        private PeopleData data;
        private Simulation simulation;

        [SetUp]
        public void SetUp()
        {
            data = new PeopleData();
            simulation = Simulation.CreateDefault(data.Registry, 5u);
        }

        [TearDown]
        public void TearDown() => data.Dispose();

        [Test]
        public void Pair_IsSymmetric_Clamped_WithLabels()
        {
            RelationBook relations = simulation.World.Relations;
            AdventurersBalance balance = data.Balance.Adventurers;

            SimulationRun.Do(simulation, ctx =>
            {
                RelationService.Change(ctx, 100, 200, 30f);
                RelationService.Change(ctx, 200, 100, 15f);
            });
            Assert.AreEqual(45f, relations.GetValue(100, 200));
            Assert.AreEqual(45f, relations.GetValue(200, 100));
            Assert.AreEqual(RelationLabels.Friends, RelationService.LabelsOf(relations, 100, 200, balance));

            SimulationRun.Do(simulation, ctx =>
            {
                RelationService.Change(ctx, 100, 200, 500f);
                Assert.AreEqual(100f, relations.GetValue(100, 200));
                RelationService.Change(ctx, 100, 200, -1000f);
                for (int i = 0; i < 10; i++) RelationService.AddJointQuest(ctx, 200, 100);
            });
            Assert.AreEqual(-100f, relations.GetValue(100, 200));
            Assert.AreEqual(10, relations.GetJointQuests(100, 200));
            Assert.AreEqual(RelationLabels.Dislike | RelationLabels.LongPartners, RelationService.LabelsOf(relations, 100, 200, balance));

            Assert.AreEqual(0f, relations.GetValue(100, 300), "нет записи — 0");
            Assert.AreEqual(RelationLabels.None, RelationService.LabelsOf(relations, 100, 300, balance));
            Assert.Throws<System.ArgumentException>(() => relations.GetValue(7, 7));
        }
    }
}
