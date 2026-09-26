using System;
using UnityEngine;

namespace GuildMaster.Data
{
    /// <summary>Раздел <c>Expenses</c>: расходы авантюриста из своего кошелька.</summary>
    [Serializable]
    public sealed class ExpensesBalance
    {
        [Tooltip("Еда в день")]
        [SerializeField, Min(0f)] private float food = 2f;

        [Tooltip("Еда в день, если ел в таверне")]
        [SerializeField, Min(0f)] private float tavernFood = 3f;

        [Tooltip("Жильё в городе в день")]
        [SerializeField, Min(0f)] private float cityHousing = 3f;

        [Tooltip("Место в Общежитии в день (доход гильдии)")]
        [SerializeField, Min(0f)] private float dormHousing = 1f;

        [Tooltip("Лечение в Лазарете в день (доход гильдии)")]
        [SerializeField, Min(0f)] private float infirmary = 5f;

        [Tooltip("Тренировка на дворе в день (доход гильдии)")]
        [SerializeField, Min(0f)] private float training = 2f;

        [Tooltip("Выпивка за вечер в таверне")]
        [SerializeField] private IntRange drink = new IntRange(2, 5);

        [Tooltip("Пьёт в таверне, если стресс выше (или Пьяница)")]
        [SerializeField, Range(0f, 100f)] private float drinkStressThreshold = 30f;

        [Tooltip("Ремонт снаряжения разово")]
        [SerializeField] private IntRange gearRepair = new IntRange(10, 40);

        [Tooltip("«Расходы на неделю» для порогов кошелька: дней")]
        [SerializeField, Min(1)] private int reserveDays = 7;

        public float Food => food;
        public float TavernFood => tavernFood;
        public float CityHousing => cityHousing;
        public float DormHousing => dormHousing;
        public float Infirmary => infirmary;
        public float Training => training;
        public IntRange Drink => drink;
        public float DrinkStressThreshold => drinkStressThreshold;
        public IntRange GearRepair => gearRepair;
        public int ReserveDays => reserveDays;
    }
}
