using System.Collections.Generic;
using System.Linq;
using GuildMaster.Core;
using GuildMaster.Data;
using NUnit.Framework;

namespace GuildMaster.Tests
{
    /// <summary>
    /// Распоряжения: включение и выключение с областью и сроком, снятие по сроку, штраф за отмену льготы, эффекты всех
    /// четырёх, отчёт месяца, лента, детерминизм.
    /// </summary>
    public sealed class DecreeTests
    {
        private const string Lodging = "FreeLodgingForNewcomers";
        private const string GroupOnly = "GroupOnlyFromRank";
        private const string Compensation = "InjuryCompensation";
        private const string Prohibition = "Prohibition";

        private static void Toggle(Simulation simulation, string id, bool enabled, DecreeDuration duration = DecreeDuration.Permanent,
            params GuildRank[] ranks)
        {
            simulation.Send(new ToggleDecreeCommand(id, enabled, ranks, duration));
            simulation.ApplyCommandsNow();
        }

        private static List<SimEvent> TickCollect(Simulation simulation, int ticks = 1) =>
            SimulationRun.Collect(simulation, s => { for (int i = 0; i < ticks; i++) s.Tick(); });

        // ---------- Включение, область, срок ----------

        [Test]
        public void Toggle_EnableWithScopeAndTerm_ChangeAndDisable()
        {
            using var world = new StateWorld();
            Simulation simulation = world.Simulation;
            long now = world.Time.TotalHours;

            Toggle(simulation, GroupOnly, true, DecreeDuration.Week, GuildRank.D, GuildRank.C, GuildRank.D);
            Assert.IsTrue(simulation.World.Decrees.TryGetActive(GroupOnly, out ActiveDecree active), "распоряжение включено");
            CollectionAssert.AreEqual(new[] { GuildRank.D, GuildRank.C }, active.Ranks, "область — ранги без повторов по порядку");
            Assert.AreEqual(now + 7 * 24, active.EndsAtHours, "срок — 7 суток от включения");
            Assert.AreEqual(1, simulation.Events.Events.Count(e => e.Type == SimEventType.DecreeEnabled));

            world.Days(2);
            long changedAt = world.Time.TotalHours;
            Toggle(simulation, GroupOnly, true, DecreeDuration.Month, GuildRank.E, GuildRank.D, GuildRank.C);
            Assert.AreEqual(3, active.Ranks.Count, "область расширена");
            Assert.AreEqual(changedAt + 30 * 24, active.EndsAtHours, "срок отсчитывается заново");
            Assert.AreEqual(1, simulation.Events.Events.Count(e => e.Type == SimEventType.DecreeChanged));

            Toggle(simulation, GroupOnly, true, DecreeDuration.Month, GuildRank.C, GuildRank.D, GuildRank.E);
            Assert.AreEqual(1, simulation.Events.Events.Count(e => e.Type == SimEventType.DecreeChanged), "то же самое — без событий");

            Toggle(simulation, GroupOnly, false);
            Assert.IsFalse(simulation.World.Decrees.IsActive(GroupOnly), "выключено игроком");
            Assert.AreEqual(1, simulation.Events.Events.Count(e => e.Type == SimEventType.DecreeRevoked));

            Toggle(simulation, GroupOnly, true, DecreeDuration.Permanent, GuildRank.C);
            Assert.IsTrue(simulation.World.Decrees.TryGetActive(GroupOnly, out active), "повторно включить можно сразу");
            Assert.IsTrue(active.IsPermanent);
            Assert.AreEqual(0, active.EndsAtHours, "бессрочное не кончается");
        }

        [Test]
        public void Toggle_EmptyRankScope_DoesNothing_AndScopelessIgnoresRanks()
        {
            using var world = new StateWorld();
            Toggle(world.Simulation, GroupOnly, true);
            Assert.IsFalse(world.Simulation.World.Decrees.IsActive(GroupOnly), "без рангов распоряжение с областью не включается");

            Toggle(world.Simulation, Prohibition, true, DecreeDuration.Permanent, GuildRank.C);
            Assert.IsTrue(world.Simulation.World.Decrees.TryGetActive(Prohibition, out ActiveDecree active));
            Assert.AreEqual(0, active.Ranks.Count, "у распоряжения без области рангов нет");
        }

