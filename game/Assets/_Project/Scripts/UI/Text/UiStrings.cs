using System.Collections.Generic;
using GuildMaster.Core;

namespace GuildMaster.UI
{
    /// <summary>
    /// Подписи интерфейса: пункты навигации, вкладки, заголовки столбцов, кнопки. Строки с игровым смыслом и склонением
    /// (занятие, фаза задания, рана, стадия постройки) — шаблоны <see cref="UiTextKeys"/> в данных.
    /// </summary>
    public static class UiStrings
    {
        // Навигация и вкладки
        public const string Guild = "Гильдия";
        public const string Board = "Доска";
        public const string Quests = "Задания";
        public const string People = "Люди";
        public const string Parties = "Группы";
        public const string Buildings = "Постройки";
        public const string Staff = "Персонал";
        public const string GuildFeed = "Лента гильдии";

        // Верхняя панель
        public const string Treasury = "Казна";
        public const string Reputation = "Репутация";
        public const string Headcount = "Люди";
        public const string Notifications = "Уведомления";
        public const string NoNotifications = "Уведомлений нет";
        public const string NotifyImportantFormat = "Важный заказ ждёт ответа: {0} {1}, {2}";
        public const string NotifyEventQuestFormat = "Событийное задание ждёт ответа: {0} {1}, {2}";
        public const string NotifyCandidateFormat = "Кандидат в гильдию: {0}, {1}";
        public const string NotifyStaffCandidateFormat = "Кандидат на вакансию «{0}»: {1}";
        public const string NotifyReportFormat = "Отчёт за месяц {0}, год {1}";
        public const string Pause = "||";
        public const string DateFormat = "День {0}, месяц {1}, год {2} · {3:00}:00";
        public const string BankruptcyFormat = "до закрытия: {0}";

        public static readonly string[] DayPhases = { "утро", "день", "вечер", "ночь" };

        // Время
        public const string Calendar = "Календарь";
        public const string Today = "Сегодня";
        public const string UntilMonthFormat = "До начала месяца: {0}";
        public const string Upcoming = "Ближайшие события";
        public const string NothingUpcoming = "Ничего не ожидается";
        public const string AutopauseSettings = "Автопауза";
        public const string CalendarConstructionFormat = "Готова постройка: {0}";
        public const string CalendarAnswerFormat = "Срок ответа на заказ: {0}, {1}";
        public const string CalendarReturnFormat = "Возвращение: {0}";
        public const string Close = "Закрыть";

        /// <summary>Переключатели автопаузы по видам. Нет строки — показывается имя вида.</summary>
        public static readonly Dictionary<AutopauseKind, string> AutopauseKinds = new Dictionary<AutopauseKind, string>
        {
            { AutopauseKind.AdventurerDied, "Гибель авантюриста" },
            { AutopauseKind.AdventurerFled, "Бегство с задания" },
            { AutopauseKind.QuestCatastrophe, "Катастрофа на задании" },
            { AutopauseKind.PartyRetreated, "Отступление группы" },
            { AutopauseKind.TraitRevealed, "Раскрытие черты" },
            { AutopauseKind.MemberLeftGuild, "Уход из гильдии" },
            { AutopauseKind.BankruptcyStarted, "Начало банкротства" },
            { AutopauseKind.DilemmaReceived, "Обращение" },
            { AutopauseKind.ImportantOrder, "Важный заказ" },
        };

        public static string AutopauseKindName(AutopauseKind kind) => AutopauseKinds.TryGetValue(kind, out string name) ? name : kind.ToString();

        // Люди
        public const string ColName = "Имя";
        public const string ColArchetype = "Архетип";
        public const string ColRank = "Ранг";
        public const string ColActivity = "Занятие";
        public const string ColWounds = "Раны";
        public const string ColFatigue = "Усталость";
        public const string ColStress = "Стресс";
        public const string ColContentment = "Довольство";
        public const string ColLoyalty = "Лояльность";
        public const string ColWallet = "Кошелёк";
        public const string FilterAll = "Все";
        public const string FilterOnQuest = "На заданиях";
        public const string FilterResting = "Отдыхают";
        public const string FilterTavern = "В таверне";
        public const string FilterTraining = "Тренируются";
        public const string FilterInfirmary = "Лечатся";
        public const string FilterBreakdown = "Срыв";
        public const string Candidates = "Кандидаты";
        public const string CandidateUntilFormat = "ждёт ещё {0}";
        public const string NoPeople = "Никого";

        // Группы
        public const string NoParties = "Постоянных групп нет";
        public const string PartyStatsFormat = "заданий вместе: {0}, удачных: {1}";
        public const string PartyOnQuest = "на задании";

