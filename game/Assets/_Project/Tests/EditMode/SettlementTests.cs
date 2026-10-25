using System.Collections.Generic;
using System.Linq;
using GuildMaster.Core;
using GuildMaster.Data;
using GuildMaster.UI;
using NUnit.Framework;

namespace GuildMaster.Tests
{
    /// <summary>Поселение: старт, Безопасность по итогам заказов, уровни, население, нападения, покидание, доход таверны.</summary>
    public sealed class SettlementTests
    {
        private const float Tolerance = 1e-4f;

        private static SettlementState Settlement(StateWorld world) => world.Simulation.World.Settlement;

        private static void ChangeSecurity(StateWorld world, float delta) =>
            world.Do(ctx => SettlementService.ChangeSecurity(ctx, delta, SettlementReasons.Debug));

        /// <summary>Такты до 00:00 первого числа следующего месяца.</summary>
        private static void TickToMonthStart(StateWorld world)
        {
            do world.Simulation.Tick();
            while (!(world.Time.Day == 1 && world.Time.Hour == 0));
        }

        [Test]
        public void Start_SecurityPopulationLevel_NameFromList()
        {
            using (var data = new PeopleData())
            {
                Simulation simulation = Simulation.CreateDefault(data.Registry, 3u);
                SettlementState settlement = simulation.World.Settlement;
                Assert.AreEqual(60f, settlement.Security);
                Assert.AreEqual(80, settlement.Population);
                Assert.AreEqual(SettlementLevel.Normal, settlement.Level);
                Assert.IsNull(settlement.ZeroSecuritySinceHours);
                Assert.IsEmpty(settlement.Changes);
                CollectionAssert.Contains(data.Registry.Names.SettlementNames, settlement.Name, "название — из списка");
            }
        }

        [Test]
        public void Midnight_DailyDecay_WithReason()
        {
            using (var world = new StateWorld(settlement: true))
            {
                world.TickToHour(0);
                Assert.AreEqual(60f - 0.3f, Settlement(world).Security, Tolerance);
                SettlementChange change = Settlement(world).Changes.Last();
                Assert.AreEqual(SettlementReasons.Night, change.Reason);
                Assert.AreEqual(0, change.OrderId);
            }
        }

        [TestCase("Extermination", GuildRank.E, true, 0.5f * 3)]
        [TestCase("Hunt", GuildRank.G, true, 0.5f * 1)]
        [TestCase("Delivery", GuildRank.D, false, 0.2f * 4)]
        [TestCase("Escort", GuildRank.C, false, 0.2f * 5)]
        public void QuestDone_RaisesSecurity_ByTypeAndRank(string type, GuildRank rank, bool threat, float expected)
        {
            using (var quests = new QuestWorld(settlement: true))
            {
                Order order = quests.AddOrder(type, rank);
                QuestRun run = quests.Start(order, quests.Add());
                quests.World.Do(ctx =>
                {
                    run.Completed = true;
                    QuestSettlement.Finish(ctx, run);
                });
                quests.Simulation.Tick();

                SettlementState settlement = Settlement(quests.World);
                Assert.AreEqual(60f + expected, settlement.Security, Tolerance);
                SettlementChange change = settlement.Changes.Last();
                Assert.AreEqual(threat ? SettlementReasons.ThreatDone : SettlementReasons.OtherDone, change.Reason);
                Assert.AreEqual(order.Id, change.OrderId);
                Assert.AreEqual(type, change.QuestTypeId);
                Assert.AreEqual(threat ? 1 : 0, settlement.Totals.ThreatsDone);
            }
        }