        [Test]
        public void Term_Expires_ByItself_WithoutPenalty()
        {
            using var world = new StateWorld();
            world.AddMany(3);
            Toggle(world.Simulation, Compensation, true, DecreeDuration.Week);
            long endsAt = world.Simulation.World.Decrees.Active[0].EndsAtHours;

            List<SimEvent> events = TickCollect(world.Simulation, 7 * 24 + 1);
            Assert.IsFalse(world.Simulation.World.Decrees.IsActive(Compensation), "срок вышел — выключено");
            SimEvent expired = events.Single(e => e.Type == SimEventType.DecreeExpired);
            Assert.AreEqual(endsAt, expired.TimeHours, "выключается в час окончания срока");
            Assert.IsFalse(events.Any(e => e.Type == SimEventType.DecreeRevoked), "по сроку — не отмена игроком");
            Assert.AreEqual(7 * 24, world.Simulation.World.Decrees.GetActiveHours(Compensation, 0, world.Time.TotalHours));
            Assert.IsTrue(world.Simulation.World.Feed.Guild.Any(f => f.Text.Contains("Срок вышел")), "строка в ленте");
        }

        // ---------- Отмена льготы ----------

        [Test]
        public void BenefitRevokedByPlayer_LowersContentmentOfAffected()
        {
            using var world = new StateWorld();
            Adventurer newcomer = world.Add();
            Adventurer veteran = world.Add();
            veteran.JoinedAtHours = world.Time.TotalHours - 40 * 24;

            Toggle(world.Simulation, Compensation, true);
            Toggle(world.Simulation, Compensation, false);
            Assert.AreEqual(45f, newcomer.State.Contentment, 1e-4f, "компенсация касалась всех");
            Assert.AreEqual(45f, veteran.State.Contentment, 1e-4f);

            Toggle(world.Simulation, Lodging, true);
            Toggle(world.Simulation, Lodging, false);
            Assert.AreEqual(40f, newcomer.State.Contentment, 1e-4f, "кормёжка касалась новичка");
            Assert.AreEqual(45f, veteran.State.Contentment, 1e-4f, "давнего — не касалась");

            Toggle(world.Simulation, Prohibition, true);
            Toggle(world.Simulation, Prohibition, false);
            Assert.AreEqual(40f, newcomer.State.Contentment, 1e-4f, "не льгота — без штрафа");

            List<SimEvent> events = TickCollect(world.Simulation);
            Assert.IsTrue(world.Simulation.World.Feed.Guild.Any(f => f.Text.Contains("встретили молча")), "строка отмены льготы");
            Assert.IsTrue(events.Any(e => e.Type == SimEventType.DecreeRevoked && e.Importance == EventImportance.Notable));
        }

        // ---------- Еда и жильё для новичков ----------

        [Test]
        public void Lodging_GuildPaysForNewcomers_NotForVeterans()
        {
            using var world = new StateWorld();
            Adventurer newcomer = world.Add();
            Adventurer veteran = world.Add();
            veteran.JoinedAtHours = world.Time.TotalHours - 40 * 24;
            Toggle(world.Simulation, Lodging, true);

            world.TickToHour(23);
            int newcomerWallet = newcomer.State.Wallet;
            int veteranWallet = veteran.State.Wallet;
            world.Simulation.Tick();

            IReadOnlyList<LedgerEntry> ledger = world.Simulation.World.Treasury.Ledger;
            LedgerEntry paid = ledger.Single(e => e.Category == LedgerCategories.Decrees);
            Assert.AreEqual(newcomer.Id, paid.RelatedId, "гильдия платит за новичка");
            Assert.AreEqual(-5, paid.Amount, "5 в день");
            Assert.AreEqual(Lodging, paid.Comment);
            Assert.AreEqual(newcomerWallet, newcomer.State.Wallet, "новичок сам не платит");
            Assert.Less(veteran.State.Wallet, veteranWallet, "давний платит сам");
        }

