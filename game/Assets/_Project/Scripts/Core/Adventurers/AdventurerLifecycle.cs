namespace GuildMaster.Core
{
    /// <summary>
    /// Уход из гильдии: человек переходит из активных в архив и остаётся там для отчёта месяца и лент.
    /// Когда и почему уходят, решает вызывающая система; событие
    /// со своим типом и автопаузой публикуют они. Отношения и черты партнёров не трогаются.
    /// Долг гильдии погибшего или пропавшего списывается (закон о долгах — вне прототипа).
    /// </summary>
    public static class AdventurerLifecycle
    {
        public static void Retire(SimContext ctx, Adventurer adventurer, LeaveReason reason)
        {
            ctx.World.Adventurers.MoveToArchive(adventurer);
            adventurer.LeftAtHours = ctx.World.Time.TotalHours;
            adventurer.LeaveReason = reason;
            adventurer.State.InInfirmary = false;
            if (reason == LeaveReason.Died || reason == LeaveReason.Disappeared) adventurer.State.DebtToGuild = 0; // долг пропадает
        }
    }
}
