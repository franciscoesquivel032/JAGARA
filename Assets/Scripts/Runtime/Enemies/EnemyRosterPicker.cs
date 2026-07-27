using System;

namespace Jagara.Runtime.Enemies
{
    /// <summary>
    /// Static, pure weighted-random selection over an enemy roster's weights.
    /// No LINQ, no allocations beyond the loop itself.
    /// </summary>
    public static class EnemyRosterPicker
    {
        /// <summary>
        /// Picks an index into <paramref name="weights"/> with probability
        /// proportional to its weight, using <paramref name="rng"/> as the
        /// sole source of randomness (deterministic given the same seeded
        /// <see cref="System.Random"/> instance).
        /// Returns -1 if <paramref name="weights"/> is null/empty or every
        /// weight is zero (no valid pick). Zero-weight entries are never
        /// selected.
        /// </summary>
        public static int PickIndex(int[] weights, Random rng)
        {
            if (weights == null || weights.Length == 0)
            {
                return -1;
            }

            int total = 0;
            for (int i = 0; i < weights.Length; i++)
            {
                total += weights[i];
            }

            if (total <= 0)
            {
                return -1;
            }

            int draw = rng.Next(total);
            int accumulated = 0;
            for (int i = 0; i < weights.Length; i++)
            {
                accumulated += weights[i];
                if (draw < accumulated)
                {
                    return i;
                }
            }

            // Unreachable in practice (draw is always < total), but keep a
            // safe fallback instead of an implicit -2/garbage return.
            return -1;
        }
    }
}