        [Test]
        public void QuestFailed_Lowers_AllDead_LowersMore()
        {
            using (var quests = new QuestWorld(settlement: true))
            {
                Order failed = quests.AddOrder("Hunt", GuildRank.E);
                QuestRun failedRun = quests.Start(failed, quests.Add());
                quests.World.Do(ctx => QuestSettlement.Finish(ctx, failedRun));
                quests.Simulation.Tick();
                Assert.AreEqual(60f - 0.5f * 3, Settlement(quests.World).Security, Tolerance, "не выполнено: − 0,5 × ранг");

                Order lost = quests.AddOrder("Delivery", GuildRank.F);
                Adventurer victim = quests.Add();
                QuestRun lostRun = quests.Start(lost, victim);
                quests.World.Do(ctx =>
                {
                    lostRun.AddDead(victim.Id);
                    QuestSettlement.Finish(ctx, lostRun);
                });
                quests.Simulation.Tick();
                Assert.AreEqual(60f - 1.5f - 0.5f * 2 - 5f, Settlement(quests.World).Security, Tolerance, "погибли все: ещё − 5");
                CollectionAssert.AreEqual(new[] { SettlementReasons.Failed, SettlementReasons.Failed, SettlementReasons.PartyLost },
                    Settlement(quests.World).Changes.Select(c => c.Reason).ToArray());
            }
        }

        [Test]
        public void PromotionAndEventQuest_DoNotMoveSecurity()
        {
            using (var quests = new QuestWorld(settlement: true))
            {
                foreach (bool promotion in new[] { true, false })
                {
                    Order order = quests.AddOrder("Hunt", GuildRank.D);
                    quests.World.Do(ctx =>
                    {
                        if (promotion) order.IsPromotion = true;
                        else order.IsEventQuest = true;
                    });
                    QuestRun run = quests.Start(order, quests.Add());
                    quests.World.Do(ctx => QuestSettlement.Finish(ctx, run));
                    quests.Simulation.Tick();
                }
                Assert.AreEqual(60f, Settlement(quests.World).Security);
                Assert.IsEmpty(Settlement(quests.World).Changes);
            }
        }

        [TestCase(SimEventType.OrderExpired, false, SettlementReasons.Expired)]
        [TestCase(SimEventType.OrderDeclinedByRegistrar, false, SettlementReasons.DeclinedByRegistrar)]
        [TestCase(SimEventType.OrderDeclinedByPlayer, false, SettlementReasons.DeclinedByPlayer)]
        [TestCase(SimEventType.OrderDeclinedByPlayer, true, SettlementReasons.NoAnswer)]
        public void ThreatMissed_LowersByRank_OtherOrdersDoNot(SimEventType type, bool noAnswer, string reason)
        {
            using (var quests = new QuestWorld(settlement: true))
            {
                Order threat = quests.AddOrder("Hunt", GuildRank.C);
                Order other = quests.AddOrder("Escort", GuildRank.C);
                quests.World.Do(ctx =>
                {
                    foreach (Order order in new[] { threat, other })
                    {
                        SimEvent simEvent = OrderSystem.Publish(ctx, type, order);
                        if (type == SimEventType.OrderDeclinedByPlayer) simEvent.With("noAnswer", noAnswer);
                    }
                });
                quests.Simulation.Tick();

                SettlementState settlement = Settlement(quests.World);
                Assert.AreEqual(60f - 1f * 5, settlement.Security, Tolerance, "только угроза: − 1 × ранг C");
                Assert.AreEqual(1, settlement.Changes.Count);
                Assert.AreEqual(reason, settlement.Changes[0].Reason);
                Assert.AreEqual(threat.Id, settlement.Changes[0].OrderId);
                Assert.AreEqual(1, settlement.Totals.ThreatsMissed);
            }
        }

