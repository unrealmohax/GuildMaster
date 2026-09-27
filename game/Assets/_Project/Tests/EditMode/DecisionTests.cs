using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using GuildMaster.Core;
using GuildMaster.Data;
using GuildMaster.Debugging;
using NUnit.Framework;

namespace GuildMaster.Tests
{
    /// <summary>Точки решения, выбор, причины в логе, решение действует со следующего часа.</summary>
    public sealed class DecisionTests
    {
        private StateWorld world;

        [TearDown]
        public void TearDown() => world?.Dispose();

        [Test]
        public void FreePeople_DecideAtEachPoint_DecisionAndReasonsInLog()
        {
            var writer = new StringWriter();
            world = new StateWorld(log: new SimLogger(SimLogLevel.Trace, writer));
            world.TickToHour(10);
            List<Adventurer> people = world.AddMany(3); // вступили днём
            world.TickToHour(world.Balance.Time.NightHour + 1);
            world.Days(1);

            string log = writer.ToString();
            foreach (Adventurer adventurer in people)
            {
                string who = $"#{adventurer.Id} ";
                Assert.AreEqual(1, Count(log, $@"\[Info\] decide {who}Тест freed: "), "вступил — сразу решает");
                Assert.AreEqual(1, Count(log, $@"\[Info\] decide {who}Тест morning: (Rest|Tavern) "), "утро — раз в сутки");
                Assert.AreEqual(2, Count(log, $@"\[Info\] decide {who}Тест evening: (Rest|Tavern) [\d.]+ \((best|second); \w+ [\d.]+\) because \w+ [\d.]+"));
                Assert.AreEqual(2, Count(log, $@"\[Debug\] decide {who}Тест night: Sleep \(no choice\)"));
                Assert.That(Count(log, $@"\[Trace\] scores {who}Тест evening Tavern=[\d.]+: Money "), Is.EqualTo(2));
            }
            StringAssert.Contains("[Debug] ban #", log, "утром таверна запрещена — запрет с причиной в логе");
            StringAssert.Contains("tavern in daytime", log);
        }

        [Test]
        public void Decision_TakesEffectNextHour_ActivitySystemKeepsIt()
        {
            world = new StateWorld();
            world.Data.Set("decisions.bestChoiceChance", 1f);
            Adventurer adventurer = world.Add();

            world.TickToHour(world.Balance.Time.EveningHour);
            Assert.AreEqual(Activity.Resting, adventurer.State.Activity, "за час решения — прежнее занятие");
            Assert.AreEqual(Activity.Tavern, adventurer.State.PlannedActivity);

            for (int hour = world.Balance.Time.EveningHour + 1; hour < world.Balance.Time.NightHour; hour++)
            {
                world.Simulation.Tick();
                Assert.AreEqual(Activity.Tavern, adventurer.State.Activity, $"{hour}:00");
                Assert.IsNull(adventurer.State.PlannedActivity);
            }
            world.Simulation.Tick();
            Assert.AreEqual(Activity.Sleeping, adventurer.State.Activity);
        }

        [Test]
        public void Collapse_NoDecisions_ThenFreed()
        {
            var writer = new StringWriter();
            world = new StateWorld(log: new SimLogger(SimLogLevel.Info, writer));
            Adventurer adventurer = world.Add();
            world.TickToHour(10);
            world.Do(ctx => StateService.StartBreakdown(ctx, adventurer, BreakdownKind.Collapse));
            long endsAt = adventurer.State.BreakdownEndsAtHours;
            int before = Count(writer.ToString(), "decide ");

            while (world.Time.TotalHours < endsAt - 1) world.Simulation.Tick();
            Assert.AreEqual(before, Count(writer.ToString(), "decide "), "в срыве решений нет");

            world.Simulation.Tick();
            StringAssert.Contains($"decide #{adventurer.Id} Тест freed: ", writer.ToString());
        }

