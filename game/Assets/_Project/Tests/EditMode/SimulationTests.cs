using System;
using System.Collections.Generic;
using GuildMaster.Core;
using NUnit.Framework;

namespace GuildMaster.Tests
{
    public sealed class SimulationTests
    {
        [Test]
        public void Tick_RunsWithoutScene()
        {
            using var data = new TestData();
            var simulation = Simulation.CreateDefault(data.Registry, 123);

            simulation.Tick();

            Assert.AreEqual(1, simulation.TicksDone);
            Assert.AreEqual("1.1.1 07:00", simulation.World.Time.ToString());
        }

        [Test]
        public void Systems_RunInGivenOrder_WithOwnStreamAndSource()
        {
            using var data = new TestData();
            var order = new List<string>();
            var simulation = new Simulation(data.Registry, 1, new ISimSystem[]
            {
                new CommandSystem(),
                new TimeSystem(),
                new ProbeSystem("First", order),
                new ProbeSystem("Second", order),
            });

            simulation.Tick();

            CollectionAssert.AreEqual(new[] { "First", "Second" }, order);
            Assert.AreSame(simulation.Rng.Stream("Second"), ProbeSystem.LastRng);
        }

        [Test]
        public void DuplicateSystemNames_AreRejected()
        {
            using var data = new TestData();
            Assert.Throws<ArgumentException>(() =>
                new Simulation(data.Registry, 1, new ISimSystem[] { new TimeSystem(), new TimeSystem() }));
        }

        [Test]
        public void Commands_ApplyAtStartOfNextTick_BeforeTimeMoves()
        {
            using var data = new TestData();
            var simulation = Simulation.CreateDefault(data.Registry, 1);
            string log = SimulationLog.Record(simulation, sim =>
            {
                sim.Send(new TraceCommand("accept"));
                Assert.AreEqual(1, sim.Commands.Count);
                Assert.AreEqual(0, sim.World.Ids.LastIssued, "command must not apply before the tick");
                sim.Tick();
            });

            Assert.AreEqual(0, simulation.Commands.Count);
            StringAssert.StartsWith("[1.1.1 06:00] [CommandSystem] [Important] Debug ids=1 command=accept", log);
        }

        [Test]
        public void ApplyCommandsNow_AppliesImmediately_AndLogsWithNextTick()
        {
            using var data = new TestData();
            var simulation = Simulation.CreateDefault(data.Registry, 1);
            int changes = 0;
            simulation.StateChanged += () => changes++;

            simulation.Send(new TraceCommand("paused"));
            simulation.ApplyCommandsNow();

            Assert.AreEqual(1, simulation.World.Ids.LastIssued);
            Assert.AreEqual(1, changes);
            Assert.AreEqual(1, simulation.Events.Events.Count);

            string log = SimulationLog.Record(simulation, sim => sim.Tick());
            StringAssert.StartsWith("[1.1.1 06:00] [CommandSystem] [Important] Debug ids=1 command=paused", log);
            Assert.AreEqual(0, simulation.Events.Events.Count, "events are cleared after the tick");
        }

        [Test]
        public void PauseRequest_IsRaisedAndConsumed()
        {
            using var data = new TestData();
            var simulation = new Simulation(data.Registry, 1, new ISimSystem[] { new TimeSystem(), new PauseAtHourSystem(9) });

            int ticks = 0;
            while (!simulation.PauseRequested && ticks < 48)
            {
                simulation.Tick();
                ticks++;
            }

            Assert.AreEqual(3, ticks);
            Assert.IsTrue(simulation.ConsumePauseRequest());
            Assert.IsFalse(simulation.PauseRequested);
        }

        [Test]
        public void Tick_InsideTickCompleted_IsRejected()
        {
            using var data = new TestData();
            var simulation = Simulation.CreateDefault(data.Registry, 1);
            simulation.TickCompleted += _ => simulation.Tick();
            Assert.Throws<InvalidOperationException>(() => simulation.Tick());
        }

        private sealed class ProbeSystem : ISimSystem
        {
            private readonly List<string> order;

            public ProbeSystem(string name, List<string> order)
            {
                Name = name;
                this.order = order;
            }

            public static Rng LastRng { get; private set; }

            public string Name { get; }

            public void Tick(SimContext ctx)
            {
                Assert.AreEqual(Name, ctx.CurrentSystem);
                Assert.AreEqual(Name, ctx.Events.Publish(SimEventType.Debug).Source);
                order.Add(Name);
                LastRng = ctx.Rng;
            }
        }

        private sealed class PauseAtHourSystem : ISimSystem
        {
            private readonly int hour;

            public PauseAtHourSystem(int hour)
            {
                this.hour = hour;
            }

            public string Name => nameof(PauseAtHourSystem);

            public void Tick(SimContext ctx)
            {
                if (ctx.World.Time.Hour == hour) ctx.RequestPause();
            }
        }
    }
}
