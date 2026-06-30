using System.Collections.Generic;
using Jbltx.Ugas.Schema;

namespace Jbltx.Ugas.Attributes
{
    /// <summary>
    /// A runtime attribute-set instance (SPEC §6): a named collection of <see cref="Attribute"/>
    /// objects created from an <see cref="AttributeSetDefinition"/>. Owns recomputation of current
    /// values via the <see cref="AttributeAggregator"/>, including resolution of clamp bounds that
    /// reference other attributes (e.g. Health clamps to MaxHealth's current value).
    /// </summary>
    public sealed class AttributeSet
    {
        private readonly Dictionary<string, Attribute> _attributes = new Dictionary<string, Attribute>();

        public string Name { get; }
        public AttributeSetDefinition Definition { get; }

        /// <summary>Names of attribute sets this set depends on (SPEC §6 cross-set dependencies).</summary>
        public IReadOnlyList<string> Dependencies => Definition?.Dependencies ?? new List<string>();

        public AttributeSet(AttributeSetDefinition definition)
        {
            Definition = definition;
            Name = definition.Name;
            foreach (var attrDef in definition.Attributes)
            {
                _attributes[attrDef.Name] = new Attribute(attrDef);
            }
        }

        public IEnumerable<Attribute> Attributes => _attributes.Values;

        public bool TryGet(string name, out Attribute attribute) => _attributes.TryGetValue(name, out attribute);

        public Attribute Get(string name) => _attributes.TryGetValue(name, out var a) ? a : null;

        public bool Has(string name) => _attributes.ContainsKey(name);

        public float GetBaseValue(string name) => Get(name)?.BaseValue ?? 0f;

        public float GetCurrentValue(string name) => Get(name)?.CurrentValue ?? 0f;

        /// <summary>Sets the base value of an attribute (the only value Instant effects change).</summary>
        public void SetBaseValue(string name, float value)
        {
            if (_attributes.TryGetValue(name, out var a)) a.BaseValue = value;
        }

        /// <summary>
        /// Recomputes <see cref="Attribute.CurrentValue"/> for every attribute in the set from its
        /// base value and the supplied modifiers. Clamp bounds that reference another attribute are
        /// resolved against that attribute's current base value first (a single pass; the spec
        /// forbids circular dependencies).
        /// </summary>
        public void Recalculate(IEnumerable<AttributeModifier> modifiers)
        {
            // Materialize once so we can iterate per attribute.
            var modList = modifiers as IReadOnlyList<AttributeModifier> ?? new List<AttributeModifier>(modifiers);

            foreach (var attr in _attributes.Values)
            {
                ResolveClamp(attr.Definition.Clamping, out float? min, out float? max);
                attr.CurrentValue = AttributeAggregator.Aggregate(
                    attr.Name, attr.BaseValue, modList, min, max);
            }
        }

        private void ResolveClamp(AttributeClamping clamping, out float? min, out float? max)
        {
            min = null;
            max = null;
            if (clamping == null) return;
            min = ResolveBound(clamping.Min);
            max = ResolveBound(clamping.Max);
        }

        private float? ResolveBound(AttributeBound bound)
        {
            if (bound == null) return null;
            if (bound.IsLiteral) return bound.Literal;
            // Reference: use the referenced attribute's current value (falls back to base if not yet computed).
            if (!string.IsNullOrEmpty(bound.AttributeReference) &&
                _attributes.TryGetValue(bound.AttributeReference, out var refAttr))
            {
                return refAttr.CurrentValue;
            }
            return null;
        }
    }
}
