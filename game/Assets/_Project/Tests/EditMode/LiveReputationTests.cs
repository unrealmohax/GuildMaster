using System.Collections.Generic;
using System.IO;
using System.Linq;
using GuildMaster.Core;
using GuildMaster.Data;
using GuildMaster.Debugging;
using GuildMaster.UI;
using NUnit.Framework;

namespace GuildMaster.Tests
{
    /// <summary>Живая репутация: убывающая отдача, остывание, потери, последние изменения, сводка и интерфейс.</summary>
    public sealed class LiveReputationTests
    {
        private PeopleData data;

        [SetUp]
        public void SetUp() => data = new PeopleData();

        [TearDown]
        public void TearDown() => data.Dispose();

        [Test]
        public void QuestDone_DiminishingReturn()
        {
            GuildBalance balance = data.Balance.Guild;
            float perRank = balance.SuccessReputationPerRank;
            Assert.That(ReputationService.QuestDoneDelta(balance, 2, 0f), Is.EqualTo(perRank * 2).Within(1e-6f));
            Assert.That(ReputationService.QuestDoneDelta(balance, 2, 50f), Is.EqualTo(perRank * 2 * 0.5f).Within(1e-6f));
            Assert.That(ReputationService.QuestDoneDelta(balance, 2, 90f), Is.EqualTo(perRank * 2 * 0.1f).Within(1e-6f));
            Assert.AreEqual(0f, ReputationService.QuestDoneDelta(balance, 2, 100f));
        }

        [Test]
        public void Decay_AtMidnight_OnlyWithoutQuestsDone()
        {
            data.NoQuests();
            data.NoBankruptcy();
            data.Set("guild.reputationDecayPerDay", 0.05f);
            var simulation = Simulation.CreateDefault(data.Registry, 3u);
            SimulationRun.Do(simulation, ctx => ReputationService.Change(ctx, 45f, "test"));
            float start = simulation.World.Guild.Reputation;

            TickToMidnight(simulation);
            Assert.That(simulation.World.Guild.Reputation, Is.EqualTo(start - 0.05f).Within(1e-4f), "сутки без заданий — остыла");
            ReputationChange last = simulation.World.Guild.Changes.Last();
            Assert.AreEqual(ReputationReasons.Decay, last.Reason);
            Assert.AreEqual(0, last.AdventurerId);
            Assert.AreEqual(0, last.OrderId);

            simulation.Tick();
            SimulationRun.Do(simulation, ctx => ReputationService.QuestDone(ctx, 1, null));
            float afterQuest = simulation.World.Guild.Reputation;
            TickToMidnight(simulation);
            Assert.AreEqual(afterQuest, simulation.World.Guild.Reputation, 1e-6f, "задание выполнено — не остывает");

            TickToMidnight(simulation);
            Assert.That(simulation.World.Guild.Reputation, Is.EqualTo(afterQuest - 0.05f).Within(1e-4f), "счёт заданий начат заново");
        }

        [Test]
        public void Decay_DoesNotGoBelowZero_NorRunsWhenClosed()
        {
            data.NoQuests();
            data.NoBankruptcy();
            data.Set("guild.reputationDecayPerDay", 1f);
            var simulation = Simulation.CreateDefault(data.Registry, 3u);
            SimulationRun.Days(simulation, 10);
            Assert.AreEqual(0f, simulation.World.Guild.Reputation);

            SimulationRun.Do(simulation, ctx => ReputationService.Change(ctx, 50f, "test"));
            SimulationRun.Do(simulation, ctx => GuildClosure.Close(ctx, ClosedCause.Bankruptcy));
            SimulationRun.Days(simulation, 3);
            Assert.AreEqual(50f, simulation.World.Guild.Reputation, "гильдия закрыта — не остывает");
        }

