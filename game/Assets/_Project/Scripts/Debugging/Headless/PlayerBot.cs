using System;
using System.Collections.Generic;
using GuildMaster.Core;
using GuildMaster.Data;

namespace GuildMaster.Debugging
{
    /// <summary>
    /// Ход бота перед тактом: мир на чтение и отправка команд — ровно то, что доступно игроку через интерфейс.
    /// Команда применится в начале ближайшего такта.
    /// </summary>
    public sealed class BotTurn
    {
        private readonly Action<ICommand> send;

        internal BotTurn(ISimulationClient game, Action<ICommand> send)
        {
            Game = game;
            this.send = send;
        }

        public ISimulationClient Game { get; }
        public WorldState World => Game.World;

        /// <summary>Сколько тактов сделано до этого хода; команда хода применится в такте с этим номером (с нуля).</summary>
        public long Tick { get; internal set; }

        public void Send(ICommand command) => send(command ?? throw new ArgumentNullException(nameof(command)));
    }

    /// <summary>Правило бота: смотрит на мир и отдаёт команды. Правила бота выполняются по порядку каждый ход.</summary>
    public interface IBotRule
    {
        void Act(BotTurn turn);
    }

    /// <summary>Бот игрока для прогона без интерфейса: имя и список правил. Без правил — ничего не делает.</summary>
    public sealed class PlayerBot
    {
        private readonly List<IBotRule> rules;

        public PlayerBot(string name, params IBotRule[] rules)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("Bot name is required", nameof(name));
            Name = name;
            this.rules = new List<IBotRule>(rules ?? Array.Empty<IBotRule>());
        }

        public string Name { get; }
        public IReadOnlyList<IBotRule> Rules => rules;

