namespace GuildMaster.Core
{
    /// <summary>
    /// Типы событий симуляции. Системы добавляют свои типы в своих ТЗ (08–13).
    /// </summary>
    public enum SimEventType
    {
        /// <summary>Отладочное событие: тесты, отладочная панель (ТЗ 16).</summary>
        Debug,

        // Время (ТЗ 03)
        HourStarted,

        /// <summary>Новые сутки, 00:00.</summary>
        DayStarted,

        MorningStarted,

        /// <summary>Начало дневной фазы (09:00), не путать с <see cref="DayStarted"/>.</summary>
        DaytimeStarted,

        EveningStarted,
        NightStarted,
        MonthStarted,

        /// <summary>Игрок включил или выключил вид автопаузы (ТЗ 03).</summary>
        AutopauseChanged,

        // Авантюристы (ТЗ 04). Участник — человек; у черт с партнёром второй участник — партнёр.

        /// <summary>Пришёл кандидат в авантюристы, ждёт ответа. [О], без автопаузы.</summary>
        CandidateArrived,

        /// <summary>Кандидат не дождался ответа и ушёл.</summary>
        CandidateLeft,

        /// <summary>Игрок отказал кандидату.</summary>
        CandidateRejected,

        /// <summary>Игрок принял кандидата — человек вступил в гильдию.</summary>
        AdventurerJoined,

        /// <summary>Сменился архетип по параметрам. [О], без автопаузы.</summary>
        ArchetypeChanged,

        /// <summary>Раскрыт полюс оси. [В], автопауза <see cref="AutopauseKind.TraitRevealed"/>.</summary>
        AxisRevealed,

        /// <summary>Нейтральная ось раскрылась сама — «уравновешен». [З], без автопаузы (решение 2026-09-26).</summary>
        AxisBalanced,

        /// <summary>Появилась особая черта (скрытая, пока не раскрыта).</summary>
        TraitAcquired,

        /// <summary>Раскрыта особая черта. [В], автопауза <see cref="AutopauseKind.TraitRevealed"/>.</summary>
        TraitRevealed,

        /// <summary>Повышение ранга гильдии.</summary>
        RankPromoted,
    }
}