        [Test]
        public void BestChoice_EightyPercent_ElseSecond()
        {
            world = new StateWorld();
            List<Adventurer> people = world.AddMany(40);
            int tavern = 0;
            const int days = 30;
            for (int day = 0; day < days; day++)
            {
                world.TickToHour(world.Balance.Time.EveningHour);
                tavern += people.Count(a => a.State.PlannedActivity == Activity.Tavern);
            }

            int trials = people.Count * days;
            TestContext.WriteLine($"tavern {tavern}/{trials}");
            Assert.That(tavern, Is.EqualTo(0.8f * trials).Within(Frequency.Tolerance(trials, 0.8f)));
        }

        private static int Count(string text, string pattern) => Regex.Matches(text, pattern).Count;
    }

    /// <summary>Черты и состояние сдвигают выбор; частоты — с допуском 3σ.</summary>
    public sealed class DecisionMotiveTests
    {
        private const int Group = 40;
        private const int Days = 30;

        private StateWorld world;

        [TearDown]
        public void TearDown() => world?.Dispose();

        /// <summary>
        /// Усталые (80) вечером: ровному отдых ценнее таверны — в таверну ~20%; Командному (Товарищи × 2) и при стрессе 70
        /// (Утешение × 2) таверна ценнее — ~80%.
        /// </summary>
        [Test]
        public void TiredEvening_StressAndTeamPlayers_ChooseTavernMoreOften()
        {
            var plain = new List<Adventurer>();
            var team = new List<Adventurer>();
            var stressed = new List<Adventurer>();
            world = new StateWorld(beforeState: new LambdaSystem("Hold", ctx =>
            {
                foreach (Adventurer a in plain.Concat(team).Concat(stressed)) a.State.Fatigue = 80f;
                foreach (Adventurer a in stressed) a.State.Stress = 70f;
            }));
            plain.AddRange(world.AddMany(Group));
            team.AddRange(world.AddMany(Group));
            stressed.AddRange(world.AddMany(Group));
            foreach (Adventurer a in team) a.SetAxis(AxisId.People, 100f);

            int plainTavern = 0, teamTavern = 0, stressedTavern = 0;
            for (int day = 0; day < Days; day++)
            {
                world.TickToHour(world.Balance.Time.EveningHour);
                plainTavern += plain.Count(a => a.State.PlannedActivity == Activity.Tavern);
                teamTavern += team.Count(a => a.State.PlannedActivity == Activity.Tavern);
                stressedTavern += stressed.Count(a => a.State.PlannedActivity == Activity.Tavern);
            }

            int trials = Group * Days;
            TestContext.WriteLine($"tavern: plain {plainTavern}, team {teamTavern}, stressed {stressedTavern} of {trials}");
            Assert.That(plainTavern, Is.EqualTo(0.2f * trials).Within(Frequency.Tolerance(trials, 0.2f)));
            Assert.That(teamTavern, Is.EqualTo(0.8f * trials).Within(Frequency.Tolerance(trials, 0.8f)));
            Assert.That(stressedTavern, Is.EqualTo(0.8f * trials).Within(Frequency.Tolerance(trials, 0.8f)));
        }