        public void Act(BotTurn turn)
        {
            foreach (IBotRule rule in rules) rule.Act(turn);
        }
    }

    /// <summary>Готовый бот для списка в окне прогона.</summary>
    public sealed class BotPreset
    {
        public BotPreset(string name, Func<PlayerBot> create)
        {
            Name = name;
            Create = create;
        }

        public string Name { get; }
        public Func<PlayerBot> Create { get; }
    }

    /// <summary>
    /// Боты прогона. Новый бот — строка в <see cref="Presets"/>; новое поведение готового бота — правило в его списке.
    /// Сценарий из файла — <see cref="Scenario"/>: ему нужен файл, поэтому в список готовых он не входит.
    /// </summary>
    public static class PlayerBots
    {
        public const string PassiveName = "Пассивный";
        public const string SimpleName = "Простой";
        public const string RecklessName = "Безрассудный";
        public const string SmartName = "Умный";
        public const string ScenarioName = "Сценарий";

        public static readonly IReadOnlyList<BotPreset> Presets = new[]
        {
            new BotPreset(PassiveName, Passive),
            new BotPreset(SimpleName, Simple),
            new BotPreset(RecklessName, Reckless),
            new BotPreset(SmartName, Smart),
        };

        /// <summary>Ничего не делает: мир живёт сам.</summary>
        public static PlayerBot Passive() => new PlayerBot(PassiveName);

        /// <summary>Комиссия бота «Простой».</summary>
        public const float SimpleCommission = 0.2f;

        /// <summary>Сколько месяцев расходов бот «Простой» держит в запасе, прежде чем строить.</summary>
        public const int SimpleReserveMonths = 3;

        /// <summary>Сколько месяцев расходов бот «Простой» оставляет в казне после подъёмных, когда людей хватает.</summary>
        public const int SimpleCandidateReserveMonths = 1;

        /// <summary>Пока людей меньше, бот «Простой» принимает каждого, на чьи подъёмные хватает казны, без запаса.</summary>
        public const int SimpleMinPeople = 8;

        /// <summary>
        /// Ставит комиссию 20%, принимает кандидатов в авантюристы: пока людей меньше восьми — каждого, на чьи подъёмные хватает
        /// казны, дальше — если после подъёмных в казне остаётся месяц расходов; Регистратору велит брать все типы до высшего ранга людей
        /// гильдии без порога награды, важные заказы принимает, как только в гильдии есть человек этого ранга или выше; до тех пор
        /// заказ ждёт, и без ответа заказчик уходит сам. Строит Общежитие → Лазарет → Тренировочный двор по одной, когда денег не
        /// меньше цены и трёх месяцев расходов; на вакансию нанимает кандидата с высшим уровнем за просимую зарплату; на обращения
        /// отвечает наугад (или не отвечает — тогда по сроку срабатывает вариант по умолчанию).
        /// </summary>
        public static PlayerBot Simple() =>
            new PlayerBot(SimpleName, new SetCommissionOnceRule(SimpleCommission), new AcceptCandidatesWithReserveRule(SimpleCandidateReserveMonths, SimpleMinPeople),
                new RegistrarUpToTopRankRule(), new AnswerImportantOrdersByRankRule(), new AnswerEventQuestsBySourceRankRule(),
                new BuildWithReserveRule(SimpleReserveMonths, BuildingFunction.Dormitory, BuildingFunction.Infirmary, BuildingFunction.TrainingYard),
                new HireBestCandidateRule(), new AnswerDilemmasRandomlyRule());

        /// <summary>Комиссия бота «Безрассудный».</summary>
        public const float RecklessCommission = 0.5f;

        /// <summary>
        /// Жадный игрок без запаса: ставит комиссию 50%, принимает каждого кандидата, на чьи подъёмные хватает казны, Регистратору велит
        /// брать все типы до высшего ранга людей, строит Общежитие → Лазарет → Тренировочный двор, как только денег не меньше цены,
        /// на вакансию нанимает кандидата с высшим уровнем за просимую зарплату, на обращения отвечает наугад. На важные заказы и
        /// событийные задания не отвечает.
        /// </summary>
        public static PlayerBot Reckless() =>
            new PlayerBot(RecklessName, new SetCommissionOnceRule(RecklessCommission), new AcceptCandidatesWithReserveRule(0),
                new RegistrarUpToTopRankRule(),
                new BuildWithReserveRule(0, BuildingFunction.Dormitory, BuildingFunction.Infirmary, BuildingFunction.TrainingYard),
                new HireBestCandidateRule(), new AnswerDilemmasRandomlyRule());

        /// <summary>Бот «Умный» принимает кандидатов, только пока людей меньше, чем заказов в день × это число.</summary>
        public const int SmartPeoplePerDailyOrder = 8;

        /// <summary>Бот «Умный» ставит доплату на заказы-угрозы, пока Безопасность ниже этого.</summary>
        public const float SmartSurchargeBelowSecurity = 50f;

        /// <summary>Доплата бота «Умный» на заказ-угрозу — эта доля награды.</summary>
        public const float SmartSurchargeShare = 0.25f;

        /// <summary>
        /// Готовится к кризисам: правила «Простого» (комиссия 20%, Регистратор, важные заказы и событийные задания, найм, обращения),
        /// но кандидатов принимает, только пока людей меньше, чем заказов в день × 8; строит Общежитие → Лазарет → Тренировочный двор →
        /// Дозорную вышку → Частокол → Колодец → Арсенал → Дорогу, затем улучшает Арсенал, — только если после стройки в казне остаётся
        /// ближайший налог и три месяца расходов; пока Безопасность ниже 50, ставит доплату 25% награды на заказы-угрозы на доске
        /// (с тем же запасом).
        /// </summary>
        public static PlayerBot Smart() =>
            new PlayerBot(SmartName, new SetCommissionOnceRule(SimpleCommission),
                new AcceptCandidatesWithReserveRule(SimpleCandidateReserveMonths, SimpleMinPeople, SmartPeopleLimit),
                new RegistrarUpToTopRankRule(), new AnswerImportantOrdersByRankRule(), new AnswerEventQuestsBySourceRankRule(),
                new BuildWithTaxReserveRule(SimpleReserveMonths, BuildingFunction.Dormitory, BuildingFunction.Infirmary, BuildingFunction.TrainingYard,
                    BuildingFunction.Watchtower, BuildingFunction.Palisade, BuildingFunction.Well, BuildingFunction.Armory, BuildingFunction.Road),
                new SurchargeThreatsRule(SmartSurchargeBelowSecurity, SmartSurchargeShare, SimpleReserveMonths),
                new HireBestCandidateRule(), new AnswerDilemmasRandomlyRule());

        /// <summary>Предел людей бота «Умный»: заказов в день сейчас × <see cref="SmartPeoplePerDailyOrder"/>, вниз.</summary>
        public static int SmartPeopleLimit(WorldState world, DataRegistry data) =>
            (int)(CrisisRules.OrdersPerDay(world, data) * SmartPeoplePerDailyOrder);

        /// <summary>Повторяет команды сценария в их такты.</summary>
        public static PlayerBot Scenario(ScenarioScript script) => new PlayerBot(ScenarioName, new ScenarioRule(script));
    }

    /// <summary>Поставить комиссию один раз, в первый ход.</summary>
    public sealed class SetCommissionOnceRule : IBotRule
    {
        private readonly float commission;
        private bool sent;

        public SetCommissionOnceRule(float commission)
        {
            this.commission = commission;
        }

        public void Act(BotTurn turn)
        {
            if (sent) return;
            sent = true;
            turn.Send(new SetCommissionCommand(commission));
        }
    }

    /// <summary>Принять каждого ожидающего кандидата в авантюристы.</summary>
    public sealed class AcceptAllCandidatesRule : IBotRule
    {
        public void Act(BotTurn turn)
        {
            IReadOnlyList<Candidate> candidates = turn.World.Adventurers.Candidates;
            for (int i = 0; i < candidates.Count; i++) turn.Send(new AcceptCandidateCommand(candidates[i].Adventurer.Id));
        }
    }

    /// <summary>
    /// Принять ожидающих кандидатов по порядку прихода, пока после их подъёмных в казне остаётся не меньше <c>reserveMonths</c>
    /// месяцев расходов (как запас перед стройкой, <see cref="BuildWithReserveRule.Reserve"/>). Пока людей вместе с принятыми
    /// меньше <c>minPeople</c>, запаса не нужно — хватило бы на подъёмные. Не хватает — кандидат ждёт.
    /// </summary>
    public sealed class AcceptCandidatesWithReserveRule : IBotRule
    {
        private readonly int reserveMonths;
        private readonly int minPeople;
        private readonly Func<WorldState, DataRegistry, int> peopleLimit;

        /// <param name="peopleLimit">Предел людей в гильдии (с принятыми в этот ход); не задан — без предела.</param>
        public AcceptCandidatesWithReserveRule(int reserveMonths, int minPeople = 0, Func<WorldState, DataRegistry, int> peopleLimit = null)
        {
            this.reserveMonths = reserveMonths;
            this.minPeople = minPeople;
            this.peopleLimit = peopleLimit;
        }

        public void Act(BotTurn turn)
        {
            IReadOnlyList<Candidate> candidates = turn.World.Adventurers.Candidates;
            if (candidates.Count == 0) return;

            DataRegistry data = turn.Game.Data;
            long money = turn.World.Treasury.Money;
            int reserve = BuildWithReserveRule.Reserve(turn.World, data, reserveMonths);
            int people = turn.World.Adventurers.Active.Count;
            int limit = peopleLimit?.Invoke(turn.World, data) ?? int.MaxValue;
            for (int i = 0; i < candidates.Count && people < limit; i++)
            {
                int bonus = RecruitSystem.SigningBonus(data, candidates[i]);
                if (money - bonus < (people < minPeople ? 0 : reserve)) continue;
                money -= bonus;
                people++;
                turn.Send(new AcceptCandidateCommand(candidates[i].Adventurer.Id));
            }
        }
    }

    /// <summary>
    /// Правила Регистратора — все типы, до высшего ранга людей гильдии, без порога награды. Высший ранг сменился — новые правила.
    /// Людей нет — до ранга G.
    /// </summary>
    public sealed class RegistrarUpToTopRankRule : IBotRule
    {
        public void Act(BotTurn turn)
        {
            var wanted = new RegistrarRules(null, TopRank(turn.World), 0);
            if (!turn.World.Orders.Rules.IsSameAs(wanted)) turn.Send(new SetRegistrarRulesCommand(wanted));
        }

        /// <summary>Высший ранг среди людей в гильдии; никого нет — G.</summary>
        public static GuildRank TopRank(WorldState world)
        {
            GuildRank top = GuildRank.G;
            foreach (Adventurer adventurer in world.Adventurers.Active)
            {
                if (adventurer.GuildRank > top) top = adventurer.GuildRank;
            }
            return top;
        }
    }

    /// <summary>
    /// Важный заказ: принять без доплаты, как только в гильдии есть человек его ранга или выше. Нет такого — не отвечать:
    /// заказ ждёт (вдруг кто-то вырастет или придёт), без ответа заказчик уходит сам.
    /// </summary>
    public sealed class AnswerImportantOrdersByRankRule : IBotRule
    {
        public void Act(BotTurn turn)
        {
            IReadOnlyList<Order> orders = turn.World.Orders.Open;
            GuildRank top = RegistrarUpToTopRankRule.TopRank(turn.World);
            bool anyone = turn.World.Adventurers.Active.Count > 0;
            for (int i = 0; i < orders.Count; i++)
            {
                Order order = orders[i];
                if (order.Status != OrderStatus.AwaitingPlayer || order.IsEventQuest || !anyone || order.Rank > top) continue;
                turn.Send(new AnswerImportantOrderCommand(order.Id, true));
            }
        }
    }

    /// <summary>
    /// Событийное задание: назначить ранг задания, в котором сделана находка, как только в гильдии есть человек этого ранга или
    /// выше. Нет такого — не отвечать: без ответа задание отклоняется само.
    /// </summary>
    public sealed class AnswerEventQuestsBySourceRankRule : IBotRule
    {
        public void Act(BotTurn turn)
        {
            IReadOnlyList<Order> orders = turn.World.Orders.Open;
            GuildRank top = RegistrarUpToTopRankRule.TopRank(turn.World);
            bool anyone = turn.World.Adventurers.Active.Count > 0;
            for (int i = 0; i < orders.Count; i++)
            {
                Order order = orders[i];
                if (order.Status != OrderStatus.AwaitingPlayer || !order.IsEventQuest || !anyone || order.SourceRank > top) continue;
                turn.Send(new AnswerEventQuestCommand(order.Id, order.SourceRank));
            }
        }
    }

    /// <summary>
    /// Стройка по списку назначений: когда стройки и очереди нет, заказать первую ещё не построенную постройку списка, если в казне
    /// не меньше её цены плюс <c>reserveMonths</c> месяцев расходов (зарплаты и содержание готовых построек).
    /// </summary>
    public sealed class BuildWithReserveRule : IBotRule
    {
        private readonly int reserveMonths;
        private readonly BuildingFunction[] order;

        public BuildWithReserveRule(int reserveMonths, params BuildingFunction[] order)
        {
            this.reserveMonths = reserveMonths;
            this.order = order ?? Array.Empty<BuildingFunction>();
        }

        public void Act(BotTurn turn)
        {
            WorldState world = turn.World;
            DataRegistry data = turn.Game.Data;
            if (!data.HasDefinitions || world.Buildings.Current != null || world.Buildings.Queue.Count > 0) return;

            foreach (BuildingFunction function in order)
            {
                BuildingDefinition definition = data.BuildingWith(function);
                if (definition == null || world.Buildings.TryGetByDefinition(definition.Id, out _)) continue;

                if (world.Treasury.Money >= definition.Cost + Reserve(world, data, reserveMonths)) turn.Send(new StartBuildingCommand(definition.Id));
                return;
            }
        }

        /// <summary>Запас: <paramref name="months"/> месяцев расходов — зарплаты и содержание готовых построек.</summary>
        public static int Reserve(WorldState world, DataRegistry data, int months) =>
            months * (StaffRules.MonthlySalaries(world) + BuildingRules.MonthlyUpkeep(world, data));
    }

    /// <summary>
    /// Стройка с запасом на налог: постройки по порядку (одна за раз, следующая — когда нет стройки и очереди), потом улучшение
    /// построек с уровнями (Арсенал) до наибольшего; только если после цены в казне остаются ближайший налог и
    /// <c>reserveMonths</c> месяцев расходов.
    /// </summary>
    public sealed class BuildWithTaxReserveRule : IBotRule
    {
        private readonly int reserveMonths;
        private readonly BuildingFunction[] order;

        public BuildWithTaxReserveRule(int reserveMonths, params BuildingFunction[] order)
        {
            this.reserveMonths = reserveMonths;
            this.order = order ?? Array.Empty<BuildingFunction>();
        }

        public void Act(BotTurn turn)
        {
            WorldState world = turn.World;
            DataRegistry data = turn.Game.Data;
            if (!data.HasDefinitions || world.Buildings.Current != null || world.Buildings.Queue.Count > 0) return;

            int reserve = Reserve(world, data, reserveMonths);
            foreach (BuildingFunction function in order)
            {
                BuildingDefinition definition = data.BuildingWith(function);
                if (definition == null || world.Buildings.TryGetByDefinition(definition.Id, out _)) continue;
                if (world.Treasury.Money >= definition.Cost + reserve) turn.Send(new StartBuildingCommand(definition.Id));
                return;
            }

            foreach (BuildingFunction function in order)
            {
                BuildingDefinition definition = data.BuildingWith(function);
                if (definition == null || !world.Buildings.TryGetByDefinition(definition.Id, out Building building) || !building.IsReady
                    || building.Level >= definition.MaxLevel) continue;
                if (world.Treasury.Money >= BuildingService.CostFor(definition, building.Level + 1) + reserve)
                    turn.Send(new UpgradeBuildingCommand(building.Id));
                return;
            }
        }

        /// <summary>Запас: ближайший налог (объявленный — его сумма, иначе по доходам последних трёх месяцев) и месяцы расходов.</summary>
        public static int Reserve(WorldState world, DataRegistry data, int months) =>
            NextTax(world, data) + BuildWithReserveRule.Reserve(world, data, months);

        /// <summary>Ближайший налог: объявленный — его сумма; не объявлен — по доходам гильдии за последние три месяца.</summary>
        public static int NextTax(WorldState world, DataRegistry data)
        {
            foreach (Crisis crisis in world.Crises.All)
            {
                if (crisis.Kind == CrisisKind.Tax && crisis.Status == CrisisStatus.Warned) return crisis.Amount;
            }
            long now = world.Time.TotalHours;
            long quarter = 3L * data.Balance.Time.DaysPerMonth * data.Balance.Time.HoursPerDay;
            return CrisisRules.Tax(CrisisRules.IncomeSince(world, Math.Max(0L, now - quarter)), data.Balance.Crises);
        }
    }

    /// <summary>
    /// Пока Безопасность ниже порога — доплата (доля награды) на заказы-угрозы на доске без доплаты, если после неё в казне остаются
    /// ближайший налог и месяцы расходов.
    /// </summary>
    public sealed class SurchargeThreatsRule : IBotRule
    {
        private readonly float belowSecurity;
        private readonly float share;
        private readonly int reserveMonths;

        public SurchargeThreatsRule(float belowSecurity, float share, int reserveMonths)
        {
            this.belowSecurity = belowSecurity;
            this.share = share;
            this.reserveMonths = reserveMonths;
        }

        public void Act(BotTurn turn)
        {
            WorldState world = turn.World;
            DataRegistry data = turn.Game.Data;
            if (!data.HasDefinitions || world.Settlement.Security >= belowSecurity) return;

            long money = world.Treasury.Money - BuildWithTaxReserveRule.Reserve(world, data, reserveMonths);
            foreach (Order order in world.Orders.Open)
            {
                if (order.Status != OrderStatus.OnBoard || order.Surcharge > 0 || order.IsPromotion || !SettlementRules.IsThreat(data, order.TypeId)) continue;
                int amount = (int)Math.Round(order.Reward * share, MidpointRounding.AwayFromZero);
                if (amount <= 0 || money < amount) continue;
                money -= amount;
                turn.Send(new SetSurchargeCommand(order.Id, amount));
            }
        }
    }

    /// <summary>Вакансия с кандидатами: предложить кандидату с высшим уровнем (при равенстве — пришедшему раньше) просимую зарплату.</summary>
    public sealed class HireBestCandidateRule : IBotRule
    {
        public void Act(BotTurn turn)
        {
            IReadOnlyList<StaffCandidate> candidates = turn.World.Staff.Candidates;
            var roles = new HashSet<string>();
            for (int i = 0; i < candidates.Count; i++)
            {
                string role = candidates[i].RoleId;
                if (!roles.Add(role) || turn.World.Staff.HasRole(role)) continue;

                StaffCandidate best = candidates[i];
                for (int j = i + 1; j < candidates.Count; j++)
                {
                    if (candidates[j].RoleId == role && candidates[j].Level > best.Level) best = candidates[j];
                }
                turn.Send(new OfferSalaryCommand(best.Id, best.AskedSalary));
            }
        }
    }

    /// <summary>
    /// Обращения: на каждое новое — один ответ наугад из вариантов, доступных сейчас, или «не отвечать» (равный шанс). Случайность —
    /// своя у каждого обращения (из его id и часа появления): мир не сдвигается, а один прогон с тем же зерном отвечает так же.
    /// </summary>
    public sealed class AnswerDilemmasRandomlyRule : IBotRule
    {
        private readonly HashSet<int> seen = new HashSet<int>();

        public void Act(BotTurn turn)
        {
            IReadOnlyList<Dilemma> open = turn.World.Dilemmas.Open;
            for (int i = 0; i < open.Count; i++)
            {
                Dilemma dilemma = open[i];
                if (!seen.Add(dilemma.Id)) continue;

                List<int> options = DilemmaRules.AvailableOptions(turn.World, turn.Game.Data, dilemma);
                var rng = new Rng(((ulong)(uint)dilemma.Id << 32) ^ (ulong)dilemma.ArrivedAtHours, (ulong)dilemma.Id);
                int pick = rng.Range(0, options.Count + 1);
                if (pick < options.Count) turn.Send(new AnswerDilemmaCommand(dilemma.Id, options[pick]));
            }
        }
    }
}