        [Test]
        public void Lodging_RaisesCandidateChance_AndShiftsWorkAxis()
        {
            using var world = new StateWorld();
            WorldState state = world.Simulation.World;
            float before = RecruitSystem.CandidateChance(world.Registry, state);
            Assert.AreEqual(RecruitSystem.CandidateChance(world.Registry, state.Guild.Reputation), before, 1e-6f, "без распоряжения × 1");
            Toggle(world.Simulation, Lodging, true);
            Assert.AreEqual(before * 1.5f, RecruitSystem.CandidateChance(world.Registry, state), 1e-6f, "приток × 1,5");
            Assert.AreEqual(-20f, DecreeRules.CandidateWorkShift(state, world.Registry));

            Adventurer plain = AdventurerGenerator.Generate(new Rng(11), world.Registry, state, 900, false, new List<string>()).Adventurer;
            Adventurer lazy = AdventurerGenerator.Generate(new Rng(11), world.Registry, state, 900, false, new List<string>(),
                workAxisShift: -20f).Adventurer;
            Assert.AreEqual(AxisMath.Clamp(plain.GetAxis(AxisId.Work) - 20f), lazy.GetAxis(AxisId.Work), 0.01f, "ось Труд ниже на 20");
            Assert.AreEqual(plain.Name, lazy.Name, "броски генератора не сдвинулись");
            Assert.AreEqual(plain.GetAxis(AxisId.People), lazy.GetAxis(AxisId.People));
            Assert.AreEqual(plain.State.Wallet, lazy.State.Wallet);
        }

        // ---------- Только группой ----------

        [Test]
        public void GroupOnly_BansSoloForRanksInScope_NotPromotion()
        {
            using var quests = new QuestWorld();
            Adventurer person = quests.Add();
            Order c = quests.AddOrder(rank: GuildRank.C);
            Order d = quests.AddOrder(rank: GuildRank.D);
            Order e = quests.AddOrder(rank: GuildRank.E);
            Order exam = quests.AddOrder(rank: GuildRank.C);
            exam.IsPromotion = true;
            Toggle(quests.Simulation, GroupOnly, true, DecreeDuration.Permanent, GuildRank.D, GuildRank.C);

            DecisionBan ban = DecisionBans.Default.Single(b => b.Reason == "solo forbidden by decree");
            bool Banned(Order order, DecisionActionKind kind = DecisionActionKind.TakeOrder) => quests.World.Do(ctx =>
                ban.Applies(ctx, person, new DecisionAction(kind, Activity.Resting, false, (s, a, sc) => { }, order.Id)));

            Assert.IsTrue(Banned(c), "ранг C в области — одному нельзя");
            Assert.IsTrue(Banned(d), "ранг D в области");
            Assert.IsFalse(Banned(e), "ранг E вне области");
            Assert.IsFalse(Banned(exam), "экзамен — можно одному");
            Assert.IsFalse(Banned(c, DecisionActionKind.SeekParty), "собрать группу — можно");

            Toggle(quests.Simulation, GroupOnly, false);
            Assert.IsFalse(Banned(c), "выключено — запрета нет");
        }

        [Test]
        public void GroupOnly_InitiatorWithoutParty_GivesUp()
        {
            using var quests = new QuestWorld();
            quests.Data.Set("decisions.bestChoiceChance", 1f);
            Adventurer person = quests.Add();
            for (int i = 0; i < Vocabulary.StatCount; i++) person.SetStat((StatId)i, 80f);
            person.GuildRank = GuildRank.C;
            Order order = quests.AddOrder(rank: GuildRank.C, requirement: 10f, reward: 400);
            Toggle(quests.Simulation, GroupOnly, true, DecreeDuration.Permanent, GuildRank.C);

            quests.World.Days(2);
            Assert.AreEqual(0, person.QuestsCompleted + person.QuestsFailed, "одному заказ ранга C не взять");
            Assert.IsTrue(person.State.PlannedOrderId == 0 && !quests.Simulation.World.Quests.Active.Any(), "заданий нет");
        }

