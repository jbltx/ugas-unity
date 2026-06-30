using System.Collections.Generic;
using Jbltx.Ugas.Definitions;

namespace Jbltx.Ugas.Runtime
{
    /// <summary>
    /// A live attribute-set instance (SPEC §6): a named collection of <see cref="RuntimeAttribute"/>
    /// created from an <see cref="AttributeSetDefinition"/>. Stores the attributes and resolves clamp
    /// bounds (including bounds that reference another attribute's current value, e.g. Health →
    /// MaxHealth). The actual aggregation is driven by <see cref="UgasController"/> via the shared
    /// kernel.
    /// </summary>
    public sealed class RuntimeAttributeSet
    {
        private readonly Dictionary<string, RuntimeAttribute> _attributes = new Dictionary<string, RuntimeAttribute>();

        public string Name { get; }
        public AttributeSetDefinition Definition { get; }
        public IReadOnlyList<string> Dependencies => Definition.Dependencies;

        public RuntimeAttributeSet(AttributeSetDefinition definition)
        {
            Definition = definition;
            Name = definition.SetName;
            foreach (var attrDef in definition.Attributes)
            {
                _attributes[attrDef.Name] = new RuntimeAttribute(attrDef);
            }
        }

        public IEnumerable<RuntimeAttribute> Attributes => _attributes.Values;

        public bool TryGet(string name, out RuntimeAttribute attribute) => _attributes.TryGetValue(name, out attribute);

        public RuntimeAttribute Get(string name) => _attributes.TryGetValue(name, out var a) ? a : null;

        public bool Has(string name) => _attributes.ContainsKey(name);

        /// <summary>
        /// Resolves the clamp bounds for an attribute. Attribute-reference bounds are looked up via
        /// <paramref name="resolveRef"/>, which should return the current value of the referenced
        /// attribute (searching across all sets on the controller).
        /// </summary>
        public static void ResolveClamp(in AttributeDefinition def, System.Func<string, float?> resolveRef,
            out bool hasMin, out float min, out bool hasMax, out float max)
        {
            ResolveBound(def.Min, resolveRef, out hasMin, out min);
            ResolveBound(def.Max, resolveRef, out hasMax, out max);
        }

        private static void ResolveBound(in AttributeBound bound, System.Func<string, float?> resolveRef,
            out bool has, out float value)
        {
            has = false;
            value = 0f;
            if (!bound.Enabled) return;

            if (bound.IsReference)
            {
                var refVal = resolveRef?.Invoke(bound.AttributeReference);
                if (refVal.HasValue) { has = true; value = refVal.Value; }
            }
            else
            {
                has = true;
                value = bound.Literal;
            }
        }
    }
}
