using System;
using UnityEngine;

namespace GuildMaster.Data
{
    /// <summary>
    /// Раздел <c>Dilemmas</c>: общие правила обращений и числа триггеров.
    /// Суммы и сдвиги вариантов — в эффектах вариантов <see cref="DilemmaDefinition"/>.
    /// </summary>
    [Serializable]
    public sealed class DilemmasBalance
    {
        [Header("Общее")]
        [Tooltip("Срок ответа, дней; без ответа — вариант по умолчанию")]
        [SerializeField, Min(1)] private int responseDays = 2;

        [Tooltip("Открытых обращений одновременно не больше")]
        [SerializeField, Min(1)] private int maxOpen = 3;

        [Tooltip("Перезарядка по умолчанию, дней")]
        [SerializeField, Min(0)] private int defaultCooldownDays = 30;

        [Tooltip("Час ежедневной проверки триггеров")]
        [SerializeField, Range(0, 23)] private int checkHour = 12;

        [Header("№1 Просьба в долг")]
        [Tooltip("Кошелёк меньше расходов на столько дней")]
        [SerializeField, Min(1)] private int loanWalletDays = 3;

        [Tooltip("Без черты: шанс в неделю")]
        [SerializeField, Range(0f, 1f)] private float loanWeeklyChance = 0.1f;

        [Tooltip("Сумма — расходы на столько дней")]
        [SerializeField, Min(1)] private int loanSumDays = 30;

        [Header("№3 Ссора из-за добычи")]
        [Tooltip("Ось Деньги от")]
        [SerializeField, Range(0f, 100f)] private float lootDisputeMoneyAxis = 50f;

        [Tooltip("Отношения пары ниже")]
        [SerializeField, Range(-100f, 100f)] private float lootDisputeRelationBelow = 0f;

        [SerializeField, Range(0f, 1f)] private float lootDisputeChance = 0.3f;

        [Header("№4 Раненый рвётся на задание")]
        [Tooltip("Кошелёк меньше расходов на столько дней")]
        [SerializeField, Min(1)] private int woundedWalletDays = 7;

        [Header("№10 Влюблённые просят одну группу")]
        [Tooltip("Оба в гильдии не меньше, дней")]
        [SerializeField, Min(0)] private int loversMinDaysInGuild = 7;

        [Header("№13 Трактирщик предлагает праздник")]
        [Tooltip("Средний стресс авантюристов выше")]
        [SerializeField, Range(0f, 100f)] private float feastAverageStress = 45f;

        [Tooltip("…или гибель в гильдии за последние, дней")]
        [SerializeField, Min(1)] private int feastRecentDeathDays = 7;

        public int ResponseDays => responseDays;
        public int MaxOpen => maxOpen;
        public int DefaultCooldownDays => defaultCooldownDays;
        public int CheckHour => checkHour;
        public int LoanWalletDays => loanWalletDays;
        public float LoanWeeklyChance => loanWeeklyChance;
        public int LoanSumDays => loanSumDays;
        public float LootDisputeMoneyAxis => lootDisputeMoneyAxis;
        public float LootDisputeRelationBelow => lootDisputeRelationBelow;
        public float LootDisputeChance => lootDisputeChance;
        public int WoundedWalletDays => woundedWalletDays;
        public int LoversMinDaysInGuild => loversMinDaysInGuild;
        public float FeastAverageStress => feastAverageStress;
        public int FeastRecentDeathDays => feastRecentDeathDays;
    }
}