        [Test]
        public void Leave_CostsReputation_FollowerDoesNot()
        {
            using var world = new StateWorld(beforeState: null);
            world.Data.Set("state.leaveChance", 1f);
            world.Data.Set("state.followFriendChance", 1f);
            world.Do(ctx => ReputationService.Change(ctx, 45f, "test"));
            Adventurer leaver = world.Add();
            Adventurer friend = world.Add();
            leaver.State.Loyalty = 0f;
            friend.State.Loyalty = 100f;
            world.Do(ctx => RelationService.Set(ctx, leaver.Id, friend.Id, 100f));
            float start = world.Simulation.World.Guild.Reputation;

            List<SimEvent> events = world.Collect(() =>
            {
                do
                {
                    leaver.State.Contentment = 50f;
                    friend.State.Contentment = 10f;
                    world.Simulation.Tick();
                }
                while (!(world.Time.Day == 1 && world.Time.Hour == 0));
            });

            Assert.AreEqual(2, events.Count(e => e.Type == SimEventType.AdventurerLeft), "ушёл и увёл друга");
            List<ReputationChange> losses = world.Simulation.World.Guild.Changes.Where(c => c.Reason == ReputationReasons.Left).ToList();
            Assert.AreEqual(1, losses.Count, "за ушедшего вслед — без потери");
            Assert.AreEqual(leaver.Id, losses[0].AdventurerId);
            Assert.AreEqual(-1f, losses[0].Delta, 1e-6f);
            Assert.AreEqual(start - 1f, world.Simulation.World.Guild.Reputation, 1e-4f);
            SimEvent changed = events.Single(e => e.Type == SimEventType.ReputationChanged
                && e.TryGet("reason", out string reason) && reason == ReputationReasons.Left);
            Assert.AreEqual(leaver.Id, changed.Participants[0], "человек — участник события");
        }

