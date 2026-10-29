using GuildMaster.Core;

namespace GuildMaster.UI
{
    /// <summary>
    /// Что показывает верхняя панель: дата, время года и фаза дня, скорость, казна, репутация, люди, поселение (название, жители,
    /// Безопасность, уровень, отсчёт покидания), ближайший кризис, банкротство.
    /// </summary>
    public sealed class TopBarModel
    {
        public string Date { get; private set; }
        public string Phase { get; private set; }

        /// <summary>Время года: «весна».</summary>
        public string Season { get; private set; }

        /// <summary>Ближайший объявленный или идущий кризис: «Налог через 12 дн.: ~1 400»; пусто — нет.</summary>
        public string Crisis { get; private set; }
        public int Money { get; private set; }
        public bool MoneyNegative => Money < 0;
        public int Reputation { get; private set; }
        public string Headcount { get; private set; }

        /// <summary>«Ольховка · 80 жителей · Безопасность 60».</summary>
        public string Settlement { get; private set; }

        public SettlementLevel SettlementLevel { get; private set; }

        /// <summary>«Уходят через 12 дн.»; пусто — отсчёта покидания нет.</summary>
        public string SettlementLeaving { get; private set; }

        /// <summary>«до закрытия: 3 мес. 12 дн.», «Гильдия закрыта»; пусто — банкротства нет.</summary>
        public string Bankruptcy { get; private set; }

        public bool Paused { get; private set; }

        /// <summary>Номер обычной скорости; при отладочной — −1.</summary>
        public int SpeedIndex { get; private set; }

        public bool DebugSpeed { get; private set; }

        public static TopBarModel Build(ISimulationClient client, GameClock clock)
        {
            WorldState world = client.World;
            var model = new TopBarModel
            {
                Date = UiFormat.Date(world.Time),
                Phase = UiFormat.Phase(client.Rhythm.PhaseAt(world.Time)),
                Season = CrisisModel.SeasonOf(client),
                Crisis = CrisisModel.TopLine(client),
                Money = world.Treasury.Money,
                Reputation = (int)System.Math.Round(world.Guild.Reputation),
                Headcount = $"{world.Adventurers.Active.Count} / {client.Data.Balance.Guild.MaxAdventurers}",
                Bankruptcy = string.Empty,
                Paused = clock == null || clock.Paused,
                DebugSpeed = clock != null && clock.DebugSpeed,
            };
            model.SpeedIndex = clock == null || clock.DebugSpeed ? -1 : clock.SpeedIndex;

            SettlementState settlement = world.Settlement;
            model.Settlement = string.Format(UiStrings.SettlementTopFormat, settlement.Name?.Nominative ?? UiStrings.SettlementDefaultName,
                UiFormat.Plural(settlement.Population, "житель", "жителя", "жителей"), UiFormat.Number(settlement.Security));
            model.SettlementLevel = settlement.Level;
            int daysLeft = SettlementRules.DaysUntilAbandoned(settlement, world.Time.TotalHours, client.Calendar, client.Data.Balance.Settlement);
            model.SettlementLeaving = daysLeft >= 0 ? string.Format(UiStrings.SettlementLeavingFormat, daysLeft) : string.Empty;

            Bankruptcy bankruptcy = world.Treasury.Bankruptcy;
            if (world.Treasury.IsClosed)
            {
                model.Bankruptcy = UiStrings.GuildClosed;
            }
            else if (bankruptcy.Active)
            {
                string left = UiFormat.Duration(bankruptcy.EndsAtHours - world.Time.TotalHours, client.Calendar);
                model.Bankruptcy = string.Format(UiStrings.BankruptcyFormat, left);
            }
            return model;
        }
    }
}
