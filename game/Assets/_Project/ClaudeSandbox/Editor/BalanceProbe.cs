using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using GuildMaster.Core;
using GuildMaster.Data;
using GuildMaster.Debugging;
using UnityEditor;
using UnityEngine;

namespace GuildMaster.ClaudeSandbox
{
    /// <summary>
    /// Песочница. Замер баланса: серия прогонов по 10 лет ботом «Простой» без лога. На каждое зерно — помесячная сводка
    /// (<c>summary_{зерно}.csv</c>) и годовая (<c>yearly_{зерно}.csv</c>: ранги людей, сила, заказы и задания по рангам, гибели);
    /// на серию — свёртка сводок. Файлы — в <c>Logs/Balance/{метка}</c>, метка — из <c>Logs/Balance/label.txt</c> (или время запуска).
    /// </summary>
    public static class BalanceProbe
    {
        private const string Menu = "GuildMaster/Sandbox/Balance/";
        private const int Days = 3600;
        private static readonly GuildRank[] Ranks = { GuildRank.G, GuildRank.F, GuildRank.E, GuildRank.D, GuildRank.C };

        [MenuItem(Menu + "10 Years Seeds 1-3")]
        private static void Three() => Run(3);

        [MenuItem(Menu + "10 Years Seeds 1-3 Passive Bot")]
        private static void ThreePassive() => Run(3, PlayerBots.Passive);

        [MenuItem(Menu + "10 Years Seeds 1-3 Reckless Bot")]
        private static void ThreeReckless() => Run(3, PlayerBots.Reckless);

        [MenuItem(Menu + "10 Years Seeds 1-5")]
        private static void Five() => Run(5);

        [MenuItem(Menu + "10 Years Seeds 1-5 Passive Bot")]
        private static void FivePassive() => Run(5, PlayerBots.Passive);

        private sealed class Year
        {
            public readonly int[] PeopleByRank = new int[5];
            public readonly int[] OrdersByRank = new int[5];
            public readonly int[] QuestsByRank = new int[5];
            public readonly int[] DoneByRank = new int[5];
            public readonly int[] DeathsByRank = new int[5];
            public readonly double[] RewardByRank = new double[5];
            public int People, Joined, Left, Died, Promotions;
            public double Power, TopPower, Treasury, Reputation;
            public double Security, MinSecurity = double.MaxValue;
            public int Population, WorstLevel, Attacks, ThreatsDone, ThreatsMissed;
            public int Quests, Done, Brilliant, SoloQuests, SoloDone, GroupQuests, GroupDone, GroupMembers, PermanentQuests, PermanentDone;
            public int Rounds, CappedRounds, ThreatQuests, ThreatQuestsDone;
        }

        private static void Run(int runs, Func<PlayerBot> bot = null)
        {
            GameConfig config = EditorAssets.FindGameConfig();
            if (config == null)
            {
                Debug.Log("[BalanceProbe] no GameConfig");
                return;
            }

            string root = Path.Combine(LogFiles.DefaultFolder, "Balance");
            Directory.CreateDirectory(root);
            string labelFile = Path.Combine(root, "label.txt");
            string label = File.Exists(labelFile) ? File.ReadAllText(labelFile).Trim() : LogFiles.Stamp(DateTime.Now);
            string folder = Path.Combine(root, label);
            Directory.CreateDirectory(folder);

            List<Action> restore = ApplyOverrides(config, Path.Combine(root, "overrides.txt"), out string applied);
            try
            {
                RunSeries(config, runs, folder, applied, bot ?? PlayerBots.Simple);
            }
            finally
            {
                foreach (Action undo in restore) undo();
            }
        }

        /// <summary>Записать переопределения в ассеты насовсем (и сохранить).</summary>
        [MenuItem(Menu + "Apply Overrides To Assets")]
        private static void ApplyToAssets()
        {
            GameConfig config = EditorAssets.FindGameConfig();
            string file = Path.Combine(LogFiles.DefaultFolder, "Balance", "overrides.txt");
            ApplyOverrides(config, file, out string applied, persist: true);
            EditorUtility.SetDirty(config.Balance); // новые поля разделов — в файл, даже если значения не менялись
            AssetDatabase.SaveAssets();
            Debug.Log("[BalanceProbe] applied to assets:\n" + applied);
        }

