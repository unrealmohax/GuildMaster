using System;
using System.Collections.Generic;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Часть мира: заказы. Открытые — на доске и ждущие игрока, по порядку прихода; в работе — взятые и идущие задания;
    /// закрытые (отклонённые, снятые, выполненные и проваленные) — архив не больше <c>closedOrdersLimit</c>, старые
    /// выбрасываются. Правила Регистратора, счётчики заказов за игру (для отчёта месяца). У заказов свой счётчик id.
    /// </summary>
    public sealed class OrderBoard
    {
        private readonly List<Order> open = new List<Order>();
        private readonly List<Order> inWork = new List<Order>();
        private readonly List<Order> closed = new List<Order>();
        private int nextId = 1;

        /// <summary>Открытые заказы: на доске и ждущие ответа игрока, по порядку прихода.</summary>
        public IReadOnlyList<Order> Open => open;

        /// <summary>Взятые заказы и заказы, по которым идёт задание, по порядку взятия.</summary>
        public IReadOnlyList<Order> InWork => inWork;

        /// <summary>Отклонённые, снятые, выполненные и проваленные заказы, от старых к новым (последние <c>closedOrdersLimit</c>).</summary>
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

        /// <summary>Заказ по id: открытый, в работе или из архива закрытых.</summary>
        public bool TryGetOrder(int id, out Order order)
        {
            if (TryGetOpen(id, out order)) return true;
            if (Find(inWork, id, out order)) return true;
            return Find(closed, id, out order);
        }

        /// <summary>Сколько гильдия обещала доплатить по заказам на доске и в работе.</summary>
        public int GetPromisedSurcharges()
        {
            int sum = 0;
            foreach (Order order in open)
            {
                if (order.Status == OrderStatus.OnBoard) sum += order.Surcharge;
            }
            foreach (Order order in inWork) sum += order.Surcharge;
            return sum;
        }

        /// <summary>Незакрытое задание на повышение этого человека (на доске или в работе); нет — <c>false</c>.</summary>
        public bool TryGetPromotion(int ownerId, out Order order)
        {
            foreach (Order candidate in open)
            {
                if (!candidate.IsPromotion || candidate.OwnerId != ownerId) continue;
                order = candidate;
                return true;
            }
            foreach (Order candidate in inWork)
            {
                if (!candidate.IsPromotion || candidate.OwnerId != ownerId) continue;
                order = candidate;
                return true;
            }
            order = null;
            return false;
        }

        internal int NextId() => nextId++;

        internal void AddOpen(Order order) => open.Add(order);

        /// <summary>Заказ взят: из открытых — в работу.</summary>
        internal void MoveToWork(Order order)
        {
            open.Remove(order);
            inWork.Add(order);
        }

        /// <summary>Взятый заказ снова на доске: человек не смог выйти.</summary>
        internal void ReturnToOpen(Order order)
        {
            if (inWork.Remove(order)) open.Add(order);
        }

        /// <summary>Закрыть заказ: убрать из открытых или из работы (если он там был) и положить в архив.</summary>
        internal void Close(Order order, int limit)
        {
            open.Remove(order);
            inWork.Remove(order);
            closed.Add(order);
            int excess = closed.Count - Math.Max(1, limit);
            if (excess > 0) closed.RemoveRange(0, excess);
        }

        private static bool Find(List<Order> list, int id, out Order order)
        {
            foreach (Order candidate in list)
            {
                if (candidate.Id != id) continue;
                order = candidate;
                return true;
            }
            order = null;
            return false;
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
        public OrderTotals(int arrived, int posted, int declinedByRegistrar, int declinedByPlayer, int expired,
            int taken = 0, int done = 0, int failed = 0)
        {
            Arrived = arrived;
            Posted = posted;
            DeclinedByRegistrar = declinedByRegistrar;
            DeclinedByPlayer = declinedByPlayer;
            Expired = expired;
            Taken = taken;
            Done = done;
            Failed = failed;
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

        /// <summary>Взято людьми (задания на повышение не считаются).</summary>
        public int Taken { get; }

        /// <summary>Задание по заказу выполнено.</summary>
        public int Done { get; }

        /// <summary>Задание по заказу не выполнено.</summary>
        public int Failed { get; }

        internal OrderTotals AddArrived() => With(arrived: 1);
        internal OrderTotals AddPosted() => With(posted: 1);
        internal OrderTotals AddDeclinedByRegistrar() => With(declinedByRegistrar: 1);
        internal OrderTotals AddDeclinedByPlayer() => With(declinedByPlayer: 1);
        internal OrderTotals AddExpired() => With(expired: 1);
        internal OrderTotals AddTaken() => With(taken: 1);
        internal OrderTotals AddDone() => With(done: 1);
        internal OrderTotals AddFailed() => With(failed: 1);

        private OrderTotals With(int arrived = 0, int posted = 0, int declinedByRegistrar = 0, int declinedByPlayer = 0, int expired = 0,
            int taken = 0, int done = 0, int failed = 0) =>
            new OrderTotals(Arrived + arrived, Posted + posted, DeclinedByRegistrar + declinedByRegistrar, DeclinedByPlayer + declinedByPlayer,
                Expired + expired, Taken + taken, Done + done, Failed + failed);
    }
}