        /// <summary>Днём таверна — только Пьянице (Утешение × 2 только в таверне) и при стрессе выше 60.</summary>
        [Test]
        public void Daytime_DrunkardAndHighStress_GoToTavern_OthersRest()
        {
            var stressed = new List<Adventurer>();
            world = new StateWorld(beforeState: new LambdaSystem("Hold", ctx =>
            {
                foreach (Adventurer a in stressed) a.State.Stress = 70f;
            }));
            world.Data.Set("traits.drunkardSkipChance", 0f);
            world.Data.Set("traits.drunkardSkipChanceStressed", 0f);
            List<Adventurer> plain = world.AddMany(Group);
            List<Adventurer> drunkards = world.AddMany(Group, "Drunkard");
            stressed.AddRange(world.AddMany(Group));

            int plainTavern = 0, drunkardTavern = 0, stressedTavern = 0;
            for (int day = 0; day < Days; day++)
            {
                world.TickToHour(world.Balance.Time.DayHour + 1);
                plainTavern += plain.Count(a => a.State.Activity == Activity.Tavern);
                drunkardTavern += drunkards.Count(a => a.State.Activity == Activity.Tavern);
                stressedTavern += stressed.Count(a => a.State.Activity == Activity.Tavern);
            }

            int trials = Group * Days;
            TestContext.WriteLine($"daytime tavern: plain {plainTavern}, drunkard {drunkardTavern}, stressed {stressedTavern} of {trials}");
            Assert.AreEqual(0, plainTavern);
            Assert.That(drunkardTavern, Is.EqualTo(0.8f * trials).Within(Frequency.Tolerance(trials, 0.8f)));
            Assert.That(stressedTavern, Is.EqualTo(0.8f * trials).Within(Frequency.Tolerance(trials, 0.8f)));
        }

        [Test]
        public void Weights_TraitsSmoothFromCenter_StateMultipliers()
        {
            world = new StateWorld();
            DataRegistry data = world.Registry;
            Adventurer adventurer = world.Add("Drunkard");
            adventurer.SetAxis(AxisId.Money, 50f);   // Жадный на полпути: Деньги ↑↑ → × 1,5
            adventurer.SetAxis(AxisId.Risk, -100f);  // трус: Безопасность ↑↑ → × 2, Слава ↓ → × 0,5
            adventurer.State.Fatigue = 75f;         // Отдых × 2
            adventurer.State.Stress = 70f;          // Утешение × 2, Безопасность × 1,3
            adventurer.State.Wallet = 0;            // Деньги × 2

            MotiveWeights rest = Motives.Weigh(adventurer, data, inTavern: false);
            MotiveWeights tavern = Motives.Weigh(adventurer, data, inTavern: true);

            Assert.AreEqual(3f, rest[Motive.Money], 1e-4f);
            Assert.AreEqual(0.5f, rest[Motive.Glory], 1e-4f);
            Assert.AreEqual(2.6f, rest[Motive.Safety], 1e-4f);
            Assert.AreEqual(2f, rest[Motive.Rest], 1e-4f);
            Assert.AreEqual(2f, rest[Motive.Comfort], 1e-4f, "утешение Пьяницы — только в таверне");
            Assert.AreEqual(4f, tavern[Motive.Comfort], 1e-4f);
            Assert.AreEqual(1f, rest[Motive.Companions], 1e-4f);
            Assert.IsTrue(rest.TryGetFactor(Motive.Money, out StateFactor factor, out _));
            Assert.AreEqual(StateFactor.LowWallet, factor);
        }

        [Test]
        public void TavernScores_PriceByWallet_FriendsThere()
        {
            world = new StateWorld();
            world.Data.Set("decisions.bestChoiceChance", 1f);
            Adventurer poor = world.Add();
            Adventurer friend = world.Add();
            poor.State.Wallet = 0;
            poor.State.Stress = 50f;
            world.Do(ctx => RelationService.Set(ctx, poor.Id, friend.Id, 50f));
            friend.State.PlannedActivity = Activity.Tavern;

            float[] scores = world.Do(ctx => DecisionActions.Scores(new DecisionScope(ctx), poor, DecisionActions.Tavern));
            Assert.AreEqual(-1f, scores[(int)Motive.Money], 1e-4f, "пьёт (стресс > 30), кошелёк пуст — −1");
            Assert.AreEqual(0.7f, scores[(int)Motive.Companions], 1e-4f, "0,6 + друг уже там");
            Assert.AreEqual(0.7f, scores[(int)Motive.Comfort], 1e-4f);
            Assert.AreEqual(1f, scores[(int)Motive.Safety], 1e-4f);
            Assert.AreEqual(0.3f, scores[(int)Motive.Rest], 1e-4f);
        }
    }

