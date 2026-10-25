using System.Collections.Generic;
using System.IO;
using System.Linq;
using GuildMaster.Core;
using GuildMaster.Data;
using GuildMaster.Debugging;
using NUnit.Framework;

namespace GuildMaster.Tests
{
    /// <summary>Приток из поселения, молва, уровни кандидатов по репутации, подъёмные.</summary>
    public sealed class PeopleReputationTests
    {
        private PeopleData data;

        [SetUp]
        public void SetUp() => data = new PeopleData();

        [TearDown]
        public void TearDown() => data.Dispose();

        [Test]
        public void PopulationMultiplier_IsPopulationOverStart_Capped()
        {
            // Стартовое население 80, потолок × 2.
            Assert.That(RecruitSystem.PopulationMultiplier(data.Registry, 80), Is.EqualTo(1f).Within(1e-6f));
            Assert.That(RecruitSystem.PopulationMultiplier(data.Registry, 40), Is.EqualTo(0.5f).Within(1e-6f));
            Assert.That(RecruitSystem.PopulationMultiplier(data.Registry, 160), Is.EqualTo(2f).Within(1e-6f));
            Assert.That(RecruitSystem.PopulationMultiplier(data.Registry, 400), Is.EqualTo(2f).Within(1e-6f));
            Assert.AreEqual(0f, RecruitSystem.PopulationMultiplier(data.Registry, 0));
        }

        [Test]
        public void Rumor_FollowsAverageContentment_NoPeople_IsOne()
        {
            Simulation simulation = Simulation.CreateDefault(data.Registry, 1u);
            IReadOnlyList<Adventurer> active = simulation.World.Adventurers.Active;

            SetContentment(active, 50f);
            Assert.That(RecruitSystem.Rumor(data.Registry, simulation.World), Is.EqualTo(1f).Within(1e-5f));
            SetContentment(active, 20f);
            Assert.That(RecruitSystem.Rumor(data.Registry, simulation.World), Is.EqualTo(0.25f).Within(1e-5f));
            SetContentment(active, 5f);
            Assert.AreEqual(0f, RecruitSystem.Rumor(data.Registry, simulation.World), "недовольные гасят приток совсем");
            SetContentment(active, 80f);
            Assert.That(RecruitSystem.Rumor(data.Registry, simulation.World), Is.EqualTo(1.5f).Within(1e-5f), "не выше предела");

            foreach (Adventurer adventurer in active.ToList())
                SimulationRun.Do(simulation, ctx => AdventurerLifecycle.Retire(ctx, adventurer, LeaveReason.Left));
            Assert.AreEqual(1f, RecruitSystem.Rumor(data.Registry, simulation.World));
        }

        [Test]
        public void CandidateChance_FallsWithPopulationAndDiscontent_GrowsWithReputation()
        {
            Simulation simulation = Simulation.CreateDefault(data.Registry, 1u);
            WorldState world = simulation.World;
            SetContentment(world.Adventurers.Active, 50f);
            float baseChance = RecruitSystem.CandidateChance(data.Registry, world.Guild.Reputation);
            Assert.That(RecruitSystem.CandidateChance(data.Registry, world), Is.EqualTo(baseChance).Within(1e-6f),
                "население стартовое, довольство 50 — как без множителей");

            world.Settlement.Population = 40;
            Assert.That(RecruitSystem.CandidateChance(data.Registry, world), Is.EqualTo(baseChance * 0.5f).Within(1e-6f), "население вдвое меньше");

            SetContentment(world.Adventurers.Active, 20f);
            Assert.That(RecruitSystem.CandidateChance(data.Registry, world), Is.EqualTo(baseChance * 0.5f * 0.25f).Within(1e-6f), "недовольство");

            world.Guild.Reputation = 60f;
            Assert.That(RecruitSystem.CandidateChance(data.Registry, world),
                Is.EqualTo(RecruitSystem.CandidateChance(data.Registry, 60f) * 0.5f * 0.25f).Within(1e-6f));
            Assert.That(RecruitSystem.CandidateChance(data.Registry, 60f), Is.GreaterThan(baseChance), "репутация поднимает шанс");
        }

        [Test]
        public void LevelMix_ByReputationSteps()
        {
            AdventurersBalance balance = data.Balance.Adventurers;
            CollectionAssert.AreEqual(new[] { 0.65f, 0.3f, 0.05f }, AdventurerGenerator.LevelMixFor(balance, 0f).Weights);
            CollectionAssert.AreEqual(new[] { 0.65f, 0.3f, 0.05f }, AdventurerGenerator.LevelMixFor(balance, 29.9f).Weights);
            CollectionAssert.AreEqual(new[] { 0.5f, 0.35f, 0.15f }, AdventurerGenerator.LevelMixFor(balance, 30f).Weights);
            CollectionAssert.AreEqual(new[] { 0.35f, 0.4f, 0.25f }, AdventurerGenerator.LevelMixFor(balance, 100f).Weights);
        }

