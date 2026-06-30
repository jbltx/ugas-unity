using System;
using System.Collections.Generic;

namespace Jbltx.Ugas.Schema
{
    /// <summary>
    /// The tag sets that govern an ability's activation, blocking, and cancellation behaviour
    /// (SPEC §8). Mirrors the <c>Tags</c> object of <c>schemas/gameplay_ability.yaml</c>.
    /// </summary>
    [Serializable]
    public sealed class AbilityTagSet
    {
        /// <summary>Tags that describe this ability.</summary>
        public List<string> AbilityTags = new List<string>();

        /// <summary>Tags that prevent this ability from running.</summary>
        public List<string> BlockedByTags = new List<string>();

        /// <summary>While active, block abilities that have these tags.</summary>
        public List<string> BlockAbilitiesWithTags = new List<string>();

        /// <summary>On activation, cancel abilities that have these tags.</summary>
        public List<string> CancelAbilitiesWithTags = new List<string>();

        /// <summary>Tags required on the GC to activate this ability (HasAll).</summary>
        public List<string> ActivationRequiredTags = new List<string>();

        /// <summary>Tags that block activation of this ability (HasAny negated).</summary>
        public List<string> ActivationBlockedTags = new List<string>();

        /// <summary>Tags granted to the GC while this ability is active (via auto-generated Infinite effect).</summary>
        public List<string> ActivationOwnedTags = new List<string>();
    }

    /// <summary>A single ability task entry (SPEC §10). Mirrors one item of the <c>Tasks</c> array.</summary>
    [Serializable]
    public sealed class AbilityTaskDefinition
    {
        /// <summary>Task type identifier (e.g. <c>PlayMontage</c>, <c>WaitGameplayEvent</c>). Required.</summary>
        public string Type;

        /// <summary>Task-specific parameters, kept as a loose key/value map.</summary>
        public Dictionary<string, object> Params = new Dictionary<string, object>();

        /// <summary>Seconds between task ticks; 0 means every frame. Only meaningful for ticking tasks.</summary>
        public float TickInterval;

        /// <summary>Tick scheduling priority when the per-frame tick budget is exhausted; higher ticks first.</summary>
        public int Priority;
    }

    /// <summary>Display metadata for an ability.</summary>
    [Serializable]
    public sealed class GameplayAbilityMetadata
    {
        public string DisplayName;
        public string Description;
        public string Icon;
    }

    /// <summary>
    /// Engine-agnostic gameplay-ability definition. One-to-one mapping of
    /// <c>schemas/gameplay_ability.yaml</c> (SPEC §8).
    /// </summary>
    [Serializable]
    public sealed class GameplayAbilityDefinition
    {
        /// <summary>Unique identifier for this ability. Required.</summary>
        public string Name;

        /// <summary>Activation/blocking/cancellation tag sets.</summary>
        public AbilityTagSet Tags = new AbilityTagSet();

        /// <summary>Reference to the cost <see cref="GameplayEffectDefinition"/> (by name).</summary>
        public string Cost;

        /// <summary>Reference to the cooldown <see cref="GameplayEffectDefinition"/> (by name).</summary>
        public string Cooldown;

        /// <summary>Ordered ability tasks.</summary>
        public List<AbilityTaskDefinition> Tasks = new List<AbilityTaskDefinition>();

        /// <summary>Optional display metadata.</summary>
        public GameplayAbilityMetadata Metadata;
    }
}
