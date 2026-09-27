using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GuildMaster.Core;
using GuildMaster.Data;
using GuildMaster.Debugging;
using NUnit.Framework;

namespace GuildMaster.Tests
{
    /// <summary>Помощники тестов групп: люди с нужной Слаженностью, заказ с профилем, ценность варианта.</summary>
    internal static class PartyTestSupport
    {
        /// <summary>Человек: все параметры <paramref name="stats"/>, Слаженность <paramref name="cohesion"/>, оси 0.</summary>
        public static Adventurer Person(QuestWorld quests, float stats = 30f, float cohesion = 80f, GuildRank rank = GuildRank.G)
        {
            Adventurer person = quests.Add();
            for (int i = 0; i < Vocabulary.StatCount; i++) person.SetStat((StatId)i, stats);
            person.SetStat(StatId.Cohesion, cohesion);
            person.GuildRank = rank;
            return person;
        }

        public static List<SimEvent> Of(this List<SimEvent> events, SimEventType type) => events.Where(e => e.Type == type).ToList();
    }

    /// <summary>Сбор групп под задание: сами, кого звали, кто отказался и почему, дыры профиля, черты, предел 6.</summary>
    public sealed class PartyGatheringTests
    {
        [Test]
        public void PeopleGather_ByThemselves_LogShowsWhoWasInvitedAndWhoRefusedWhy()
        {
            var text = new StringWriter();
            using var quests = new QuestWorld(log: new SimLogger(SimLogLevel.Trace, text));
            quests.Data.Set("decisions.bestChoiceChance", 1f);
            // Одиночку зовут первым (оценки напарников равны — первый в списке), но у него свой заказ по рангу, лёгкий.
            Adventurer initiator = PartyTestSupport.Person(quests);
            Adventurer loner = PartyTestSupport.Person(quests, rank: GuildRank.F);
            loner.SetAxis(AxisId.People, -100f);
            Adventurer partner = PartyTestSupport.Person(quests, rank: GuildRank.F);
            Order order = quests.AddOrder(requirement: 45f, reward: 200);
            quests.AddOrder(rank: GuildRank.F, requirement: 20f, reward: 150);

            List<SimEvent> events = quests.World.Collect(() => quests.Simulation.Tick());

            SimEvent gathered = events.Of(SimEventType.PartyGathered).Single();
            CollectionAssert.AreEqual(new[] { initiator.Id, partner.Id }, gathered.Participants, "инициатор первым, потом согласившийся");
            SimEvent declined = events.Of(SimEventType.InvitationDeclined).Single();
            Assert.AreEqual(loner.Id, declined.Participants[0]);
            Assert.AreEqual(initiator.Id, declined.Participants[1]);
            Assert.IsTrue(declined.TryGet("reason", out string reason) && reason.Length > 0, "причина отказа видна");
            Assert.AreEqual(order.Id, initiator.State.PlannedOrderId);
            Assert.AreEqual(order.Id, partner.State.PlannedOrderId);
            Assert.AreNotEqual(order.Id, loner.State.PlannedOrderId);

            string log = text.ToString();
            StringAssert.Contains("SeekParty#" + order.Id, log, "решение собрать группу");
            StringAssert.Contains("party-presume", log, "предполагаемая группа — Trace");
            StringAssert.Contains("partner-eval", log, "оценки напарников — Trace");
            StringAssert.Contains("invite #" + initiator.Id + ": JoinParty#" + order.Id, log, "кого звали и что решил");
            StringAssert.Contains("InvitationDeclined", log, "отказ с причиной — событие в логе");

            List<FeedEntry> feed = quests.Simulation.World.Feed.Guild.ToList();
            Assert.IsTrue(feed.Any(f => f.TemplateKey == FeedKeys.PartyGatheredPair), "строка «собрал группу»");
            Assert.IsTrue(feed.Any(f => f.TemplateKey == FeedKeys.InvitationDeclinedSolo && f.Text.Contains(reason)), "строка отказа с причиной");

            List<SimEvent> departure = quests.World.Collect(() => quests.Simulation.Tick());
            QuestRun run = quests.Simulation.World.Quests.Active.Single(r => r.OrderId == order.Id);
            CollectionAssert.AreEquivalent(new[] { initiator.Id, partner.Id }, run.Members, "группа выходит вместе в следующем часу");
            Assert.AreNotEqual(0, run.PartyId);
            Assert.IsFalse(run.IsPermanentParty);
            Assert.AreEqual(PartyContext.InGroup, initiator.State.QuestParty);
            Assert.AreEqual(PartyContext.InGroup, partner.State.QuestParty);
            Assert.AreEqual(2, departure.Of(SimEventType.QuestDeparted).Count, "группа и одиночка");
        }

