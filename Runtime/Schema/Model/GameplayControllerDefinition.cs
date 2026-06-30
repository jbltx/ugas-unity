using System;
using System.Collections.Generic;

namespace Jbltx.Ugas.Schema
{
    /// <summary>An actor reference (owner or avatar) on a gameplay controller (SPEC §4).</summary>
    [Serializable]
    public sealed class ActorReference
    {
        public string ActorID;
        public string ActorType;
    }

    /// <summary>A single attribute's persisted Base/Current pair inside a serialized GC.</summary>
    [Serializable]
    public sealed class SerializedAttribute
    {
        public string Name;
        public float BaseValue;
        public float CurrentValue;
    }

    /// <summary>A serialized attribute set on a GC snapshot.</summary>
    [Serializable]
    public sealed class SerializedAttributeSet
    {
        public string Name;
        public List<SerializedAttribute> Attributes = new List<SerializedAttribute>();
    }

    /// <summary>A granted-ability grant on a GC (SPEC §4).</summary>
    [Serializable]
    public sealed class GrantedAbilityRecord
    {
        public string AbilityClass;
        public int Level = 1;
        public string InputID;
        public string Handle;
        public bool IsActive;
    }

    /// <summary>Periodic execution state for an active effect record (SPEC §14.3.3).</summary>
    [Serializable]
    public sealed class PeriodicStateRecord
    {
        public float PeriodElapsed;
        public int ExecutionCount;
    }

    /// <summary>
    /// One active (non-Instant) effect on a GC (SPEC §14). The schema exposes both a
    /// <c>RemainingDuration</c> field and the genre packs sometimes use a <c>Duration: -1</c>
    /// shorthand for Infinite; the loader tolerates both.
    /// </summary>
    [Serializable]
    public sealed class ActiveEffectRecord
    {
        public string Handle;
        public string EffectClass;
        public DurationPolicy? DurationPolicy;
        public float? RemainingDuration;
        public int Stacks = 1;
        public float StartTime;
        public int Level = 1;
        public string InstigatorGC;
        public string SourceAbility;
        public PeriodicStateRecord PeriodicState;
        public Dictionary<string, float> CapturedAttributes = new Dictionary<string, float>();
        public Dictionary<string, float> SetByCallerMagnitudes = new Dictionary<string, float>();
    }

    /// <summary>GC replication strategy (SPEC §4).</summary>
    public enum GCReplicationMode
    {
        Minimal,
        Mixed,
        Full,
        None
    }

    /// <summary>Display/debug metadata for a GC.</summary>
    [Serializable]
    public sealed class GameplayControllerMetadata
    {
        public string DisplayName;
        public string Description;
        public List<string> Tags = new List<string>();
        public string DebugCategory;
    }

    /// <summary>
    /// Engine-agnostic gameplay-controller definition / snapshot. One-to-one mapping of
    /// <c>schemas/gameplay_controller.yaml</c> (SPEC §4, §14). This is the authoritative
    /// serializable container; the runtime <see cref="Jbltx.Ugas.IGameplayController"/> is
    /// hydrated from it.
    /// </summary>
    [Serializable]
    public sealed class GameplayControllerDefinition
    {
        /// <summary>Logical owner (lifecycle, authority, persistence). Required.</summary>
        public ActorReference OwnerActor;

        /// <summary>World spatial representation (optional; may equal the owner).</summary>
        public ActorReference AvatarActor;

        /// <summary>Registered attribute containers. Required, at least one.</summary>
        public List<SerializedAttributeSet> AttributeSets = new List<SerializedAttributeSet>();

        /// <summary>Abilities granted to this GC.</summary>
        public List<GrantedAbilityRecord> GrantedAbilities = new List<GrantedAbilityRecord>();

        /// <summary>Currently active effects (serialization protocol §14).</summary>
        public List<ActiveEffectRecord> ActiveEffects = new List<ActiveEffectRecord>();

        /// <summary>Currently active ActionSet names (derived from tag evaluation at runtime).</summary>
        public List<string> ActiveActionSets = new List<string>();

        /// <summary>Current semantic state tags (hierarchical dot notation).</summary>
        public List<string> OwnedTags = new List<string>();

        /// <summary>Replication strategy. Defaults to Mixed.</summary>
        public GCReplicationMode ReplicationMode = GCReplicationMode.Mixed;

        /// <summary>Whether this GC is currently active.</summary>
        public bool IsActive = true;

        /// <summary>Optional metadata.</summary>
        public GameplayControllerMetadata Metadata;
    }
}
