using System.Collections.Generic;

namespace GuildMaster.Core
{
    /// <summary>
    /// Какие события ставят автопаузу и под каким переключателем.
    /// Система, которая заводит такое событие, добавляет сюда одну строку
    /// <c>{ SimEventType.Тип, AutopauseKind.Вид },</c>. Одному виду может соответствовать несколько событий.
    /// </summary>
    public static class AutopauseRules
    {
        public static IReadOnlyDictionary<SimEventType, AutopauseKind> Default { get; } =
            new Dictionary<SimEventType, AutopauseKind>
            {
                { SimEventType.AxisRevealed, AutopauseKind.TraitRevealed },
                { SimEventType.TraitRevealed, AutopauseKind.TraitRevealed },
                { SimEventType.AdventurerLeft, AutopauseKind.MemberLeftGuild },
                { SimEventType.BankruptcyStarted, AutopauseKind.BankruptcyStarted },
                { SimEventType.OrderAwaitingPlayer, AutopauseKind.ImportantOrder },
                { SimEventType.EventQuestAwaitingPlayer, AutopauseKind.ImportantOrder },
                { SimEventType.AdventurerDied, AutopauseKind.AdventurerDied },
                { SimEventType.AdventurerFled, AutopauseKind.AdventurerFled },
                { SimEventType.QuestCatastrophe, AutopauseKind.QuestCatastrophe },
                { SimEventType.PartyRetreated, AutopauseKind.PartyRetreated },
                { SimEventType.AdventurerDisappeared, AutopauseKind.MemberLeftGuild },
            };
    }
}