        [Test]
        public void PartnerScore_ClosesHoleInProfile()
        {
            using var quests = new QuestWorld();
            Adventurer initiator = PartyTestSupport.Person(quests, stats: 50f, cohesion: 60f);
            initiator.SetStat(StatId.Endurance, 10f);
            var clones = Enumerable.Range(0, 3).Select(_ =>
            {
                Adventurer clone = PartyTestSupport.Person(quests, stats: 50f, cohesion: 60f);
                clone.SetStat(StatId.Endurance, 10f);
                return clone;
            }).ToList();
            Adventurer shield = PartyTestSupport.Person(quests, stats: 20f, cohesion: 60f);
            shield.SetStat(StatId.Endurance, 90f);
            Order order = quests.AddOrder(requirement: 30f);
            order.SetRequirement(StatId.Endurance, 70f);

            var candidates = new List<Adventurer>(clones) { shield };
            var lens = new PartyLens(initiator, order, false, 0.2f, quests.Simulation.World.Relations, quests.Registry);
            Assert.AreSame(shield, PartyMath.BestPartner(lens, new List<Adventurer> { initiator }, candidates), "Щит закрывает дыру по Выносливости");
        }

        [Test]
        public void PresumedParties_CoverProfileBetterThanRandomOnes()
        {
            using var quests = new QuestWorld();
            var random = new Random(12);
            DataRegistry data = quests.Registry;
            int trials = 0, wins = 0;
            double presumedSum = 0, randomSum = 0;
            for (int t = 0; t < 60; t++)
            {
                List<Adventurer> people = Enumerable.Range(0, 9).Select(_ =>
                {
                    Adventurer person = PartyTestSupport.Person(quests, cohesion: 40f + random.Next(0, 41));
                    for (int i = 0; i < Vocabulary.StatCount; i++)
                    {
                        if (Vocabulary.IsDiagramAxis((StatId)i)) person.SetStat((StatId)i, 10f + random.Next(0, 61));
                    }
                    return person;
                }).ToList();
                Order order = quests.AddOrder(requirement: 20f, reward: 60);
                foreach (StatId stat in data.Stats.RadarOrder) order.SetRequirement(stat, 20f + random.Next(0, 51));

                Adventurer initiator = people[0];
                var lens = new PartyLens(initiator, order, false, 0.2f, quests.Simulation.World.Relations, data);
                PresumedParty presumed = PartyMath.Presume(lens, people.Skip(1).ToList());
                if (presumed.Members.Count < 2) continue;

                trials++;
                float mine = QuestMath.RealOverlap(presumed.Members, order, data);
                double others = 0;
                for (int r = 0; r < 30; r++)
                {
                    var group = new List<Adventurer> { initiator };
                    group.AddRange(people.Skip(1).OrderBy(_ => random.Next()).Take(presumed.Members.Count - 1));
                    others += QuestMath.RealOverlap(group, order, data);
                }
                others /= 30;
                presumedSum += mine;
                randomSum += others;
                if (mine > others) wins++;
            }

            Assert.Greater(trials, 20, "группы вообще складываются");
            Assert.Greater(wins, trials * 0.8, $"группа по оценке напарника лучше случайной: {wins} из {trials}");
            Assert.Greater(presumedSum / trials, randomSum / trials);
        }

