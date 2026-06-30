using System.Collections.Generic;
using Jbltx.Ugas.Schema;

namespace Jbltx.Ugas.Effects
{
    /// <summary>
    /// The Gameplay Effects pillar (SPEC §9): applies effects to a gameplay controller, tracks
    /// active (HasDuration / Infinite) effects, and advances their durations and periodic
    /// executions over time. Instant effects modify base values and do not stay active.
    /// </summary>
    /// <remarks>
    /// The interface is complete; the default implementation
    /// (<see cref="GameplayEffectsSystem"/>) fully handles Instant application and active-effect
    /// bookkeeping, while the execution-policy scheduling
    /// (<see cref="ExecutionPolicy.RunInSequence"/> / <see cref="ExecutionPolicy.RunInMerge"/>) and
    /// magnitude resolution for non-literal magnitudes are stubbed pending full implementation.
    /// </remarks>
    public interface IGameplayEffectsSystem
    {
        /// <summary>Currently active (non-Instant) effects.</summary>
        IReadOnlyList<ActiveGameplayEffect> ActiveEffects { get; }

        /// <summary>
        /// Applies an effect to the owning GC. Instant effects mutate base values immediately and
        /// return null; HasDuration / Infinite effects are tracked and the live instance returned.
        /// </summary>
        ActiveGameplayEffect ApplyEffect(GameplayEffectDefinition effect, int level = 1, string instigatorGc = null, string sourceAbility = null);

        /// <summary>Removes an active effect by handle. Returns true if one was removed.</summary>
        bool RemoveEffect(string handle);

        /// <summary>
        /// Advances all active effects by <paramref name="deltaSeconds"/>: ticks periodic
        /// executions and expires HasDuration effects whose remaining duration reaches zero.
        /// </summary>
        void Tick(float deltaSeconds);
    }
}
