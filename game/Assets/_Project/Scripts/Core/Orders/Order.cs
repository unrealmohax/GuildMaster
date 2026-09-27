using System.Collections.Generic;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>Где заказ в своей жизни. Новые статусы — в конец.</summary>
    public enum OrderStatus
    {
        /// <summary>Только что пришёл, ещё не разобран Регистратором.</summary>
        Incoming,

        /// <summary>Висит на доске.</summary>
        OnBoard,

        /// <summary>Важный заказ ждёт решения игрока.</summary>
        AwaitingPlayer,

        /// <summary>Отклонён: Регистратором, игроком или без ответа (<see cref="Order.DeclinedBy"/>).</summary>
        Declined,

        /// <summary>Никто не взял — снят с доски по сроку.</summary>
        Expired,

        /// <summary>Человек взял заказ и выйдет в следующем часу.</summary>
        Taken,

        /// <summary>Задание идёт.</summary>
        InProgress,

        /// <summary>Задание выполнено.</summary>
        Done,

        /// <summary>Задание не выполнено: отступили или погибли все.</summary>
        Failed,
    }

    public enum OrderDistance
    {
        Near,
        Far,
    }

    /// <summary>Кто отклонил заказ.</summary>
    public enum OrderDeclinedBy
    {
        None,

        /// <summary>Регистратор — по правилам игрока.</summary>
        Registrar,

        /// <summary>Игрок ответил «нет» на важный заказ.</summary>
        Player,

        /// <summary>Игрок не ответил на важный заказ вовремя.</summary>
        NoAnswer,
    }

    /// <summary>
    /// Заказ. Игрок видит тип, заказчика, ранг, расстояние, награду, доплату, описание и срок на доске. Профиль требований,
    /// потолки и точность описания скрыты: снаружи симуляции их не прочитать.
    /// </summary>
    public sealed class Order
    {
        private readonly float[] profile = new float[Vocabulary.StatCount];
        private readonly int[] statCeilings = new int[Vocabulary.StatCount];
        private readonly List<StatId> hintStats = new List<StatId>();

        /// <summary>Все требования профиля по <see cref="StatId"/> (скрыто).</summary>
        internal float[] Profile => profile;

        internal Order(int id, string typeId, GuildRank rank, OrderDistance distance)
        {
            Id = id;
            TypeId = typeId;
            Rank = rank;
            Distance = distance;
        }

        public int Id { get; }

        /// <summary>Id типа задания (<see cref="QuestTypeDefinition"/>).</summary>
        public string TypeId { get; }

        /// <summary>Ранг заказа. У событийного задания его назначает игрок.</summary>
        public GuildRank Rank { get; internal set; }
        public OrderDistance Distance { get; }

        public NounForms Client { get; internal set; }
        public NounForms Place { get; internal set; }
        public NounForms Enemy { get; internal set; }

        /// <summary>Груз; у типов без грузов — <c>null</c>.</summary>
        public NounForms Cargo { get; internal set; }

        /// <summary>Текст для игрока и людей: описание и намёки.</summary>
        public string Description { get; internal set; }

        /// <summary>На какие оси профиля намекает описание.</summary>
        public IReadOnlyList<StatId> HintStats => hintStats;

        /// <summary>Награда заказчика.</summary>
        public int Reward { get; internal set; }

        /// <summary>Доплата гильдии — обещание; из казны сейчас не списывается.</summary>
        public int Surcharge { get; internal set; }

        public OrderStatus Status { get; internal set; }
        public OrderDeclinedBy DeclinedBy { get; internal set; }

        /// <summary>Важный: идёт игроку, а не Регистратору.</summary>
        public bool IsImportant { get; internal set; }

        /// <summary>Сложный не по времени: ранг выше, чем обычно приходит при такой репутации.</summary>
        public bool IsHardEarly { get; internal set; }

        /// <summary>Задание на повышение — экзамен владельца: личный, без награды, виден только владельцу.</summary>
        public bool IsPromotion { get; internal set; }

        /// <summary>Владелец задания на повышение; 0 — заказ общий.</summary>
        public int OwnerId { get; internal set; }

        /// <summary>Событийное задание из находки: ранг назначает игрок, награду платит гильдия.</summary>
        public bool IsEventQuest { get; internal set; }

        /// <summary>Событийное задание: ранг задания, в котором сделана находка (подсказка игроку).</summary>
        public GuildRank SourceRank { get; internal set; }

        /// <summary>Кто взял заказ; 0 — никто.</summary>
        public int TakenBy { get; internal set; }

        /// <summary>Задание по заказу (<see cref="QuestRun.Id"/>); 0 — не начато.</summary>
        public int QuestRunId { get; internal set; }

        public long ArrivedAtHours { get; internal set; }

        /// <summary>Когда повесили на доску; −1 — не висел.</summary>
        public long PostedAtHours { get; internal set; } = -1;

        /// <summary>Сколько дней висеть на доске.</summary>
        public int BoardDays { get; internal set; }

        /// <summary>Когда снимут с доски; −1 — не на доске.</summary>
        public long ExpiresAtHours { get; internal set; } = -1;

        /// <summary>До какого часа игрок может ответить на важный заказ; −1 — ответа не ждут.</summary>
        public long AnswerDueAtHours { get; internal set; } = -1;

        /// <summary>Когда отклонён или снят; −1 — ещё открыт.</summary>
        public long ClosedAtHours { get; internal set; } = -1;

        /// <summary>Открыт: на доске или ждёт игрока.</summary>
        public bool IsOpen => Status == OrderStatus.OnBoard || Status == OrderStatus.AwaitingPlayer;

        /// <summary>В работе: взят или идёт задание.</summary>
        public bool IsInWork => Status == OrderStatus.Taken || Status == OrderStatus.InProgress;

        /// <summary>Истинный ранг находки у событийного задания: от него — скрытый профиль.</summary>
        internal GuildRank TrueRank { get; set; }

        // ---------- Скрытое ----------

        /// <summary>Требование профиля по параметру. У Слаженности — 0: она не ось диаграммы.</summary>
        internal float Requirement(StatId stat) => profile[(int)stat];

        internal void SetRequirement(StatId stat, float value) => profile[(int)stat] = value;

        /// <summary>Потолок размера группы; 0 — нет.</summary>
        internal int PartySizeCeiling { get; set; }

        /// <summary>Потолок по параметру; 0 — нет.</summary>
        internal int StatCeiling(StatId stat) => statCeilings[(int)stat];

        internal void SetStatCeiling(StatId stat, int value) => statCeilings[(int)stat] = value;

        /// <summary>Насколько описание недооценивает (&lt; 1) или переоценивает (&gt; 1) задание.</summary>
        internal float DescriptionAccuracy { get; set; }

        internal void AddHint(StatId stat) => hintStats.Add(stat);
    }
}
