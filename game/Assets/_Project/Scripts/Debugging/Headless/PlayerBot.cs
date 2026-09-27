using System;
using System.Collections.Generic;
using GuildMaster.Core;

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

        /// <summary>Ставит комиссию 20% и принимает всех кандидатов в авантюристы.</summary>
        public static PlayerBot Simple() =>
            new PlayerBot(SimpleName, new SetCommissionOnceRule(SimpleCommission), new AcceptAllCandidatesRule());

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
}
