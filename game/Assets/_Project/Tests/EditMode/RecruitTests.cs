using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using GuildMaster.Core;
using GuildMaster.Data;
using NUnit.Framework;

namespace GuildMaster.Tests
{
    /// <summary>Кандидаты в авантюристы.</summary>
    public sealed class RecruitTests
    {
        private PeopleData data;

        [SetUp]
        public void SetUp() => data = new PeopleData();

        [TearDown]
        public void TearDown() => data.Dispose();

        [Test]
        public void CandidateChance_IsBasePlusStartReputation()
        {
            // Репутация — стартовая (5): 3% + 5 × 0,3% = 4,5%.
            Assert.That(RecruitSystem.CandidateChance(data.Registry), Is.EqualTo(0.045f).Within(1e-6f));
        }

        [Test]
        public void Candidates_ArriveWithExpectedFrequency_OnceADayInTheMorning()
        {
            const int days = 3600;
            const int seeds = 5;
            int total = 0;
            for (uint seed = 1; seed <= seeds; seed++)
            {
                Simulation simulation = Simulation.CreateDefault(data.Registry, seed);
                List<SimEvent> arrived = SimulationRun.Collect(simulation, sim => SimulationRun.Days(sim, days))
                    .Where(e => e.Type == SimEventType.CandidateArrived).ToList();
                total += arrived.Count;

                Assert.IsTrue(arrived.All(e => simulation.Calendar.At(e.TimeHours).Hour == data.Balance.Time.MorningHour));
                Assert.AreEqual(arrived.Count, arrived.Select(e => e.TimeHours / simulation.Calendar.HoursPerDay).Distinct().Count(),
                    "не больше одного в сутки");
            }

            float expected = RecruitSystem.CandidateChance(data.Registry) * days * seeds;
            TestContext.WriteLine($"{total} candidates in {days * seeds} days, expected {expected:0}");
            Assert.That(total, Is.EqualTo(expected).Within(expected * 0.1f));
        }

        [Test]
        public void UnansweredCandidate_LeavesAfterWaitDays()
        {
            Simulation simulation = Simulation.CreateDefault(data.Registry, 99u);
            List<SimEvent> events = SimulationRun.Collect(simulation, sim => SimulationRun.Days(sim, 400));

            List<SimEvent> arrived = events.Where(e => e.Type == SimEventType.CandidateArrived).ToList();
            Assert.IsNotEmpty(arrived);
            long wait = simulation.Calendar.DaysToHours(data.Balance.Adventurers.CandidateWaitDays);
            foreach (SimEvent arrival in arrived.Where(e => e.TimeHours + wait <= simulation.World.Time.TotalHours))
            {
                int id = arrival.Participants[0];
                SimEvent left = events.Single(e => e.Type == SimEventType.CandidateLeft && e.Participants[0] == id);
                Assert.AreEqual(arrival.TimeHours + wait, left.TimeHours);
            }
            Assert.AreEqual(6, simulation.World.Adventurers.Active.Count, "без ответа никто не вступает");
            Assert.IsTrue(simulation.World.Adventurers.Candidates.All(c => c.ExpiresAtHours > simulation.World.Time.TotalHours));
        }

        [Test]
        public void GuildLimit_CountsWaitingCandidates()
        {
            data.Set("guild.maxAdventurers", 8);
            Simulation simulation = Simulation.CreateDefault(data.Registry, 3u);
            int maxHead = 0;

            for (int day = 0; day < 1500; day++)
            {
                for (int hour = 0; hour < simulation.Calendar.HoursPerDay; hour++)
                {
                    simulation.Tick();
                    AdventurerRoster roster = simulation.World.Adventurers;
                    maxHead = System.Math.Max(maxHead, roster.HeadCount);
                    Assert.That(roster.HeadCount, Is.LessThanOrEqualTo(8));
                }
                // Раз в 5 дней принимаем всех ожидающих.
                if (day % 5 != 0) continue;
                foreach (Candidate candidate in simulation.World.Adventurers.Candidates.ToList())
                    simulation.Send(new AcceptCandidateCommand(candidate.Adventurer.Id));
                simulation.ApplyCommandsNow();
            }

            Assert.AreEqual(8, simulation.World.Adventurers.Active.Count);
            Assert.AreEqual(8, maxHead);
        }

