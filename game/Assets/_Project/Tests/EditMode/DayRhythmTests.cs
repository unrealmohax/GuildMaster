using System;
using System.Collections.Generic;
using System.Linq;
using GuildMaster.Core;
using NUnit.Framework;

namespace GuildMaster.Tests
{
    /// <summary>Фазы дня, ночлег в пути и время выхода на задание (ТЗ 03 → «Ритм дня»).</summary>
    public sealed class DayRhythmTests
    {
        [Test]
        public void Phase_FollowsBalanceHours()
        {
            using var data = new TestData();
            var simulation = Simulation.CreateDefault(data.Registry, 1);
            DayRhythm rhythm = simulation.Rhythm;

            AssertPhases(rhythm, (0, DayPhase.Night), (5, DayPhase.Night), (6, DayPhase.Morning), (8, DayPhase.Morning),
                (9, DayPhase.Day), (17, DayPhase.Day), (18, DayPhase.Evening), (21, DayPhase.Evening),
                (22, DayPhase.Night), (23, DayPhase.Night));
            Assert.AreEqual(DayPhase.Morning, rhythm.PhaseAt(simulation.World.Time), "game starts in the morning");
        }

        [Test]
        public void Phase_FollowsChangedNumbers_WithoutRestart()
        {
            using var data = new TestData();
            var simulation = Simulation.CreateDefault(data.Registry, 1);

            GameData.Edit(data.Balance, "time.morningHour", p => p.intValue = 5);
            GameData.Edit(data.Balance, "time.dayHour", p => p.intValue = 10);
            GameData.Edit(data.Balance, "time.eveningHour", p => p.intValue = 19);
            GameData.Edit(data.Balance, "time.nightHour", p => p.intValue = 23);

            AssertPhases(simulation.Rhythm, (4, DayPhase.Night), (5, DayPhase.Morning), (9, DayPhase.Morning),
                (10, DayPhase.Day), (18, DayPhase.Day), (19, DayPhase.Evening), (22, DayPhase.Evening), (23, DayPhase.Night));

            var announced = new List<(int hour, SimEventType type)>();
            simulation.TickCompleted += events =>
            {
                foreach (SimEvent e in events) announced.Add((simulation.Calendar.At(e.TimeHours).Hour, e.Type));
            };
            for (int i = 0; i < 48; i++) simulation.Tick();

            CollectionAssert.AreEquivalent(new[] { 5, 5 }, HoursOf(announced, SimEventType.MorningStarted));
            CollectionAssert.AreEquivalent(new[] { 10, 10 }, HoursOf(announced, SimEventType.DaytimeStarted));
            CollectionAssert.AreEquivalent(new[] { 19, 19 }, HoursOf(announced, SimEventType.EveningStarted));
            CollectionAssert.AreEquivalent(new[] { 23, 23 }, HoursOf(announced, SimEventType.NightStarted));
        }

        [Test]
        public void Rhythm_RejectsPhasesOutOfOrder()
        {
            using var data = new TestData();
            GameData.Edit(data.Balance, "time.dayHour", p => p.intValue = 19);
            Assert.Throws<ArgumentException>(() => Simulation.CreateDefault(data.Registry, 1));
        }

        [Test]
        public void Camp_FromCampHourUntilMorning()
        {
            using var data = new TestData();
            DayRhythm rhythm = Simulation.CreateDefault(data.Registry, 1).Rhythm;

            Assert.IsFalse(rhythm.IsCampHour(19));
            Assert.IsTrue(rhythm.IsCampHour(20));
            Assert.IsTrue(rhythm.IsCampHour(23));
            Assert.IsTrue(rhythm.IsCampHour(0));
            Assert.IsTrue(rhythm.IsCampHour(5));
            Assert.IsFalse(rhythm.IsCampHour(6));
            Assert.IsFalse(rhythm.IsCampHour(12));

            GameData.Edit(data.Balance, "time.campHour", p => p.intValue = 21);
            Assert.IsFalse(rhythm.IsCampHour(20));
            Assert.IsTrue(rhythm.IsCampHour(21));
        }

