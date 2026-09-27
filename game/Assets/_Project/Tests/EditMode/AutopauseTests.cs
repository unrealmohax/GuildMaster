using System;
using System.Collections.Generic;
using GuildMaster.Core;
using NUnit.Framework;

namespace GuildMaster.Tests
{
    /// <summary>
    /// Автопауза. Механизм проверяется на отладочном событии,
    /// которое тест сопоставляет с видом автопаузы.
    /// </summary>
    public sealed class AutopauseTests
    {
        private static readonly Dictionary<SimEventType, AutopauseKind> TestRules = new Dictionary<SimEventType, AutopauseKind>
        {
            { SimEventType.Debug, AutopauseKind.AdventurerDied },
        };

        [Test]
        public void AllKinds_EnabledByDefault()
        {
            var world = new WorldState();
            foreach (AutopauseKind kind in Enum.GetValues(typeof(AutopauseKind)))
            {
                Assert.IsTrue(world.Autopause.IsEnabled(kind), kind.ToString());
            }
            Assert.IsEmpty(world.Autopause.Triggers);
        }

        [Test]
        public void EventFromList_PausesAtEndOfTick_AndRemembersTrigger()
        {
            using var data = new TestData();
            Simulation simulation = CreateWithEmitter(data, hour: 9);

            int ticks = TickUntilPause(simulation, maxTicks: 48);

            Assert.AreEqual(3, ticks, "06:00 + 3 ticks = 09:00");
            Assert.IsTrue(simulation.ConsumePauseRequest());
            IReadOnlyList<AutopauseTrigger> triggers = simulation.World.Autopause.Triggers;
            Assert.AreEqual(1, triggers.Count);
            Assert.AreEqual(AutopauseKind.AdventurerDied, triggers[0].Kind);
            Assert.AreEqual(SimEventType.Debug, triggers[0].Event.Type);
            Assert.AreEqual(nameof(EmitterSystem), triggers[0].Event.Source);
            Assert.AreEqual("1.1.1 09:00", simulation.Calendar.At(triggers[0].Event.TimeHours).ToString());

            simulation.Tick();
            Assert.IsFalse(simulation.PauseRequested);
            Assert.IsEmpty(simulation.World.Autopause.Triggers, "trigger is kept only until the next tick");
        }

        [Test]
        public void DisabledByCommand_DoesNotPause_AndCanBeEnabledAgain()
        {
            using var data = new TestData();
            Simulation simulation = CreateWithEmitter(data, hour: 9);

            simulation.Send(new SetAutopauseCommand(AutopauseKind.AdventurerDied, false));
            simulation.ApplyCommandsNow();
            Assert.IsFalse(simulation.World.Autopause.IsEnabled(AutopauseKind.AdventurerDied));
            Assert.IsTrue(simulation.World.Autopause.IsEnabled(AutopauseKind.AdventurerFled), "other kinds stay on");

            Assert.AreEqual(-1, TickUntilPause(simulation, maxTicks: 48));
            Assert.IsEmpty(simulation.World.Autopause.Triggers);

            simulation.Send(new SetAutopauseCommand(AutopauseKind.AdventurerDied, true));
            Assert.AreEqual(3, TickUntilPause(simulation, maxTicks: 48), "the command applies at the start of the next tick");
        }

        [Test]
        public void EventNotInList_DoesNotPause()
        {
            using var data = new TestData();
            var simulation = new Simulation(data.Registry, 1, new ISimSystem[]
            {
                new CommandSystem(),
                new TimeSystem(),
                new EmitterSystem(9),
                new AutopauseSystem(new Dictionary<SimEventType, AutopauseKind>()),
            });

            Assert.AreEqual(-1, TickUntilPause(simulation, maxTicks: 48));
        }

        [Test]
        public void SetAutopauseCommand_LogsOnlyRealChanges()
        {
            using var data = new TestData();
            var simulation = Simulation.CreateDefault(data.Registry, 1);

            string log = SimulationLog.Record(simulation, sim =>
            {
                sim.Send(new SetAutopauseCommand(AutopauseKind.TraitRevealed, true));
                sim.Send(new SetAutopauseCommand(AutopauseKind.TraitRevealed, false));
                sim.Send(new SetAutopauseCommand(AutopauseKind.TraitRevealed, false));
                sim.Tick();
            });

            StringAssert.StartsWith("[1.1.1 06:00] [CommandSystem] [Normal] AutopauseChanged kind=TraitRevealed enabled=False\n", log);
            Assert.AreEqual(1, log.Split(new[] { "AutopauseChanged" }, StringSplitOptions.None).Length - 1);
        }

        private static Simulation CreateWithEmitter(TestData data, int hour) =>
            new Simulation(data.Registry, 1, new ISimSystem[]
            {
                new CommandSystem(),
                new TimeSystem(),
                new EmitterSystem(hour),
                new AutopauseSystem(TestRules),
            });

        /// <summary>Сколько тактов до автопаузы; -1 — не было.</summary>
        private static int TickUntilPause(Simulation simulation, int maxTicks)
        {
            for (int i = 1; i <= maxTicks; i++)
            {
                simulation.Tick();
                if (simulation.PauseRequested) return i;
            }
            return -1;
        }

        /// <summary>Публикует важное отладочное событие в заданный час.</summary>
        private sealed class EmitterSystem : ISimSystem
        {
            private readonly int hour;

            public EmitterSystem(int hour)
            {
                this.hour = hour;
            }

            public string Name => nameof(EmitterSystem);

            public void Tick(SimContext ctx)
            {
                if (ctx.World.Time.Hour == hour) ctx.Events.Publish(SimEventType.Debug, EventImportance.Important);
            }
        }
    }
}
