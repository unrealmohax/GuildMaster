using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GuildMaster.Core;
using GuildMaster.Data;
using GuildMaster.Debugging;
using UnityEditor;
using UnityEngine;

namespace GuildMaster.ClaudeSandbox
{
    /// <summary>
    /// Песочница. Группы за год на 10 зёрнах (бот «Простой»): доля заданий соло и в группе, средний размер, закрывают ли группы
    /// «дыры» профиля лучше случайного набора того же размера из свободных людей нужного ранга, размер групп у жадных, командных
    /// и одиночек (кто собирал), доля соло по полюсам оси Люди, постоянные группы (сложилось, распалось, почему уходили), отказы
    /// по причинам, гибели, шанс раунда, выполнено, время года. Итог — в консоль.
    /// </summary>
    public static class PartyProbe
    {
        private sealed class Stats
        {
            public int Solo, Group, GroupSize, Done, Returned, Deaths, Rounds, Seeds;
            public double RoundChance, Seconds;
            public int Gathered, HolesWins, HolesTies;
            public double HolesMine, HolesRandom, HolesSolo;
            public int Formed, Disbanded, PermanentQuests;
            public readonly Dictionary<string, int> Left = new Dictionary<string, int>();
            public readonly Dictionary<string, int> Declined = new Dictionary<string, int>();
            public readonly Dictionary<string, (int Count, int Size)> ByInitiator = new Dictionary<string, (int, int)>();
            public readonly Dictionary<string, (int Solo, int Group)> ByPole = new Dictionary<string, (int, int)>();
        }

        [MenuItem("GuildMaster/Sandbox/Parties/Groups 10 Years")]
        private static void Groups()
        {
            GameConfig config = EditorAssets.FindGameConfig();
            if (config == null)
            {
                Debug.Log("[PartyProbe] no GameConfig");
                return;
            }

            DataRegistry data = DataRegistry.FromConfig(config);
            Stats stats = Collect(data);
            WithoutParties(config, solo =>
            {
                Stats baseline = Collect(solo);
                Debug.Log("[PartyProbe] БЕЗ ГРУПП (предел группы 1): " + Report(baseline).Split((char)10)[1] + " | " + Report(baseline).Split((char)10)[2]);
            });
            foreach (string line in Report(stats).Split((char)10)) if (line.Length > 0) Debug.Log("[PartyProbe] " + line);
        }

        /// <summary>Сколько каких строк Trace за год (зерно 1): цена оценки групп.</summary>
        [MenuItem("GuildMaster/Sandbox/Parties/Trace Counts Seed 1")]
        private static void TraceCounts()
        {
            DataRegistry data = DataRegistry.FromConfig(EditorAssets.FindGameConfig());
            var writer = new CountingWriter();
            HeadlessRun.Result result = HeadlessRun.Run(data, 1u, 360, PlayerBots.Simple(), new SimLogger(SimLogLevel.Trace, writer));
            foreach (var pair in writer.Counts.OrderByDescending(p => p.Value).Take(15)) Debug.Log($"[PartyProbe] {pair.Key}: {pair.Value}");
            Debug.Log($"[PartyProbe] lines {result.LogLines}, {result.ElapsedSeconds:0.00} s");
        }

