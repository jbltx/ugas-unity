namespace Jbltx.Ugas.Prediction
{
    /// <summary>
    /// A deterministic, seedable random stream for predicted gameplay (SPEC §9 <c>context.RNG</c> /
    /// §13.8.1). Seeded from a base seed plus a sub-stream id so each predicted activation draws a
    /// disjoint, reproducible sequence: the predicting client and the authoritative server, given the
    /// same server-coordinated seed, produce identical results (no rollback from RNG drift alone), and
    /// reconciliation replay (§13.5) re-draws the same values. The <see cref="DrawIndex"/> is the
    /// monotonic draw counter of the §13.8.1 <c>(Sub, draw index)</c> scheme.
    /// </summary>
    /// <remarks>
    /// Uses SplitMix64 — small, fast, and platform-stable: integer operations only, no floating-point
    /// in the generator, so the sequence is identical across machines and architectures. A mutable
    /// value type; copy it to fork a stream at a known position. Consumers: the <c>context.RNG</c> of a
    /// predicted Execution Calculation (§9); wiring that seam + <c>PredictionKey</c> is the networking
    /// follow-up.
    /// </remarks>
    public struct UgasRandom
    {
        private const ulong GoldenGamma = 0x9E3779B97F4A7C15UL;

        private ulong _state;

        /// <summary>Number of draws taken from this stream (§13.8.1 monotonic draw index).</summary>
        public uint DrawIndex { get; private set; }

        /// <summary>
        /// Creates a stream from a base <paramref name="seed"/> and a <paramref name="sub"/>-stream id.
        /// Distinct <paramref name="sub"/> values yield disjoint sequences; identical <c>(seed, sub)</c>
        /// pairs yield identical sequences.
        /// </summary>
        public UgasRandom(ulong seed, uint sub = 0)
        {
            // Fold the sub-stream id into the seed so distinct subs start from unrelated states (§13.8.1).
            _state = (seed ^ GoldenGamma) + Mix(sub + 0x2545F4914F6CDD1DUL);
            DrawIndex = 0;
        }

        /// <summary>The next 64-bit draw; advances the stream and the draw index.</summary>
        public ulong NextUInt64()
        {
            DrawIndex++;
            _state += GoldenGamma;
            ulong z = _state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        /// <summary>A float in <c>[0, 1)</c> with 2^-24 resolution.</summary>
        public float NextFloat()
        {
            return (NextUInt64() >> 40) * (1.0f / 16777216.0f);
        }

        /// <summary>A float in <c>[min, max)</c>.</summary>
        public float NextRange(float min, float max) => min + NextFloat() * (max - min);

        /// <summary>An int in <c>[minInclusive, maxExclusive)</c>; returns <paramref name="minInclusive"/> for an empty range.</summary>
        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            ulong range = (ulong)((long)maxExclusive - minInclusive);
            return minInclusive + (int)(NextUInt64() % range);
        }

        /// <summary>True with probability <paramref name="chance"/> (e.g. a crit roll); clamped to [0, 1].</summary>
        public bool Chance(float chance)
        {
            if (chance <= 0f) return false;
            if (chance >= 1f) return true;
            return NextFloat() < chance;
        }

        private static ulong Mix(ulong z)
        {
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }
}