        [Test]
        public void Flee_CostsReputation()
        {
            using var quests = new QuestWorld();
            quests.World.Do(ctx => ReputationService.Change(ctx, 45f, "test"));
            Adventurer fugitive = quests.Add();
            Adventurer stays = quests.Add();
            QuestRun run = quests.Start(quests.AddOrder(), fugitive, stays);
            float start = quests.Simulation.World.Guild.Reputation;

            quests.World.Do(ctx => QuestTension.Flee(ctx, run, fugitive));
            Assert.AreEqual(start - 1f, quests.Simulation.World.Guild.Reputation, 1e-4f);
            ReputationChange change = quests.Simulation.World.Guild.Changes.Last();
            Assert.AreEqual(ReputationReasons.Fled, change.Reason);
            Assert.AreEqual(fugitive.Id, change.AdventurerId);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ImportantOrderFailed_CostsExtra(bool important)
        {
            using var quests = new QuestWorld();
            quests.Data.Set("decisions.bestChoiceChance", 1f);
            quests.World.Do(ctx => ReputationService.Change(ctx, 45f, "test"));
            Adventurer a = quests.Add();
            Order order = quests.AddOrder(requirement: 500f, reward: 100, rank: GuildRank.F);
            order.IsImportant = important;
            QuestRun run = quests.Start(order, a);
            float start = quests.Simulation.World.Guild.Reputation;
            quests.AtSite(run, failedRounds: 2);
            quests.RunToEnd(run);

            Assert.IsFalse(run.Completed);
            GuildBalance guild = quests.Data.Balance.Guild;
            float expected = start + guild.FailureReputationPerRank * 2 + (important ? guild.ImportantFailedReputation : 0f);
            Assert.AreEqual(expected, quests.Simulation.World.Guild.Reputation, 1e-4f);
            List<ReputationChange> changes = quests.Simulation.World.Guild.Changes.Where(c => c.OrderId == order.Id).ToList();
            Assert.AreEqual(ReputationReasons.QuestFailed, changes[0].Reason);
            Assert.AreEqual(important ? 2 : 1, changes.Count);
            if (important) Assert.AreEqual(ReputationReasons.ImportantFailed, changes[1].Reason);
        }

        [Test]
        public void QuestDone_RecordsOrderAndCountsForDecay()
        {
            using var quests = new QuestWorld();
            quests.Data.Set("travel.eventChanceNear", 0f);
            quests.Data.Set("travel.discoveryChance", 0f);
            Adventurer a = quests.Add();
            Order order = quests.AddOrder(requirement: 10f, reward: 100);
            QuestRun run = quests.Start(order, a);
            quests.RunToEnd(run);

            Assert.IsTrue(run.Completed);
            ReputationChange change = quests.Simulation.World.Guild.Changes.Single(c => c.Reason == ReputationReasons.QuestDone);
            Assert.AreEqual(order.Id, change.OrderId);
            Assert.AreEqual(order.TypeId, change.QuestTypeId);
            Assert.AreEqual(order.Rank, change.Rank);
            Assert.AreEqual(1, quests.Simulation.World.Guild.QuestsDoneToday, "засчитано для остывания");
        }

        [Test]
        public void Changes_KeepOnlyRecent()
        {
            data.Set("guild.recentReputationChanges", 3);
            var simulation = Simulation.CreateDefault(data.Registry, 1u);
            for (int i = 1; i <= 5; i++)
            {
                float delta = i;
                SimulationRun.Do(simulation, ctx => ReputationService.Change(ctx, delta, ReputationReasons.Debug));
            }
            CollectionAssert.AreEqual(new[] { 3f, 4f, 5f }, simulation.World.Guild.Changes.Select(c => c.Delta).ToArray());
        }

        [Test]
        public void Summary_DecayAndLossColumns()
        {
            data.NoQuests();
            data.NoBankruptcy();
            data.Set("guild.reputationDecayPerDay", 0.05f);
            data.Set("guild.startReputation", 50f);
            HeadlessRun.Result result = HeadlessRun.Run(data.Registry, 5u, 61, PlayerBots.Simple());
            RunSummary summary = result.Summary;
            List<string> names = summary.MonthColumns.Select(c => c.Name).ToList();
            int decay = names.IndexOf("Репутация: остывание");
            int losses = names.IndexOf("Репутация: потери");
            Assert.GreaterOrEqual(decay, 0);
            Assert.GreaterOrEqual(losses, 0);
            foreach (MonthRow month in summary.Months)
            {
                Assert.AreEqual(-0.05 * month.Days, month.Values[decay], 0.051, "по 0,05 за сутки месяца");
                Assert.AreEqual(0.0, month.Values[losses], 1e-9, "без заданий и уходов — потерь нет");
            }
        }

        [Test]
        public void SameSeed_SameLog_WithLiveReputation()
        {
            data.Set("guild.reputationDecayPerDay", 0.05f);
            string Run()
            {
                var writer = new StringWriter();
                HeadlessRun.Run(data.Registry, 23u, 120, PlayerBots.Simple(), new SimLogger(SimLogLevel.Debug, writer));
                return writer.ToString();
            }

            string first = Run();
            StringAssert.Contains("reason=questDone", first);
            StringAssert.Contains("reputation day quests=", first);
            Assert.AreEqual(first, Run());
        }

        [Test]
        public void SameSeed_SameLog_AllPeopleAndReputationMechanics()
        {
            // Жадный бот: подъёмные, низкое довольство, молва, «не в духе», уходы вслед за другом, живая репутация — всё сразу.
            data.Set("state.sulkContentment", 100f);
            data.Set("state.followContentment", 100f);
            data.Set("state.followFriendChance", 1f);
            string Run()
            {
                var writer = new StringWriter();
                HeadlessRun.Run(data.Registry, 24u, 240, PlayerBots.Reckless(), new SimLogger(SimLogLevel.Debug, writer));
                return writer.ToString();
            }

            string first = Run();
            StringAssert.Contains("signingBonuses", first, "подъёмные платились");
            StringAssert.Contains("sulk-day", first, "броски «не в духе» были");
            StringAssert.Contains("reason=questDone", first, "репутация росла за задания");
            StringAssert.Contains("reputation day quests=", first, "полночная проверка остывания была");
            Assert.AreEqual(first, Run());
        }

        [Test]
        public void Ui_ChangesNewestFirst_WithReasonsAndLinks()
        {
            using var quests = new QuestWorld();
            Adventurer a = quests.Add();
            Order order = quests.AddOrder(rank: GuildRank.E);
            quests.World.Do(ctx => ReputationService.Change(ctx, -1f, ReputationReasons.Left, a.Id, null));
            quests.World.Do(ctx => ReputationService.Change(ctx, 0.0114f, ReputationReasons.QuestDone, 0, order));
            quests.World.Do(ctx => ReputationService.Change(ctx, -0.05f, ReputationReasons.Decay));

            ReputationModel model = ReputationModel.Build(quests.Simulation);
            Assert.AreEqual(3, model.Changes.Count);
            Assert.AreEqual("сутки без выполненных заданий", model.Changes[0].Text);
            Assert.AreEqual(TextLinkKind.None, model.Changes[0].Link.Kind);
            StringAssert.EndsWith(" E: выполнено", model.Changes[1].Text);
            Assert.AreEqual(new TextLink(TextLinkKind.Order, order.Id).Id, model.Changes[1].Link.Id);
            Assert.AreEqual(TextLinkKind.Order, model.Changes[1].Link.Kind);
            Assert.AreEqual($"уход из гильдии: {a.Name}", model.Changes[2].Text);
            Assert.AreEqual(TextLinkKind.Adventurer, model.Changes[2].Link.Kind);

            Assert.AreEqual("−1", ReputationModel.Delta(-1f));
            Assert.AreEqual("+0,01", ReputationModel.Delta(0.0114f));
            Assert.AreEqual("−0,05", ReputationModel.Delta(-0.05f));
            Assert.AreEqual("+0,004", ReputationModel.Delta(0.004f));
        }

        private static void TickToMidnight(Simulation simulation)
        {
            do simulation.Tick();
            while (simulation.World.Time.Hour != 0);
        }
    }
}