        [Test]
        public void Greedy_SmallerParties_Team_Larger_Loner_Alone()
        {
            using var quests = new QuestWorld();
            List<Adventurer> pool = Enumerable.Range(0, 8).Select(_ => PartyTestSupport.Person(quests, cohesion: 50f)).ToList();
            Order order = quests.AddOrder(requirement: 50f, reward: 90);

            int Size(float money, float people)
            {
                Adventurer initiator = PartyTestSupport.Person(quests, cohesion: 50f);
                initiator.SetAxis(AxisId.Money, money);
                initiator.SetAxis(AxisId.People, people);
                initiator.State.Wallet = 0;
                var lens = new PartyLens(initiator, order, false, 0.2f, quests.Simulation.World.Relations, quests.Registry);
                return PartyMath.Presume(lens, pool).Members.Count;
            }

            int neutral = Size(0f, 0f);
            int greedy = Size(100f, 0f);
            int team = Size(0f, 100f);
            int loner = Size(0f, -100f);
            TestContext.WriteLine($"neutral {neutral}, greedy {greedy}, team {team}, loner {loner}");
            Assert.Less(greedy, team, "жадный делит долю — группа меньше, чем у командного");
            Assert.LessOrEqual(greedy, neutral);
            Assert.GreaterOrEqual(team, neutral);
            Assert.LessOrEqual(loner, neutral, "одиночке группа не в радость");
        }

        [Test]
        public void Party_NeverMoreThanSix()
        {
            using var quests = new QuestWorld();
            quests.Data.Set("decisions.bestChoiceChance", 1f);
            List<Adventurer> people = Enumerable.Range(0, 10).Select(_ => PartyTestSupport.Person(quests, stats: 20f, cohesion: 30f)).ToList();
            foreach (Adventurer person in people) person.SetAxis(AxisId.People, 100f);
            Order order = quests.AddOrder(requirement: 60f, reward: 400);

            var lens = new PartyLens(people[0], order, false, 0.2f, quests.Simulation.World.Relations, quests.Registry);
            Assert.LessOrEqual(PartyMath.Presume(lens, people.Skip(1).ToList()).Members.Count, 6);

            List<SimEvent> events = quests.World.Collect(() => quests.Simulation.Tick());
            List<SimEvent> gathered = events.Of(SimEventType.PartyGathered);
            Assert.IsNotEmpty(gathered);
            foreach (SimEvent simEvent in gathered) Assert.LessOrEqual(simEvent.Participants.Count, 6);
            Assert.AreEqual(6, quests.Registry.Balance.Rounds.MaxPartySize);
        }

        [Test]
        public void Loner_RefusesParty_AndGoesAlone_Reveals()
        {
            using var quests = new QuestWorld();
            quests.Data.Set("decisions.bestChoiceChance", 1f);
            Adventurer initiator = PartyTestSupport.Person(quests);
            Adventurer loner = PartyTestSupport.Person(quests, rank: GuildRank.F);
            loner.SetAxis(AxisId.People, -100f);
            Order hard = quests.AddOrder(requirement: 45f, reward: 200);
            Order easy = quests.AddOrder(rank: GuildRank.F, requirement: 20f, reward: 150);

            List<SimEvent> events = quests.World.Collect(() => quests.Simulation.Tick());

            SimEvent declined = events.Of(SimEventType.InvitationDeclined).Single();
            Assert.IsTrue(declined.TryGet("solo", out bool solo) && solo, "взял заказ один");
            Assert.AreEqual(easy.Id, loner.State.PlannedOrderId);
            Assert.IsTrue(loner.IsAxisRevealed(AxisId.People), "Одиночка раскрыт");
            Assert.IsTrue(events.Of(SimEventType.AxisRevealed).Any(e => e.Participants[0] == loner.Id));
            Assert.AreNotEqual(0, hard.Id);
        }

        [Test]
        public void Team_WhenNobodyCame_RefusesToGoAlone_Reveals()
        {
            using var quests = new QuestWorld();
            quests.Data.Set("decisions.bestChoiceChance", 1f);
            Adventurer team = PartyTestSupport.Person(quests);
            team.SetAxis(AxisId.People, 100f);
            team.State.Fatigue = 60f; // один на тяжёлый заказ — хуже отдыха, с группой — лучше
            Adventurer loner = PartyTestSupport.Person(quests, rank: GuildRank.F);
            loner.SetAxis(AxisId.People, -100f);
            Order order = quests.AddOrder(requirement: 60f, reward: 200);
            quests.AddOrder(rank: GuildRank.F, requirement: 20f, reward: 150);

            List<SimEvent> events = quests.World.Collect(() => quests.Simulation.Tick());

            SimEvent failed = events.Of(SimEventType.PartyNotGathered).Single();
            Assert.AreEqual(team.Id, failed.Participants[0]);
            Assert.IsTrue(failed.TryGet("solo", out bool solo) && !solo, "одному не пошёл");
            Assert.AreEqual(OrderStatus.OnBoard, order.Status, "заказ снова на доске");
            Assert.AreEqual(0, team.State.PlannedOrderId);
            Assert.IsTrue(team.IsAxisRevealed(AxisId.People), "Командный раскрыт");
            Assert.IsTrue(quests.Simulation.World.Feed.Guild.Any(f => f.TemplateKey == FeedKeys.PartyNotGathered));
        }

