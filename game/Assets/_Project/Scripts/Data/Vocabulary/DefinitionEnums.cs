namespace GuildMaster.Data
{
    // Кодовые id правил, которые делает код. Значения записаны в ассетах числами:
    // новые — только в конец своей группы, существующие не менять.

    /// <summary>Категория особой черты: не больше одной черты из категории.</summary>
    public enum TraitCategory
    {
        Past = 0,
        Habit = 1,
        Strength = 2,
        Relation = 3,
        Acquired = 4,
    }

    /// <summary>Особые правила черт, которые делает код. Числа — <see cref="TraitsBalance"/>.</summary>
    public enum TraitHook
    {
        /// <summary>Ветеран: после задания с провалом раунда — шанс срыва в запой.</summary>
        VeteranBinge = 1,
        /// <summary>Пьяница: утром шанс пропустить день в таверне.</summary>
        DrunkardSkipsDay = 2,
        /// <summary>Пьяница: утешение — только в таверне.</summary>
        DrunkardComfortOnlyInTavern = 3,
        /// <summary>Хвастун: репутация гильдии за каждый успех.</summary>
        BraggartReputation = 4,
        /// <summary>Железные нервы: стресс от гибели товарищей слабее.</summary>
        IronNervesComradeDeath = 5,
        /// <summary>Семейный: часть дохода — домой.</summary>
        FamilySendsMoneyHome = 6,
        /// <summary>Семейный: выше шанс ухода при ежемесячной проверке.</summary>
        FamilyLeaveChance = 7,
        /// <summary>Соперник: опыт на совместном задании выше, в одной группе — штраф к перекрытию.</summary>
        RivalPartner = 8,
        /// <summary>Влюблённый: синергия в одной группе, при гибели партнёра — стресс и «Потерявший товарища».</summary>
        LoverPartner = 9,
        /// <summary>Кошмары: шанс отказаться от заказа того же типа.</summary>
        NightmaresRefuseSimilar = 10,
        /// <summary>Потерявший товарища: по окончании срока — «сломался» или «ожесточился».</summary>
        GrievingOutcome = 11,
        /// <summary>Проверенный: разовая прибавка лояльности и Слаженности при появлении.</summary>
        TestedOnAcquire = 12,
    }

    /// <summary>Триггер раскрытия полюса оси или черты. Код вызывает раскрытие в своём месте.</summary>
    public enum RevealTrigger
    {
        None = 0,

        // Полюса осей
        CowardPanicOrFlee = 1,
        RecklessRush = 2,
        SelflessUnderpaidOrder = 3,
        GreedyRefusedPay = 4,
        LonerWentSolo = 5,
        TeamRefusedSolo = 6,
        LazyIdleDays = 7,
        AmbitiousTopOrder = 8,
        MercenaryLeaveCheck = 9,
        LoyalStayedInHardTimes = 10,
        UnprincipledCaughtSkimming = 11,
        HonestChoice = 12,

        // Особые черты
        VeteranFirstTension = 20,
        DeserterFled = 21,
        DrunkardSkippedDay = 22,
        BraggartOverreached = 23,
        IronNervesHeld = 24,
        FamilyRefusedDanger = 25,
        RivalFirstClash = 26,
        /// <summary>Раскрывается самой дилеммой (Влюблённый).</summary>
        ByDilemma = 27,
        NightmaresRefusedOrder = 28,
        /// <summary>Сразу при появлении (Калека, Проверенный).</summary>
        OnAcquire = 29,
        GrievingAfterDecline = 30,
    }

    /// <summary>Вид архетипа.</summary>
    public enum ArchetypeKind
    {
        Role = 0,
        JackOfAllTrades = 1,
        Novice = 2,
    }

    /// <summary>Из какой строки ранга брать значения профиля события: главные или второстепенные оси.</summary>
    public enum RankValueSource
    {
        MainAxes = 0,
        SecondaryAxes = 1,
    }

    /// <summary>Исход события в пути или находки.</summary>
    public enum OutcomeKind
    {
        Nothing = 0,
        LightWound = 1,
        HeavyWound = 2,
        Death = 3,
        Loot = 4,
    }

    public enum StaffLevelEffect
    {
        None = 0,
        /// <summary>Снятие стресса в таверне.</summary>
        TavernStressRelief = 1,
        /// <summary>Скорость лечения в Лазарете.</summary>
        HealingSpeed = 2,
    }

    public enum DecreeScopeKind
    {
        None = 0,
        Ranks = 1,
    }

    /// <summary>Срок распоряжения. Число дней — <see cref="DecreesBalance"/>.</summary>
    public enum DecreeDuration
    {
        Permanent = 0,
        Week = 1,
        Month = 2,
    }

    public enum DilemmaSource
    {
        Adventurer = 0,
        Staff = 1,
    }

    /// <summary>Код триггера дилеммы.</summary>
    public enum DilemmaTrigger
    {
        LoanRequest = 1,
        DeserterReturned = 2,
        LootDispute = 3,
        WoundedWantsQuest = 4,
        LoversSameParty = 10,
        TavernFeast = 13,
    }

    /// <summary>Кого касается эффект дилеммы.</summary>
    public enum DilemmaTarget
    {
        /// <summary>{имя} — кто обратился (в ссоре — {A}).</summary>
        Subject = 0,
        /// <summary>{напарник} — второй участник (в ссоре — {B}).</summary>
        Partner = 1,
        SubjectAndPartner = 2,
        /// <summary>Брошенные беглецом.</summary>
        Abandoned = 3,
        /// <summary>Все, кто вечером в таверне.</summary>
        AllInTavern = 4,
        AllAdventurers = 5,
        /// <summary>Гильдия (казна).</summary>
        Guild = 6,
    }

    /// <summary>Эффект варианта дилеммы. Число — в <c>DilemmaEffect.value</c>.</summary>
    public enum DilemmaEffectKind
    {
        /// <summary>Казна: +/− монет.</summary>
        Treasury = 0,
        Contentment = 1,
        Loyalty = 2,
        Stress = 3,
        Fatigue = 4,
        /// <summary>Отношения: у пары {имя}–{напарник}, у брошенных — к {имя}, у всех в таверне — попарно.</summary>
        Relation = 5,
        /// <summary>Доля кошелька — в казну (штраф).</summary>
        WalletToTreasury = 6,
        /// <summary>Выдать в долг долю запрошенной суммы (сумма считается триггером).</summary>
        GiveLoan = 7,
        /// <summary>Оплатить расходы на неделю и Лазарет до выздоровления.</summary>
        PayWeekAndTreatment = 8,
        RevealAxisPole = 9,
        RevealTrait = 10,
        SetMemoryFlag = 11,
        /// <summary>Выгнать из гильдии.</summary>
        Expel = 12,
        /// <summary>Можно брать задания с тяжёлой раной; число — множитель профиля.</summary>
        AllowQuestWhileWounded = 13,
        /// <summary>Разрешить одну группу; число — прибавка к оценке напарника.</summary>
        AllowSameParty = 14,
        ForbidSameParty = 15,
        /// <summary>Утаенная добыча возвращается в общий делёж.</summary>
        ReturnSkimmedLoot = 16,
        /// <summary>Довольство всем, если в гильдии недавно была гибель.</summary>
        ContentmentIfRecentDeath = 17,
    }

    /// <summary>Флаги памяти человека для цепочек.</summary>
    public enum MemoryFlag
    {
        TookLoan = 0,
        Fled = 1,
        Pardoned = 2,
        Punished = 3,
        WoundedQuestAllowed = 4,
        LoversTogether = 5,
        LoversSeparated = 6,
    }

    public enum FeedKind
    {
        Quest = 0,
        Guild = 1,
    }

    /// <summary>Важность строки ленты: [О], [З], [В]. Соответствует <c>GuildMaster.Core.EventImportance</c>.</summary>
    public enum FeedImportance
    {
        Normal = 0,
        Notable = 1,
        Important = 2,
    }

    /// <summary>Условие шаблона ленты.</summary>
    public enum FeedConditionKind
    {
        Solo = 0,
        Group = 1,
        Far = 2,
        Near = 3,
        Archetype = 4,
        QuestType = 5,
        /// <summary>Полюс оси у {имя} раскрыт.</summary>
        RevealedAxisPole = 6,
        /// <summary>Черта у {имя} раскрыта.</summary>
        RevealedTrait = 7,
        /// <summary>У Калеки снижена эта характеристика.</summary>
        MaimedStat = 8,
    }
}
