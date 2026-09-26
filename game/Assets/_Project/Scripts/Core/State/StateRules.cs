using System;
using GuildMaster.Data;

namespace GuildMaster.Core
{
    /// <summary>
    /// Чистые правила состояния (ТЗ 05): изменение за час по занятию, цель довольства, запреты, слова лояльности.
    /// Мир не меняют. Числа — <see cref="StateBalance"/>, <see cref="ExpensesBalance"/>, <see cref="TraitsBalance"/>.
    /// </summary>
    public static class StateRules
    {
        /// <summary>Край шкал состояния: 0..100.</summary>
        public const float Limit = 100f;

        public static float Clamp(float value) => Math.Max(0f, Math.Min(Limit, value));

        /// <summary>Усталость за час занятия, до множителей черт.</summary>
        public static float FatiguePerHour(Activity activity, StateBalance state)
        {
            switch (activity)
            {
                case Activity.OnQuestTravel:
                case Activity.OnQuestRound: return state.QuestFatigue;
                case Activity.OnQuestCamp: return state.CampFatigue;
                case Activity.Training: return state.TrainingFatigue;
                case Activity.Resting: return state.RestFatigue;
                case Activity.Sleeping: return state.SleepFatigue;
                case Activity.Tavern: return state.TavernFatigue;
                case Activity.Infirmary: return state.InfirmaryFatigue;
                case Activity.Binge: return state.BingeFatigue;
                default: throw new ArgumentOutOfRangeException(nameof(activity), activity, null);
            }
        }

        /// <summary>Стресс за час занятия, до множителей черт. На задании — «само по себе», события — отдельно (ТЗ 09).</summary>
        public static float StressPerHour(Activity activity, StateBalance state)
        {
            switch (activity)
            {
                case Activity.OnQuestTravel:
                case Activity.OnQuestRound:
                case Activity.OnQuestCamp:
                case Activity.Training: return state.PassiveStress;
                case Activity.Resting: return state.RestStress;
                case Activity.Sleeping: return state.SleepStress;
                case Activity.Tavern: return state.TavernStress;
                case Activity.Infirmary: return state.InfirmaryStress;
                case Activity.Binge: return state.BingeStress;
                default: throw new ArgumentOutOfRangeException(nameof(activity), activity, null);
            }
        }

        /// <summary>Усталость выше <c>fatigueProfileThreshold</c> (70): эффективный профиль × 0,8.</summary>
        public static bool IsFatigueLowersProfile(AdventurerState state, StateBalance balance) =>
            state.Fatigue > balance.FatigueProfileThreshold;

        /// <summary>Усталость выше <c>fatigueQuestBanThreshold</c> (90): нельзя брать задания (запрет модели решений, ТЗ 06).</summary>
        public static bool IsTooTiredForQuests(AdventurerState state, StateBalance balance) =>
            state.Fatigue > balance.FatigueQuestBanThreshold;

        /// <summary>
        /// Запреты ТЗ 05 для модели решений (ТЗ 06 → «Шаг 1»): нельзя брать задания при тяжёлой ране, усталости выше 90
        /// и во время срыва (запой, отказ труса, «сел и не смог подняться»).
        /// </summary>
        public static bool CanTakeQuests(AdventurerState state, StateBalance balance) =>
            !state.HasHeavyWound() && !IsTooTiredForQuests(state, balance) && state.Breakdown == BreakdownKind.None;

        /// <summary>
        /// Цель довольства: <c>contentmentBase</c> + условия (numbers.md → Довольство), 0..100.
        /// Жильё, еда в таверне за прошедшие сутки, пустой кошелёк, комиссия гильдии (× чувствительность черт),
        /// стресс выше 70. Распоряжений до ТЗ 12 нет.
        /// </summary>
        public static float ContentmentTarget(Adventurer adventurer, float commission, DataRegistry data)
        {
            StateBalance balance = data.Balance.State;
            AdventurerState state = adventurer.State;

            float target = balance.ContentmentBase;
            target += adventurer.Housing == Housing.Dorm ? balance.DormHousingContentment : balance.CityHousingContentment;
            if (state.AteInTavernToday) target += balance.TavernFoodContentment;
            if (state.IsWalletEmpty) target += balance.EmptyWalletContentment;
            target += CommissionContentment(commission, balance) * StateRates.PayContentmentSensitivity(adventurer, data);
            if (state.Stress > balance.HighStressThreshold) target += balance.HighStressContentment;
            return Clamp(target);
        }

        /// <summary>Вклад комиссии в цель: <c>commissionStepContentment</c> (−5) за каждые полные 5% выше 25%.</summary>
        public static float CommissionContentment(float commission, StateBalance balance)
        {
            float over = commission - balance.CommissionThreshold;
            if (over <= 0f) return 0f;
            // Допуск: 0,30 − 0,25 в float чуть меньше 0,05, а это полный шаг.
            float steps = (float)Math.Floor(over / balance.CommissionStep + 1e-4f);
            return steps * balance.CommissionStepContentment;
        }

        /// <summary>
        /// Слова лояльности в карточке (ТЗ 05 → «Отображение»): номер интервала по <c>loyaltyWordThresholds</c>,
        /// 0 — ниже первой границы. Тексты — <see cref="LoyaltyWord"/>.
        /// </summary>
        public static int LoyaltyWordIndex(float loyalty, StateBalance balance)
        {
            int index = 0;
            foreach (float threshold in balance.LoyaltyWordThresholds)
            {
                if (loyalty < threshold) break;
                index++;
            }
            return index;
        }

        /// <summary>❔ Слова для примерной лояльности (ТЗ 05): от «подумывает уйти» до «предан гильдии», мужской род.</summary>
        public static readonly string[] LoyaltyWordsMale =
        {
            "Кажется, подумывает уйти",
            "Кажется, сомневается",
            "Кажется, спокоен",
            "Кажется, доволен гильдией",
            "Кажется, предан гильдии",
        };

        public static readonly string[] LoyaltyWordsFemale =
        {
            "Кажется, подумывает уйти",
            "Кажется, сомневается",
            "Кажется, спокойна",
            "Кажется, довольна гильдией",
            "Кажется, предана гильдии",
        };

        /// <summary>Что игрок видит вместо числа лояльности.</summary>
        public static string LoyaltyWord(float loyalty, Gender gender, StateBalance balance)
        {
            string[] words = gender == Gender.Female ? LoyaltyWordsFemale : LoyaltyWordsMale;
            return words[Math.Min(LoyaltyWordIndex(loyalty, balance), words.Length - 1)];
        }
    }
}