        /// <summary>Цена отдельных расчётов оценки групп: перекрытие, веса мотивов, синергии, ценность группы — на стартовых людях зерна 1.</summary>
        [MenuItem("GuildMaster/Sandbox/Parties/Cost Benchmark")]
        private static void Benchmark()
        {
            DataRegistry data = DataRegistry.FromConfig(EditorAssets.FindGameConfig());
            Simulation simulation = Simulation.CreateDefault(data, 1u);
            List<Adventurer> people = simulation.World.Adventurers.Active.ToList();
            Order order = simulation.World.Orders.Open[0];
            RelationBook relations = simulation.World.Relations;
            var group = people.Take(3).ToList();
            float[] profile = QuestMath.GroupProfile(group, data);
            var lens = new PartyLens(people[0], order, false, 0.2f, relations, data);
            const int n = 100000;
            float sink = 0f;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < n; i++) sink += QuestMath.RealOverlap(group, order, data);
            Debug.Log($"[PartyProbe] RealOverlap (профиль + перекрытие): {watch.Elapsed.TotalMilliseconds * 1000 / n:0.00} мкс");
            watch.Restart();
            for (int i = 0; i < n; i++) sink += lens.Overlap(group);
            Debug.Log($"[PartyProbe] lens.Overlap: {watch.Elapsed.TotalMilliseconds * 1000 / n:0.00} мкс");
            watch.Restart();
            for (int i = 0; i < n; i++) sink += Motives.Weigh(people[i % people.Count], data, false)[Motive.Money];
            Debug.Log($"[PartyProbe] Motives.Weigh: {watch.Elapsed.TotalMilliseconds * 1000 / n:0.00} мкс");
            watch.Restart();
            for (int i = 0; i < n; i++) sink += QuestMath.Synergy(group, relations, data);
            Debug.Log($"[PartyProbe] Synergy (3 чел.): {watch.Elapsed.TotalMilliseconds * 1000 / n:0.00} мкс");
            watch.Restart();
            for (int i = 0; i < n; i++) sink += lens.Value(group);
            Debug.Log($"[PartyProbe] lens.Value (3 чел.): {watch.Elapsed.TotalMilliseconds * 1000 / n:0.00} мкс");
            watch.Restart();
            for (int i = 0; i < n / 10; i++) sink += new PartyLens(people[i % people.Count], order, false, 0.2f, relations, data).Overlap(group);
            Debug.Log($"[PartyProbe] new PartyLens + Overlap: {watch.Elapsed.TotalMilliseconds * 1000 / (n / 10):0.00} мкс ({sink:0})");
        }

        /// <summary>Время года (зерно 1, 3 прогона, лучший) с группами и без них (копия данных, предел группы 1).</summary>
        [MenuItem("GuildMaster/Sandbox/Parties/Year Time With And Without Parties")]
        private static void YearTime()
        {
            GameConfig config = EditorAssets.FindGameConfig();
            DataRegistry normal = DataRegistry.FromConfig(config);
            double Best(DataRegistry data)
            {
                double best = double.MaxValue;
                for (int i = 0; i < 3; i++) best = Math.Min(best, HeadlessRun.Run(data, 1u, 360, PlayerBots.Simple()).ElapsedSeconds);
                return best;
            }
            double with = Best(normal);
            WithoutParties(config, solo => Debug.Log($"[PartyProbe] год: с группами {with:0.00} с, без групп {Best(solo):0.00} с"));
        }

        /// <summary>Считает строки лога по первому слову текста (после меток времени, системы и уровня).</summary>
        private sealed class CountingWriter : System.IO.TextWriter
        {
            private readonly StringBuilder line = new StringBuilder();
            public readonly Dictionary<string, int> Counts = new Dictionary<string, int>();
            public override Encoding Encoding => Encoding.UTF8;

            public override void Write(char value)
            {
                if (value != (char)10)
                {
                    line.Append(value);
                    return;
                }
                string text = line.ToString();
                line.Clear();
                int level = text.IndexOf("] ", text.IndexOf("] ", text.IndexOf("] ", StringComparison.Ordinal) + 2, StringComparison.Ordinal) + 2, StringComparison.Ordinal);
                string rest = level >= 0 ? text.Substring(level + 2) : text;
                int space = rest.IndexOf(' ');
                Add(Counts, space > 0 ? rest.Substring(0, space) : rest);
            }
        }

        private static Stats Collect(DataRegistry data)
        {
            var stats = new Stats();
            for (uint seed = 1; seed <= 10; seed++)
            {
                var random = new System.Random((int)seed);
                HeadlessRun.Result result = HeadlessRun.Run(data, seed, 360, PlayerBots.Simple(), null,
                    simulation => simulation.TickCompleted += events => Observe(simulation, data, events, stats, random));
                stats.Seconds += result.ElapsedSeconds;
                stats.Seeds++;
            }
            return stats;
        }

