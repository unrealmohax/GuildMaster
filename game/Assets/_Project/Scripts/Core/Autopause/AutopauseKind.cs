namespace GuildMaster.Core
{
    /// <summary>
    /// Вид автопаузы — переключатель в настройках (ТЗ 03 → «Автопауза», экран «Время» ТЗ 15).
    /// Какие события к нему относятся — <see cref="AutopauseRules"/>.
    /// </summary>
    public enum AutopauseKind
    {
        /// <summary>Гибель авантюриста (ТЗ 09).</summary>
        AdventurerDied,

        /// <summary>Бегство авантюриста (ТЗ 09).</summary>
        AdventurerFled,

        /// <summary>Катастрофа на задании (ТЗ 09).</summary>
        QuestCatastrophe,

        /// <summary>Отступление группы (ТЗ 09).</summary>
        PartyRetreated,

        /// <summary>Раскрытие скрытой черты (ТЗ 04).</summary>
        TraitRevealed,

        /// <summary>Уход авантюриста или сотрудника из гильдии (ТЗ 05, 10).</summary>
        MemberLeftGuild,

        /// <summary>Начало банкротства (ТЗ 10).</summary>
        BankruptcyStarted,

        /// <summary>Новое обращение — дилемма (ТЗ 13).</summary>
        DilemmaReceived,
    }
}
