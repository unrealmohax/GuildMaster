using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GuildMaster.Core;
using GuildMaster.Data;
using NUnit.Framework;

namespace GuildMaster.Tests
{
    /// <summary>Постройки и персонал: старт, стройка и очередь, Общежитие, Лазарет, двор, найм, зарплаты и долг.</summary>
    public sealed class BuildingStaffTests
    {
        private StateWorld world;

        [TearDown]
        public void TearDown() => world?.Dispose();

        /// <summary>Постройка сразу готова (в обход стройки).</summary>
        private static Building Ready(StateWorld w, string definitionId) => w.Do(ctx =>
        {
            Building building = ctx.World.Buildings.Create(definitionId);
            building.State = BuildingState.Ready;
            return building;
        });

        private static StaffMember Hire(StateWorld w, string roleId, int level) => w.Do(ctx =>
        {
            var member = new StaffMember(ctx.World.Staff.NextId(), "Сотрудник", Gender.Male, roleId, level, 100, ctx.World.Time.TotalHours);
            ctx.World.Staff.AddMember(member);
            return member;
        });

        private static void SetMoney(StateWorld w, int money) => w.Do(ctx => ctx.World.Treasury.Money = money);

        private static void Build(StateWorld w, string definitionId) => w.Do(ctx => new StartBuildingCommand(definitionId).Apply(ctx));

        private static void TickToMonthStart(StateWorld w)
        {
            do w.Simulation.Tick();
            while (!(w.Time.Day == 1 && w.Time.Hour == 0));
        }

        // ---------- Старт ----------

        /// <summary>Утро следующего дня и час после него: кандидаты на вакансию приходят в начале утра.</summary>
        private static void NextMorning(StateWorld w)
        {
            w.TickToHour(w.Balance.Time.MorningHour);
            w.Simulation.Tick();
        }

        [Test]
        public void Start_HallAndTavern_RegistrarAndInnkeeper()
        {
            using var data = new PeopleData(); // StateWorld выравнивает уровни персонала — здесь нужен настоящий старт
            WorldState state = Simulation.CreateDefault(data.Registry, 7u).World;
            CollectionAssert.AreEquivalent(new[] { "GuildHall", "Tavern" }, state.Buildings.All.Select(b => b.DefinitionId));
            Assert.IsTrue(state.Buildings.All.All(b => b.IsReady));
            CollectionAssert.AreEquivalent(new[] { "Registrar", "Innkeeper" }, state.Staff.Members.Select(m => m.RoleId));

            foreach (StaffMember member in state.Staff.Members)
            {
                Assert.That(member.Level, Is.InRange(30, 70));
                var role = data.Registry.Get<StaffRoleDefinition>(member.RoleId);
                Assert.AreEqual(StaffRules.AskedSalary(role, member.Level, data.Balance.Staff), member.Salary, "стартовые — за просимую");
                Assert.IsFalse(string.IsNullOrEmpty(member.Name));
            }
        }

        [Test]
        public void Start_SameSeed_SameStaff_OwnStream()
        {
            world = new StateWorld(11u);
            using var other = new StateWorld(11u);
            CollectionAssert.AreEqual(world.Simulation.World.Staff.Members.Select(m => m.Name + m.Level),
                other.Simulation.World.Staff.Members.Select(m => m.Name + m.Level));
        }

        // ---------- Стройка и очередь ----------

        [Test]
        public void Building_DebitsCost_TakesBuildDays_ThenReady()
        {
            world = new StateWorld();
            int money = world.Simulation.World.Treasury.Money;
            long start = world.Time.TotalHours;
            List<SimEvent> events = world.Collect(() => Build(world, "TrainingYard"));

            Building yard = world.Simulation.World.Buildings.Current;
            Assert.IsNotNull(yard);
            Assert.AreEqual(BuildingState.UnderConstruction, yard.State);
            Assert.AreEqual(money - 600, world.Simulation.World.Treasury.Money);
            Assert.AreEqual(LedgerCategories.Construction, world.Simulation.World.Treasury.Ledger.Last().Category);
            Assert.AreEqual(start + 7 * 24, yard.ConstructionEndsAtHours);

            while (world.Time.TotalHours < yard.ConstructionEndsAtHours - 1) world.Simulation.Tick();
            Assert.IsFalse(yard.IsReady, "за час до срока ещё строится");
            events = world.Collect(() => world.Simulation.Tick());
            Assert.IsTrue(yard.IsReady);
            Assert.IsNull(world.Simulation.World.Buildings.Current);
            Assert.AreEqual(1, events.Count(e => e.Type == SimEventType.BuildingReady));
        }

