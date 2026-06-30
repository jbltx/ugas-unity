using Jbltx.Ugas.Definitions;

namespace Jbltx.Ugas.Runtime
{
    /// <summary>
    /// A live instance of a non-Instant <see cref="GameplayEffectDefinition"/> applied to a
    /// controller (SPEC §9, §14). These records are pooled by <see cref="GameplayEffectsSystem"/>
    /// (reset and reused) to avoid per-application GC allocations on the hot path.
    /// </summary>
    public sealed class ActiveGameplayEffect
    {
        public int Handle;
        public GameplayEffectDefinition Definition;
        public int Level = 1;

        /// <summary>Seconds remaining (HasDuration); negative means Infinite/none.</summary>
        public float RemainingDuration;
        public bool HasDuration;

        public int Stacks = 1;
        public float PeriodElapsed;
        public int ExecutionCount;
        public int InstigatorId = -1;

        public bool IsInfinite => Definition != null && Definition.DurationPolicy == DurationPolicy.Infinite;
        public bool IsPeriodic => Definition != null && Definition.IsPeriodic;

        /// <summary>Resets the record for reuse from the pool.</summary>
        public void Reset()
        {
            Handle = 0;
            Definition = null;
            Level = 1;
            RemainingDuration = -1f;
            HasDuration = false;
            Stacks = 1;
            PeriodElapsed = 0f;
            ExecutionCount = 0;
            InstigatorId = -1;
        }
    }
}