        /// <summary>
        /// Переопределения из файла: строка «balance путь значение» или «IdОпределения путь значение» (путь — как у SerializedProperty).
        /// Без <paramref name="persist"/> — только в памяти, возвращает действия, которые вернут старые значения.
        /// </summary>
        private static List<Action> ApplyOverrides(GameConfig config, string file, out string applied, bool persist = false)
        {
            var restore = new List<Action>();
            var log = new StringBuilder();
            applied = string.Empty;
            if (!File.Exists(file)) return restore;
            foreach (string raw in File.ReadAllLines(file))
            {
                string line = raw.Trim();
                int hash = line.IndexOf('#');
                if (hash >= 0) line = line.Substring(0, hash).Trim();
                if (line.Length == 0) continue;
                string[] parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 3) throw new FormatException("Bad override: " + raw);

                UnityEngine.Object target = parts[0] == "balance" ? (UnityEngine.Object)config.Balance : null;
                if (target == null)
                    foreach (Definition definition in config.AllDefinitions())
                        if (definition.Id == parts[0]) target = definition;
                if (target == null) throw new ArgumentException("No target: " + parts[0]);

                var so = new SerializedObject(target);
                SerializedProperty property = so.FindProperty(parts[1]);
                if (property == null) throw new ArgumentException("No property: " + line);
                string old = Set(property, parts[2]);
                if (persist) so.ApplyModifiedProperties();
                else so.ApplyModifiedPropertiesWithoutUndo();
                log.Append(parts[0]).Append(' ').Append(parts[1]).Append(": ").Append(old).Append(" -> ").Append(parts[2]).Append('\n');
                if (persist) continue;
                string path = parts[1];
                restore.Insert(0, () =>
                {
                    var back = new SerializedObject(target);
                    Set(back.FindProperty(path), old);
                    back.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.ClearDirty(target);
                });
            }
            applied = log.ToString();
            return restore;
        }

