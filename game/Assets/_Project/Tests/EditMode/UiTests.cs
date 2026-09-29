using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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
    /// Интерфейс наблюдения: модели экранов показывают видимое и не показывают скрытое, ссылки ведут куда нужно, данные
    /// обновляются по ходу игры, экраны и окна собираются без ошибок.
    /// </summary>
    public sealed class UiTests
    {
        private const uint Seed = 7;

        private PeopleData data;

        [SetUp]
        public void SetUp() => data = new PeopleData();

        [TearDown]
        public void TearDown() => data.Dispose();

        private Simulation NewSimulation(uint seed = Seed) => Simulation.CreateDefault(data.Registry, seed);

        /// <summary>Прогон, пока не будет идущего задания (не дольше дней).</summary>
        private static QuestRun RunUntilQuest(Simulation simulation, int maxDays = 6, QuestPhase? phase = null)
        {
            long limit = simulation.Calendar.DaysToHours(maxDays);
            for (long i = 0; i < limit; i++)
            {
                simulation.Tick();
                foreach (QuestRun run in simulation.World.Quests.Active)
                {
                    if (phase == null || run.Phase == phase) return run;
                }
            }
            return null;
        }

        // ---------- Ссылки в текстах ----------

        [Test]
        public void TextRenderer_ValueWithLink_RecordsSpanOverSubstitutedText()
        {
            NounForms forms = data.Registry.NameForms("Ян");
            var source = new UiTextSource().Set("имя", TextValue.Person("Ян", forms, Gender.Male).WithLink(new TextLink(TextLinkKind.Adventurer, 5)));
            var spans = new List<TextSpan>();

            string text = TextRenderer.Render("Письмо для {имя:р}. {имя} рад.", source, null, spans);

            Assert.AreEqual(2, spans.Count, "обе подстановки — со ссылкой");
            Assert.AreEqual(forms != null ? forms.Get(GrammaticalCase.Genitive) : "Ян", text.Substring(spans[0].Start, spans[0].Length));
            Assert.AreEqual("Ян", text.Substring(spans[1].Start, spans[1].Length));
            Assert.AreEqual(new TextLink(TextLinkKind.Adventurer, 5).ToString(), spans[0].Link.ToString());
        }

        [Test]
        public void GuildFeed_RealRun_NamesAreLinkedAtTheirPlaces()
        {
            Simulation simulation = NewSimulation();
            SimulationRun.Days(simulation, 4);

            int people = 0;
            foreach (FeedEntry entry in simulation.World.Feed.Guild)
            {
                foreach (TextSpan span in entry.Spans)
                {
                    Assert.IsTrue(span.Start >= 0 && span.Start + span.Length <= entry.Text.Length, $"место ссылки внутри строки: {entry.Text}");
                    if (span.Link.Kind != TextLinkKind.Adventurer) continue;
                    Adventurer person = EventTextSource.FindPerson(simulation.World, span.Link.Id);
                    Assert.IsNotNull(person, $"ссылка на известного человека: {entry.Text}");
                    string shown = entry.Text.Substring(span.Start, span.Length);
                    StringAssert.StartsWith(person.Name.Substring(0, 2).ToLowerInvariant(), shown.ToLowerInvariant(), $"на месте ссылки — имя: {entry.Text}");
                    people++;
                }
            }
            Assert.Greater(people, 0, "за четыре дня в ленте есть имена со ссылками");
        }

        [Test]
        public void LinkCodec_RoundTrip()
        {
            var link = new TextLink(TextLinkKind.Order, 42);
            Assert.IsTrue(LinkCodec.TryDecode(LinkCodec.Encode(link), out TextLink back));
            Assert.AreEqual(link.Kind, back.Kind);
            Assert.AreEqual(link.Id, back.Id);
            Assert.IsFalse(LinkCodec.TryDecode("garbage", out _));
        }

        [Test]
        public void FeedFormatter_WrapsSpansInTmpLinks_AndEscapesText()
        {
            var spans = new[] { new TextSpan(0, 2, new TextLink(TextLinkKind.Adventurer, 3)) };
            string text = FeedFormatter.WithLinks("Ян <ушёл>", spans, "#FFFFFF");
            StringAssert.StartsWith("<link=\"Adventurer:3\"><color=#FFFFFF>Ян</color></link>", text);
            StringAssert.Contains("‹ушёл›", text, "угловые скобки текста не становятся разметкой");
        }

        [Test]
        public void LinkRouter_LeadsToCardsScreensAndSelections()
        {
            Simulation simulation = NewSimulation();
            QuestRun run = RunUntilQuest(simulation);
            Assert.IsNotNull(run, "за несколько дней кто-то вышел на задание");
            WorldState world = simulation.World;

            Adventurer person = world.Adventurers.Active[0];
            Destination card = LinkRouter.Resolve(new TextLink(TextLinkKind.Adventurer, person.Id), simulation);
            Assert.IsTrue(card.IsCard);
            Assert.AreEqual(person.Id, card.SelectId);

            Assert.IsFalse(LinkRouter.Resolve(new TextLink(TextLinkKind.Adventurer, 99999), simulation).IsValid, "нет человека — нет перехода");

            Destination quest = LinkRouter.Resolve(new TextLink(TextLinkKind.Quest, run.Id), simulation);
            Assert.AreEqual(ScreenId.Quests, quest.Screen);
            Assert.AreEqual(run.Id, quest.SelectId);

            Destination inWork = LinkRouter.Resolve(new TextLink(TextLinkKind.Order, run.OrderId), simulation);
            Assert.AreEqual(ScreenId.Quests, inWork.Screen, "взятый заказ ведёт к его заданию");
            Assert.AreEqual(run.Id, inWork.SelectId);

            Order open = world.Orders.Open.FirstOrDefault();
            if (open != null)
            {
                Destination board = LinkRouter.Resolve(new TextLink(TextLinkKind.Order, open.Id), simulation);
                Assert.AreEqual(ScreenId.Board, board.Screen);
                Assert.AreEqual(open.Id, board.SelectId);
            }

            Building building = world.Buildings.All[0];
            Destination buildings = LinkRouter.Resolve(new TextLink(TextLinkKind.Building, building.Id), simulation);
            Assert.AreEqual(GuildTab.Buildings, buildings.Tab);
            Assert.AreEqual(building.Id, buildings.SelectId);

            StaffMember staff = world.Staff.Members[0];
            Destination staffTab = LinkRouter.Resolve(new TextLink(TextLinkKind.Staff, staff.Id), simulation);
            Assert.AreEqual(GuildTab.Staff, staffTab.Tab);
            Assert.AreEqual(staff.Id, staffTab.SelectId);
        }

        // ---------- Скрытое не показывается ----------

        [Test]
        public void Card_HiddenAxesAndTraits_NotShown_UnlessRevealAll()
        {
            Simulation simulation = NewSimulation();
            Adventurer person = simulation.World.Adventurers.Active[0];
            AxisId hiddenAxis = Enumerable.Range(0, Vocabulary.AxisCount).Select(i => (AxisId)i).First(a => !person.IsAxisRevealed(a));

            SpecialTraitDefinition hiddenTrait = TraitRules.BirthTraits(data.Registry)
                .First(t => !t.RequiresPartner && !person.HasTrait(t.Id) && TraitRules.CanAdd(person, t, data.Registry));
            TraitService.AddAtGeneration(person, hiddenTrait, 0, 0);
            Assert.IsFalse(person.Traits.First(t => t.TraitId == hiddenTrait.Id).Revealed);

            CardModel card = CardModel.Build(simulation, person.Id);
            string axisName = data.Registry.Axis(hiddenAxis).DisplayName;
            Assert.AreEqual(UiStrings.Unknown, card.Axes.First(a => a.Axis == axisName).Value, "нераскрытая ось — «?»");
            Assert.IsFalse(card.Traits.Any(t => t.StartsWith(hiddenTrait.DisplayName) || t.StartsWith(hiddenTrait.DisplayNameFemale)),
                "скрытая черта не показывается");
            Assert.AreEqual(person.Traits.Count(t => t.Revealed), card.Traits.Count, "показаны только раскрытые черты, число скрытых — нигде");
            Assert.IsFalse(Regex.IsMatch(card.Loyalty, @"\d"), "лояльность — только словами");

            CardModel revealed = CardModel.Build(simulation, person.Id, revealAll: true);
            Assert.AreNotEqual(UiStrings.Unknown, revealed.Axes.First(a => a.Axis == axisName).Value, "«Раскрыть всё» показывает ось");
            Assert.IsTrue(revealed.Traits.Any(t => t.Contains(UiStrings.HiddenMark)), "«Раскрыть всё» показывает скрытую черту с пометкой");
            Assert.IsTrue(Regex.IsMatch(revealed.Loyalty, @"\d"), "«Раскрыть всё» показывает число лояльности");
        }

        [Test]
        public void Board_NeverShowsProfileOrChance_UnlessRevealAll()
        {
            Simulation simulation = NewSimulation();
            Order order = simulation.World.Orders.Open[0];
            var board = new BoardModel { SelectedId = order.Id };

            board.Refresh(simulation);
            Assert.AreEqual(order.Id, board.Selected?.Id ?? 0, "заказ выбран");
            Assert.IsEmpty(board.DetailHidden, "профиль и шанс не показываются");
            StringAssert.DoesNotContain(UiStrings.HiddenProfile, board.DetailTitle + board.DetailFacts + board.DetailDescription);
            Assert.IsNotEmpty(board.DetailDescription, "описание с намёками есть");

            board.RevealAll = true;
            board.Refresh(simulation);
            StringAssert.StartsWith(UiStrings.HiddenProfile, board.DetailHidden, "в отладке профиль виден");
        }

        [Test]
        public void Quests_NeverShowChance_UnlessRevealAll()
        {
            Simulation simulation = NewSimulation();
            QuestRun run = RunUntilQuest(simulation);
            Assert.IsNotNull(run);

            var quests = new QuestsModel { SelectedId = run.Id };
            quests.Refresh(simulation);
            Assert.IsEmpty(quests.DetailHidden);

            quests.RevealAll = true;
            quests.Refresh(simulation);
            StringAssert.Contains(UiStrings.HiddenProfile, quests.DetailHidden);
        }

        [Test]
        public void People_LoyaltySort_ByWordsNotHiddenNumber()
        {
            Simulation simulation = NewSimulation();
            var people = new PeopleModel();
            people.SortBy(PeopleColumn.Loyalty);
            people.Refresh(simulation);

            for (int i = 1; i < people.Rows.Count; i++)
                Assert.LessOrEqual(people.Rows[i - 1].LoyaltyLevel, people.Rows[i].LoyaltyLevel, "по возрастанию слова лояльности");
            for (int i = 1; i < people.Rows.Count; i++)
            {
                if (people.Rows[i - 1].LoyaltyLevel == people.Rows[i].LoyaltyLevel)
                    Assert.Less(people.Rows[i - 1].Id, people.Rows[i].Id, "при равном слове — по id, а не по скрытому числу");
            }
        }

        // ---------- Данные обновляются ----------

        [Test]
        public void TopBar_AndPeople_UpdateAsTimeGoes()
        {
            Simulation simulation = NewSimulation();
            var clock = new GameClock(data.Registry.Balance.Time, false, startPaused: true);
            TopBarModel before = TopBarModel.Build(simulation, clock);
            Assert.IsTrue(before.Paused);
            StringAssert.Contains($"{simulation.World.Adventurers.Active.Count} / {data.Registry.Balance.Guild.MaxAdventurers}", before.Headcount);

            SimulationRun.Days(simulation, 1);
            TopBarModel after = TopBarModel.Build(simulation, clock);
            Assert.AreNotEqual(before.Date, after.Date);

            var people = new PeopleModel();
            people.Refresh(simulation);
            Assert.AreEqual(simulation.World.Adventurers.Active.Count, people.Rows.Count);
            Assert.IsTrue(people.Rows.All(r => !r.Activity.Contains("{") && !r.Activity.StartsWith("ui.")), "занятие — готовым текстом");

            people.Filter = PeopleFilter.OnQuest;
            people.Refresh(simulation);
            Assert.IsTrue(people.Rows.All(r => r.Group == PeopleFilter.OnQuest));
        }

        [Test]
        public void Quests_ShowPhaseProgressAndFailures()
        {
            Simulation simulation = NewSimulation();
            QuestRun run = RunUntilQuest(simulation);
            Assert.IsNotNull(run);
            Assert.Greater(run.PhaseHours, 0, "у идущей фазы есть длина");

            var quests = new QuestsModel { SelectedId = run.Id };
            quests.Refresh(simulation);
            Assert.IsTrue(quests.Active.Any(r => r.Id == run.Id));
            Assert.IsNotEmpty(quests.DetailPhase);
            Assert.That(quests.DetailProgress, Is.InRange(0f, 1f));
            Assert.That(quests.DetailFailedRounds, Is.InRange(0, QuestsModel.FailureSteps));
            Assert.AreEqual(run.Departed.Count, quests.Members.Count);
        }

        [Test]
        public void ReturnTime_KnownOnlyOnTheWayBack()
        {
            Simulation simulation = NewSimulation();
            QuestRun back = RunUntilQuest(simulation, 12, QuestPhase.TravelBack);
            Assert.IsNotNull(back, "группа на обратном пути");
            foreach (QuestRun run in simulation.World.Quests.Active)
            {
                bool known = ObserverQueries.TryGetReturnAtHours(run, simulation.World, simulation.Rhythm, out long at);
                Assert.AreEqual(run.Phase == QuestPhase.TravelBack, known);
                if (known) Assert.GreaterOrEqual(at, simulation.World.Time.TotalHours + run.PhaseHoursLeft);
            }
        }

        [Test]
        public void Calendar_ListsConstructionAndSortsByTime()
        {
            Simulation simulation = NewSimulation();
            BuildingDefinition target = data.Registry.All<BuildingDefinition>().First(b => !b.BuiltAtStart);
            simulation.Send(new StartBuildingCommand(target.Id));
            simulation.Send(new DebugMoneyCommand(target.Cost * 2));
            simulation.Tick();
            simulation.Tick();

            var calendar = new CalendarModel();
            calendar.Refresh(simulation);
            CalendarItem item = calendar.Items.FirstOrDefault(i => i.Destination.Tab == GuildTab.Buildings && !i.Destination.IsCard && i.Destination.Screen == ScreenId.Guild);
            Assert.IsNotNull(item, "конец стройки — в календаре");
            StringAssert.Contains(target.DisplayName, item.Text);
            for (int i = 1; i < calendar.Items.Count; i++) Assert.LessOrEqual(calendar.Items[i - 1].AtHours, calendar.Items[i].AtHours);
            StringAssert.Contains("До начала месяца", calendar.UntilMonth);
        }

        [Test]
        public void Notifications_CandidatesAndUnreadReports()
        {
            Simulation simulation = NewSimulation();
            SimulationRun.Days(simulation, 31);
            var model = new NotificationsModel();
            model.Refresh(simulation);

            foreach (Candidate candidate in simulation.World.Adventurers.Candidates)
            {
                Assert.IsTrue(model.Items.Any(n => n.Destination.IsCard && n.Destination.SelectId == candidate.Adventurer.Id),
                    "кандидат — уведомление, ведущее к его карточке");
            }
            Assert.AreEqual(1, simulation.World.Reports.Reports.Count, "за месяц — один отчёт");
            Assert.IsTrue(model.Items.Any(n => n.ReportIndex == 0), "непрочитанный отчёт — уведомление");

            model.ReportsSeen = 1;
            model.Refresh(simulation);
            Assert.IsFalse(model.Items.Any(n => n.ReportIndex >= 0), "прочитанный отчёт уведомлением не висит");
        }

        [Test]
        public void LeftAdventurer_KeepsReasonText_ForCard()
        {
            data.NoQuests();
            data.Set("state.leaveChance", 1f);
            Simulation simulation = NewSimulation();
            Adventurer person = simulation.World.Adventurers.Active[0];
            long beforeMonth = simulation.Calendar.ToTotalHours(1, 2, 1, 0) - 1;
            while (simulation.World.Time.TotalHours < beforeMonth) simulation.Tick();
            simulation.Send(new DebugSetStateCommand(person.Id, StateStat.Loyalty, 0f));
            simulation.Tick();
            simulation.Tick();

            Assert.IsFalse(simulation.World.Adventurers.IsActive(person.Id), "при лояльности 0 и шансе 1 человек ушёл");
            Assert.IsNotEmpty(person.LeaveReasonText);
            CardModel card = CardModel.Build(simulation, person.Id);
            Assert.IsTrue(card.Found && !card.IsActive);
            StringAssert.Contains(person.LeaveReasonText, card.Now, "в карточке — причина ухода");
        }

        // ---------- Отладочные команды ----------

        [Test]
        public void DebugCommands_ChangeWorld()
        {
            Simulation simulation = NewSimulation();
            WorldState world = simulation.World;
            int money = world.Treasury.Money;
            simulation.Send(new DebugMoneyCommand(500));
            simulation.Send(new DebugMoneyCommand(-200));
            simulation.Send(new DebugSetReputationCommand(55f));
            simulation.ApplyCommandsNow();
            Assert.AreEqual(money + 300, world.Treasury.Money);
            Assert.AreEqual(55f, world.Guild.Reputation, 0.001f);
            Assert.IsTrue(world.Treasury.Ledger.Any(e => e.Category == LedgerCategories.DebugIncome && e.Amount == 500));

            ArchetypeDefinition role = data.Registry.All<ArchetypeDefinition>().First(a => a.Kind == ArchetypeKind.Role);
            int count = world.Adventurers.Active.Count;
            simulation.Send(new DebugSpawnAdventurerCommand(role.Id));
            simulation.ApplyCommandsNow();
            Assert.AreEqual(count + 1, world.Adventurers.Active.Count);
            Adventurer spawned = world.Adventurers.Active[world.Adventurers.Active.Count - 1];

            simulation.Send(new DebugSetStatCommand(spawned.Id, StatId.Strength, 77f));
            simulation.Send(new DebugSetAxisCommand(spawned.Id, AxisId.Loyalty, -60f));
            simulation.Send(new DebugSetStateCommand(spawned.Id, StateStat.Stress, 90f));
            simulation.ApplyCommandsNow();
            Assert.AreEqual(77f, spawned.GetStat(StatId.Strength));
            Assert.AreEqual(-60f, spawned.GetAxis(AxisId.Loyalty));
            Assert.AreEqual(90f, spawned.State.Stress);

            QuestTypeDefinition type = data.Registry.All<QuestTypeDefinition>().First(t => t.GenerationWeight > 0);
            simulation.Send(new DebugSpawnOrderCommand(type.Id, GuildRank.D));
            simulation.ApplyCommandsNow();
            Order order = world.Orders.Open[world.Orders.Open.Count - 1];
            Assert.AreEqual(type.Id, order.TypeId);
            Assert.AreEqual(GuildRank.D, order.Rank);
            Assert.AreEqual(OrderStatus.OnBoard, order.Status);
        }

        [Test]
        public void DebugAmbush_HappensNextTravelHour()
        {
            Simulation simulation = NewSimulation();
            QuestRun run = RunUntilQuest(simulation, 6, QuestPhase.TravelOut);
            Assert.IsNotNull(run);
            simulation.Send(new DebugEventCommand(DebugEventKind.Ambush, run.Id));

            List<SimEvent> events = SimulationRun.Collect(simulation, s =>
            {
                for (int i = 0; i < 16; i++) s.Tick();
            });
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.TravelEvent && e.TryGet("quest", out int q) && q == run.Id),
                "засада случилась в ближайший ходовой час");
        }

        [Test]
        public void DebugCommands_SameSeedSameCommands_SameWorld()
        {
            string Run()
            {
                Simulation simulation = NewSimulation();
                return SimulationLog.Record(simulation, s =>
                {
                    s.Send(new DebugSpawnAdventurerCommand(data.Registry.All<ArchetypeDefinition>().First().Id));
                    s.Send(new DebugSpawnOrderCommand(data.Registry.All<QuestTypeDefinition>().First().Id, GuildRank.F));
                    SimulationRun.Days(s, 3);
                });
            }

            Assert.AreEqual(Run(), Run());
        }

        // ---------- Сборка интерфейса ----------

        [Test]
        public void UiRoot_BuildsScreensAndWindows_AndDebugPanelOnlyInDebugBuild()
        {
            Simulation simulation = NewSimulation();
            SimulationRun.Days(simulation, 2);
            var clock = new GameClock(data.Registry.Balance.Time, Debug.isDebugBuild, startPaused: true);

            var go = new GameObject("UiTest", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            try
            {
                var root = go.AddComponent<UiRoot>();
                root.Bind(simulation, clock);
                Assert.AreEqual(Debug.isDebugBuild, root.HasDebugPanel, "отладочная панель — только в отладочной сборке");

                foreach (ScreenId screen in System.Enum.GetValues(typeof(ScreenId)))
                {
                    root.Navigator.ShowScreen(screen);
                    root.RefreshNow();
                    Assert.AreEqual(screen, root.Navigator.Current.Id, "экран открывается из навигации");
                }

                int person = simulation.World.Adventurers.Active[0].Id;
                root.Navigator.Go(Destination.Card(person));
                Assert.IsTrue(root.Navigator.Windows.Any(w => w is CardWindow), "карточка открыта поверх");
                root.ToggleTime();
                root.ToggleNotifications();
                root.ToggleDebug();
                root.Context.RevealAll = true;
                root.RefreshNow();
                while (root.Navigator.CloseTop()) { }

                root.Navigator.Go(Destination.ToGuild(GuildTab.Buildings, simulation.World.Buildings.All[0].Id));
                Assert.AreEqual(ScreenId.Guild, root.Navigator.Current.Id);
                Assert.AreEqual(GuildTab.Buildings, ((GuildScreen)root.Navigator.Current).Tab);
                root.RefreshNow();
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
            LogAssert.NoUnexpectedReceived();
        }
    }
}