        [Test]
        public void CandidateLevels_StrongerAtHighReputation_NoviceAlwaysWeak()
        {
            Dictionary<AdventurerLevel, int> low = GenerateLevels(0f, out int lowNoviceNotWeak);
            Dictionary<AdventurerLevel, int> high = GenerateLevels(80f, out int highNoviceNotWeak);
            TestContext.WriteLine($"rep 0: {string.Join(", ", low)}; rep 80: {string.Join(", ", high)}");

            Assert.AreEqual(0, lowNoviceNotWeak + highNoviceNotWeak, "Новичок — всегда слабый");
            Assert.That(high[AdventurerLevel.Strong], Is.GreaterThan(low[AdventurerLevel.Strong] * 3), "сильных больше при высокой репутации");
            Assert.That(high[AdventurerLevel.Weak], Is.LessThan(low[AdventurerLevel.Weak]), "слабых меньше");
        }

        [Test]
        public void SigningBonus_ByLevel()
        {
            Assert.AreEqual(20, RecruitSystem.SigningBonus(data.Registry, AdventurerLevel.Weak));
            Assert.AreEqual(40, RecruitSystem.SigningBonus(data.Registry, AdventurerLevel.Medium));
            Assert.AreEqual(60, RecruitSystem.SigningBonus(data.Registry, AdventurerLevel.Strong));
        }

        [Test]
        public void Accept_PaysSigningBonus_FromTreasuryToWallet()
        {
            Simulation simulation = WithCandidate(out Candidate candidate);
            Adventurer newcomer = candidate.Adventurer;
            int bonus = RecruitSystem.SigningBonus(data.Registry, candidate);
            int money = simulation.World.Treasury.Money;
            int wallet = newcomer.State.Wallet;
            int ledger = simulation.World.Treasury.Ledger.Count;

            simulation.Send(new AcceptCandidateCommand(newcomer.Id));
            simulation.ApplyCommandsNow();

            Assert.IsTrue(simulation.World.Adventurers.IsActive(newcomer.Id));
            Assert.AreEqual(money - bonus, simulation.World.Treasury.Money);
            Assert.AreEqual(wallet + bonus, newcomer.State.Wallet);
            Assert.AreEqual(ledger + 1, simulation.World.Treasury.Ledger.Count);
            LedgerEntry entry = simulation.World.Treasury.Ledger[ledger];
            Assert.AreEqual(LedgerCategories.SigningBonuses, entry.Category);
            Assert.AreEqual(-bonus, entry.Amount);
            Assert.AreEqual(newcomer.Id, entry.RelatedId);
            Assert.IsTrue(simulation.Events.Events.Any(e => e.Type == SimEventType.AdventurerJoined && e.TryGet("amount", out int paid) && paid == bonus));
        }

        [Test]
        public void Accept_WithoutMoney_IsRefused_CandidateKeepsWaiting()
        {
            Simulation simulation = WithCandidate(out Candidate candidate);
            int bonus = RecruitSystem.SigningBonus(data.Registry, candidate);
            simulation.World.Treasury.Money = bonus - 1;
            int ledger = simulation.World.Treasury.Ledger.Count;
            int wallet = candidate.Adventurer.State.Wallet;

            Assert.IsFalse(RecruitSystem.CanAffordSigningBonus(data.Registry, simulation.World, candidate));
            simulation.Send(new AcceptCandidateCommand(candidate.Adventurer.Id));
            simulation.ApplyCommandsNow();

            Assert.IsFalse(simulation.World.Adventurers.IsActive(candidate.Adventurer.Id));
            Assert.IsTrue(simulation.World.Adventurers.TryGetCandidate(candidate.Adventurer.Id, out _), "кандидат ждёт дальше");
            Assert.AreEqual(bonus - 1, simulation.World.Treasury.Money);
            Assert.AreEqual(ledger, simulation.World.Treasury.Ledger.Count);
            Assert.AreEqual(wallet, candidate.Adventurer.State.Wallet);
            SimEvent refused = simulation.Events.Events.Single(e => e.Type == SimEventType.CandidateAcceptRefused);
            Assert.AreEqual(candidate.Adventurer.Id, refused.Participants[0]);
            Assert.IsTrue(refused.TryGet("reason", out string reason));
            Assert.AreEqual(AcceptCandidateCommand.RefusedForMoney, reason);
            Assert.IsFalse(simulation.Events.Events.Any(e => e.Type == SimEventType.AdventurerJoined));
        }