        /// <summary>Реестр из копии данных, где в группе не больше одного человека: игра без групп.</summary>
        private static void WithoutParties(GameConfig config, Action<DataRegistry> action)
        {
            GameConfig copy = UnityEngine.Object.Instantiate(config);
            BalanceSettings balance = UnityEngine.Object.Instantiate(config.Balance);
            try
            {
                var balanceObject = new SerializedObject(balance);
                balanceObject.FindProperty("rounds.maxPartySize").intValue = 1;
                balanceObject.ApplyModifiedPropertiesWithoutUndo();
                var configObject = new SerializedObject(copy);
                configObject.FindProperty("balance").objectReferenceValue = balance;
                configObject.ApplyModifiedPropertiesWithoutUndo();
                action(DataRegistry.FromConfig(copy));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(copy);
                UnityEngine.Object.DestroyImmediate(balance);
            }
        }

        private static void Observe(Simulation simulation, DataRegistry data, IReadOnlyList<SimEvent> events, Stats stats, System.Random random)
        {
            WorldState world = simulation.World;
            foreach (SimEvent e in events)
            {
                switch (e.Type)
                {
                    case SimEventType.QuestReturned:
                        if (e.TryGet("promotion", out bool promotion) && promotion) break;
                        e.TryGet("total", out int total);
                        if (total <= 1) stats.Solo++;
                        else
                        {
                            stats.Group++;
                            stats.GroupSize += total;
                        }
                        stats.Returned++;
                        if (e.TryGet("done", out bool done) && done) stats.Done++;
                        break;
                    case SimEventType.AdventurerDied:
                        stats.Deaths++;
                        break;
                    case SimEventType.RoundSuccess:
                    case SimEventType.RoundFail:
                        if (e.TryGet("chance", out float chance))
                        {
                            stats.RoundChance += chance;
                            stats.Rounds++;
                        }
                        break;
                    case SimEventType.QuestDeparted:
                        ObserveDeparture(world, e, stats);
                        break;
                    case SimEventType.PartyGathered:
                        ObserveGathered(world, data, e, stats, random);
                        break;
                    case SimEventType.PermanentPartyFormed:
                        stats.Formed++;
                        break;
                    case SimEventType.PermanentPartyDisbanded:
                        stats.Disbanded++;
                        break;
                    case SimEventType.PartyMemberLeft:
                        e.TryGet("cause", out string cause);
                        Add(stats.Left, cause ?? "?");
                        break;
                    case SimEventType.InvitationDeclined:
                        e.TryGet("cause", out string refusal);
                        Add(stats.Declined, refusal ?? "?");
                        break;
                }
            }
        }

        private static void ObserveDeparture(WorldState world, SimEvent e, Stats stats)
        {
            if (!e.TryGet("order", out int orderId) || !world.Orders.TryGetOrder(orderId, out Order order) || order.IsPromotion) return;
            bool solo = e.Participants.Count == 1;
            foreach (int id in e.Participants)
            {
                if (!world.Adventurers.TryGetActive(id, out Adventurer person)) continue;
                string pole = PeoplePole(person);
                stats.ByPole.TryGetValue(pole, out (int Solo, int Group) counts);
                stats.ByPole[pole] = solo ? (counts.Solo + 1, counts.Group) : (counts.Solo, counts.Group + 1);
            }
        }

        private static void ObserveGathered(WorldState world, DataRegistry data, SimEvent e, Stats stats, System.Random random)
        {
            if (e.TryGet("permanent", out bool permanent) && permanent)
            {
                stats.PermanentQuests++;
                return;
            }
            if (!e.TryGet("order", out int orderId) || !world.Orders.TryGetOrder(orderId, out Order order)) return;
            List<Adventurer> group = e.Participants.Select(id => world.Adventurers.TryGetActive(id, out Adventurer a) ? a : null).Where(a => a != null).ToList();
            if (group.Count < 2) return;

            Adventurer initiator = group[0];
            string kind = Kind(initiator);
            stats.ByInitiator.TryGetValue(kind, out (int Count, int Size) byKind);
            stats.ByInitiator[kind] = (byKind.Count + 1, byKind.Size + group.Count);

            // Случайный набор того же размера: инициатор и свободные люди нужного ранга (не на задании, могут брать задания).
            List<Adventurer> pool = world.Adventurers.Active.Where(a => a.Id != initiator.Id && !a.State.IsOnQuest() && a.GuildRank >= order.Rank
                && StateRules.CanTakeQuests(a.State, data.Balance.State)).ToList();
            if (pool.Count < group.Count - 1) return;

            float mine = QuestMath.RealOverlap(group, order, data);
            double others = 0;
            const int draws = 30;
            for (int r = 0; r < draws; r++)
            {
                var other = new List<Adventurer> { initiator };
                other.AddRange(pool.OrderBy(_ => random.Next()).Take(group.Count - 1));
                others += QuestMath.RealOverlap(other, order, data);
            }
            others /= draws;
            stats.Gathered++;
            stats.HolesMine += mine;
            stats.HolesRandom += others;
            stats.HolesSolo += QuestMath.RealSoloOverlap(initiator, order, data);
            if (Math.Abs(mine - others) < 1e-3) stats.HolesTies++;
            else if (mine > others) stats.HolesWins++;
        }

