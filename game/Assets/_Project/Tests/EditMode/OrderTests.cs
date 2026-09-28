using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using GuildMaster.Core;
using GuildMaster.Data;
using GuildMaster.Debugging;
using NUnit.Framework;

namespace GuildMaster.Tests
{
    /// <summary>Генерация заказа: ранги, «сложный не по времени», тип, расстояние, профиль, награда, описание, важность.</summary>
    public sealed class OrderGenerationTests
    {
        private PeopleData data;

        [SetUp]
        public void SetUp() => data = new PeopleData();

        [TearDown]
        public void TearDown() => data.Dispose();

        private List<Order> Generate(int count, float reputation, uint seed = 1u)
        {
            var rng = new Rng(seed);
            var errors = new List<string>();
            var orders = new List<Order>(count);
            for (int i = 0; i < count; i++)
            {
                orders.Add(OrderGenerator.Generate(rng, data.Registry, reputation, i + 1, 0, null, errors));
                Assert.IsEmpty(errors, "описание без ошибок подстановки");
            }
            return orders;
        }

        /// <summary>Ожидаемая доля рангов: смесь репутации, с шансом «сложного» — ранг выше на бонус (не выше C).</summary>
        private double[] ExpectedRanks(float reputation)
        {
            OrdersBalance balance = data.Balance.Orders;
            IReadOnlyList<float> weights = OrderGenerator.RankMixFor(balance, reputation).Weights;
            double total = weights.Sum();
            var shares = new double[Vocabulary.RankCount];
            for (int rank = 0; rank < weights.Count; rank++)
            {
                double share = weights[rank] / total;
                shares[rank] += share * (1 - balance.HardEarlyChance);
                shares[Math.Min((int)GuildRank.C, rank + balance.HardEarlyRankBonus)] += share * balance.HardEarlyChance;
            }
            return shares;
        }

        [TestCase(5f)]
        [TestCase(30f)]
        [TestCase(50f)]
        [TestCase(80f)]
        public void RankMix_FollowsReputation(float reputation)
        {
            const int count = 20000;
            List<Order> orders = Generate(count, reputation);
            double[] expected = ExpectedRanks(reputation);
            for (int rank = 0; rank < Vocabulary.RankCount; rank++)
            {
                int actual = orders.Count(o => (int)o.Rank == rank);
                float tolerance = Math.Max(1f, Frequency.Tolerance(count, (float)expected[rank]));
                TestContext.WriteLine($"rep {reputation} {(GuildRank)rank}: {actual} expected {expected[rank] * count:0}");
                Assert.That(actual, Is.EqualTo(expected[rank] * count).Within(tolerance), $"{(GuildRank)rank}");
            }
        }

        [Test]
        public void RankMix_RowIsChosenByReputationThreshold()
        {
            OrdersBalance balance = data.Balance.Orders;
            Assert.AreEqual(0, OrderGenerator.RankMixFor(balance, 0f).FromReputation);
            Assert.AreEqual(0, OrderGenerator.RankMixFor(balance, 19.9f).FromReputation);
            Assert.AreEqual(20, OrderGenerator.RankMixFor(balance, 20f).FromReputation);
            Assert.AreEqual(60, OrderGenerator.RankMixFor(balance, 100f).FromReputation);
        }

        [Test]
        public void RankMix_EasyOrdersNeverVanish()
        {
            foreach (RankMixEntry entry in data.Balance.Orders.RankMix)
            {
                Assert.Greater(entry.Weights[(int)GuildRank.G], 0f, $"G при репутации от {entry.FromReputation}");
                Assert.Greater(entry.Weights[(int)GuildRank.F], 0f, $"F при репутации от {entry.FromReputation}");
            }
        }

        [Test]
        public void HardEarly_FivePercent_RankTwoAboveMix()
        {
            const int count = 20000;
            List<Order> orders = Generate(count, 5f);
            int hard = orders.Count(o => o.IsHardEarly);
            float chance = data.Balance.Orders.HardEarlyChance;
            Assert.That(hard, Is.EqualTo(chance * count).Within(Frequency.Tolerance(count, chance)));
            // При репутации 5 выпадают G и F: «сложный» — E или D, обычный — G или F.
            Assert.IsTrue(orders.Where(o => o.IsHardEarly).All(o => o.Rank == GuildRank.E || o.Rank == GuildRank.D));
            Assert.IsTrue(orders.Where(o => !o.IsHardEarly).All(o => o.Rank <= GuildRank.F));
        }

        [Test]
        public void TypeAndDistance_Shares()
        {
            const int count = 20000;
            List<Order> orders = Generate(count, 5f);
            IReadOnlyList<QuestTypeDefinition> types = data.Registry.All<QuestTypeDefinition>();
            float totalWeight = types.Sum(t => t.GenerationWeight);
            foreach (QuestTypeDefinition type in types)
            {
                float share = type.GenerationWeight / totalWeight;
                Assert.That(orders.Count(o => o.TypeId == type.Id), Is.EqualTo(share * count).Within(Frequency.Tolerance(count, share)), type.Id);
            }
            float far = data.Balance.Orders.FarChance;
            Assert.That(orders.Count(o => o.Distance == OrderDistance.Far), Is.EqualTo(far * count).Within(Frequency.Tolerance(count, far)));
        }

