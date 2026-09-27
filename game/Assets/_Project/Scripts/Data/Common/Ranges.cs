using System;
using UnityEngine;

namespace GuildMaster.Data
{
    /// <summary>Диапазон целых «от–до» включительно (3–7 дней). Валидатор проверяет min ≤ max.</summary>
    [Serializable]
    public struct IntRange
    {
        [SerializeField] private int min;
        [SerializeField] private int max;

        public IntRange(int min, int max)
        {
            this.min = min;
            this.max = max;
        }

        public int Min => min;
        public int Max => max;

        public override string ToString() => $"{min}–{max}";
    }

    /// <summary>Диапазон дробных «от–до» (0,8–1,2). Валидатор проверяет min ≤ max.</summary>
    [Serializable]
    public struct FloatRange
    {
        [SerializeField] private float min;
        [SerializeField] private float max;

        public FloatRange(float min, float max)
        {
            this.min = min;
            this.max = max;
        }

        public float Min => min;
        public float Max => max;

        public override string ToString() => $"{min}–{max}";
    }

    /// <summary>Ссылка на ассет может быть пустой: валидатор не считает это ошибкой.</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class OptionalReferenceAttribute : Attribute
    {
    }
}
