using System;
using UnityEngine;

namespace GuildMaster.Data
{
    /// <summary>Раздел <c>Guild</c>: старт, репутация, приток людей.</summary>
    [Serializable]
    public sealed class GuildBalance
    {
        [SerializeField] private int startMoney = 2000;
        [SerializeField, Range(0f, 100f)] private float startReputation = 5f;
        [SerializeField, Min(1f)] private float maxReputation = 100f;

        [Header("Репутация за задание: × номер ранга (G = 1 … C = 5)")]
        [Tooltip("Задание выполнено; × (1 − репутация / maxReputation) — чем выше репутация, тем меньше прибавка")]
        [SerializeField] private float successReputationPerRank = 0.03f;

        [Tooltip("Задание не выполнено: отступили или погибли все")]
        [SerializeField] private float failureReputationPerRank = -0.2f;

        [Tooltip("За каждого погибшего")]
        [SerializeField] private float deathReputation = -2f;

        [Header("Живая репутация")]
        [Tooltip("Остывание: в 00:00 минус столько, если за сутки гильдия не выполнила ни одного задания (экзамены не в счёт)")]
        [SerializeField, Min(0f)] private float reputationDecayPerDay = 0.05f;

        [Tooltip("Человек ушёл из гильдии сам (кроме ухода вслед за другом)")]
        [SerializeField] private float leaveReputation = -1f;

        [Tooltip("Человек сбежал с задания")]
        [SerializeField] private float fleeReputation = -1f;

        [Tooltip("Не выполнен важный заказ, принятый игроком (сверх обычного за провал)")]
        [SerializeField] private float importantFailedReputation = -3f;

        [Tooltip("Сколько последних изменений репутации помнит гильдия (для интерфейса)")]
        [SerializeField, Min(1)] private int recentReputationChanges = 50;

        [Header("Приток людей: шанс кандидата в день = (база + репутация × множитель) × население × молва")]
        [SerializeField, Range(0f, 1f)] private float influxBaseChance = 0.03f;
        [SerializeField, Range(0f, 0.1f)] private float influxChancePerReputation = 0.003f;

        [Tooltip("Множитель населения = население / стартовое население, не больше этого")]
        [SerializeField, Min(0f)] private float influxPopulationCap = 2f;

        [Tooltip("Молва = это + среднее довольство людей гильдии / делитель, в пределах 0..rumorMax; людей нет — × 1. "
            + "Отрицательное — при низком довольстве молва гасит приток до нуля")]
        [SerializeField] private float rumorBase = -0.25f;

        [Tooltip("Делитель довольства в молве")]
        [SerializeField, Min(1f)] private float rumorDivisor = 40f;

        [Tooltip("Молва не больше этого")]
        [SerializeField, Min(0f)] private float rumorMax = 1.5f;

        [Header("Подъёмные: гильдия платит новичку при приёме, по уровню (Новичок — как слабый)")]
        [SerializeField, Min(0)] private int signingBonusWeak = 20;
        [SerializeField, Min(0)] private int signingBonusMedium = 40;
        [SerializeField, Min(0)] private int signingBonusStrong = 60;

        [Tooltip("Людей в гильдии не больше (с ожидающими кандидатами)")]
        [SerializeField, Min(1)] private int maxAdventurers = 30;

        public int StartMoney => startMoney;
        public float StartReputation => startReputation;
        public float MaxReputation => maxReputation;
        public float SuccessReputationPerRank => successReputationPerRank;
        public float FailureReputationPerRank => failureReputationPerRank;
        public float DeathReputation => deathReputation;
        public float ReputationDecayPerDay => reputationDecayPerDay;
        public float LeaveReputation => leaveReputation;
        public float FleeReputation => fleeReputation;
        public float ImportantFailedReputation => importantFailedReputation;
        public int RecentReputationChanges => recentReputationChanges;
        public float InfluxBaseChance => influxBaseChance;
        public float InfluxChancePerReputation => influxChancePerReputation;
        public float InfluxPopulationCap => influxPopulationCap;
        public float RumorBase => rumorBase;
        public float RumorDivisor => rumorDivisor;
        public float RumorMax => rumorMax;
        public int SigningBonusWeak => signingBonusWeak;
        public int SigningBonusMedium => signingBonusMedium;
        public int SigningBonusStrong => signingBonusStrong;
        public int MaxAdventurers => maxAdventurers;
    }
}
