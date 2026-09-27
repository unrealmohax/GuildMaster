using System.Collections.Generic;

namespace GuildMaster.Core
{
    /// <summary>
    /// Показатели состояния человека: шкалы 0..100, личные деньги, раны, занятие и флаги.
    /// Часть мира: снаружи Core только чтение. Менять — <see cref="StateService"/>, <see cref="WalletService"/>,
    /// <see cref="HealthService"/> и системы шагов 5–7 (<see cref="ActivitySystem"/>, <see cref="StateSystem"/>,
    /// <see cref="HealthSystem"/>).
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

        /// <summary>Лежит в Лазарете (койку распределяет <see cref="HealthSystem"/> раз в сутки).</summary>
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

        public bool HasHeavyWound() => TryGetCondition(ConditionKind.HeavyWound, out _);

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
