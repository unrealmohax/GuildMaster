using System.Collections.Generic;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>Уровень Безопасности поселения: от спокойного к осаде.</summary>
    public enum SettlementLevel
    {
        /// <summary>Спокойно: население растёт.</summary>
        Calm,

        /// <summary>Норма: население держится.</summary>
        Normal,

        /// <summary>Тревога: жители уезжают.</summary>
        Alarm,

        /// <summary>Осада: жители уезжают быстрее, по ночам бывают нападения.</summary>
        Siege,
    }

    /// <summary>
    /// Часть мира: поселение рядом с гильдией — название, Безопасность 0..<c>maxSecurity</c>, население, уровень, отсчёт
    /// покидания, последние изменения Безопасности и счётчики за игру. Меняется только через <see cref="SettlementService"/>.
    /// </summary>
    public sealed class SettlementState
    {
        private readonly List<SettlementChange> changes = new List<SettlementChange>();

        internal SettlementState()
        {
        }

        /// <summary>Название в шести падежах; <c>null</c> — реестр без определений (названий нет).</summary>
        public NounForms Name { get; internal set; }

        public float Security { get; internal set; }

        public int Population { get; internal set; }

        public SettlementLevel Level { get; internal set; }

        /// <summary>Безопасность и население на начало игры.</summary>
        public float StartSecurity { get; private set; }

        public int StartPopulation { get; private set; }

        /// <summary>С какого часа Безопасность на нуле; <c>null</c> — не на нуле, отсчёта покидания нет.</summary>
        public long? ZeroSecuritySinceHours { get; internal set; }

        /// <summary>Последние изменения Безопасности, от старых к новым (не больше <c>recentChanges</c>).</summary>
        public IReadOnlyList<SettlementChange> Changes => changes;

        /// <summary>Счётчики за игру: угрозы, нападения, ущерб. Отчёт месяца берёт их разницу.</summary>
        public SettlementTotals Totals { get; internal set; }

        /// <summary>Дробный остаток дохода таверны от жителей, ещё не зачисленный в целых монетах.</summary>
        internal double TavernRemainder { get; set; }

        internal void Initialize(float security, int population, SettlementLevel level)
        {
            StartSecurity = security;
            StartPopulation = population;
            Security = security;
            Population = population;
            Level = level;
        }

        internal void AddChange(SettlementChange change, int limit)
        {
            changes.Add(change);
            if (changes.Count > limit) changes.RemoveRange(0, changes.Count - limit);
        }
    }

    /// <summary>
    /// Изменение Безопасности: когда, на сколько (фактически, после обрезки), почему (<see cref="SettlementReasons"/>) и о каком
    /// заказе (<see cref="OrderId"/>, тип и ранг — чтобы подпись не зависела от архива заказов); 0 — без заказа.
    /// </summary>
    public sealed class SettlementChange
    {
        internal SettlementChange(long timeHours, float delta, string reason, int orderId, string questTypeId, GuildRank rank)
        {
            TimeHours = timeHours;
            Delta = delta;
            Reason = reason;
            OrderId = orderId;
            QuestTypeId = questTypeId;
            Rank = rank;
        }

        public long TimeHours { get; }
        public float Delta { get; }
        public string Reason { get; }
        public int OrderId { get; }
        public string QuestTypeId { get; }
        public GuildRank Rank { get; }
    }

    /// <summary>Счётчики поселения за игру.</summary>
    public readonly struct SettlementTotals
    {
        public SettlementTotals(int threatsDone, int threatsMissed, int attacks, int damage, int killed)
        {
            ThreatsDone = threatsDone;
            ThreatsMissed = threatsMissed;
            Attacks = attacks;
            Damage = damage;
            Killed = killed;
        }

        /// <summary>Выполнено заказов-угроз.</summary>
        public int ThreatsDone { get; }

        /// <summary>Упущено заказов-угроз: сняты по сроку или отклонены.</summary>
        public int ThreatsMissed { get; }

        public int Attacks { get; }

        /// <summary>Ущерб казне от нападений.</summary>
        public int Damage { get; }

        /// <summary>Жителей погибло при нападениях.</summary>
        public int Killed { get; }

        internal SettlementTotals AddThreatDone() => new SettlementTotals(ThreatsDone + 1, ThreatsMissed, Attacks, Damage, Killed);
        internal SettlementTotals AddThreatMissed() => new SettlementTotals(ThreatsDone, ThreatsMissed + 1, Attacks, Damage, Killed);
        internal SettlementTotals AddAttack(int damage, int killed) =>
            new SettlementTotals(ThreatsDone, ThreatsMissed, Attacks + 1, Damage + damage, Killed + killed);
    }

    /// <summary>Поселение на момент: Безопасность, население, счётчики — для отчёта месяца.</summary>
    public readonly struct SettlementSnapshot
    {
        public SettlementSnapshot(float security, int population, SettlementTotals totals)
        {
            Security = security;
            Population = population;
            Totals = totals;
        }

        public float Security { get; }
        public int Population { get; }
        public SettlementTotals Totals { get; }

        public static SettlementSnapshot Of(SettlementState settlement) =>
            new SettlementSnapshot(settlement.Security, settlement.Population, settlement.Totals);

        /// <summary>Поселение на начало игры: стартовые Безопасность и население, счётчики — нули.</summary>
        public static SettlementSnapshot AtStart(SettlementState settlement) =>
            new SettlementSnapshot(settlement.StartSecurity, settlement.StartPopulation, default);
    }

    /// <summary>Причины изменения Безопасности и населения — ключи для лога и подписей интерфейса.</summary>
    public static class SettlementReasons
    {
        /// <summary>Суточный спад: дикие земли наступают.</summary>
        public const string Night = "night";
        public const string ThreatDone = "threatDone";
        public const string OtherDone = "otherDone";
        public const string Failed = "failed";
        public const string PartyLost = "partyLost";
        public const string Expired = "expired";
        public const string DeclinedByRegistrar = "registrar";
        public const string DeclinedByPlayer = "player";
        public const string NoAnswer = "noAnswer";
        public const string Debug = "debug";

        /// <summary>Набег отбит.</summary>
        public const string RaidWon = "raidWon";

        /// <summary>Набег не отбит.</summary>
        public const string RaidLost = "raidLost";

        /// <summary>Налог увёл казну гильдии в минус — лорд недоволен.</summary>
        public const string TaxUnpaid = "taxUnpaid";

        // Население.
        public const string Month = "month";
        public const string Building = "building";
        public const string Attack = "attack";

        /// <summary>Готов проект поселения (Колодец, Дорога).</summary>
        public const string Project = "project";

        /// <summary>Набег не отбит: часть жителей уходит.</summary>
        public const string RaidFled = "raidFled";

        /// <summary>Мор.</summary>
        public const string Plague = "plague";
    }
}
