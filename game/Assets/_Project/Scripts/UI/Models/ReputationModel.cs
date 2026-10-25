using System.Collections.Generic;
using System.Globalization;
using GuildMaster.Core;

namespace GuildMaster.UI
{
    /// <summary>
    /// Изменение репутации строкой: время, сдвиг, подпись («Охота E: выполнено», «уход: Ян») и ссылка на заказ или человека
    /// (<see cref="TextLinkKind.None"/> — без ссылки).
    /// </summary>
    public sealed class ReputationChangeRow
    {
        public string Time;
        public float Delta;
        public string Text;
        public TextLink Link;
    }

    /// <summary>Блок «Репутация» на экране «Казна»: текущая репутация и её последние изменения с причинами (новые сверху).</summary>
    public sealed class ReputationModel
    {
        public float Reputation { get; private set; }
        public float MaxReputation { get; private set; }

        public List<ReputationChangeRow> Changes { get; } = new List<ReputationChangeRow>();

        public static ReputationModel Build(ISimulationClient client)
        {
            GuildState guild = client.World.Guild;
            var model = new ReputationModel
            {
                Reputation = guild.Reputation,
                MaxReputation = client.Data.Balance.Guild.MaxReputation,
            };
            IReadOnlyList<ReputationChange> changes = guild.Changes;
            for (int i = changes.Count - 1; i >= 0; i--) model.Changes.Add(Row(client, changes[i]));
            return model;
        }

        /// <summary>Подпись изменения: «Охота E: выполнено», «уход: Ян», «сутки без выполненных заданий».</summary>
        public static string ChangeText(ISimulationClient client, ReputationChange change)
        {
            string reason = UiText.Render(client.Data, UiTextKeys.ReputationReasonPrefix + change.Reason);
            if (change.AdventurerId != 0)
            {
                Adventurer person = EventTextSource.FindPerson(client.World, change.AdventurerId);
                if (person != null) return $"{reason}: {person.Name}";
            }
            if (change.OrderId != 0) return $"{BoardModel.TypeName(client.Data, change.QuestTypeId)} {UiFormat.Rank(change.Rank)}: {reason}";
            return reason;
        }

        /// <summary>
        /// Сдвиг репутации со знаком: до двух знаков после запятой, а если это ноль — до трёх («+0,01», «−1», «+0,004»).
        /// </summary>
        public static string Delta(float delta)
        {
            float abs = System.Math.Abs(delta);
            string number = abs.ToString("0.##", CultureInfo.InvariantCulture);
            if (number == "0") number = abs.ToString("0.###", CultureInfo.InvariantCulture);
            return (delta >= 0 ? "+" : "−") + number.Replace('.', ',');
        }

        private static ReputationChangeRow Row(ISimulationClient client, ReputationChange change)
        {
            TextLink link = default;
            if (change.AdventurerId != 0 && EventTextSource.FindPerson(client.World, change.AdventurerId) != null)
                link = new TextLink(TextLinkKind.Adventurer, change.AdventurerId);
            else if (change.OrderId != 0)
                link = new TextLink(TextLinkKind.Order, change.OrderId);

            return new ReputationChangeRow
            {
                Time = UiFormat.Stamp(client.Calendar.At(change.TimeHours)),
                Delta = change.Delta,
                Text = ChangeText(client, change),
                Link = link,
            };
        }
    }
}