        [Test]
        public void Levels_DropToAlarmPausesWithImportantEvent_RiseIsNotable()
        {
            using (var world = new StateWorld(settlement: true))
            {
                List<SimEvent> down = world.Collect(() =>
                {
                    ChangeSecurity(world, -25f);
                    world.Simulation.Tick();
                });
                Assert.AreEqual(SettlementLevel.Alarm, Settlement(world).Level);
                SimEvent dropped = down.Single(e => e.Type == SimEventType.SettlementLevelDropped);
                Assert.AreEqual(EventImportance.Important, dropped.Importance);
                Assert.IsTrue(world.Simulation.World.Autopause.Triggers.Any(t => t.Kind == AutopauseKind.SettlementAlarm), "автопауза");

                List<SimEvent> siege = world.Collect(() =>
                {
                    ChangeSecurity(world, -16f);
                    world.Simulation.Tick();
                });
                Assert.AreEqual(SettlementLevel.Siege, Settlement(world).Level);
                Assert.IsTrue(siege.Any(e => e.Type == SimEventType.SettlementLevelDropped));

                List<SimEvent> up = world.Collect(() =>
                {
                    ChangeSecurity(world, 56f);
                    world.Simulation.Tick();
                });
                Assert.AreEqual(SettlementLevel.Calm, Settlement(world).Level);
                SimEvent rose = up.Single(e => e.Type == SimEventType.SettlementLevelChanged);
                Assert.AreEqual(EventImportance.Notable, rose.Importance);
                Assert.IsFalse(up.Any(e => e.Type == SimEventType.SettlementLevelDropped));
                Assert.IsFalse(world.Simulation.World.Autopause.Triggers.Any(t => t.Kind == AutopauseKind.SettlementAlarm), "вверх — без автопаузы");

                List<SimEvent> calmToNormal = world.Collect(() =>
                {
                    ChangeSecurity(world, -20f);
                    world.Simulation.Tick();
                });
                Assert.AreEqual(SettlementLevel.Normal, Settlement(world).Level);
                Assert.IsTrue(calmToNormal.Any(e => e.Type == SimEventType.SettlementLevelChanged), "со «спокойно» на «норму» — без автопаузы");
                Assert.IsFalse(calmToNormal.Any(e => e.Type == SimEventType.SettlementLevelDropped));
            }
        }

        [Test]
        public void Security_ClampedToRange()
        {
            using (var world = new StateWorld(settlement: true))
            {
                ChangeSecurity(world, 500f);
                Assert.AreEqual(100f, Settlement(world).Security);
                ChangeSecurity(world, -500f);
                Assert.AreEqual(0f, Settlement(world).Security);
                Assert.AreEqual(-100f, Settlement(world).Changes.Last().Delta, Tolerance, "в изменениях — фактический сдвиг");
            }
        }

        [TestCase(80, SettlementLevel.Calm, 2)]
        [TestCase(80, SettlementLevel.Normal, 0)]
        [TestCase(80, SettlementLevel.Alarm, -2)]
        [TestCase(80, SettlementLevel.Siege, -4)]
        [TestCase(10, SettlementLevel.Calm, 1)]
        [TestCase(10, SettlementLevel.Alarm, -1)]
        [TestCase(0, SettlementLevel.Calm, 0)]
        public void MonthlyPopulationChange_ShareRoundedAwayFromZero(int population, SettlementLevel level, int expected)
        {
            Assert.AreEqual(expected, SettlementRules.MonthlyPopulationChange(population, level, new SettlementBalance()));
        }

        [TestCase(15f, 82)]
        [TestCase(0f, 80)]
        [TestCase(-25f, 78)]
        [TestCase(-45f, 76)]
        public void MonthStart_PopulationByLevel(float securityDelta, int expected)
        {
            using (var world = new StateWorld(settlement: true))
            {
                world.Data.Set("settlement.dailyDecay", 0f);
                world.Data.Set("settlement.attackChancePerNight", 0f);
                if (securityDelta != 0f) ChangeSecurity(world, securityDelta);
                List<SimEvent> events = world.Collect(() => TickToMonthStart(world));
                Assert.AreEqual(expected, Settlement(world).Population);
                Assert.AreEqual(expected == 80 ? 0 : 1, events.Count(e => e.Type == SimEventType.PopulationChanged), "раз в месяц");
            }
        }

