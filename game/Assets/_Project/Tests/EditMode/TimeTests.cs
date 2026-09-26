using System.Collections.Generic;
using System.Linq;
using GuildMaster.Core;
using NUnit.Framework;

namespace GuildMaster.Tests
{
    public sealed class TimeTests
    {
        [Test]
        public void Calendar_RollsOverDayMonthYear()
        {
            using var data = new TestData();
            var calendar = new Calendar(data.Balance.Time);

            Assert.AreEqual("1.1.1 00:00", calendar.At(0).ToString());
            Assert.AreEqual("1.1.1 23:00", calendar.At(23).ToString());
            Assert.AreEqual("1.1.2 00:00", calendar.At(24).ToString());
            Assert.AreEqual("1.2.1 00:00", calendar.At(24 * 30).ToString());
            Assert.AreEqual("2.1.1 00:00", calendar.At(24 * 30 * 12).ToString());
            Assert.AreEqual("1.12.30 23:00", calendar.At(24 * 30 * 12 - 1).ToString());

            long total = calendar.ToTotalHours(3, 7, 15, 9);
            GameTime time = calendar.At(total);
            Assert.AreEqual((3, 7, 15, 9), (time.Year, time.Month, time.Day, time.Hour));
        }

        [Test]
        public void Simulation_StartsAtSixInTheMorningOfDayOne()
        {
            using var data = new TestData();
            var simulation = Simulation.CreateDefault(data.Registry, 1);
            Assert.AreEqual("1.1.1 06:00", simulation.World.Time.ToString());
        }

        [Test]
        public void TimeSystem_AnnouncesHourDayMonthAndPhases()
        {
            using var data = new TestData();
            var simulation = Simulation.CreateDefault(data.Registry, 1);
            var byHour = new List<(GameTime time, SimEventType type)>();
            simulation.TickCompleted += events =>
            {
                foreach (SimEvent e in events) byHour.Add((simulation.Calendar.At(e.TimeHours), e.Type));
            };

            int ticks = (int)simulation.Calendar.DaysToHours(31);
            for (int i = 0; i < ticks; i++) simulation.Tick();

            Assert.AreEqual(ticks, byHour.Count(e => e.type == SimEventType.HourStarted));
            Assert.AreEqual(31, byHour.Count(e => e.type == SimEventType.DayStarted));
            Assert.AreEqual(31, byHour.Count(e => e.type == SimEventType.MorningStarted));
            Assert.AreEqual(31, byHour.Count(e => e.type == SimEventType.EveningStarted));
            Assert.AreEqual(31, byHour.Count(e => e.type == SimEventType.NightStarted));

            Assert.IsTrue(byHour.Where(e => e.type == SimEventType.DayStarted).All(e => e.time.Hour == 0));
            Assert.IsTrue(byHour.Where(e => e.type == SimEventType.MorningStarted).All(e => e.time.Hour == 6));
            Assert.IsTrue(byHour.Where(e => e.type == SimEventType.EveningStarted).All(e => e.time.Hour == 18));
            Assert.IsTrue(byHour.Where(e => e.type == SimEventType.NightStarted).All(e => e.time.Hour == 22));

            (GameTime time, SimEventType type)[] months = byHour.Where(e => e.type == SimEventType.MonthStarted).ToArray();
            Assert.AreEqual(1, months.Length);
            Assert.AreEqual("1.2.1 00:00", months[0].time.ToString());
            Assert.IsTrue(byHour.All(e => e.time.TotalHours > simulation.Calendar.StartTotalHours));
        }
    }
}
