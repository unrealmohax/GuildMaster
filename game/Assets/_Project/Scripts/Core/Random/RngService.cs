using System;
using System.Collections.Generic;

namespace GuildMaster.Core
{
    /// <summary>
    /// Потоки случайных чисел: у каждой системы свой, с зерном = хеш(мастер-зерно, имя потока).
    /// Новая проверка в одной системе не сдвигает броски в других.
    /// </summary>
    public sealed class RngService
    {
        private readonly Dictionary<string, Rng> streams = new Dictionary<string, Rng>(StringComparer.Ordinal);

        public RngService(uint masterSeed)
        {
            MasterSeed = masterSeed;
        }

        public uint MasterSeed { get; }

        public Rng Stream(string name)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("Stream name is required", nameof(name));
            if (!streams.TryGetValue(name, out Rng rng))
            {
                ulong seed = StableHash.Combine(MasterSeed, StableHash.Fnv1a64(name));
                rng = new Rng(seed, StableHash.SplitMix64(seed));
                streams.Add(name, rng);
            }
            return rng;
        }
    }
}
