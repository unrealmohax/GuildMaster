using System.Collections.Generic;
using System.IO;
using System.Linq;
using GuildMaster.Core;
using GuildMaster.Data;
using GuildMaster.Debugging;
using NUnit.Framework;

namespace GuildMaster.Tests
{
    /// <summary>«Не в духе» (профиль ниже, день без работы) и уход вслед за другом.</summary>
    public sealed class SulkFollowTests
    {
        [Test]
        public void Sulking_LowersWholeProfile()
        {
            using var world = new StateWorld();
            Adventurer person = world.Add();
            float before = AdventurerStats.Effective(person, StatId.Strength, world.Registry);

            person.State.Contentment = 24f;
            Assert.IsTrue(StateRules.IsSulking(person.State, world.Balance.State));
            Assert.That(AdventurerStats.Effective(person, StatId.Strength, world.Registry), Is.EqualTo(before * 0.9f).Within(1e-4f));
            Assert.AreEqual(person.GetStat(StatId.Strength), AdventurerStats.Permanent(person, StatId.Strength, world.Registry),
                "архетип и постоянный профиль не трогает");

            person.State.Contentment = 25f;
            Assert.That(AdventurerStats.Effective(person, StatId.Strength, world.Registry), Is.EqualTo(before).Within(1e-4f));
        }

        [Test]
        public void SulkingDay_NoOrdersUntilNight_EventAndFeedLine()
        {
            using var world = new StateWorld(beforeState: KeepContentment(10f));
            world.Data.Set("state.sulkSkipChance", 1f);
            world.Data.Set("state.sulkSkipChanceMax", 1f);
            Adventurer person = world.Add();
            person.State.Contentment = 10f;

            List<SimEvent> events = world.Collect(() => world.TickToHour(world.Balance.Time.MorningHour));
            SimEvent sulk = events.Single(e => e.Type == SimEventType.SulkingDay);
            Assert.AreEqual(person.Id, sulk.Participants[0]);
            Assert.IsTrue(person.State.IsSulkingDay(world.Time.TotalHours));
            Assert.IsTrue(world.Simulation.World.Feed.Guild.Any(f => f.Text.Contains("не в духе — сегодня не работает")));

            var order = new DecisionAction(DecisionActionKind.TakeOrder, Activity.Resting, false, null, 1);
            var join = new DecisionAction(DecisionActionKind.JoinParty, Activity.Resting, false, null, 1);
            Assert.IsTrue(world.Do(ctx => SulkBan.Applies(ctx, person, order)), "заказ одному запрещён");
            Assert.IsTrue(world.Do(ctx => SulkBan.Applies(ctx, person, join)), "заказ с группой запрещён");
            Assert.IsFalse(world.Do(ctx => SulkBan.Applies(ctx, person, DecisionActions.Rest)), "отдых — можно");

            world.TickToHour(world.Balance.Time.NightHour);
            Assert.IsFalse(person.State.IsSulkingDay(world.Time.TotalHours), "к ночи день кончился");
            Assert.IsFalse(world.Do(ctx => SulkBan.Applies(ctx, person, order)));
        }

        [Test]
        public void Content_NoSulkRoll()
        {
            var writer = new StringWriter();
            using var world = new StateWorld(beforeState: KeepContentment(50f), log: new SimLogger(SimLogLevel.Debug, writer));
            world.Data.Set("state.sulkSkipChance", 1f);
            world.Add();
            List<SimEvent> events = world.Collect(() => world.Days(3));
            Assert.IsFalse(events.Any(e => e.Type == SimEventType.SulkingDay));
            StringAssert.DoesNotContain("sulk-day", writer.ToString(), "довольный не бросает");
        }

        [Test]
        public void SulkChance_Frequency()
        {
            using var world = new StateWorld(beforeState: KeepContentment(10f));
            world.AddMany(20);
            List<SimEvent> events = world.Collect(() => world.Days(20));
            int trials = 20 * 20;
            float share = events.Count(e => e.Type == SimEventType.SulkingDay) / (float)trials;
            float chance = StateRules.SulkSkipChance(10f, world.Balance.State);
            Assert.That(share, Is.EqualTo(chance).Within(Frequency.Tolerance(trials, chance)));
        }

        [Test]
        public void SulkChance_GrowsWithDiscontent()
        {
            using var world = new StateWorld();
            StateBalance balance = world.Balance.State;
            Assert.That(StateRules.SulkSkipChance(25f, balance), Is.EqualTo(0.15f).Within(1e-5f), "на границе — базовый шанс");
            Assert.That(StateRules.SulkSkipChance(12.5f, balance), Is.EqualTo(0.425f).Within(1e-5f), "посередине — по прямой");
            Assert.That(StateRules.SulkSkipChance(0f, balance), Is.EqualTo(0.7f).Within(1e-5f), "при нуле — наибольший");
        }