        [Test]
        public void BuildingReady_AddsResidents()
        {
            using (var world = new StateWorld(settlement: true))
            {
                world.Simulation.Send(new StartBuildingCommand("Dormitory"));
                List<SimEvent> events = world.Collect(() => world.Days(12));
                Assert.IsTrue(events.Any(e => e.Type == SimEventType.BuildingReady), "Общежитие построено");
                Assert.AreEqual(85, Settlement(world).Population);
                SimEvent grown = events.Single(e => e.Type == SimEventType.PopulationChanged);
                Assert.IsTrue(grown.TryGet("reason", out string reason) && reason == SettlementReasons.Building);
            }
        }

        [Test]
        public void Siege_AttackAtNight_KillsResidents_StressesThoseHome_DamagesTreasury()
        {
            using (var world = new StateWorld(settlement: true))
            {
                world.Data.Set("settlement.attackChancePerNight", 1f);
                Adventurer home = world.Add();
                ChangeSecurity(world, -45f);
                int money = world.Simulation.World.Treasury.Money;

                List<SimEvent> events = world.Collect(() => world.TickToHour(world.Data.Balance.Time.NightHour));
                SimEvent attack = events.Single(e => e.Type == SimEventType.SettlementAttacked);
                Assert.AreEqual(EventImportance.Important, attack.Importance);
                CollectionAssert.Contains(attack.Participants, home.Id);
                Assert.That(80 - Settlement(world).Population, Is.InRange(2, 5));
                LedgerEntry damage = world.Simulation.World.Treasury.Ledger.Last(e => e.Category == LedgerCategories.AttackDamage);
                Assert.That(-damage.Amount, Is.InRange(100, 300));
                Assert.Less(world.Simulation.World.Treasury.Money, money);
                Assert.AreEqual(1, Settlement(world).Totals.Attacks);
                Assert.IsTrue(world.Simulation.World.Autopause.Triggers.Any(t => t.Kind == AutopauseKind.SettlementAttack));
            }
        }

        [Test]
        public void Attack_SparesThoseOnQuest()
        {
            using (var quests = new QuestWorld(settlement: true))
            {
                Adventurer away = quests.Add();
                Adventurer home = quests.Add();
                quests.Start(quests.AddOrder("Hunt", GuildRank.G, distance: OrderDistance.Far), away);
                List<SimEvent> events = quests.DoEvents(ctx => SettlementService.Attack(ctx));
                SimEvent attack = events.Single(e => e.Type == SimEventType.SettlementAttacked);
                CollectionAssert.Contains(attack.Participants, home.Id);
                CollectionAssert.DoesNotContain(attack.Participants, away.Id);
            }
        }

        [Test]
        public void NotUnderSiege_NoAttacks_NoRolls()
        {
            using (var world = new StateWorld(settlement: true))
            {
                world.Data.Set("settlement.attackChancePerNight", 1f);
                world.Data.Set("settlement.dailyDecay", 0f);
                ChangeSecurity(world, -35f); // тревога
                List<SimEvent> events = world.Collect(() => world.Days(5));
                Assert.IsFalse(events.Any(e => e.Type == SimEventType.SettlementAttacked));
                Assert.AreEqual(80, Settlement(world).Population);
            }
        }

