using System;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Чистые правила походных пайков: сколько брать, какой тип по карману, чей запас, кто делится. Мир не меняют, случайных
    /// чисел не тратят. Числа — <see cref="FoodBalance"/>.
    /// <para>
    /// Сколько брать: приёмы пищи ожидаемого задания (<see cref="TravelPlan"/>) + запас на задержки. Запас сдвигает ось Риск:
    /// к Трусу — больше, до + <c>cautiousExtraMeals</c> по силе полюса; к Безрассудному — меньше, до 0.
    /// Тип — по бюджету на еду, как в городе: обычные, если вся покупка по карману без захода в резерв
    /// (<see cref="FoodRules.Reserve"/>); иначе дешёвые — сколько хватит денег, резерв уже не держит.
    /// </para>
    /// </summary>
    public static class RationRules
    {
        /// <summary>Запас на задержки, приёмов: <c>rationReserveMeals</c> × (1 − сила Безрассудного) + <c>cautiousExtraMeals</c> × сила Труса.</summary>
        public static int ReserveMeals(Adventurer adventurer, FoodBalance food)
        {
            float risk = adventurer.GetAxis(AxisId.Risk);
            float reserve = food.RationReserveMeals * (1f - AxisMath.Strength(risk, AxisPole.Positive))
                            + food.CautiousExtraMeals * AxisMath.Strength(risk, AxisPole.Negative);
            return Math.Max(0, (int)Math.Round(reserve, MidpointRounding.AwayFromZero));
        }

        /// <summary>Сколько пайков нужно одному на задание с <paramref name="meals"/> приёмами пищи: приёмы + запас.</summary>
        public static int Need(Adventurer adventurer, int meals, FoodBalance food) => meals + ReserveMeals(adventurer, food);

        /// <summary>
        /// Какой тип и сколько пайков человек купит из <paramref name="wanted"/>: обычные — если на все хватает без захода
        /// в резерв; иначе дешёвые, сколько хватит денег. Зимой — по зимним ценам.
        /// </summary>
        public static (RationTier Tier, int Count) Afford(Adventurer adventurer, int wanted, BalanceSettings balance, bool winter = false)
        {
            if (wanted <= 0) return (RationTier.Cheap, 0);
            int wallet = adventurer.State.Wallet;
            int spare = wallet - FoodRules.Reserve(adventurer, balance, winter);
            if (spare >= wanted * FoodRules.RationPrice(RationTier.Regular, balance, winter)) return (RationTier.Regular, wanted);
            int price = FoodRules.RationPrice(RationTier.Cheap, balance, winter);
            return (RationTier.Cheap, price > 0 ? Math.Min(wanted, wallet / price) : wanted);
        }

        /// <summary>Цена покупки <paramref name="count"/> пайков этого типа (зимой — по зимней цене).</summary>
        public static int Cost(RationTier tier, int count, BalanceSettings balance, bool winter = false) =>
            count * FoodRules.RationPrice(tier, balance, winter);

        /// <summary>
        /// Запас, на который человек рассчитывает перед выбором заказа: личный, а у члена постоянной группы ещё его доля общего
        /// (общий / члены, вниз). <paramref name="parties"/> не задан — только личный.
        /// </summary>
        public static int OwnStock(Adventurer adventurer, PartyBook parties)
        {
            int own = adventurer.Rations.Total;
            if (parties != null && adventurer.PermanentPartyId != 0 && parties.TryGetParty(adventurer.PermanentPartyId, out Party party)
                && party.MemberIds.Count > 0)
                own += party.Rations.Total / party.MemberIds.Count;
            return own;
        }

        /// <summary>
        /// Сколько человеку стоят пайки на этот заказ, как он их прикидывает: нужно на ожидаемое задание + запас, минус свой
        /// запас, по типу, который он купил бы (не больше, чем есть денег). Зимой — по зимним ценам.
        /// </summary>
        public static int OrderCost(Adventurer adventurer, Order order, DataRegistry data, int ownStock, bool winter = false)
        {
            FoodBalance food = data.Balance.Food;
            int wanted = Need(adventurer, TravelPlan.ExpectedMeals(order, data), food) - ownStock;
            if (wanted <= 0) return 0;
            (RationTier tier, int count) = Afford(adventurer, wanted, data.Balance, winter);
            return Cost(tier, count, data.Balance, winter);
        }

        /// <summary>Жадный не делится пайками: ось Деньги не ниже <c>greedyNoShareAxis</c>.</summary>
        public static bool IsGreedy(Adventurer adventurer, FoodBalance food) => adventurer.GetAxis(AxisId.Money) >= food.GreedyNoShareAxis;

        /// <summary>Командный делится первым: ось Люди к Командному, вне нейтральной зоны.</summary>
        public static bool IsTeamPlayer(Adventurer adventurer, AdventurersBalance balance)
        {
            float people = adventurer.GetAxis(AxisId.People);
            return people > 0f && !AxisMath.IsNeutral(people, balance);
        }
    }
}