        [Test]
        public void Rivals_FirstJointQuest_Reveals()
        {
            using var quests = new QuestWorld();
            Adventurer a = quests.Add();
            Adventurer b = quests.Add();
            Assert.IsTrue(quests.World.Do(ctx => TraitService.TryAcquire(ctx, a, "Rival", b.Id)));
            TraitInstance rival = a.Traits.Single();
            Assert.IsFalse(rival.Revealed);

            Order order = quests.AddOrder();
            List<SimEvent> events = quests.DoEvents(ctx => QuestSystem.Depart(ctx, TakeOrder(ctx, order), new[] { a, b }));
            Assert.IsTrue(rival.Revealed, "первое совместное задание раскрывает Соперника");
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.TraitRevealed && e.Participants[0] == a.Id));
        }

        private static Order TakeOrder(SimContext ctx, Order order)
        {
            order.Status = OrderStatus.Taken;
            ctx.World.Orders.MoveToWork(order);
            return order;
        }

        [Test]
        public void SharesByPower_InPartyForOneOrder_Companions()
        {
            using var quests = new QuestWorld();
            Adventurer strong = quests.Add();
            Adventurer weak = quests.Add();
            strong.PowerScore = 30f;
            weak.PowerScore = 10f;
            Order order = quests.AddOrder(reward: 100);
            var group = new List<Adventurer> { strong, weak };
            Assert.AreEqual(order.Reward * 0.8f * 0.75f, PartyMath.ExpectedShare(order, 0.2f, group, strong, equal: false), 1e-3);
            Assert.AreEqual(order.Reward * 0.8f * 0.5f, PartyMath.ExpectedShare(order, 0.2f, group, strong, equal: true), 1e-3);

            DecisionsBalance decisions = quests.Registry.Balance.Decisions;
            RelationBook relations = quests.Simulation.World.Relations;
            Assert.AreEqual(0f, PartyMath.Companions(strong, new[] { strong }, false, relations, quests.Registry), "соло — 0");
            Assert.AreEqual(decisions.CompanionsInGroup, PartyMath.Companions(strong, group, false, relations, quests.Registry), 1e-5);
            quests.World.Do(ctx => RelationService.Set(ctx, strong.Id, weak.Id, 50f));
            Assert.AreEqual(decisions.CompanionsInGroup + decisions.CompanionsFriendBonus + decisions.CompanionsPermanentBonus,
                PartyMath.Companions(strong, group, true, relations, quests.Registry), 1e-5, "друг и постоянная группа");
            strong.SetAxis(AxisId.People, -50f);
            Assert.AreEqual(-decisions.LonerGroupCompanions * 0.5f, PartyMath.Companions(strong, group, true, relations, quests.Registry), 1e-5,
                "одиночке группа — минус");
        }
    }

    /// <summary>Постоянные группы: складываются, ходят вместе, делят поровну, растят Слаженность, теряют людей и распадаются.</summary>
    public sealed class PermanentPartyTests
    {
        private static void Befriend(QuestWorld quests, params Adventurer[] people)
        {
            quests.World.Do(ctx =>
            {
                for (int i = 0; i < people.Length; i++)
                for (int j = i + 1; j < people.Length; j++)
                    RelationService.Set(ctx, people[i].Id, people[j].Id, 30f);
            });
        }

        [Test]
        public void Forms_AfterThreeJointSuccesses_WithName()
        {
            using var quests = new QuestWorld();
            quests.Data.NoQuests();
            Adventurer a = quests.Add(), b = quests.Add(), c = quests.Add();
            Befriend(quests, a, b, c);

            var formed = new List<SimEvent>();
            for (int i = 0; i < 3; i++)
            {
                QuestRun run = quests.Start(quests.AddOrder(requirement: 10f), a, b, c);
                formed.AddRange(quests.RunToEnd(run).Of(SimEventType.PermanentPartyFormed));
                Assert.IsTrue(run.Completed);
                if (i < 2) Assert.IsEmpty(formed, "до третьего успеха группы нет");
            }

            SimEvent simEvent = formed.Single();
            CollectionAssert.AreEquivalent(new[] { a.Id, b.Id, c.Id }, simEvent.Participants);
            Party party = quests.Simulation.World.Parties.Active.Single(p => p.IsPermanent);
            Assert.AreEqual(party.Id, a.PermanentPartyId);
            Assert.AreEqual(party.Id, c.PermanentPartyId);
            NameList names = quests.Registry.Names;
            Assert.IsTrue(names.GroupAdjectives.Any(adj => party.Name.StartsWith(adj + " ")) && names.GroupNouns.Any(n => party.Name.EndsWith(" " + n)),
                party.Name);
            Assert.IsTrue(simEvent.TryGet("title", out string title) && title == party.Name);
            Assert.IsTrue(quests.Simulation.World.Feed.Guild.Any(f => f.TemplateKey == FeedKeys.PermanentPartyFormed && f.Text.Contains(party.Name)));
            Assert.AreEqual(1, quests.Simulation.World.Parties.PermanentFormed);
        }

        [Test]
        public void NoParty_WhenRelationsLow()
        {
            using var quests = new QuestWorld();
            quests.Data.NoQuests();
            Adventurer a = quests.Add(), b = quests.Add();
            quests.World.Do(ctx => RelationService.Set(ctx, a.Id, b.Id, -30f));
            for (int i = 0; i < 3; i++) quests.RunToEnd(quests.Start(quests.AddOrder(requirement: 10f), a, b));
            Assert.AreEqual(3, quests.Simulation.World.Relations.GetJointSuccesses(a.Id, b.Id));
            Assert.IsFalse(quests.Simulation.World.Parties.Active.Any(p => p.IsPermanent), "отношения ниже порога");
        }

        [Test]
        public void SplitsRewardEqually_UnlikePartyForOneOrder()
        {
            using var quests = new QuestWorld();
            quests.Data.NoQuests();
            Adventurer strong = quests.Add(), weak = quests.Add();
            strong.PowerScore = 40f;
            weak.PowerScore = 10f;

            (int, int) Gains(Party party)
            {
                int s0 = strong.State.Wallet, w0 = weak.State.Wallet;
                Order order = quests.AddOrder(requirement: 10f, reward: 200);
                QuestRun run = party != null ? quests.StartParty(order, party, strong, weak) : quests.Start(order, strong, weak);
                quests.RunToEnd(run);
                Assert.IsTrue(run.Completed);
                return (strong.State.Wallet - s0, weak.State.Wallet - w0);
            }

            (int strongAdHoc, int weakAdHoc) = Gains(null);
            Assert.Greater(strongAdHoc, weakAdHoc + 20, "под задание — по силе");

            Party permanent = quests.MakePermanent(strong, weak);
            (int strongEqual, int weakEqual) = Gains(permanent);
            Assert.LessOrEqual(Math.Abs(strongEqual - weakEqual), 2, "постоянная группа — поровну");
        }

        [Test]
        public void GoesTogether_LeaderChoosesForAll_Departs()
        {
            using var quests = new QuestWorld();
            quests.Data.Set("decisions.bestChoiceChance", 1f);
            Adventurer a = PartyTestSupport.Person(quests), b = PartyTestSupport.Person(quests), c = PartyTestSupport.Person(quests);
            a.GuildRank = GuildRank.F;
            Party party = quests.MakePermanent(a, b, c);
            Order order = quests.AddOrder(requirement: 45f, reward: 200);

            List<SimEvent> events = quests.World.Collect(() => quests.Simulation.Tick());
            SimEvent gathered = events.Of(SimEventType.PartyGathered).Single();
            Assert.IsTrue(gathered.TryGet("permanent", out bool permanent) && permanent);
            Assert.AreEqual(a.Id, gathered.Participants[0], "лидер — высший ранг");
            Assert.AreEqual(a.Id, party.InitiatorId);
            Assert.IsTrue(new[] { a, b, c }.All(p => p.State.PlannedOrderId == order.Id));

            quests.Simulation.Tick();
            QuestRun run = quests.Simulation.World.Quests.Active.Single();
            Assert.IsTrue(run.IsPermanentParty);
            Assert.AreEqual(party.Name, run.PartyTitle);
            Assert.IsTrue(run.Log.Any(f => f.Text.Contains("«" + party.Name + "»")), "{группа} — название постоянной группы");
        }

        [Test]
        public void MemberWhoPrefersRest_StaysBehind_OthersGo()
        {
            using var quests = new QuestWorld();
            quests.Data.Set("decisions.bestChoiceChance", 1f);
            Adventurer a = PartyTestSupport.Person(quests), b = PartyTestSupport.Person(quests), c = PartyTestSupport.Person(quests);
            c.State.Fatigue = 85f; // устал: заказ ему хуже отдыха
            quests.MakePermanent(a, b, c);
            Order order = quests.AddOrder(requirement: 45f, reward: 200);

            List<SimEvent> events = quests.World.Collect(() => quests.Simulation.Tick());
            SimEvent gathered = events.Of(SimEventType.PartyGathered).Single();
            CollectionAssert.AreEquivalent(new[] { a.Id, b.Id }, gathered.Participants);
            Assert.AreEqual(0, c.State.PlannedOrderId);
            Assert.IsTrue(events.Of(SimEventType.InvitationDeclined).Any(e => e.Participants[0] == c.Id));
            Assert.AreNotEqual(0, order.Id);
        }

        [Test]
        public void Cohesion_GrowsFaster_InPermanentParty()
        {
            using var quests = new QuestWorld();
            quests.Data.NoQuests();
            Adventurer a = quests.Add(), b = quests.Add(), guest = quests.Add();
            Party party = quests.MakePermanent(a, b);
            float before = a.GetStat(StatId.Cohesion);
            float guestBefore = guest.GetStat(StatId.Cohesion);
            quests.RunToEnd(quests.StartParty(quests.AddOrder(requirement: 10f), party, a, b, guest));
            GrowthBalance growth = quests.Registry.Balance.Growth;
            Assert.Greater(a.GetStat(StatId.Cohesion) - before, guest.GetStat(StatId.Cohesion) - guestBefore, "член постоянной группы растёт быстрее гостя");
            Assert.Greater(growth.CohesionPerPermanentPartyQuest, growth.CohesionPerGroupQuest);
        }

        [Test]
        public void Guest_BecomesMember_AfterThreeSuccesses()
        {
            using var quests = new QuestWorld();
            quests.Data.NoQuests();
            Adventurer a = quests.Add(), b = quests.Add(), guest = quests.Add();
            quests.World.Do(ctx => RelationService.Set(ctx, a.Id, guest.Id, -30f)); // своей постоянной группы с ними не сложит
            Party party = quests.MakePermanent(a, b);
            var joined = new List<SimEvent>();
            for (int i = 0; i < 3; i++) joined.AddRange(quests.RunToEnd(quests.StartParty(quests.AddOrder(requirement: 10f), party, a, b, guest))
                .Of(SimEventType.PartyMemberJoined));
            Assert.AreEqual(guest.Id, joined.Single().Participants[0]);
            Assert.AreEqual(party.Id, guest.PermanentPartyId);
            CollectionAssert.AreEqual(new[] { a.Id, b.Id, guest.Id }, party.MemberIds);
        }

        [Test]
        public void Quarrel_WorseLoyalMemberLeaves_TwoLeft_Disbands()
        {
            using var quests = new QuestWorld();
            Adventurer a = quests.Add(), b = quests.Add(), c = quests.Add();
            Party party = quests.MakePermanent(a, b, c);
            quests.World.Do(ctx =>
            {
                RelationService.Set(ctx, a.Id, b.Id, -50f);
                RelationService.Set(ctx, b.Id, c.Id, 30f);
            });

            List<SimEvent> events = quests.DoEvents(PartyService.CheckQuarrels);
            SimEvent left = events.Of(SimEventType.PartyMemberLeft).Single();
            Assert.AreEqual(a.Id, left.Participants[0], "хуже относится к остальным — уходит");
            Assert.IsTrue(left.TryGet("cause", out string cause) && cause == "quarrel");
            Assert.AreEqual(0, a.PermanentPartyId);
            CollectionAssert.AreEqual(new[] { b.Id, c.Id }, party.MemberIds);

            quests.World.Do(ctx => RelationService.Set(ctx, b.Id, c.Id, -45f));
            events = quests.DoEvents(PartyService.CheckQuarrels);
            Assert.AreEqual(1, events.Of(SimEventType.PermanentPartyDisbanded).Count, "остался один — распалась");
            Assert.IsFalse(quests.Simulation.World.Parties.Active.Contains(party));
            Assert.AreEqual(0, b.PermanentPartyId);
            Assert.AreEqual(0, c.PermanentPartyId);
            Assert.AreEqual(1, quests.Simulation.World.Parties.PermanentDisbanded);
        }

        [Test]
        public void Loner_LeavesAfterThreeQuests()
        {
            using var quests = new QuestWorld();
            quests.Data.NoQuests();
            Adventurer loner = quests.Add(), b = quests.Add(), c = quests.Add();
            loner.SetAxis(AxisId.People, -80f);
            Party party = quests.MakePermanent(loner, b, c);
            var left = new List<SimEvent>();
            for (int i = 0; i < 3; i++)
            {
                left.AddRange(quests.RunToEnd(quests.StartParty(quests.AddOrder(requirement: 10f), party, loner, b, c)).Of(SimEventType.PartyMemberLeft));
                if (i < 2) Assert.IsEmpty(left);
            }
            Assert.AreEqual(loner.Id, left.Single().Participants[0]);
            Assert.IsTrue(left[0].TryGet("cause", out string cause) && cause == "loner");
            Assert.IsTrue(quests.Simulation.World.Feed.Guild.Any(f => f.TemplateKey == FeedKeys.PartyMemberLeftLoner));
            Assert.AreEqual(3, party.JointQuests);
        }

        [Test]
        public void Death_RemovesMember_LastOneLeft_Disbands()
        {
            using var quests = new QuestWorld();
            Adventurer a = quests.Add(), b = quests.Add();
            Party party = quests.MakePermanent(a, b);
            List<SimEvent> events = quests.DoEvents(ctx => AdventurerLifecycle.Retire(ctx, a, LeaveReason.Died));
            Assert.AreEqual(1, events.Of(SimEventType.PermanentPartyDisbanded).Count);
            Assert.AreEqual(0, b.PermanentPartyId);
            Assert.IsFalse(quests.Simulation.World.Parties.Active.Contains(party));
        }
    }

    /// <summary>Группы и случайность, сводка.</summary>
    public sealed class PartyDeterminismTests
    {
        [Test]
        public void SameSeed_SameLog_WithParties()
        {
            using var data = new PeopleData();
            string Run()
            {
                var text = new StringWriter();
                HeadlessRun.Run(data.Registry, 3u, 90, PlayerBots.Simple(), new SimLogger(SimLogLevel.Info, text));
                return text.ToString();
            }
            string first = Run();
            StringAssert.Contains("gathered for", first, "группы складываются");
            Assert.AreEqual(first, Run());
        }

        [Test]
        public void NoPartyPossible_NoRolls_SameLogAsWithoutParties()
        {
            // Один человек: собрать группу не из кого — код групп не тратит случайных чисел, лог как у мира, где группы запрещены.
            string Run(int maxParty)
            {
                var text = new StringWriter();
                using var quests = new QuestWorld(log: new SimLogger(SimLogLevel.Debug, text));
                quests.Data.Set("rounds.maxPartySize", maxParty);
                PartyTestSupport.Person(quests);
                for (int i = 0; i < 3; i++) quests.AddOrder(requirement: 20f + 10f * i);
                quests.World.Days(5);
                return text.ToString();
            }
            string withParties = Run(6);
            StringAssert.DoesNotContain("party #", withParties);
            StringAssert.DoesNotContain("SeekParty", withParties);
            Assert.AreEqual(Run(1), withParties);
        }

        [Test]
        public void Summary_HasPartyColumns()
        {
            using var data = new PeopleData();
            HeadlessRun.Result result = HeadlessRun.Run(data.Registry, 5u, 120, PlayerBots.Simple());
            RunSummary summary = result.Summary;
            string[] names = summary.MonthColumns.Select(c => c.Name).ToArray();
            foreach (string name in new[] { "Заданий соло", "Заданий в группе", "Средний размер группы", "Постоянных групп", "Групп сложилось", "Групп распалось" })
                CollectionAssert.Contains(names, name);
            int group = Array.IndexOf(names, "Заданий в группе");
            Assert.Greater(summary.Months.Sum(m => m.Values[group]), 0, "групповые задания есть");
        }
    }
}
