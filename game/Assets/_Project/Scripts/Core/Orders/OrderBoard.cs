using System;
using System.Collections.Generic;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Часть мира: заказы. Открытые — на доске и ждущие игрока, по порядку прихода; закрытые (отклонённые и снятые) — архив
    /// не больше <c>closedOrdersLimit</c>, старые выбрасываются. Правила Регистратора, счётчики заказов за игру (для отчёта
    /// месяца). У заказов свой счётчик id.
    /// </summary>
    public sealed class OrderBoard
    {
        private readonly List<Order> open = new List<Order>();
        private readonly List<Order> closed = new List<Order>();
        private int nextId = 1;

        /// <summary>Открытые заказы: на доске и ждущие ответа игрока, по порядку прихода.</summary>
        public IReadOnlyList<Order> Open => open;

        /// <summary>Отклонённые и снятые заказы, от старых к новым (последние <c>closedOrdersLimit</c>).</summary>
        public IReadOnlyList<Order> Closed => closed;

        /// <summary>Правила, по которым Регистратор ставит обычные заказы на доску.</summary>
        public RegistrarRules Rules { get; internal set; } = RegistrarRules.Default;

        /// <summary>Сколько заказов пришло, повешено, отклонено и снято за игру (стартовые заказы не считаются).</summary>
        public OrderTotals Totals { get; internal set; }

        public bool TryGetOpen(int id, out Order order)
        {
            foreach (Order candidate in open)
            {
                if (candidate.Id != id) continue;
                order = candidate;
                return true;
            }
            order = null;
            return false;
        }

        /// <summary>Заказ по id: открытый или из архива закрытых.</summary>
        public bool TryGetOrder(int id, out Order order)
        {
            if (TryGetOpen(id, out order)) return true;
            foreach (Order candidate in closed)
            {
                if (candidate.Id != id) continue;
                order = candidate;
                return true;
            }
            order = null;
            return false;
        }

        /// <summary>Сколько гильдия обещала доплатить по заказам на доске.</summary>
        public int GetPromisedSurcharges()
        {
            int sum = 0;
            foreach (Order order in open)
            {
                if (order.Status == OrderStatus.OnBoard) sum += order.Surcharge;
            }
            return sum;
        }

        internal int NextId() => nextId++;

        internal void AddOpen(Order order) => open.Add(order);

        /// <summary>Закрыть заказ: убрать из открытых (если он там был) и положить в архив.</summary>
        internal void Close(Order order, int limit)
        {
            open.Remove(order);
            closed.Add(order);
            int excess = closed.Count - Math.Max(1, limit);
            if (excess > 0) closed.RemoveRange(0, excess);
        }
    }

    /// <summary>
    /// Правила Регистратора: какие типы заданий брать, до какого ранга, с какой награды. По умолчанию — все типы, до ранга C,
    /// без минимума награды.
    /// </summary>
    public sealed class RegistrarRules
    {
        private readonly HashSet<string> allowedTypes;

        /// <param name="allowedTypes">Id типов заданий; <c>null</c> — все типы.</param>
        public RegistrarRules(IEnumerable<string> allowedTypes, GuildRank maxRank, int minReward)
        {
            this.allowedTypes = allowedTypes != null ? new HashSet<string>(allowedTypes, StringComparer.Ordinal) : null;
            MaxRank = maxRank;
            MinReward = Math.Max(0, minReward);
        }

        public static RegistrarRules Default { get; } = new RegistrarRules(null, GuildRank.C, 0);

        /// <summary>Берутся все типы заданий.</summary>
        public bool AllTypes => allowedTypes == null;

        /// <summary>Разрешённые типы по порядку id; при <see cref="AllTypes"/> — пусто.</summary>
        public IReadOnlyList<string> AllowedTypes
        {
            get
            {
                var list = allowedTypes != null ? new List<string>(allowedTypes) : new List<string>();
                list.Sort(StringComparer.Ordinal);
                return list;
            }
        }

        /// <summary>До какого ранга Регистратор берёт сам (включительно).</summary>
        public GuildRank MaxRank { get; }

        /// <summary>Минимальная награда.</summary>
        public int MinReward { get; }

        public bool IsTypeAllowed(string typeId) => allowedTypes == null || allowedTypes.Contains(typeId);

        /// <summary>Обычный заказ подходит под правила.</summary>
        public bool IsAccepted(Order order) => IsTypeAllowed(order.TypeId) && order.Rank <= MaxRank && order.Reward >= MinReward;

        public bool IsSameAs(RegistrarRules other)
        {
            if (other == null || MaxRank != other.MaxRank || MinReward != other.MinReward || AllTypes != other.AllTypes) return false;
            return allowedTypes == null || allowedTypes.SetEquals(other.allowedTypes);
        }

        /// <summary>Для лога и сценария: «all C 0», «Escort,Hunt E 50».</summary>
        public override string ToString() =>
            (AllTypes ? "all" : AllowedTypes.Count == 0 ? "none" : string.Join(",", AllowedTypes)) + " " + MaxRank + " " +
            MinReward.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>Счётчики заказов за игру. Отчёт месяца берёт разницу между своим концом и прошлым отчётом.</summary>
    public readonly struct OrderTotals
    {
        public OrderTotals(int arrived, int posted, int declinedByRegistrar, int declinedByPlayer, int expired)
        {
            Arrived = arrived;
            Posted = posted;
            DeclinedByRegistrar = declinedByRegistrar;
            DeclinedByPlayer = declinedByPlayer;
            Expired = expired;
        }

        /// <summary>Пришло заказов.</summary>
        public int Arrived { get; }

        /// <summary>Повешено на доску: Регистратором и принятых игроком важных.</summary>
        public int Posted { get; }

        public int DeclinedByRegistrar { get; }

        /// <summary>Отклонено игроком, в том числе без ответа.</summary>
        public int DeclinedByPlayer { get; }

        /// <summary>Снято по сроку.</summary>
        public int Expired { get; }

        internal OrderTotals AddArrived() => new OrderTotals(Arrived + 1, Posted, DeclinedByRegistrar, DeclinedByPlayer, Expired);
        internal OrderTotals AddPosted() => new OrderTotals(Arrived, Posted + 1, DeclinedByRegistrar, DeclinedByPlayer, Expired);
        internal OrderTotals AddDeclinedByRegistrar() => new OrderTotals(Arrived, Posted, DeclinedByRegistrar + 1, DeclinedByPlayer, Expired);
        internal OrderTotals AddDeclinedByPlayer() => new OrderTotals(Arrived, Posted, DeclinedByRegistrar, DeclinedByPlayer + 1, Expired);
        internal OrderTotals AddExpired() => new OrderTotals(Arrived, Posted, DeclinedByRegistrar, DeclinedByPlayer, Expired + 1);
    }
}