        [Test]
        public void Profile_FollowsTypeTemplate_AllAxesAtLeastFloor()
        {
            var rng = new Rng(7u);
            OrdersBalance orders = data.Balance.Orders;
            float floor = orders.RequirementFloor;
            foreach (QuestTypeDefinition type in data.Registry.All<QuestTypeDefinition>())
            foreach (GuildRank rank in Enum.GetValues(typeof(GuildRank)))
            foreach (OrderDistance distance in Enum.GetValues(typeof(OrderDistance)))
            {
                RankEntry ranks = data.Balance.Ranks.For(rank);
                var secondary = new HashSet<StatId>(type.SecondaryAxes);
                if (distance == OrderDistance.Far && !type.MainAxes.Contains(StatId.Survival)) secondary.Add(StatId.Survival);

                for (int n = 0; n < 200; n++)
                {
                    float[] profile = OrderGenerator.BuildProfile(rng, data.Registry, type, rank, distance);
                    for (int i = 0; i < Vocabulary.StatCount; i++)
                    {
                        var stat = (StatId)i;
                        float value = profile[i];
                        string what = $"{type.Id} {rank} {distance} {stat}={value}";
                        if (stat == StatId.Cohesion)
                        {
                            Assert.AreEqual(0f, value, what + ": Слаженность — не ось диаграммы");
                            continue;
                        }
                        Assert.GreaterOrEqual(value, floor, what);
                        IntRange range = type.MainAxes.Contains(stat) ? ranks.MainAxes : secondary.Contains(stat) ? ranks.SecondaryAxes : default;
                        if (type.MainAxes.Contains(stat) || secondary.Contains(stat))
                        {
                            Assert.GreaterOrEqual(value, Math.Max(floor, range.Min * orders.AxisVariance.Min) - 1e-3f, what);
                            Assert.LessOrEqual(value, Math.Max(floor, range.Max * orders.AxisVariance.Max) + 1e-3f, what);
                        }
                        else
                        {
                            Assert.AreEqual(floor, value, what + ": ось вне шаблона — порог");
                        }
                    }
                }
            }
        }

        [Test]
        public void Reward_ByRank_FarTimesOneAndHalf_RoundedToFive()
        {
            var rng = new Rng(3u);
            OrdersBalance orders = data.Balance.Orders;
            foreach (GuildRank rank in Enum.GetValues(typeof(GuildRank)))
            foreach (OrderDistance distance in Enum.GetValues(typeof(OrderDistance)))
            {
                IntRange range = data.Balance.Ranks.For(rank).Reward;
                float multiplier = distance == OrderDistance.Far ? orders.FarRewardMultiplier : 1f;
                for (int n = 0; n < 300; n++)
                {
                    int reward = OrderGenerator.Reward(rng, data.Registry, rank, distance);
                    Assert.AreEqual(0, reward % orders.RewardRounding, $"{reward}");
                    Assert.GreaterOrEqual(reward, range.Min * multiplier - orders.RewardRounding / 2f, $"{rank} {distance}");
                    Assert.LessOrEqual(reward, range.Max * multiplier + orders.RewardRounding / 2f, $"{rank} {distance}");
                }
            }
        }

        [Test]
        public void Description_HintsTheLargestAxes_FarHasFarHint()
        {
            List<Order> orders = Generate(3000, 50f, 11u);
            OrderTextTemplates texts = data.Registry.OrderTexts;
            IntRange hintCount = data.Balance.Orders.HintCount;
            int firstOnMain = 0;
            foreach (Order order in orders)
            {
                Assert.That(order.HintStats.Count, Is.InRange(hintCount.Min, hintCount.Max), order.Description);
                CollectionAssert.AreEqual(OrderGenerator.LargestAxes(order, order.HintStats.Count), order.HintStats, order.Description);
                foreach (StatId stat in order.HintStats)
                {
                    IReadOnlyList<string> lines = texts.Hints.First(h => h.Stat == stat).Lines;
                    Assert.IsTrue(lines.Any(line => order.Description.Contains(line)), $"намёк на {stat}: {order.Description}");
                }
                Assert.AreEqual(order.Distance == OrderDistance.Far, order.Description.Contains(texts.FarHint), order.Description);
                Assert.IsFalse(order.Description.Contains("{") || order.Description.Contains("["), order.Description);
                Assert.IsTrue(char.IsUpper(order.Description[0]), order.Description);

                QuestTypeDefinition type = data.Registry.Get<QuestTypeDefinition>(order.TypeId);
                if (type.MainAxes.Contains(order.HintStats[0])) firstOnMain++;
            }
            TestContext.WriteLine($"first hint on a main axis: {firstOnMain} of {orders.Count}; e.g. {orders[0].Description}");
            Assert.That(firstOnMain, Is.GreaterThan(orders.Count * 0.95), "самая большая ось — почти всегда главная ось типа");
        }

