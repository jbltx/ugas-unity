using System.Collections.Generic;

namespace Jbltx.Ugas.Attributes
{
    /// <summary>
    /// Implements the SPEC §5 modifier-aggregation pipeline. Pure and stateless — given a base
    /// value, the modifiers targeting an attribute, and optional clamp bounds, it returns the
    /// computed current value.
    /// </summary>
    /// <remarks>
    /// The pipeline order is:
    /// <code>
    /// V_current = clamp( ( V_base + Σ Add ) × Π_channels( 1 + Σ Multiply_in_channel ) + Σ AddPost , min, max )
    /// </code>
    /// with an Override step that, when present, replaces the computed result (highest
    /// <c>Priority</c> wins; on a tie the most-recently-applied modifier wins) before clamping.
    /// Key channel rule: <i>Multiply</i> bonuses sharing a channel are summed (two +20% in one
    /// channel give ×1.40); different channels multiply (×1.40 and ×1.30 give ×1.82). A Multiply
    /// with no channel forms its own implicit channel.
    /// </remarks>
    public static class AttributeAggregator
    {
        /// <summary>Computes the current value for one attribute. <paramref name="modifiers"/> may
        /// include modifiers for other attributes; they are ignored unless their name matches.</summary>
        public static float Aggregate(
            string attributeName,
            float baseValue,
            IEnumerable<AttributeModifier> modifiers,
            float? min = null,
            float? max = null)
        {
            float flatAdd = 0f;
            float flatAddPost = 0f;

            // Per-channel summed multiply bonuses. The null/empty channel is bucketed under each
            // distinct unnamed modifier... but the spec says an unnamed Multiply is its own implicit
            // channel, so we keep a separate list of standalone factors.
            var channelBonus = new Dictionary<string, float>();
            var standaloneFactors = new List<float>();

            bool hasOverride = false;
            float overrideValue = 0f;
            int overridePriority = int.MinValue;
            long overrideSequence = long.MinValue;

            foreach (var mod in modifiers)
            {
                if (mod.AttributeName != attributeName) continue;

                switch (mod.Operation)
                {
                    case Schema.ModifierOperation.Add:
                        flatAdd += mod.Magnitude;
                        break;

                    case Schema.ModifierOperation.AddPost:
                        flatAddPost += mod.Magnitude;
                        break;

                    case Schema.ModifierOperation.Multiply:
                        if (string.IsNullOrEmpty(mod.Channel))
                        {
                            // Implicit singleton channel: contributes (1 + bonus) independently.
                            standaloneFactors.Add(1f + mod.Magnitude);
                        }
                        else
                        {
                            channelBonus.TryGetValue(mod.Channel, out float sum);
                            channelBonus[mod.Channel] = sum + mod.Magnitude;
                        }
                        break;

                    case Schema.ModifierOperation.Override:
                        // Highest priority wins; LIFO (highest sequence) on tie.
                        if (!hasOverride
                            || mod.Priority > overridePriority
                            || (mod.Priority == overridePriority && mod.Sequence >= overrideSequence))
                        {
                            hasOverride = true;
                            overrideValue = mod.Magnitude;
                            overridePriority = mod.Priority;
                            overrideSequence = mod.Sequence;
                        }
                        break;
                }
            }

            float value = baseValue + flatAdd;

            // Each named channel contributes one factor (1 + Σ bonuses); factors multiply together.
            foreach (var kv in channelBonus)
            {
                value *= 1f + kv.Value;
            }
            foreach (var factor in standaloneFactors)
            {
                value *= factor;
            }

            value += flatAddPost;

            if (hasOverride)
            {
                value = overrideValue;
            }

            if (min.HasValue && value < min.Value) value = min.Value;
            if (max.HasValue && value > max.Value) value = max.Value;

            return value;
        }
    }
}