        [Test]
        public void ZeroSecurity_ThirtyDays_GuildClosedAsAbandoned()
        {
            using (var world = new StateWorld(settlement: true))
            {
                world.Data.Set("settlement.dailyDecay", 0f);
                world.Data.Set("settlement.attackChancePerNight", 0f);
                List<SimEvent> start = world.Collect(() =>
                {
                    ChangeSecurity(world, -60f);
                    world.Simulation.Tick();
                });
                Assert.IsTrue(start.Any(e => e.Type == SimEventType.SettlementAbandoning));
                Assert.IsNotNull(Settlement(world).ZeroSecuritySinceHours);

                world.Days(29);
                Assert.IsFalse(world.Simulation.IsFinished, "29 суток — ещё живы");
                List<SimEvent> end = world.Collect(() => world.Days(2));
                Assert.IsTrue(world.Simulation.IsFinished);
                Assert.AreEqual(ClosedCause.SettlementAbandoned, world.Simulation.World.Treasury.ClosedCause);
                SimEvent closed = end.Single(e => e.Type == SimEventType.GuildClosed);
                Assert.IsTrue(closed.TryGet("cause", out ClosedCause cause) && cause == ClosedCause.SettlementAbandoned);
                Assert.AreEqual(FeedKeys.GuildClosedSettlement, FeedKeys.Of(closed, world.Simulation.World, world.Registry));
                StringAssert.Contains(UiStrings.DefeatSettlementFormat.Substring(0, 10), DefeatModel.Build(world.Simulation).Cause);
            }
        }

        [Test]
        public void ZeroSecurity_RaisedInTime_CountdownLifted()
        {
            using (var world = new StateWorld(settlement: true))
            {
                world.Data.Set("settlement.dailyDecay", 0f);
                world.Data.Set("settlement.attackChancePerNight", 0f);
                ChangeSecurity(world, -60f);
                world.Days(20);
                List<SimEvent> lifted = world.Collect(() =>
                {
                    ChangeSecurity(world, 5f);
                    world.Simulation.Tick();
                });
                Assert.IsTrue(lifted.Any(e => e.Type == SimEventType.SettlementAbandoningLifted));
                Assert.IsNull(Settlement(world).ZeroSecuritySinceHours);
                world.Days(40);
                Assert.IsFalse(world.Simulation.IsFinished);
            }
        }

        [Test]
        public void NoResidents_GuildClosed()
        {
            using (var world = new StateWorld(settlement: true))
            {
                world.Do(ctx => SettlementService.ChangePopulation(ctx, -1000, SettlementReasons.Attack));
                world.Simulation.Tick();
                Assert.IsTrue(world.Simulation.IsFinished);
                Assert.AreEqual(ClosedCause.SettlementAbandoned, world.Simulation.World.Treasury.ClosedCause);
            }
        }

        [Test]
        public void Bankruptcy_ClosesWithItsCause()
        {
            using (var world = new StateWorld())
            {
                world.Do(ctx => TreasuryService.Debit(ctx, LedgerCategories.DebugExpense, 100_000));
                for (int day = 0; day < 720 && !world.Simulation.IsFinished; day++) world.Days(1);
                Assert.IsTrue(world.Simulation.IsFinished);
                Assert.AreEqual(ClosedCause.Bankruptcy, world.Simulation.World.Treasury.ClosedCause);
            }
        }

        [Test]
        public void LocalsTavern_IncomeAtMidnight_HalfUnderSiege()
        {
            using (var world = new StateWorld(settlement: true))
            {
                world.Data.Set("settlement.dailyDecay", 0f);
                world.TickToHour(0);
                LedgerEntry entry = world.Simulation.World.Treasury.Ledger.Single(e => e.Category == LedgerCategories.TavernLocals);
                Assert.AreEqual(4, entry.Amount, "80 жителей × 0,05");

                world.Data.Set("settlement.attackChancePerNight", 0f);
                ChangeSecurity(world, -45f);
                world.TickToHour(0);
                Assert.AreEqual(2, world.Simulation.World.Treasury.Ledger.Last(e => e.Category == LedgerCategories.TavernLocals).Amount, "в осаде × 0,5");
            }
        }

        [Test]
        public void OrdersPerDay_FromPopulationAndReputation()
        {
            var orders = new OrdersBalance();
            Assert.AreEqual(1f + 80f / 40f + 5f / 33f, OrderGenerator.OrdersPerDay(orders, 5f, 80), Tolerance, "на старте — около трёх");
            Assert.AreEqual(1f + 40f / 40f, OrderGenerator.OrdersPerDay(orders, 0f, 40), Tolerance);
        }