        [Test]
        public void Description_SameHintLineIsNotRepeated()
        {
            // У Силы и Выносливости один и тот же намёк «Их там целая стая» — в описании он один раз.
            foreach (Order order in Generate(3000, 50f, 13u))
            {
                string[] sentences = order.Description.TrimEnd('.').Split(new[] { ". " }, StringSplitOptions.None);
                Assert.AreEqual(sentences.Length, sentences.Distinct().Count(), order.Description);
            }
        }

        [Test]
        public void Important_IsHardEarlyOrRewardFromThreshold()
        {
            int threshold = data.Balance.Orders.ImportantRewardThreshold;
            List<Order> orders = Generate(10000, 30f, 5u);
            Assert.IsTrue(orders.All(o => o.IsImportant == (o.IsHardEarly || o.Reward >= threshold)));
            Assert.IsTrue(orders.Any(o => o.IsImportant && !o.IsHardEarly), "есть важные по награде");
            Assert.IsTrue(orders.Any(o => o.IsImportant && o.IsHardEarly), "есть «сложные не по времени»");
        }

        [Test]
        public void Accuracy_InRange()
        {
            FloatRange range = data.Balance.Orders.DescriptionAccuracy;
            List<Order> orders = Generate(2000, 5f);
            Assert.IsTrue(orders.All(o => o.DescriptionAccuracy >= range.Min && o.DescriptionAccuracy <= range.Max));
            Assert.Greater(orders.Select(o => o.DescriptionAccuracy).Distinct().Count(), 100);
        }

        [Test]
        public void HiddenFields_AreNotVisibleOutsideCore()
        {
            string[] hidden = { "Profile", "Requirement", "SetRequirement", "PartySizeCeiling", "StatCeiling", "SetStatCeiling", "DescriptionAccuracy" };
            MemberInfo[] members = typeof(Order).GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
            foreach (string name in hidden)
            {
                Assert.IsFalse(members.Any(m => m.Name == name), $"Order.{name} виден снаружи");
            }
            Assert.IsFalse(members.OfType<PropertyInfo>().Any(p => p.PropertyType == typeof(float[])), "профиль массивом наружу не отдаётся");
        }
    }

    /// <summary>Заказы в симуляции: старт, утро, Регистратор, важные, доплата, снятие по сроку, репутация, отчёт, детерминизм.</summary>
    public sealed class OrderBoardTests
    {
        private PeopleData data;

        [SetUp]
        public void SetUp() => data = new PeopleData();

        [TearDown]
        public void TearDown() => data.Dispose();

        private static int OrderId(SimEvent simEvent) => simEvent.TryGet("order", out int id) ? id : 0;

        [Test]
        public void Start_ThreeRankGOrdersOnBoard_WithoutEvents()
        {
            var simulation = Simulation.CreateDefault(data.Registry, 3u);
            IReadOnlyList<Order> open = simulation.World.Orders.Open;
            long start = simulation.Calendar.StartTotalHours;

            Assert.AreEqual(data.Balance.Orders.StartOrders, open.Count);
            foreach (Order order in open)
            {
                Assert.AreEqual(GuildRank.G, order.Rank);
                Assert.AreEqual(OrderStatus.OnBoard, order.Status);
                Assert.IsFalse(order.IsImportant);
                Assert.AreEqual(start, order.PostedAtHours);
                Assert.AreEqual(start + simulation.Calendar.DaysToHours(order.BoardDays), order.ExpiresAtHours);
            }
            Assert.IsEmpty(simulation.Events.Events, "стартовое состояние — не новость");
            Assert.AreEqual(0, simulation.World.Orders.Totals.Arrived);
        }

        [Test]
        public void StartOrders_DoNotShiftStartPeople()
        {
            string with = PeopleDump.Of(Simulation.CreateDefault(data.Registry, 21u));
            data.Set("orders.startOrders", 0);
            var without = Simulation.CreateDefault(data.Registry, 21u);
            Assert.AreEqual(0, without.World.Orders.Open.Count);
            Assert.AreEqual(with, PeopleDump.Of(without));
        }

        [TestCase(5f, 1.5f)]
        [TestCase(35f, 4.5f)]
        public void Orders_ArriveAtMorning_ExpectedCountPerDay(float reputation, float perDay)
        {
            data.Set("guild.startReputation", reputation);
            data.NoQuests(); // задания меняют репутацию, а с ней — число заказов
            data.NoBankruptcy();
            const int days = 400;
            var simulation = Simulation.CreateDefault(data.Registry, 4u);
            List<SimEvent> arrived = SimulationRun.Collect(simulation, sim => SimulationRun.Days(sim, days))
                .Where(e => e.Type == SimEventType.OrderArrived).ToList();

            Assert.IsTrue(arrived.All(e => simulation.Calendar.At(e.TimeHours).Hour == data.Balance.Time.MorningHour), "только утром");
            // Игра начинается в 06:00 первого дня, первый такт — 07:00: утра в прогоне — со второго дня по первое после прогона.
            int mornings = days;
            float fraction = perDay - (float)Math.Floor(perDay);
            float expected = perDay * mornings;
            TestContext.WriteLine($"rep {reputation}: {arrived.Count} orders in {mornings} mornings, expected {expected:0}");
            Assert.That(arrived.Count, Is.EqualTo(expected).Within(Frequency.Tolerance(mornings, fraction)));
            Assert.AreEqual(arrived.Count, simulation.World.Orders.Totals.Arrived);
        }