        [Test]
        public void GroupOnly_LonersAreUnhappy()
        {
            using var world = new StateWorld();
            Adventurer loner = world.Add();
            loner.SetAxis(AxisId.People, -40f);
            Adventurer other = world.Add();
            WorldState state = world.Simulation.World;
            float lonerBefore = StateRules.ContentmentTarget(loner, 0.2f, world.Registry, state);
            Toggle(world.Simulation, GroupOnly, true, DecreeDuration.Permanent, GuildRank.C);
            Assert.AreEqual(lonerBefore - 3f, StateRules.ContentmentTarget(loner, 0.2f, world.Registry, state), 1e-4f, "одиночка −3");
            Assert.AreEqual(StateRules.ContentmentTarget(other, 0.2f, world.Registry),
                StateRules.ContentmentTarget(other, 0.2f, world.Registry, state), 1e-4f, "остальных не касается");
        }

        // ---------- Компенсация за ранение ----------

        [Test]
        public void Compensation_PaysForEachWound_MaimInsteadOfHeavy()
        {
            using var world = new StateWorld();
            Adventurer person = world.Add();
            world.Do(ctx => HealthService.Wound(ctx, person, ConditionKind.LightWound));
            Assert.IsFalse(world.Simulation.World.Treasury.Ledger.Any(e => e.Category == LedgerCategories.Decrees), "выключено — не платят");

            Toggle(world.Simulation, Compensation, true);
            int wallet = person.State.Wallet;
            int money = world.Simulation.World.Treasury.Money;
            world.Do(ctx => HealthService.Wound(ctx, person, ConditionKind.LightWound));
            Assert.AreEqual(wallet + 20, person.State.Wallet, "лёгкая — 20 раненому");
            Assert.AreEqual(money - 20, world.Simulation.World.Treasury.Money);

            world.Do(ctx => HealthService.Wound(ctx, person, ConditionKind.HeavyWound));
            Assert.AreEqual(wallet + 120, person.State.Wallet, "тяжёлая — 100");
            world.Do(ctx => HealthService.Wound(ctx, person, ConditionKind.HeavyWound));
            Assert.AreEqual(wallet + 420, person.State.Wallet, "увечье — 300 вместо 100");

            int[] paid = world.Simulation.World.Treasury.Ledger.Where(e => e.Category == LedgerCategories.Decrees).Select(e => -e.Amount).ToArray();
            CollectionAssert.AreEqual(new[] { 20, 100, 300 }, paid);
            Assert.AreEqual(3, world.Simulation.Events.Events.Count(e => e.Type == SimEventType.InjuryCompensated));
        }

        [Test]
        public void Compensation_SafetyInOrderOptions_AndDailyLoyalty()
        {
            float LoyaltyAfterDay(bool compensation)
            {
                using var world = new StateWorld();
                Adventurer person = world.Add();
                if (compensation) Toggle(world.Simulation, Compensation, true);
                world.TickToHour(0);
                return person.State.Loyalty;
            }

            using (var world = new StateWorld())
            {
                Assert.AreEqual(1f, world.Do(ctx => new DecisionScope(ctx).SafetyMultiplier), "выключено — × 1");
                Toggle(world.Simulation, Compensation, true);
                Assert.AreEqual(0.85f, world.Do(ctx => new DecisionScope(ctx).SafetyMultiplier), 1e-6f, "Безопасность × 0,85");
            }
            Assert.AreEqual(LoyaltyAfterDay(false) + 0.05f, LoyaltyAfterDay(true), 1e-4f, "лояльность +0,05 за сутки");
        }

        // ---------- Сухой закон ----------

