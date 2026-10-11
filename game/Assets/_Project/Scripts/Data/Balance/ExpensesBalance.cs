using System;
using UnityEngine;

namespace GuildMaster.Data
{
    /// <summary>Раздел <c>Expenses</c>: расходы авантюриста из своего кошелька, кроме еды (она — в <see cref="FoodBalance"/>).</summary>
    [Serializable]
    public sealed class ExpensesBalance
    {
        [Tooltip("Жильё в городе в день")]
        [SerializeField, Min(0f)] private float cityHousing = 3f;

        [Tooltip("Место в Общежитии в день (доход гильдии)")]
        [SerializeField, Min(0f)] private float dormHousing = 1f;

        [Tooltip("Лечение в Лазарете в день (доход гильдии)")]
        [SerializeField, Min(0f)] private float infirmary = 5f;

        [Tooltip("Тренировка на дворе в день (доход гильдии)")]
        [SerializeField, Min(0f)] private float training = 2f;

        [Tooltip("Цена кружки выпивки")]
        [SerializeField] private IntRange drink = new IntRange(1, 1);

        [Tooltip("Тянет в таверну выпить, если стресс выше (или Пьяница); выше него каждые drinkStressStep — ещё кружка вечером")]
        [SerializeField, Range(0f, 100f)] private float drinkStressThreshold = 30f;

        [Tooltip("Ремонт снаряжения разово")]
        [SerializeField] private IntRange gearRepair = new IntRange(10, 40);

        [Tooltip("«Расходы на неделю» для порогов кошелька: дней")]
        [SerializeField, Min(1)] private int reserveDays = 7;

        [Tooltip("Кружек за вечер в городе")]
        [SerializeField] private IntRange eveningDrinks = new IntRange(3, 4);

        [Tooltip("Кружек за вечер в городе у Пьяницы")]
        [SerializeField] private IntRange drunkardEveningDrinks = new IntRange(10, 15);

        [Tooltip("Стресс выше drinkStressThreshold: ещё кружка за каждые столько")]
        [SerializeField, Min(1f)] private float drinkStressStep = 20f;

        public float CityHousing => cityHousing;
        public float DormHousing => dormHousing;
        public float Infirmary => infirmary;
        public float Training => training;
        public IntRange Drink => drink;
        public float DrinkStressThreshold => drinkStressThreshold;
        public IntRange GearRepair => gearRepair;
        public int ReserveDays => reserveDays;
        public IntRange EveningDrinks => eveningDrinks;
        public IntRange DrunkardEveningDrinks => drunkardEveningDrinks;
        public float DrinkStressStep => drinkStressStep;
    }
}
