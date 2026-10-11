using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using GuildMaster.Core;
using GuildMaster.Data;

namespace GuildMaster.ClaudeSandbox
{
    /// <summary>
    /// Песочница. Бюджет авантюристов по рангам: каждый такт сравнивает кошелёк каждого человека с прошлым тактом и раскладывает
    /// разницу по статьям. Точно известные из событий — еда (<see cref="SimEventType.MealEaten"/>, цена типа), семья
    /// (<see cref="SimEventType.FamilyMoneySent"/>), ремонт снаряжения; остаток — по часу и занятию: в 00:00 — жильё (до его цены),
    /// плюс — награда (если человек вернулся с задания в этот такт), заём (обращение) или прочий доход; минус — выпивка (начало ночи, таверна,
    /// запой), Лазарет, двор, пайки (на задании) или прочее. Сумма по рангу и человеко-часы на ранге — в CSV (по годам).
    /// </summary>
    public sealed class BudgetTracker
    {
        public static readonly string[] Categories =
        {
            "Награды и добыча", "Займы", "Прочий доход",
            "Еда", "Жильё", "Семья", "Выпивка", "Пайки", "Ремонт", "Лазарет", "Двор", "Прочий расход",
        };

        private const int Reward = 0, Loan = 1, OtherIncome = 2, Food = 3, Housing = 4, Family = 5, Drink = 6, Rations = 7, Repair = 8,
            Infirmary = 9, Training = 10, OtherExpense = 11;

        private readonly Simulation simulation;
        private readonly Dictionary<int, int> wallets = new Dictionary<int, int>();
        private readonly Dictionary<int, int> known = new Dictionary<int, int>();
        private readonly HashSet<int> returned = new HashSet<int>();
        private readonly HashSet<int> loans = new HashSet<int>();

        // [год, ранг, статья] — монеты; [год, ранг] — человеко-часы, выполненные задания, сумма кошельков (по часам).
        private readonly double[,,] money;
        private readonly double[,] hours, quests, walletHours;
        private readonly int years;

        public BudgetTracker(Simulation simulation, int years)
        {
            this.simulation = simulation;
            this.years = years;
            money = new double[years, 5, Categories.Length];
            hours = new double[years, 5];
            quests = new double[years, 5];
            walletHours = new double[years, 5];
            simulation.TickCompleted += OnTick;
        }

        private void OnTick(IReadOnlyList<SimEvent> events)
        {
            WorldState world = simulation.World;
            int year = Math.Min(years - 1, (int)((world.Time.TotalHours - 1) / (simulation.Calendar.HoursPerDay * 360L)));
            FoodBalance food = simulation.Data.Balance.Food;
            known.Clear();
            returned.Clear();
            loans.Clear();
            var family = new Dictionary<int, int>();
            var repair = new Dictionary<int, int>();

            foreach (SimEvent e in events)
            {
                switch (e.Type)
                {
                    case SimEventType.MealEaten:
                        if (e.TryGet("guildPays", out bool guildPays) && guildPays) break;
                        e.TryGet("tier", out MealTier tier);
                        int price = tier == MealTier.Cheap ? food.Cheap.Price : tier == MealTier.Simple ? food.Simple.Price : food.Luxury.Price;
                        foreach (int id in e.Participants) Add(known, id, price);
                        break;
                    case SimEventType.FamilyMoneySent:
                        if (e.TryGet("amount", out int sent)) foreach (int id in e.Participants) Add(family, id, sent);
                        break;
                    case SimEventType.QuestLoss:
                        if (e.TryGet("kind", out string kind) && kind == "gear" && e.TryGet("paid", out int paid))
                            foreach (int id in e.Participants) Add(repair, id, paid);
                        break;
                    case SimEventType.QuestReturned:
                        bool promotion = e.TryGet("promotion", out bool p) && p;
                        bool done = e.TryGet("done", out bool d) && d;
                        foreach (int id in e.Participants)
                        {
                            returned.Add(id);
                            if (!promotion && done && world.Adventurers.TryGetActive(id, out Adventurer a)) quests[year, (int)a.GuildRank]++;
                        }
                        break;
                    case SimEventType.DilemmaAnswered:
                        if (e.TryGet("amount", out int amount) && amount > 0) foreach (int id in e.Participants) loans.Add(id);
                        break;
                }
            }

            bool midnight = world.Time.Hour == 0;
            bool night = world.Time.Hour == simulation.Data.Balance.Time.NightHour;
            foreach (Adventurer a in world.Adventurers.Active)
            {
                int rank = (int)a.GuildRank;
                int wallet = a.State.Wallet;
                hours[year, rank]++;
                walletHours[year, rank] += wallet;
                if (!wallets.TryGetValue(a.Id, out int before))
                {
                    wallets[a.Id] = wallet; // пришёл — стартовый кошелёк не доход
                    continue;
                }
                wallets[a.Id] = wallet;

                int mealCost = known.TryGetValue(a.Id, out int m) ? m : 0;
                int familyCost = family.TryGetValue(a.Id, out int f) ? f : 0;
                int repairCost = repair.TryGetValue(a.Id, out int r) ? r : 0;
                money[year, rank, Food] += mealCost;
                money[year, rank, Family] += familyCost;
                money[year, rank, Repair] += repairCost;

                int rest = wallet - before + mealCost + familyCost + repairCost;
                if (rest > 0)
                {
                    int category = returned.Contains(a.Id) ? Reward : loans.Contains(a.Id) ? Loan : OtherIncome;
                    money[year, rank, category] += rest;
                    continue;
                }
                if (rest == 0) continue;

                int spent = -rest;
                if (midnight)
                {
                    int housing = Math.Min(spent, FoodRules.HousingCost(a, simulation.Data.Balance.Expenses));
                    money[year, rank, Housing] += housing;
                    spent -= housing;
                    if (spent == 0) continue;
                }
                money[year, rank, night ? Drink : ExpenseFor(a.State.Activity)] += spent; // в начале ночи — вечерняя выпивка
            }
        }

        private static int ExpenseFor(Activity activity)
        {
            switch (activity)
            {
                case Activity.Tavern:
                case Activity.Binge:
                    return Drink;
                case Activity.Infirmary:
                    return Infirmary;
                case Activity.Training:
                    return Training;
                case Activity.OnQuestTravel:
                case Activity.OnQuestRound:
                case Activity.OnQuestCamp:
                    return Rations;
                default:
                    return OtherExpense;
            }
        }

        private static void Add(Dictionary<int, int> map, int id, int value) => map[id] = (map.TryGetValue(id, out int old) ? old : 0) + value;

        /// <summary>CSV: год, ранг, человеко-часы, выполненных заданий, средний кошелёк, затем монеты по статьям.</summary>
        public string ToCsv()
        {
            CultureInfo inv = CultureInfo.InvariantCulture;
            var text = new StringBuilder("Год,Ранг,Человеко-часов,Заданий выполнено,Кошелёк средний," + string.Join(",", Categories) + "\n");
            for (int y = 0; y < years; y++)
            {
                for (int k = 0; k < 5; k++)
                {
                    text.Append(y + 1).Append(',').Append((GuildRank)k).Append(',').Append(hours[y, k].ToString(inv)).Append(',')
                        .Append(quests[y, k].ToString(inv)).Append(',')
                        .Append((hours[y, k] > 0 ? walletHours[y, k] / hours[y, k] : 0).ToString("0", inv));
                    for (int c = 0; c < Categories.Length; c++) text.Append(',').Append(money[y, k, c].ToString(inv));
                    text.Append('\n');
                }
            }
            return text.ToString();
        }
    }
}
