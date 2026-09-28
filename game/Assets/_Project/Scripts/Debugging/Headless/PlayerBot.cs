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
        public const string ScenarioName = "Сценарий";

        public static readonly IReadOnlyList<BotPreset> Presets = new[]
        {
            new BotPreset(PassiveName, Passive),
            new BotPreset(SimpleName, Simple),
        };

        /// <summary>Ничего не делает: мир живёт сам.</summary>
        public static PlayerBot Passive() => new PlayerBot(PassiveName);

        /// <summary>Комиссия бота «Простой».</summary>
        public const float SimpleCommission = 0.2f;

        /// <summary>Сколько месяцев расходов бот «Простой» держит в запасе, прежде чем строить.</summary>
        public const int SimpleReserveMonths = 3;

        /// <summary>
        /// Ставит комиссию 20%, принимает всех кандидатов в авантюристы, Регистратору велит брать все типы до высшего ранга людей
        /// гильдии без порога награды, важные заказы принимает, как только в гильдии есть человек этого ранга или выше; до тех пор
        /// заказ ждёт, и без ответа заказчик уходит сам. Строит Общежитие → Лазарет → Тренировочный двор по одной, когда денег не
        /// меньше цены и трёх месяцев расходов; на вакансию нанимает кандидата с высшим уровнем за просимую зарплату.
        /// </summary>
        public static PlayerBot Simple() =>
            new PlayerBot(SimpleName, new SetCommissionOnceRule(SimpleCommission), new AcceptAllCandidatesRule(),
                new RegistrarUpToTopRankRule(), new AnswerImportantOrdersByRankRule(), new AnswerEventQuestsBySourceRankRule(),
                new BuildWithReserveRule(SimpleReserveMonths, BuildingFunction.Dormitory, BuildingFunction.Infirmary, BuildingFunction.TrainingYard),
                new HireBestCandidateRule());

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

                int reserve = reserveMonths * (StaffRules.MonthlySalaries(world) + BuildingRules.MonthlyUpkeep(world, data));
                if (world.Treasury.Money >= definition.Cost + reserve) turn.Send(new StartBuildingCommand(definition.Id));
                return;
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
}
