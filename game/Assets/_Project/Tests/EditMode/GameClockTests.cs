using GuildMaster.Core;
using NUnit.Framework;

namespace GuildMaster.Tests
{
    /// <summary>Пауза и скорости (ТЗ 03 → «Скорости и пауза»).</summary>
    public sealed class GameClockTests
    {
        [Test]
        public void Speeds_ComeFromBalance()
        {
            using var data = new TestData();
            var clock = new GameClock(data.Balance.Time, debugSpeedAllowed: false);

            Assert.IsFalse(clock.Paused);
            Assert.AreEqual(1, clock.Multiplier);
            Assert.IsTrue(clock.SetSpeed(1));
            Assert.AreEqual(2, clock.Multiplier);
            Assert.IsTrue(clock.SetSpeed(2));
            Assert.AreEqual(4, clock.Multiplier);
            Assert.IsFalse(clock.SetSpeed(3));
            Assert.IsFalse(clock.SetSpeed(-1));
            Assert.AreEqual(4, clock.Multiplier);

            GameData.Edit(data.Balance, "time.speedMultipliers", list =>
            {
                list.arraySize = 3;
                list.GetArrayElementAtIndex(0).intValue = 1;
                list.GetArrayElementAtIndex(1).intValue = 3;
                list.GetArrayElementAtIndex(2).intValue = 6;
            });
            Assert.AreEqual(6, clock.Multiplier);
            clock.SetSpeed(1);
            Assert.AreEqual(3, clock.Multiplier);
        }

        [Test]
        public void DebugSpeed_UnavailableOutsideDebugBuild()
        {
            using var data = new TestData();
            var release = new GameClock(data.Balance.Time, debugSpeedAllowed: false);
            release.SetSpeed(1);

            Assert.IsFalse(release.SetDebugSpeed());
            Assert.IsFalse(release.DebugSpeed);
            Assert.AreEqual(2, release.Multiplier);
            Assert.AreEqual(2, release.TakeTicks(1f));

            var debug = new GameClock(data.Balance.Time, debugSpeedAllowed: true);
            Assert.IsTrue(debug.SetDebugSpeed());
            Assert.AreEqual(50, debug.Multiplier);
            GameData.Edit(data.Balance, "time.debugSpeedMultiplier", p => p.intValue = 30);
            Assert.AreEqual(30, debug.Multiplier);

            debug.SetSpeed(0);
            Assert.IsFalse(debug.DebugSpeed);
            Assert.AreEqual(1, debug.Multiplier);
        }

        [Test]
        public void TakeTicks_FollowsSpeed_AndStopsOnPause()
        {
            using var data = new TestData();
            var clock = new GameClock(data.Balance.Time, debugSpeedAllowed: true);

            Assert.AreEqual(0, clock.TakeTicks(0.5f));
            Assert.AreEqual(1, clock.TakeTicks(0.5f), "x1: one tick per real second");

            clock.SetSpeed(2);
            Assert.AreEqual(4, clock.TakeTicks(1f));

            clock.TogglePause();
            Assert.IsTrue(clock.Paused);
            Assert.AreEqual(0, clock.TakeTicks(10f));

            clock.TogglePause();
            Assert.AreEqual(4, clock.TakeTicks(1f), "time spent on pause is not carried over");
        }

        [Test]
        public void TakeTicks_IsCappedPerFrame()
        {
            using var data = new TestData();
            var clock = new GameClock(data.Balance.Time, debugSpeedAllowed: true);
            clock.SetDebugSpeed();

            Assert.AreEqual(10, clock.TakeTicks(1f), "x50 in a one-second frame: capped at maxTicksPerFrame");
            Assert.AreEqual(1, clock.TakeTicks(0.001f), "the tail of a stalled frame is dropped, at most one hour kept");
        }

        [Test]
        public void SetSpeed_ResumesFromPause()
        {
            using var data = new TestData();
            var clock = new GameClock(data.Balance.Time, debugSpeedAllowed: false, startPaused: true);
            int changes = 0;
            clock.Changed += () => changes++;

            Assert.IsTrue(clock.Paused);
            clock.Pause();
            Assert.AreEqual(0, changes, "pausing a paused clock changes nothing");

            clock.SetSpeed(1);
            Assert.IsFalse(clock.Paused);
            Assert.AreEqual(2, clock.Multiplier);
            Assert.AreEqual(1, changes);
        }
    }
}