        [Test]
        public void Prohibition_TavernIncome_StressRelief_Drunkard()
        {
            using var world = new StateWorld();
            Adventurer drunkard = world.Add("Drunkard");
            Adventurer sober = world.Add();
            WorldState state = world.Simulation.World;
            DataRegistry data = world.Registry;
            float soberTarget = StateRules.ContentmentTarget(sober, 0.2f, data, state);
            float drunkardTarget = StateRules.ContentmentTarget(drunkard, 0.2f, data, state);

            Toggle(world.Simulation, Prohibition, true, DecreeDuration.Week);
            Assert.AreEqual(0.6f, DecreeRules.TavernIncomeMultiplier(state, data), 1e-6f, "доход таверны −40%");
            Assert.AreEqual(0.7f, DecreeRules.TavernStressReliefMultiplier(sober, state, data), 1e-6f, "снятие стресса × 0,7");
            Assert.AreEqual(0f, DecreeRules.TavernStressReliefMultiplier(drunkard, state, data), "Пьяница в таверне стресс не снимает");
            Assert.AreEqual(0f, DecreeRules.DrunkardSkipChance(state, data, 0.3f), "запой-пропуск дня — 0");
            Assert.AreEqual(drunkardTarget - 10f, StateRules.ContentmentTarget(drunkard, 0.2f, data, state), 1e-4f, "Пьяница −10");
            Assert.AreEqual(soberTarget, StateRules.ContentmentTarget(sober, 0.2f, data, state), 1e-4f);

            int money = state.Treasury.Money;
            world.Do(ctx =>
            {
                state.Treasury.TavernFood = 10;
                state.Treasury.TavernDrinks = 10;
                TreasuryService.SettleTavern(ctx);
            });
            Assert.AreEqual(money + 6, state.Treasury.Money, "20 порций × 0,5 × 0,6 = 6");

            sober.State.Activity = Activity.Tavern;
            sober.State.Stress = 50f;
            drunkard.State.Activity = Activity.Tavern;
            drunkard.State.Stress = 50f;
            world.Do(ctx =>
            {
                StateService.ApplyHour(ctx, sober, PartyContext.None);
                StateService.ApplyHour(ctx, drunkard, PartyContext.None);
            });
            Assert.AreEqual(50f, drunkard.State.Stress, 1e-4f, "стресс Пьяницы не снят");
            Assert.Less(sober.State.Stress, 50f, "трезвый снимает стресс");
        }

        [Test]
        public void Prohibition_FeedLine_WithKnownDrunkardOrQuiet()
        {
            using var world = new StateWorld();
            Adventurer drunkard = world.Add("Drunkard");
            Toggle(world.Simulation, Prohibition, true);
            TickCollect(world.Simulation);
            FeedEntry line = world.Simulation.World.Feed.Guild.Last();
            Assert.AreEqual("guild.decree.prohibition.quiet", line.TemplateKey, "черта скрыта — Пьяница не назван");
            Assert.IsFalse(world.Simulation.World.Feed.Guild.Any(f => f.TemplateKey == "guild.decree.enabled"), "вместо «Объявлено»");

            Toggle(world.Simulation, Prohibition, false);
            drunkard.Traits.Single().Revealed = true;
            Toggle(world.Simulation, Prohibition, true);
            TickCollect(world.Simulation);
            line = world.Simulation.World.Feed.Guild.Last(f => f.TemplateKey.StartsWith("guild.decree.prohibition"));
            Assert.AreEqual("guild.decree.prohibition", line.TemplateKey);
            Assert.IsTrue(line.Text.Contains(drunkard.Name), "известный Пьяница назван");
        }

        [Test]
        public void Enabled_FeedLine_Announced()
        {
            using var world = new StateWorld();
            Toggle(world.Simulation, Compensation, true);
            TickCollect(world.Simulation);
            Assert.IsTrue(world.Simulation.World.Feed.Guild.Any(f => f.Text.StartsWith("Объявлено: Компенсация за ранение")));
        }

        // ---------- Отчёт месяца ----------