        [Test]
        public void Registrar_PostsByPlayerRules_DeclinesTheRest_OneFeedLinePerMorning()
        {
            var simulation = Simulation.CreateDefault(data.Registry, 8u);
            simulation.Send(new SetRegistrarRulesCommand(new[] { "Hunt", "Delivery" }, GuildRank.G, 40));
            List<SimEvent> events = SimulationRun.Collect(simulation, sim => SimulationRun.Days(sim, 120));

            List<SimEvent> posted = events.Where(e => e.Type == SimEventType.OrderPosted).ToList();
            List<SimEvent> declined = events.Where(e => e.Type == SimEventType.OrderDeclinedByRegistrar).ToList();
            Assert.IsNotEmpty(posted);
            Assert.IsNotEmpty(declined);
            foreach (SimEvent simEvent in posted)
            {
                simEvent.TryGet("questType", out string type);
                simEvent.TryGet("rank", out GuildRank rank);
                simEvent.TryGet("reward", out int reward);
                Assert.IsTrue((type == "Hunt" || type == "Delivery") && rank == GuildRank.G && reward >= 40, simEvent.ToLogLine(simulation.Calendar));
            }
            foreach (SimEvent simEvent in declined)
            {
                simEvent.TryGet("questType", out string type);
                simEvent.TryGet("rank", out GuildRank rank);
                simEvent.TryGet("reward", out int reward);
                Assert.IsFalse((type == "Hunt" || type == "Delivery") && rank == GuildRank.G && reward >= 40, simEvent.ToLogLine(simulation.Calendar));
            }

            // Одна строка ленты на утро: сколько повешено и сколько отклонено.
            int postedLines = events.Where(e => e.Type == SimEventType.NewOrdersPosted).Sum(e => e.TryGet("count", out int n) ? n : 0);
            int declinedLines = events.Where(e => e.Type == SimEventType.RegistrarDeclinedOrders).Sum(e => e.TryGet("count", out int n) ? n : 0);
            Assert.AreEqual(posted.Count, postedLines);
            Assert.AreEqual(declined.Count, declinedLines);
            IReadOnlyList<FeedEntry> feed = simulation.World.Feed.Guild;
            Assert.IsTrue(feed.Any(f => f.TemplateKey == FeedKeys.NewOrders && f.Text.StartsWith("Новых заказов на доске: ")));
            Assert.IsTrue(feed.Any(f => f.TemplateKey == FeedKeys.RegistrarDeclined && f.Text.StartsWith("Регистратор отказал заказчикам: ")));
        }

        [Test]
        public void ImportantOrder_GoesToPlayerDespiteRules_Autopause_DeclinedWithoutAnswerAfterTwoDays()
        {
            data.Set("orders.hardEarlyChance", 1f); // все заказы — «сложные не по времени», значит важные
            var simulation = Simulation.CreateDefault(data.Registry, 9u);
            simulation.Send(new SetRegistrarRulesCommand(new string[0], GuildRank.G, 0)); // Регистратор не берёт ничего

            var events = new List<SimEvent>();
            var pausedAt = new List<long>();
            simulation.TickCompleted += tickEvents => events.AddRange(tickEvents);
            for (int i = 0; i < simulation.Calendar.DaysToHours(10); i++)
            {
                simulation.Tick();
                if (simulation.ConsumePauseRequest()) pausedAt.Add(simulation.World.Time.TotalHours);
            }

            List<SimEvent> awaiting = events.Where(e => e.Type == SimEventType.OrderAwaitingPlayer).ToList();
            Assert.IsNotEmpty(awaiting);
            Assert.IsFalse(events.Any(e => e.Type == SimEventType.OrderDeclinedByRegistrar), "важные мимо правил Регистратора");
            Assert.IsTrue(awaiting.All(e => e.Importance == EventImportance.Important));
            Assert.IsTrue(awaiting.All(e => pausedAt.Contains(e.TimeHours)), "автопауза на важный заказ");

            long wait = simulation.Calendar.DaysToHours(data.Balance.Orders.PlayerResponseDays);
            List<SimEvent> noAnswer = events.Where(e => e.Type == SimEventType.OrderDeclinedByPlayer).ToList();
            foreach (SimEvent arrival in awaiting.Where(e => e.TimeHours + wait <= simulation.World.Time.TotalHours))
            {
                SimEvent declined = noAnswer.Single(e => OrderId(e) == OrderId(arrival));
                Assert.AreEqual(arrival.TimeHours + wait, declined.TimeHours);
                Assert.IsTrue(declined.TryGet("noAnswer", out bool flag) && flag);
                Assert.IsTrue(simulation.World.Orders.TryGetOrder(OrderId(arrival), out Order order));
                Assert.AreEqual(OrderStatus.Declined, order.Status);
                Assert.AreEqual(OrderDeclinedBy.NoAnswer, order.DeclinedBy);
            }
            Assert.IsTrue(simulation.World.Feed.Guild.Any(f => f.TemplateKey == FeedKeys.AwaitingPlayer && f.Importance == EventImportance.Important));
            Assert.IsTrue(simulation.World.Feed.Guild.Any(f => f.TemplateKey == FeedKeys.OrderNoAnswer && f.Text.Contains("не дождался ответа и ушёл")));
            Assert.AreEqual(noAnswer.Count, simulation.World.Orders.Totals.DeclinedByPlayer);
        }