        private static string Kind(Adventurer person)
        {
            float money = person.GetAxis(AxisId.Money);
            float people = person.GetAxis(AxisId.People);
            if (money >= 30f) return "жадный";
            if (people >= 30f) return "командный";
            if (people <= -30f) return "одиночка";
            return "прочие";
        }

        private static string PeoplePole(Adventurer person)
        {
            float people = person.GetAxis(AxisId.People);
            return people <= -30f ? "одиночки" : people >= 30f ? "командные" : "Люди нейтр.";
        }

        private static void Add(Dictionary<string, int> counts, string key) => counts[key] = counts.TryGetValue(key, out int n) ? n + 1 : 1;

        private static string Report(Stats s)
        {
            int quests = s.Solo + s.Group;
            var text = new StringBuilder($"{s.Seeds} лет, {s.Seconds / s.Seeds:0.00} с на год\n");
            text.AppendLine($"Заданий: {quests / (float)s.Seeds:0} в год; соло {s.Solo / (float)s.Seeds:0} ({100f * s.Solo / Math.Max(1, quests):0}%), " +
                            $"в группе {s.Group / (float)s.Seeds:0}; средний размер группы {s.GroupSize / (float)Math.Max(1, s.Group):0.00}");
            text.AppendLine($"Выполнено {100f * s.Done / Math.Max(1, s.Returned):0.0}%, гибелей {s.Deaths / (float)s.Seeds:0.0} в год, " +
                            $"шанс раунда {s.RoundChance / Math.Max(1, s.Rounds):0.000}");
            text.AppendLine($"Дыры профиля (группы под задание, {s.Gathered}): группа {s.HolesMine / Math.Max(1, s.Gathered):0.000}, " +
                            $"случайный набор {s.HolesRandom / Math.Max(1, s.Gathered):0.000}, инициатор один {s.HolesSolo / Math.Max(1, s.Gathered):0.000}; " +
                            $"группа лучше случайной в {100f * s.HolesWins / Math.Max(1, s.Gathered):0}%, хуже в " +
                            $"{100f * (s.Gathered - s.HolesWins - s.HolesTies) / Math.Max(1, s.Gathered):0}%, поровну (обе закрывают всё) {100f * s.HolesTies / Math.Max(1, s.Gathered):0}%");
            text.Append("Размер группы по тому, кто собрал:");
            foreach (var pair in s.ByInitiator.OrderBy(p => p.Key))
                text.Append($" {pair.Key} {pair.Value.Size / (float)Math.Max(1, pair.Value.Count):0.00} ({pair.Value.Count})");
            text.AppendLine();
            text.Append("Доля выходов соло:");
            foreach (var pair in s.ByPole.OrderBy(p => p.Key))
                text.Append($" {pair.Key} {100f * pair.Value.Solo / Math.Max(1, pair.Value.Solo + pair.Value.Group):0}% ({pair.Value.Solo + pair.Value.Group})");
            text.AppendLine();
            text.AppendLine($"Постоянные группы: сложилось {s.Formed}, распалось {s.Disbanded}, заданий постоянной группой {s.PermanentQuests}; " +
                            "ушли: " + string.Join(", ", s.Left.OrderBy(p => p.Key).Select(p => $"{p.Key} {p.Value}")));
            text.AppendLine("Отказы от приглашений: " + string.Join(", ", s.Declined.OrderByDescending(p => p.Value).Select(p => $"{p.Key} {p.Value}")));
            return text.ToString();
        }
    }
}
