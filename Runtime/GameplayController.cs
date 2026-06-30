using System.Collections.Generic;
using Jbltx.Ugas.Abilities;
using Jbltx.Ugas.Attributes;
using Jbltx.Ugas.Effects;
using Jbltx.Ugas.Schema;
using Jbltx.Ugas.Tags;

namespace Jbltx.Ugas
{
    /// <summary>
    /// The reference Gameplay Controller (SPEC §4): authoritative container wiring the four pillars.
    /// It owns attribute sets, the owned-tag container, granted abilities, and the effects system;
    /// it implements <see cref="IEffectTarget"/> so effects can mutate attributes and tags; and it
    /// supports save/restore per SPEC §14.
    /// </summary>
    public sealed class GameplayController : IGameplayController, IEffectTarget
    {
        private readonly Dictionary<string, AttributeSet> _attributeSets = new Dictionary<string, AttributeSet>();
        private readonly Dictionary<string, GameplayAbility> _abilities = new Dictionary<string, GameplayAbility>();
        private readonly GameplayEffectsSystem _effects;
        private long _modifierSequence;

        public ActorReference OwnerActor { get; set; }
        public ActorReference AvatarActor { get; set; }
        public GameplayTagContainer OwnedTags { get; } = new GameplayTagContainer();
        public IGameplayEffectsSystem Effects => _effects;
        public IReadOnlyDictionary<string, AttributeSet> AttributeSets => _attributeSets;

        public GameplayController()
        {
            _effects = new GameplayEffectsSystem(this);
        }

        public void RegisterAttributeSet(AttributeSet set)
        {
            // Validate declared dependencies are present (SPEC §6).
            foreach (var dep in set.Dependencies)
            {
                if (!_attributeSets.ContainsKey(dep))
                {
                    throw new UgasDependencyException(
                        $"AttributeSet '{set.Name}' requires '{dep}', which is not registered.");
                }
            }
            _attributeSets[set.Name] = set;
            RecalculateAttributes();
        }

        public Attribute FindAttribute(string attributeName)
        {
            foreach (var set in _attributeSets.Values)
            {
                if (set.TryGet(attributeName, out var attr)) return attr;
            }
            return null;
        }

        public IGameplayAbility GrantAbility(GameplayAbilityDefinition ability, int level = 1, string inputId = null)
        {
            var instance = new GameplayAbility(ability, level);
            instance.Grant();
            _abilities[ability.Name] = instance;
            return instance;
        }

        public bool TryActivateAbility(string abilityName)
        {
            return _abilities.TryGetValue(abilityName, out var ability) && ability.TryActivate(this);
        }

        public ActiveGameplayEffect ApplyEffect(GameplayEffectDefinition effect, int level = 1, string sourceAbility = null)
        {
            string instigator = OwnerActor?.ActorID;
            return _effects.ApplyEffect(effect, level, instigator, sourceAbility);
        }

        public void Tick(float deltaSeconds)
        {
            _effects.Tick(deltaSeconds);
            // TODO(tasks): tick active ability tasks here (SPEC §10).
        }

        // ---- IEffectTarget ----

        public float ResolveMagnitude(MagnitudeDefinition magnitude, int level)
        {
            if (magnitude == null) return 0f;
            switch (magnitude.Type)
            {
                case MagnitudeType.ScalableFloat:
                    // TODO: scale Value by the named Curve at this level when curve tables exist.
                    return magnitude.Value;

                case MagnitudeType.AttributeBased:
                {
                    // Source/Target distinction collapses to this GC in the single-authority scaffold.
                    var backing = FindAttribute(magnitude.BackingAttribute);
                    float baseVal = backing?.CurrentValue ?? 0f;
                    return (baseVal + magnitude.PreMultiplyAdditive) * magnitude.Coefficient
                           + magnitude.PostMultiplyAdditive;
                }

                case MagnitudeType.SetByCaller:
                    // TODO: look up the runtime-provided magnitude by DataTag.
                    return magnitude.Value;

                case MagnitudeType.CustomCalculation:
                    // TODO: invoke the named CalculatorClass.
                    return magnitude.Value;

                default:
                    return magnitude.Value;
            }
        }

        public void AddToBaseValue(string attributeName, float delta)
        {
            var attr = FindAttribute(attributeName);
            if (attr != null) attr.BaseValue += delta;
        }

        public void SetBaseValue(string attributeName, float value)
        {
            var attr = FindAttribute(attributeName);
            if (attr != null) attr.BaseValue = value;
        }

        public void GrantTag(string tag) => OwnedTags.AddTag(new GameplayTag(tag));

        public void RemoveGrantedTag(string tag) => OwnedTags.RemoveTag(new GameplayTag(tag));

        /// <summary>
        /// Rebuilds the modifier list from all active (HasDuration / Infinite) effects and
        /// recomputes every attribute's current value through the §5 pipeline. Instant effects do
        /// not contribute here — they have already mutated base values.
        /// </summary>
        public void RecalculateAttributes()
        {
            var modifiers = BuildActiveModifiers();
            foreach (var set in _attributeSets.Values)
            {
                set.Recalculate(modifiers);
            }
        }

        private List<AttributeModifier> BuildActiveModifiers()
        {
            var result = new List<AttributeModifier>();
            foreach (var active in _effects.ActiveEffects)
            {
                int priority = active.Definition.Priority;
                foreach (var mod in active.Definition.Modifiers)
                {
                    float magnitude = ResolveMagnitude(mod.Magnitude, active.Level);
                    // Each active modifier stacks once per stack count.
                    for (int s = 0; s < active.Stacks; s++)
                    {
                        result.Add(new AttributeModifier(
                            mod.Attribute, mod.Operation, magnitude, mod.Channel, priority, _modifierSequence++));
                    }
                }
            }
            return result;
        }
    }
}