        [Test]
        public void AcceptAndReject_WorkOnPause()
        {
            Simulation simulation = Simulation.CreateDefault(data.Registry, 99u);
            while (simulation.World.Adventurers.Candidates.Count < 2) simulation.Tick();

            Candidate accepted = simulation.World.Adventurers.Candidates[0];
            Candidate rejected = simulation.World.Adventurers.Candidates[1];
            long now = simulation.World.Time.TotalHours;

            simulation.Send(new AcceptCandidateCommand(accepted.Adventurer.Id));
            simulation.Send(new RejectCandidateCommand(rejected.Adventurer.Id));
            simulation.Send(new RejectCandidateCommand(12345));
            simulation.ApplyCommandsNow();

            AdventurerRoster roster = simulation.World.Adventurers;
            Assert.IsTrue(roster.IsActive(accepted.Adventurer.Id), "принят сразу, на паузе");
            Assert.AreEqual(now, accepted.Adventurer.JoinedAtHours);
            Assert.IsFalse(roster.TryGetCandidate(accepted.Adventurer.Id, out _));
            Assert.IsFalse(roster.TryGetCandidate(rejected.Adventurer.Id, out _));
            Assert.IsFalse(roster.IsActive(rejected.Adventurer.Id));

            List<SimEvent> events = SimulationRun.Collect(simulation, sim => sim.Tick());
            Assert.AreEqual(1, events.Count(e => e.Type == SimEventType.AdventurerJoined && e.Participants[0] == accepted.Adventurer.Id));
            Assert.AreEqual(1, events.Count(e => e.Type == SimEventType.CandidateRejected && e.Participants[0] == rejected.Adventurer.Id));
            Assert.IsFalse(simulation.PauseRequested, "кандидаты — без автопаузы");
        }

        [Test]
        public void AcceptedCandidate_WithPartnerTrait_LinksPartner()
        {
            Simulation simulation = Simulation.CreateDefault(data.Registry, 99u);
            while (simulation.World.Adventurers.Candidates.Count == 0) simulation.Tick();
            Candidate candidate = simulation.World.Adventurers.Candidates[0];
            Adventurer newcomer = candidate.Adventurer;

            Adventurer partner = simulation.World.Adventurers.Active.First(a => a.Traits.Count == 0);
            foreach (TraitInstance trait in newcomer.Traits.ToList()) newcomer.RemoveTrait(trait);
            TraitService.AddAtGeneration(newcomer, data.Registry.Get<SpecialTraitDefinition>("Lover"), 0, partner.Id);

            simulation.Send(new AcceptCandidateCommand(newcomer.Id));
            simulation.ApplyCommandsNow();

            Assert.IsTrue(partner.TryGetTrait("Lover", out TraitInstance back));
            Assert.AreEqual(newcomer.Id, back.PartnerId);
            Assert.IsFalse(back.Revealed);
        }

