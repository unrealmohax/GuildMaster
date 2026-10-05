using System.Collections.Generic;
using System.Linq;
using GuildMaster.Core;
using GuildMaster.Data;
using GuildMaster.UI;
using NUnit.Framework;

namespace GuildMaster.Tests
{
    /// <summary>
    /// Обращения: триггеры, лимит и перезарядка, ответ и вариант по сроку, деньги через казну, цепочки по флагам памяти,
    /// раскрытия, группы Влюблённых, праздник в таверне, отчёт месяца, модели интерфейса, детерминизм.
    /// </summary>
    public sealed class DilemmaTests
    {
        private static IReadOnlyList<Dilemma> Open(StateWorld world) => world.Simulation.World.Dilemmas.Open;

        private static Dilemma OpenOf(StateWorld world, DilemmaTrigger trigger) => Open(world).Single(d => d.Trigger == trigger);

        private static void Answer(StateWorld world, Dilemma dilemma, int option)
        {
            world.Simulation.Send(new AnswerDilemmaCommand(dilemma.Id, option));
            world.Simulation.ApplyCommandsNow();
        }

        private static StateWorld LoanWorld(uint seed = 7u)
        {
            var world = new StateWorld(seed, dilemmas: true);
            world.Data.Set("dilemmas.loanWeeklyChance", 0f);
            return world;
        }

        private static void AssertLedgerBalanced(StateWorld world)
        {
            Treasury treasury = world.Simulation.World.Treasury;
            Assert.AreEqual(treasury.Money, treasury.StartMoney + treasury.Ledger.Sum(e => e.Amount), "казна сходится с журналом");
        }

        // ---------- №1 Просьба в долг ----------

