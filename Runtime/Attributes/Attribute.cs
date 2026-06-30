using Jbltx.Ugas.Schema;

namespace Jbltx.Ugas.Attributes
{
    /// <summary>
    /// A runtime attribute instance implementing the dual-value Base/Current pattern (SPEC §5).
    /// </summary>
    /// <remarks>
    /// <para><see cref="BaseValue"/> is the permanent value, changed only by Instant effects and
    /// persisted in save data.</para>
    /// <para><see cref="CurrentValue"/> is derived: it is recomputed by the
    /// <see cref="AttributeAggregator"/> from the base value plus all active modifiers, and is not
    /// itself persisted.</para>
    /// </remarks>
    public sealed class Attribute
    {
        /// <summary>The static definition this instance was created from.</summary>
        public AttributeDefinition Definition { get; }

        /// <summary>Attribute name (from the definition).</summary>
        public string Name => Definition.Name;

        /// <summary>Permanent base value. Set by Instant effects and on load.</summary>
        public float BaseValue { get; set; }

        /// <summary>Derived value (base + active modifiers, clamped). Maintained by the aggregator.</summary>
        public float CurrentValue { get; set; }

        public Attribute(AttributeDefinition definition)
        {
            Definition = definition;
            BaseValue = definition.DefaultBaseValue;
            CurrentValue = definition.DefaultBaseValue;
        }

        public Attribute(AttributeDefinition definition, float baseValue, float currentValue)
        {
            Definition = definition;
            BaseValue = baseValue;
            CurrentValue = currentValue;
        }

        public override string ToString() => $"{Name}(Base={BaseValue}, Current={CurrentValue})";
    }
}
