using System.Collections.Generic;
using System.Linq;
using GuildMaster.Core;
using GuildMaster.Data;
using NUnit.Framework;

namespace GuildMaster.Tests
{
    /// <summary>Эффекты черт на состояние и стресс от событий.</summary>
    public sealed class StateTraitTests
    {
        private StateWorld world;

        [SetUp]
        public void SetUp() => world = new StateWorld();

        [TearDown]
        public void TearDown() => world.Dispose();

        [TestCase(0f, 10f)]
        [TestCase(-50f, 15f)]      // трус −50: × 1,5
        [TestCase(-100f, 20f)]     // крайний трус: × 2 (↑↑)
        [TestCase(50f, 10f)]       // безрассудному стресс не быстрее
        public void Coward_GainsStressFaster(float risk, float gained)
        {
            Adventurer adventurer = world.Add();
            adventurer.SetAxis(AxisId.Risk, risk);
            adventurer.State.Stress = 50f;

            Assert.AreEqual(gained, world.Do(ctx => StateService.AddStress(ctx, adventurer, 10f)), 1e-4f);
            Assert.AreEqual(-10f, world.Do(ctx => StateService.AddStress(ctx, adventurer, -10f)), 1e-4f, "снимается как у всех");
        }

        [Test]
        public void Nightmares_FatigueGrowsThirtyPercentFaster()
        {
            Adventurer haunted = world.Add("Nightmares");
            haunted.State.Fatigue = 20f;
            haunted.State.Activity = Activity.OnQuestTravel;
            world.Do(ctx => StateService.ApplyHour(ctx, haunted, PartyContext.None));
            Assert.AreEqual(23.9f, haunted.State.Fatigue, 1e-4f);

            haunted.State.Activity = Activity.Sleeping;
            world.Do(ctx => StateService.ApplyHour(ctx, haunted, PartyContext.None));
            Assert.AreEqual(15.9f, haunted.State.Fatigue, 1e-4f, "отдыхает как все");
        }

        [Test]
        public void LonerAndTeamPlayer_StressDependsOnParty()
        {
            Adventurer loner = world.Add();
            Adventurer team = world.Add();
            loner.SetAxis(AxisId.People, -100f);
            team.SetAxis(AxisId.People, 100f);

            Assert.AreEqual(15f, world.Do(ctx => StateService.AddStress(ctx, loner, 10f, PartyContext.InGroup)), 1e-4f);
            Assert.AreEqual(10f, world.Do(ctx => StateService.AddStress(ctx, loner, 10f, PartyContext.Solo)), 1e-4f);
            Assert.AreEqual(15f, world.Do(ctx => StateService.AddStress(ctx, team, 10f, PartyContext.Solo)), 1e-4f);
            Assert.AreEqual(10f, world.Do(ctx => StateService.AddStress(ctx, team, 10f, PartyContext.None)), 1e-4f, "вне задания — нет");
        }

        [Test]
        public void RoundFailed_StressByFailureLadder()
        {
            List<Adventurer> party = world.AddMany(3);
            foreach (Adventurer member in party) member.State.Stress = 0f;

            world.Do(ctx => StressEvents.RoundFailed(ctx, party, 1));
            world.Do(ctx => StressEvents.RoundFailed(ctx, party, 2));
            Assert.IsTrue(party.All(m => m.State.Stress == 10f));
            world.Do(ctx => StressEvents.RoundFailed(ctx, party, 3));
            Assert.IsTrue(party.All(m => m.State.Stress == 18f));

            world.Do(ctx => StressEvents.ComradeWounded(ctx, party, party[0]));
            Assert.AreEqual(18f, party[0].State.Stress);
            Assert.IsTrue(party.Skip(1).All(m => m.State.Stress == 23f));
        }

