using System;
using UnityEngine;

namespace GuildMaster.Data
{
    /// <summary>
    /// Раздел <c>Economy</c>: комиссия, доход таверны, банкротство (numbers.md → Награды, Банкротство, ТЗ 10).
    /// Доходы от Общежития, Лазарета и двора — те же суммы, что расходы авантюристов (<see cref="ExpensesBalance"/>).
    /// </summary>
    [Serializable]
    public sealed class EconomyBalance
    {
        [Tooltip("Комиссия гильдии по умолчанию")]
        [SerializeField, Range(0f, 1f)] private float defaultCommission = 0.2f;

        [Tooltip("Пределы комиссии для игрока")]
        [SerializeField] private FloatRange commissionLimits = new FloatRange(0f, 0.5f);

        [Tooltip("Таверна: гильдии с каждой еды или выпивки")]
        [SerializeField, Min(0f)] private float tavernIncomePerServing = 0.5f;

        [Tooltip("Банкротство начинается после стольких дней с отрицательной казной подряд")]
        [SerializeField, Min(1)] private int bankruptcyStartDays = 30;

        [Tooltip("Срок на исправление, месяцев")]
        [SerializeField, Min(1)] private int bankruptcyMonths = 4;

        public float DefaultCommission => defaultCommission;
        public FloatRange CommissionLimits => commissionLimits;
        public float TavernIncomePerServing => tavernIncomePerServing;
        public int BankruptcyStartDays => bankruptcyStartDays;
        public int BankruptcyMonths => bankruptcyMonths;
    }
}