        [Test]
        public void LeaverTakesDiscontentFriends_NoChain()
        {
            using var world = new StateWorld(beforeState: null);
            world.Data.Set("state.leaveChance", 1f);
            world.Data.Set("state.followFriendChance", 1f);
            Adventurer leaver = world.Add();
            Adventurer sadFriend = world.Add();
            Adventurer happyFriend = world.Add();
            Adventurer sadStranger = world.Add();
            Adventurer friendOfFollower = world.Add();
            var contentment = new Dictionary<Adventurer, float>
            {
                [leaver] = 50f, [sadFriend] = 10f, [happyFriend] = 60f, [sadStranger] = 10f, [friendOfFollower] = 10f,
            };
            leaver.State.Loyalty = 0f;
            foreach (Adventurer stays in new[] { sadFriend, happyFriend, sadStranger, friendOfFollower }) stays.State.Loyalty = 100f;
            world.Do(ctx =>
            {
                RelationService.Set(ctx, leaver.Id, sadFriend.Id, 100f);
                RelationService.Set(ctx, leaver.Id, happyFriend.Id, 100f);
                RelationService.Set(ctx, sadFriend.Id, friendOfFollower.Id, 100f);
            });

            List<SimEvent> events = world.Collect(() =>
            {
                do
                {
                    foreach (KeyValuePair<Adventurer, float> pair in contentment) pair.Key.State.Contentment = pair.Value;
                    world.Simulation.Tick();
                }
                while (!(world.Time.Day == 1 && world.Time.Hour == 0));
            });

            List<SimEvent> left = events.Where(e => e.Type == SimEventType.AdventurerLeft).ToList();
            Assert.AreEqual(2, left.Count);
            Assert.AreEqual(leaver.Id, left[0].Participants[0]);
            Assert.AreEqual(sadFriend.Id, left[1].Participants[0]);
            Assert.IsTrue(left[1].TryGet("cause", out LeaveCause cause) && cause == LeaveCause.FollowedFriend);
            Assert.IsTrue(left[1].TryGet("friend", out int friendId) && friendId == leaver.Id);
            StringAssert.Contains("вслед за ", sadFriend.LeaveReasonText);

            WorldState state = world.Simulation.World;
            Assert.IsTrue(state.Adventurers.IsActive(happyFriend.Id), "довольный друг остался");
            Assert.IsTrue(state.Adventurers.IsActive(sadStranger.Id), "не друг — остался");
            Assert.IsTrue(state.Adventurers.IsActive(friendOfFollower.Id), "ушедший вслед друзей не уводит");
            Assert.IsTrue(state.Feed.Guild.Any(f => f.Text.Contains("из гильдии вслед за ")));
        }

        [Test]
        public void SameSeed_SameLog_WithSulkingAndFollowing()
        {
            using var data = new PeopleData();
            data.Set("state.sulkContentment", 100f);
            data.Set("state.followContentment", 100f);
            data.Set("state.followFriendChance", 1f);

            string Run()
            {
                var writer = new StringWriter();
                HeadlessRun.Run(data.Registry, 31u, 120, PlayerBots.Simple(), new SimLogger(SimLogLevel.Debug, writer));
                return writer.ToString();
            }

            string first = Run();
            StringAssert.Contains("sulk-day", first, "броски «не в духе» были");
            Assert.AreEqual(first, Run());
        }

        [Test]
        public void Summary_HasSulkAndFollowColumns()
        {
            using var data = new PeopleData();
            data.Set("state.sulkContentment", 100f);
            HeadlessRun.Result result = HeadlessRun.Run(data.Registry, 5u, 40, PlayerBots.Simple());
            RunSummary summary = result.Summary;
            int sulk = summary.MonthColumns.ToList().FindIndex(c => c.Name == "Не в духе, дней");
            Assert.That(sulk, Is.GreaterThanOrEqualTo(0));
            Assert.That(summary.MonthColumns.Any(c => c.Name == "Ушли вслед за другом"));
            Assert.That(summary.Months.Sum(m => m.Values[sulk]), Is.GreaterThan(0));
        }

        private static LambdaSystem KeepContentment(float value) =>
            new LambdaSystem("keep-contentment", ctx =>
            {
                foreach (Adventurer adventurer in ctx.World.Adventurers.Active) adventurer.State.Contentment = value;
            });

        private static DecisionBan SulkBan => DecisionBans.Default.Single(ban => ban.Reason.StartsWith("sulking"));
    }
}