        [Test]
        public void ComradeDeath_Stress_Friend_Lover_IronNerves_AndGrieving()
        {
            Adventurer deceased = world.Add();
            Adventurer stranger = world.Add();
            Adventurer friend = world.Add();
            Adventurer lover = world.Add();
            Adventurer ironFriend = world.Add("IronNerves");
            foreach (Adventurer adventurer in new[] { stranger, friend, lover, ironFriend }) adventurer.State.Stress = 0f;
            world.Do(ctx =>
            {
                RelationService.Set(ctx, friend.Id, deceased.Id, 40f);
                RelationService.Set(ctx, ironFriend.Id, deceased.Id, 60f);
                TraitService.TryAcquire(ctx, lover, "Lover", deceased.Id);
            });

            world.Do(ctx =>
            {
                AdventurerLifecycle.Retire(ctx, deceased, LeaveReason.Died);
                StressEvents.AdventurerDied(ctx, deceased);
            });

            Assert.AreEqual(25f, stranger.State.Stress, 1e-4f);
            Assert.AreEqual(40f, friend.State.Stress, 1e-4f);
            Assert.AreEqual(60f, lover.State.Stress, 1e-4f);
            Assert.AreEqual(20f, ironFriend.State.Stress, 1e-4f, "Железные нервы — × 0,5");

            Assert.IsFalse(stranger.HasTrait("Grieving"));
            foreach (Adventurer mourner in new[] { friend, lover, ironFriend })
            {
                Assert.IsTrue(mourner.TryGetTrait("Grieving", out TraitInstance grieving), mourner.Id.ToString());
                Assert.AreEqual(deceased.Id, grieving.PartnerId);
                Assert.IsFalse(grieving.Revealed);
            }
        }

        [Test]
        public void Grieving_HalvesStressRelief_ThenBreaksOrHardens_AndGoes()
        {
            var outcomes = new List<string>();
            for (int i = 0; i < 60; i++)
            {
                Adventurer adventurer = world.Add();
                world.Do(ctx => TraitService.TryAcquire(ctx, adventurer, "Grieving", 1));
                if (i == 0)
                {
                    adventurer.State.Stress = 50f;
                    Assert.AreEqual(-5f, world.Do(ctx => StateService.AddStress(ctx, adventurer, -10f)), 1e-4f, "снятие стресса × 0,5");
                }
            }
            List<Adventurer> mourners = world.Simulation.World.Adventurers.Active.ToList();

            world.Days(29);
            Assert.IsTrue(mourners.All(a => a.HasTrait("Grieving")), "спад — 30 дней");

            float composureBefore = mourners[0].GetStat(StatId.Composure);
            List<SimEvent> events = world.Collect(() => world.Days(2));
            List<SimEvent> ended = events.Where(e => e.Type == SimEventType.GrievingEnded).ToList();
            Assert.AreEqual(mourners.Count, ended.Count);
            Assert.IsTrue(mourners.All(a => !a.HasTrait("Grieving")), "черта уходит");
            Assert.AreEqual(mourners.Count, events.Count(e => e.Type == SimEventType.TraitRevealed), "раскрытие — сразу после спада");
            foreach (SimEvent end in ended)
            {
                Assert.IsTrue(end.TryGet("outcome", out string outcome));
                outcomes.Add(outcome);
                Adventurer adventurer = mourners.Single(a => a.Id == end.Participants[0]);
                if (outcome == "hardened") Assert.AreEqual(composureBefore + 5f, adventurer.GetStat(StatId.Composure), 1e-4f);
            }
            CollectionAssert.AreEquivalent(new[] { "broke", "hardened" }, outcomes.Distinct());
            Assert.That(outcomes.Count(o => o == "broke"), Is.InRange(15, 45));
        }

        [Test]
        public void Tested_AddsLoyaltyAndCohesion()
        {
            Adventurer adventurer = world.Add();
            adventurer.State.Loyalty = 55f;
            world.Do(ctx => TraitService.TryAcquire(ctx, adventurer, "Tested"));
            Assert.AreEqual(65f, adventurer.State.Loyalty, 1e-4f);
            Assert.AreEqual(35f, adventurer.GetStat(StatId.Cohesion), 1e-4f);
        }