        // Постройки и персонал
        public const string ColBuilding = "Постройка";
        public const string ColState = "Состояние";
        public const string ColCapacity = "Места";
        public const string ColRole = "Должность";
        public const string ColLevel = "Уровень";
        public const string ColSalary = "Зарплата";
        public const string ColSalaryDebt = "Долг";
        public const string NoStaff = "Персонала нет";
        public const string StaffCandidates = "Кандидаты на вакансии";
        public const string StaffCandidateFormat = "уровень {0}, просит {1}, ждёт ещё {2}";

        // Доска
        public const string ColType = "Тип";
        public const string ColClient = "Заказчик";
        public const string ColDistance = "Путь";
        public const string ColReward = "Награда";
        public const string ColSurcharge = "Доплата";
        public const string ColTimeLeft = "Висит";
        public const string ColTakenBy = "Взял";
        public const string Near = "близко";
        public const string Far = "далеко";
        public const string AwaitingAnswerFormat = "ждёт ответа: {0}";
        public const string OrderInWork = "в работе";
        public const string NoTerm = "без срока";
        public const string SelectOrder = "Выберите заказ";
        public const string OrderQuestLink = "Задание";
        public const string NoOrders = "Доска пуста";
        public const string ImportantMark = "важный";
        public const string EventQuestMark = "событийное";
        public const string PromotionMark = "экзамен";

        // Задания
        public const string ActiveQuests = "Идут";
        public const string ColPlace = "Место";
        public const string RecentQuests = "Недавно вернулись";
        public const string NoActiveQuests = "Никто не на задании";
        public const string SelectQuest = "Выберите задание";
        public const string QuestOrder = "Заказ";
        public const string QuestMembers = "Участники";
        public const string QuestFeed = "Лента задания";
        public const string FailedRounds = "Проваленные раунды";
        public const string Solo = "один";
        public const string Done = "выполнено";
        public const string NotDone = "не выполнено";
        public const string Panic = "паника";
        public const string TurnedBack = "повернул назад";
        public const string Fled = "сбежал";
        public const string Died = "погиб";

        // Карточка
        public const string RoleProfile = "Профиль ролей";
        public const string Characteristics = "Характеристики";
        public const string Skills = "Навыки";
        public const string Character = "Характер";
        public const string Traits = "Особые черты";
        public const string NoTraits = "не замечены";
        public const string State = "Состояние";
        public const string Health = "Здоровье";
        public const string Healthy = "здоров";
        public const string Money = "Деньги";
        public const string Relations = "Отношения";
        public const string NoRelations = "ни с кем особенно";
        public const string Now = "Сейчас";
        public const string History = "История";
        public const string Unknown = "?";
        public const string Male = "мужчина";
        public const string Female = "женщина";
        public const string AgeFormat = "{0} лет";
        public const string RankPointsFormat = "ранг {0} · очки {1}/{2}";
        public const string RankTop = "ранг {0} · высший";
        public const string PowerFormat = "ранг характеристик {0}";
        public const string WalletFormat = "кошелёк {0}";
        public const string DebtFormat = "долг гильдии {0}";
        public const string HousingCity = "живёт в городе";
        public const string HousingDormitory = "живёт в Общежитии";
        public const string Friends = "друзья";
        public const string Dislike = "неприязнь";
        public const string Rival = "соперник";
        public const string Lover = "влюблённые";
        public const string QuestsDoneFormat = "заданий выполнено {0}, провалено {1}";
        public const string LastLines = "Последние строки ленты";
        public const string LeftGuild = "ушёл из гильдии";
        public const string DiedState = "погиб";
        public const string Disappeared = "пропал";
        public const string Expelled = "исключён из гильдии";
        public const string Candidate = "кандидат";
        public const string BalancedMale = "уравновешен";
        public const string BalancedFemale = "уравновешена";
        public const string HiddenMark = "скрыта";

        // Окна
        public const string AutopauseTitle = "Пауза";
        public const string GoTo = "Перейти";
        public const string Continue = "Продолжить";
        public const string MonthReport = "Отчёт месяца";

        // Отладка
        public const string DebugTitle = "Отладка (F1)";
        public const string Seed = "Зерно";
        public const string Restart = "Перезапустить";
        public const string DebugSpeed = "×50";
        public const string Rewind = "Перемотать";
        public const string Hours = "часов";
        public const string Days = "дней";
        public const string RevealAll = "Раскрыть всё";
        public const string Apply = "Задать";
        public const string Create = "Создать";
        public const string Person = "Человек";
        public const string Order = "Заказ";
        public const string Event = "Событие";
        public const string TraitsHint = "черты: id через запятую";
        public const string SelectedPersonFormat = "выбран: {0}";
        public const string SelectedQuestFormat = "задание: {0}";
        public const string NothingSelected = "не выбран — откройте карточку или задание";
        public const string Ambush = "Засада";
        public const string Discovery = "Находка";
        public const string Breakdown = "Срыв";
        public const string HiddenProfile = "Профиль (скрыто)";
        public const string RoundChanceFormat = "шанс раунда {0:0}%";
    }
}
