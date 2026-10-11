using System.Collections.Generic;
using System.IO;
using System.Linq;
using GuildMaster.Core;
using GuildMaster.Data;
using GuildMaster.Debugging;
using NUnit.Framework;

namespace GuildMaster.Tests
{
    public sealed class RationTests
    {
        private QuestWorld quests;

        [TearDown]
        public void TearDown() => quests?.Dispose();

        private FoodBalance Food => quests.Registry.Balance.Food;

        private int MealsAhead(Order order) =>
            TravelPlan.ExpectedMeals(order, quests.Registry, quests.Simulation.World.Time.Hour);

        // ---------- Путь ----------

        [Test]
        public void OrderGenerator_TravelHours_NearFixed_FarRolled()
        {
            using var data = new PeopleData();
            TravelBalance travel = data.Balance.Travel;
            var rng = new Rng(4u);
            var errors = new List<string>();
            bool sawNear = false, sawFar = false;
            for (int i = 0; i < 200; i++)
            {
                Order order = OrderGenerator.Generate(rng, data.Registry, 0f, i + 1, 0, null, errors);
                if (order.Distance == OrderDistance.Near)
                {
                    sawNear = true;
                    Assert.AreEqual(travel.NearHours, order.TravelHours, "близко — без броска");
                }
                else
                {
                    sawFar = true;
                    Assert.That(order.TravelHours, Is.InRange(travel.FarHours.Min, travel.FarHours.Max));
                }
            }
            Assert.IsTrue(sawNear && sawFar);
        }

        [Test]
        public void TravelTime_SameBothWays_FromOrder()
        {
            quests = new QuestWorld();
            Adventurer a = quests.Add();
            a.SetStat(StatId.Strength, 99f);
            Order order = quests.AddOrder(distance: OrderDistance.Far, requirement: 1f);
            order.TravelHours = 27;
            QuestRun run = quests.Start(order, a);
            Assert.AreEqual(27, run.PhaseHours, "путь туда — из заказа");
            var legs = new List<int>();
            for (int i = 0; i < 30 * 24 && run.Phase != QuestPhase.Returned; i++)
            {
                QuestPhase before = run.Phase;
                quests.Simulation.Tick();
                if (run.Phase == QuestPhase.TravelBack && before != QuestPhase.TravelBack) legs.Add(run.PhaseHours);
            }
            Assert.AreEqual(new List<int> { 27 }, legs, "путь назад — столько же");
        }

        [Test]
        public void TravelPlan_CountsMealsAndNights()
        {
            using var data = new PeopleData();
            TimeBalance time = data.Balance.Time;
            FoodBalance food = data.Balance.Food;
            // Выход в 08:00, 9 ходовых часов (08..16): приём в 13:00 — в пути, в 16:00 уже дома.
            Assert.AreEqual(1, TravelPlan.MealsDuring(time, food, 8, 9));
            // 24 ходовых часа с 06:00: ночлег 20..06, конец в 16:00 вторых суток — приёмы 7, 13, 19, 7, 13.
            Assert.AreEqual(5, TravelPlan.MealsDuring(time, food, 6, 24));
            Assert.AreEqual(34, TravelPlan.CalendarHours(time, 6, 24));
            Assert.AreEqual(3, TravelPlan.CalendarHours(time, 6, 3));
        }

        // ---------- Покупка ----------

        [Test]
        public void Depart_BuysNeedPlusReserve_Regular_WhenAffordable_TavernCounts()
        {
            quests = new QuestWorld();
            Adventurer a = quests.Add();
            Order order = quests.AddOrder(distance: OrderDistance.Far);
            int meals = MealsAhead(order);
            int wallet = a.State.Wallet;
            List<SimEvent> events = quests.DoEvents(ctx =>
            {
                order.Status = OrderStatus.Taken;
                ctx.World.Orders.MoveToWork(order);
                QuestSystem.Depart(ctx, order, new[] { a });
            });

            int need = meals + Food.RationReserveMeals;
            Assert.AreEqual(need, a.Rations.Regular, "богатый берёт обычные: приёмы пути + запас");
            Assert.AreEqual(0, a.Rations.Cheap);
            Assert.AreEqual(wallet - need * Food.RationRegular.Price, a.State.Wallet);
            Assert.AreEqual(need, quests.Simulation.World.Treasury.TavernRations[(int)RationTier.Regular], "пайки — в доход таверны");
            SimEvent bought = events.Single(e => e.Type == SimEventType.RationsBought);
            Assert.IsTrue(bought.TryGet("kind", out string kind) && kind == "bought");
            Assert.IsTrue(bought.TryGet("amount", out int amount) && amount == need);
        }