        [Test]
        public void Drunkard_SkipsDayInTavern_TenOrTwentyPercent_RevealsTrait()
        {
            List<Adventurer> calm = world.AddMany(30, "Drunkard");
            List<Adventurer> stressed = world.AddMany(30, "Drunkard");
            int calmSkips = 0, stressedSkips = 0;
            const int days = 200;
            for (int day = 0; day < days; day++)
            {
                world.TickToHour(world.Balance.Time.MorningHour - 1);
                foreach (Adventurer adventurer in calm) adventurer.State.Stress = 50f;
                foreach (Adventurer adventurer in stressed) adventurer.State.Stress = 60f;

                world.TickToHour(world.Balance.Time.DayHour + 1);
                // Днём в таверну Пьяница ходит и по своему решению — считаем именно пропуск дня.
                long now = world.Time.TotalHours;
                calmSkips += calm.Count(a => now < a.State.SkipsDayUntilHours);
                stressedSkips += stressed.Count(a => now < a.State.SkipsDayUntilHours);
            }

            int trials = 30 * days;
            float calmTolerance = Frequency.Tolerance(trials, 0.1f);
            float stressedTolerance = Frequency.Tolerance(trials, 0.2f);
            TestContext.WriteLine($"skips: calm {calmSkips} (≈600 ± {calmTolerance:0}), stressed {stressedSkips} (≈1200 ± {stressedTolerance:0})");
            Assert.That(calmSkips, Is.EqualTo(0.1f * trials).Within(calmTolerance));
            Assert.That(stressedSkips, Is.EqualTo(0.2f * trials).Within(stressedTolerance));
            Assert.IsTrue(calm.Concat(stressed).All(a => a.Traits.Single().Revealed), "первый пропущенный день раскрывает");
        }

        [Test]
        public void TraitEffects_ActWhileHidden()
        {
            Adventurer haunted = world.Add("Nightmares");
            Assert.IsFalse(haunted.Traits.Single().Revealed);
            Assert.AreEqual(1.3f, StateRates.Multiplier(haunted, StateStat.Fatigue, RateDirection.Growth, world.Registry), 1e-4f);
        }
    }

    /// <summary>Детерминизм и скорость с системами состояния.</summary>
    public sealed class StateDeterminismTests
    {
        private PeopleData data;

        [SetUp]
        public void SetUp() => data = new PeopleData();

        [TearDown]
        public void TearDown() => data.Dispose();

        [Test]
        public void StateSystems_DoNotShiftOtherSystems()
        {
            data.NoQuests(); // задания меняют репутацию, а с ней — шанс кандидата
            string Run(bool withStateSystems)
            {
                List<ISimSystem> systems = SimulationSystems.CreateDefault();
                if (!withStateSystems) systems.RemoveAll(s => s is ActivitySystem || s is StateSystem || s is HealthSystem);
                systems.Add(new NoiseSystem("Veteran", issueIds: false));
                var simulation = new Simulation(data.Registry, 5u, systems);
                string log = SimulationLog.Record(simulation, sim => SimulationRun.Days(sim, 120));
                string[] others = { "[TimeSystem]", "[RecruitSystem]", "[AdventurerSystem]", "[Veteran]" };
                return string.Join("\n", log.Split('\n').Where(line => others.Any(line.Contains)));
            }

            string without = Run(withStateSystems: false);
            StringAssert.Contains("CandidateArrived", without);
            Assert.AreEqual(without, Run(withStateSystems: true));
        }

        [Test]
        public void SameSeed_SameStateAndWounds()
        {
            string Run()
            {
                Simulation simulation = Simulation.CreateDefault(data.Registry, 77u);
                string log = SimulationLog.Record(simulation, sim =>
                {
                    for (int day = 0; day < 120; day++)
                    {
                        SimulationRun.Days(sim, 1);
                        if (day % 10 != 0) continue;
                        // Раны и стресс «от заданий» — службами напрямую, как их вызывало бы задание.
                        SimulationRun.Do(sim, ctx =>
                        {
                            IReadOnlyList<Adventurer> active = ctx.World.Adventurers.Active;
                            HealthService.Wound(ctx, ctx.Rng.Pick(active), ctx.Rng.Chance(0.5f) ? ConditionKind.LightWound : ConditionKind.HeavyWound);
                            StressEvents.RoundFailed(ctx, active, 3);
                        });
                    }
                });
                return log + PeopleDump.Of(simulation);
            }

            string first = Run();
            StringAssert.Contains("AdventurerWounded", first);
            StringAssert.Contains("WoundHealed", first);
            Assert.AreEqual(first, Run());
        }

