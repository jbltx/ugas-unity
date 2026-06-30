using Jbltx.Ugas.Schema;

namespace Jbltx.Ugas.Attributes
{
    /// <summary>
    /// A resolved modifier ready to feed the aggregation pipeline: the target attribute, the
    /// operation, the already-evaluated numeric magnitude, an optional channel, and (for Override
    /// conflict resolution) the source effect's priority and a monotonic application sequence used
    /// as the LIFO tie-breaker.
    /// </summary>
    /// <remarks>
    /// Magnitudes are resolved <i>before</i> reaching the aggregator (the schema's
    /// <see cref="MagnitudeDefinition"/> may reference attributes/curves/callers); the aggregator
    /// deals only in concrete numbers so the pipeline math stays pure and testable.
    /// </remarks>
    public readonly struct AttributeModifier
    {
        public string AttributeName { get; }
        public ModifierOperation Operation { get; }

        /// <summary>The resolved numeric magnitude. For Multiply this is the signed bonus (e.g. +0.25).</summary>
        public float Magnitude { get; }

        /// <summary>Optional aggregation channel name; null/empty means the implicit global channel.</summary>
        public string Channel { get; }

        /// <summary>Source effect priority, used for Override conflict resolution (higher wins).</summary>
        public int Priority { get; }

        /// <summary>Monotonic application order, used as the LIFO tie-breaker on equal priority.</summary>
        public long Sequence { get; }

        public AttributeModifier(
            string attributeName,
            ModifierOperation operation,
            float magnitude,
            string channel = null,
            int priority = 0,
            long sequence = 0)
        {
            AttributeName = attributeName;
            Operation = operation;
            Magnitude = magnitude;
            Channel = channel;
            Priority = priority;
            Sequence = sequence;
        }
    }
}
