using System.Collections.Generic;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Показатели состояния человека: шкалы 0..100, личные деньги, раны, занятие и флаги.
    /// Часть мира: снаружи Core только чтение. Менять — <see cref="StateService"/>, <see cref="WalletService"/>,
    /// <see cref="HealthService"/>, системы шагов 5–8 (<see cref="ActivitySystem"/>, <see cref="StateSystem"/>,
    /// <see cref="HealthSystem"/>, <see cref="DecisionSystem"/>).
    /// </summary>
    public sealed class AdventurerState
    {
        private readonly List<Condition> conditions = new List<Condition>();

        internal AdventurerState()
        {
        }

        /// <summary>Усталость, 0..100. Выше 70 — профиль ниже, выше 90 — нельзя брать задания.</summary>
        public float Fatigue { get; internal set; }

        /// <summary>Стресс, 0..100. Выше 80 — шанс срыва раз в сутки.</summary>
        public float Stress { get; internal set; }

        /// <summary>Довольство, 0..100. Раз в сутки идёт к цели (<see cref="StateRules.ContentmentTarget"/>).</summary>
        public float Contentment { get; internal set; }

        /// <summary>Лояльность, 0..100. Игрок видит её только словами (<see cref="StateRules.LoyaltyWord"/>).</summary>
        public float Loyalty { get; internal set; }

        /// <summary>Монеты, ≥ 0.</summary>
        public int Wallet { get; internal set; }

        /// <summary>Долг гильдии, монеты, ≥ 0 (дилемма «Просьба в долг»).</summary>
        public int DebtToGuild { get; internal set; }

        /// <summary>Раны в порядке получения. Увечье — не рана, а черта «Калека».</summary>
        public IReadOnlyList<Condition> Conditions => conditions;

        /// <summary>Занятие в текущем часу.</summary>
        public Activity Activity { get; internal set; }

        /// <summary>
        /// Флаг «кошелёк пуст»: какой-то расход не удалось оплатить целиком. Снимается доходом. Довольство — условие в цели.
        /// </summary>
        public bool IsWalletEmpty { get; internal set; }

        /// <summary>Идущий срыв; <see cref="BreakdownKind.None"/> — нет. Драка мгновенна и здесь не остаётся.</summary>
        public BreakdownKind Breakdown { get; internal set; }

        /// <summary>Когда кончится срыв (запой — «в запое до …»).</summary>
        public long BreakdownEndsAtHours { get; internal set; }

        /// <summary>
        /// Лежит в Лазарете: койка занята до выздоровления. Тяжело раненого кладёт <see cref="HealthSystem"/>, легко раненый
        /// ложится сам (решение «лечь в Лазарет»).
        /// </summary>
        public bool InInfirmary { get; internal set; }

        /// <summary>Пьяница пропускает этот день в таверне: до этого часа днём — таверна.</summary>
        public long SkipsDayUntilHours { get; internal set; }

        // Учёт текущих суток; расходы за них списываются и учёт сбрасывается в 00:00 (StateSystem).

        /// <summary>За текущие сутки хотя бы раз ел в таверне: еда дороже, довольство выше.</summary>
        public bool AteInTavernToday { get; internal set; }

        /// <summary>За текущие сутки уже заплатил за выпивку (вечер в таверне или день запоя).</summary>
        public bool DrankToday { get; internal set; }

        /// <summary>За текущие сутки уже заплатил за Лазарет.</summary>
        public bool PaidInfirmaryToday { get; internal set; }

        // Тренировка на дворе.

        /// <summary>Когда кончится текущее занятие на дворе; после него человек свободен и решает снова.</summary>
        public long TrainingEndsAtHours { get; internal set; }

        /// <summary>Что тренирует в текущем занятии; <c>null</c> — ещё не выбрано.</summary>
        public StatId? TrainingStat { get; internal set; }

        /// <summary>Номер суток последней тренировки (<c>часы / часов в сутках</c>); −1 — не тренировался. Второй раз за сутки нельзя.</summary>
        public long TrainedOnDay { get; internal set; } = -1;

        /// <summary>Когда начался последний срыв; <c>null</c> — срывов не было.</summary>
        public long? LastBreakdownAtHours { get; internal set; }

        // Решения (DecisionSystem).

        /// <summary>
        /// Занятие, выбранное решением в этом часу: <see cref="ActivitySystem"/> ставит его со следующего часа, если человеку
        /// ничего не мешает (сон, срыв, рана). <c>null</c> — решения не было.
        /// </summary>
        public Activity? PlannedActivity { get; internal set; }

        /// <summary>В прошлом часу человек был свободен и мог решать сам (для точки «освободился»).</summary>
        public bool WasFreeLastHour { get; internal set; }

        /// <summary>Номер суток последнего утреннего решения (<c>часы / часов в сутках</c>); −1 — не было.</summary>
        public long MorningDecisionDay { get; internal set; } = -1;

        /// <summary>Хотя бы час этого вечера провёл в таверне. Итоги вечера (отношения, ссоры) — в начале ночи.</summary>
        public bool InTavernThisEvening { get; internal set; }

        // Задания.

        /// <summary>Заказ, взятый решением в этом часу: выход — в следующем. 0 — нет.</summary>
        public int PlannedOrderId { get; internal set; }

        /// <summary>
        /// Задание, на котором человек сейчас (<see cref="QuestRun.Id"/>); 0 — не на задании. Беглец и повернувший назад
        /// идут домой одни — у них 0, но занятие задания.
        /// </summary>
        public int QuestRunId { get; internal set; }

        /// <summary>С кем человек на задании: один или в группе; вне задания — <see cref="PartyContext.None"/>.</summary>
        public PartyContext QuestParty { get; internal set; }

        /// <summary>
        /// Гильдия разрешила ходить на задания с тяжёлой раной: множитель профиля на время раны (0 — разрешения нет).
        /// Снимается, когда тяжёлая рана заживёт или новая рана станет увечьем.
        /// </summary>
        public float WoundedQuestProfile { get; internal set; }

        public bool HasWoundedQuestPermission => WoundedQuestProfile > 0f;

        public bool HasHeavyWound() => TryGetCondition(ConditionKind.HeavyWound, out _);

        /// <summary>Тяжёлая рана держит человека дома: она есть, и разрешения ходить с ней на задания нет.</summary>
        public bool IsLaidUpByWound() => HasHeavyWound() && !HasWoundedQuestPermission;

        public bool HasLightWound() => TryGetCondition(ConditionKind.LightWound, out _);

        public bool IsOnQuest() => Activity == Activity.OnQuestTravel || Activity == Activity.OnQuestRound || Activity == Activity.OnQuestCamp;

        /// <summary>Первая рана этого вида.</summary>
        public bool TryGetCondition(ConditionKind kind, out Condition condition)
        {
            foreach (Condition entry in conditions)
            {
                if (entry.Kind == kind)
                {
                    condition = entry;
                    return true;
                }
            }
            condition = null;
            return false;
        }

        internal void AddCondition(Condition condition) => conditions.Add(condition);

        internal bool RemoveCondition(Condition condition) => conditions.Remove(condition);

        internal void ClearConditions() => conditions.Clear();
    }
}
