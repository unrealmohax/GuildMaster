using System;
using UnityEngine;

namespace GuildMaster.Data
{
    /// <summary>
    /// Раздел <c>Family</c>: состав семьи при генерации, недельная плата семье, нужда, болезни и смерти родных, траур.
    /// «Лет сверх» — возраст человека минус нижняя граница возраста генерации.
    /// </summary>
    [Serializable]
    public sealed class FamilyBalance
    {
        [Header("Состав")]
        [Tooltip("Каждый из родителей жив: шанс при минимальном возрасте")]
        [SerializeField, Range(0f, 1f)] private float parentAliveAtMin = 0.9f;

        [Tooltip("…минус это за каждый год сверх")]
        [SerializeField, Range(0f, 1f)] private float parentAlivePerYear = 0.02f;

        [Tooltip("Супруг: шанс при минимальном возрасте")]
        [SerializeField, Range(0f, 1f)] private float spouseBase = 0.1f;

        [Tooltip("…плюс это за каждый год сверх")]
        [SerializeField, Range(0f, 1f)] private float spousePerYear = 0.03f;

        [Tooltip("Ось Люди к Одиночке: шанс супруга × (1 − это × сила полюса)")]
        [SerializeField, Range(0f, 1f)] private float loneSpouseCut = 0.5f;

        [Tooltip("Семейный: шанс супруга ×")]
        [SerializeField, Min(1f)] private float familySpouseMultiplier = 2f;

        [Tooltip("Детей не больше (бросок на каждого, только при супруге)")]
        [SerializeField, Min(0)] private int maxChildren = 3;

        [Tooltip("Ребёнок: шанс при минимальном возрасте")]
        [SerializeField, Range(0f, 1f)] private float childBase = 0.2f;

        [Tooltip("…плюс это за каждый год сверх")]
        [SerializeField, Range(0f, 1f)] private float childPerYear = 0.02f;

        [Tooltip("Ребёнок — сын: шанс")]
        [SerializeField, Range(0f, 1f)] private float childMaleChance = 0.5f;

        [Header("Плата семье")]
        [Tooltip("Плата раз в столько суток, в 00:00, считая от начала игры")]
        [SerializeField, Min(1)] private int paymentIntervalDays = 7;

        [Tooltip("Норма за родителя, монет за раз")]
        [SerializeField, Min(0)] private int parentCost = 6;

        [Tooltip("Норма за супруга, монет за раз")]
        [SerializeField, Min(0)] private int spouseCost = 10;

        [Tooltip("Норма за ребёнка, монет за раз")]
        [SerializeField, Min(0)] private int childCost = 6;

        [Tooltip("Больной родной: его норма ×")]
        [SerializeField, Min(1f)] private float illCostMultiplier = 2f;

        [Tooltip("Семейный платит первым и сверх нормы: норма × (1 + это)")]
        [SerializeField, Min(0f)] private float familyExtraShare = 0.3f;

        [Header("Нужда")]
        [Tooltip("Семья в нужде: стресс в сутки (00:00)")]
        [SerializeField, Min(0f)] private float needStressPerDay = 1f;

        [Tooltip("Семейный: стресс нужды ×")]
        [SerializeField, Min(0f)] private float familyNeedMultiplier = 2f;

        [Tooltip("Семья в нужде: цель довольства −")]
        [SerializeField, Min(0f)] private float needContentment = 10f;

        [Tooltip("Семья в нужде: мотив «Деньги» ×")]
        [SerializeField, Min(0f)] private float needMoneyMultiplier = 1.5f;

        [Header("Болезнь и смерть")]
        [Tooltip("Шанс заболеть в месяц: родитель")]
        [SerializeField, Range(0f, 1f)] private float illChanceParent = 0.06f;

        [Tooltip("…супруг")]
        [SerializeField, Range(0f, 1f)] private float illChanceSpouse = 0.02f;

        [Tooltip("…ребёнок")]
        [SerializeField, Range(0f, 1f)] private float illChanceChild = 0.04f;

        [Tooltip("Болезнь длится, недель (интервалов платы)")]
        [SerializeField] private IntRange illWeeks = new IntRange(2, 6);

        [Tooltip("Смерть за неделю болезни: норма этой недели заплачена полностью")]
        [SerializeField, Range(0f, 1f)] private float deathChancePaid = 0.03f;

        [Tooltip("…заплачена не полностью")]
        [SerializeField, Range(0f, 1f)] private float deathChanceUnpaid = 0.12f;

        [Tooltip("Весть о смерти: стресс +")]
        [SerializeField, Min(0f)] private float deathStress = 30f;

        [Tooltip("…у Семейного")]
        [SerializeField, Min(0f)] private float familyDeathStress = 50f;

        [Tooltip("Траур, суток")]
        [SerializeField, Min(0)] private int mourningDays = 30;

        [Tooltip("Траур: снятие стресса ×")]
        [SerializeField, Range(0f, 1f)] private float mourningStressRelief = 0.5f;

        public float ParentAliveAtMin => parentAliveAtMin;
        public float ParentAlivePerYear => parentAlivePerYear;
        public float SpouseBase => spouseBase;
        public float SpousePerYear => spousePerYear;
        public float LoneSpouseCut => loneSpouseCut;
        public float FamilySpouseMultiplier => familySpouseMultiplier;
        public int MaxChildren => maxChildren;
        public float ChildBase => childBase;
        public float ChildPerYear => childPerYear;
        public float ChildMaleChance => childMaleChance;
        public int PaymentIntervalDays => paymentIntervalDays;
        public int ParentCost => parentCost;
        public int SpouseCost => spouseCost;
        public int ChildCost => childCost;
        public float IllCostMultiplier => illCostMultiplier;
        public float FamilyExtraShare => familyExtraShare;
        public float NeedStressPerDay => needStressPerDay;
        public float FamilyNeedMultiplier => familyNeedMultiplier;
        public float NeedContentment => needContentment;
        public float NeedMoneyMultiplier => needMoneyMultiplier;
        public float IllChanceParent => illChanceParent;
        public float IllChanceSpouse => illChanceSpouse;
        public float IllChanceChild => illChanceChild;
        public IntRange IllWeeks => illWeeks;
        public float DeathChancePaid => deathChancePaid;
        public float DeathChanceUnpaid => deathChanceUnpaid;
        public float DeathStress => deathStress;
        public float FamilyDeathStress => familyDeathStress;
        public int MourningDays => mourningDays;
        public float MourningStressRelief => mourningStressRelief;
    }
}