        [Test]
        public void Depart_BuysOnlyMissing_LeftoverKept()
        {
            quests = new QuestWorld();
            Adventurer a = quests.Add();
            Order order = quests.AddOrder(distance: OrderDistance.Far);
            int need = MealsAhead(order) + Food.RationReserveMeals;
            quests.World.Do(ctx => a.Rations.Add(RationTier.Cheap, need - 2));
            List<SimEvent> events = quests.DoEvents(ctx =>
            {
                order.Status = OrderStatus.Taken;
                ctx.World.Orders.MoveToWork(order);
                QuestSystem.Depart(ctx, order, new[] { a });
            });
            Assert.AreEqual(need, a.Rations.Total, "докупил только недостающее");
            Assert.AreEqual(2, a.Rations.Regular);

            quests.World.Do(ctx => a.Rations.Add(RationTier.Cheap, 50));
            Order next = quests.AddOrder(distance: OrderDistance.Far);
            events = quests.DoEvents(ctx =>
            {
                QuestSystem.LeaveQuest(a);
                next.Status = OrderStatus.Taken;
                ctx.World.Orders.MoveToWork(next);
                QuestSystem.Depart(ctx, next, new[] { a });
            });
            SimEvent enough = events.Single(e => e.Type == SimEventType.RationsBought);
            Assert.IsTrue(enough.TryGet("kind", out string kind) && kind == "enough", "хватило оставшихся");
        }

        [Test]
        public void Depart_Poor_BuysCheap_Broke_BuysWhatHeCan_Short()
        {
            quests = new QuestWorld();
            Adventurer poor = quests.Add();
            Adventurer broke = quests.Add();
            poor.State.Wallet = 60; // остаток над резервом 18 — на обычные не хватает
            broke.State.Wallet = 4;
            Order first = quests.AddOrder(distance: OrderDistance.Far);
            Order second = quests.AddOrder(distance: OrderDistance.Far);
            int need = MealsAhead(first) + Food.RationReserveMeals;
            quests.Start(first, poor);
            List<SimEvent> events = quests.DoEvents(ctx =>
            {
                second.Status = OrderStatus.Taken;
                ctx.World.Orders.MoveToWork(second);
                QuestSystem.Depart(ctx, second, new[] { broke });
            });

            Assert.AreEqual(need, poor.Rations.Cheap, "бедный — дешёвые");
            Assert.AreEqual(4, broke.Rations.Cheap, "без денег — сколько хватит");
            Assert.AreEqual(0, broke.State.Wallet);
            SimEvent bought = events.Single(e => e.Type == SimEventType.RationsBought);
            Assert.IsTrue(bought.TryGet("kind", out string kind) && kind == "short", "впритык");
        }

        [Test]
        public void Reserve_CowardMore_RecklessNone()
        {
            quests = new QuestWorld();
            Adventurer coward = quests.Add();
            Adventurer reckless = quests.Add();
            Adventurer plain = quests.Add();
            coward.SetAxis(AxisId.Risk, -100f);
            reckless.SetAxis(AxisId.Risk, 100f);
            Assert.AreEqual(Food.RationReserveMeals + Food.CautiousExtraMeals, RationRules.ReserveMeals(coward, Food));
            Assert.AreEqual(0, RationRules.ReserveMeals(reckless, Food));
            Assert.AreEqual(Food.RationReserveMeals, RationRules.ReserveMeals(plain, Food));
        }

        [Test]
        public void PermanentParty_SharedStock_PaidEqually_GuestBuysOwn()
        {
            quests = new QuestWorld();
            Adventurer a = quests.Add();
            Adventurer b = quests.Add();
            Adventurer guest = quests.Add();
            Party party = quests.MakePermanent(a, b);
            Order order = quests.AddOrder(distance: OrderDistance.Far);
            int meals = MealsAhead(order);
            int walletA = a.State.Wallet, walletB = b.State.Wallet;
            quests.World.Do(ctx => party.OrderId = order.Id);
            quests.StartParty(order, party, a, b, guest);

            int need = (meals + Food.RationReserveMeals) * 2;
            Assert.AreEqual(need, party.Rations.Total, "общий запас на двоих");
            Assert.AreEqual(0, a.Rations.Total + b.Rations.Total);
            Assert.AreEqual(walletA - walletB, a.State.Wallet - b.State.Wallet, "платят поровну");
            Assert.AreEqual(walletA - need / 2 * Food.RationRegular.Price, a.State.Wallet);
            Assert.AreEqual(meals + Food.RationReserveMeals, guest.Rations.Total, "гость — свои");
        }