        [Test]
        public void AnswerImportantOrder_AcceptWithSurcharge_BoardTimeFromAcceptance_DeclineWithoutFeedLine()
        {
            data.Set("orders.hardEarlyChance", 1f);
            var simulation = Simulation.CreateDefault(data.Registry, 10u);
            while (simulation.World.Orders.Open.Count(o => o.Status == OrderStatus.AwaitingPlayer) < 2) simulation.Tick();
            for (int i = 0; i < 3; i++) simulation.Tick(); // ответ — через несколько часов после прихода

            List<Order> waiting = simulation.World.Orders.Open.Where(o => o.Status == OrderStatus.AwaitingPlayer).ToList();
            Order accepted = waiting[0];
            Order rejected = waiting[1];
            simulation.Send(new AnswerImportantOrderCommand(accepted.Id, true, 30));
            simulation.Send(new AnswerImportantOrderCommand(rejected.Id, false));
            int feedBefore = simulation.World.Feed.Guild.Count(f => f.TemplateKey == FeedKeys.OrderNoAnswer);
            List<SimEvent> events = SimulationRun.Collect(simulation, sim => sim.Tick());
            long now = simulation.World.Time.TotalHours - 1; // команды применяются в начале такта, до сдвига часа

            Assert.AreEqual(OrderStatus.OnBoard, accepted.Status);
            Assert.AreEqual(30, accepted.Surcharge);
            Assert.AreEqual(now, accepted.PostedAtHours);
            Assert.AreEqual(now + simulation.Calendar.DaysToHours(accepted.BoardDays), accepted.ExpiresAtHours, "срок на доске — от принятия");
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.OrderPosted && OrderId(e) == accepted.Id && e.TryGet("by", out string by) && by == "player"));

            Assert.AreEqual(OrderStatus.Declined, rejected.Status);
            Assert.AreEqual(OrderDeclinedBy.Player, rejected.DeclinedBy);
            Assert.AreEqual(feedBefore, simulation.World.Feed.Guild.Count(f => f.TemplateKey == FeedKeys.OrderNoAnswer), "отказ игрока строки не даёт");

