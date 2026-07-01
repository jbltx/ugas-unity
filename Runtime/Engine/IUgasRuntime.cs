using Jbltx.Ugas.Definitions;
using Jbltx.Ugas.Tags;

namespace Jbltx.Ugas.Runtime
{
    /// <summary>
    /// The runtime surface that the Effects and Abilities systems use to act on their owning
    /// controller, decoupling those systems from the concrete <see cref="UgasController"/>
    /// MonoBehaviour.
    /// </summary>
    public interface IUgasRuntime
    {
        /// <summary>The owned-tag container (interned handles).</summary>
        GameplayTagContainer OwnedTags { get; }

        /// <summary>Reads the named attribute's current (derived) value; 0 if absent.</summary>
        float GetCurrentValue(string attributeName);

        /// <summary>Applies a gameplay effect to this controller (e.g. an ability's cost/cooldown).</summary>
        ActiveGameplayEffect ApplyEffect(GameplayEffectDefinition effect, int level);

        /// <summary>
        /// Resolves a magnitude definition to a concrete value. AttributeBased magnitudes read this
        /// controller's attributes unless <paramref name="source"/> is given and the magnitude's
        /// <c>Source</c> is <c>Source</c> (§9.4.2), in which case they read the source/instigator.
        /// </summary>
        float ResolveMagnitude(in MagnitudeDefinition magnitude, int level, IUgasRuntime source = null);

        /// <summary>Adds <paramref name="delta"/> to the named attribute's base value (Instant effects).</summary>
        void AddToBaseValue(string attributeName, float delta);

        /// <summary>Overrides the named attribute's base value (Instant Override).</summary>
        void SetBaseValue(string attributeName, float value);

        /// <summary>Grants a tag (by name) for the lifetime of an active effect.</summary>
        void GrantTag(string tag);

        /// <summary>Removes a previously granted tag (by name).</summary>
        void RemoveGrantedTag(string tag);

        /// <summary>Recomputes derived (current) attribute values after a change.</summary>
        void RecalculateAttributes();
    }
}