        // ---------- В пути ----------

        [Test]
        public void Hungry_ComradeShares_TeamFirst_GreedyNever()
        {
            quests = new QuestWorld();
            Adventurer hungry = quests.Add();
            Adventurer greedy = quests.Add();
            Adventurer team = quests.Add();
            Adventurer rich = quests.Add();
            greedy.SetAxis(AxisId.Money, 80f);
            team.SetAxis(AxisId.People, 80f);
            QuestRun run = quests.Start(quests.AddOrder(), hungry, greedy, team, rich);
            quests.World.Do(ctx =>
            {
                hungry.Rations.Clear();
                greedy.Rations.Clear();
                greedy.Rations.Add(RationTier.Cheap, 20);
                team.Rations.Clear();
                team.Rations.Add(RationTier.Cheap, 2);
                rich.Rations.Clear();
                rich.Rations.Add(RationTier.Cheap, 10);
            });

            List<SimEvent> events = quests.DoEvents(ctx => Assert.IsTrue(RationService.EatOnQuest(ctx, hungry)));
            Assert.AreEqual(1, team.Rations.Total, "Командный отдаёт первым");
            Assert.AreEqual(20, greedy.Rations.Total);
            SimEvent shared = events.Single(e => e.Type == SimEventType.RationsShared);
            CollectionAssert.AreEqual(new[] { team.Id, hungry.Id }, shared.Participants);
            Assert.Greater(quests.Simulation.World.Relations.GetValue(team.Id, hungry.Id), 0f, "отношения растут");

            quests.World.Do(ctx => team.Rations.Clear());
            quests.World.Do(ctx => RationService.EatOnQuest(ctx, hungry));
            Assert.AreEqual(9, rich.Rations.Total, "дальше — у кого больше, кроме Жадного");
            quests.World.Do(ctx => rich.Rations.Clear());
            events = quests.DoEvents(ctx => Assert.IsFalse(RationService.EatOnQuest(ctx, hungry), "Жадный не делится"));
            Assert.AreEqual(20, greedy.Rations.Total);
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.QuestMealMissed));
            Assert.IsFalse(events.Any(e => e.Type == SimEventType.RationsRanOut), "у группы ещё есть пайки — у Жадного");
            Assert.AreEqual(QuestPhase.TravelOut, run.Phase);
        }

        [Test]
        public void NoRations_Delay_Starves_RanOutOnce()
        {
            quests = new QuestWorld();
            Adventurer a = quests.Add();
            Order order = quests.AddOrder(distance: OrderDistance.Far);
            QuestRun run = quests.Start(order, a);
            quests.World.Do(ctx => a.Rations.Clear());
            List<SimEvent> events = quests.World.Collect(() =>
            {
                for (int i = 0; i < 60 && run.Phase == QuestPhase.TravelOut; i++) quests.Simulation.Tick();
            });
            Assert.AreEqual(1, events.Count(e => e.Type == SimEventType.RationsRanOut), "строка «припасы кончились» — раз");
            Assert.Greater(events.Count(e => e.Type == SimEventType.QuestMealMissed), 1);
            Assert.Less(a.State.Satiety, 100f - 20f, "голодает в пути");
        }

        [Test]
        public void Loss_EachStockLosesShare_AtLeastOne()
        {
            void Lose(out int left, out int shared)
            {
                using var world = new QuestWorld(11u);
                Adventurer a = world.Add();
                Adventurer b = world.Add();
                Party party = world.MakePermanent(a, b);
                Order order = world.AddOrder();
                world.World.Do(ctx => party.OrderId = order.Id);
                QuestRun run = world.StartParty(order, party, a, b);
                world.World.Do(ctx =>
                {
                    party.Rations.Clear();
                    a.Rations.Add(RationTier.Cheap, 8);
                    b.Rations.Add(RationTier.Cheap, 1);
                    party.Rations.Add(RationTier.Regular, 20);
                });
                world.Data.Set("food.rationLossChance", 1f);
                world.World.Do(ctx => RationService.TryLose(ctx, run));
                left = a.Rations.Total + b.Rations.Total;
                shared = party.Rations.Total;
            }

            Lose(out int personal, out int common);
            Assert.AreEqual(6 + 0, personal, "8 → 6 (−25%), 1 → 0 (хотя бы 1)");
            Assert.AreEqual(15, common, "общий 20 → 15");
        }

        [Test]
        public void Loss_EmptyStock_SpendsNoRandomNumbers()
        {
            uint NextAfter(bool lose)
            {
                using var world = new QuestWorld(5u);
                Adventurer a = world.Add();
                QuestRun run = world.Start(world.AddOrder(), a);
                world.World.Do(ctx => a.Rations.Clear());
                return world.World.Do(ctx =>
                {
                    if (lose) RationService.TryLose(ctx, run);
                    return ctx.Rng.NextUInt();
                });
            }

            Assert.AreEqual(NextAfter(false), NextAfter(true), "без пайков нечего терять — броска нет");
        }

        [Test]
        public void Death_RationsSplitAmongLiving_PermanentMemberToShared()
        {
            quests = new QuestWorld();
            Adventurer victim = quests.Add();
            Adventurer b = quests.Add();
            Adventurer c = quests.Add();
            QuestRun run = quests.Start(quests.AddOrder(), victim, b, c);
            quests.World.Do(ctx =>
            {
                foreach (Adventurer person in new[] { victim, b, c }) person.Rations.Clear();
                victim.Rations.Add(RationTier.Cheap, 5);
                victim.Rations.Add(RationTier.Regular, 2);
                QuestRounds.Kill(ctx, run, victim, everyone: false);
            });
            Assert.AreEqual(2, b.Rations.Cheap);
            Assert.AreEqual(1, b.Rations.Regular);
            Assert.AreEqual(3, c.Rations.Total, "остаток от деления пропадает");

            quests.Dispose();
            quests = new QuestWorld();
            Adventurer m1 = quests.Add();
            Adventurer m2 = quests.Add();
            Adventurer m3 = quests.Add();
            Party party = quests.MakePermanent(m1, m2, m3);
            Order order = quests.AddOrder();
            quests.World.Do(ctx => party.OrderId = order.Id);
            QuestRun permanentRun = quests.StartParty(order, party, m1, m2, m3);
            int sharedBefore = party.Rations.Total;
            quests.World.Do(ctx =>
            {
                m1.Rations.Add(RationTier.Cheap, 4);
                QuestRounds.Kill(ctx, permanentRun, m1, everyone: false);
            });
            Assert.AreEqual(sharedBefore + 4, party.Rations.Total, "член постоянной группы — в общий запас");
        }

        [Test]
        public void ShortForReturn_PushesRetreat()
        {
            quests = new QuestWorld();
            Adventurer a = quests.Add();
            QuestRun run = quests.Start(quests.AddOrder(distance: OrderDistance.Far), a);
            quests.AtSite(run, 1);
            Assert.IsFalse(quests.World.Do(ctx => RationService.IsShortForReturn(ctx, run, new List<Adventurer> { a })), "пайков хватает");
            quests.World.Do(ctx => a.Rations.Clear());
            Assert.IsTrue(quests.World.Do(ctx => RationService.IsShortForReturn(ctx, run, new List<Adventurer> { a })), "пайков нет");

            var writer = new StringWriter();
            using var logged = new QuestWorld(log: new SimLogger(SimLogLevel.Info, writer));
            logged.Data.Set("decisions.bestChoiceChance", 1f);
            logged.Data.Set("food.hungryRetreatBonus", 10f);
            Adventurer b = logged.Add();
            Order order = logged.AddOrder(distance: OrderDistance.Far);
            QuestRun hungry = logged.Start(order, b);
            logged.World.Do(ctx => b.Rations.Clear());
            logged.AtSite(hungry, 1);
            bool keepGoing = logged.World.Do(ctx => QuestChoices.DecideAfterFailure(ctx, hungry, order));
            Assert.IsFalse(keepGoing, "мало еды — отступают");
            StringAssert.Contains("because food 10", writer.ToString());
        }

        // ---------- Постоянная группа ----------

        [Test]
        public void PermanentParty_Join_Leave_Disband_ShareStock()
        {
            quests = new QuestWorld();
            Adventurer a = quests.Add();
            Adventurer b = quests.Add();
            Adventurer c = quests.Add();
            quests.World.Do(ctx =>
            {
                a.Rations.Add(RationTier.Cheap, 4);
                b.Rations.Add(RationTier.Regular, 5);
                c.Rations.Add(RationTier.Cheap, 3);
            });
            Party party = quests.MakePermanent(a, b, c);
            Assert.AreEqual(12, party.Rations.Total, "вступившие отдают личный запас в общий");
            Assert.AreEqual(0, a.Rations.Total + b.Rations.Total + c.Rations.Total);

            quests.World.Do(ctx => PartyService.Separate(ctx, a, b));
            Assert.AreEqual(2, b.Rations.Cheap + a.Rations.Cheap, "ушедший уносит долю: 7 дешёвых / 3");
            Assert.AreEqual(1, b.Rations.Regular + a.Rations.Regular, "и 5 обычных / 3");
            Assert.AreEqual(9, party.Rations.Total);

            Adventurer stayed = party.HasMember(a.Id) ? a : b;
            quests.World.Do(ctx => PartyService.OnRetired(ctx, c));
            Assert.IsFalse(quests.Simulation.World.Parties.TryGetParty(party.Id, out _), "остался один — распалась");
            Assert.AreEqual(9, stayed.Rations.Total, "распад: общий запас — оставшемуся");
        }

        // ---------- Город ----------

        [Test]
        public void Town_NoMoney_EatsRation_FromSharedIfPermanent()
        {
            quests = new QuestWorld();
            Adventurer a = quests.Add();
            Adventurer b = quests.Add();
            Adventurer loner = quests.Add();
            Party party = quests.MakePermanent(a, b);
            quests.World.Do(ctx =>
            {
                a.State.Wallet = 0;
                loner.State.Wallet = 0;
                party.Rations.Add(RationTier.Cheap, 3);
                loner.Rations.Add(RationTier.Regular, 1);
                a.State.Satiety = 20f;
                FoodService.Eat(ctx, a);
                FoodService.Eat(ctx, loner);
            });
            Assert.AreEqual(2, party.Rations.Total, "член постоянной группы — из общего");
            Assert.AreEqual(0, loner.Rations.Total);
            Assert.IsTrue(a.State.LastMealWasRation);
            Assert.AreEqual(45f, a.State.Satiety, 1e-4f, "сытость — как у пайка");
            Assert.AreEqual(0, quests.Simulation.World.Treasury.TavernMeals.Sum(), "в доход таверны не идёт");
        }

        // ---------- Оценка заказа ----------

        [Test]
        public void OrderValue_RationCostSubtracted_OwnStockCounts()
        {
            quests = new QuestWorld();
            Adventurer a = quests.Add();
            a.State.Wallet = 70;
            Order order = quests.AddOrder(distance: OrderDistance.Far, reward: 100);
            float share = QuestChoices.ExpectedShare(order, 0f, 1);
            float withoutStock = QuestChoices.MoneyScore(a, order, share, 1f, quests.Registry, 0);
            float withStock = QuestChoices.MoneyScore(a, order, share, 1f, quests.Registry, 100);
            Assert.Less(withoutStock, withStock, "без запаса заказ «весит» меньше на цену пайков");
            int cost = RationRules.OrderCost(a, order, quests.Registry, 0);
            Assert.Greater(cost, 0);
            Assert.AreEqual(System.Math.Min(1f, (share - cost) / QuestChoices.ExpenseScale(a, quests.Registry)), withoutStock, 1e-4f);
        }
    }

    public sealed class RationDeterminismTests
    {
        [Test]
        public void SameSeed_SameLog_WithRations()
        {
            using var data = new PeopleData();
            string Run()
            {
                var writer = new StringWriter();
                HeadlessRun.Result result = HeadlessRun.Run(data.Registry, 23u, 60, PlayerBots.Simple(), new SimLogger(SimLogLevel.Debug, writer));
                return writer + PeopleDump.Of(result.Simulation);
            }

            string first = Run();
            StringAssert.Contains("rations quest #", first);
            StringAssert.Contains("ration #", first);
            Assert.AreEqual(first, Run());
        }
    }
}
