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
        [Tooltip("Задание выполнено")]
        [SerializeField] private float successReputationPerRank = 0.2f;

        [Tooltip("Задание не выполнено: отступили или погибли все")]
        [SerializeField] private float failureReputationPerRank = -0.2f;

        [Tooltip("За каждого погибшего")]
        [SerializeField] private float deathReputation = -2f;

        [Header("Приток людей: шанс кандидата в день = база + репутация × множитель")]
        [SerializeField, Range(0f, 1f)] private float influxBaseChance = 0.03f;
        [SerializeField, Range(0f, 0.1f)] private float influxChancePerReputation = 0.003f;

        [Tooltip("Людей в гильдии не больше (с ожидающими кандидатами)")]
        [SerializeField, Min(1)] private int maxAdventurers = 30;

        public int StartMoney => startMoney;
        public float StartReputation => startReputation;
        public float MaxReputation => maxReputation;
        public float SuccessReputationPerRank => successReputationPerRank;
        public float FailureReputationPerRank => failureReputationPerRank;
        public float DeathReputation => deathReputation;
        public float InfluxBaseChance => influxBaseChance;
        public float InfluxChancePerReputation => influxChancePerReputation;
        public int MaxAdventurers => maxAdventurers;
    }
}
