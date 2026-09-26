namespace GuildMaster.Core
{
    /// <summary>
    /// Занятие человека в текущем часу: от него зависят усталость и стресс за час.
    /// Вне задания ставит расписание по фазам дня (<see cref="ActivitySystem"/>) или системы (срыв).
    /// Занятия на задании расписание не трогает.
    /// </summary>
    public enum Activity
    {
        /// <summary>Отдых в гильдии или в городе.</summary>
        Resting,

        /// <summary>Сон: общежитие или город.</summary>
        Sleeping,

        /// <summary>Таверна (вечер; днём — Пьяница, пропускающий день).</summary>
        Tavern,

        /// <summary>Тренировочный двор.</summary>
        Training,

        /// <summary>Лазарет: койка есть и есть Лекарь гильдии.</summary>
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

    /// <summary>Срыв при высоком стрессе. Вид зависит от черт.</summary>
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
    /// Вне задания — <see cref="None"/>: такие эффекты не действуют.
    /// </summary>
    public enum PartyContext
    {
        None,
        Solo,
        InGroup,
    }
}
