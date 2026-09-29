using GuildMaster.Core;

namespace GuildMaster.UI
{
    /// <summary>Что показывает верхняя панель: дата и фаза дня, скорость, казна, репутация, люди, банкротство.</summary>
    public sealed class TopBarModel
    {
        public string Date { get; private set; }
        public string Phase { get; private set; }
        public int Money { get; private set; }
        public bool MoneyNegative => Money < 0;
        public int Reputation { get; private set; }
        public string Headcount { get; private set; }

        /// <summary>«до закрытия: 3 мес. 12 дн.»; пусто — банкротства нет.</summary>
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
                Money = world.Treasury.Money,
                Reputation = (int)System.Math.Round(world.Guild.Reputation),
                Headcount = $"{world.Adventurers.Active.Count} / {client.Data.Balance.Guild.MaxAdventurers}",
                Bankruptcy = string.Empty,
                Paused = clock == null || clock.Paused,
                DebugSpeed = clock != null && clock.DebugSpeed,
            };
            model.SpeedIndex = clock == null || clock.DebugSpeed ? -1 : clock.SpeedIndex;

            Bankruptcy bankruptcy = world.Treasury.Bankruptcy;
            if (bankruptcy.Active)
            {
                string left = UiFormat.Duration(bankruptcy.EndsAtHours - world.Time.TotalHours, client.Calendar);
                model.Bankruptcy = string.Format(UiStrings.BankruptcyFormat, left);
            }
            return model;
        }
    }
}
