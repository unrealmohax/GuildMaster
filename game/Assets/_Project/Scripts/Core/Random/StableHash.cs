namespace GuildMaster.Core
{
    /// <summary>
    /// Хеши, не зависящие от платформы и запуска (в отличие от <c>string.GetHashCode</c>).
    /// </summary>
    internal static class StableHash
    {
        public static ulong Fnv1a64(string text)
        {
            unchecked
            {
                ulong hash = 14695981039346656037UL;
                foreach (char c in text)
                {
                    hash ^= c;
                    hash *= 1099511628211UL;
                }
                return hash;
            }
        }

        public static ulong SplitMix64(ulong x)
        {
            unchecked
            {
                x += 0x9E3779B97F4A7C15UL;
                x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
                x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
                return x ^ (x >> 31);
            }
        }

        public static ulong Combine(ulong a, ulong b) => SplitMix64(a ^ SplitMix64(b));
    }
}