        [Test]
        public void AcceptedCandidate_PartnerGone_DropsPartnerTrait()
        {
            Simulation simulation = Simulation.CreateDefault(data.Registry, 99u);
            while (simulation.World.Adventurers.Candidates.Count == 0) simulation.Tick();
            Adventurer newcomer = simulation.World.Adventurers.Candidates[0].Adventurer;

            Adventurer partner = simulation.World.Adventurers.Active.First(a => a.Traits.Count == 0);
            foreach (TraitInstance trait in newcomer.Traits.ToList()) newcomer.RemoveTrait(trait);
            TraitService.AddAtGeneration(newcomer, data.Registry.Get<SpecialTraitDefinition>("Rival"), 0, partner.Id);
            SimulationRun.Do(simulation, ctx => AdventurerLifecycle.Retire(ctx, partner, LeaveReason.Left));

            simulation.Send(new AcceptCandidateCommand(newcomer.Id));
            simulation.ApplyCommandsNow();

            Assert.IsFalse(newcomer.HasTrait("Rival"));
            Assert.IsTrue(simulation.World.Adventurers.IsActive(newcomer.Id));
            Assert.IsTrue(simulation.World.Adventurers.TryGetKnown(partner.Id, out Adventurer archived));
            Assert.AreEqual(LeaveReason.Left, archived.LeaveReason);
            CollectionAssert.Contains(simulation.World.Adventurers.Archive, partner);
        }
    }

    /// <summary>Детерминизм и скорость с людьми.</summary>
    public sealed class PeopleDeterminismTests
    {
        private PeopleData data;

        [SetUp]
        public void SetUp() => data = new PeopleData();

        [TearDown]
        public void TearDown() => data.Dispose();

        [Test]
        public void SameSeed_SameCommands_SameWorld()
        {
            (string log, string people) first = Run(2026u);
            (string log, string people) second = Run(2026u);

            Assert.AreEqual(first.log, second.log);
            Assert.AreEqual(first.people, second.people);
            StringAssert.Contains("CandidateArrived", first.log);
            StringAssert.Contains("AdventurerJoined", first.log);
        }

        [Test]
        public void AdventurerSystems_DoNotShiftOtherSystems()
        {
            string Run(bool withPeopleSystems)
            {
                List<ISimSystem> systems = withPeopleSystems
                    ? SimulationSystems.CreateDefault()
                    : new List<ISimSystem> { new CommandSystem(), new TimeSystem(), new AutopauseSystem() };
                systems.Add(new NoiseSystem("Veteran", issueIds: false));
                var simulation = new Simulation(data.Registry, 5u, systems);
                string log = SimulationLog.Record(simulation, sim => SimulationRun.Days(sim, 120));
                return string.Join("\n", log.Split('\n').Where(line => line.Contains("[Veteran]")));
            }

            string without = Run(withPeopleSystems: false);
            Assert.IsNotEmpty(without);
            Assert.AreEqual(without, Run(withPeopleSystems: true));
        }

        [Test]
        public void HeadlessYear_WithPeople_RunsUnderOneMinute()
        {
            Simulation simulation = Simulation.CreateDefault(data.Registry, 360u);
            Stopwatch stopwatch = Stopwatch.StartNew();
            for (int day = 0; day < 360; day++)
            {
                SimulationRun.Days(simulation, 1);
                foreach (Candidate candidate in simulation.World.Adventurers.Candidates.ToList())
                    simulation.Send(new AcceptCandidateCommand(candidate.Adventurer.Id));
            }
            stopwatch.Stop();

            Assert.AreEqual("2.1.1 06:00", simulation.World.Time.ToString());
            Assert.That(simulation.World.Adventurers.Active.Count, Is.GreaterThan(6));
            Assert.That(stopwatch.Elapsed.TotalSeconds, Is.LessThan(60));
            TestContext.WriteLine($"360 days with {simulation.World.Adventurers.Active.Count} people: {stopwatch.Elapsed.TotalMilliseconds:0} ms");
        }

        private (string log, string people) Run(uint seed)
        {
            Simulation simulation = Simulation.CreateDefault(data.Registry, seed);
            string log = SimulationLog.Record(simulation, sim =>
            {
                for (int day = 0; day < 180; day++)
                {
                    SimulationRun.Days(sim, 1);
                    foreach (Candidate candidate in sim.World.Adventurers.Candidates.ToList())
                        sim.Send(new AcceptCandidateCommand(candidate.Adventurer.Id));
                }
            });
            return (log, PeopleDump.Of(simulation));
        }
    }
}