        [Test]
        public void MonthReport_ListsDecreesWithDaysAndCost()
        {
            using var world = new StateWorld();
            Adventurer person = world.Add();
            Toggle(world.Simulation, Compensation, true, DecreeDuration.Week);
            world.Do(ctx => HealthService.Wound(ctx, person, ConditionKind.LightWound));
            world.Days(31);

            MonthReport report = world.Simulation.World.Reports.Reports.First();
            Assert.IsTrue(report.TryGetSection(MonthReportSections.DecreesTitle, out ReportSection section), "раздел «Распоряжения»");
            Assert.IsTrue(section.TryGetLine(MonthReportSections.DecreesActed, out ReportLine acted));
            ReportItem item = acted.Items.Single();
            Assert.AreEqual(7, item.Days, "действовало 7 дней");
            Assert.AreEqual(-20, item.Amount, "стоило 20");
            Assert.IsTrue(section.TryGetLine(MonthReportSections.DecreesCost, out ReportLine cost));
            Assert.AreEqual(-20, cost.Amount);

            List<string> errors = new List<string>();
            List<string> lines = MonthReportText.Lines(report, world.Simulation.World, world.Registry, errors);
            CollectionAssert.IsEmpty(errors);
            Assert.IsTrue(lines.Any(l => l.Contains("Компенсация за ранение — 7 дн. -20")), string.Join("\n", lines));
        }

        // ---------- Детерминизм ----------

        private static string RunWithDecrees(PeopleData data, uint seed, bool decrees)
        {
            Simulation simulation = Simulation.CreateDefault(data.Registry, seed);
            return SimulationLog.Record(simulation, s =>
            {
                for (int day = 0; day < 40; day++)
                {
                    if (decrees && day == 1)
                    {
                        s.Send(new ToggleDecreeCommand(Lodging, true));
                        s.Send(new ToggleDecreeCommand(GroupOnly, true, new[] { GuildRank.F, GuildRank.E }));
                        s.Send(new ToggleDecreeCommand(Compensation, true, null, DecreeDuration.Month));
                        s.Send(new ToggleDecreeCommand(Prohibition, true, null, DecreeDuration.Week));
                    }
                    if (decrees && day == 20) s.Send(new ToggleDecreeCommand(Lodging, false));
                    for (int h = 0; h < 24; h++) s.Tick();
                }
            });
        }

        [Test]
        public void SameSeed_WithDecrees_SameLog()
        {
            using var data = new PeopleData();
            string first = RunWithDecrees(data, 5, true);
            string second = RunWithDecrees(data, 5, true);
            Assert.AreEqual(first, second, "одно зерно и те же команды — тот же лог");
            Assert.AreNotEqual(RunWithDecrees(data, 5, false), first, "распоряжения меняют мир");
        }

        [Test]
        public void DisabledDecrees_DoNotShiftOtherRolls()
        {
            using var data = new PeopleData();
            string plain = SimulationLog.Record(Simulation.CreateDefault(data.Registry, 9), s => SimulationRun.Days(s, 30));

            List<ISimSystem> systems = SimulationSystems.CreateDefault();
            systems.RemoveAll(s => s is DecreeSystem);
            string withoutSystem = SimulationLog.Record(new Simulation(data.Registry, 9, systems), s => SimulationRun.Days(s, 30));
            Assert.AreEqual(withoutSystem, plain, "пустая система распоряжений бросков не тратит");

            // Включить и выключить на паузе до первого такта: мир тот же, кроме строк самих распоряжений.
            Simulation toggled = Simulation.CreateDefault(data.Registry, 9);
            toggled.Send(new ToggleDecreeCommand(Prohibition, true));
            toggled.Send(new ToggleDecreeCommand(Prohibition, false));
            toggled.ApplyCommandsNow();
            string afterToggle = SimulationLog.Record(toggled, s => SimulationRun.Days(s, 30));
            string Strip(string log) => string.Join("\n", log.Split('\n').Where(l => !l.Contains("Decree")));
            Assert.AreEqual(Strip(plain), Strip(afterToggle), "выключенное распоряжение чужие броски не сдвигает");
        }
    }
}