    /// <summary>Причина ухода — из мотивов и состояния, скрытые черты не называются; Наёмник раскрывается на проверке ухода.</summary>
    public sealed class LeaveReasonTests
    {
        private StateWorld world;

        [TearDown]
        public void TearDown() => world?.Dispose();

        [Test]
        public void Greedy_HiddenThenRevealed_TextNamesTraitOnlyWhenRevealed()
        {
            world = new StateWorld();
            Adventurer adventurer = world.Add();
            adventurer.SetAxis(AxisId.Money, 100f);

            List<LeaveCause> causes = Causes(adventurer);
            CollectionAssert.AreEqual(new[] { LeaveCause.LowPay }, causes);
            Assert.AreEqual("мало платят для его ранга", world.Do(ctx => LeaveReasons.Text(ctx, adventurer, causes)));

            adventurer.SetAxisRevealed(AxisId.Money);
            Assert.AreEqual("жадный — мало платят", world.Do(ctx => LeaveReasons.Text(ctx, adventurer, causes)));
        }

        [Test]
        public void Coward_Hidden_NotNamed_FemaleForms()
        {
            world = new StateWorld();
            Adventurer adventurer = world.Add();
            adventurer.Gender = Gender.Female;
            adventurer.SetAxis(AxisId.Risk, -100f);

            List<LeaveCause> causes = Causes(adventurer);
            Assert.AreEqual(LeaveCause.Danger, causes[0]);
            string hidden = world.Do(ctx => LeaveReasons.Text(ctx, adventurer, causes));
            Assert.AreEqual("слишком опасно, по её мнению", hidden);
            StringAssert.DoesNotContain("трус", hidden);

            adventurer.SetAxisRevealed(AxisId.Risk);
            Assert.AreEqual("трусиха — не пойдёт на такое", world.Do(ctx => LeaveReasons.Text(ctx, adventurer, causes)));
        }

        [Test]
        public void State_EmptyWalletTiredHeavyHeart_TopByWeight()
        {
            world = new StateWorld();
            Adventurer adventurer = world.Add();
            adventurer.State.Wallet = 0;        // Деньги × 2
            adventurer.State.Fatigue = 100f;   // Отдых × 3
            adventurer.State.Stress = 55f;     // Утешение × 1,5, Безопасность × 1,3

            List<LeaveCause> causes = Causes(adventurer);
            CollectionAssert.AreEqual(new[] { LeaveCause.Tired, LeaveCause.EmptyWallet, LeaveCause.HeavyHeart }, causes);
            Assert.AreEqual("устал, пустой кошелёк, тяжело на душе", world.Do(ctx => LeaveReasons.Text(ctx, adventurer, causes)));

            world.Do(ctx => StateService.StartBreakdown(ctx, adventurer, BreakdownKind.Collapse));
            adventurer.State.Stress = 55f;
            Assert.Contains(LeaveCause.HardAfterBreakdown, Causes(adventurer));
        }

        [Test]
        public void NothingStandsOut_LowLoyalty()
        {
            world = new StateWorld();
            Adventurer adventurer = world.Add();
            CollectionAssert.AreEqual(new[] { LeaveCause.LowLoyalty }, Causes(adventurer));
            Assert.AreEqual("ничто его здесь не держит", world.Do(ctx => LeaveReasons.Text(ctx, adventurer, Causes(adventurer))));
        }

