using System;
using System.Collections.Generic;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>Действие, которое человек может выбрать.</summary>
    public enum DecisionActionKind
    {
        Rest,
        Tavern,
        Sleep,

        /// <summary>Взять заказ с доски (один) — выход в следующем часу.</summary>
        TakeOrder,
    }

    /// <summary>
    /// Вариант решения: занятие, которое он ставит, и оценки по шести мотивам (−1..1). Вариант без оценок выбирается
    /// без сравнения (сон ночью). Новое действие — новый вариант со своей функцией оценок в <see cref="DecisionActions"/>.
    /// </summary>
    public sealed class DecisionAction
    {
        public DecisionAction(DecisionActionKind kind, Activity activity, bool inTavern, Action<DecisionScope, Adventurer, float[]> score,
            int orderId = 0)
        {
            Kind = kind;
            Activity = activity;
            InTavern = inTavern;
            Score = score;
            OrderId = orderId;
        }

        public DecisionActionKind Kind { get; }

        /// <summary>Заказ варианта «взять заказ»; 0 — вариант не о заказе.</summary>
        public int OrderId { get; }

        /// <summary>Имя для лога: «Rest», «TakeOrder#12».</summary>
        public string Label => OrderId != 0 ? Kind + "#" + OrderId.ToString(System.Globalization.CultureInfo.InvariantCulture) : Kind.ToString();

        /// <summary>Занятие, которое ставит выбор.</summary>
        public Activity Activity { get; }

        /// <summary>Вариант в таверне: для эффектов черт «только таверна».</summary>
        public bool InTavern { get; }

        /// <summary>Заполнить оценки по мотивам (индекс — <see cref="Motive"/>); <c>null</c> — вариант без выбора.</summary>
        public Action<DecisionScope, Adventurer, float[]> Score { get; }

        public bool HasScores => Score != null;
    }

    /// <summary>
    /// Что видит оценка варианта в этом часу: контекст такта и друзья (список друзей собирается один раз за час, когда
    /// впервые нужен, — отношения за час не меняются).
    /// </summary>
    public sealed class DecisionScope
    {
        private readonly Dictionary<int, List<int>> friends = new Dictionary<int, List<int>>();
        private bool friendsReady;

        public DecisionScope(SimContext ctx)
        {
            Ctx = ctx;
        }

        public SimContext Ctx { get; }

        /// <summary>
        /// Друзья, которые будут заняты этим со следующего часа: уже выбрали его в этом часу или уже заняты им и не решили
        /// иначе.
        /// </summary>
        public int FriendsHeadingTo(Adventurer adventurer, Activity activity)
        {
            if (!friendsReady) BuildFriends();
            if (!friends.TryGetValue(adventurer.Id, out List<int> ids)) return 0;

            int count = 0;
            foreach (int id in ids)
            {
                if (!Ctx.World.Adventurers.TryGetActive(id, out Adventurer friend)) continue;
                if ((friend.State.PlannedActivity ?? friend.State.Activity) == activity) count++;
            }
            return count;
        }

        private void BuildFriends()
        {
            friendsReady = true;
            float friendsThreshold = Ctx.Data.Balance.Adventurers.FriendsThreshold;
            foreach (Relation relation in Ctx.World.Relations.All)
            {
                if (relation.Value < friendsThreshold) continue; // метка «друзья»
                Add(relation.A, relation.B);
                Add(relation.B, relation.A);
            }
        }

        private void Add(int id, int friend)
        {
            if (!friends.TryGetValue(id, out List<int> list)) friends[id] = list = new List<int>();
            list.Add(friend);
        }
    }

    /// <summary>
    /// Действия и их оценки по мотивам. Коэффициенты — <see cref="DecisionsBalance"/>.
    /// <list type="bullet">
    /// <item>Отдых: Безопасность 1, Отдых = усталость / 100, Утешение 0,3.</item>
    /// <item>Таверна: Деньги = −цена / кошелёк (не ниже −1; пустой кошелёк — −1), Безопасность 1, Товарищи 0,6 + 0,1 за каждого
    /// друга, который уже там или выбрал таверну в этом часу, Отдых 0,3, Утешение = стресс / 100 + 0,2.
    /// Цена — то, что спишется за поход: средняя выпивка, если человек пьёт и сегодня ещё не платил, и наценка еды в таверне,
    /// если он там ест.</item>
    /// <item>Сон: без выбора.</item>
    /// </list>
    /// Оценки ограничены −1..1.
    /// </summary>
    public static class DecisionActions
    {
        /// <summary>Предел оценки по мотиву: −1..1.</summary>
        public const float ScoreLimit = 1f;

        public static readonly DecisionAction Rest = new DecisionAction(DecisionActionKind.Rest, Activity.Resting, false, ScoreRest);
        public static readonly DecisionAction Tavern = new DecisionAction(DecisionActionKind.Tavern, Activity.Tavern, true, ScoreTavern);
        public static readonly DecisionAction Sleep = new DecisionAction(DecisionActionKind.Sleep, Activity.Sleeping, false, null);

        private static void ScoreRest(DecisionScope scope, Adventurer adventurer, float[] scores)
        {
            DecisionsBalance decisions = scope.Ctx.Data.Balance.Decisions;
            scores[(int)Motive.Safety] = decisions.RestSafety;
            scores[(int)Motive.Rest] = adventurer.State.Fatigue / 100f;
            scores[(int)Motive.Comfort] = decisions.RestComfort;
        }

        private static void ScoreTavern(DecisionScope scope, Adventurer adventurer, float[] scores)
        {
            DecisionsBalance decisions = scope.Ctx.Data.Balance.Decisions;
            AdventurerState state = adventurer.State;
            float price = TavernPrice(adventurer, scope.Ctx.Data);
            scores[(int)Motive.Money] = price <= 0f ? 0f : state.Wallet <= 0 ? -ScoreLimit : -price / state.Wallet;
            scores[(int)Motive.Safety] = decisions.TavernSafety;
            scores[(int)Motive.Companions] = decisions.TavernCompanions + decisions.TavernCompanionsPerFriend * scope.FriendsHeadingTo(adventurer, Activity.Tavern);
            scores[(int)Motive.Rest] = decisions.TavernRest;
            scores[(int)Motive.Comfort] = state.Stress / 100f + decisions.TavernComfortBonus;
        }

        /// <summary>Сколько спишется за поход в таверну сегодня: выпивка (среднее) и наценка еды.</summary>
        public static float TavernPrice(Adventurer adventurer, DataRegistry data)
        {
            AdventurerState state = adventurer.State;
            ExpensesBalance expenses = data.Balance.Expenses;
            float price = 0f;
            if (!state.DrankToday && ActivitySystem.WantsToDrink(adventurer, data)) price += (expenses.Drink.Min + expenses.Drink.Max) / 2f;
            if (!state.AteInTavernToday && state.Wallet >= WalletService.WeeklyExpenses(adventurer, expenses))
                price += WalletService.Coins(expenses.TavernFood) - WalletService.Coins(expenses.Food);
            return price;
        }

        /// <summary>Оценки варианта по мотивам, каждая в пределах −1..1.</summary>
        public static float[] Scores(DecisionScope scope, Adventurer adventurer, DecisionAction action)
        {
            var scores = new float[Vocabulary.MotiveCount];
            action.Score?.Invoke(scope, adventurer, scores);
            for (int i = 0; i < scores.Length; i++) scores[i] = Math.Max(-ScoreLimit, Math.Min(ScoreLimit, scores[i]));
            return scores;
        }

        public static IReadOnlyList<DecisionAction> All { get; } = new[] { Rest, Tavern, Sleep };
    }
}
