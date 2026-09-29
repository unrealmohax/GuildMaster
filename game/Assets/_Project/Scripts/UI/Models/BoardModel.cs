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

    /// <summary>Заказ, который ждёт ответа игрока: важный заказ или событийное задание.</summary>
    public sealed class AwaitingRow
    {
        public int Id;
        public bool IsEventQuest;
        public string Text;

        /// <summary>Событийное задание: ранг заказа, на котором нашли находку, — подсказка для выбора ранга.</summary>
        public GuildRank SourceRank;
    }

    /// <summary>
    /// Экран «Доска»: слева — заказы на доске и в работе и те, что ждут решения игрока, справа — выбранный заказ с описанием
    /// и намёками. Скрытый профиль и шанс видны только в режиме «Раскрыть всё» отладки (<see cref="RevealAll"/>).
    /// </summary>
    public sealed class BoardModel
    {
        /// <summary>Ранги, которые игрок может назначить событийному заданию.</summary>
        public static readonly GuildRank[] EventQuestRanks = { GuildRank.G, GuildRank.F, GuildRank.E, GuildRank.D, GuildRank.C };

        public List<OrderRow> Rows { get; } = new List<OrderRow>();

        /// <summary>Важные заказы и событийные задания без ответа, по порядку прихода.</summary>
        public List<AwaitingRow> Awaiting { get; } = new List<AwaitingRow>();

        /// <summary>Выбранный заказ висит на доске — ему можно назначить доплату.</summary>
        public bool CanSetSurcharge { get; private set; }

        /// <summary>Доплата выбранного заказа (0 — нет).</summary>
        public int SelectedSurcharge { get; private set; }

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

            Awaiting.Clear();
            foreach (Order order in world.Orders.Open)
            {
                if (order.Status == OrderStatus.AwaitingPlayer) Awaiting.Add(AwaitingOf(order, client));
            }

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

        private static AwaitingRow AwaitingOf(Order order, ISimulationClient client)
        {
            string type = TypeName(client.Data, order.TypeId);
            string left = UiFormat.Duration(order.AnswerDueAtHours - client.World.Time.TotalHours, client.Calendar);
            return new AwaitingRow
            {
                Id = order.Id,
                IsEventQuest = order.IsEventQuest,
                SourceRank = order.SourceRank,
                Text = order.IsEventQuest
                    ? string.Format(UiStrings.AwaitingEventFormat, type, order.Place?.Nominative, left)
                    : string.Format(UiStrings.AwaitingImportantFormat, type, UiFormat.Rank(order.Rank), order.Client?.Nominative, left),
            };
        }

        private static void AddPerson(OrderRow row, WorldState world, int id)
        {
            if (world.Adventurers.TryGetKnown(id, out Adventurer person)) row.TakenBy.Add((id, person.Name));
        }

        private void BuildDetail(ISimulationClient client)
        {
            DetailHidden = string.Empty;
            CanSetSurcharge = false;
            SelectedSurcharge = 0;
            if (Selected == null || !client.World.Orders.TryGetOrder(Selected.Id, out Order order))
            {
                DetailTitle = UiStrings.SelectOrder;
                DetailDescription = string.Empty;
                DetailFacts = string.Empty;
                return;
            }

            CanSetSurcharge = order.Status == OrderStatus.OnBoard;
            SelectedSurcharge = order.Surcharge;
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

    /// <summary>Тип заданий в правилах Регистратора: берёт ли он такие заказы.</summary>
    public sealed class RegistrarTypeRow
    {
        public string Id;
        public string Name;
        public bool Allowed;
    }

    /// <summary>
    /// Правила Регистратора и комиссия на доске. Изменение сразу уходит командой; пока мир её не применил (время идёт — до
    /// начала такта), следующая правка строится от уже отправленных правил, а не от старых в мире.
    /// </summary>
    public sealed class RegistrarModel
    {
        private RegistrarRules pending;

        public List<RegistrarTypeRow> Types { get; } = new List<RegistrarTypeRow>();
        public GuildRank MaxRank { get; private set; }
        public int MinReward { get; private set; }

        /// <summary>Комиссия в целых процентах.</summary>
        public int CommissionPercent { get; private set; }

        public int CommissionMinPercent { get; private set; }
        public int CommissionMaxPercent { get; private set; }

        /// <summary>Типы, которые приходят обычными заказами (их и отбирает Регистратор), по порядку данных.</summary>
        public static List<QuestTypeDefinition> RegistrarTypes(DataRegistry data)
        {
            var types = new List<QuestTypeDefinition>();
            foreach (QuestTypeDefinition type in data.All<QuestTypeDefinition>())
            {
                if (type.GenerationWeight > 0f) types.Add(type);
            }
            return types;
        }

        public void Refresh(ISimulationClient client)
        {
            RegistrarRules rules = Current(client);
            Types.Clear();
            foreach (QuestTypeDefinition type in RegistrarTypes(client.Data))
            {
                Types.Add(new RegistrarTypeRow { Id = type.Id, Name = type.DisplayName, Allowed = rules.IsTypeAllowed(type.Id) });
            }
            MaxRank = rules.MaxRank;
            MinReward = rules.MinReward;

            FloatRange limits = client.Data.Balance.Economy.CommissionLimits;
            CommissionPercent = Percent(client.World.Treasury.Commission);
            CommissionMinPercent = Percent(limits.Min);
            CommissionMaxPercent = Percent(limits.Max);
        }

        public void SetTypeAllowed(ISimulationClient client, string typeId, bool allowed)
        {
            RegistrarRules rules = Current(client);
            List<QuestTypeDefinition> all = RegistrarTypes(client.Data);
            var chosen = new List<string>();
            foreach (QuestTypeDefinition type in all)
            {
                bool on = type.Id == typeId ? allowed : rules.IsTypeAllowed(type.Id);
                if (on) chosen.Add(type.Id);
            }
            Send(client, new RegistrarRules(chosen.Count == all.Count ? null : chosen, rules.MaxRank, rules.MinReward));
        }

        public void SetMaxRank(ISimulationClient client, GuildRank rank)
        {
            RegistrarRules rules = Current(client);
            Send(client, new RegistrarRules(TypesOf(rules), rank, rules.MinReward));
        }

        public void SetMinReward(ISimulationClient client, int amount)
        {
            RegistrarRules rules = Current(client);
            Send(client, new RegistrarRules(TypesOf(rules), rules.MaxRank, System.Math.Max(0, amount)));
        }

        public static void SetCommissionPercent(ISimulationClient client, int percent) =>
            client.Send(new SetCommissionCommand(percent / 100f));

        private RegistrarRules Current(ISimulationClient client)
        {
            RegistrarRules world = client.World.Orders.Rules;
            if (pending != null && world.IsSameAs(pending)) pending = null;
            return pending ?? world;
        }

        private void Send(ISimulationClient client, RegistrarRules rules)
        {
            pending = rules;
            client.Send(new SetRegistrarRulesCommand(rules));
            Refresh(client);
        }

        private static IEnumerable<string> TypesOf(RegistrarRules rules) => rules.AllTypes ? null : rules.AllowedTypes;

        private static int Percent(float fraction) => (int)System.Math.Round(fraction * 100f);
    }

    /// <summary>Действия игрока с заказами на доске: доплата и ответы на заказы, которые ждут решения. Все — командами.</summary>
    public static class BoardActions
    {
        public static void SetSurcharge(ISimulationClient client, int orderId, int amount) =>
            client.Send(new SetSurchargeCommand(orderId, System.Math.Max(0, amount)));

        /// <summary>Важный заказ: принять (с доплатой) или отклонить.</summary>
        public static void AnswerImportant(ISimulationClient client, int orderId, bool accept, int surcharge = 0) =>
            client.Send(new AnswerImportantOrderCommand(orderId, accept, System.Math.Max(0, surcharge)));

        /// <summary>Событийное задание: назначить ранг или отказаться (<c>null</c>).</summary>
        public static void AnswerEventQuest(ISimulationClient client, int orderId, GuildRank? rank) =>
            client.Send(new AnswerEventQuestCommand(orderId, rank));
    }
}