        [Test]
        public void HeadlessYear_ThirtyPeopleWithState_RunsUnderOneMinute()
        {
            data.Set("guild.maxAdventurers", 30);
            data.Set("guild.influxBaseChance", 1f);
            Simulation simulation = Simulation.CreateDefault(data.Registry, 360u);
            int peak = 0;
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            List<SimEvent> events = SimulationRun.Collect(simulation, sim =>
            {
                for (int day = 0; day < 360; day++)
                {
                    SimulationRun.Days(sim, 1);
                    foreach (Candidate candidate in sim.World.Adventurers.Candidates.ToList())
                        sim.Send(new AcceptCandidateCommand(candidate.Adventurer.Id));

                    // Раны «от заданий», как их вызывало бы задание: иначе без заданий состояние стоит на месте.
                    if (day % 10 == 0)
                    {
                        SimulationRun.Do(sim, ctx => HealthService.Wound(ctx, ctx.Rng.Pick(ctx.World.Adventurers.Active),
                            ctx.Rng.Chance(0.5f) ? ConditionKind.LightWound : ConditionKind.HeavyWound));
                    }

                    peak = System.Math.Max(peak, sim.World.Adventurers.Active.Count);
                    foreach (Adventurer adventurer in sim.World.Adventurers.Active) AssertStateInvariants(adventurer);
                }
            });
            stopwatch.Stop();

            Assert.That(peak, Is.GreaterThanOrEqualTo(25), "год прошёл с полной гильдией");
            Assert.That(stopwatch.Elapsed.TotalSeconds, Is.LessThan(60));
            Assert.That(events.Count(e => e.Type == SimEventType.AdventurerWounded), Is.GreaterThan(0));
            Assert.That(events.Count(e => e.Type == SimEventType.WoundHealed), Is.GreaterThan(0));
            TestContext.WriteLine($"360 days, peak {peak} people, now {simulation.World.Adventurers.Active.Count}: " +
                $"{stopwatch.Elapsed.TotalMilliseconds:0} ms, wounded {events.Count(e => e.Type == SimEventType.AdventurerWounded)}, " +
                $"healed {events.Count(e => e.Type == SimEventType.WoundHealed)}, left {events.Count(e => e.Type == SimEventType.AdventurerLeft)}");
        }

        private static void AssertStateInvariants(Adventurer adventurer)
        {
            AdventurerState s = adventurer.State;
            string who = $"#{adventurer.Id}";
            Assert.That(s.Fatigue, Is.InRange(0f, 100f), who);
            Assert.That(s.Stress, Is.InRange(0f, 100f), who);
            Assert.That(s.Contentment, Is.InRange(0f, 100f), who);
            Assert.That(s.Loyalty, Is.InRange(0f, 100f), who);
            Assert.That(s.Wallet, Is.GreaterThanOrEqualTo(0), who);
            Assert.That(s.DebtToGuild, Is.GreaterThanOrEqualTo(0), who);
            Assert.IsTrue(!s.IsWalletEmpty || s.Wallet == 0, $"{who}: флаг «кошелёк пуст» при деньгах");
            Assert.IsFalse(s.InInfirmary, $"{who}: Лазарета нет");
            Assert.That(s.Conditions.Count(c => c.Kind == ConditionKind.LightWound), Is.LessThanOrEqualTo(1), $"{who}: лёгкие не складываются");
            Assert.That(s.Conditions.Count(c => c.Kind == ConditionKind.HeavyWound), Is.LessThanOrEqualTo(1), $"{who}: вторая тяжёлая — увечье");
            Assert.IsTrue(s.Conditions.All(c => c.RemainingDays > 0f), $"{who}: зажившая рана осталась");
        }
    }
}
