using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GuildMaster.Core;
using GuildMaster.Data;
using GuildMaster.Debugging;
using NUnit.Framework;

namespace GuildMaster.Tests
{
    /// <summary>
    /// Риск в заданиях: убывающая отдача группы (вклад × множитель Слаженности, на каждой оси — лучший целиком, остальные
    /// с убывающими весами), потолок шанса раунда, одно правило сложения для настоящего расчёта и оценки глазами человека.
    /// </summary>
    public sealed class QuestRiskTests
    {
        private static float Effective(Adventurer person, StatId stat, QuestWorld quests) => AdventurerStats.Effective(person, stat, quests.Registry);

        [Test]
        public void OnePerson_ProfileAsBefore()
        {
            using var quests = new QuestWorld();
            Adventurer a = PartyTestSupport.Person(quests, stats: 40f, cohesion: 20f);
            a.SetStat(StatId.Strength, 55f);
            float[] profile = QuestMath.GroupProfile(new[] { a }, quests.Registry);
            float solo = quests.Registry.Balance.Rounds.SoloProfileMultiplier;
            foreach (StatId stat in quests.Registry.Stats.RadarOrder)
                Assert.AreEqual(Effective(a, stat, quests) * solo, profile[(int)stat], 1e-4, $"один — эффективный параметр × множитель одиночки ({stat})");
            Assert.AreEqual(0f, profile[(int)StatId.Cohesion], "Слаженность — не ось");
        }

        [Test]
        public void SecondPerson_AddsSixTenths_OnAxisWhereNotBest()
        {
            using var quests = new QuestWorld();
            Adventurer a = PartyTestSupport.Person(quests, stats: 30f, cohesion: 100f);
            Adventurer b = PartyTestSupport.Person(quests, stats: 30f, cohesion: 100f);
            a.SetStat(StatId.Strength, 50f);
            b.SetStat(StatId.Agility, 45f);
            float[] profile = QuestMath.GroupProfile(new[] { a, b }, quests.Registry);

            float aStrength = Effective(a, StatId.Strength, quests), bStrength = Effective(b, StatId.Strength, quests);
            Assert.Greater(aStrength, bStrength);
            Assert.AreEqual(aStrength + 0.6f * bStrength, profile[(int)StatId.Strength], 1e-4, "на Силе лучший — первый, второй × 0,6");
            float aAgility = Effective(a, StatId.Agility, quests), bAgility = Effective(b, StatId.Agility, quests);
            Assert.Greater(bAgility, aAgility);
            Assert.AreEqual(bAgility + 0.6f * aAgility, profile[(int)StatId.Agility], 1e-4, "порядок — по своей оси: на Ловкости первый — второй человек");
        }

        [Test]
        public void ManyPeople_EachAddsLessThanPrevious()
        {
            using var quests = new QuestWorld();
            var people = Enumerable.Range(0, 6).Select(_ => PartyTestSupport.Person(quests, stats: 30f, cohesion: 100f)).ToList();
            float value = Effective(people[0], StatId.Strength, quests);
            float[] weights = { 1f, 0.6f, 0.45f, 0.35f, 0.3f, 0.25f };
            float previousGain = float.MaxValue, previous = 0f;
            for (int count = 1; count <= 6; count++)
            {
                float strength = QuestMath.GroupProfile(people.Take(count).ToList(), quests.Registry)[(int)StatId.Strength];
                Assert.AreEqual(value * weights.Take(count).Sum(), strength, 1e-3, $"{count} человек — сумма весов");
                float gain = strength - previous;
                Assert.Less(gain, previousGain + 1e-4f, "каждый следующий добавляет не больше предыдущего");
                previousGain = gain;
                previous = strength;
            }
        }

        [Test]
        public void Cohesion_100_FullContribution_20_TimesPoint76()
        {
            using var quests = new QuestWorld();
            RoundsBalance rounds = quests.Registry.Balance.Rounds;
            Adventurer tight = PartyTestSupport.Person(quests, cohesion: 100f);
            Adventurer loose = PartyTestSupport.Person(quests, cohesion: 20f);
            Adventurer none = PartyTestSupport.Person(quests, cohesion: 0f);
            Assert.AreEqual(1f, QuestMath.GroupMultiplier(tight, rounds), 1e-5, "Слаженность 100 — вклад как у одиночки");
            Assert.AreEqual(0.76f, QuestMath.GroupMultiplier(loose, rounds), 1e-5, "Слаженность 20 — × 0,76");
            Assert.AreEqual(0.7f, QuestMath.GroupMultiplier(none, rounds), 1e-5, "Слаженность 0 — × 0,7, не ноль");

            Adventurer looseTwin = PartyTestSupport.Person(quests, cohesion: 20f);
            float strength = Effective(loose, StatId.Strength, quests);
            Assert.AreEqual(strength * 0.76f * 1.6f, QuestMath.GroupProfile(new[] { loose, looseTwin }, quests.Registry)[(int)StatId.Strength], 1e-3,
                "двое со Слаженностью 20: (1 + 0,6) × 0,76 вклада");
            Assert.Greater(QuestMath.GroupProfile(new[] { loose, looseTwin }, quests.Registry)[(int)StatId.Strength],
                QuestMath.GroupProfile(new[] { loose }, quests.Registry)[(int)StatId.Strength], "несыгранная пара не слабее одиночки");
        }

