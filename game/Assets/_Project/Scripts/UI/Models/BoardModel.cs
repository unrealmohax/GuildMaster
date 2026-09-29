using System.Collections.Generic;
using System.Globalization;
using System.Text;
using GuildMaster.Core;
using GuildMaster.Data;

namespace GuildMaster.UI
{
    /// <summary>Заказ в списке доски: всё, что видно игроку. Профиля требований и шанса здесь нет.</summary>
    public sealed class OrderRow
    {
        public int Id;
        public string Type;
        public GuildRank Rank;
        public string Client;
        public string Distance;
        public int Reward;
        public int Surcharge;

        /// <summary>Сколько ещё висеть («3 дн.»), ждёт ответа игрока или «в работе».</summary>
        public string TimeLeft;

        public List<(int Id, string Name)> TakenBy = new List<(int, string)>();
        public string Marks;
        public bool InWork;
        public int QuestRunId;
    }

    /// <summary>
    /// Экран «Доска»: слева — заказы на доске и в работе, справа — выбранный заказ с описанием и намёками. Скрытый профиль
    /// и шанс видны только в режиме «Раскрыть всё» отладки (<see cref="RevealAll"/>).
    /// </summary>
    public sealed class BoardModel
    {
        public List<OrderRow> Rows { get; } = new List<OrderRow>();

        public int SelectedId { get; set; }

        /// <summary>Отладка: показывать скрытое.</summary>
        public bool RevealAll { get; set; }

        public string DetailTitle { get; private set; } = string.Empty;
        public string DetailDescription { get; private set; } = string.Empty;
        public string DetailFacts { get; private set; } = string.Empty;

        /// <summary>Скрытое для отладки; пусто, если «Раскрыть всё» выключено.</summary>
        public string DetailHidden { get; private set; } = string.Empty;

        public OrderRow Selected { get; private set; }

        public void Refresh(ISimulationClient client)
        {
            Rows.Clear();
            WorldState world = client.World;
            foreach (Order order in world.Orders.Open) Rows.Add(Row(order, client));
            foreach (Order order in world.Orders.InWork) Rows.Add(Row(order, client));

            Selected = null;
            foreach (OrderRow row in Rows)
            {
                if (row.Id == SelectedId) Selected = row;
            }
            BuildDetail(client);
        }

        private static OrderRow Row(Order order, ISimulationClient client)
        {
            WorldState world = client.World;
            long now = world.Time.TotalHours;
            var row = new OrderRow
            {
                Id = order.Id,
                Type = TypeName(client.Data, order.TypeId),
                Rank = order.Rank,
                Client = order.Client?.Nominative ?? string.Empty,
                Distance = order.Distance == OrderDistance.Far ? UiStrings.Far : UiStrings.Near,
                Reward = order.Reward,
                Surcharge = order.Surcharge,
                InWork = order.IsInWork,
                QuestRunId = order.QuestRunId,
            };

            if (order.Status == OrderStatus.AwaitingPlayer)
                row.TimeLeft = string.Format(UiStrings.AwaitingAnswerFormat, UiFormat.Duration(order.AnswerDueAtHours - now, client.Calendar));
            else if (order.IsInWork)
                row.TimeLeft = UiStrings.OrderInWork;
            else if (order.ExpiresAtHours < 0 || order.ExpiresAtHours == long.MaxValue)
                row.TimeLeft = UiStrings.NoTerm;
            else
                row.TimeLeft = UiFormat.Duration(order.ExpiresAtHours - now, client.Calendar);

            if (order.QuestRunId != 0 && world.Quests.TryGetRun(order.QuestRunId, out QuestRun run))
            {
                foreach (int id in run.Departed) AddPerson(row, world, id);
            }
            else if (order.TakenBy != 0)
            {
                AddPerson(row, world, order.TakenBy);
            }

            var marks = new List<string>();
            if (order.IsImportant) marks.Add(UiStrings.ImportantMark);
            if (order.IsEventQuest) marks.Add(UiStrings.EventQuestMark);
            if (order.IsPromotion) marks.Add(UiStrings.PromotionMark);
            row.Marks = string.Join(", ", marks);
            return row;
        }

        private static void AddPerson(OrderRow row, WorldState world, int id)
        {
            if (world.Adventurers.TryGetKnown(id, out Adventurer person)) row.TakenBy.Add((id, person.Name));
        }

        private void BuildDetail(ISimulationClient client)
        {
            DetailHidden = string.Empty;
            if (Selected == null || !client.World.Orders.TryGetOrder(Selected.Id, out Order order))
            {
                DetailTitle = UiStrings.SelectOrder;
                DetailDescription = string.Empty;
                DetailFacts = string.Empty;
                return;
            }

            DetailTitle = $"{Selected.Type} · {UiFormat.Rank(order.Rank)} · {Selected.Client}";
            DetailDescription = order.Description ?? string.Empty;
            string surcharge = order.Surcharge > 0 ? $" + {UiFormat.Money(order.Surcharge)}" : string.Empty;
            DetailFacts = $"{order.Place?.Nominative} · {Selected.Distance} · {UiStrings.ColReward.ToLowerInvariant()} {UiFormat.Money(order.Reward)}{surcharge} · {Selected.TimeLeft}";
            if (RevealAll) DetailHidden = Hidden(order, client);
        }

        /// <summary>Отладка: профиль требований по осям и шанс раунда, если задание идёт.</summary>
        public static string Hidden(Order order, ISimulationClient client)
        {
            float[] profile = DebugQueries.OrderProfile(order);
            var builder = new StringBuilder(UiStrings.HiddenProfile).Append(": ");
            bool first = true;
            foreach (StatId stat in client.Data.Stats.RadarOrder)
            {
                int index = (int)stat;
                if (index >= profile.Length || profile[index] <= 0.5f) continue;
                if (!first) builder.Append(", ");
                builder.Append(client.Data.Stats.Get(stat).DisplayName).Append(' ').Append(profile[index].ToString("0", CultureInfo.InvariantCulture));
                first = false;
            }
            if (order.QuestRunId != 0 && client.World.Quests.TryGetRun(order.QuestRunId, out QuestRun run) && run.Phase != QuestPhase.Returned)
            {
                builder.Append(" · ").AppendFormat(CultureInfo.InvariantCulture, UiStrings.RoundChanceFormat,
                    DebugQueries.RoundChance(run, client.World, client.Data) * 100f);
            }
            return builder.ToString();
        }

        public static string TypeName(DataRegistry data, string typeId) =>
            data.TryGet(typeId, out QuestTypeDefinition type) ? type.DisplayName : typeId;
    }
}
