using System.Collections.Generic;
using System.Globalization;
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
    /// Песочница. Доходы и траты людей в неделю: каждая система такта обёрнута замером кошельков до и после (имя системы
    /// то же — поток случайности и мир не меняются). Изменение относится к статье по системе и часу: задания (+ награды
    /// и добыча, − пайки), состояние (00:00 — жильё, иначе еда), занятия (выпивка, Лазарет, двор — по занятию), семья,
    /// обращения (+ займы и помощь, − штрафы). Сумма — на человеко-неделю (люди в гильдии × часы / 168), всего, по рангам
    /// и по кварталам. Год, бот «Простой», зёрна 1–10. Вывод — в консоль.
    /// </summary>
    public static class IncomeProbe
    {
        private const int Seeds = 10;
        private const int Days = 360;

        [MenuItem("GuildMaster/Sandbox/Headless/Income And Spending Per Week")]
        private static void Run()
        {
            GameConfig config = EditorAssets.FindGameConfig();
            if (config == null)
            {
                Debug.Log("[IncomeProbe] no GameConfig");
                return;
            }
            DataRegistry data = DataRegistry.FromConfig(config);

            var total = new Book();
            var byRank = new SortedDictionary<GuildRank, Book>();
            var byQuarter = new SortedDictionary<int, Book>();
            for (uint seed = 1; seed <= Seeds; seed++)
            {
                var tap = new Tap();
                List<ISimSystem> systems = SimulationSystems.CreateDefault().Select(s => (ISimSystem)new TapSystem(s, tap)).ToList();
                var simulation = new Simulation(data, seed, systems);
                tap.Simulation = simulation;
                PlayerBot bot = PlayerBots.Simple();
                // Конструктор хода бота закрыт сборкой отладки — песочница создаёт его отражением.
                var turn = (BotTurn)System.Activator.CreateInstance(typeof(BotTurn),
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, null,
                    new object[] { simulation, (System.Action<ICommand>)simulation.Send }, CultureInfo.InvariantCulture);
                System.Reflection.PropertyInfo tickProperty = typeof(BotTurn).GetProperty(nameof(BotTurn.Tick));
                long ticks = simulation.Calendar.DaysToHours(Days);
                for (long i = 0; i < ticks && !simulation.IsFinished; i++)
                {
                    tickProperty.SetValue(turn, simulation.TicksDone);
                    bot.Act(turn);
                    simulation.Tick();
                    int quarter = (simulation.World.Time.Month - 1) / 3 + 1;
                    foreach (Adventurer a in simulation.World.Adventurers.Active)
                    {
                        total.Hours++;
                        Get(byRank, a.GuildRank).Hours++;
                        Get(byQuarter, quarter).Hours++;
                    }
                }
                foreach ((GuildRank rank, string item, int amount, int quarter) in tap.Entries)
                {
                    total.Add(item, amount);
                    Get(byRank, rank).Add(item, amount);
                    Get(byQuarter, quarter).Add(item, amount);
                }
            }

            var text = new StringBuilder("[IncomeProbe] на человека в неделю, ").Append(Seeds).Append(" зёрен × ").Append(Days).Append(" дней\n");
            Print(text, "Все", total);
            foreach (KeyValuePair<GuildRank, Book> pair in byRank) Print(text, "Ранг " + pair.Key, pair.Value);
            foreach (KeyValuePair<int, Book> pair in byQuarter) Print(text, "Квартал " + pair.Key, pair.Value);
            Debug.Log(text.ToString());
        }

        private static Book Get<T>(SortedDictionary<T, Book> books, T key)
        {
            if (!books.TryGetValue(key, out Book book)) books[key] = book = new Book();
            return book;
        }

        private static void Print(StringBuilder text, string title, Book book)
        {
            double weeks = book.Hours / 168.0;
            if (weeks <= 0) return;
            text.Append("\n== ").Append(title).Append(": человеко-недель ").Append(weeks.ToString("0", CultureInfo.InvariantCulture)).Append('\n');
            double income = 0, spend = 0;
            foreach (KeyValuePair<string, long> pair in book.Items.OrderByDescending(p => p.Value))
            {
                double perWeek = pair.Value / weeks;
                if (pair.Value > 0) income += perWeek;
                else spend += perWeek;
            }
            foreach (KeyValuePair<string, long> pair in book.Items.Where(p => p.Value > 0).OrderByDescending(p => p.Value))
                text.Append("  + ").Append(pair.Key).Append(": ").Append((pair.Value / weeks).ToString("0.0", CultureInfo.InvariantCulture)).Append('\n');
            text.Append("  = доход ").Append(income.ToString("0.0", CultureInfo.InvariantCulture)).Append('\n');
            foreach (KeyValuePair<string, long> pair in book.Items.Where(p => p.Value < 0).OrderBy(p => p.Value))
                text.Append("  − ").Append(pair.Key).Append(": ").Append((-pair.Value / weeks).ToString("0.0", CultureInfo.InvariantCulture)).Append('\n');
            text.Append("  = траты ").Append((-spend).ToString("0.0", CultureInfo.InvariantCulture))
                .Append(", остаток ").Append((income + spend).ToString("0.0", CultureInfo.InvariantCulture)).Append('\n');
        }

        private sealed class Book
        {
            public long Hours;
            public readonly Dictionary<string, long> Items = new Dictionary<string, long>();

            public void Add(string item, int amount)
            {
                Items.TryGetValue(item, out long sum);
                Items[item] = sum + amount;
            }
        }

        private sealed class Tap
        {
            public Simulation Simulation;
            public readonly List<(GuildRank, string, int, int)> Entries = new List<(GuildRank, string, int, int)>();
            private readonly Dictionary<int, int> before = new Dictionary<int, int>();

            public void Before(WorldState world)
            {
                before.Clear();
                foreach (Adventurer a in world.Adventurers.Active) before[a.Id] = a.State.Wallet;
            }

            public void After(WorldState world, string system)
            {
                int quarter = (world.Time.Month - 1) / 3 + 1;
                foreach (Adventurer a in world.Adventurers.Active.Concat(world.Adventurers.Archive))
                {
                    if (!before.TryGetValue(a.Id, out int was)) continue;
                    int delta = a.State.Wallet - was;
                    if (delta != 0) Entries.Add((a.GuildRank, Item(system, delta, world.Time.Hour, a), delta, quarter));
                }
            }

            private static string Item(string system, int delta, int hour, Adventurer a)
            {
                switch (system)
                {
                    case nameof(QuestSystem): return delta > 0 ? "награды и добыча (после комиссии и долга)" : "пайки";
                    case nameof(StateSystem): return delta > 0 ? "прочее (состояние)" : hour == 0 ? "жильё" : "еда";
                    case nameof(ActivitySystem):
                        switch (a.State.Activity)
                        {
                            case Activity.Tavern:
                            case Activity.Binge: return "выпивка";
                            case Activity.Infirmary: return "Лазарет";
                            case Activity.Training: return "двор";
                            default: return "занятия (прочее)";
                        }
                    case nameof(FamilySystem): return "семья";
                    case nameof(DilemmaSystem): return delta > 0 ? "займы и помощь гильдии" : "штрафы";
                    case nameof(CommandSystem): return delta > 0 ? "займы и помощь гильдии (ответ на обращение)" : "штрафы (ответ на обращение)";
                    default: return (delta > 0 ? "прочий доход: " : "прочие траты: ") + system;
                }
            }
        }

        private sealed class TapSystem : ISimSystem
        {
            private readonly ISimSystem inner;
            private readonly Tap tap;

            public TapSystem(ISimSystem inner, Tap tap)
            {
                this.inner = inner;
                this.tap = tap;
            }

            public string Name => inner.Name;

            public void Tick(SimContext ctx)
            {
                tap.Before(ctx.World);
                inner.Tick(ctx);
                tap.After(ctx.World, inner.Name);
            }
        }
    }
}
