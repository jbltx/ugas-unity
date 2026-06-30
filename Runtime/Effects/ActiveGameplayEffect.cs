using System;
using Jbltx.Ugas.Schema;

namespace Jbltx.Ugas.Effects
{
    /// <summary>
    /// A live instance of a non-Instant <see cref="GameplayEffectDefinition"/> applied to a
    /// gameplay controller (SPEC §9, §14). Carries the runtime bookkeeping the spec requires to be
    /// serialized: handle, remaining duration, periodic state, stack count, and provenance.
    /// </summary>
    public sealed class ActiveGameplayEffect
    {
        /// <summary>Unique handle, stable across save/load.</summary>
        public string Handle { get; }

        /// <summary>The originating effect definition.</summary>
        public GameplayEffectDefinition Definition { get; }

        /// <summary>Effect level at application time.</summary>
        public int Level { get; set; } = 1;

        /// <summary>Seconds of effect time remaining (HasDuration only); null for Infinite.</summary>
        public float? RemainingDuration { get; set; }

        /// <summary>Stack / instance count.</summary>
        public int Stacks { get; set; } = 1;

        /// <summary>Seconds elapsed since the last periodic execution (periodic effects only).</summary>
        public float PeriodElapsed { get; set; }

        /// <summary>Total periodic executions fired so far.</summary>
        public int ExecutionCount { get; set; }

        /// <summary>GC that applied this effect (provenance, SPEC §14).</summary>
        public string InstigatorGC { get; set; }

        /// <summary>Ability that applied this effect, if any.</summary>
        public string SourceAbility { get; set; }

        public ActiveGameplayEffect(string handle, GameplayEffectDefinition definition)
        {
            Handle = handle ?? Guid.NewGuid().ToString("N");
            Definition = definition;
        }

        /// <summary>True if this is an Infinite effect (never auto-expires).</summary>
        public bool IsInfinite => Definition.DurationPolicy == DurationPolicy.Infinite;

        /// <summary>True if this effect has periodic execution configured.</summary>
        public bool IsPeriodic => Definition.Period != null && Definition.Period.Period > 0f;
    }
}
