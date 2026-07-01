using System.Collections.Generic;
using Jbltx.Ugas.Definitions;

namespace Jbltx.Ugas.Persistence
{
    /// <summary>
    /// A snapshot of a controller's persistable state (SPEC §14.2): attribute base values, active
    /// (non-Instant) effects, and directly-granted abilities. Current values and owned tags are
    /// <i>derived</i> — recomputed on restore from the base values + re-applied effects — and are never
    /// the authoritative snapshot state (§14.2).
    /// </summary>
    /// <remarks>
    /// This reference snapshot holds live <see cref="GameplayEffectDefinition"/> /
    /// <see cref="GameplayAbilityDefinition"/> references for in-memory round-trips (save/load within a
    /// session, reconnection, replay). Disk serialization — writing effect/ability <i>class names</i>
    /// and resolving them through registries on load — is a follow-up that reuses
    /// <c>UgasController.ResolveEffect</c>.
    /// </remarks>
    public sealed class GCSnapshot
    {
        /// <summary>Monotonic snapshot version for forward-compatibility checks (§14.2).</summary>
        public int Version = 1;

        /// <summary>The snapshotted controller's identity.</summary>
        public string OwnerActorId;

        /// <summary>Game/wall-clock time at capture; the reference point for offline advancement (§14.5).</summary>
        public double CaptureTimestamp;

        public readonly List<AttributeState> Attributes = new List<AttributeState>();
        public readonly List<ActiveEffectRecord> ActiveEffects = new List<ActiveEffectRecord>();
        public readonly List<AbilityGrant> GrantedAbilities = new List<AbilityGrant>();
    }

    /// <summary>A restored attribute's authoritative base value (§14.2).</summary>
    public struct AttributeState
    {
        public string Set;
        public string Name;
        public float BaseValue;
    }

    /// <summary>
    /// A non-Instant active effect's resumable state (SPEC §14.3). Instant effects leave no active
    /// state — they are captured implicitly through the attribute base values.
    /// </summary>
    public struct ActiveEffectRecord
    {
        public GameplayEffectDefinition Effect;
        public int Level;
        public bool HasDuration;

        /// <summary>Seconds of effect time remaining at capture (§14.3.1); the timer resumes from here.</summary>
        public float RemainingDuration;

        /// <summary>Seconds since the last periodic execution (§14.3.3); the period resumes from here.</summary>
        public float PeriodElapsed;

        /// <summary>Total periodic executions fired so far (§14.3.3).</summary>
        public int ExecutionCount;

        /// <summary>Merged/stacked application count (§14.3.4).</summary>
        public int Stacks;
    }

    /// <summary>A granted ability not sourced from an active effect (§14.2).</summary>
    public struct AbilityGrant
    {
        public GameplayAbilityDefinition Ability;
        public int Level;
    }
}
