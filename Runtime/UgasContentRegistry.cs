using System.Collections.Generic;
using Jbltx.Ugas.Schema;

namespace Jbltx.Ugas
{
    /// <summary>
    /// A name-keyed registry of loaded UGAS definitions (attribute sets, effects, abilities, tag
    /// registries). It lets the persistence layer resolve the <c>EffectClass</c> /
    /// <c>AbilityClass</c> references stored in a serialized gameplay controller back to their
    /// definitions during restore.
    /// </summary>
    public sealed class UgasContentRegistry
    {
        public Dictionary<string, AttributeSetDefinition> AttributeSets { get; } =
            new Dictionary<string, AttributeSetDefinition>();

        public Dictionary<string, GameplayEffectDefinition> Effects { get; } =
            new Dictionary<string, GameplayEffectDefinition>();

        public Dictionary<string, GameplayAbilityDefinition> Abilities { get; } =
            new Dictionary<string, GameplayAbilityDefinition>();

        public List<GameplayTagDefinition> Tags { get; } = new List<GameplayTagDefinition>();

        public void Add(AttributeSetDefinition set) => AttributeSets[set.Name] = set;
        public void Add(GameplayEffectDefinition effect) => Effects[effect.Name] = effect;
        public void Add(GameplayAbilityDefinition ability) => Abilities[ability.Name] = ability;

        public void Add(GameplayTagRegistry registry)
        {
            if (registry != null) Tags.AddRange(registry.Tags);
        }

        public AttributeSetDefinition GetAttributeSet(string name) =>
            AttributeSets.TryGetValue(name, out var v) ? v : null;

        public GameplayEffectDefinition GetEffect(string name) =>
            Effects.TryGetValue(name, out var v) ? v : null;

        public GameplayAbilityDefinition GetAbility(string name) =>
            Abilities.TryGetValue(name, out var v) ? v : null;
    }
}
