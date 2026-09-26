using System;
using System.Collections.Generic;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Доступ симуляции к определениям из ScriptableObject. Только чтение: ассеты во время игры не меняются.
    /// Строится из <see cref="GameConfig"/> при старте (<see cref="FromConfig"/>); битые данные — исключение сразу,
    /// а не ошибка посреди игры. Проверить данные заранее — GuildMaster → Validate Data.
    /// </summary>
    public sealed class DataRegistry
    {
        private static readonly IReadOnlyList<FeedTemplate> NoTemplates = Array.Empty<FeedTemplate>();

        private readonly Dictionary<Type, Dictionary<string, Definition>> byId = new Dictionary<Type, Dictionary<string, Definition>>();
        private readonly Dictionary<Type, List<Definition>> byType = new Dictionary<Type, List<Definition>>();
        private readonly Dictionary<AxisId, AxisDefinition> axes = new Dictionary<AxisId, AxisDefinition>();
        private readonly Dictionary<string, List<FeedTemplate>> feedByKey = new Dictionary<string, List<FeedTemplate>>(StringComparer.Ordinal);

        /// <summary>Только числа баланса, без определений — для тестов и систем, которым хватает чисел.</summary>
        public DataRegistry(BalanceSettings balance)
        {
            Balance = balance != null ? balance : throw new ArgumentNullException(nameof(balance));
        }

        private DataRegistry(GameConfig config) : this(config.Balance)
        {
            Config = config;
            Stats = config.StatCatalog;
            Names = config.NameList;
            OrderTexts = config.OrderTexts;

            foreach (Definition definition in config.AllDefinitions())
            {
                Add(config, definition);
            }

            foreach (AxisDefinition axis in All<AxisDefinition>())
            {
                if (axes.ContainsKey(axis.Axis)) throw new ArgumentException($"{config.name}: axis {axis.Axis} is defined twice");
                axes.Add(axis.Axis, axis);
            }

            if (config.FeedTemplates != null)
            {
                foreach (FeedTemplate template in config.FeedTemplates.Templates)
                {
                    if (!feedByKey.TryGetValue(template.Key, out List<FeedTemplate> list))
                    {
                        list = new List<FeedTemplate>();
                        feedByKey.Add(template.Key, list);
                    }
                    list.Add(template);
                }
            }
        }

        public static DataRegistry FromConfig(GameConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (config.Balance == null) throw new ArgumentException($"{config.name}: Balance is not set", nameof(config));
            return new DataRegistry(config);
        }

        public BalanceSettings Balance { get; }

        /// <summary>Корневой ассет; <c>null</c>, если реестр собран только из чисел.</summary>
        public GameConfig Config { get; }

        public StatCatalog Stats { get; }
        public NameList Names { get; }
        public OrderTextTemplates OrderTexts { get; }

        /// <summary>Определение вида <typeparamref name="T"/> по id; нет такого — исключение.</summary>
        public T Get<T>(string id) where T : Definition =>
            TryGet(id, out T definition) ? definition : throw new KeyNotFoundException($"No {typeof(T).Name} with id '{id}'");

        public bool TryGet<T>(string id, out T definition) where T : Definition
        {
            definition = null;
            if (id == null || !byId.TryGetValue(typeof(T), out Dictionary<string, Definition> map)) return false;
            if (!map.TryGetValue(id, out Definition found)) return false;
            definition = (T)found;
            return true;
        }

        /// <summary>Все определения вида <typeparamref name="T"/> в порядке GameConfig.</summary>
        public IReadOnlyList<T> All<T>() where T : Definition
        {
            if (!byType.TryGetValue(typeof(T), out List<Definition> list)) return Array.Empty<T>();
            var result = new T[list.Count];
            for (int i = 0; i < list.Count; i++) result[i] = (T)list[i];
            return result;
        }

        public AxisDefinition Axis(AxisId axis) =>
            axes.TryGetValue(axis, out AxisDefinition definition) ? definition : throw new KeyNotFoundException($"No axis {axis}");

        /// <summary>Шаблоны ленты с этим ключом (с разными условиями); нет — пустой список.</summary>
        public IReadOnlyList<FeedTemplate> FeedTemplates(string key) =>
            key != null && feedByKey.TryGetValue(key, out List<FeedTemplate> list) ? list : NoTemplates;

        public bool HasFeedKey(string key) => key != null && feedByKey.ContainsKey(key);

        private void Add(GameConfig config, Definition definition)
        {
            if (definition == null) throw new ArgumentException($"{config.name}: empty reference in a definition list");
            if (string.IsNullOrEmpty(definition.Id)) throw new ArgumentException($"{config.name}: {definition.name} has no id");

            Type type = definition.GetType();
            if (!byId.TryGetValue(type, out Dictionary<string, Definition> map))
            {
                map = new Dictionary<string, Definition>(StringComparer.Ordinal);
                byId.Add(type, map);
                byType.Add(type, new List<Definition>());
            }
            if (map.TryGetValue(definition.Id, out Definition existing))
                throw new ArgumentException($"{config.name}: duplicate {type.Name} id '{definition.Id}' ({existing.name}, {definition.name})");

            map.Add(definition.Id, definition);
            byType[type].Add(definition);
        }
    }
}
