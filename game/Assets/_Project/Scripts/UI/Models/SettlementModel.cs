using System.Collections.Generic;
using GuildMaster.Core;
using GuildMaster.Data;

namespace GuildMaster.UI
{
    /// <summary>Изменение Безопасности строкой: время, сдвиг, подпись причины («Охота E снят по сроку»), заказ (0 — без заказа).</summary>
    public sealed class SettlementChangeRow
    {
        public string Time;
        public float Delta;
        public string Text;
        public int OrderId;
    }

    /// <summary>
    /// Вкладка «Поселение» и строка поселения на верхней панели: название, население и его изменение с начала месяца, Безопасность
    /// и уровень, пороги уровней, что будет дальше, отсчёт покидания, последние изменения Безопасности (новые сверху).
    /// </summary>
    public sealed class SettlementModel
    {
        public string Name { get; private set; }
        public int Population { get; private set; }

        /// <summary>Изменение населения с последнего отчёта месяца (с начала игры — у первого месяца).</summary>
        public int PopulationChange { get; private set; }

        public float Security { get; private set; }
        public float MaxSecurity { get; private set; }
        public SettlementLevel Level { get; private set; }
        public string LevelName { get; private set; }

        /// <summary>Что будет дальше при этом уровне; идёт отсчёт покидания — он.</summary>
        public string Next { get; private set; }

        /// <summary>Суток до покидания; −1 — отсчёта нет.</summary>
        public int DaysLeft { get; private set; }

        /// <summary>Пороги уровней (спокойно, норма, тревога) — отметки на полосе.</summary>
        public float[] Thresholds { get; private set; }

        public List<SettlementChangeRow> Changes { get; } = new List<SettlementChangeRow>();

        public static SettlementModel Build(ISimulationClient client)
        {
            WorldState world = client.World;
            SettlementState settlement = world.Settlement;
            SettlementBalance balance = client.Data.Balance.Settlement;
            MonthReport last = world.Reports.GetLast();
            int monthStart = last != null ? last.SettlementAtEnd.Population : settlement.StartPopulation;

            var model = new SettlementModel
            {
                Name = settlement.Name?.Nominative ?? UiStrings.SettlementDefaultName,
                Population = settlement.Population,
                PopulationChange = settlement.Population - monthStart,
                Security = settlement.Security,
                MaxSecurity = balance.MaxSecurity,
                Level = settlement.Level,
                LevelName = LevelText(client.Data, settlement.Level),
                DaysLeft = SettlementRules.DaysUntilAbandoned(settlement, world.Time.TotalHours, client.Calendar, balance),
                Thresholds = new[] { balance.CalmFrom, balance.NormalFrom, balance.AlarmFrom },
            };
            model.Next = model.DaysLeft >= 0
                ? string.Format(UiStrings.SettlementAbandonFormat, model.DaysLeft)
                : UiText.Render(client.Data, NextKey(settlement.Level));

            IReadOnlyList<SettlementChange> changes = settlement.Changes;
            for (int i = changes.Count - 1; i >= 0; i--) model.Changes.Add(Row(client, changes[i]));
            return model;
        }

        /// <summary>Название уровня: «спокойно», «норма», «тревога», «осада».</summary>
        public static string LevelText(DataRegistry data, SettlementLevel level) => UiText.Render(data, LevelKey(level));

        /// <summary>Подпись изменения: «Охота E снят по сроку», «ночь».</summary>
        public static string ChangeText(ISimulationClient client, SettlementChange change)
        {
            string reason = UiText.Render(client.Data, ReasonKey(change.Reason));
            if (change.OrderId == 0) return reason;
            return $"{BoardModel.TypeName(client.Data, change.QuestTypeId)} {UiFormat.Rank(change.Rank)} {reason}";
        }

        /// <summary>Сдвиг Безопасности со знаком и одним знаком после запятой: «+2,5», «−0,3».</summary>
        public static string Delta(float delta)
        {
            string number = System.Math.Abs(delta).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',');
            return (delta >= 0 ? "+" : "−") + number;
        }

        private static SettlementChangeRow Row(ISimulationClient client, SettlementChange change) => new SettlementChangeRow
        {
            Time = UiFormat.Stamp(client.Calendar.At(change.TimeHours)),
            Delta = change.Delta,
            Text = ChangeText(client, change),
            OrderId = change.OrderId,
        };

        private static string LevelKey(SettlementLevel level)
        {
            switch (level)
            {
                case SettlementLevel.Calm: return UiTextKeys.SettlementCalm;
                case SettlementLevel.Normal: return UiTextKeys.SettlementNormal;
                case SettlementLevel.Alarm: return UiTextKeys.SettlementAlarm;
                default: return UiTextKeys.SettlementSiege;
            }
        }

        private static string NextKey(SettlementLevel level)
        {
            switch (level)
            {
                case SettlementLevel.Calm: return UiTextKeys.SettlementNextCalm;
                case SettlementLevel.Normal: return UiTextKeys.SettlementNextNormal;
                case SettlementLevel.Alarm: return UiTextKeys.SettlementNextAlarm;
                default: return UiTextKeys.SettlementNextSiege;
            }
        }

        private static string ReasonKey(string reason) => UiTextKeys.SettlementReasonPrefix + reason;
    }
}
