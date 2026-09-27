using System;
using System.Collections.Generic;

namespace GuildMaster.Core
{
    /// <summary>
    /// Генератор случайных чисел с зерном (PCG32, XSH RR). Одинаковое зерно — одинаковая последовательность
    /// на любой платформе.
    /// </summary>
    public sealed class Rng
    {
        private const ulong Multiplier = 6364136223846793005UL;

        private ulong state;
        private readonly ulong increment;

        public Rng(ulong seed, ulong stream = 0)
        {
            increment = (stream << 1) | 1UL;
            state = 0;
            NextUInt();
            unchecked { state += seed; }
            NextUInt();
        }

        public uint NextUInt()
        {
            unchecked
            {
                ulong old = state;
                state = old * Multiplier + increment;
                uint xorShifted = (uint)(((old >> 18) ^ old) >> 27);
                int rotation = (int)(old >> 59);
                return (xorShifted >> rotation) | (xorShifted << (-rotation & 31));
            }
        }

        /// <summary>Равномерно в [0, bound), без смещения.</summary>
        public uint NextUInt(uint bound)
        {
            if (bound == 0) throw new ArgumentOutOfRangeException(nameof(bound), "bound must be positive");
            uint threshold = unchecked(0u - bound) % bound;
            while (true)
            {
                uint value = NextUInt();
                if (value >= threshold) return value % bound;
            }
        }

        /// <summary>Равномерно в [0, 1), 24 бита.</summary>
        public float NextFloat() => (NextUInt() >> 8) * (1f / 16777216f);

        /// <summary>Равномерно в [0, 1), 53 бита.</summary>
        public double NextDouble()
        {
            ulong high = NextUInt() >> 5;
            ulong low = NextUInt() >> 6;
            return (high * 67108864UL + low) * (1.0 / 9007199254740992.0);
        }

        /// <summary>Целое в [min, max): <paramref name="max"/> не включается, как в <c>UnityEngine.Random.Range</c>.</summary>
        public int Range(int min, int max)
        {
            if (max <= min) throw new ArgumentOutOfRangeException(nameof(max), $"Range({min}, {max}): max must be greater than min");
            return (int)(min + (long)NextUInt((uint)((long)max - min)));
        }

        /// <summary>Целое в [min, max], обе границы включаются.</summary>
        public int RangeInclusive(int min, int max)
        {
            if (max < min) throw new ArgumentOutOfRangeException(nameof(max), $"RangeInclusive({min}, {max}): max must not be less than min");
            long span = (long)max - min + 1;
            if (span > uint.MaxValue) return unchecked((int)NextUInt());
            return (int)(min + (long)NextUInt((uint)span));
        }

        /// <summary>Дробное в [min, max).</summary>
        public float Range(float min, float max)
        {
            if (max < min) throw new ArgumentOutOfRangeException(nameof(max), $"Range({min}, {max}): max must not be less than min");
            return min + (max - min) * NextFloat();
        }

        /// <summary>
        /// Истина с вероятностью <paramref name="probability"/> (доля 0..1). Всегда тратит одно число,
        /// даже при 0 и 1, — чтобы изменение шанса не сдвигало остальные броски потока.
        /// </summary>
        public bool Chance(float probability) => NextFloat() < probability;

        public T Pick<T>(IReadOnlyList<T> items)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            if (items.Count == 0) throw new ArgumentException("Cannot pick from an empty list", nameof(items));
            return items[Range(0, items.Count)];
        }

        /// <summary>Индекс по весам. Отрицательные веса считаются нулевыми; хотя бы один вес должен быть больше нуля.</summary>
        public int PickWeightedIndex(IReadOnlyList<float> weights)
        {
            if (weights == null) throw new ArgumentNullException(nameof(weights));

            double total = 0;
            int lastPositive = -1;
            for (int i = 0; i < weights.Count; i++)
            {
                if (weights[i] <= 0f) continue;
                total += weights[i];
                lastPositive = i;
            }
            if (lastPositive < 0) throw new ArgumentException("At least one weight must be positive", nameof(weights));

            double roll = NextDouble() * total;
            for (int i = 0; i < lastPositive; i++)
            {
                if (weights[i] <= 0f) continue;
                roll -= weights[i];
                if (roll < 0) return i;
            }
            return lastPositive;
        }

        public T PickWeighted<T>(IReadOnlyList<T> items, IReadOnlyList<float> weights)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            if (weights == null) throw new ArgumentNullException(nameof(weights));
            if (items.Count != weights.Count) throw new ArgumentException("Items and weights must have the same length");
            return items[PickWeightedIndex(weights)];
        }

        /// <summary>Нормальное распределение (Бокс — Мюллер). Тратит четыре числа (два double) на вызов.</summary>
        public float Normal(float mean, float standardDeviation)
        {
            double u1 = 1.0 - NextDouble();
            double u2 = NextDouble();
            double z = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
            return (float)(mean + standardDeviation * z);
        }
    }
}
