using System.Collections.Generic;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>Где задание в своей жизни.</summary>
    public enum QuestPhase
    {
        /// <summary>Путь к месту задания.</summary>
        TravelOut,

        /// <summary>На месте: раунды.</summary>
        AtSite,

        /// <summary>Путь назад: после выполнения или отступления.</summary>
        TravelBack,

        /// <summary>Задание кончилось: группа вернулась или не вернулся никто.</summary>
        Returned,
    }

    /// <summary>
    /// Уровень результата. Игрок видит только «выполнено / нет»; уровень — скрытый: от него строки ленты и очки ранга.
    /// Новые значения — в конец.
    /// </summary>
    public enum QuestResult
    {
        /// <summary>Ещё идёт.</summary>
        None,

        /// <summary>Выполнено без провалов раундов и без ран.</summary>
        Brilliant,

        /// <summary>Выполнено без провалов раундов, но были раны или гибель (в пути, в пещере).</summary>
        Success,

        /// <summary>Выполнено после провалов раундов.</summary>
        Partial,

        /// <summary>Не выполнено, без гибели.</summary>
        Fail,

        /// <summary>Не выполнено, была гибель, или погибли все.</summary>
        Catastrophe,
    }

    /// <summary>Что стало с находкой.</summary>
    public enum DiscoveryState
    {
        /// <summary>Находки нет или её ещё не заметили.</summary>
        None,

        /// <summary>Заметили, решение ещё не принято.</summary>
        Found,

        /// <summary>Исследовали.</summary>
        Explored,

        /// <summary>Прошли мимо — по возвращении расскажут Регистратору.</summary>
        Skipped,

        /// <summary>Рассказали Регистратору: появилось событийное задание.</summary>
        Reported,
    }

    /// <summary>Находка на задании: какая, её истинный ранг и что с ней решили.</summary>
    public sealed class QuestDiscovery
    {
        internal QuestDiscovery(string discoveryId, long atHours)
        {
            DiscoveryId = discoveryId;
            AtHours = atHours;
        }

        /// <summary>Id <see cref="DiscoveryDefinition"/>.</summary>
        public string DiscoveryId { get; }

        /// <summary>Когда группа её заметит (час пути туда).</summary>
        public long AtHours { get; }

        public DiscoveryState State { get; internal set; }

        /// <summary>Кто заметил; 0 — ещё никто.</summary>
        public int SpotterId { get; internal set; }

        /// <summary>Истинный ранг находки: от него — её профиль и профиль событийного задания.</summary>
        public GuildRank TrueRank { get; internal set; }

        /// <summary>Тайник из находки — добавится к трофеям.</summary>
        public int Loot { get; internal set; }

        /// <summary>Событийное задание, которое появилось из находки; 0 — нет.</summary>
        public int EventOrderId { get; internal set; }
    }

    /// <summary>
    /// Задание: исполнение заказа группой (в прототипе люди ходят соло, но расчёт — для любого числа). Часть мира: снаружи
    /// Core только чтение, меняет <see cref="QuestSystem"/>. Строки ленты задания — <see cref="Log"/>.
    /// </summary>
    public sealed class QuestRun
    {
        private readonly List<int> members = new List<int>();
        private readonly List<int> departed = new List<int>();
        private readonly List<int> turnedBack = new List<int>();
        private readonly List<int> fled = new List<int>();
        private readonly List<int> dead = new List<int>();
        private readonly List<int> panicked = new List<int>();
        private readonly List<int> rushing = new List<int>();
        private readonly List<int> overreached = new List<int>();
        private readonly List<FeedEntry> log = new List<FeedEntry>();

        internal QuestRun(int id, Order order, long departedAtHours)
        {
            Id = id;
            OrderId = order.Id;
            TypeId = order.TypeId;
            Rank = order.Rank;
            Distance = order.Distance;
            Place = order.Place;
            Enemy = order.Enemy;
            Client = order.Client;
            Cargo = order.Cargo;
            IsPromotion = order.IsPromotion;
            IsEventQuest = order.IsEventQuest;
            DepartedAtHours = departedAtHours;
        }

        public int Id { get; }
        public int OrderId { get; }

        /// <summary>Id типа задания.</summary>
        public string TypeId { get; }

        public GuildRank Rank { get; }
        public OrderDistance Distance { get; }
        public NounForms Place { get; }
        public NounForms Enemy { get; }
        public NounForms Client { get; }
        public NounForms Cargo { get; }
        public bool IsPromotion { get; }
        public bool IsEventQuest { get; }

        /// <summary>Кто идёт сейчас: дошли, не сбежали, живы.</summary>
        public IReadOnlyList<int> Members => members;

        /// <summary>Все, кто вышел.</summary>
        public IReadOnlyList<int> Departed => departed;

        /// <summary>Повернули назад раньше (ранены в пути).</summary>
        public IReadOnlyList<int> TurnedBack => turnedBack;

        /// <summary>Сбежали.</summary>
        public IReadOnlyList<int> Fled => fled;

        /// <summary>Погибли.</summary>
        public IReadOnlyList<int> Dead => dead;

        /// <summary>В панике в следующем раунде.</summary>
        public IReadOnlyList<int> Panicked => panicked;

        /// <summary>Бросились вперёд: вклад выше в следующем раунде, первыми получают рану.</summary>
        public IReadOnlyList<int> Rushing => rushing;

        public QuestPhase Phase { get; internal set; }

        /// <summary>Сколько ходовых часов осталось в текущей фазе (пути или раунде); ночлег не считается.</summary>
        public int PhaseHoursLeft { get; internal set; }

        /// <summary>Номер раунда на месте, с 1; 0 — ещё не дошли.</summary>
        public int Round { get; internal set; }

        /// <summary>Провалено раундов, 0–5.</summary>
        public int FailedRounds { get; internal set; }

        /// <summary>Бонус (+25% награды) потерян.</summary>
        public bool BonusLost { get; internal set; }

        /// <summary>Добыча потеряна.</summary>
        public bool LootLost { get; internal set; }

        /// <summary>Группа отступила.</summary>
        public bool Retreated { get; internal set; }

        /// <summary>Цель выполнена: раунд удался.</summary>
        public bool Completed { get; internal set; }

        /// <summary>На задании кто-то был ранен (для уровня результата).</summary>
        public bool HadWounds { get; internal set; }

        /// <summary>Находка и решение по ней; <c>null</c> — находки не будет.</summary>
        public QuestDiscovery Discovery { get; internal set; }

        /// <summary>Скрытый уровень результата; <see cref="QuestResult.None"/> — задание идёт.</summary>
        public QuestResult Result { get; internal set; }

        public long DepartedAtHours { get; }

        /// <summary>Когда задание кончилось; −1 — идёт.</summary>
        public long ReturnedAtHours { get; internal set; } = -1;

        /// <summary>Строки ленты задания, от старых к новым.</summary>
        public IReadOnlyList<FeedEntry> Log => log;

        /// <summary>Задание выполнено (цель достигнута, какой бы ни была цена).</summary>
        public bool IsDone => Completed;

        // ---------- Скрытое: план пути ----------

        /// <summary>Час события в пути (номер ходового часа в отрезке, с 1); 0 — события нет.</summary>
        internal int TravelEventHour { get; set; }

        /// <summary>Сколько ходовых часов прошло в текущем отрезке пути.</summary>
        internal int LegHoursDone { get; set; }

        /// <summary>Сколько ходовых часов шли туда (для тех, кто повернул назад, и для обратного пути).</summary>
        internal int OutHoursDone { get; set; }

        /// <summary>Группа сейчас ночует: утром — строка «ночь прошла».</summary>
        internal bool Camping { get; set; }

        /// <summary>Кто взял заказ, переоценив шансы (для раскрытия Хвастуна).</summary>
        internal IReadOnlyList<int> Overreached => overreached;

        internal void AddMember(int id)
        {
            members.Add(id);
            departed.Add(id);
        }

        internal void RemoveMember(int id)
        {
            members.Remove(id);
            panicked.Remove(id);
            rushing.Remove(id);
        }

        internal void AddTurnedBack(int id) => turnedBack.Add(id);
        internal void AddFled(int id) => fled.Add(id);
        internal void AddDead(int id) => dead.Add(id);
        internal void SetPanicked(int id) { if (!panicked.Contains(id)) panicked.Add(id); }
        internal void SetRushing(int id) { if (!rushing.Contains(id)) rushing.Add(id); }
        internal void ClearRoundEffects()
        {
            panicked.Clear();
            rushing.Clear();
        }

        internal void AddOverreached(int id) => overreached.Add(id);
        internal void AddLog(FeedEntry entry) => log.Add(entry);
    }

    /// <summary>
    /// Часть мира: задания. Идущие — по порядку выхода; законченные хранятся <c>questFeedKeepDays</c> дней после возвращения
    /// (с лентой задания), потом удаляются. Люди, идущие домой одни (беглецы, повернувшие назад), — отдельно.
    /// </summary>
    public sealed class QuestBook
    {
        private readonly List<QuestRun> active = new List<QuestRun>();
        private readonly List<QuestRun> finished = new List<QuestRun>();
        private readonly List<Straggler> stragglers = new List<Straggler>();
        private int nextId = 1;

        /// <summary>Идущие задания, по порядку выхода.</summary>
        public IReadOnlyList<QuestRun> Active => active;

        /// <summary>Законченные задания за последние <c>questFeedKeepDays</c> дней, от старых к новым.</summary>
        public IReadOnlyList<QuestRun> Finished => finished;

        /// <summary>Люди, которые идут домой одни.</summary>
        public IReadOnlyList<Straggler> Stragglers => stragglers;

        /// <summary>Сколько заданий начато за игру.</summary>
        public int StartedCount => nextId - 1;

        public bool TryGetRun(int id, out QuestRun run)
        {
            foreach (QuestRun candidate in active)
            {
                if (candidate.Id != id) continue;
                run = candidate;
                return true;
            }
            foreach (QuestRun candidate in finished)
            {
                if (candidate.Id != id) continue;
                run = candidate;
                return true;
            }
            run = null;
            return false;
        }

        internal int NextId() => nextId++;

        internal void AddActive(QuestRun run) => active.Add(run);

        internal void Finish(QuestRun run)
        {
            active.Remove(run);
            finished.Add(run);
        }

        /// <summary>Удалить законченные задания, вернувшиеся раньше <paramref name="beforeHours"/>.</summary>
        internal void ForgetFinished(long beforeHours) => finished.RemoveAll(r => r.ReturnedAtHours < beforeHours);

        internal void AddStraggler(Straggler straggler) => stragglers.Add(straggler);

        internal bool RemoveStraggler(Straggler straggler) => stragglers.Remove(straggler);
    }

    /// <summary>Почему человек идёт домой один.</summary>
    public enum StragglerKind
    {
        /// <summary>Ранен в пути и повернул назад.</summary>
        TurnedBack,

        /// <summary>Сбежал: вернётся в гильдию или исчезнет.</summary>
        Fugitive,
    }

    /// <summary>Человек, который идёт домой один: когда дойдёт и что будет.</summary>
    public sealed class Straggler
    {
        internal Straggler(int adventurerId, int questRunId, StragglerKind kind, long dueAtHours, bool returns)
        {
            AdventurerId = adventurerId;
            QuestRunId = questRunId;
            Kind = kind;
            DueAtHours = dueAtHours;
            Returns = returns;
        }

        public int AdventurerId { get; }
        public int QuestRunId { get; }
        public StragglerKind Kind { get; }

        /// <summary>Когда вернётся (или исчезнет).</summary>
        public long DueAtHours { get; }

        /// <summary>Вернётся в гильдию; <c>false</c> — беглец исчезнет.</summary>
        public bool Returns { get; }
    }
}