        [Test]
        public void Queue_OneAtATime_WaitsForMoney_ReorderAndCancel()
        {
            world = new StateWorld();
            SetMoney(world, 1000);
            BuildingBook book = world.Simulation.World.Buildings;

            Build(world, "TrainingYard"); // 600 — начинается сразу
            List<SimEvent> events = world.Collect(() =>
            {
                Build(world, "Dormitory");
                Build(world, "Infirmary");
                world.Simulation.Tick();
            });
            Assert.AreEqual("TrainingYard", book.Current.DefinitionId);
            Assert.AreEqual(400, world.Simulation.World.Treasury.Money, "деньги очереди не списаны");
            Assert.AreEqual(2, events.Count(e => e.Type == SimEventType.BuildingQueued));
            CollectionAssert.AreEqual(new[] { "Dormitory", "Infirmary" }, book.Queue.Select(b => b.DefinitionId));

            int dormitory = book.Queue[0].Id;
            int infirmary = book.Queue[1].Id;
            world.Do(ctx => new ReorderBuildQueueCommand(new[] { infirmary, dormitory }).Apply(ctx));
            CollectionAssert.AreEqual(new[] { "Infirmary", "Dormitory" }, book.Queue.Select(b => b.DefinitionId));
            world.Do(ctx => new ReorderBuildQueueCommand(new[] { infirmary }).Apply(ctx));
            Assert.AreEqual(2, book.Queue.Count, "неполный порядок — ничего");
            world.Do(ctx => new CancelBuildingCommand(dormitory).Apply(ctx));
            CollectionAssert.AreEqual(new[] { "Infirmary" }, book.Queue.Select(b => b.DefinitionId));

            while (!book.IsReady("TrainingYard")) world.Simulation.Tick();
            world.Simulation.Tick();
            Assert.IsNull(book.Current, "денег меньше цены — очередь ждёт");
            Assert.AreEqual(400, world.Simulation.World.Treasury.Money);

            SetMoney(world, 5000);
            world.Simulation.Tick();
            Assert.AreEqual("Infirmary", book.Current?.DefinitionId, "деньги появились — стройка началась");
            Assert.AreEqual(4000, world.Simulation.World.Treasury.Money);

            Build(world, "Infirmary");
            Build(world, "TrainingYard");
            Assert.AreEqual(0, book.Queue.Count, "постройка уже есть — вторую не заказать");
        }

        // ---------- Общежитие ----------

        [Test]
        public void Dormitory_MovesLongestServing_UpToCapacity_ChangesCostsAndContentment()
        {
            world = new StateWorld();
            List<Adventurer> people = world.AddMany(12);
            Ready(world, "Dormitory");
            world.TickToHour(0);

            Assert.AreEqual(10, people.Count(a => a.Housing == Housing.Dorm), "10 мест");
            Assert.IsTrue(people.Take(10).All(a => a.Housing == Housing.Dorm), "первыми — кто раньше в гильдии (при равенстве — меньший id)");
            Assert.IsTrue(people.Skip(10).All(a => a.Housing == Housing.City));

            ExpensesBalance expenses = world.Balance.Expenses;
            Assert.AreEqual(2 + 1, WalletService.DailyLivingCost(people[0], false, expenses));
            Assert.AreEqual(2 + 3, WalletService.DailyLivingCost(people[11], false, expenses));

            float commission = world.Simulation.World.Treasury.Commission;
            Assert.AreEqual(10f, StateRules.ContentmentTarget(people[0], commission, world.Registry)
                - StateRules.ContentmentTarget(people[11], commission, world.Registry), 1e-4f, "Общежитие — довольство выше");

            world.TickToHour(23);
            int ledgerFrom = world.Simulation.World.Treasury.Ledger.Count;
            world.TickToHour(0);
            int dormitoryIncome = world.Simulation.World.Treasury.Ledger.Skip(ledgerFrom)
                .Where(e => e.Category == LedgerCategories.Dormitory).Sum(e => e.Amount);
            Assert.AreEqual(10, dormitoryIncome, "плата за место — в казну");

            world.Do(ctx => AdventurerLifecycle.Retire(ctx, people[0], LeaveReason.Left));
            world.TickToHour(0);
            Assert.AreEqual(Housing.Dorm, people[10].Housing, "место освободилось — переселяется следующий");
        }