        [Test]
        public void SimpleBot_ShortHanded_AcceptsWithoutReserve_ThenKeepsMonthOfExpenses()
        {
            Simulation simulation = WithCandidate(out Candidate candidate);
            int bonus = RecruitSystem.SigningBonus(data.Registry, candidate);
            int people = simulation.World.Adventurers.Active.Count;
            Assert.That(people, Is.LessThan(PlayerBots.SimpleMinPeople), "стартовой гильдии людей не хватает");
            var shortHanded = new AcceptCandidatesWithReserveRule(PlayerBots.SimpleCandidateReserveMonths, PlayerBots.SimpleMinPeople);

            simulation.World.Treasury.Money = bonus - 1;
            Assert.IsEmpty(Act(simulation, shortHanded), "на подъёмные не хватает — не принимает");
            simulation.World.Treasury.Money = bonus;
            List<ICommand> sent = Act(simulation, shortHanded);
            Assert.AreEqual(1, sent.Count, "людей мало — принимает без запаса");
            Assert.AreEqual(candidate.Adventurer.Id, ((AcceptCandidateCommand)sent[0]).CandidateId);

            int reserve = BuildWithReserveRule.Reserve(simulation.World, data.Registry, PlayerBots.SimpleCandidateReserveMonths);
            Assert.That(reserve, Is.GreaterThan(0), "у стартовой гильдии есть расходы");
            var enough = new AcceptCandidatesWithReserveRule(PlayerBots.SimpleCandidateReserveMonths, people);
            simulation.World.Treasury.Money = reserve + bonus - 1;
            Assert.IsEmpty(Act(simulation, enough), "людей хватает, после подъёмных меньше месяца расходов — не принимает");
            simulation.World.Treasury.Money = reserve + bonus;
            Assert.AreEqual(1, Act(simulation, enough).Count);
        }

        [Test]
        public void Summary_HasSigningBonusesCandidateChanceAndRumor()
        {
            HeadlessRun.Result result = HeadlessRun.Run(data.Registry, 5u, 90, PlayerBots.Simple());
            RunSummary summary = result.Summary;

            double joined = Total(summary, "Пришло");
            Assert.That(joined, Is.GreaterThan(0));
            Assert.That(Total(summary, "Расход: Подъёмные"), Is.GreaterThanOrEqualTo(joined * 20).And.LessThanOrEqualTo(joined * 60));
            Assert.That(Total(summary, "Шанс кандидата"), Is.GreaterThan(0).And.LessThan(1));
            foreach (MonthRow month in summary.Months)
                Assert.That(month.Values[Column(summary, "Молва")], Is.InRange(0.5, 1.5));
        }

        [Test]
        public void SameSeed_SameLog_WithSigningBonuses()
        {
            string Run()
            {
                var writer = new StringWriter();
                HeadlessRun.Run(data.Registry, 22u, 120, PlayerBots.Simple(), new SimLogger(SimLogLevel.Debug, writer));
                return writer.ToString();
            }

            string first = Run();
            StringAssert.Contains("signingBonuses", first, "подъёмные платились");
            Assert.AreEqual(first, Run());
        }

        private Simulation WithCandidate(out Candidate candidate)
        {
            Simulation simulation = Simulation.CreateDefault(data.Registry, 99u);
            SimulationRun.TickUntil(simulation, () => simulation.World.Adventurers.Candidates.Count > 0, "кандидата");
            candidate = simulation.World.Adventurers.Candidates[0];
            return simulation;
        }

        private Dictionary<AdventurerLevel, int> GenerateLevels(float reputation, out int noviceNotWeak)
        {
            Simulation simulation = Simulation.CreateDefault(data.Registry, 7u);
            simulation.World.Guild.Reputation = reputation;
            var counts = new Dictionary<AdventurerLevel, int> { [AdventurerLevel.Weak] = 0, [AdventurerLevel.Medium] = 0, [AdventurerLevel.Strong] = 0 };
            noviceNotWeak = 0;
            var rng = new Rng(123u);
            var names = new HashSet<string>();
            for (int i = 0; i < 2000; i++)
            {
                GeneratedAdventurer generated = AdventurerGenerator.Generate(rng, data.Registry, simulation.World, 10_000 + i, atStart: false, names);
                counts[generated.Level]++;
                if (generated.Type.Kind == ArchetypeKind.Novice && generated.Level != AdventurerLevel.Weak) noviceNotWeak++;
            }
            return counts;
        }

        private static void SetContentment(IEnumerable<Adventurer> people, float value)
        {
            foreach (Adventurer adventurer in people) adventurer.State.Contentment = value;
        }

        private static List<ICommand> Act(Simulation simulation, IBotRule rule)
        {
            var sent = new List<ICommand>();
            rule.Act(new BotTurn(simulation, sent.Add));
            return sent;
        }

        private static int Column(RunSummary summary, string name)
        {
            for (int i = 0; i < summary.MonthColumns.Count; i++)
            {
                if (summary.MonthColumns[i].Name == name) return i;
            }
            Assert.Fail($"нет столбца «{name}»");
            return -1;
        }

        private static double Total(RunSummary summary, string name) => summary.Totals[Column(summary, name)];
    }
}