        [Test]
        public void CampEnd_And_MarchEnd_SkipTheNight()
        {
            using var data = new TestData();
            var simulation = Simulation.CreateDefault(data.Registry, 1);
            DayRhythm rhythm = simulation.Rhythm;
            Calendar calendar = simulation.Calendar;
            long At(int day, int hour) => calendar.ToTotalHours(1, 1, day, hour);

            Assert.AreEqual(At(1, 19), rhythm.CampEnd(At(1, 19)));
            Assert.AreEqual(At(2, 6), rhythm.CampEnd(At(1, 20)));
            Assert.AreEqual(At(2, 6), rhythm.CampEnd(At(2, 0)));
            Assert.AreEqual(At(2, 6), rhythm.CampEnd(At(2, 5)));
            Assert.AreEqual(At(2, 6), rhythm.CampEnd(At(2, 6)));

            Assert.AreEqual(At(1, 9), rhythm.MarchEnd(At(1, 6), 3));
            Assert.AreEqual(At(1, 20), rhythm.MarchEnd(At(1, 19), 1), "the last hour before camp still counts");
            Assert.AreEqual(At(2, 7), rhythm.MarchEnd(At(1, 19), 2));
            Assert.AreEqual(At(2, 9), rhythm.MarchEnd(At(1, 22), 3), "a march started at night waits for the morning");
            Assert.AreEqual(At(1, 22), rhythm.MarchEnd(At(1, 22), 0));
            Assert.AreEqual(At(2, 16), rhythm.MarchEnd(At(1, 6), 24), "14 daytime hours a day: 24 hours of road take two days");
            Assert.Throws<ArgumentOutOfRangeException>(() => rhythm.MarchEnd(At(1, 6), -1));
        }

        [Test]
        public void QuestStart_FromMorningUntilLatestDepartureHour()
        {
            using var data = new TestData();
            var simulation = Simulation.CreateDefault(data.Registry, 1);
            DayRhythm rhythm = simulation.Rhythm;
            Calendar calendar = simulation.Calendar;
            long At(int day, int hour) => calendar.ToTotalHours(1, 1, day, hour);

            Assert.IsFalse(rhythm.CanStartQuest(5));
            Assert.IsTrue(rhythm.CanStartQuest(6));
            Assert.IsTrue(rhythm.CanStartQuest(14));
            Assert.IsFalse(rhythm.CanStartQuest(15));
            Assert.IsFalse(rhythm.CanStartQuest(22));

            Assert.AreEqual(At(1, 6), rhythm.NextQuestStart(At(1, 3)));
            Assert.AreEqual(At(1, 6), rhythm.NextQuestStart(At(1, 6)));
            Assert.AreEqual(At(1, 14), rhythm.NextQuestStart(At(1, 14)));
            Assert.AreEqual(At(2, 6), rhythm.NextQuestStart(At(1, 15)));
            Assert.AreEqual(At(2, 6), rhythm.NextQuestStart(At(1, 23)));

            GameData.Edit(data.Balance, "time.latestDepartureHour", p => p.intValue = 12);
            Assert.IsTrue(rhythm.CanStartQuest(12));
            Assert.IsFalse(rhythm.CanStartQuest(13));
        }

        private static void AssertPhases(DayRhythm rhythm, params (int hour, DayPhase phase)[] expected)
        {
            foreach ((int hour, DayPhase phase) in expected)
            {
                Assert.AreEqual(phase, rhythm.PhaseAt(hour), $"{hour}:00");
            }
        }

        private static int[] HoursOf(List<(int hour, SimEventType type)> events, SimEventType type) =>
            events.Where(e => e.type == type).Select(e => e.hour).ToArray();
    }
}
