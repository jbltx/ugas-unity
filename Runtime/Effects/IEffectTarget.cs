using Jbltx.Ugas.Schema;

namespace Jbltx.Ugas.Effects
{
    /// <summary>
    /// The surface the effects system uses to act on its owning gameplay controller, decoupling the
    /// Effects pillar from the concrete GC type. The GC implements this to expose attribute mutation
    /// and tag granting to effect application.
    /// </summary>
    public interface IEffectTarget
    {
        /// <summary>Resolves a modifier's magnitude to a concrete number for this target.</summary>
        float ResolveMagnitude(MagnitudeDefinition magnitude, int level);

        /// <summary>Adds <paramref name="delta"/> to the named attribute's base value (Instant effects).</summary>
        void AddToBaseValue(string attributeName, float delta);

        /// <summary>Overrides the named attribute's base value (Instant Override).</summary>
        void SetBaseValue(string attributeName, float value);

        /// <summary>Grants a tag for the lifetime of an active effect.</summary>
        void GrantTag(string tag);

        /// <summary>Removes a previously granted tag.</summary>
        void RemoveGrantedTag(string tag);

        /// <summary>Recomputes derived (current) attribute values after a change.</summary>
        void RecalculateAttributes();
    }
}