            // Повторный ответ — ничего.
            simulation.Send(new AnswerImportantOrderCommand(rejected.Id, true));
            simulation.Tick();
            Assert.AreEqual(OrderStatus.Declined, rejected.Status);
        }

        [Test]
        public void Surcharge_SetByCommand_StoredInOrder_NotTakenFromTreasury()
        {
            data.NoQuests();
            var simulation = Simulation.CreateDefault(data.Registry, 12u);
            Order order = simulation.World.Orders.Open[0];
            int money = simulation.World.Treasury.Money;

            simulation.Send(new SetSurchargeCommand(order.Id, 25));
            List<SimEvent> events = SimulationRun.Collect(simulation, sim => sim.Tick());
            Assert.AreEqual(25, order.Surcharge);
            Assert.AreEqual(25, simulation.World.Orders.GetPromisedSurcharges());
            Assert.AreEqual(money, simulation.World.Treasury.Money, "доплата — обещание");
            Assert.AreEqual(1, events.Count(e => e.Type == SimEventType.SurchargeSet));

            simulation.Send(new SetSurchargeCommand(order.Id, 25));
            Assert.IsFalse(SimulationRun.Collect(simulation, sim => sim.Tick()).Any(e => e.Type == SimEventType.SurchargeSet), "та же сумма — без события");

            simulation.Send(new SetSurchargeCommand(order.Id, -10));
            simulation.Tick();
            Assert.AreEqual(0, order.Surcharge, "меньше нуля — 0");

            simulation.Send(new SetSurchargeCommand(999, 50));
            simulation.Tick();
            Assert.AreEqual(0, simulation.World.Orders.GetPromisedSurcharges());
        }

        [Test]
        public void Expired_RemovedFromBoard_OnTime_WithFeedLineOfType()
        {
            data.NoQuests();
            var simulation = Simulation.CreateDefault(data.Registry, 14u);
            List<Order> start = simulation.World.Orders.Open.ToList();
            List<SimEvent> events = SimulationRun.Collect(simulation, sim => SimulationRun.Days(sim, 8));

            foreach (Order order in start)
            {
                Assert.AreEqual(OrderStatus.Expired, order.Status);
                SimEvent expired = events.Single(e => e.Type == SimEventType.OrderExpired && OrderId(e) == order.Id);
                Assert.AreEqual(order.ExpiresAtHours, expired.TimeHours);
                Assert.AreEqual(order.ExpiresAtHours, order.ClosedAtHours);
                Assert.IsFalse(simulation.World.Orders.TryGetOpen(order.Id, out _));
                Assert.IsTrue(simulation.World.Orders.Closed.Contains(order));
            }
            List<FeedEntry> lines = simulation.World.Feed.Guild.Where(f => f.TemplateKey == FeedKeys.OrderExpired).ToList();
            Assert.AreEqual(events.Count(e => e.Type == SimEventType.OrderExpired), lines.Count);
            Assert.IsTrue(lines.All(f => f.Text.Contains("сняли — никто не взял") && f.Text.StartsWith("Заказ на ")), string.Join("\n", lines.Select(f => f.Text)));
        }

        [Test]
        public void ClosedOrders_KeptUpToLimit()
        {
            data.Set("orders.closedOrdersLimit", 10);
            var simulation = Simulation.CreateDefault(data.Registry, 15u);
            SimulationRun.Days(simulation, 60);
            Assert.AreEqual(10, simulation.World.Orders.Closed.Count);
            Assert.Greater(simulation.World.Orders.Totals.Expired, 10);
        }

        [Test]
        public void Reputation_ClampedAndLogged()
        {
            var simulation = Simulation.CreateDefault(data.Registry, 16u);
            Assert.AreEqual(data.Balance.Guild.StartReputation, simulation.World.Guild.Reputation);

            float changed = 0f;
            SimulationRun.Do(simulation, ctx => changed = ReputationService.Change(ctx, 500f, "test"));
            Assert.AreEqual(100f, simulation.World.Guild.Reputation);
            Assert.AreEqual(95f, changed, 1e-4f);
            SimEvent simEvent = simulation.Events.Events.Single(e => e.Type == SimEventType.ReputationChanged);
            Assert.IsTrue(simEvent.TryGet("reason", out string reason) && reason == "test");

            SimulationRun.Do(simulation, ctx => ReputationService.Change(ctx, -500f, "test"));
            Assert.AreEqual(0f, simulation.World.Guild.Reputation);
            SimulationRun.Do(simulation, ctx => changed = ReputationService.Change(ctx, -1f, "test"));
            Assert.AreEqual(0f, changed);
            Assert.AreEqual(2, simulation.Events.Events.Count(e => e.Type == SimEventType.ReputationChanged), "без изменения — без события");
        }

        [Test]
        public void CandidateChance_FollowsCurrentReputation()
        {
            const int days = 1500;
            data.NoQuests(); // репутация стоит на месте
            data.NoBankruptcy();
            var simulation = Simulation.CreateDefault(data.Registry, 17u);
            SimulationRun.Do(simulation, ctx => ReputationService.Change(ctx, 55f, "test"));
            float chance = RecruitSystem.CandidateChance(data.Registry, simulation.World.Guild.Reputation);
            Assert.AreEqual(0.21f, chance, 1e-5f);

            int arrived = SimulationRun.Collect(simulation, sim => SimulationRun.Days(sim, days)).Count(e => e.Type == SimEventType.CandidateArrived);
            TestContext.WriteLine($"{arrived} candidates in {days} days at reputation 60, expected {chance * days:0}");
            Assert.That(arrived, Is.EqualTo(chance * days).Within(Frequency.Tolerance(days, chance)));
        }

        [Test]
        public void MonthReport_OrdersAndReputationSections_MatchEvents()
        {
            data.NoQuests();
            var simulation = Simulation.CreateDefault(data.Registry, 18u);
            List<SimEvent> events = SimulationRun.Collect(simulation, sim => SimulationRun.Days(sim, 61));
            IReadOnlyList<MonthReport> reports = simulation.World.Reports.Reports;
            Assert.AreEqual(2, reports.Count);

            long from = long.MinValue;
            foreach (MonthReport report in reports)
            {
                long to = report.ToHours;
                int Count(SimEventType type) => events.Count(e => e.Type == type && e.TimeHours > from && e.TimeHours <= to);

                Assert.IsTrue(report.TryGetSection(MonthReportSections.OrdersTitle, out ReportSection orders));
                Assert.AreEqual(Count(SimEventType.OrderArrived), Line(orders, MonthReportSections.OrdersArrived).Amount);
                Assert.AreEqual(Count(SimEventType.OrderPosted), Line(orders, MonthReportSections.OrdersPosted).Amount);
                Assert.AreEqual(Count(SimEventType.OrderDeclinedByRegistrar), Line(orders, MonthReportSections.OrdersDeclinedByRegistrar).Amount);
                Assert.AreEqual(Count(SimEventType.OrderDeclinedByPlayer), Line(orders, MonthReportSections.OrdersDeclinedByPlayer).Amount);
                Assert.AreEqual(Count(SimEventType.OrderExpired), Line(orders, MonthReportSections.OrdersExpired).Amount);

                Assert.IsTrue(report.TryGetSection(MonthReportSections.ReputationTitle, out ReportSection reputation));
                ReportLine change = Line(reputation, MonthReportSections.ReputationChange);
                Assert.IsTrue(change.HasChange);
                Assert.AreEqual(5f, change.From);
                Assert.AreEqual(5f, change.To);
                from = to;
            }

            List<string> lines = MonthReportText.Lines(reports[0], simulation.World, data.Registry);
            TestContext.WriteLine(string.Join("\n", lines));
            CollectionAssert.Contains(lines, "Заказы");
            CollectionAssert.Contains(lines, "За месяц: 5 → 5");
            Assert.IsTrue(lines.Any(l => l.StartsWith("Пришло: ")));
        }

        private static ReportLine Line(ReportSection section, string key)
        {
            Assert.IsTrue(section.TryGetLine(key, out ReportLine line), key);
            return line;
        }
    }

    /// <summary>Одно зерно — те же заказы и доска; заказы не сдвигают чужие броски.</summary>
    public sealed class OrderDeterminismTests
    {
        private PeopleData data;

        [SetUp]
        public void SetUp() => data = new PeopleData();

        [TearDown]
        public void TearDown() => data.Dispose();

        private static string Dump(Simulation simulation)
        {
            var text = new StringBuilder();
            OrderBoard board = simulation.World.Orders;
            foreach (Order order in board.Open.Concat(board.Closed).OrderBy(o => o.Id))
            {
                text.Append(order.Id).Append(' ').Append(order.TypeId).Append(' ').Append(order.Rank).Append(' ').Append(order.Distance)
                    .Append(' ').Append(order.Reward).Append(' ').Append(order.Status).Append(' ').Append(order.DeclinedBy)
                    .Append(' ').Append(order.ExpiresAtHours).Append(' ').Append(order.DescriptionAccuracy.ToString("R"));
                for (int i = 0; i < Vocabulary.StatCount; i++) text.Append(' ').Append(order.Requirement((StatId)i).ToString("R"));
                text.Append(' ').Append(order.Description).Append('\n');
            }
            return text.ToString();
        }

        [Test]
        public void SameSeed_SameOrdersBoardAndFeed()
        {
            data.NoQuests(); // заказы доживают до снятия по сроку; детерминизм с заданиями — в тестах заданий
            string Run()
            {
                var simulation = Simulation.CreateDefault(data.Registry, 31u);
                simulation.Send(new SetRegistrarRulesCommand(null, GuildRank.F, 50));
                SimulationRun.Days(simulation, 90);
                return Dump(simulation) + string.Join("\n", simulation.World.Feed.Guild.Select(f => f.Text));
            }

            string first = Run();
            StringAssert.Contains("Expired", first);
            Assert.AreEqual(first, Run());
        }

        [Test]
        public void OrderSystem_DoesNotShiftOtherSystems()
        {
            data.NoQuests(); // без заказов нет и заданий: сравниваем мир, где задания не случаются
            string Run(bool withOrders)
            {
                List<ISimSystem> systems = SimulationSystems.CreateDefault();
                if (!withOrders) systems.RemoveAll(s => s is OrderSystem);
                var simulation = new Simulation(data.Registry, 5u, systems);
                string log = SimulationLog.Record(simulation, sim => SimulationRun.Days(sim, 120));
                string events = string.Join("\n", log.Split('\n').Where(line => !line.Contains("[OrderSystem]")));
                string feed = string.Join("\n", simulation.World.Feed.Guild.Where(f => !f.TemplateKey.StartsWith("guild.board.")).Select(f => f.Text));
                return events + "\n" + feed + "\n" + PeopleDump.Of(simulation);
            }

            string without = Run(withOrders: false);
            StringAssert.Contains("CandidateArrived", without);
            Assert.AreEqual(without, Run(withOrders: true));
        }
    }

    /// <summary>Команды заказов в сценарии, бот «Простой», сводка.</summary>
    public sealed class OrderScenarioTests
    {
        private PeopleData data;

        [SetUp]
        public void SetUp() => data = new PeopleData();

        [TearDown]
        public void TearDown() => data.Dispose();

        [TestCase("registrar all C 0")]
        [TestCase("registrar Escort,Hunt E 50")]
        [TestCase("registrar none G 0")]
        [TestCase("answer 5 accept 30")]
        [TestCase("answer 5 decline")]
        [TestCase("surcharge 3 25")]
        public void Command_ParsesAndFormatsBack(string line)
        {
            string[] parts = line.Split(' ');
            ICommand command = ScenarioCommands.Parse(parts[0], parts.Skip(1).ToArray());
            Assert.IsTrue(ScenarioCommands.TryFormat(command, out string text));
            Assert.AreEqual(line, text);
        }

        [Test]
        public void SimpleBot_RegistrarUpToTopRank_ImportantOrderWaitsForRank_SummaryHasOrders()
        {
            data.Set("orders.hardEarlyChance", 0.2f); // чаще важные заказы
            data.NoQuests(); // репутация и ранги людей не меняются
            HeadlessRun.Result result = HeadlessRun.Run(data.Registry, 41u, 120, PlayerBots.Simple());
            WorldState world = result.Simulation.World;

            Assert.AreEqual(RegistrarUpToTopRankRule.TopRank(world), world.Orders.Rules.MaxRank);
            Assert.IsTrue(world.Orders.Rules.AllTypes);
            Assert.AreEqual(0, world.Orders.Rules.MinReward);
            StringAssert.Contains(" registrar all ", result.Scenario.ToString());
            // Людей рангов E–D нет: бот важные не принимает и не отклоняет — заказчик уходит сам.
            Assert.IsFalse(result.Scenario.ToString().Contains(" decline"), "бот не отклоняет важные заказы");
            Assert.IsFalse(world.Orders.Closed.Any(o => o.DeclinedBy == OrderDeclinedBy.Player));
            Assert.IsTrue(world.Orders.Closed.Any(o => o.DeclinedBy == OrderDeclinedBy.NoAnswer), "важный заказ ждёт и уходит без ответа");

            RunSummary summary = result.Summary;
            List<string> names = summary.MonthColumns.Select(c => c.Name).ToList();
            foreach (string name in new[] { "Заказов пришло", "На доску", "Отклонено Регистратором", "Отклонено игроком", "Снято по сроку", "Репутация" })
                Assert.GreaterOrEqual(names.IndexOf(name), 0, name);
            double arrived = summary.Totals[names.IndexOf("Заказов пришло")];
            Assert.Greater(arrived, 0);
            Assert.LessOrEqual(arrived, world.Orders.Totals.Arrived, "последнее утро прогона — в неполном месяце, которого нет в сводке");
            Assert.AreEqual(5.0, summary.Totals[names.IndexOf("Репутация")], 1e-6);
        }

        [Test]
        public void SimpleBot_AcceptsImportantOrder_WhenSomeoneHasTheRank()
        {
            // «Сложный» — на ранг выше: из G выходит F (в стартовой гильдии есть люди ранга F), из F — E (таких нет).
            data.Set("orders.hardEarlyChance", 0.3f);
            data.Set("orders.hardEarlyRankBonus", 1);
            // Репутация стоит на месте: иначе выполненные задания поднимают ранги заказов выше рангов людей.
            data.Set("guild.successReputationPerRank", 0f);
            data.Set("guild.failureReputationPerRank", 0f);
            data.Set("guild.deathReputation", 0f);
            HeadlessRun.Result result = HeadlessRun.Run(data.Registry, 43u, 60, PlayerBots.Simple());
            OrderBoard board = result.Simulation.World.Orders;
            // Люди растут в ранге по ходу прогона: ранг людей на старте — нижняя граница, на конце — верхняя.
            GuildRank startTop = RegistrarUpToTopRankRule.TopRank(HeadlessRun.Run(data.Registry, 43u, 0, PlayerBots.Simple()).Simulation.World);
            GuildRank endTop = RegistrarUpToTopRankRule.TopRank(result.Simulation.World);
            List<Order> important = board.Open.Concat(board.InWork).Concat(board.Closed)
                .Where(o => o.IsImportant && o.Status != OrderStatus.AwaitingPlayer).ToList();

            string seen = $"ранг людей {startTop}…{endTop}; важные: " + string.Join(", ", important.Select(o => $"#{o.Id} {o.Rank} {o.Status} {o.DeclinedBy}"));
            Assert.IsTrue(important.Any(o => o.Rank <= startTop), "есть важные заказы по силам гильдии; " + seen);
            Assert.IsTrue(important.Where(o => o.Rank <= startTop).All(o => o.PostedAtHours >= 0), "их бот принимает; " + seen);
            Assert.IsTrue(important.Where(o => o.Rank > endTop).All(o => o.DeclinedBy == OrderDeclinedBy.NoAnswer), "остальные ждут и уходят сами; " + seen);
        }

        [Test]
        public void ScenarioReplay_RepeatsOrders()
        {
            HeadlessRun.Result recorded = HeadlessRun.Run(data.Registry, 42u, 60, PlayerBots.Simple());
            ScenarioScript script = ScenarioScript.Parse(recorded.Scenario.ToString());
            HeadlessRun.Result replayed = HeadlessRun.Run(data.Registry, 42u, 60, PlayerBots.Scenario(script));

            string Orders(HeadlessRun.Result r) => string.Join("\n", r.Simulation.World.Orders.Open.Concat(r.Simulation.World.Orders.Closed)
                .OrderBy(o => o.Id).Select(o => $"{o.Id} {o.Status} {o.DeclinedBy} {o.Surcharge} {o.Description}"));
            Assert.AreEqual(Orders(recorded), Orders(replayed));
        }
    }
}
