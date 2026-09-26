using System;
using UnityEngine;

namespace GuildMaster.Data
{
    /// <summary>Раздел <c>Health</c>: раны и лечение (numbers.md → Здоровье, ТЗ 05). Увечье — эффект черты «Калека».</summary>
    [Serializable]
    public sealed class HealthBalance
    {
        [Tooltip("Лёгкая рана, дней")]
        [SerializeField] private IntRange lightWoundDays = new IntRange(3, 5);

        [Tooltip("Тяжёлая рана, дней")]
        [SerializeField] private IntRange heavyWoundDays = new IntRange(14, 21);

        [Tooltip("Лёгкая рана: профиль ×")]
        [SerializeField, Range(0f, 1f)] private float lightWoundProfileMultiplier = 0.85f;

        [Tooltip("В Лазарете: срок ×")]
        [SerializeField, Min(0.01f)] private float infirmaryTimeMultiplier = 0.7f;

        [Tooltip("Без Лазарета: срок ×")]
        [SerializeField, Min(0.01f)] private float noInfirmaryTimeMultiplier = 1.5f;

        [Tooltip("Без Лазарета: шанс осложнения при тяжёлой ране")]
        [SerializeField, Range(0f, 1f)] private float complicationChance = 0.2f;

        [Tooltip("Осложнение: к тяжёлой ране дней")]
        [SerializeField, Min(0)] private int complicationExtraDays = 7;

        [Tooltip("Осложнение: стресс")]
        [SerializeField] private float complicationStress = 10f;

        [Tooltip("Лекарь в группе: шанс снизить тяжесть раны на ступень")]
        [SerializeField, Range(0f, 1f)] private float fieldMedicChance = 0.3f;

        public IntRange LightWoundDays => lightWoundDays;
        public IntRange HeavyWoundDays => heavyWoundDays;
        public float LightWoundProfileMultiplier => lightWoundProfileMultiplier;
        public float InfirmaryTimeMultiplier => infirmaryTimeMultiplier;
        public float NoInfirmaryTimeMultiplier => noInfirmaryTimeMultiplier;
        public float ComplicationChance => complicationChance;
        public int ComplicationExtraDays => complicationExtraDays;
        public float ComplicationStress => complicationStress;
        public float FieldMedicChance => fieldMedicChance;
    }
}
