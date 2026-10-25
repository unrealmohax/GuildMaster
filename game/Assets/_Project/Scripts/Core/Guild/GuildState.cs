using System.Collections.Generic;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Часть мира: гильдия как целое — репутация 0..100 и её последние изменения. От репутации зависят поток и ранги заказов
    /// и приток кандидатов. Меняется только через <see cref="ReputationService"/>.
    /// </summary>
    public sealed class GuildState
    {
        private readonly List<ReputationChange> changes = new List<ReputationChange>();

        /// <summary>Репутация на начало игры.</summary>
        public float StartReputation { get; private set; }

        /// <summary>Текущая репутация, 0..<c>maxReputation</c>.</summary>
        public float Reputation { get; internal set; }

        /// <summary>Последние изменения репутации, от старых к новым (не больше <c>recentReputationChanges</c>).</summary>
        public IReadOnlyList<ReputationChange> Changes => changes;

        /// <summary>Выполнено заданий (без экзаменов) с последней полуночной проверки остывания.</summary>
        internal int QuestsDoneToday { get; set; }

        internal void Initialize(float reputation)
        {
            StartReputation = reputation;
            Reputation = reputation;
        }

        internal void AddChange(ReputationChange change, int limit)
        {
            changes.Add(change);
            if (changes.Count > limit) changes.RemoveRange(0, changes.Count - limit);
        }
    }

    /// <summary>
    /// Изменение репутации: когда, на сколько (фактически, после обрезки), почему (<see cref="ReputationReasons"/>), о ком
    /// (<see cref="AdventurerId"/>) и о каком заказе (<see cref="OrderId"/>, тип и ранг — чтобы подпись не зависела от архива
    /// заказов); 0 — без человека или без заказа.
    /// </summary>
    public sealed class ReputationChange
    {
        internal ReputationChange(long timeHours, float delta, string reason, int adventurerId, int orderId, string questTypeId, GuildRank rank)
        {
            TimeHours = timeHours;
            Delta = delta;
            Reason = reason;
            AdventurerId = adventurerId;
            OrderId = orderId;
            QuestTypeId = questTypeId;
            Rank = rank;
        }

        public long TimeHours { get; }
        public float Delta { get; }
        public string Reason { get; }
        public int AdventurerId { get; }
        public int OrderId { get; }
        public string QuestTypeId { get; }
        public GuildRank Rank { get; }
    }

    /// <summary>Причины изменения репутации — ключи для лога и подписей интерфейса.</summary>
    public static class ReputationReasons
    {
        public const string QuestDone = "questDone";
        public const string QuestFailed = "questFailed";

        /// <summary>Не выполнен важный заказ, принятый игроком (сверх обычного за провал).</summary>
        public const string ImportantFailed = "importantFailed";

        /// <summary>Остывание: сутки без выполненных заданий.</summary>
        public const string Decay = "decay";

        public const string Death = "death";

        /// <summary>Человек ушёл из гильдии сам.</summary>
        public const string Left = "left";

        /// <summary>Человек сбежал с задания.</summary>
        public const string Fled = "fled";

        /// <summary>Хвастун расписал выполненное задание.</summary>
        public const string Braggart = "braggart";

        public const string Debug = "debug";

        /// <summary>Все причины — для подписей интерфейса и проверки шаблонов.</summary>
        public static IReadOnlyList<string> All { get; } = new[]
        {
            QuestDone, QuestFailed, ImportantFailed, Decay, Death, Left, Fled, Braggart, Debug,
        };
    }
}