        private static string Set(SerializedProperty property, string value)
        {
            CultureInfo inv = CultureInfo.InvariantCulture;
            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer:
                    string oldInt = property.intValue.ToString(inv);
                    property.intValue = int.Parse(value, inv);
                    return oldInt;
                case SerializedPropertyType.Float:
                    string oldFloat = property.floatValue.ToString("R", inv);
                    property.floatValue = float.Parse(value, inv);
                    return oldFloat;
                case SerializedPropertyType.Boolean:
                    string oldBool = property.boolValue ? "true" : "false";
                    property.boolValue = value == "true";
                    return oldBool;
                default:
                    throw new ArgumentException($"Unsupported {property.propertyType}: {property.propertyPath}");
            }
        }

        private static void RunSeries(GameConfig config, int runs, string folder, string applied, Func<PlayerBot> bot)
        {
            File.WriteAllText(Path.Combine(folder, "overrides_applied.txt"), applied);
            DataRegistry data = DataRegistry.FromConfig(config);
            var summaries = new List<RunSummary>();
            var report = new StringBuilder();
            double seconds = 0;
            for (int i = 0; i < runs; i++)
            {
                uint seed = (uint)(1 + i);
                var years = new List<Year>();
                Year current = null;
                BudgetTracker budget = null;
                HeadlessRun.Result result = HeadlessRun.Run(data, seed, Days, bot(), null, simulation =>
                {
                    budget = new BudgetTracker(simulation, Days / 360);
                    current = new Year();
                    simulation.TickCompleted += events =>
                    {
                        foreach (SimEvent e in events) Count(current, e, simulation);
                        WorldState world = simulation.World;
                        current.MinSecurity = Math.Min(current.MinSecurity, world.Settlement.Security);
                        current.WorstLevel = Math.Max(current.WorstLevel, (int)world.Settlement.Level);
                        if (world.Time.TotalHours % (simulation.Calendar.HoursPerDay * 360) != 0) return;
                        Close(current, world);
                        years.Add(current);
                        current = new Year();
                    };
                });
                seconds += result.ElapsedSeconds;
                summaries.Add(result.Summary);
                File.WriteAllText(Path.Combine(folder, LogFiles.SummaryName(seed)), SummaryCsv.Write(result.Summary), new UTF8Encoding(true));
                File.WriteAllText(Path.Combine(folder, $"yearly_{seed}.csv"), Yearly(years), new UTF8Encoding(true));
                File.WriteAllText(Path.Combine(folder, $"budget_{seed}.csv"), budget.ToCsv(), new UTF8Encoding(true));
                WorldState end = result.Simulation.World;
                if (current != null && result.GuildClosed)
                {
                    Close(current, end);
                    years.Add(current);
                }
                string closed = result.GuildClosed ? $"{end.Treasury.ClosedCause} at {end.Time}" : "no";
                report.Append($"seed {seed}: {result.ElapsedSeconds:0.0} s, closed={closed}, years={years.Count}, security={end.Settlement.Security:0.#}, "
                    + $"population={end.Settlement.Population}, attacks={end.Settlement.Totals.Attacks}, threats done/missed="
                    + $"{end.Settlement.Totals.ThreatsDone}/{end.Settlement.Totals.ThreatsMissed}\n");
            }

            if (summaries.Count > 1)
                File.WriteAllText(Path.Combine(folder, LogFiles.AggregateName(1, summaries.Count)), SummaryCsv.Write(SummaryAggregate.Of(summaries)),
                    new UTF8Encoding(true));
            File.WriteAllText(Path.Combine(folder, "done.txt"), report.ToString());
            Debug.Log($"[BalanceProbe] {runs} run(s) x {Days} days: {seconds:0.0} s -> {folder}\n{report}");
        }

        private static void Count(Year year, SimEvent e, ISimulationClient game)
        {
            WorldState world = game.World;
            switch (e.Type)
            {
                case SimEventType.OrderArrived:
                    if (e.TryGet("rank", out GuildRank arrived)) year.OrdersByRank[(int)arrived]++;
                    break;
                case SimEventType.QuestReturned:
                    if ((e.TryGet("promotion", out bool promotion) && promotion) || !e.TryGet("rank", out GuildRank rank)) break;
                    year.QuestsByRank[(int)rank]++;
                    bool isDone = e.TryGet("done", out bool done) && done;
                    if (isDone) year.DoneByRank[(int)rank]++;
                    CountParty(year, e, world, isDone);
                    if (e.TryGet("questType", out string questType) && SettlementRules.IsThreat(game.Data, questType))
                    {
                        year.ThreatQuests++;
                        if (isDone) year.ThreatQuestsDone++;
                    }
                    break;
                case SimEventType.RoundSuccess:
                case SimEventType.RoundFail:
                    year.Rounds++;
                    if (e.TryGet("chance", out float chance) && chance >= game.Data.Balance.Rounds.MaxRoundChance - 1e-5f) year.CappedRounds++;
                    break;
                case SimEventType.OrderTaken:
                    if (e.TryGet("rank", out GuildRank taken) && e.TryGet("reward", out int reward)) year.RewardByRank[(int)taken] += reward;
                    break;
                case SimEventType.AdventurerDied:
                    year.Died++;
                    break;
                case SimEventType.AdventurerJoined:
                    year.Joined++;
                    break;
                case SimEventType.AdventurerLeft:
                    year.Left++;
                    break;
                case SimEventType.RankPromoted:
                    year.Promotions++;
                    break;
                case SimEventType.SettlementAttacked:
                    year.Attacks++;
                    break;
                case SimEventType.SecurityChanged:
                    e.TryGet("reason", out string reason);
                    if (reason == SettlementReasons.ThreatDone) year.ThreatsDone++;
                    else if (reason == SettlementReasons.Expired || reason == SettlementReasons.DeclinedByRegistrar
                        || reason == SettlementReasons.DeclinedByPlayer || reason == SettlementReasons.NoAnswer) year.ThreatsMissed++;
                    break;
            }
        }

        /// <summary>Задание (не экзамен) по составу: соло, группа (размер), постоянная группа; выполнено и «блестяще».</summary>
        private static void CountParty(Year year, SimEvent e, WorldState world, bool done)
        {
            year.Quests++;
            if (done) year.Done++;
            if (e.TryGet("kind", out string kind) && kind == "brilliant") year.Brilliant++;
            int total = e.TryGet("total", out int departed) ? departed : 0;
            if (total <= 1)
            {
                year.SoloQuests++;
                if (done) year.SoloDone++;
                return;
            }
            year.GroupQuests++;
            year.GroupMembers += total;
            if (done) year.GroupDone++;
            if (e.TryGet("quest", out int questId) && world.Quests.TryGetRun(questId, out QuestRun run) && run.IsPermanentParty)
            {
                year.PermanentQuests++;
                if (done) year.PermanentDone++;
            }
        }

        private static void Close(Year year, WorldState world)
        {
            IReadOnlyList<Adventurer> active = world.Adventurers.Active;
            year.People = active.Count;
            double sum = 0, top = 0;
            foreach (Adventurer a in active)
            {
                year.PeopleByRank[(int)a.GuildRank]++;
                sum += a.PowerScore;
                top = Math.Max(top, a.PowerScore);
            }
            year.Power = active.Count > 0 ? sum / active.Count : 0;
            year.TopPower = top;
            year.Treasury = world.Treasury.Money;
            year.Reputation = world.Guild.Reputation;
            year.Security = world.Settlement.Security;
            year.Population = world.Settlement.Population;
        }

        private static string Yearly(List<Year> years)
        {
            var text = new StringBuilder();
            text.Append("Год,Людей,Пришло,Ушло,Погибло,Повышений,Сила,Сила max,Казна,Репутация,"
                + "Безопасность,Безопасность min,Худший уровень,Население,Нападений,Угроз выполнено,Угроз упущено,"
                + "Заданий,Выполнено всего,Блестяще,Соло,Соло выполнено,В группе,В группе выполнено,Людей в группах,Постоянной группой,Постоянной выполнено,"
                + "Раундов,Раундов на потолке,Заданий-угроз,Угроз выполнено заданий");
            foreach (string prefix in new[] { "Люди ", "Заказы ", "Задания ", "Выполнено ", "Награда ср. " })
                foreach (GuildRank rank in Ranks) text.Append(',').Append(prefix).Append(rank);
            text.Append('\n');
            CultureInfo inv = CultureInfo.InvariantCulture;
            for (int y = 0; y < years.Count; y++)
            {
                Year r = years[y];
                text.Append(string.Format(inv, "{0},{1},{2},{3},{4},{5},{6:0.#},{7:0.#},{8:0},{9:0.#}", y + 1, r.People, r.Joined, r.Left, r.Died, r.Promotions,
                    r.Power, r.TopPower, r.Treasury, r.Reputation));
                text.Append(string.Format(inv, ",{0:0.#},{1:0.#},{2},{3},{4},{5},{6}", r.Security, r.MinSecurity, r.WorstLevel, r.Population, r.Attacks,
                    r.ThreatsDone, r.ThreatsMissed));
                text.Append(string.Format(inv, ",{0},{1},{2},{3},{4},{5},{6},{7},{8},{9}", r.Quests, r.Done, r.Brilliant, r.SoloQuests, r.SoloDone,
                    r.GroupQuests, r.GroupDone, r.GroupMembers, r.PermanentQuests, r.PermanentDone));
                text.Append(string.Format(inv, ",{0},{1},{2},{3}", r.Rounds, r.CappedRounds, r.ThreatQuests, r.ThreatQuestsDone));
                foreach (int v in r.PeopleByRank) text.Append(',').Append(v);
                foreach (int v in r.OrdersByRank) text.Append(',').Append(v);
                foreach (int v in r.QuestsByRank) text.Append(',').Append(v);
                foreach (int v in r.DoneByRank) text.Append(',').Append(v);
                for (int k = 0; k < 5; k++) text.Append(',').Append((r.QuestsByRank[k] > 0 ? r.RewardByRank[k] / Math.Max(1, r.QuestsByRank[k]) : 0).ToString("0", inv));
                text.Append('\n');
            }
            return text.ToString();
        }
    }
}
