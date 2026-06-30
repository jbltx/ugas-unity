using Jbltx.Ugas.Definitions;

namespace Jbltx.Ugas.Runtime
{
    /// <summary>
    /// A live attribute instance implementing the dual-value Base/Current pattern (SPEC §5).
    /// <see cref="BaseValue"/> is permanent (changed by Instant effects, persisted);
    /// <see cref="CurrentValue"/> is derived by the aggregation kernel from the base value plus all
    /// active modifiers and is not persisted.
    /// </summary>
    public sealed class RuntimeAttribute
    {
        public AttributeDefinition Definition { get; }
        public string Name => Definition.Name;

        public float BaseValue;
        public float CurrentValue;

        public RuntimeAttribute(in AttributeDefinition definition)
        {
            Definition = definition;
            BaseValue = definition.DefaultBaseValue;
            CurrentValue = definition.DefaultBaseValue;
        }

        public override string ToString() => $"{Name}(Base={BaseValue}, Current={CurrentValue})";
    }
}
