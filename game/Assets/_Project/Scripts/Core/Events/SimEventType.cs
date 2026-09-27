namespace GuildMaster.Core
{
    /// <summary>
    /// Типы событий симуляции. Новые типы добавляются в конец.
    /// </summary>
    public enum SimEventType
    {
        /// <summary>Отладочное событие: тесты, отладочная панель.</summary>
        Debug,

        // Время
        HourStarted,

        /// <summary>Новые сутки, 00:00.</summary>
        DayStarted,

        MorningStarted,

        /// <summary>Начало дневной фазы (09:00), не путать с <see cref="DayStarted"/>.</summary>
        DaytimeStarted,

        EveningStarted,
        NightStarted,
        MonthStarted,

        /// <summary>Игрок включил или выключил вид автопаузы.</summary>
        AutopauseChanged,

        // Авантюристы. Участник — человек; у черт с партнёром второй участник — партнёр.

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

        /// <summary>Нейтральная ось раскрылась сама — «уравновешен». [З], без автопаузы.</summary>
        AxisBalanced,

        /// <summary>Появилась особая черта (скрытая, пока не раскрыта).</summary>
        TraitAcquired,

        /// <summary>Раскрыта особая черта. [В], автопауза <see cref="AutopauseKind.TraitRevealed"/>.</summary>
        TraitRevealed,

        /// <summary>Повышение ранга гильдии.</summary>
        RankPromoted,

        // Состояние и здоровье. Участник — человек.

        /// <summary>Рана: лёгкая [З], тяжёлая [В]; <c>maimed</c> — вторая тяжёлая стала увечьем.</summary>
        AdventurerWounded,

        /// <summary>Тяжёлая рана осложнилась (+7 дней, стресс). [З].</summary>
        WoundComplicated,

        /// <summary>Рана зажила. [О].</summary>
        WoundHealed,

        /// <summary>Срыв при высоком стрессе: запой, драка (второй участник — с кем), отказ, «сел и не смог подняться». [В], без автопаузы.</summary>
        Breakdown,

        /// <summary>Пьяница пропускает день в таверне. [О].</summary>
        DrunkardSkippedDay,

        /// <summary>Не хватило денег: флаг «кошелёк пуст». [З].</summary>
        WalletEmptied,

        /// <summary>
        /// Ушёл из гильдии по ежемесячной проверке. [В], автопауза <see cref="AutopauseKind.MemberLeftGuild"/>. <c>cause</c> — главная
        /// причина (<see cref="LeaveCause"/>), <c>causes</c> — все, <c>reason</c> — текст причин для игрока.
        /// </summary>
        AdventurerLeft,

        /// <summary>Спад «Потерявшего товарища» кончился: <c>outcome</c> — сломался / ожесточился. Второй участник — погибший.</summary>
        GrievingEnded,

        // Решения. Участники — двое.

        /// <summary>Ссора вечером в таверне: <c>relation</c> — отношения после, <c>rivals</c> — пара Соперников. [З].</summary>
        Quarrel,
    }
}