        [Test]
        public void SameSeed_SameSettlement()
        {
            using (var data = new PeopleData())
            {
                string Run()
                {
                    Simulation simulation = Simulation.CreateDefault(data.Registry, 21u);
                    string log = SimulationLog.Record(simulation, sim => SimulationRun.Days(sim, 150));
                    SettlementState settlement = simulation.World.Settlement;
                    return log + "\n" + settlement.Security + " " + settlement.Population + " "
                        + string.Join(",", settlement.Changes.Select(c => c.Reason + c.Delta));
                }

                string first = Run();
                StringAssert.Contains("SecurityChanged", first);
                Assert.AreEqual(first, Run());
            }
        }

        [Test]
        public void SettlementSystem_DoesNotShiftOtherSystems()
        {
            using (var data = new PeopleData())
            {
                data.NoQuests(); // задания меняют репутацию, а с ней — шанс кандидата
                data.NeutralRumor(); // без заданий довольство падает, молва гасит приток — кандидатов не было бы
                data.Set("settlement.dailyDecay", 0.3f); // поселение живёт: спад, тревога, население, доход от жителей
                data.Set("settlement.localsTavernIncome", 0.05f);
                string Run(bool withSettlement)
                {
                    List<ISimSystem> systems = SimulationSystems.CreateDefault();
                    if (!withSettlement) systems.RemoveAll(s => s is SettlementSystem);
                    var simulation = new Simulation(data.Registry, 5u, systems);
                    string log = SimulationLog.Record(simulation, sim => SimulationRun.Days(sim, 120));
                    // Заказы не сравниваются: их число законно зависит от населения.
                    string[] others = { "[TimeSystem]", "[RecruitSystem]", "[AdventurerSystem]" };
                    return string.Join("\n", log.Split('\n').Where(line => others.Any(line.Contains)));
                }

                string without = Run(withSettlement: false);
                StringAssert.Contains("CandidateArrived", without);
                Assert.AreEqual(without, Run(withSettlement: true));
            }
        }
    }

    /// <summary>Поселение в сводке прогона.</summary>
    public sealed class SettlementSummaryTests
    {
        [Test]
        public void Summary_SettlementColumns_MatchWorld()
        {
            using (var data = new PeopleData())
            {
                // Сводка кончается последним целым месяцем: сверяем с поселением на конец месяца (последний час его последних суток).
                SettlementSnapshot atMonthEnd = default;
                Debugging.HeadlessRun.Result result = Debugging.HeadlessRun.Run(data.Registry, 12u, 90, Debugging.PlayerBots.Simple(), null,
                    simulation => simulation.TickCompleted += _ =>
                    {
                        GameTime time = simulation.World.Time;
                        if (time.Day == simulation.Calendar.DaysPerMonth && time.Hour == simulation.Calendar.HoursPerDay - 1)
                            atMonthEnd = SettlementSnapshot.Of(simulation.World.Settlement);
                    });
                Debugging.RunSummary summary = result.Summary;

                double Total(string name) => summary.Totals[summary.MonthColumns.Select(c => c.Name).ToList().IndexOf(name)];
                Assert.AreEqual(atMonthEnd.Totals.ThreatsDone, Total("Угроз выполнено"));
                Assert.AreEqual(atMonthEnd.Totals.ThreatsMissed, Total("Угроз упущено"));
                Assert.Greater(Total("Угроз выполнено") + Total("Угроз упущено"), 0, "за три месяца угрозы были");
                Assert.AreEqual(atMonthEnd.Population, Total("Население"));
                Assert.AreEqual(atMonthEnd.Security, Total("Безопасность"), 0.01);
            }
        }
    }

