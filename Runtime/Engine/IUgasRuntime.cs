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

        /// <summary>Resolves a magnitude definition to a concrete value for this controller.</summary>
        float ResolveMagnitude(in MagnitudeDefinition magnitude, int level);

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
