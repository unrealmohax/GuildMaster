namespace GuildMaster.Core
{
    /// <summary>Фаза дня (ТЗ 03 → «Ритм дня»). Границы — часы из <see cref="GuildMaster.Data.TimeBalance"/>.</summary>
    public enum DayPhase
    {
        Morning,
        Day,
        Evening,
        Night,
    }
}