    /// <summary>Поселение в интерфейсе: верхняя панель, вкладка, уведомления, ссылка, пометка угрозы.</summary>
    public sealed class SettlementUiTests
    {
        [Test]
        public void TopBar_NameResidentsSecurity_CountdownWhenZero()
        {
            using (var world = new StateWorld(settlement: true))
            {
                TopBarModel model = TopBarModel.Build(world.Simulation, null);
                StringAssert.Contains(world.Simulation.World.Settlement.Name.Nominative, model.Settlement);
                StringAssert.Contains("80 жителей", model.Settlement);
                StringAssert.Contains("Безопасность 60", model.Settlement);
                Assert.IsEmpty(model.SettlementLeaving);

                world.Do(ctx => SettlementService.ChangeSecurity(ctx, -60f, SettlementReasons.Debug));
                model = TopBarModel.Build(world.Simulation, null);
                Assert.AreEqual(SettlementLevel.Siege, model.SettlementLevel);
                Assert.AreEqual(string.Format(UiStrings.SettlementLeavingFormat, 30), model.SettlementLeaving);
            }
        }

        [Test]
        public void Tab_ChangesWithReasonsAndOrderLinks_NewestFirst()
        {
            using (var quests = new QuestWorld(settlement: true))
            {
                Order threat = quests.AddOrder("Hunt", GuildRank.E);
                quests.World.Do(ctx => OrderSystem.Publish(ctx, SimEventType.OrderExpired, threat));
                quests.Simulation.Tick();
                quests.World.TickToHour(0);

                SettlementModel model = SettlementModel.Build(quests.Simulation);
                Assert.AreEqual(2, model.Changes.Count);
                Assert.AreEqual("ночь", model.Changes[0].Text);
                Assert.AreEqual(0, model.Changes[0].OrderId);
                Assert.AreEqual("Охота E снят по сроку", model.Changes[1].Text);
                Assert.AreEqual(threat.Id, model.Changes[1].OrderId);
                Assert.AreEqual("−3", SettlementModel.Delta(model.Changes[1].Delta));
                Assert.AreEqual("норма", model.LevelName);
                Assert.AreEqual(-1, model.DaysLeft);
            }
        }

        [Test]
        public void Notifications_AlarmAndSiege_LeadToSettlementTab()
        {
            using (var world = new StateWorld(settlement: true))
            {
                var notifications = new NotificationsModel();
                notifications.Refresh(world.Simulation);
                Assert.IsFalse(notifications.Items.Any(n => n.Destination.Tab == GuildTab.Settlement), "в норме уведомления нет");

                world.Do(ctx => SettlementService.ChangeSecurity(ctx, -25f, SettlementReasons.Debug));
                notifications.Refresh(world.Simulation);
                Notification alarm = notifications.Items.Single(n => n.Destination.Tab == GuildTab.Settlement);
                StringAssert.Contains("тревога", alarm.Text);
                Assert.AreEqual(ScreenId.Guild, alarm.Destination.Screen);
            }
        }

        [Test]
        public void SettlementLink_LeadsToSettlementTab()
        {
            using (var world = new StateWorld(settlement: true))
            {
                Destination destination = LinkRouter.Resolve(new TextLink(TextLinkKind.Settlement, 1), world.Simulation);
                Assert.IsTrue(destination.IsValid);
                Assert.AreEqual(GuildTab.Settlement, destination.Tab);
            }
        }

        [Test]
        public void DebugCommands_SecurityAndAttack()
        {
            using (var world = new StateWorld(settlement: true))
            {
                world.Simulation.Send(new DebugSecurityCommand(-10f));
                world.Simulation.ApplyCommandsNow();
                Assert.AreEqual(50f, world.Simulation.World.Settlement.Security);
                world.Simulation.Send(new DebugAttackCommand());
                world.Simulation.ApplyCommandsNow();
                Assert.AreEqual(1, world.Simulation.World.Settlement.Totals.Attacks);
                Assert.Less(world.Simulation.World.Settlement.Population, 80);
            }
        }
    }
}
