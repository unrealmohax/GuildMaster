using System;
using UnityEngine;

namespace GuildMaster.Data
{
    /// <summary>Раздел <c>Staff</c>: кандидаты, зарплата, переговоры, эффект уровня. Базовые зарплаты — в должностях.</summary>
    [Serializable]
    public sealed class StaffBalance
    {
        [Tooltip("Уровень возможностей кандидата")]
        [SerializeField] private IntRange candidateLevel = new IntRange(30, 70);

        [Header("Просимая зарплата = базовая × (база + уровень / делитель)")]
        [SerializeField, Min(0f)] private float salaryLevelBase = 0.7f;
        [SerializeField, Min(1f)] private float salaryLevelDivisor = 100f;

        [Header("Эффект уровня = база + уровень / делитель")]
        [SerializeField, Min(0f)] private float levelEffectBase = 0.8f;
        [SerializeField, Min(1f)] private float levelEffectDivisor = 250f;

        [Header("Кандидаты при вакансии")]
        [SerializeField, Min(1)] private int candidatesPerVisit = 2;
        [SerializeField, Min(1)] private int candidateIntervalDays = 3;
        [SerializeField, Min(1)] private int candidateWaitDays = 3;

        [Header("Переговоры: 1 − (просимая − предложение) / (просимая × допуск) + репутация / делитель")]
        [SerializeField, Range(0.01f, 1f)] private float negotiationTolerance = 0.3f;
        [SerializeField, Min(1f)] private float negotiationReputationDivisor = 200f;

        [Tooltip("Невыплаченная зарплата: увольнение через столько платёжных сроков")]
        [SerializeField, Min(1)] private int unpaidMonthsToQuit = 1;

        public IntRange CandidateLevel => candidateLevel;
        public float SalaryLevelBase => salaryLevelBase;
        public float SalaryLevelDivisor => salaryLevelDivisor;
        public float LevelEffectBase => levelEffectBase;
        public float LevelEffectDivisor => levelEffectDivisor;
        public int CandidatesPerVisit => candidatesPerVisit;
        public int CandidateIntervalDays => candidateIntervalDays;
        public int CandidateWaitDays => candidateWaitDays;
        public float NegotiationTolerance => negotiationTolerance;
        public float NegotiationReputationDivisor => negotiationReputationDivisor;
        public int UnpaidMonthsToQuit => unpaidMonthsToQuit;
    }
}
