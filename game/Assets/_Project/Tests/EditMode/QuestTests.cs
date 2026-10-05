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
    /// <summary>
    /// Мир для тестов заданий: <see cref="StateWorld"/> (свои люди, пустая доска) + заказы, собранные вручную, и задания,
    /// начатые сразу этой группой.
    /// </summary>
    internal sealed class QuestWorld : IDisposable
    {
        public QuestWorld(uint seed = 7u, SimLogger log = null, bool dilemmas = false)
        {
            World = new StateWorld(seed, log: log, dilemmas: dilemmas);
        }

        public StateWorld World { get; }
        public Simulation Simulation => World.Simulation;
        public DataRegistry Registry => World.Registry;
        public PeopleData Data => World.Data;

        public Adventurer Add(params string[] traits) => World.Add(traits);

        /// <summary>Заказ на доске: все оси диаграммы — <paramref name="requirement"/>.</summary>
        public Order AddOrder(string type = "Hunt", GuildRank rank = GuildRank.G, float requirement = 20f, int reward = 100,
            OrderDistance distance = OrderDistance.Near)
        {
            return World.Do(ctx =>
            {
                OrderBoard board = ctx.World.Orders;
                var order = new Order(board.NextId(), type, rank, distance)
                {
                    Reward = reward,
                    DescriptionAccuracy = 1f,
                    BoardDays = 5,
                    Place = ctx.Data.OrderTexts.QuestTypes[0].Places[0],
                    Enemy = ctx.Data.OrderTexts.QuestTypes[0].Enemies[0],
                    Client = ctx.Data.OrderTexts.Clients[0],
                };
                for (int i = 0; i < Vocabulary.StatCount; i++)
                    order.SetRequirement((StatId)i, Vocabulary.IsDiagramAxis((StatId)i) ? requirement : 0f);
                OrderSystem.Post(ctx, order);
                board.AddOpen(order);
                return order;
            });
        }

        /// <summary>Начать задание этой группой сразу (в обход модели решений).</summary>
        public QuestRun Start(Order order, params Adventurer[] party) => World.Do(ctx =>
        {
            order.Status = OrderStatus.Taken;
            ctx.World.Orders.MoveToWork(order);
            return QuestSystem.Depart(ctx, order, party);
        });

        /// <summary>Начать задание группой <paramref name="party"/> (постоянной или под задание) сразу, в обход модели решений.</summary>
        public QuestRun StartParty(Order order, Party party, params Adventurer[] people) => World.Do(ctx =>
        {
            order.Status = OrderStatus.Taken;
            ctx.World.Orders.MoveToWork(order);
            return QuestSystem.Depart(ctx, order, people, party);
        });

        /// <summary>Постоянная группа из этих людей (название — из списка, как в игре).</summary>
        public Party MakePermanent(params Adventurer[] members) => World.Do(ctx => PartyService.Form(ctx, members.ToList()));

        /// <summary>Такты, пока задание не кончится (не дольше <paramref name="maxDays"/>).</summary>
        public List<SimEvent> RunToEnd(QuestRun run, int maxDays = 20) => World.Collect(() =>
        {
            for (int i = 0; i < maxDays * 24 && run.Phase != QuestPhase.Returned; i++) Simulation.Tick();
        });

        /// <summary>Поставить группу на место задания: следующий такт — конец раунда.</summary>
        public void AtSite(QuestRun run, int failedRounds = 0) => World.Do(ctx =>
        {
            run.Phase = QuestPhase.AtSite;
            run.Round = failedRounds + 1;
            run.FailedRounds = failedRounds;
            run.PhaseHoursLeft = 1;
            run.TravelEventHour = 0;
        });

        /// <summary>События действия на паузе: они лежат в шине до следующего такта.</summary>
        public List<SimEvent> DoEvents(Action<SimContext> action)
        {
            int before = Simulation.Events.Events.Count;
            World.Do(action);
            return Simulation.Events.Events.Skip(before).ToList();
        }

        public void Dispose() => World.Dispose();
    }

    public sealed class QuestMathTests
    {
        private static readonly StatId[] Square = { StatId.Strength, StatId.Reaction, StatId.Marksmanship, StatId.Stealth };

        private static float[] Profile(params float[] values)
        {
            var profile = new float[Vocabulary.StatCount];
            for (int i = 0; i < Square.Length; i++) profile[(int)Square[i]] = values[i];
            return profile;
        }

        [Test]
        public void FullCover_Half_Zero()
        {
            float[] q = Profile(20, 30, 40, 25);
            Assert.AreEqual(1f, QuestMath.Overlap(Square, q, Profile(20, 30, 40, 25)), 1e-5);
            Assert.AreEqual(1f, QuestMath.Overlap(Square, q, Profile(99, 99, 99, 99)), 1e-5);
            Assert.AreEqual(0.25f, QuestMath.Overlap(Square, q, Profile(10, 15, 20, 12.5f)), 1e-5);
            Assert.AreEqual(0f, QuestMath.Overlap(Square, q, Profile(0, 0, 0, 0)), 1e-5);
            Assert.AreEqual(1f, QuestMath.Overlap(Square, Profile(0, 0, 0, 0), Profile(5, 5, 5, 5)), 1e-5, "требований нет");
        }

        [Test]
        public void CrossingSegments_AreaByShoelace()
        {
            // Квадратная диаграмма, требования 10 по всем осям, группа 20/5/20/5: в каждом секторе отрезки пересекаются
            // в (20/3, 10/3), площадь пересечения 100/3 из 50 — перекрытие 2/3.
            float overlap = QuestMath.Overlap(Square, Profile(10, 10, 10, 10), Profile(20, 5, 20, 5));
            Assert.AreEqual(2f / 3f, overlap, 1e-5);
        }

        [Test]
        public void RealRadar_HalfEverywhere_Quarter()
        {
            using var data = new PeopleData();
            IReadOnlyList<StatId> radar = data.Registry.Stats.RadarOrder;
            Assert.AreEqual(13, radar.Count);
            var q = new float[Vocabulary.StatCount];
            var g = new float[Vocabulary.StatCount];
            foreach (StatId stat in radar)
            {
                q[(int)stat] = 40f;
                g[(int)stat] = 20f;
            }
            Assert.AreEqual(0.25f, QuestMath.Overlap(radar, q, g), 1e-5);
        }
    }

    public sealed class QuestRoundTests
    {
        [Test]
        public void GroupProfile_SoloFull_GroupTimesCohesion_PanicAndRush()
        {
            using var quests = new QuestWorld();
            Adventurer a = quests.Add();
            Adventurer b = quests.Add();
            a.SetStat(StatId.Cohesion, 50f);
            b.SetStat(StatId.Cohesion, 20f);

            Assert.AreEqual(30f, QuestMath.GroupProfile(new[] { a }, quests.Registry)[(int)StatId.Strength], 1e-4, "один — × 1");
            Assert.AreEqual(30f * 0.5f + 30f * 0.2f, QuestMath.GroupProfile(new[] { a, b }, quests.Registry)[(int)StatId.Strength], 1e-4,
                "группа — × Слаженность / 100");
            Assert.AreEqual(0f, QuestMath.GroupProfile(new[] { a }, quests.Registry)[(int)StatId.Cohesion], "Слаженность — не ось");
            Assert.AreEqual(15f, QuestMath.GroupProfile(new[] { a }, quests.Registry, new[] { a.Id })[(int)StatId.Strength], 1e-4, "паника × 0,5");
            Assert.AreEqual(39f, QuestMath.GroupProfile(new[] { a }, quests.Registry, null, new[] { a.Id })[(int)StatId.Strength], 1e-4, "бросок × 1,3");
        }

        [Test]
        public void Synergies_PerPair_CappedAtTwentyPercent()
        {
            using var quests = new QuestWorld();
            Adventurer a = quests.Add();
            Adventurer b = quests.Add();
            Adventurer c = quests.Add();
            quests.World.Do(ctx => RelationService.Set(ctx, a.Id, b.Id, 50f));
            var pairs = new List<PairSynergy>();
            Assert.AreEqual(0.05f, QuestMath.Synergy(new[] { a, b }, quests.Simulation.World.Relations, quests.Registry, pairs), 1e-5);
            Assert.AreEqual("friends", pairs.Single().Kind);

            quests.World.Do(ctx => RelationService.Set(ctx, b.Id, c.Id, -50f));
            Assert.AreEqual(0.0f, QuestMath.Synergy(new[] { a, b, c }, quests.Simulation.World.Relations, quests.Registry), 1e-5, "друзья + неприязнь");

            quests.World.Do(ctx =>
            {
                RelationService.Set(ctx, a.Id, c.Id, 50f);
                RelationService.Set(ctx, b.Id, c.Id, 50f);
                for (int i = 0; i < 10; i++)
                {
                    RelationService.AddJointQuest(ctx, a.Id, b.Id);
                    RelationService.AddJointQuest(ctx, a.Id, c.Id);
                    RelationService.AddJointQuest(ctx, b.Id, c.Id);
                }
            });
            Assert.AreEqual(0.2f, QuestMath.Synergy(new[] { a, b, c }, quests.Simulation.World.Relations, quests.Registry), 1e-5, "0,3 → потолок 0,2");
        }

        [Test]
        public void RoundChance_IsOverlapPlusSynergy_InLog()
        {
            var writer = new StringWriter();
            using var quests = new QuestWorld(log: new SimLogger(SimLogLevel.Debug, writer));
            Adventurer a = quests.Add();
            Adventurer b = quests.Add();
            quests.World.Do(ctx => RelationService.Set(ctx, a.Id, b.Id, 50f));
            Order order = quests.AddOrder(requirement: 60f);
            QuestRun run = quests.Start(order, a, b);
            quests.AtSite(run);
            List<SimEvent> events = quests.World.Collect(() => quests.Simulation.Tick());

            float overlap = QuestMath.Overlap(quests.Registry.Stats.RadarOrder, order.Profile,
                QuestMath.GroupProfile(new[] { a, b }, quests.Registry));
            SimEvent round = events.Single(e => e.Type == SimEventType.RoundSuccess || e.Type == SimEventType.RoundFail);
            round.TryGet("chance", out float chance);
            Assert.AreEqual(overlap + 0.05f, chance, 1e-4);
            StringAssert.Contains("synergy=0.05", writer.ToString());
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.Synergy), "строка синергии в первом раунде");
        }

        [Test]
        public void Ceiling_FailsRoundAutomatically()
        {
            using var quests = new QuestWorld();
            Adventurer a = quests.Add();
            Adventurer b = quests.Add();
            Order order = quests.AddOrder(requirement: 10f);
            order.PartySizeCeiling = 1;
            QuestRun run = quests.Start(order, a, b);
            quests.AtSite(run);
            List<SimEvent> events = quests.World.Collect(() => quests.Simulation.Tick());

            Assert.IsTrue(events.Any(e => e.Type == SimEventType.CeilingTriggered));
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.RoundFail), "перекрытие 1, но раунд провален потолком");
            Assert.AreEqual(1, run.FailedRounds);
        }

        [Test]
        public void FirstFailure_StressTime_LightWoundAndBonusWithLadderChances()
        {
            const int trials = 400;
            int wounds = 0, bonuses = 0;
            for (int t = 0; t < trials; t++)
            {
                using var quests = new QuestWorld((uint)(100 + t));
                quests.Data.Set("tension.panicOffset", -1000f); // без паники и напряжения
                Adventurer a = quests.Add();
                QuestRun run = quests.Start(quests.AddOrder(requirement: 30000f), a); // шанс раунда ~0
                quests.AtSite(run);
                float stress = a.State.Stress;
                List<SimEvent> events = quests.World.Collect(() => quests.Simulation.Tick());

                Assert.AreEqual(1, run.FailedRounds);
                Assert.IsTrue(events.Any(e => e.Type == SimEventType.QuestLoss && Kind(e) == "time"));
                Assert.GreaterOrEqual(a.State.Stress, stress + 5f - 1f, "стресс +5 (минус час)");
                if (events.Any(e => e.Type == SimEventType.AdventurerWounded)) wounds++;
                if (run.BonusLost) bonuses++;
            }
            Assert.That(wounds, Is.EqualTo(trials * 0.3f).Within(Frequency.Tolerance(trials, 0.3f)));
            Assert.That(bonuses, Is.EqualTo(trials * 0.5f).Within(Frequency.Tolerance(trials, 0.5f)));
        }

        [Test]
        public void SecondFailure_BonusLostForSure()
        {
            using var quests = new QuestWorld();
            Adventurer a = quests.Add();
            QuestRun run = quests.Start(quests.AddOrder(requirement: 500f), a);
            quests.AtSite(run, failedRounds: 1);
            quests.Simulation.Tick();
            Assert.AreEqual(2, run.FailedRounds);
            Assert.IsTrue(run.BonusLost);
        }

        [Test]
        public void FourthFailure_KillsMostWounded_MedicMaySave()
        {
            using (var quests = new QuestWorld())
            {
                quests.Data.Set("rounds.medicSaveMax", 0f);
                Adventurer strong = quests.Add();
                Adventurer hurt = quests.Add();
                Order order = quests.AddOrder(requirement: 500f);
                QuestRun run = quests.Start(order, strong, hurt);
                quests.World.Do(ctx => HealthService.Wound(ctx, hurt, ConditionKind.LightWound));
                strong.SetStat(StatId.Medicine, 10f);
                quests.AtSite(run, failedRounds: 3);
                List<SimEvent> events = quests.World.Collect(() => quests.Simulation.Tick());

                Assert.AreEqual(LeaveReason.Died, hurt.LeaveReason, "погиб самый тяжело раненый");
                Assert.IsTrue(run.Dead.Contains(hurt.Id));
                Assert.IsTrue(events.Any(e => e.Type == SimEventType.AdventurerDied && e.Participants[0] == hurt.Id));
                Assert.AreEqual(quests.Data.Balance.Guild.StartReputation + quests.Data.Balance.Guild.DeathReputation,
                    quests.Simulation.World.Guild.Reputation, 1e-4, "гибель −2");
            }

            using (var quests = new QuestWorld())
            {
                quests.Data.Set("rounds.medicSaveDivisor", 1f); // Медицина 30 / 1 → шанс упирается в максимум
                quests.Data.Set("rounds.medicSaveMax", 1f);
                Adventurer medic = quests.Add();
                Adventurer hurt = quests.Add();
                QuestRun run = quests.Start(quests.AddOrder(requirement: 500f), medic, hurt);
                quests.World.Do(ctx => HealthService.Wound(ctx, hurt, ConditionKind.HeavyWound));
                quests.AtSite(run, failedRounds: 3);
                List<SimEvent> events = quests.World.Collect(() => quests.Simulation.Tick());

                Assert.AreEqual(LeaveReason.None, hurt.LeaveReason, "Лекарь спас");
                Assert.IsTrue(events.Any(e => e.Type == SimEventType.MedicSaved));
                Assert.IsTrue(hurt.Traits.Any(t => t.TraitId == "Maimed"), "спасённый — Калека");
            }
        }

        [Test]
        public void FifthFailure_KillsEveryonePresent_Catastrophe()
        {
            using var quests = new QuestWorld();
            List<Adventurer> party = quests.World.AddMany(3);
            QuestRun run = quests.Start(quests.AddOrder(requirement: 500f), party.ToArray());
            quests.AtSite(run, failedRounds: 4);
            List<SimEvent> events = quests.World.Collect(() => quests.Simulation.Tick());

            Assert.IsTrue(party.All(p => p.LeaveReason == LeaveReason.Died));
            Assert.AreEqual(QuestPhase.Returned, run.Phase);
            Assert.AreEqual(QuestResult.Catastrophe, run.Result);
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.QuestLost));
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.QuestCatastrophe));
            Assert.IsFalse(run.Completed);
        }

        private static string Kind(SimEvent simEvent) => simEvent.TryGet("kind", out string kind) ? kind : null;
    }

    public sealed class QuestTensionTests
    {
        [Test]
        public void PanicChance_FromStressComposureAndTraits()
        {
            using var quests = new QuestWorld();
            Adventurer calm = quests.Add();
            calm.State.Stress = 50f;
            calm.SetStat(StatId.Composure, 40f);
            Assert.AreEqual((50f - 40f + 20f) / 100f, QuestTension.PanicChance(calm, quests.Registry), 1e-4);

            Adventurer coward = quests.Add();
            coward.State.Stress = 50f;
            coward.SetStat(StatId.Composure, 40f);
            coward.SetAxis(AxisId.Risk, -50f);
            Assert.AreEqual(0.3f + 0.2f * 0.5f, QuestTension.PanicChance(coward, quests.Registry), 1e-4, "трус — до +0,2 по оси");

            Adventurer iron = quests.Add("IronNerves");
            iron.State.Stress = 50f;
            iron.SetStat(StatId.Composure, 40f);
            Assert.AreEqual(0f, QuestTension.PanicChance(iron, quests.Registry), 1e-4, "Железные нервы −0,3");

            Adventurer veteran = quests.Add("Veteran");
            veteran.State.Stress = 50f;
            veteran.SetStat(StatId.Composure, 40f);
            Assert.AreEqual(0.1f, QuestTension.PanicChance(veteran, quests.Registry), 1e-4, "Ветеран −0,2");
        }

        [Test]
        public void FleeAndRushChances_OnlyAtExtremePole_DeserterWhenStressed()
        {
            using var quests = new QuestWorld();
            Adventurer coward = quests.Add();
            coward.SetAxis(AxisId.Risk, -60f);
            Assert.AreEqual(0f, QuestTension.TraitChance(coward, TensionKind.Flee, quests.Registry), 1e-5);
            coward.SetAxis(AxisId.Risk, -80f);
            Assert.AreEqual(0.15f, QuestTension.TraitChance(coward, TensionKind.Flee, quests.Registry), 1e-5);

            Adventurer reckless = quests.Add();
            reckless.SetAxis(AxisId.Risk, 75f);
            Assert.AreEqual(0.15f, QuestTension.TraitChance(reckless, TensionKind.Rush, quests.Registry), 1e-5);

            Adventurer deserter = quests.Add("Deserter");
            deserter.State.Stress = 50f;
            Assert.AreEqual(0f, QuestTension.TraitChance(deserter, TensionKind.Flee, quests.Registry), 1e-5);
            deserter.State.Stress = 65f;
            Assert.AreEqual(0.1f, QuestTension.TraitChance(deserter, TensionKind.Flee, quests.Registry), 1e-5);
        }

        [Test]
        public void Panic_RevealsCoward_IronNervesHold()
        {
            using var quests = new QuestWorld();
            Adventurer coward = quests.Add();
            coward.SetAxis(AxisId.Risk, -50f);
            coward.State.Stress = 79f;
            coward.SetStat(StatId.Composure, 10f);
            Adventurer iron = quests.Add("IronNerves");
            QuestRun run = quests.Start(quests.AddOrder(), coward, iron);

            List<SimEvent> events = quests.DoEvents(ctx => QuestTension.Moment(ctx, run));
            Assert.IsTrue(run.Panicked.Contains(coward.Id));
            Assert.IsTrue(coward.IsAxisRevealed(AxisId.Risk), "трус раскрыт паникой");
            Assert.IsTrue(iron.Traits[0].Revealed, "Железные нервы: не дрогнул, когда другой запаниковал");
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.TensionMoment && e.TryGet("kind", out string k) && k == "hold"));
        }

        [Test]
        public void Rush_RevealsReckless_TakesWoundFirst()
        {
            using var quests = new QuestWorld(3u);
            Adventurer reckless = quests.Add();
            reckless.SetAxis(AxisId.Risk, 90f);
            reckless.SetStat(StatId.Endurance, 10f);
            Adventurer shield = quests.Add();
            shield.SetStat(StatId.Endurance, 90f);
            QuestRun run = quests.Start(quests.AddOrder(), reckless, shield);

            for (int i = 0; i < 200 && !run.Rushing.Contains(reckless.Id); i++) quests.World.Do(ctx => QuestTension.Moment(ctx, run));
            Assert.IsTrue(run.Rushing.Contains(reckless.Id));
            Assert.IsTrue(reckless.IsAxisRevealed(AxisId.Risk), "безрассудный раскрыт броском");
            Assert.AreSame(reckless, QuestParty.Shield(new[] { reckless, shield }, run, quests.Registry), "бросившийся первым принимает удар");
        }

        [Test]
        public void Hero_TakesHeavyWound_RelationUp()
        {
            using var quests = new QuestWorld();
            quests.Data.Set("tension.heroChance", 1f);
            Adventurer target = quests.Add();
            Adventurer hero = quests.Add();
            hero.SetStat(StatId.Composure, 70f);
            hero.SetAxis(AxisId.People, 40f);
            QuestRun run = quests.Start(quests.AddOrder(), target, hero);

            Adventurer wounded = quests.World.Do(ctx => QuestRounds.Wound(ctx, run, ConditionKind.HeavyWound, fromShield: false, target: target));
            Assert.AreSame(hero, wounded);
            Assert.IsTrue(hero.State.HasHeavyWound());
            Assert.IsFalse(target.State.HasHeavyWound());
            Assert.AreEqual(20f, quests.Simulation.World.Relations.GetValue(hero.Id, target.Id), 1e-4);
        }

        [Test]
        public void Flee_RevealsCowardAndDeserter_AbandonedSuffer_SoloEndsInRetreat()
        {
            using var quests = new QuestWorld();
            Adventurer fugitive = quests.Add("Deserter");
            fugitive.SetAxis(AxisId.Risk, -80f);
            Adventurer left = quests.Add();
            QuestRun run = quests.Start(quests.AddOrder(), fugitive, left);
            float stress = left.State.Stress;

            List<SimEvent> events = quests.DoEvents(ctx => QuestTension.Flee(ctx, run, fugitive));
            Assert.IsTrue(run.Fled.Contains(fugitive.Id));
            Assert.IsFalse(run.Members.Contains(fugitive.Id));
            Assert.IsTrue(fugitive.IsAxisRevealed(AxisId.Risk));
            Assert.IsTrue(fugitive.Traits[0].Revealed, "Бывший дезертир");
            Assert.AreEqual(stress + 5f, left.State.Stress, 1e-3);
            Assert.AreEqual(-20f, quests.Simulation.World.Relations.GetValue(left.Id, fugitive.Id), 1e-4);
            SimEvent fled = events.Single(e => e.Type == SimEventType.AdventurerFled);
            Assert.IsTrue(fled.TryGet("reason", out string reason) && reason.Length > 0);
            Assert.AreEqual(1, quests.Simulation.World.Quests.Stragglers.Count);

            using var solo = new QuestWorld();
            Adventurer alone = solo.Add();
            QuestRun soloRun = solo.Start(solo.AddOrder(), alone);
            solo.World.Do(ctx => QuestTension.Flee(ctx, soloRun, alone));
            Assert.IsTrue(soloRun.Retreated);
            solo.Simulation.Tick();
            Assert.AreEqual(QuestPhase.Returned, soloRun.Phase);
            Assert.AreEqual(QuestResult.Fail, soloRun.Result);
        }

        [Test]
        public void Fugitive_ReturnsAsDeserter_OrDisappears()
        {
            foreach (float chance in new[] { 1f, 0f })
            {
                using var quests = new QuestWorld();
                quests.Data.Set("tension.fugitiveReturnChance", chance);
                Adventurer fugitive = quests.Add();
                Adventurer left = quests.Add();
                QuestRun run = quests.Start(quests.AddOrder(), fugitive, left);
                quests.World.Do(ctx => QuestTension.Flee(ctx, run, fugitive));
                List<SimEvent> events = quests.World.Collect(() => quests.World.Days(4));

                if (chance == 1f)
                {
                    Assert.IsTrue(fugitive.IsDeserter);
                    Assert.IsTrue(events.Any(e => e.Type == SimEventType.DeserterReturned));
                    Assert.IsFalse(fugitive.State.IsOnQuest());
                }
                else
                {
                    Assert.AreEqual(LeaveReason.Disappeared, fugitive.LeaveReason);
                    Assert.IsTrue(events.Any(e => e.Type == SimEventType.AdventurerDisappeared));
                }
            }
        }

        [Test]
        public void OnlyFugitiveSurvived_WhenRestDie()
        {
            using var quests = new QuestWorld();
            Adventurer fugitive = quests.Add();
            List<Adventurer> rest = quests.World.AddMany(2);
            QuestRun run = quests.Start(quests.AddOrder(requirement: 500f), fugitive, rest[0], rest[1]);
            quests.World.Do(ctx => QuestTension.Flee(ctx, run, fugitive));
            quests.AtSite(run, failedRounds: 4);
            List<SimEvent> events = quests.World.Collect(() => quests.Simulation.Tick());

            Assert.IsTrue(events.Any(e => e.Type == SimEventType.QuestLost));
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.OnlyFugitiveSurvived && e.Participants[0] == fugitive.Id));
        }

        [Test]
        public void Autopause_OnDeathFleeCatastropheRetreat()
        {
            IReadOnlyDictionary<SimEventType, AutopauseKind> rules = AutopauseRules.Default;
            Assert.AreEqual(AutopauseKind.AdventurerDied, rules[SimEventType.AdventurerDied]);
            Assert.AreEqual(AutopauseKind.AdventurerFled, rules[SimEventType.AdventurerFled]);
            Assert.AreEqual(AutopauseKind.QuestCatastrophe, rules[SimEventType.QuestCatastrophe]);
            Assert.AreEqual(AutopauseKind.PartyRetreated, rules[SimEventType.PartyRetreated]);

            using var quests = new QuestWorld();
            Adventurer a = quests.Add();
            QuestRun run = quests.Start(quests.AddOrder(requirement: 500f), a);
            quests.AtSite(run, failedRounds: 4);
            quests.Simulation.Tick();
            Assert.IsTrue(quests.Simulation.ConsumePauseRequest(), "гибель ставит автопаузу");
        }
    }

    public sealed class QuestDecisionTests
    {
        [Test]
        public void Leader_HighestRankThenComposure()
        {
            using var quests = new QuestWorld();
            Adventurer a = quests.Add();
            Adventurer b = quests.Add();
            Adventurer c = quests.Add();
            b.SetStat(StatId.Composure, 60f);
            Assert.AreSame(b, QuestParty.Leader(new[] { a, b, c }, quests.Registry));
            c.GuildRank = GuildRank.F;
            Assert.AreSame(c, QuestParty.Leader(new[] { a, b, c }, quests.Registry));
        }

        [Test]
        public void AfterFailure_DeciderChooses_ReasonsInLog()
        {
            var writer = new StringWriter();
            using var quests = new QuestWorld(log: new SimLogger(SimLogLevel.Info, writer));
            Adventurer a = quests.Add();
            Adventurer b = quests.Add();
            b.GuildRank = GuildRank.F;
            QuestRun run = quests.Start(quests.AddOrder(requirement: 500f), a, b);
            quests.AtSite(run);
            List<SimEvent> events = quests.World.Collect(() => quests.Simulation.Tick());

            string log = writer.ToString();
            StringAssert.Contains("decide #" + b.Id, log);
            StringAssert.Contains("after-failure 1: ", log);
            StringAssert.Contains(" because ", log);
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.PartyDecision || e.Type == SimEventType.PartyRetreated));
        }

        [Test]
        public void HopelessQuest_DeadlyNextStep_Retreat()
        {
            using var quests = new QuestWorld();
            quests.Data.Set("decisions.bestChoiceChance", 1f);
            Adventurer a = quests.Add();
            QuestRun run = quests.Start(quests.AddOrder(requirement: 500f), a);
            quests.AtSite(run, failedRounds: 2);
            List<SimEvent> events = quests.World.Collect(() => quests.Simulation.Tick());

            Assert.IsTrue(run.Retreated, "шанс около нуля, следующий провал — гибель");
            Assert.AreEqual(QuestPhase.TravelBack, run.Phase);
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.PartyRetreated));
        }

        [Test]
        public void EasyQuest_Continue()
        {
            using var quests = new QuestWorld();
            quests.Data.Set("decisions.bestChoiceChance", 1f);
            Adventurer a = quests.Add();
            Order order = quests.AddOrder(requirement: 12f);
            QuestRun run = quests.Start(order, a);
            bool keepGoing = quests.World.Do(ctx => QuestChoices.DecideAfterFailure(ctx, run, order));
            Assert.IsTrue(keepGoing);
        }
    }

    public sealed class QuestTravelTests
    {
        [Test]
        public void TravelEventAndDiscovery_WithTheirChances()
        {
            const int trials = 600;
            int near = 0, far = 0, discoveries = 0;
            using var quests = new QuestWorld();
            for (int t = 0; t < trials; t++)
            {
                Adventurer a = quests.Add();
                QuestRun nearRun = quests.Start(quests.AddOrder(), a);
                if (nearRun.TravelEventHour > 0) near++;
                if (nearRun.Discovery != null) discoveries++;
                Adventurer b = quests.Add();
                QuestRun farRun = quests.Start(quests.AddOrder(distance: OrderDistance.Far), b);
                if (farRun.TravelEventHour > 0) far++;
                Assert.That(farRun.PhaseHoursLeft, Is.InRange(24, 36));
                Assert.AreEqual(3, nearRun.PhaseHoursLeft);
            }
            Assert.That(near, Is.EqualTo(trials * 0.1f).Within(Frequency.Tolerance(trials, 0.1f)));
            Assert.That(far, Is.EqualTo(trials * 0.25f).Within(Frequency.Tolerance(trials, 0.25f)));
            Assert.That(discoveries, Is.EqualTo(trials * 0.05f).Within(Frequency.Tolerance(trials, 0.05f)));
        }

        [Test]
        public void FarTravel_CampsAtNight_TwoToThreeDays()
        {
            using var quests = new QuestWorld();
            Adventurer a = quests.Add();
            QuestRun run = quests.Start(quests.AddOrder(distance: OrderDistance.Far), a);
            int hours = run.PhaseHoursLeft;
            long start = quests.Simulation.World.Time.TotalHours;
            bool camped = false;
            while (run.Phase == QuestPhase.TravelOut)
            {
                quests.Simulation.Tick();
                camped |= a.State.Activity == Activity.OnQuestCamp;
            }
            Assert.IsTrue(camped);
            long elapsed = quests.Simulation.World.Time.TotalHours - start;
            Assert.AreEqual(quests.Simulation.Rhythm.MarchEnd(start + 1, hours), quests.Simulation.World.Time.TotalHours, 1, "путь — ходовые часы с ночлегами");
            Assert.That(elapsed / 24f, Is.InRange(1.5f, 3.2f));
        }

        [Test]
        public void TravelEvent_HeavyWound_GroupMemberTurnsBack()
        {
            bool turnedBack = false;
            for (uint seed = 1; seed < 400 && !turnedBack; seed++)
            {
                using var quests = new QuestWorld(seed);
                Adventurer a = quests.Add();
                Adventurer b = quests.Add();
                QuestRun run = quests.Start(quests.AddOrder(rank: GuildRank.C), a, b);
                quests.World.Do(ctx => QuestEncounters.TravelEvent(ctx, run));
                if (run.TurnedBack.Count == 0) continue;
                turnedBack = true;
                Assert.AreEqual(1, run.Members.Count, "часть группы не дошла");
                Assert.AreEqual(StragglerKind.TurnedBack, quests.Simulation.World.Quests.Stragglers.Single().Kind);
            }
            Assert.IsTrue(turnedBack);
        }

        [Test]
        public void Discovery_SkipThenReport_EventQuestAwaitsPlayer_GuildPays()
        {
            using var quests = new QuestWorld();
            quests.Data.Set("decisions.skipDiscoverySafety", 1f);
            quests.Data.Set("decisions.exploreGlory", -1f); // исследовать невыгодно
            quests.Data.Set("decisions.bestChoiceChance", 1f);
            Adventurer a = quests.Add();
            Order order = quests.AddOrder(requirement: 12f, reward: 100);
            QuestRun run = quests.Start(order, a);
            quests.World.Do(ctx =>
            {
                run.Discovery = new QuestDiscovery("Cave", 1);
                QuestEncounters.Discover(ctx, run);
            });
            Assert.AreEqual(DiscoveryState.Skipped, run.Discovery.State);
            int money = quests.Simulation.World.Treasury.Money;
            List<SimEvent> events = quests.RunToEnd(run);

            Assert.AreEqual(DiscoveryState.Reported, run.Discovery.State);
            Assert.IsTrue(quests.Simulation.World.Orders.TryGetOrder(run.Discovery.EventOrderId, out Order eventQuest));
            Assert.IsTrue(eventQuest.IsEventQuest);
            Assert.AreEqual(OrderStatus.AwaitingPlayer, eventQuest.Status);
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.EventQuestAwaitingPlayer));
            Assert.IsTrue(quests.Simulation.World.Treasury.Ledger.Any(e => e.Category == LedgerCategories.Discoveries && e.Amount == -10));

            quests.Simulation.Send(new AnswerEventQuestCommand(eventQuest.Id, GuildRank.F));
            quests.Simulation.Tick();
            Assert.AreEqual(OrderStatus.OnBoard, eventQuest.Status);
            Assert.AreEqual(GuildRank.F, eventQuest.Rank);
            Assert.That(eventQuest.Reward, Is.InRange(70, 130));
        }

        [Test]
        public void EventQuest_Done_PaidByGuild_NoCommission()
        {
            using var quests = new QuestWorld();
            Adventurer a = quests.Add();
            Order order = quests.AddOrder(requirement: 10f, reward: 100);
            order.IsEventQuest = true;
            QuestRun run = quests.Start(order, a);
            quests.RunToEnd(run);

            Assert.IsTrue(run.Completed);
            IReadOnlyList<LedgerEntry> ledger = quests.Simulation.World.Treasury.Ledger;
            Assert.IsTrue(ledger.Any(e => e.Category == LedgerCategories.EventQuests && e.Amount == -100));
            Assert.IsFalse(ledger.Any(e => e.Category == LedgerCategories.Commission));
        }
    }

    public sealed class QuestSettlementTests
    {
        [Test]
        public void Done_RewardCommissionSurchargeDebt_PointsReputationExperience()
        {
            using var quests = new QuestWorld();
            quests.Data.Set("travel.eventChanceNear", 0f);
            quests.Data.Set("travel.discoveryChance", 0f);
            Adventurer a = quests.Add();
            a.State.Wallet = 0;
            a.State.DebtToGuild = 100;
            Order order = quests.AddOrder(requirement: 10f, reward: 100);
            order.Surcharge = 20;
            QuestRun run = quests.Start(order, a);
            float perception = a.GetStat(StatId.Perception);
            List<SimEvent> events = quests.RunToEnd(run);

            Assert.IsTrue(run.Completed);
            Assert.AreEqual(QuestResult.Brilliant, run.Result);
            IReadOnlyList<LedgerEntry> ledger = quests.Simulation.World.Treasury.Ledger;
            Assert.AreEqual(20, ledger.Where(e => e.Category == LedgerCategories.Commission).Sum(e => e.Amount), "20% с награды");
            Assert.AreEqual(-20, ledger.Where(e => e.Category == LedgerCategories.Surcharges).Sum(e => e.Amount), "доплата — из казны");
            // Доля 100 − 20 + 20 = 100: 20% — в долг, в казну.
            Assert.AreEqual(20, ledger.Where(e => e.Category == LedgerCategories.DebtRepayment).Sum(e => e.Amount));
            Assert.AreEqual(80, a.State.DebtToGuild);
            SimEvent paid = events.Single(e => e.Type == SimEventType.QuestPaid);
            paid.TryGet("trophies", out int trophies);
            Assert.GreaterOrEqual(a.State.Wallet, 80 + trophies - 20, "доля и трофеи (минус суточные расходы)");
            Assert.AreEqual(2f, a.RankPoints, 1e-4, "G = 1, блестящий × 2");
            GuildBalance guild = quests.Data.Balance.Guild;
            Assert.AreEqual(guild.StartReputation + guild.SuccessReputationPerRank * 1, quests.Simulation.World.Guild.Reputation, 1e-4,
                "выполнено: + ранг G (1) × множитель");
            Assert.Greater(a.GetStat(StatId.Perception), perception, "опыт по осям задания");
            Assert.AreEqual(1, a.QuestsCompleted);
            Assert.AreEqual(OrderStatus.Done, order.Status);
        }

        [Test]
        public void Retreat_NoReward_ReputationDown()
        {
            using var quests = new QuestWorld();
            quests.Data.Set("decisions.bestChoiceChance", 1f);
            Adventurer a = quests.Add();
            Order order = quests.AddOrder(requirement: 500f, reward: 100, rank: GuildRank.F);
            order.Surcharge = 30;
            QuestRun run = quests.Start(order, a);
            quests.AtSite(run, failedRounds: 2);
            quests.RunToEnd(run);

            Assert.IsFalse(run.Completed);
            Assert.IsTrue(run.Retreated);
            Assert.AreEqual(QuestResult.Fail, run.Result);
            Assert.IsFalse(quests.Simulation.World.Treasury.Ledger.Any(e => e.Category == LedgerCategories.Commission || e.Category == LedgerCategories.Surcharges),
                "не выполнено — ни награды, ни доплаты");
            GuildBalance guild = quests.Data.Balance.Guild;
            Assert.AreEqual(guild.StartReputation + guild.FailureReputationPerRank * 2, quests.Simulation.World.Guild.Reputation, 1e-4,
                "F: ранг 2 × множитель провала");
            Assert.AreEqual(0f, a.RankPoints);
            Assert.AreEqual(OrderStatus.Failed, order.Status);
        }

        [Test]
        public void Split_ByPowerScore_DeadShareGoesToSurvivors()
        {
            using var quests = new QuestWorld();
            Adventurer strong = quests.Add();
            Adventurer weak = quests.Add();
            strong.PowerScore = 60f;
            weak.PowerScore = 20f;
            int[] shares = QuestSettlement.Split(100, new[] { strong, weak });
            CollectionAssert.AreEqual(new[] { 75, 25 }, shares);
            CollectionAssert.AreEqual(new[] { 34, 33, 33 }, QuestSettlement.Split(100, new[] { quests.Add(), quests.Add(), quests.Add() }.Select(p => { p.PowerScore = 0f; return p; }).ToList()));
        }

        [Test]
        public void Done_WithDeath_FullRewardToSurvivors()
        {
            using var quests = new QuestWorld();
            Adventurer a = quests.Add();
            Adventurer b = quests.Add();
            a.State.Wallet = 0;
            Order order = quests.AddOrder(requirement: 10f, reward: 100);
            QuestRun run = quests.Start(order, a, b);
            quests.World.Do(ctx => QuestRounds.Kill(ctx, run, b, everyone: false));
            quests.RunToEnd(run);

            Assert.IsTrue(run.Completed, "цель выполнена");
            Assert.AreEqual(QuestResult.Success, run.Result, "без провалов, но с гибелью — успех");
            Assert.AreEqual(20, quests.Simulation.World.Treasury.Ledger.Where(e => e.Category == LedgerCategories.Commission).Sum(e => e.Amount),
                "награда целиком");
            Assert.GreaterOrEqual(a.State.Wallet, 80 - 20, "доля погибшего — выжившему");
        }

        [Test]
        public void Promotion_Exam_NoRewardNoReputation_PassRanksUp_FailRetryLater()
        {
            using var quests = new QuestWorld();
            quests.Data.NoQuests(); // экзамен начинаем сами
            Adventurer a = quests.Add();
            a.RankPoints = 10f;
            quests.World.TickToHour(quests.Data.Balance.Time.MorningHour);
            Assert.IsTrue(quests.Simulation.World.Orders.TryGetPromotion(a.Id, out Order exam), "экзамен появился утром");
            Assert.AreEqual(GuildRank.F, exam.Rank);
            Assert.AreEqual(0, exam.Reward);

            exam.Status = OrderStatus.OnBoard;
            for (int i = 0; i < Vocabulary.StatCount; i++) exam.SetRequirement((StatId)i, Vocabulary.IsDiagramAxis((StatId)i) ? 10f : 0f);
            QuestRun run = quests.Start(exam, a);
            Assert.AreEqual(0, run.TravelEventHour, "на экзамене нет событий в пути");
            Assert.IsNull(run.Discovery);
            quests.RunToEnd(run);

            Assert.AreEqual(GuildRank.F, a.GuildRank);
            Assert.AreEqual(0f, a.RankPoints);
            Assert.IsFalse(quests.Simulation.World.Treasury.Ledger.Any(e => e.Category != LedgerCategories.Tavern), "ни награды, ни комиссии");
            Assert.AreEqual(quests.Data.Balance.Guild.StartReputation, quests.Simulation.World.Guild.Reputation, 1e-4);

            using var failing = new QuestWorld();
            failing.Data.NoQuests();
            Adventurer b = failing.Add();
            b.RankPoints = 10f;
            failing.World.TickToHour(failing.Data.Balance.Time.MorningHour);
            failing.Simulation.World.Orders.TryGetPromotion(b.Id, out Order hard);
            for (int i = 0; i < Vocabulary.StatCount; i++) hard.SetRequirement((StatId)i, Vocabulary.IsDiagramAxis((StatId)i) ? 500f : 0f);
            QuestRun failRun = failing.Start(hard, b);
            failing.RunToEnd(failRun);
            Assert.AreEqual(GuildRank.G, b.GuildRank);
            Assert.AreEqual(0, b.State.Conditions.Count, "без ран");
            Assert.Greater(b.PromotionReadyAtHours, failing.Simulation.World.Time.TotalHours + failing.Simulation.Calendar.DaysToHours(25));
        }
    }

    public sealed class QuestRevealTests
    {
        private static Order Order(QuestWorld quests, int reward, GuildRank rank = GuildRank.G, float requirement = 20f) =>
            quests.AddOrder(rank: rank, reward: reward, requirement: requirement);

        private static DecisionAction Action(Order order) =>
            new DecisionAction(DecisionActionKind.TakeOrder, Activity.Resting, false, null, order.Id);

        [Test]
        public void Selfless_TookUnderpaidOrder()
        {
            using var quests = new QuestWorld();
            quests.Simulation.Send(new SetCommissionCommand(0.5f));
            Adventurer a = quests.Add();
            a.SetAxis(AxisId.Money, -50f);
            Order order = Order(quests, reward: 40);
            quests.World.Do(ctx => OrderChoice.Take(ctx, a, Action(order), new[] { Action(order) }));
            Assert.IsTrue(a.IsAxisRevealed(AxisId.Money));
            Assert.AreEqual(OrderStatus.Taken, order.Status);
            Assert.AreEqual(order.Id, a.State.PlannedOrderId);
        }

        [Test]
        public void Greedy_RefusedLowPay_Family_RefusedDanger()
        {
            using var quests = new QuestWorld();
            Adventurer greedy = quests.Add();
            greedy.SetAxis(AxisId.Money, 50f);
            Order cheap = Order(quests, reward: 30);
            quests.World.Do(ctx => OrderChoice.Refused(ctx, greedy, Action(cheap), morning: false));
            Assert.IsTrue(greedy.IsAxisRevealed(AxisId.Money));

            Adventurer family = quests.Add("Family");
            Order dangerous = Order(quests, reward: 60, requirement: 90f);
            quests.World.Do(ctx => OrderChoice.Refused(ctx, family, Action(dangerous), morning: false));
            Assert.IsTrue(family.Traits[0].Revealed);
        }

        [Test]
        public void Lazy_SevenMorningsWithoutOrders()
        {
            using var quests = new QuestWorld();
            Adventurer lazy = quests.Add();
            lazy.SetAxis(AxisId.Work, -50f);
            Order order = Order(quests, reward: 60);
            for (int i = 0; i < 6; i++) quests.World.Do(ctx => OrderChoice.Refused(ctx, lazy, Action(order), morning: true));
            Assert.IsFalse(lazy.IsAxisRevealed(AxisId.Work));
            quests.World.Do(ctx => OrderChoice.Refused(ctx, lazy, Action(order), morning: true));
            Assert.IsTrue(lazy.IsAxisRevealed(AxisId.Work));
        }

        [Test]
        public void Ambitious_TookTopRankOrder()
        {
            using var quests = new QuestWorld();
            Adventurer a = quests.Add();
            a.GuildRank = GuildRank.F;
            a.SetAxis(AxisId.Work, 50f);
            Order low = Order(quests, reward: 50);
            Order top = Order(quests, reward: 90, rank: GuildRank.F);
            quests.World.Do(ctx => OrderChoice.Take(ctx, a, Action(top), new[] { Action(low), Action(top) }));
            Assert.IsTrue(a.IsAxisRevealed(AxisId.Work));
        }

        [Test]
        public void Nightmares_RefusesSameType()
        {
            using var quests = new QuestWorld();
            quests.Data.Set("traits.nightmaresRefuseChance", 1f);
            Adventurer a = quests.Add("Nightmares");
            a.Traits[0].SourceQuestTypeId = "Hunt";
            Order hunt = Order(quests, reward: 60);
            bool refused = false;
            List<SimEvent> events = quests.DoEvents(ctx => refused = OrderChoice.RefusesFromNightmares(ctx, a, Action(hunt)));
            Assert.IsTrue(refused, "отказ от заказа того же типа");
            Assert.IsTrue(a.Traits[0].Revealed);
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.OrderRefused && e.TryGet("reason", out string r) && r.Length > 0));
            Order escort = quests.AddOrder(type: "Escort");
            Assert.IsFalse(quests.World.Do(ctx => OrderChoice.RefusesFromNightmares(ctx, a, Action(escort))));
        }

        [Test]
        public void Unprincipled_CaughtSkimming_Honest_RevealedByHandingIn()
        {
            using var quests = new QuestWorld();
            quests.Data.Set("adventurers.skimCaughtChance", 1f);
            Adventurer thief = quests.Add();
            thief.SetAxis(AxisId.Principles, -50f);
            Adventurer honest = quests.Add();
            honest.SetAxis(AxisId.Principles, 80f);
            honest.JoinedAtHours = -quests.Simulation.Calendar.DaysToHours(70);
            QuestRun run = quests.Start(quests.AddOrder(requirement: 10f, reward: 200), thief, honest);
            List<SimEvent> events = quests.RunToEnd(run);

            Assert.IsTrue(run.Completed);
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.LootSkimmed));
            Assert.IsTrue(thief.IsAxisRevealed(AxisId.Principles));
            Assert.IsTrue(honest.IsAxisRevealed(AxisId.Principles));
        }

        [Test]
        public void Braggart_Overreached_RevealedOnFailure()
        {
            using var quests = new QuestWorld();
            Adventurer a = quests.Add("Braggart");
            // Требования 60, параметры 30: на деле перекрытие 0,25; Хвастун видит требования × 0,6 — около 0,7.
            QuestRun run = quests.Start(quests.AddOrder(requirement: 60f), a);
            Assert.IsTrue(run.Overreached.Contains(a.Id), "видит ≥ 60%, на деле < 40%");

            quests.World.Do(ctx =>
            {
                run.Retreated = true;
                QuestSettlement.Finish(ctx, run);
            });
            Assert.IsTrue(a.Traits[0].Revealed, "не справился");
        }

        [Test]
        public void Veteran_FirstTension_Reveals()
        {
            using var quests = new QuestWorld();
            Adventurer veteran = quests.Add("Veteran");
            QuestRun run = quests.Start(quests.AddOrder(), veteran);
            quests.World.Do(ctx => QuestTension.Moment(ctx, run));
            Assert.IsTrue(veteran.Traits[0].Revealed);
        }
    }

    public sealed class QuestChoiceTests
    {
        private static float Value(QuestWorld quests, Adventurer adventurer, Order order)
        {
            var scores = new float[Vocabulary.MotiveCount];
            float perceived = QuestMath.PerceivedSoloOverlap(adventurer, order, quests.Registry);
            QuestChoices.OrderScores(adventurer, order, perceived, quests.Simulation.World.Treasury.Commission, quests.Registry, scores);
            MotiveWeights weights = Motives.Weigh(adventurer, quests.Registry, false);
            float value = 0f;
            for (int m = 0; m < scores.Length; m++) value += weights[(Motive)m] * Math.Max(-1f, Math.Min(1f, scores[m]));
            return value;
        }

        [Test]
        public void Coward_PrefersEasier_Greedy_PrefersRicher()
        {
            using var quests = new QuestWorld();
            // Награды — края диапазона G…F: доля за вычетом комиссии заметна на фоне расходов за moneyExpenseDays.
            Order easyCheap = quests.AddOrder(requirement: 20f, reward: 30);
            Order hardRich = quests.AddOrder(requirement: 45f, reward: 100);

            Adventurer coward = quests.Add();
            coward.SetAxis(AxisId.Risk, -90f);
            coward.State.Wallet = 0;
            Assert.Greater(Value(quests, coward, easyCheap), Value(quests, coward, hardRich), "трус — легче");

            Adventurer greedy = quests.Add();
            greedy.SetAxis(AxisId.Money, 90f);
            greedy.SetAxis(AxisId.Risk, 60f);
            greedy.State.Wallet = 0;
            Assert.Greater(Value(quests, greedy, hardRich), Value(quests, greedy, easyCheap), "жадный — дороже");
        }

        [Test]
        public void EmptyWallet_RaisesOrderValue_CommissionAndSurchargeMatter()
        {
            using var quests = new QuestWorld();
            Order order = quests.AddOrder(requirement: 20f, reward: 30);
            Adventurer a = quests.Add();
            a.State.Wallet = 1000;
            float rich = Value(quests, a, order);
            a.State.Wallet = 0;
            float poor = Value(quests, a, order);
            Assert.Greater(poor, rich, "пустой кошелёк — Деньги × 2");

            quests.Simulation.Send(new SetCommissionCommand(0.5f));
            quests.Simulation.Tick();
            float highCommission = Value(quests, a, order);
            Assert.Less(highCommission, poor, "высокая комиссия — заказ дешевле");
            order.Surcharge = 30;
            Assert.Greater(Value(quests, a, order), highCommission, "доплата — дороже");
        }

        [Test]
        public void PromotionExam_AboveRank_OnlyOwnerMayTakeIt()
        {
            using var quests = new QuestWorld();
            quests.Data.NoQuests();
            Adventurer owner = quests.Add();
            Adventurer other = quests.Add();
            owner.RankPoints = 10f;
            quests.World.TickToHour(quests.Data.Balance.Time.MorningHour);
            Assert.IsTrue(quests.Simulation.World.Orders.TryGetPromotion(owner.Id, out Order exam));
            Assert.Greater(exam.Rank, owner.GuildRank, "экзамен — на ранг выше");

            var action = new DecisionAction(DecisionActionKind.TakeOrder, Activity.Resting, false, null, exam.Id);
            string[] rankAndOwner = { "order above guild rank", "someone else's promotion" };
            List<string> Bans(Adventurer adventurer) => quests.World.Do(ctx => DecisionBans.Default
                .Where(ban => rankAndOwner.Contains(ban.Reason) && ban.Applies(ctx, adventurer, action)).Select(ban => ban.Reason).ToList());
            Assert.IsEmpty(Bans(owner), "свой экзамен можно взять");
            CollectionAssert.AreEquivalent(new[] { "someone else's promotion" }, Bans(other), "чужой — нельзя");
        }

        [Test]
        public void PeopleTakeOrders_ByThemselves_TakenLeavesBoard()
        {
            using var data = new PeopleData();
            var simulation = Simulation.CreateDefault(data.Registry, 5u);
            List<SimEvent> events = SimulationRun.Collect(simulation, sim => SimulationRun.Days(sim, 10));
            List<SimEvent> taken = events.Where(e => e.Type == SimEventType.OrderTaken).ToList();
            Assert.IsNotEmpty(taken);
            foreach (SimEvent simEvent in taken)
            {
                simEvent.TryGet("order", out int id);
                Assert.IsFalse(simulation.World.Orders.TryGetOpen(id, out _), "взятый заказ ушёл с доски");
            }
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.QuestDeparted));
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.QuestReturned));
            Assert.IsTrue(taken.All(e => simulation.Calendar.At(e.TimeHours).Hour <= data.Balance.Time.LatestDepartureHour - 1));
        }
    }

    public sealed class QuestFeedTests
    {
        [Test]
        public void EveryStep_GivesQuestFeedLine_OfTemplateImportance()
        {
            using var data = new PeopleData();
            var simulation = Simulation.CreateDefault(data.Registry, 12u);
            var expected = new Dictionary<int, int>();
            simulation.TickCompleted += events =>
            {
                foreach (SimEvent simEvent in events)
                {
                    string key = FeedKeys.Of(simEvent, simulation.World, data.Registry);
                    if (key == null || data.Registry.FeedTemplates(key)[0].Feed != FeedKind.Quest) continue;
                    simEvent.TryGet("quest", out int run);
                    expected.TryGetValue(run, out int count);
                    expected[run] = count + 1;
                }
            };
            SimulationRun.Days(simulation, 20);

            Assert.IsNotEmpty(expected);
            foreach (KeyValuePair<int, int> pair in expected)
            {
                if (!simulation.World.Quests.TryGetRun(pair.Key, out QuestRun run)) continue;
                int questLines = run.Log.Count(l => l.TemplateKey.StartsWith("quest."));
                Assert.AreEqual(pair.Value, questLines, $"задание #{run.Id}");
                foreach (FeedEntry entry in run.Log)
                {
                    Assert.AreEqual(run.Id, entry.QuestRunId);
                    StringAssert.DoesNotContain("{", entry.Text);
                    Assert.AreEqual(FeedSystem.ToImportance(data.Registry.FeedTemplates(entry.TemplateKey)[0].Importance), entry.Importance);
                }
            }
        }

        [Test]
        public void Templates_SoloAndGroup_Conditions()
        {
            using var quests = new QuestWorld();
            Adventurer a = quests.Add();
            Adventurer b = quests.Add();
            Adventurer c = quests.Add();
            QuestRun solo = quests.Start(quests.AddOrder(), a);
            QuestRun group = quests.Start(quests.AddOrder(), b, c);
            quests.Simulation.Tick();
            StringAssert.Contains("од", solo.Log.First(l => l.TemplateKey == "quest.departed").Text, "соло — «ушёл один»");
            StringAssert.DoesNotContain("один", group.Log.First(l => l.TemplateKey == "quest.departed").Text);
        }
    }

    public sealed class QuestDeterminismTests
    {
        [Test]
        public void SameSeed_SameQuestsLogAndFeed()
        {
            string Run()
            {
                using var data = new PeopleData();
                var writer = new StringWriter();
                HeadlessRun.Result result = HeadlessRun.Run(data.Registry, 77u, 90, PlayerBots.Simple(), new SimLogger(SimLogLevel.Debug, writer));
                WorldState world = result.Simulation.World;
                IEnumerable<string> quests = world.Quests.Active.Concat(world.Quests.Finished)
                    .Select(q => q.Id + " " + q.Result + " " + string.Join("|", q.Log.Select(l => l.Text)));
                return writer + string.Join("\n", quests) + string.Join("\n", world.Feed.Guild.Select(f => f.Text));
            }

            string first = Run();
            StringAssert.Contains("QuestReturned", first);
            Assert.AreEqual(first, Run());
        }

        [Test]
        public void QuestSystem_WithoutQuests_DoesNotShiftOtherSystems()
        {
            using var data = new PeopleData();
            data.NoQuests();
            string Run(bool withQuests)
            {
                List<ISimSystem> systems = SimulationSystems.CreateDefault();
                if (!withQuests) systems.RemoveAll(s => s is QuestSystem);
                var simulation = new Simulation(data.Registry, 9u, systems);
                string log = SimulationLog.Record(simulation, sim => SimulationRun.Days(sim, 90));
                return string.Join("\n", log.Split('\n').Where(line => !line.Contains("[QuestSystem]"))) + PeopleDump.Of(simulation);
            }

            Assert.AreEqual(Run(withQuests: false), Run(withQuests: true));
        }
    }
}
