namespace GuildMaster.Core
{
    /// <summary>
    /// Вид автопаузы — переключатель в настройках (экран «Время»).
    /// Какие события к нему относятся — <see cref="AutopauseRules"/>.
    /// </summary>
    public enum AutopauseKind
    {
        /// <summary>Гибель авантюриста.</summary>
        AdventurerDied,

        /// <summary>Бегство авантюриста.</summary>
        AdventurerFled,

        /// <summary>Катастрофа на задании.</summary>
        QuestCatastrophe,

        /// <summary>Отступление группы.</summary>
        PartyRetreated,

        /// <summary>Раскрытие скрытой черты.</summary>
        TraitRevealed,

        /// <summary>Уход авантюриста или сотрудника из гильдии.</summary>
        MemberLeftGuild,

        /// <summary>Начало банкротства.</summary>
        BankruptcyStarted,

        /// <summary>Новое обращение — дилемма.</summary>
        DilemmaReceived,

        /// <summary>Важный заказ ждёт решения игрока.</summary>
        ImportantOrder,
    }
}