        [Test]
        public void Leaving_EventAndFeedCarryReason_NoHiddenTraits()
        {
            world = new StateWorld(3u);
            List<Adventurer> people = world.AddMany(30);
            foreach (Adventurer adventurer in people)
            {
                adventurer.SetAxis(AxisId.Risk, -100f);
                adventurer.State.Loyalty = adventurer.State.Contentment = 5f;
            }

            List<SimEvent> events = world.Collect(() => { while (!(world.Time.Day == 1 && world.Time.Hour == 0)) world.Simulation.Tick(); });
            List<SimEvent> left = events.Where(e => e.Type == SimEventType.AdventurerLeft).ToList();
            Assert.IsNotEmpty(left);
            foreach (SimEvent e in left)
            {
                Assert.IsTrue(e.TryGet("cause", out LeaveCause cause));
                Assert.AreEqual(LeaveCause.Danger, cause);
                Assert.IsTrue(e.TryGet("reason", out string reason));
                StringAssert.StartsWith("слишком опасно", reason);
            }

            List<FeedEntry> lines = world.Simulation.World.Feed.Guild.Where(f => f.TemplateKey == FeedKeys.AdventurerLeft).ToList();
            Assert.AreEqual(left.Count, lines.Count);
            Assert.IsTrue(lines.All(f => f.Text.Contains("ушёл из гильдии: слишком опасно") && !f.Text.Contains("трус")), lines.First().Text);
        }

        [Test]
        public void Mercenary_MoneyMainReason_RevealedEvenIfStays()
        {
            world = new StateWorld();
            world.Data.Set("state.leaveChance", 0f);
            Adventurer mercenary = world.Add();
            Adventurer mercenaryRich = world.Add();
            foreach (Adventurer a in new[] { mercenary, mercenaryRich })
            {
                a.SetAxis(AxisId.Loyalty, -100f);
                a.State.Loyalty = a.State.Contentment = 5f;
            }
            mercenary.State.Wallet = 0;

            while (!(world.Time.Day == 1 && world.Time.Hour == 0)) world.Simulation.Tick();

            Assert.IsTrue(world.Simulation.World.Adventurers.IsActive(mercenary.Id), "остался");
            Assert.IsTrue(mercenary.IsAxisRevealed(AxisId.Loyalty), "главная причина — деньги: Наёмник раскрыт");
            Assert.IsFalse(mercenaryRich.IsAxisRevealed(AxisId.Loyalty), "деньги не главная причина — не раскрыт");
        }

        private List<LeaveCause> Causes(Adventurer adventurer) =>
            LeaveReasons.Of(adventurer, world.Registry, world.Time.TotalHours, world.Simulation.Calendar);
    }

    /// <summary>Вечер в таверне: отношения, ссоры, раскрытие Соперника.</summary>
    public sealed class TavernEveningTests
    {
        private StateWorld world;

        [TearDown]
        public void TearDown() => world?.Dispose();

        [Test]
        public void EveningTogether_RelationPlusHalf()
        {
            world = new StateWorld();
            world.Data.Set("decisions.bestChoiceChance", 1f);
            Adventurer a = world.Add();
            Adventurer b = world.Add();

            world.TickToHour(world.Balance.Time.NightHour);
            Assert.AreEqual(0.5f, world.Simulation.World.Relations.GetValue(a.Id, b.Id), 1e-4f);
            Assert.IsFalse(a.State.InTavernThisEvening, "отметка вечера сброшена");
        }

        [Test]
        public void Quarrels_BelowMinusTwenty_FivePercent_NotBetweenOthers()
        {
            world = new StateWorld(11u);
            world.Data.Set("decisions.bestChoiceChance", 1f);
            List<Adventurer> enemies = world.AddMany(10);
            List<Adventurer> neutral = world.AddMany(10);
            world.Do(ctx =>
            {
                for (int i = 0; i < enemies.Count; i++)
                for (int j = i + 1; j < enemies.Count; j++)
                    RelationService.Set(ctx, enemies[i].Id, enemies[j].Id, -80f);
            });
            var enemyIds = new HashSet<int>(enemies.Select(a => a.Id));

            const int evenings = 40;
            List<SimEvent> quarrels = world.Collect(() => world.Days(evenings)).Where(e => e.Type == SimEventType.Quarrel).ToList();

            int trials = 45 * evenings;
            TestContext.WriteLine($"quarrels {quarrels.Count}/{trials}");
            Assert.IsTrue(quarrels.All(e => enemyIds.Contains(e.Participants[0]) && enemyIds.Contains(e.Participants[1])), "только пары ниже −20");
            Assert.That(quarrels.Count, Is.EqualTo(0.05f * trials).Within(Frequency.Tolerance(trials, 0.05f)));
            Assert.IsTrue(quarrels.All(e => e.Importance == EventImportance.Notable));
            Assert.AreEqual(quarrels.Count, world.Simulation.World.Feed.Guild.Count(f => f.TemplateKey == FeedKeys.Quarrel), "по строке ленты на ссору");
        }