        // ---------- Лазарет ----------

        [Test]
        public void Infirmary_HealsFasterOnlyWithHealer_BedsLimited()
        {
            world = new StateWorld();
            world.Data.Set("health.complicationChance", 0f);
            Ready(world, "Infirmary");

            Adventurer early = world.Add();
            Condition wound = world.Do(ctx => HealthService.Wound(ctx, early, ConditionKind.HeavyWound));
            world.Simulation.Tick();
            Assert.IsFalse(early.State.InInfirmary, "без Лекаря Лазарет не лечит");

            Hire(world, "Healer", 50); // уровень 50 — ×1
            List<Adventurer> patients = world.AddMany(5);
            foreach (Adventurer patient in patients) world.Do(ctx => HealthService.Wound(ctx, patient, ConditionKind.HeavyWound));
            world.Simulation.Tick();
            Assert.AreEqual(4, world.Simulation.World.Adventurers.Active.Count(a => a.State.InInfirmary), "4 койки");
            Assert.IsTrue(early.State.InInfirmary, "первым — кто раньше ранен");

            float remaining = wound.RemainingDays;
            world.TickToHour(0);
            Assert.AreEqual(remaining - 1f / 0.7f, wound.RemainingDays, 1e-3f, "в Лазарете срок × 0,7");

            Adventurer outside = patients.First(a => !a.State.InInfirmary);
            Condition outsideWound = outside.State.Conditions[0];
            float before = outsideWound.RemainingDays;
            world.TickToHour(0);
            Assert.AreEqual(before - 1f / 1.5f, outsideWound.RemainingDays, 1e-3f, "без койки — × 1,5");
        }

        [Test]
        public void Infirmary_HealerLevelSpeedsUp_FeeGoesToTreasury()
        {
            world = new StateWorld();
            world.Data.Set("health.complicationChance", 0f);
            Ready(world, "Infirmary");
            Hire(world, "Healer", 100); // 0,8 + 100/250 = 1,2
            Adventurer patient = world.Add();
            Condition wound = world.Do(ctx => HealthService.Wound(ctx, patient, ConditionKind.HeavyWound));
            world.TickToHour(0);
            float remaining = wound.RemainingDays;
            int ledgerFrom = world.Simulation.World.Treasury.Ledger.Count;
            world.TickToHour(0);
            Assert.AreEqual(remaining - 1.2f / 0.7f, wound.RemainingDays, 1e-3f);
            Assert.AreEqual(5, world.Simulation.World.Treasury.Ledger.Skip(ledgerFrom)
                .Where(e => e.Category == LedgerCategories.Infirmary).Sum(e => e.Amount), "5 в день — в казну");

            patient.State.Wallet = 2;
            ledgerFrom = world.Simulation.World.Treasury.Ledger.Count;
            world.TickToHour(0);
            Assert.AreEqual(2, world.Simulation.World.Treasury.Ledger.Skip(ledgerFrom)
                .Where(e => e.Category == LedgerCategories.Infirmary).Sum(e => e.Amount), "недостача — недополученный доход");
        }

