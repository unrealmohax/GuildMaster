using System.Collections.Generic;
using System.Linq;
using GuildMaster.Core;
using GuildMaster.Data;
using GuildMaster.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace GuildMaster.Tests
{
    /// <summary>
    /// Интерфейс решений: действия игрока на экранах уходят командами и на паузе сразу видны в моделях; уведомления ведут
    /// к окнам решений; журнал казны фильтруется, отчёт открывается за любой прошедший месяц; поражение показывает итоги и
    /// начинает игру заново с новым зерном.
    /// </summary>
    public sealed class UiDecisionTests
    {
        private const uint Seed = 7;

        private PeopleData data;
        private GameObject go;
        private UiRoot root;
        private Simulation simulation;

        [SetUp]
        public void SetUp() => data = new PeopleData();

        [TearDown]
        public void TearDown()
        {
            if (go != null) Object.DestroyImmediate(go);
            data.Dispose();
        }

        /// <summary>Перезапуск игры, как у запуска игры: новая симуляция с этим зерном, интерфейс привязывается к ней.</summary>
        private sealed class FakeSession : IGameSession
        {
            private readonly UiDecisionTests owner;

            public FakeSession(UiDecisionTests owner, uint seed)
            {
                this.owner = owner;
                Seed = seed;
            }

            public uint Seed { get; private set; }
            public int Restarts { get; private set; }

            public void Restart(uint seed)
            {
                Seed = seed;
                Restarts++;
                owner.simulation = Simulation.CreateDefault(owner.data.Registry, seed);
                owner.root.Bind(owner.simulation, new GameClock(owner.data.Registry.Balance.Time, false, startPaused: true), this);
            }

            public void Advance(int hours) { }
        }

        private FakeSession Build(int days = 0)
        {
            simulation = Simulation.CreateDefault(data.Registry, Seed);
            if (days > 0) SimulationRun.Days(simulation, days);
            go = new GameObject("UiDecisionTest", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            root = go.AddComponent<UiRoot>();
            var session = new FakeSession(this, Seed);
            root.Bind(simulation, new GameClock(data.Registry.Balance.Time, false, startPaused: true), session);
            return session;
        }

        /// <summary>Пауза: команды применяются сразу, интерфейс перерисовывается.</summary>
        private void ApplyOnPause()
        {
            simulation.ApplyCommandsNow();
            root.RefreshNow();
        }

        private T Show<T>(ScreenId id) where T : ScreenView
        {
            root.Navigator.ShowScreen(id);
            return (T)root.Navigator.Current;
        }

        /// <summary>Сделать открытый заказ ждущим ответа игрока: важным или событийным.</summary>
        private Order MakeAwaiting(bool eventQuest)
        {
            Order order = simulation.World.Orders.Open.First(o => o.Status == OrderStatus.OnBoard);
            SimulationRun.Do(simulation, ctx =>
            {
                order.Status = OrderStatus.AwaitingPlayer;
                order.IsImportant = !eventQuest;
                order.IsEventQuest = eventQuest;
                order.SourceRank = GuildRank.F;
                order.AnswerDueAtHours = ctx.World.Time.TotalHours + 48;
            });
            return order;
        }

        // ---------- Доска ----------

        [Test]
        public void Board_Surcharge_FromField_VisibleAtOnceOnPause()
        {
            Build();
            Order order = simulation.World.Orders.Open.First(o => o.Status == OrderStatus.OnBoard);
            root.Navigator.Go(Destination.ToScreen(ScreenId.Board, order.Id));
            var board = (BoardScreen)root.Navigator.Current;
            Assert.IsTrue(board.Model.CanSetSurcharge, "заказу на доске можно назначить доплату");

            board.SurchargeField.text = "1 250";
            board.ApplySurcharge();
            ApplyOnPause();

            Assert.AreEqual(1250, order.Surcharge);
            Assert.AreEqual(1250, board.Model.Selected.Surcharge, "доплата видна в модели доски сразу");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Board_Awaiting_ImportantAccepted_WithSurcharge_AndDeclined()
        {
            Build();
            Order accepted = MakeAwaiting(false);
            var board = Show<BoardScreen>(ScreenId.Board);
            Assert.IsTrue(board.Model.Awaiting.Any(a => a.Id == accepted.Id && !a.IsEventQuest), "важный заказ — в «Ждут решения»");

            BoardActions.AnswerImportant(simulation, accepted.Id, true, 300);
            ApplyOnPause();
            Assert.AreEqual(OrderStatus.OnBoard, accepted.Status);
            Assert.AreEqual(300, accepted.Surcharge, "принят с доплатой");
            Assert.IsFalse(board.Model.Awaiting.Any(a => a.Id == accepted.Id), "ответ снимает заказ из «Ждут решения»");

            Order declined = MakeAwaiting(false);
            root.RefreshNow();
            board.AnswerAwaiting(declined.Id, false);
            ApplyOnPause();
            Assert.IsFalse(declined.IsOpen, "отклонён — в архиве");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Board_Awaiting_EventQuest_RankChosen()
        {
            Build();
            Order order = MakeAwaiting(true);
            var board = Show<BoardScreen>(ScreenId.Board);
            AwaitingRow row = board.Model.Awaiting.Single(a => a.Id == order.Id);
            Assert.IsTrue(row.IsEventQuest);

            BoardActions.AnswerEventQuest(simulation, order.Id, GuildRank.D);
            ApplyOnPause();
            Assert.AreEqual(OrderStatus.OnBoard, order.Status);
            Assert.AreEqual(GuildRank.D, order.Rank, "ранг назначен игроком");
            Assert.IsTrue(board.Model.Rows.Any(r => r.Id == order.Id && r.Rank == GuildRank.D));
        }

        [Test]
        public void Registrar_TwoChangesBeforeTick_BothApplied_AndCommission()
        {
            Build();
            var board = Show<BoardScreen>(ScreenId.Board);
            RegistrarModel registrar = board.Registrar;
            List<RegistrarTypeRow> types = registrar.Types.ToList();
            Assert.AreEqual(RegistrarModel.RegistrarTypes(data.Registry).Count, types.Count);
            Assert.IsTrue(types.All(t => t.Allowed), "по умолчанию — все типы");

            // Две правки подряд, мир их ещё не применил: вторая строится от первой.
            registrar.SetTypeAllowed(simulation, types[0].Id, false);
            registrar.SetTypeAllowed(simulation, types[1].Id, false);
            registrar.SetMaxRank(simulation, GuildRank.E);
            registrar.SetMinReward(simulation, 80);
            RegistrarModel.SetCommissionPercent(simulation, 35);
            ApplyOnPause();

            RegistrarRules rules = simulation.World.Orders.Rules;
            Assert.IsFalse(rules.IsTypeAllowed(types[0].Id));
            Assert.IsFalse(rules.IsTypeAllowed(types[1].Id));
            Assert.IsTrue(rules.IsTypeAllowed(types[2].Id));
            Assert.AreEqual(GuildRank.E, rules.MaxRank);
            Assert.AreEqual(80, rules.MinReward);
            Assert.AreEqual(0.35f, simulation.World.Treasury.Commission, 1e-4f);
            Assert.AreEqual(35, registrar.CommissionPercent, "комиссия видна на доске сразу");

            registrar.SetTypeAllowed(simulation, types[0].Id, true);
            registrar.SetTypeAllowed(simulation, types[1].Id, true);
            ApplyOnPause();
            Assert.IsTrue(simulation.World.Orders.Rules.AllTypes, "все типы снова — «все»");
        }

        // ---------- Постройки ----------

        [Test]
        public void Buildings_Order_Reorder_Cancel()
        {
            Build();
            root.Navigator.Go(Destination.ToGuild(GuildTab.Buildings));
            var guild = (GuildScreen)root.Navigator.Current;
            root.RefreshNow();
            List<BuildingRow> orderable = guild.BuildingRows.Where(r => r.CanOrder).ToList();
            Assume.That(orderable.Count >= 3, "в данных есть три не построенные постройки");

            foreach (BuildingRow row in orderable.Take(3))
            {
                guild.SelectedBuilding = row.RowId;
                root.RefreshNow();
                guild.OrderSelectedBuilding();
                ApplyOnPause();
            }
            BuildingBook book = simulation.World.Buildings;
            Assert.IsNotNull(book.Current, "первая стройка началась");
            Assert.AreEqual(2, book.Queue.Count, "две — в очереди");
            Assert.IsFalse(guild.BuildingRows.Any(r => r.DefinitionId == orderable[2].DefinitionId && r.CanOrder), "заказанную второй раз не заказать");

            int first = book.Queue[0].Id;
            int second = book.Queue[1].Id;
            guild.MoveQueued(1, 0);
            ApplyOnPause();
            Assert.AreEqual(second, book.Queue[0].Id, "перестановка очереди");
            Assert.AreEqual(first, book.Queue[1].Id);

            BuildingActions.Move(simulation, second, 1);
            ApplyOnPause();
            Assert.AreEqual(first, book.Queue[0].Id, "▼ сдвигает вниз");

            BuildingActions.Cancel(simulation, first);
            ApplyOnPause();
            Assert.AreEqual(1, book.Queue.Count, "снята из очереди");
            Assert.IsTrue(guild.BuildingRows.Any(r => r.Id == 0 && r.CanOrder), "снятую снова можно заказать");
            LogAssert.NoUnexpectedReceived();
        }

        // ---------- Персонал ----------

        [Test]
        public void Staff_Offer_Hires_Reject_OnlyInUi_Dismiss_NeedsConfirm()
        {
            Build();
            StaffRoleDefinition role = data.Registry.All<StaffRoleDefinition>().First(r => r.RequiredBuilding == null || r.RequiredBuilding.BuiltAtStart);
            SimulationRun.Do(simulation, ctx =>
            {
                if (simulation.World.Staff.TryGetByRole(role.Id, out StaffMember member)) StaffService.Dismiss(ctx, member.Id);
                StaffService.AddCandidates(ctx, role);
            });
            Assume.That(simulation.World.Staff.Candidates.Count >= 2, "пришли хотя бы двое кандидатов");
            StaffCandidate hired = simulation.World.Staff.Candidates[0];
            StaffCandidate rejected = simulation.World.Staff.Candidates[1];

            // Отказ — только в интерфейсе: кандидат в мире остаётся, но не приходит уведомлением и окном.
            root.Context.Notifications.RejectStaffCandidate(rejected.Id);
            root.RefreshNow();
            root.Context.Notifications.Refresh(simulation);
            Assert.IsFalse(root.Context.Notifications.Items.Any(n => n.Popup.Kind == PopupKind.StaffCandidate && n.Popup.Id == rejected.Id));
            Assert.IsTrue(simulation.World.Staff.TryGetCandidate(rejected.Id, out _), "мир не менялся");

            StaffModel.Offer(simulation, hired.Id, hired.AskedSalary);
            ApplyOnPause();
            Assert.IsTrue(simulation.World.Staff.TryGetByRole(role.Id, out StaffMember member), "предложили просимую — нанят");
            Assert.AreEqual(hired.AskedSalary, member.Salary);

            root.Navigator.Go(Destination.ToGuild(GuildTab.Staff, member.Id));
            var guild = (GuildScreen)root.Navigator.Current;
            guild.OnDismiss();
            ApplyOnPause();
            Assert.IsTrue(simulation.World.Staff.TryGetMember(member.Id, out _), "первое нажатие только спрашивает");
            guild.OnDismiss();
            ApplyOnPause();
            Assert.IsFalse(simulation.World.Staff.TryGetMember(member.Id, out _), "второе — увольняет");
            LogAssert.NoUnexpectedReceived();
        }

        // ---------- Окна и уведомления ----------

        [Test]
        public void Popups_OpenOneAtATime_AndNotificationsLeadToDecisionWindows()
        {
            Build(31);
            for (int i = 0; i < 24 * 120 && simulation.World.Adventurers.Candidates.Count == 0; i++) simulation.Tick();
            Assume.That(simulation.World.Adventurers.Candidates.Count > 0, "пришёл кандидат");
            root.RefreshNow();

            Assert.IsTrue(root.ShowPendingPopup(), "новый кандидат — окно само");
            Assert.IsTrue(root.Navigator.Windows.Last() is CandidateWindow);
            Assert.IsFalse(root.ShowPendingPopup(), "пока открыто окно, следующее ждёт");
            while (root.Navigator.CloseTop()) { }

            root.Context.Notifications.Refresh(simulation);
            foreach (Notification item in root.Context.Notifications.Items.ToList())
            {
                while (root.Navigator.CloseTop()) { }
                root.Context.OpenPopup(item.Popup);
                WindowView top = root.Navigator.Windows.LastOrDefault();
                switch (item.Popup.Kind)
                {
                    case PopupKind.AdventurerCandidate: Assert.IsInstanceOf<CandidateWindow>(top); break;
                    case PopupKind.StaffCandidate: Assert.IsInstanceOf<StaffCandidateWindow>(top); break;
                    case PopupKind.Report: Assert.IsInstanceOf<ReportWindow>(top); break;
                    default: Assert.IsTrue(item.Destination.IsValid, "без окна — ведёт на экран"); break;
                }
            }
            while (root.Navigator.CloseTop()) { }

            // Ссылка на кандидата — тоже окно решения, а не карточка.
            Candidate candidate = simulation.World.Adventurers.Candidates[0];
            root.Navigator.Go(Destination.Card(candidate.Adventurer.Id));
            var window = (CandidateWindow)root.Navigator.Windows.Last();
            Assert.IsTrue(window.IsWaiting);
            PeopleActions.AnswerCandidate(simulation, candidate.Adventurer.Id, true);
            ApplyOnPause();
            Assert.IsTrue(simulation.World.Adventurers.IsActive(candidate.Adventurer.Id), "принят — в гильдии");
            Assert.IsFalse(window.IsWaiting, "окно видит, что кандидат уже не ждёт");
            LogAssert.NoUnexpectedReceived();
        }

        // ---------- Казна ----------

        [Test]
        public void Treasury_LedgerFilter_AndReportForEveryPastMonth()
        {
            data.NoQuests();
            Build(92);
            IReadOnlyList<MonthReport> reports = simulation.World.Reports.Reports;
            Assert.GreaterOrEqual(reports.Count, 3);

            root.Navigator.Go(Destination.ToScreen(ScreenId.Treasury));
            var screen = (TreasuryScreen)root.Navigator.Current;
            TreasuryModel model = screen.Model;
            model.Limit = int.MaxValue;

            for (int i = 0; i < reports.Count; i++)
            {
                screen.SelectMonth(i);
                root.RefreshNow();
                Assert.AreEqual(i, screen.ShownReport, "отчёт выбранного месяца");
                Assert.AreEqual(reports[i].LedgerTo - reports[i].LedgerFrom, model.Matched, "журнал — записи этого месяца");
                StringAssert.DoesNotContain("report.", ReportWindow.Render(reports[i], simulation, root.Context.Theme), "все строки отчёта — из шаблонов");
            }

            screen.SelectMonth(0);
            Assume.That(reports[0].LedgerTo > reports[0].LedgerFrom, "в первом месяце были операции");
            LedgerCategory category = simulation.World.Treasury.Ledger[reports[0].LedgerFrom].Category;
            screen.ToggleCategory(category);
            root.RefreshNow();
            string name = TreasuryModel.CategoryName(data.Registry, category);
            Assert.Greater(model.Rows.Count, 0);
            Assert.IsTrue(model.Rows.All(r => r.Category == name), "фильтр оставляет только выбранную статью");
            int expected = 0;
            for (int i = reports[0].LedgerFrom; i < reports[0].LedgerTo; i++)
            {
                if (simulation.World.Treasury.Ledger[i].Category == category) expected++;
            }
            Assert.AreEqual(expected, model.Matched);

            screen.ToggleCategory(category);
            root.RefreshNow();
            Assert.AreEqual(reports[0].LedgerTo - reports[0].LedgerFrom, model.Matched, "фильтр снят — снова все");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void TopBar_TreasuryClick_LeadsToTreasury()
        {
            Build();
            Button money = go.GetComponentsInChildren<Button>(true).First(b => b.name == "Treasury");
            money.onClick.Invoke();
            Assert.AreEqual(ScreenId.Treasury, root.Navigator.Current.Id);
        }

        // ---------- Поражение ----------

        [Test]
        public void Defeat_ShowsSummary_AndRestartsWithNewSeed()
        {
            data.NoQuests();
            data.Set("economy.bankruptcyStartDays", 1);
            data.Set("economy.bankruptcyMonths", 1);
            FakeSession session = Build();
            simulation.Send(new DebugMoneyCommand(-1_000_000));
            for (int i = 0; i < 24 * 90 && !simulation.IsFinished; i++) simulation.Tick();
            Assert.IsTrue(simulation.IsFinished, "казна в минусе — гильдия закрылась");
            root.RefreshNow();

            Assert.IsTrue(root.ShowPendingPopup(), "окно поражения открывается само");
            var defeat = root.Navigator.Windows.Last() as DefeatWindow;
            Assert.IsNotNull(defeat);
            Assert.IsNotEmpty(defeat.Model.Days);
            StringAssert.Contains(UiFormat.Money(simulation.World.Treasury.Money), defeat.Model.Money);
            Assert.Greater(defeat.Model.Best.Count, 0, "лучшие люди");
            root.Context.Notifications.Refresh(simulation);
            Assert.AreEqual(PopupKind.Defeat, root.Context.Notifications.Items[0].Popup.Kind, "уведомление возвращает окно итогов");

            uint oldSeed = session.Seed;
            Button restart = defeat.Root.GetComponentsInChildren<Button>(true).First(b => b.GetComponentInChildren<TMPro.TextMeshProUGUI>().text == UiStrings.StartOver);
            restart.onClick.Invoke();

            Assert.AreEqual(1, session.Restarts);
            Assert.AreNotEqual(oldSeed, session.Seed, "новое зерно");
            Assert.AreEqual(session.Seed, simulation.MasterSeed);
            Assert.IsFalse(simulation.IsFinished, "новая игра идёт");
            Assert.AreSame(simulation, root.Client, "интерфейс привязан к новой симуляции");
            Assert.IsFalse(root.Navigator.Windows.Any(w => w is DefeatWindow), "итогов в новой игре нет");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void DecisionActions_SameSeedSameActions_SameWorld()
        {
            string Run()
            {
                Simulation s = Simulation.CreateDefault(data.Registry, Seed);
                return SimulationLog.Record(s, sim =>
                {
                    var registrar = new RegistrarModel();
                    registrar.SetMaxRank(sim, GuildRank.E);
                    RegistrarModel.SetCommissionPercent(sim, 30);
                    BuildingActions.Order(sim, data.Registry.All<BuildingDefinition>().First(b => !b.BuiltAtStart).Id);
                    SimulationRun.Days(sim, 20);
                });
            }

            Assert.AreEqual(Run(), Run());
        }

        // ---------- Распоряжения ----------

        [Test]
        public void Decrees_Screen_ToggleScopeAndTerm_VisibleAtOnceOnPause()
        {
            Build();
            var screen = Show<DecreesScreen>(ScreenId.Decrees);
            Assert.AreEqual(4, screen.Model.Cards.Count, "четыре распоряжения");
            DecreeCard groupOnly = screen.Model.Find("GroupOnlyFromRank");
            Assert.IsFalse(groupOnly.IsOn);
            CollectionAssert.AreEqual(new[] { GuildRank.C }, groupOnly.Ranks, "область по умолчанию — C");

            screen.Model.ToggleRank(simulation, "GroupOnlyFromRank", GuildRank.D);
            screen.Model.SetDuration(simulation, "GroupOnlyFromRank", DecreeDuration.Week);
            Assert.IsFalse(simulation.World.Decrees.IsActive("GroupOnlyFromRank"), "у выключенного выбор команд не шлёт");
            screen.Model.Toggle(simulation, "GroupOnlyFromRank");
            ApplyOnPause();

            Assert.IsTrue(simulation.World.Decrees.TryGetActive("GroupOnlyFromRank", out ActiveDecree active), "включено командой");
            CollectionAssert.AreEqual(new[] { GuildRank.D, GuildRank.C }, active.Ranks);
            Assert.AreEqual(DecreeDuration.Week, active.Duration);
            groupOnly = screen.Model.Find("GroupOnlyFromRank");
            Assert.IsTrue(groupOnly.IsOn);
            StringAssert.StartsWith("осталось 7 дн.", groupOnly.Remaining);

            screen.Model.ToggleRank(simulation, "GroupOnlyFromRank", GuildRank.E);
            ApplyOnPause();
            Assert.AreEqual(3, active.Ranks.Count, "у включённого ранг — сразу командой");
            screen.Model.ToggleRank(simulation, "GroupOnlyFromRank", GuildRank.D);
            screen.Model.ToggleRank(simulation, "GroupOnlyFromRank", GuildRank.E);
            screen.Model.ToggleRank(simulation, "GroupOnlyFromRank", GuildRank.C);
            ApplyOnPause();
            CollectionAssert.AreEqual(new[] { GuildRank.C }, active.Ranks, "последний ранг не убрать");

            var calendar = new CalendarModel();
            calendar.Refresh(simulation);
            CalendarItem end = calendar.Items.Single(i => i.Text.Contains("распоряжения"));
            Assert.AreEqual(active.EndsAtHours, end.AtHours, "в календаре — окончание срока");
            Assert.AreEqual(ScreenId.Decrees, end.Destination.Screen);

            screen.Model.Toggle(simulation, "GroupOnlyFromRank");
            ApplyOnPause();
            Assert.IsFalse(simulation.World.Decrees.IsActive("GroupOnlyFromRank"), "выключено командой");
            Assert.IsFalse(screen.Model.Find("GroupOnlyFromRank").IsOn);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Decrees_BenefitCancelledFromScreen_LowersContentment()
        {
            Build();
            var screen = Show<DecreesScreen>(ScreenId.Decrees);
            screen.Model.Toggle(simulation, "InjuryCompensation");
            ApplyOnPause();
            Assert.AreEqual("бессрочно", screen.Model.Find("InjuryCompensation").Remaining);
            Dictionary<int, float> before = simulation.World.Adventurers.Active.ToDictionary(a => a.Id, a => a.State.Contentment);

            screen.Model.Toggle(simulation, "InjuryCompensation");
            ApplyOnPause();
            foreach (Adventurer adventurer in simulation.World.Adventurers.Active)
                Assert.AreEqual(System.Math.Max(0f, before[adventurer.Id] - 5f), adventurer.State.Contentment, 1e-4f, "отмена льготы — довольство −5");
        }
    }
}