        [Test]
        public void Loan_FamilyWithEmptyWallet_Asks_RevealsFamily_AmountIsMonthOfLiving()
        {
            using StateWorld world = LoanWorld();
            Adventurer family = world.Add("Family");
            family.State.Wallet = 0;
            Adventurer plain = world.Add();
            plain.State.Wallet = 0;
            world.Add("Family"); // кошелёк полон — не просит

            List<SimEvent> events = world.Collect(() => world.TickToHour(12));

            Dilemma loan = OpenOf(world, DilemmaTrigger.LoanRequest);
            Assert.AreEqual(1, Open(world).Count, "просит только Семейный с пустым кошельком: у простого шанс 0");
            Assert.AreEqual(family.Id, loan.SubjectId);
            Assert.AreEqual(30 * WalletService.DailyLivingCost(family, false, world.Balance.Expenses), loan.Amount, "сумма — расходы на 30 дней");
            Assert.AreEqual("Дома ждут, а платить нечем.", loan.Reason, "у Семейного — своя причина");
            Assert.IsTrue(family.Traits[0].Revealed, "причина раскрывает Семейного");
            Assert.AreEqual(world.Time.TotalHours + 48, loan.DeadlineAtHours, "срок ответа — 2 суток");
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.DilemmaArrived && e.Participants[0] == family.Id));
            Assert.AreEqual(AutopauseKind.DilemmaReceived, AutopauseRules.Default[SimEventType.DilemmaArrived], "новое обращение ставит автопаузу");
        }

        [Test]
        public void Loan_DrunkardAsks_PlainOnlyByWeeklyChance()
        {
            using var world = new StateWorld(dilemmas: true);
            world.Data.Set("dilemmas.loanWeeklyChance", 1f);
            Adventurer plain = world.Add();
            plain.State.Wallet = 0;
            Adventurer withDebt = world.Add("Drunkard");
            withDebt.State.Wallet = 0;
            withDebt.State.DebtToGuild = 10;

            world.TickToHour(12);
            Assert.AreEqual(plain.Id, OpenOf(world, DilemmaTrigger.LoanRequest).SubjectId, "шанс в неделю 1 — просит каждый день");
            Assert.IsFalse(Open(world).Any(d => d.SubjectId == withDebt.Id), "с долгом перед гильдией в долг не просят");
        }

        [Test]
        public void Loan_FullSum_FromTreasuryAsLoan_WalletAndDebtGrow_FlagTookLoan()
        {
            using StateWorld world = LoanWorld();
            Adventurer family = world.Add("Family");
            family.State.Wallet = 0;
            world.TickToHour(12);
            Dilemma loan = OpenOf(world, DilemmaTrigger.LoanRequest);
            int money = world.Simulation.World.Treasury.Money;
            float contentment = family.State.Contentment;
            float loyalty = family.State.Loyalty;

            Answer(world, loan, 0);

            Assert.AreEqual(DilemmaStatus.Answered, loan.Status);
            Assert.AreEqual(0, loan.OptionIndex);
            Assert.AreEqual(money - loan.Amount, world.Simulation.World.Treasury.Money);
            LedgerEntry entry = world.Simulation.World.Treasury.Ledger.Last();
            Assert.AreEqual(LedgerCategories.Loans, entry.Category, "статья «Займы авантюристам»");
            Assert.AreEqual(family.Id, entry.RelatedId);
            Assert.AreEqual(loan.Amount, family.State.Wallet);
            Assert.AreEqual(loan.Amount, family.State.DebtToGuild);
            Assert.AreEqual(contentment + 10f, family.State.Contentment, 1e-4);
            Assert.AreEqual(loyalty + 5f, family.State.Loyalty, 1e-4);
            Assert.IsTrue(family.Memory.HasFlag(MemoryFlag.TookLoan));
            AssertLedgerBalanced(world);
        }

        [Test]
        public void Loan_NotEnoughMoney_OptionUnavailable_CommandIgnored_RefusalStillPossible()
        {
            using StateWorld world = LoanWorld();
            Adventurer family = world.Add("Family");
            family.State.Wallet = 0;
            world.TickToHour(12);
            Dilemma loan = OpenOf(world, DilemmaTrigger.LoanRequest);
            world.Do(ctx => TreasuryService.Debit(ctx, LedgerCategories.DebugExpense, ctx.World.Treasury.Money - 10));

            Assert.IsFalse(DilemmaRules.IsAvailable(world.Simulation.World, world.Registry, loan, 0), "в минус ради займа не уходим");
            Assert.IsTrue(DilemmaRules.IsUnaffordable(world.Simulation.World, world.Registry, loan, 0));
            Answer(world, loan, 0);
            Assert.IsTrue(loan.IsOpen, "команда на недоступный вариант ничего не делает");
            Assert.AreEqual(0, family.State.DebtToGuild);

            Answer(world, loan, 2);
            Assert.AreEqual(DilemmaStatus.Answered, loan.Status, "отказ доступен всегда");
            AssertLedgerBalanced(world);
        }

        [Test]
        public void NoAnswer_DefaultOptionAfterTwoDays()
        {
            using StateWorld world = LoanWorld();
            Adventurer family = world.Add("Family");
            family.State.Wallet = 0;
            world.TickToHour(12);
            Dilemma loan = OpenOf(world, DilemmaTrigger.LoanRequest);

            List<SimEvent> events = world.Collect(() => world.Days(2));

            Assert.AreEqual(DilemmaStatus.TimedOut, loan.Status);
            Assert.AreEqual(2, loan.OptionIndex, "без ответа — отказ");
            Assert.AreEqual(loan.DeadlineAtHours, loan.ClosedAtHours, "ровно в срок");
            SimEvent answered = events.Single(e => e.Type == SimEventType.DilemmaAnswered);
            Assert.IsTrue(answered.TryGet("timedOut", out bool timedOut) && timedOut);
            Assert.AreEqual(0, family.State.DebtToGuild, "денег не дали");
        }

        [Test]
        public void Limit_ThreeOpen_OnePerPerson_CooldownThirtyDays()
        {
            using StateWorld world = LoanWorld();
            List<Adventurer> people = world.AddMany(5, "Family");
            foreach (Adventurer person in people) person.State.Wallet = 0;

            world.TickToHour(12);
            Assert.AreEqual(3, Open(world).Count, "не больше 3 открытых");
            world.Days(1);
            Assert.AreEqual(3, Open(world).Count, "одинаковых по одному человеку — не больше одного; лимит держит остальных");

            List<int> first = Open(world).Select(d => d.SubjectId).ToList();
            foreach (Dilemma dilemma in Open(world).ToList()) Answer(world, dilemma, 2);
            world.Days(1);
            CollectionAssert.AreEquivalent(people.Select(p => p.Id).Except(first), Open(world).Select(d => d.SubjectId), "очередь остальных");

            world.Days(20);
            foreach (int id in first)
                Assert.AreEqual(1, world.Simulation.World.Dilemmas.Closed.Count(d => d.SubjectId == id), "перезарядка 30 дней на человека");
        }

        [Test]
        public void SubjectLeft_DilemmaWithdrawn_WithoutEffects()
        {
            using StateWorld world = LoanWorld();
            Adventurer family = world.Add("Family");
            family.State.Wallet = 0;
            world.TickToHour(12);
            Dilemma loan = OpenOf(world, DilemmaTrigger.LoanRequest);
            world.Do(ctx => AdventurerLifecycle.Retire(ctx, family, LeaveReason.Left));
            int money = world.Simulation.World.Treasury.Money;

            world.Simulation.Tick();
            Assert.AreEqual(DilemmaStatus.Withdrawn, loan.Status);
            Assert.AreEqual(-1, loan.OptionIndex);
            Assert.AreEqual(money, world.Simulation.World.Treasury.Money);
        }

        // ---------- №2 Беглец вернулся и цепочка «прощён» ----------

        private static (QuestWorld Quests, Adventurer Fugitive, Adventurer Left, Dilemma Dilemma) DeserterReturned()
        {
            var quests = new QuestWorld(dilemmas: true);
            quests.Data.Set("tension.fugitiveReturnChance", 1f);
            Adventurer fugitive = quests.Add();
            Adventurer left = quests.Add();
            QuestRun run = quests.Start(quests.AddOrder(), fugitive, left);
            quests.World.Do(ctx => QuestTension.Flee(ctx, run, fugitive));
            DilemmaBook book = quests.Simulation.World.Dilemmas;
            for (int i = 0; i < 5 * 24 && !book.IsAwaiting(DilemmaTrigger.DeserterReturned, fugitive.Id); i++) quests.Simulation.Tick();
            Dilemma dilemma = book.Open.Single(d => d.Trigger == DilemmaTrigger.DeserterReturned);
            return (quests, fugitive, left, dilemma);
        }

        [Test]
        public void Deserter_ReturnsAsDilemma_AbandonedKnown_NoOrdersUntilAnswer_FlagFled()
        {
            (QuestWorld quests, Adventurer fugitive, Adventurer left, Dilemma dilemma) = DeserterReturned();
            using (quests)
            {
                Assert.AreEqual(fugitive.Id, dilemma.SubjectId);
                CollectionAssert.AreEqual(new[] { left.Id }, dilemma.Abandoned, "брошенные — кто остался в группе");
                Assert.IsTrue(fugitive.IsDeserter, "отметка «беглец» остаётся");
                Assert.IsTrue(fugitive.Memory.HasFlag(MemoryFlag.Fled));
                Assert.IsTrue(DecisionBans.Default.Any(b => b.Reason.StartsWith("deserter")), "пока ждёт ответа — заказов не берёт");
            }
        }

        [Test]
        public void Deserter_Forgive_AbandonedLikeHimLess_PardonedFlag()
        {
            (QuestWorld quests, Adventurer fugitive, Adventurer left, Dilemma dilemma) = DeserterReturned();
            using (quests)
            {
                RelationBook relations = quests.Simulation.World.Relations;
                float relation = relations.GetValue(left.Id, fugitive.Id);
                Answer(quests.World, dilemma, 0);
                Assert.AreEqual(relation - 10f, relations.GetValue(left.Id, fugitive.Id), 1e-4);
                Assert.IsTrue(fugitive.Memory.HasFlag(MemoryFlag.Pardoned));
                Assert.IsTrue(quests.Simulation.World.Adventurers.IsActive(fugitive.Id));
            }
        }

        [Test]
        public void Deserter_Fine_HalfWalletToTreasury_Expel_LeavesAsExpelled()
        {
            (QuestWorld quests, Adventurer fugitive, Adventurer left, Dilemma dilemma) = DeserterReturned();
            using (quests)
            {
                fugitive.State.Wallet = 300;
                int money = quests.Simulation.World.Treasury.Money;
                Answer(quests.World, dilemma, 1);
                Assert.AreEqual(150, fugitive.State.Wallet);
                Assert.AreEqual(money + 150, quests.Simulation.World.Treasury.Money);
                Assert.AreEqual(LedgerCategories.Fines, quests.Simulation.World.Treasury.Ledger.Last().Category);
                Assert.IsTrue(fugitive.Memory.HasFlag(MemoryFlag.Punished));
            }

            (quests, fugitive, left, dilemma) = DeserterReturned();
            using (quests)
            {
                float contentment = left.State.Contentment;
                Answer(quests.World, dilemma, 2);
                Assert.AreEqual(LeaveReason.Expelled, fugitive.LeaveReason);
                Assert.AreEqual(contentment + 5f, left.State.Contentment, 1e-4, "брошенные довольны");
            }
        }

        [Test]
        public void Pardoned_FirstTension_RedemptionMakesHero_OrFlagIsSpent()
        {
            using var quests = new QuestWorld();
            quests.Data.Set("dilemmas.pardonRedemptionChance", 1f);
            quests.Data.Set("tension.heroChance", 0.4f);
            Adventurer pardoned = quests.Add();
            pardoned.SetAxis(AxisId.People, -50f); // без искупления героем не стал бы
            Adventurer comrade = quests.Add();
            QuestRun run = quests.Start(quests.AddOrder(), pardoned, comrade);
            quests.World.Do(ctx => pardoned.Memory.Set(MemoryFlag.Pardoned, ctx.World.Time.TotalHours));

            quests.World.Do(ctx => QuestTension.Moment(ctx, run));
            Assert.IsTrue(run.IsRedeeming(pardoned.Id), "искупление");
            Assert.IsFalse(pardoned.Memory.HasFlag(MemoryFlag.Pardoned), "флаг тратится в первом моменте напряжения");
            Adventurer hero = quests.World.Do(ctx => QuestTension.TryHero(ctx, run, QuestParty.Present(ctx.World, run), comrade));
            Assert.AreEqual(pardoned, hero, "шанс геройства × 3 без порогов");

            using var other = new QuestWorld();
            other.Data.Set("dilemmas.pardonRedemptionChance", 0f);
            Adventurer second = other.Add();
            QuestRun secondRun = other.Start(other.AddOrder(), second, other.Add());
            other.World.Do(ctx => second.Memory.Set(MemoryFlag.Pardoned, ctx.World.Time.TotalHours));
            other.World.Do(ctx => QuestTension.Moment(ctx, secondRun));
            Assert.IsFalse(secondRun.IsRedeeming(second.Id));
            Assert.IsFalse(second.Memory.HasFlag(MemoryFlag.Pardoned));
        }

        // ---------- №3 Ссора из-за добычи ----------

        [Test]
        public void LootDispute_CaughtSkimmerAgainstDisliked_SupportB_ReturnsSkimmed()
        {
            using var quests = new QuestWorld(dilemmas: true);
            quests.Data.Set("dilemmas.lootDisputeChance", 1f);
            quests.Data.Set("adventurers.skimCaughtChance", 1f);
            Adventurer skimmer = quests.Add();
            skimmer.SetAxis(AxisId.Principles, -80f);
            Adventurer other = quests.Add();
            quests.World.Do(ctx => RelationService.Set(ctx, skimmer.Id, other.Id, -30f));
            QuestRun run = quests.Start(quests.AddOrder(requirement: 5f, reward: 200), skimmer, other);

            quests.RunToEnd(run);
            Assert.IsTrue(run.SkimCaught, "утаивание заметили");
            Dilemma dispute = OpenOf(quests.World, DilemmaTrigger.LootDispute);
            Assert.AreEqual(skimmer.Id, dispute.SubjectId, "{A} — утаивший");
            Assert.AreEqual(other.Id, dispute.PartnerId);
            Assert.IsNotNull(dispute.Place);

            int expected = System.Math.Min(skimmer.State.Wallet, run.SkimmedAmount);
            int wallet = other.State.Wallet;
            Answer(quests.World, dispute, 1);
            Assert.AreEqual(wallet + expected, other.State.Wallet, "утаенное вернулось в делёж");
        }

        // ---------- №4 Раненый рвётся на задание ----------

        [Test]
        public void Wounded_Allow_TakesQuestsWithProfileTimesPointSix_NextWoundMaims()
        {
            using var world = new StateWorld(dilemmas: true);
            world.Data.Set("dilemmas.loanWeeklyChance", 0f);
            Adventurer wounded = world.Add();
            wounded.State.Wallet = 0;
            world.Do(ctx => HealthService.Wound(ctx, wounded, ConditionKind.HeavyWound));
            Assert.IsFalse(StateRules.CanTakeQuests(wounded.State, world.Balance.State));

            world.TickToHour(12);
            Dilemma dilemma = OpenOf(world, DilemmaTrigger.WoundedWantsQuest);
            Answer(world, dilemma, 0);

            Assert.IsTrue(wounded.State.HasWoundedQuestPermission);
            Assert.IsTrue(StateRules.CanTakeQuests(wounded.State, world.Balance.State));
            Assert.IsTrue(DecisionPoints.CanDecide(wounded.State, world.Time.TotalHours));
            Assert.IsFalse(wounded.State.InInfirmary);
            float permanent = AdventurerStats.Permanent(wounded, StatId.Strength, world.Registry);
            Assert.AreEqual(permanent * 0.6f, AdventurerStats.Effective(wounded, StatId.Strength, world.Registry), 1e-3);

            world.Do(ctx => HealthService.Wound(ctx, wounded, ConditionKind.LightWound));
            Assert.IsTrue(PersonText.IsMaimed(wounded, world.Registry), "следующая рана — сразу увечье");
            Assert.IsFalse(wounded.State.HasWoundedQuestPermission);
        }

        [Test]
        public void Wounded_PayTreatment_WeekOfLivingToWallet_FromTreasury()
        {
            using var world = new StateWorld(dilemmas: true);
            world.Data.Set("dilemmas.loanWeeklyChance", 0f);
            Adventurer wounded = world.Add("Family");
            world.Do(ctx => HealthService.Wound(ctx, wounded, ConditionKind.HeavyWound));
            world.TickToHour(12);
            Dilemma dilemma = OpenOf(world, DilemmaTrigger.WoundedWantsQuest);
            int cost = DilemmaRules.OptionCost(world.Simulation.World, world.Registry, dilemma, 2);
            Assert.AreEqual(WalletService.WeeklyExpenses(wounded, world.Balance.Expenses), cost, "Лазарета нет — только неделя");
            int wallet = wounded.State.Wallet;

            Answer(world, dilemma, 2);
            Assert.AreEqual(wallet + cost, wounded.State.Wallet);
            Assert.AreEqual(LedgerCategories.Dilemmas, world.Simulation.World.Treasury.Ledger.Last().Category);
            AssertLedgerBalanced(world);
        }

        // ---------- №10 Влюблённые ----------

        private static (Adventurer A, Adventurer B) Lovers(StateWorld world)
        {
            Adventurer a = world.Add();
            Adventurer b = world.Add();
            SpecialTraitDefinition lover = world.Registry.Get<SpecialTraitDefinition>("Lover");
            TraitService.AddAtGeneration(a, lover, 0, b.Id);
            TraitService.AddAtGeneration(b, lover, 0, a.Id);
            a.JoinedAtHours = b.JoinedAtHours = world.Time.TotalHours - 8 * 24;
            return (a, b);
        }

        [Test]
        public void Lovers_AskOnce_RevealBoth_AllowAddsPartnerScore()
        {
            using var world = new StateWorld(dilemmas: true);
            (Adventurer a, Adventurer b) = Lovers(world);
            world.TickToHour(12);
            Dilemma dilemma = OpenOf(world, DilemmaTrigger.LoversSameParty);
            Assert.IsTrue(a.Traits[0].Revealed && b.Traits[0].Revealed, "дилемма раскрывает черту у обоих");

            RelationBook relations = world.Simulation.World.Relations;
            float before = PartyMath.PartnerScore(a, b, 0f, relations, world.Registry);
            Answer(world, dilemma, 0);
            Assert.AreEqual(before + 0.5f, PartyMath.PartnerScore(a, b, 0f, relations, world.Registry), 1e-4);
            Assert.AreEqual(before + 0.5f, PartyMath.PartnerScore(b, a, 0f, relations, world.Registry), 1e-4);

            world.Days(40);
            Assert.AreEqual(1, world.Simulation.World.Dilemmas.ArrivedCount, "раз на пару");
        }

        [Test]
        public void Lovers_Forbid_Separated_LaterJoinedLeavesPermanentParty()
        {
            using var world = new StateWorld(dilemmas: true);
            (Adventurer a, Adventurer b) = Lovers(world);
            Adventurer c = world.Add();
            Party party = world.Do(ctx => PartyService.Form(ctx, new List<Adventurer> { a, b, c }));
            world.TickToHour(12);
            Dilemma dilemma = OpenOf(world, DilemmaTrigger.LoversSameParty);

            Answer(world, dilemma, 1);
            Assert.IsTrue(DilemmaRules.AreSeparated(a, b));
            Assert.AreEqual(0, b.PermanentPartyId, "позже вступивший выходит из группы");
            Assert.AreEqual(party.Id, a.PermanentPartyId);
            CollectionAssert.AreEqual(new[] { a.Id, c.Id }, party.MemberIds);
        }

        // ---------- №13 Праздник ----------

        [Test]
        public void Feast_InnkeeperOffers_WhenStressed_PaidNow_AppliedToTavernEvening()
        {
            using var world = new StateWorld(dilemmas: true);
            List<Adventurer> people = world.AddMany(3);
            foreach (Adventurer person in people) person.State.Stress = 60f;
            world.TickToHour(12);
            Dilemma feast = OpenOf(world, DilemmaTrigger.TavernFeast);
            Assert.AreNotEqual(0, feast.StaffId, "обращается Трактирщик");
            Assert.AreEqual(0, feast.SubjectId);

            int money = world.Simulation.World.Treasury.Money;
            Answer(world, feast, 0);
            Assert.AreEqual(money - 150, world.Simulation.World.Treasury.Money);
            Assert.AreEqual(feast.Id, world.Simulation.World.Dilemmas.PendingFeastId);

            world.Do(ctx =>
            {
                people[0].State.InTavernThisEvening = true;
                people[1].State.InTavernThisEvening = true;
            });
            float[] stress = people.Select(p => p.State.Stress).ToArray();
            float[] fatigue = people.Select(p => p.State.Fatigue).ToArray();
            world.Do(ctx => TavernEvening.Settle(ctx));

            Assert.AreEqual(stress[0] - 20f, people[0].State.Stress, 1e-3);
            Assert.AreEqual(fatigue[1] + 10f, people[1].State.Fatigue, 1e-3);
            Assert.AreEqual(stress[2], people[2].State.Stress, 1e-3, "кого не было в таверне, праздник не касается");
            Assert.AreEqual(0, world.Simulation.World.Dilemmas.PendingFeastId, "праздник проходит один раз");
            AssertLedgerBalanced(world);
        }

        // ---------- Отчёт, отладка, интерфейс ----------

        [Test]
        public void MonthReport_HasDilemmasSection()
        {
            using StateWorld world = LoanWorld();
            Adventurer family = world.Add("Family");
            family.State.Wallet = 0;
            world.Days(31);
            ReportSection section = world.Simulation.World.Reports.Reports[0].Sections.Single(s => s.TitleKey == MonthReportSections.DilemmasTitle);
            Assert.IsTrue(section.TryGetLine(MonthReportSections.DilemmasArrived, out ReportLine arrived) && arrived.Amount >= 1);
            Assert.IsTrue(section.TryGetLine(MonthReportSections.DilemmasTimedOut, out ReportLine timedOut) && timedOut.Amount >= 1);
        }

        [Test]
        public void DebugCommand_OpensAnyDilemma_ForSelectedPerson()
        {
            using var world = new StateWorld(dilemmas: true);
            Adventurer a = world.Add();
            Adventurer b = world.Add();
            world.Simulation.Send(new DebugDilemmaCommand(DilemmaTrigger.LootDispute, a.Id));
            world.Simulation.ApplyCommandsNow();
            Dilemma dispute = OpenOf(world, DilemmaTrigger.LootDispute);
            Assert.AreEqual(a.Id, dispute.SubjectId);
            Assert.AreEqual(b.Id, dispute.PartnerId);
            Assert.IsNotNull(dispute.Place);
            string body = DilemmaTextSource.Render(world.Registry.Get<DilemmaDefinition>(dispute.DefinitionId).BodyTemplate, dispute,
                world.Simulation.World, world.Registry);
            StringAssert.DoesNotContain("{", body, "все метки подставлены");
        }

        [Test]
        public void UiModels_CardWithVisibleConsequences_Unaffordable_Notification_Calendar_Popup()
        {
            using StateWorld world = LoanWorld();
            Adventurer family = world.Add("Family");
            family.State.Wallet = 0;
            world.TickToHour(12);
            Dilemma loan = OpenOf(world, DilemmaTrigger.LoanRequest);
            world.Do(ctx => TreasuryService.Debit(ctx, LedgerCategories.DebugExpense, ctx.World.Treasury.Money - loan.Amount / 2 - 1));

            var model = new DilemmasModel();
            model.Refresh(world.Simulation);
            Assert.AreEqual(1, model.Open.Count);
            DilemmaCard card = model.Card(world.Simulation, loan.Id, "#FFFFFF");
            Assert.AreEqual(3, card.Choices.Count);
            StringAssert.Contains(loan.Amount.ToString(), card.Choices[0].Consequences, "видна сумма варианта");
            Assert.IsTrue(card.Choices[0].Unaffordable && !card.Choices[0].Available, "вся сумма — не по карману");
            Assert.IsTrue(card.Choices[1].Available, "половина — по карману");
            StringAssert.Contains(family.Name, card.From);
            StringAssert.Contains("<link=", card.Body, "имя в тексте — ссылка");

            var notifications = new NotificationsModel();
            notifications.Refresh(world.Simulation);
            Assert.IsTrue(notifications.Items.Any(n => n.Popup.Kind == PopupKind.Dilemma && n.Popup.Id == loan.Id));
            Assert.IsTrue(new PopupQueue().TryNext(world.Simulation, notifications, out Popup popup) && popup.Kind == PopupKind.Dilemma);
            var calendar = new CalendarModel();
            calendar.Refresh(world.Simulation);
            Assert.IsTrue(calendar.Items.Any(i => i.AtHours == loan.DeadlineAtHours && i.Destination.Screen == ScreenId.Dilemmas));

            Assert.IsTrue(model.Answer(world.Simulation, loan.Id, 1));
            world.Simulation.ApplyCommandsNow();
            Assert.AreEqual(DilemmaStatus.Answered, loan.Status);
            Assert.AreEqual(1, loan.OptionIndex);
        }

        // ---------- Детерминизм ----------

        private static string Run(uint seed)
        {
            using var world = new StateWorld(seed, dilemmas: true);
            world.Add("Family").State.Wallet = 0;
            Adventurer wounded = world.Add();
            wounded.State.Wallet = 0;
            world.Do(ctx => HealthService.Wound(ctx, wounded, ConditionKind.HeavyWound));
            Lovers(world);
            foreach (Adventurer person in world.AddMany(3)) person.State.Stress = 70f;
            return SimulationLog.Record(world.Simulation, s => SimulationRun.Days(s, 40));
        }

        [Test]
        public void SameSeed_SameDilemmas_SameLog()
        {
            string first = Run(5u);
            StringAssert.Contains(nameof(SimEventType.DilemmaArrived), first, "обращения были");
            Assert.AreEqual(first, Run(5u));
        }

        [Test]
        public void NoTriggers_NoRolls_SameLogAsWithoutDilemmaSystem()
        {
            string Log(bool dilemmas)
            {
                using var world = new StateWorld(9u, dilemmas: dilemmas);
                world.AddMany(3);
                return SimulationLog.Record(world.Simulation, s => SimulationRun.Days(s, 30));
            }

            string with = Log(true);
            StringAssert.DoesNotContain(nameof(SimEventType.DilemmaArrived), with);
            Assert.AreEqual(Log(false), with, "пустой случай бросков не тратит и мир не меняет");
        }
    }
}