        [Test]
        public void LightWounded_ChoosesToHeal_WhenBedAndHealer()
        {
            world = new StateWorld();
            world.Data.Set("decisions.bestChoiceChance", 1f);
            world.Data.NoQuests();
            Ready(world, "Infirmary");
            Hire(world, "Healer", 50);
            Adventurer adventurer = world.Add();
            world.Do(ctx => HealthService.Wound(ctx, adventurer, ConditionKind.LightWound));

            List<SimEvent> events = world.Collect(() => world.TickToHour(world.Balance.Time.MorningHour + 2));
            Assert.IsTrue(adventurer.State.InInfirmary, "лечение ценнее отдыха");
            Assert.AreEqual(Activity.Infirmary, adventurer.State.Activity);
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.InfirmaryAdmitted && e.TryGet("self", out bool self) && self));
        }

        // ---------- Тренировочный двор ----------

        [Test]
        public void Yard_PeopleChooseToTrain_CapacityLimited_FeeAndGrowth()
        {
            world = new StateWorld(3u);
            world.Data.Set("decisions.bestChoiceChance", 1f);
            world.Data.NoQuests();
            Ready(world, "TrainingYard");
            List<Adventurer> ambitious = world.AddMany(10);
            foreach (Adventurer adventurer in ambitious) adventurer.SetAxis(AxisId.Work, 100f);
            float[] before = ambitious.Select(TrainableSum).ToArray();

            int maxTrainees = 0;
            List<SimEvent> events = world.Collect(() =>
            {
                for (int i = 0; i < 24 * 3; i++)
                {
                    world.Simulation.Tick();
                    maxTrainees = Math.Max(maxTrainees, ambitious.Count(a => a.State.Activity == Activity.Training));
                }
            });

            Assert.AreEqual(6, maxTrainees, "мест на дворе — 6");
            int sessions = events.Count(e => e.Type == SimEventType.TrainingStarted);
            Assert.That(sessions, Is.GreaterThanOrEqualTo(6 * 3), "амбициозные сами идут тренироваться");
            int fees = world.Simulation.World.Treasury.Ledger.Where(e => e.Category == LedgerCategories.TrainingYard).Sum(e => e.Amount);
            Assert.AreEqual(2 * sessions, fees, "2 за день тренировки — в казну");
            Assert.That(ambitious.Select((a, i) => TrainableSum(a) - before[i]).Sum(), Is.GreaterThan(0f), "параметры растут");
        }

        private static float TrainableSum(Adventurer adventurer)
        {
            float sum = 0f;
            for (int i = 0; i < Vocabulary.StatCount; i++)
            {
                if (Growth.IsTrainable((StatId)i)) sum += adventurer.GetStat((StatId)i);
            }
            return sum;
        }

        [Test]
        public void Training_LastsTrainingHours_OncePerDay()
        {
            world = new StateWorld();
            world.Data.Set("decisions.bestChoiceChance", 1f);
            world.Data.NoQuests();
            Ready(world, "TrainingYard");
            Adventurer adventurer = world.Add();
            adventurer.SetAxis(AxisId.Work, 100f);
            int wallet = adventurer.State.Wallet;

            int hours = 0;
            List<SimEvent> events = world.Collect(() =>
            {
                for (int i = 0; i < 24; i++)
                {
                    world.Simulation.Tick();
                    if (adventurer.State.Activity == Activity.Training) hours++;
                }
            });
            Assert.AreEqual(world.Balance.Growth.TrainingHoursPerDay, hours, "занятие — день тренировки");
            Assert.AreEqual(1, events.Count(e => e.Type == SimEventType.TrainingStarted), "второй раз за сутки нельзя");
            Assert.That(wallet - adventurer.State.Wallet, Is.GreaterThanOrEqualTo(2));
        }

        // ---------- Персонал ----------

        [Test]
        public void Vacancy_TwoCandidatesEveryThreeDays_WaitThreeDays()
        {
            world = new StateWorld();
            world.TickToHour(world.Balance.Time.MorningHour + 1);
            Assert.AreEqual(0, world.Simulation.World.Staff.Candidates.Count, "вакансий нет — кандидатов нет");

            Ready(world, "Infirmary");
            List<SimEvent> events = world.Collect(() => NextMorning(world));
            IReadOnlyList<StaffCandidate> candidates = world.Simulation.World.Staff.Candidates;
            Assert.AreEqual(2, candidates.Count);
            Assert.IsTrue(candidates.All(c => c.RoleId == "Healer" && c.Level >= 30 && c.Level <= 70));
            Assert.IsTrue(candidates.All(c => c.AskedSalary == StaffRules.AskedSalary(world.Registry.Get<StaffRoleDefinition>("Healer"), c.Level, world.Balance.Staff)));
            Assert.AreEqual(2, events.Count(e => e.Type == SimEventType.StaffCandidateArrived));
            int[] first = candidates.Select(c => c.Id).ToArray();

            world.Days(2);
            CollectionAssert.AreEqual(first, world.Simulation.World.Staff.Candidates.Select(c => c.Id), "ждут 3 дня, новых нет");
            world.Days(1);
            Assert.AreEqual(2, world.Simulation.World.Staff.Candidates.Count);
            CollectionAssert.IsEmpty(world.Simulation.World.Staff.Candidates.Select(c => c.Id).Intersect(first), "старые ушли, пришли новые");
        }

        [Test]
        public void Offer_AtAskedSalary_Hires_OthersLeave_WorksImmediately()
        {
            world = new StateWorld();
            Ready(world, "Infirmary");
            NextMorning(world);
            StaffCandidate candidate = world.Simulation.World.Staff.Candidates[0];

            List<SimEvent> events = world.Collect(() =>
            {
                world.Do(ctx => new OfferSalaryCommand(candidate.Id, candidate.AskedSalary).Apply(ctx));
                world.Simulation.Tick();
            });

            Assert.IsTrue(world.Simulation.World.Staff.TryGetByRole("Healer", out StaffMember healer));
            Assert.AreEqual(candidate.AskedSalary, healer.Salary);
            Assert.AreEqual(candidate.Level, healer.Level);
            CollectionAssert.IsEmpty(world.Simulation.World.Staff.Candidates, "место занято — второй кандидат ушёл");
            Assert.AreEqual(1, events.Count(e => e.Type == SimEventType.StaffHired));
            Assert.IsTrue(world.Do(ctx => BuildingInfirmary.Instance.HasMedic(ctx)), "Лекарь работает сразу");
        }

        [Test]
        public void Offer_TooLow_Refused_CandidateLeaves()
        {
            world = new StateWorld();
            Ready(world, "Infirmary");
            NextMorning(world);
            StaffCandidate candidate = world.Simulation.World.Staff.Candidates[0];

            List<SimEvent> events = world.Collect(() =>
            {
                world.Do(ctx => new OfferSalaryCommand(candidate.Id, 1).Apply(ctx));
                world.Simulation.Tick(); // события команды уходят вместе с тактом
            });
            Assert.IsFalse(world.Simulation.World.Staff.HasRole("Healer"));
            Assert.IsFalse(world.Simulation.World.Staff.TryGetCandidate(candidate.Id, out _));
            Assert.AreEqual(1, world.Simulation.World.Staff.Candidates.Count);
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.StaffCandidateLeft && e.TryGet("cause", out string cause) && cause == "refused"));
        }

        [Test]
        public void AcceptChance_Formula()
        {
            using var data = new PeopleData();
            StaffBalance staff = data.Balance.Staff;
            Assert.AreEqual(1f, StaffRules.AcceptChance(200, 200, 0f, staff));
            Assert.AreEqual(1f, StaffRules.AcceptChance(200, 300, 0f, staff));
            Assert.AreEqual(1f - 30f / 60f, StaffRules.AcceptChance(200, 170, 0f, staff), 1e-5f);
            Assert.AreEqual(1f - 30f / 60f + 50f / 200f, StaffRules.AcceptChance(200, 170, 50f, staff), 1e-5f, "репутация помогает");
            Assert.AreEqual(0f, StaffRules.AcceptChance(200, 100, 0f, staff));

            var healer = data.Registry.Get<StaffRoleDefinition>("Healer");
            Assert.AreEqual(250 * (0.7f + 0.3f), StaffRules.AskedSalary(healer, 30, staff));
            Assert.AreEqual(250 * (0.7f + 0.7f), StaffRules.AskedSalary(healer, 70, staff));
            Assert.AreEqual(1f, StaffRules.LevelEffect(50, staff), 1e-5f);
        }

        [Test]
        public void MonthStart_UpkeepThenSalaries()
        {
            world = new StateWorld();
            WorldState state = world.Simulation.World;
            int salaries = StaffRules.MonthlySalaries(state);
            int upkeep = BuildingRules.MonthlyUpkeep(state, world.Registry);
            Assert.AreEqual(90, upkeep, "Зал 50 + Таверна 40");

            world.TickToHour(23);
            int ledgerFrom = state.Treasury.Ledger.Count;
            TickToMonthStart(world);
            List<LedgerEntry> month = state.Treasury.Ledger.Skip(ledgerFrom).ToList();
            Assert.AreEqual(-upkeep, month.Where(e => e.Category == LedgerCategories.Upkeep).Sum(e => e.Amount));
            Assert.AreEqual(-salaries, month.Where(e => e.Category == LedgerCategories.Salaries).Sum(e => e.Amount));
            int lastUpkeep = month.FindLastIndex(e => e.Category == LedgerCategories.Upkeep);
            int firstSalary = month.FindIndex(e => e.Category == LedgerCategories.Salaries);
            Assert.That(lastUpkeep, Is.LessThan(firstSalary), "сначала содержание, потом зарплаты");
        }

        [Test]
        public void UnpaidSalary_Debt_RepaidWhenMoneyAppears()
        {
            world = new StateWorld();
            WorldState state = world.Simulation.World;
            SetMoney(world, 0);
            List<SimEvent> events = world.Collect(() => TickToMonthStart(world));

            Assert.IsTrue(state.Staff.Members.All(m => m.UnpaidSalary == m.Salary && m.UnpaidMonths == 1), "денег нет — долг");
            Assert.AreEqual(2, events.Count(e => e.Type == SimEventType.SalaryUnpaid));
            Assert.Less(state.Treasury.Money, 0, "содержание — даже в минус");
            Assert.IsFalse(state.Treasury.Ledger.Any(e => e.Category == LedgerCategories.Salaries), "в минус ради зарплаты не уходим");

            SetMoney(world, 10000);
            world.TickToHour(0);
            Assert.IsTrue(state.Staff.Members.All(m => m.UnpaidSalary == 0 && m.UnpaidMonths == 0), "долг погашен в 00:00");
            Assert.AreEqual(2, state.Staff.Members.Count);
        }

        [Test]
        public void UnpaidSalary_QuitAfterOnePayPeriod_WithAutopause()
        {
            world = new StateWorld();
            WorldState state = world.Simulation.World;
            SetMoney(world, 0);
            TickToMonthStart(world);
            Assert.AreEqual(2, state.Staff.Members.Count, "первый срок — ещё служат");

            SetMoney(world, 0);
            List<SimEvent> events = world.Collect(() => TickToMonthStart(world));
            Assert.AreEqual(0, state.Staff.Members.Count, "долг висит платёжный срок — ушли");
            Assert.AreEqual(2, state.Staff.Former.Count(m => m.LeaveReason == StaffLeaveReason.Quit));
            Assert.AreEqual(2, events.Count(e => e.Type == SimEventType.StaffQuit && e.Importance == EventImportance.Important));
            Assert.AreEqual(AutopauseKind.MemberLeftGuild, AutopauseRules.Default[SimEventType.StaffQuit]);
        }

        [Test]
        public void Dismiss_OpensVacancy()
        {
            world = new StateWorld();
            StaffMember registrar = world.Simulation.World.Staff.Members.First(m => m.RoleId == "Registrar");
            world.Do(ctx => new DismissStaffCommand(registrar.Id).Apply(ctx));
            Assert.AreEqual(StaffLeaveReason.Dismissed, registrar.LeaveReason);
            NextMorning(world);
            Assert.AreEqual(2, world.Simulation.World.Staff.Candidates.Count(c => c.RoleId == "Registrar"));
        }

        [Test]
        public void Innkeeper_ScalesTavernStressRelief()
        {
            world = new StateWorld();
            Adventurer adventurer = world.Add();
            StaffMember innkeeper = world.Simulation.World.Staff.Members.First(m => m.RoleId == "Innkeeper");

            float Relief()
            {
                adventurer.State.Stress = 50f;
                adventurer.State.Activity = Activity.Tavern;
                world.Do(ctx => StateService.ApplyHour(ctx, adventurer, PartyContext.None));
                return 50f - adventurer.State.Stress;
            }

            innkeeper.Level = 100;
            Assert.AreEqual(1.2f, Relief(), 1e-4f, "0,8 + 100/250");
            innkeeper.Level = 25;
            Assert.AreEqual(0.9f, Relief(), 1e-4f);
            world.Do(ctx => new DismissStaffCommand(innkeeper.Id).Apply(ctx));
            Assert.AreEqual(0.8f, Relief(), 1e-4f, "без Трактирщика — как уровень 0");
        }

        // ---------- Отчёт, детерминизм ----------

        [Test]
        public void MonthReport_BuildingsAndStaffSections()
        {
            world = new StateWorld();
            Build(world, "TrainingYard");
            Build(world, "Dormitory");
            Ready(world, "Infirmary");
            NextMorning(world);
            StaffCandidate candidate = world.Simulation.World.Staff.Candidates[0];
            world.Do(ctx => new OfferSalaryCommand(candidate.Id, candidate.AskedSalary).Apply(ctx));
            TickToMonthStart(world);

            List<string> lines = MonthReportText.Lines(world.Simulation.World.Reports.GetLast(), world.Simulation.World, world.Registry);
            TestContext.WriteLine(string.Join("\n", lines));
            CollectionAssert.Contains(lines, "Постройки");
            Assert.IsTrue(lines.Any(l => l.StartsWith("Построено:") && l.Contains("Тренировочный двор")));
            Assert.IsTrue(lines.Any(l => l.StartsWith("Персонал")));
            Assert.IsTrue(lines.Any(l => l.StartsWith("Наняты:") && l.Contains("Лекарь гильдии")));
            CollectionAssert.Contains(lines, "Стройка: -1400", "статья «Стройка» в деньгах");
        }

        [Test]
        public void SameSeed_WithBuildingsAndStaff_SameLog()
        {
            string Run()
            {
                using var data = new PeopleData();
                var text = new StringWriter();
                var log = new SimLogger(SimLogLevel.Debug, text);
                var simulation = Simulation.CreateDefault(data.Registry, 42u, log);
                SimulationRun.Do(simulation, ctx =>
                {
                    ctx.World.Treasury.Money = 10000;
                    new StartBuildingCommand("TrainingYard").Apply(ctx);
                    new StartBuildingCommand("Infirmary").Apply(ctx);
                    new StartBuildingCommand("Dormitory").Apply(ctx);
                });
                for (int i = 0; i < 24 * 45; i++)
                {
                    foreach (StaffCandidate candidate in simulation.World.Staff.Candidates.ToList())
                        simulation.Send(new OfferSalaryCommand(candidate.Id, candidate.AskedSalary - 20));
                    simulation.Tick();
                }
                return text.ToString();
            }

            string first = Run();
            StringAssert.Contains("staff offer", first);
            StringAssert.Contains("BuildingReady", first);
            Assert.AreEqual(first, Run());
        }

        [Test]
        public void NewSystems_WithoutBuildingsAndVacancies_DoNotShiftOtherRolls()
        {
            string Run(bool withNewSystems)
            {
                using var data = new PeopleData();
                var text = new StringWriter();
                var log = new SimLogger(SimLogLevel.Debug, text);
                List<ISimSystem> systems = SimulationSystems.CreateDefault();
                if (!withNewSystems) systems.RemoveAll(s => s is StaffSystem || s is BuildingSystem || s is SalarySystem);
                var simulation = new Simulation(data.Registry, 5u, systems, log);
                SimulationRun.Days(simulation, 60);
                string[] skip = { "[StaffSystem]", "[BuildingSystem]", "[SalarySystem]", "[MonthReportSystem]", "[FeedSystem]", "ledger ", "MonthReportReady", "report " };
                return string.Join("\n", text.ToString().Split('\n').Where(line => !skip.Any(line.Contains)));
            }

            string without = Run(withNewSystems: false);
            StringAssert.Contains("roll ", without);
            Assert.AreEqual(without, Run(withNewSystems: true));
        }
    }
}
