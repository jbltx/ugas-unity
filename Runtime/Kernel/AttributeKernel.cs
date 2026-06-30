using System;

namespace Jbltx.Ugas.Kernel
{
    /// <summary>
    /// The shared, hot-path aggregation kernel implementing the SPEC §5 modifier pipeline. These are
    /// pure, allocation-free, UnityEngine-free static methods over blittable
    /// <see cref="ModifierSample"/> values, so the <i>same</i> code is called by both the managed
    /// MonoBehaviour runtime and the Burst-compiled DOTS jobs. This is a shared kernel, not a
    /// portability layer.
    /// </summary>
    /// <remarks>
    /// Pipeline (identical to the value that yields the spec's worked example WeaponDamage = 18.0):
    /// <code>
    /// V = clamp( ( base + Σ Add ) × Π_channels( 1 + Σ Multiply_in_channel ) + Σ AddPost , min, max )
    /// </code>
    /// with an Override (highest Priority, then highest Sequence) replacing the result before
    /// clamping. The method is Burst-compatible: it takes <see cref="ReadOnlySpan{T}"/> inputs and a
    /// caller-provided <see cref="Span{T}"/> scratch buffer for per-channel sums, so it never
    /// allocates.
    /// </remarks>
    public static class AttributeKernel
    {
        /// <summary>
        /// Aggregates <paramref name="modifiers"/> over <paramref name="baseValue"/>.
        /// </summary>
        /// <param name="baseValue">The attribute's permanent base value.</param>
        /// <param name="modifiers">Resolved modifiers targeting this attribute (already filtered).</param>
        /// <param name="channelScratch">
        /// Scratch buffer for per-channel multiply sums. Its length defines how many distinct named
        /// channels are addressable; a modifier's <see cref="ModifierSample.ChannelId"/> indexes into
        /// it. Pass <c>default</c> (empty) if no named channels are used. The buffer is fully
        /// overwritten; callers may reuse one buffer across attributes.
        /// </param>
        /// <param name="hasMin">Whether <paramref name="min"/> applies.</param>
        /// <param name="min">Lower clamp bound.</param>
        /// <param name="hasMax">Whether <paramref name="max"/> applies.</param>
        /// <param name="max">Upper clamp bound.</param>
        public static float Aggregate(
            float baseValue,
            ReadOnlySpan<ModifierSample> modifiers,
            Span<float> channelScratch,
            bool hasMin = false, float min = 0f,
            bool hasMax = false, float max = 0f)
        {
            float flatAdd = 0f;
            float flatAddPost = 0f;

            // Reset channel sums; track which channels were actually touched so untouched buffer
            // slots don't contribute a spurious ×1 factor (they would be harmless, but we skip them).
            for (int i = 0; i < channelScratch.Length; i++) channelScratch[i] = 0f;

            // Product of implicit singleton multiply factors (unchanneled multiplies).
            float standaloneProduct = 1f;
            bool anyChannelUsed = false;

            bool hasOverride = false;
            float overrideValue = 0f;
            int overridePriority = int.MinValue;
            long overrideSequence = long.MinValue;

            for (int i = 0; i < modifiers.Length; i++)
            {
                ModifierSample m = modifiers[i];
                switch (m.Op)
                {
                    case ModifierOp.Add:
                        flatAdd += m.Magnitude;
                        break;

                    case ModifierOp.AddPost:
                        flatAddPost += m.Magnitude;
                        break;

                    case ModifierOp.Multiply:
                        if (m.ChannelId < 0 || m.ChannelId >= channelScratch.Length)
                        {
                            // Implicit singleton channel: contributes (1 + bonus) on its own.
                            standaloneProduct *= 1f + m.Magnitude;
                        }
                        else
                        {
                            channelScratch[m.ChannelId] += m.Magnitude;
                            anyChannelUsed = true;
                        }
                        break;

                    case ModifierOp.Override:
                        if (!hasOverride
                            || m.Priority > overridePriority
                            || (m.Priority == overridePriority && m.Sequence >= overrideSequence))
                        {
                            hasOverride = true;
                            overrideValue = m.Magnitude;
                            overridePriority = m.Priority;
                            overrideSequence = m.Sequence;
                        }
                        break;
                }
            }

            float value = baseValue + flatAdd;

            if (anyChannelUsed)
            {
                for (int c = 0; c < channelScratch.Length; c++)
                {
                    float sum = channelScratch[c];
                    if (sum != 0f) value *= 1f + sum;
                }
            }

            value *= standaloneProduct;
            value += flatAddPost;

            if (hasOverride) value = overrideValue;

            if (hasMin && value < min) value = min;
            if (hasMax && value > max) value = max;

            return value;
        }
    }
}
