using System;
using UnityEngine;

namespace GuildMaster.Data
{
    /// <summary>
    /// Раздел <c>Tension</c>: моменты напряжения, беглец (numbers.md → Моменты напряжения, ТЗ 09).
    /// Добавки черт к шансам (трус, Железные нервы, Ветеран, дезертир, безрассудный) — эффекты черт.
    /// </summary>
    [Serializable]
    public sealed class TensionBalance
    {
        [Header("Паника: max(0, (Стресс − Хладнокровие + сдвиг) / делитель)")]
        [SerializeField] private float panicOffset = 20f;
        [SerializeField, Min(1f)] private float panicDivisor = 100f;

        [Tooltip("Паника: профиль × в следующем раунде")]
        [SerializeField, Range(0f, 1f)] private float panicProfileMultiplier = 0.5f;

        [Tooltip("Бросок вперёд: вклад × в следующем раунде")]
        [SerializeField, Min(1f)] private float rushProfileMultiplier = 1.3f;

        [Header("Геройство")]
        [Tooltip("Шанс, когда товарищ должен получить тяжёлую рану")]
        [SerializeField, Range(0f, 1f)] private float heroChance = 0.1f;
        [SerializeField, Range(0f, 100f)] private float heroMinComposure = 60f;

        [Tooltip("Ось Люди от")]
        [SerializeField, Range(-100f, 100f)] private float heroMinPeopleAxis = 30f;

        [Tooltip("Отношения с товарищем +")]
        [SerializeField] private float heroRelation = 20f;

        [Header("Бегство и беглец")]
        [Tooltip("Стресс брошенных")]
        [SerializeField] private float abandonedStress = 5f;

        [Tooltip("Беглец возвращается в гильдию с шансом")]
        [SerializeField, Range(0f, 1f)] private float fugitiveReturnChance = 0.5f;

        [Tooltip("…через дней")]
        [SerializeField] private IntRange fugitiveReturnDays = new IntRange(1, 3);

        [Header("Прощённый беглец (дилемма «Беглец вернулся»)")]
        [Tooltip("В следующем моменте напряжения: шанс искупления (иначе — срыв в бегство)")]
        [SerializeField, Range(0f, 1f)] private float redemptionChance = 0.5f;

        [Tooltip("Искупление: шанс геройства ×")]
        [SerializeField, Min(1f)] private float redemptionHeroMultiplier = 3f;

        [Tooltip("Не искупил: шанс бегства ×")]
        [SerializeField, Min(1f)] private float relapseFleeMultiplier = 1.5f;

        public float PanicOffset => panicOffset;
        public float PanicDivisor => panicDivisor;
        public float PanicProfileMultiplier => panicProfileMultiplier;
        public float RushProfileMultiplier => rushProfileMultiplier;
        public float HeroChance => heroChance;
        public float HeroMinComposure => heroMinComposure;
        public float HeroMinPeopleAxis => heroMinPeopleAxis;
        public float HeroRelation => heroRelation;
        public float AbandonedStress => abandonedStress;
        public float FugitiveReturnChance => fugitiveReturnChance;
        public IntRange FugitiveReturnDays => fugitiveReturnDays;
        public float RedemptionChance => redemptionChance;
        public float RedemptionHeroMultiplier => redemptionHeroMultiplier;
        public float RelapseFleeMultiplier => relapseFleeMultiplier;
    }
}