        [Test]
        public void RoundChance_NotAboveCap()
        {
            using var quests = new QuestWorld();
            RoundsBalance rounds = quests.Registry.Balance.Rounds;
            Assert.AreEqual(rounds.MaxRoundChance, QuestMath.RoundChance(1f, 0.2f, rounds), 1e-6);
            Assert.AreEqual(0.5f, QuestMath.RoundChance(0.45f, 0.05f, rounds), 1e-6, "ниже потолка — перекрытие + синергия");
            Assert.AreEqual(0f, QuestMath.RoundChance(0.05f, -0.2f, rounds), 1e-6, "не ниже 0");

            Adventurer a = PartyTestSupport.Person(quests, stats: 90f);
            Adventurer b = PartyTestSupport.Person(quests, stats: 90f);
            quests.World.Do(ctx => RelationService.Set(ctx, a.Id, b.Id, 50f));
            Order order = quests.AddOrder(requirement: 10f);
            Assert.AreEqual(rounds.MaxRoundChance, PartyMath.PerceivedChance(a, order, new[] { a, b }, quests.Simulation.World.Relations, quests.Registry), 1e-6,
                "воспринимаемый шанс — с тем же потолком");

            QuestRun run = quests.Start(order, a, b);
            Assert.AreEqual(rounds.MaxRoundChance, DebugQueries.RoundChance(run, quests.Simulation.World, quests.Registry), 1e-6, "шанс в отладке");
            quests.AtSite(run);
            List<SimEvent> events = quests.World.Collect(() => quests.Simulation.Tick());
            SimEvent round = events.Single(e => e.Type == SimEventType.RoundSuccess || e.Type == SimEventType.RoundFail);
            round.TryGet("chance", out float chance);
            Assert.AreEqual(rounds.MaxRoundChance, chance, 1e-6, "настоящий раунд — не выше потолка");
        }

        [Test]
        public void Exam_ChanceNotAboveCap()
        {
            using var quests = new QuestWorld();
            quests.Data.NoQuests();
            Adventurer a = quests.Add();
            a.RankPoints = 10f;
            quests.World.TickToHour(quests.Data.Balance.Time.MorningHour);
            Assert.IsTrue(quests.Simulation.World.Orders.TryGetPromotion(a.Id, out Order exam));
            exam.Status = OrderStatus.OnBoard;
            for (int i = 0; i < Vocabulary.StatCount; i++) exam.SetRequirement((StatId)i, Vocabulary.IsDiagramAxis((StatId)i) ? 10f : 0f);
            QuestRun run = quests.Start(exam, a);
            List<SimEvent> events = quests.RunToEnd(run);
            events.Single(e => e.Type == SimEventType.PromotionExam).TryGet("chance", out float chance);
            Assert.AreEqual(quests.Registry.Balance.Rounds.MaxRoundChance, chance, 1e-6, "перекрытие 1 — шанс экзамена на потолке");
        }

        [Test]
        public void RealCalculation_AndViewerEstimate_GiveSameOverlap()
        {
            using var quests = new QuestWorld();
            DataRegistry data = quests.Registry;
            var random = new Random(23);
            for (int t = 0; t < 20; t++)
            {
                List<Adventurer> people = Enumerable.Range(0, 6).Select(_ =>
                {
                    Adventurer person = PartyTestSupport.Person(quests, cohesion: random.Next(0, 101));
                    foreach (StatId stat in data.Stats.RadarOrder) person.SetStat(stat, 10f + random.Next(0, 71));
                    return person;
                }).ToList();
                Order order = quests.AddOrder(requirement: 20f);
                foreach (StatId stat in data.Stats.RadarOrder) order.SetRequirement(stat, 20f + random.Next(0, 120));

                Adventurer viewer = people[0];
                var lens = new PartyLens(viewer, order, false, 0.2f, quests.Simulation.World.Relations, data);
                for (int count = 1; count <= people.Count; count++)
                {
                    List<Adventurer> group = people.Take(count).ToList();
                    Assert.AreEqual(QuestMath.RealOverlap(group, order, data), lens.Overlap(group), 1e-5,
                        "ровный человек и точное описание: оценка глазами человека = настоящее перекрытие");
                    float[] perceived = QuestMath.PerceivedRequirements(order.Profile, order.DescriptionAccuracy, viewer, data);
                    Assert.AreEqual(QuestMath.Overlap(data.Stats.RadarOrder, perceived, QuestMath.GroupProfile(group, data)), lens.Overlap(group), 1e-5,
                        "одно правило сложения");
                }

                order.DescriptionAccuracy = 0.85f;
                var skewed = new PartyLens(viewer, order, false, 0.2f, quests.Simulation.World.Relations, data);
                float[] skewedRequirements = QuestMath.PerceivedRequirements(order.Profile, order.DescriptionAccuracy, viewer, data);
                Assert.AreEqual(QuestMath.Overlap(data.Stats.RadarOrder, skewedRequirements, QuestMath.GroupProfile(people, data)), skewed.Overlap(people), 1e-5,
                    "неточное описание: та же группа, те же требования — то же перекрытие");
            }
        }

        [Test]
        public void SameSeed_SameLog_WithGroups()
        {
            using var data = new PeopleData();
            string Run()
            {
                var writer = new StringWriter();
                HeadlessRun.Run(data.Registry, 23u, 120, PlayerBots.Simple(), new SimLogger(SimLogLevel.Debug, writer));
                return writer.ToString();
            }

            string first = Run();
            string second = Run();
            StringAssert.Contains(" gathered for #", first, "в прогоне собирались группы");
            StringAssert.Contains(" round ", first, "в прогоне были раунды");
            Assert.AreEqual(first, second);
        }
    }
}
