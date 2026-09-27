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

        // Экономика гильдии.

        /// <summary>Игрок сменил комиссию: <c>from</c>, <c>to</c> — доли. [О].</summary>
        CommissionChanged,

        /// <summary>Казна в минусе дольше срока — началось банкротство: <c>endsAt</c> — час, когда истечёт срок. [В], автопауза.</summary>
        BankruptcyStarted,

        /// <summary>Казна снова не в минусе — банкротство снято. [З].</summary>
        BankruptcyLifted,

        /// <summary>
        /// Срок банкротства истёк при минусе — гильдия закрыта, игра проиграна, симуляция остановлена. Участники — лучшие люди
        /// гильдии; итоги — <c>days</c> (сколько прожили), <c>died</c>, <c>left</c>, <c>money</c>. [В].
        /// </summary>
        GuildClosed,

        /// <summary>Готов отчёт за прошедший месяц (<see cref="MonthReport"/>): <c>income</c>, <c>expense</c>, <c>count</c> — погибших. [З], без автопаузы.</summary>
        MonthReportReady,

        // Репутация и заказы. Id заказа — в данных события (order), вместе с типом, рангом, расстоянием, наградой и названиями.

        /// <summary>Изменилась репутация гильдии: <c>from</c>, <c>to</c>, <c>reason</c>. [О], без строки ленты.</summary>
        ReputationChanged,

        /// <summary>Пришёл заказ (до Регистратора): <c>important</c>, <c>hardEarly</c>. [О].</summary>
        OrderArrived,

        /// <summary>Заказ повешен на доску: <c>by</c> — registrar / player, <c>expiresAt</c>. [О].</summary>
        OrderPosted,

        /// <summary>Регистратор отклонил заказ по правилам игрока: <c>rules</c>. [О].</summary>
        OrderDeclinedByRegistrar,

        /// <summary>Важный заказ ждёт решения игрока: <c>answerDueAt</c>. [В], автопауза <see cref="AutopauseKind.ImportantOrder"/>.</summary>
        OrderAwaitingPlayer,

        /// <summary>Игрок отклонил важный заказ или не ответил вовремя (<c>noAnswer</c>). [О].</summary>
        OrderDeclinedByPlayer,

        /// <summary>Заказ никто не взял — снят с доски по сроку. [О].</summary>
        OrderExpired,

        /// <summary>За утро повешено на доску: <c>count</c>. [О].</summary>
        NewOrdersPosted,

        /// <summary>За утро Регистратор отказал заказчикам: <c>count</c>. [О].</summary>
        RegistrarDeclinedOrders,

        /// <summary>Игрок сменил правила Регистратора: <c>from</c>, <c>to</c>. [О].</summary>
        RegistrarRulesChanged,

        /// <summary>Игрок назначил доплату к заказу: <c>order</c>, <c>from</c>, <c>to</c>. [О].</summary>
        SurchargeSet,

        // Задания. Участник — человек (кто взял, кого касается); у событий задания в данных — id задания (quest), заказ, тип,
        // ранг, расстояние, «один ли» (solo), названия (place, enemy, client, cargo, party) и решающий (leader).

        /// <summary>Человек взял заказ с доски: выйдет в следующем часу. [О].</summary>
        OrderTaken,

        /// <summary>Человек отказался от выбранного заказа из-за Кошмаров — важное решение: <c>reason</c> — текст причины. [З].</summary>
        OrderRefused,

        /// <summary>Человеку открыто задание на повышение — экзамен на следующий ранг. [О].</summary>
        PromotionOffered,

        /// <summary>Группа вышла на задание. [О].</summary>
        QuestDeparted,

        /// <summary>День пути кончился, группа встала на ночлег. [О].</summary>
        TravelProgress,

        /// <summary>Ночь в пути прошла. [О].</summary>
        QuestCamp,

        /// <summary>Случайное событие в пути: <c>event</c> — id, <c>kind</c> — start / success / fail. [З] / [О].</summary>
        TravelEvent,

        /// <summary>Находка: <c>discovery</c> — id, <c>kind</c> — found / explore / skip / empty / loot / fail / reported.</summary>
        Discovery,

        /// <summary>Раунд удался: <c>chance</c>, <c>overlap</c>. [О].</summary>
        RoundSuccess,

        /// <summary>Раунд провален: <c>failed</c> — номер провала, <c>chance</c>, <c>overlap</c>. [З].</summary>
        RoundFail,

        /// <summary>Потери после провала: <c>kind</c> — time / stress / bonus / gear / loot.</summary>
        QuestLoss,

        /// <summary>Рана на задании (строка ленты задания): <c>kind</c> — shield / light / heavy / maimed.</summary>
        QuestWound,

        /// <summary>Решение группы после провала: <c>kind</c> — continue / doubt / argue; <c>reasons</c> — главные причины. [З].</summary>
        PartyDecision,

        /// <summary>Группа отступила. [З], автопауза <see cref="AutopauseKind.PartyRetreated"/>.</summary>
        PartyRetreated,

        /// <summary>Момент напряжения: <c>kind</c> — panic / rush / hero / hold / breakdown.</summary>
        TensionMoment,

        /// <summary>Человек сбежал с задания: <c>reason</c> — причина. [В], автопауза <see cref="AutopauseKind.AdventurerFled"/>.</summary>
        AdventurerFled,

        /// <summary>Сработал потолок: раунд провален сам. [З].</summary>
        CeilingTriggered,

        /// <summary>Синергия пары в раунде: <c>kind</c> — friends / lovers / rivals.</summary>
        Synergy,

        /// <summary>Человек погиб на задании: <c>all</c> — погибли все. [В], автопауза <see cref="AutopauseKind.AdventurerDied"/>.</summary>
        AdventurerDied,

        /// <summary>Группа не вернулась — погибли все. [В].</summary>
        QuestLost,

        /// <summary>Из группы выжил только беглец. [В].</summary>
        OnlyFugitiveSurvived,

        /// <summary>Лекарь спас умиравшего — тот стал Калекой: <c>medic</c>. [В].</summary>
        MedicSaved,

        /// <summary>Группа повернула к дому: <c>kind</c> — hard (несут раненых) / normal.</summary>
        ReturnTrip,

        /// <summary>
        /// Задание кончилось, кто-то вернулся: <c>kind</c> — уровень (brilliant / success / partial / fail / catastrophe), <c>done</c>,
        /// <c>count</c> — вернулось, <c>total</c> — вышло.
        /// </summary>
        QuestReturned,

        /// <summary>Катастрофа на задании (без строки). Автопауза <see cref="AutopauseKind.QuestCatastrophe"/>.</summary>
        QuestCatastrophe,

        /// <summary>Трофеи сданы трактирщику: <c>amount</c>. [О].</summary>
        LootHandedIn,

        /// <summary>Беспринципный утаил часть трофеев: <c>amount</c>, <c>caught</c>. [О].</summary>
        LootSkimmed,

        /// <summary>Беглец вернулся в гильдию. [В].</summary>
        DeserterReturned,

        /// <summary>Беглец не вернулся — исчез, ушёл из гильдии. [В], автопауза <see cref="AutopauseKind.MemberLeftGuild"/>.</summary>
        AdventurerDisappeared,

        /// <summary>Событийное задание из находки ждёт ранга от игрока. [В], автопауза <see cref="AutopauseKind.ImportantOrder"/>.</summary>
        EventQuestAwaitingPlayer,

        /// <summary>Игрок ответил на событийное задание: <c>rank</c> или отказ (<c>declined</c>, <c>noAnswer</c>). [О].</summary>
        EventQuestAnswered,

        /// <summary>Экзамен на повышение: <c>passed</c>. [З].</summary>
        PromotionExam,

        /// <summary>Выплаты задания: <c>reward</c>, <c>commission</c>, <c>surcharge</c>, <c>trophies</c>. [О], без строки.</summary>
        QuestPaid,

        /// <summary>
        /// Группа под задание собралась: участники — инициатор, затем остальные по порядку согласия; <c>order</c>, <c>count</c> —
        /// сколько ещё, кроме первых двух, <c>total</c> — всего. [О].
        /// </summary>
        PartyGathered,

        /// <summary>Группа не собралась: участник — инициатор; <c>order</c>, <c>solo</c> — пошёл один. [О].</summary>
        PartyNotGathered,

        /// <summary>
        /// Приглашённый отказался идти с группой: участники — он, инициатор; <c>order</c>, <c>cause</c> (мотив), <c>reason</c> —
        /// текст причины, <c>solo</c> — вместо этого взял заказ один. [О].
        /// </summary>
        InvitationDeclined,

        /// <summary>Сложилась постоянная группа: участники — члены; <c>party</c>, <c>title</c> — название, <c>count</c> — сколько ещё, кроме первых двух. [З].</summary>
        PermanentPartyFormed,

        /// <summary>Постоянная группа распалась: участники — кто в ней оставался; <c>party</c>, <c>cause</c>. [З].</summary>
        PermanentPartyDisbanded,

        /// <summary>
        /// Человек ушёл из постоянной группы: <c>party</c>, <c>cause</c> — quarrel (ссора, второй участник — с кем), loner
        /// (одиночка), gone (погиб или ушёл из гильдии). [О].
        /// </summary>
        PartyMemberLeft,

        /// <summary>Гость стал членом постоянной группы: <c>party</c>. [О].</summary>
        PartyMemberJoined,
    }
}
