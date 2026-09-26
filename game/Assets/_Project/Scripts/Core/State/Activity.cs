namespace GuildMaster.Core
{
    /// <summary>
    /// Занятие человека в текущем часу (ТЗ 05 → «Занятие»): от него зависят усталость и стресс за час.
    /// Выбирает модель решений (ТЗ 06) или задают системы; до ТЗ 06 — расписание-заглушка <see cref="ActivitySystem"/>.
    /// Занятия на задании ставит QuestSystem (ТЗ 09), расписание их не трогает.
    /// </summary>
    public enum Activity
    {
        /// <summary>Отдых в гильдии или в городе.</summary>
        Resting,

        /// <summary>Сон: общежитие или город.</summary>
        Sleeping,

        /// <summary>Таверна (вечер; днём — Пьяница, пропускающий день).</summary>
        Tavern,

        /// <summary>Тренировочный двор (ТЗ 11).</summary>
        Training,

        /// <summary>Лазарет: койка есть и есть Лекарь гильдии (ТЗ 11).</summary>
        Infirmary,

        /// <summary>Запой (срыв): в таверне.</summary>
        Binge,

        /// <summary>Задание: путь.</summary>
        OnQuestTravel,

        /// <summary>Задание: раунд.</summary>
        OnQuestRound,

        /// <summary>Задание: ночлег в пути.</summary>
        OnQuestCamp,
    }

    /// <summary>Срыв при высоком стрессе (ТЗ 05 → «Срыв»). Вид зависит от черт.</summary>
    public enum BreakdownKind
    {
        None,

        /// <summary>Запой: занятие <see cref="Activity.Binge"/>, тратит на выпивку. Пьяница или Ветеран войны.</summary>
        Binge,

        /// <summary>Драка в гильдии: мгновенно. Безрассудный.</summary>
        Brawl,

        /// <summary>Отказ от заданий. Трус.</summary>
        RefuseQuests,

        /// <summary>«Сел и не смог подняться»: только отдых. Остальные.</summary>
        Collapse,
    }

    /// <summary>
    /// С кем человек сейчас на задании — для эффектов черт с условием «в группе» / «один» (Одиночка, Командный).
    /// Вне задания — <see cref="None"/>: такие эффекты не действуют. Группы — ТЗ 07, задания — ТЗ 09.
    /// </summary>
    public enum PartyContext
    {
        None,
        Solo,
        InGroup,
    }
}