        [Test]
        public void Rivals_FirstQuarrel_RevealsTrait()
        {
            world = new StateWorld();
            world.Data.Set("decisions.bestChoiceChance", 1f);
            world.Data.Set("adventurers.quarrelChance", 1f);
            Adventurer a = world.Add();
            Adventurer b = world.Add();
            SpecialTraitDefinition rival = world.Registry.Get<SpecialTraitDefinition>("Rival");
            TraitService.AddAtGeneration(a, rival, world.Time.TotalHours, b.Id);
            TraitService.AddAtGeneration(b, rival, world.Time.TotalHours, a.Id);
            world.Do(ctx => RelationService.Set(ctx, a.Id, b.Id, 30f));

            List<SimEvent> events = world.Collect(() => world.TickToHour(world.Balance.Time.NightHour));

            SimEvent quarrel = events.Single(e => e.Type == SimEventType.Quarrel);
            Assert.IsTrue(quarrel.TryGet("rivals", out bool rivals) && rivals, "Соперники ссорятся и при хороших отношениях");
            Assert.AreEqual(20.5f, world.Simulation.World.Relations.GetValue(a.Id, b.Id), 1e-4f, "30 + 0,5 − 10");
            Assert.IsTrue(a.Traits.Single().Revealed && b.Traits.Single().Revealed);
            Assert.AreEqual(2, events.Count(e => e.Type == SimEventType.TraitRevealed));
        }
    }

    /// <summary>Одно зерно — те же решения; решения не сдвигают чужие броски.</summary>
    public sealed class DecisionDeterminismTests
    {
        private PeopleData data;

        [SetUp]
        public void SetUp() => data = new PeopleData();

        [TearDown]
        public void TearDown() => data.Dispose();

        [Test]
        public void SameSeed_SameDecisionsLogAndFeed()
        {
            (string log, string feed) Run()
            {
                var writer = new StringWriter();
                HeadlessRun.Result result = HeadlessRun.Run(data.Registry, 77u, 90, PlayerBots.Simple(), new SimLogger(SimLogLevel.Trace, writer));
                return (writer.ToString(), string.Join("\n", result.Simulation.World.Feed.Guild.Select(f => f.Text)));
            }

            (string log, string feed) first = Run();
            (string log, string feed) second = Run();
            StringAssert.Contains("decide #", first.log);
            Assert.AreEqual(first.log, second.log);
            Assert.AreEqual(first.feed, second.feed);
        }

        [Test]
        public void DecisionSystem_DoesNotShiftRecruitRolls()
        {
            data.NoQuests(); // задания меняют репутацию, а с ней — шанс кандидата
            string Run(bool withDecisions)
            {
                List<ISimSystem> systems = SimulationSystems.CreateDefault();
                if (!withDecisions) systems.RemoveAll(s => s is DecisionSystem);
                var simulation = new Simulation(data.Registry, 5u, systems);
                string log = SimulationLog.Record(simulation, sim => SimulationRun.Days(sim, 60));
                return string.Join("\n", log.Split('\n').Where(line => line.Contains("CandidateArrived")));
            }

            string without = Run(withDecisions: false);
            Assert.IsNotEmpty(without);
            Assert.AreEqual(without, Run(withDecisions: true));
        }
    }
}
