using System;
using System.Collections.Generic;

namespace Jbltx.Ugas.Schema
{
    /// <summary>How long a gameplay effect lasts (SPEC §9). Mirrors the schema <c>DurationPolicy</c> enum.</summary>
    public enum DurationPolicy
    {
        /// <summary>Modifies BaseValue immediately and permanently; does not stay active.</summary>
        Instant,

        /// <summary>Modifies CurrentValue for a finite <c>Duration</c>, then reverts.</summary>
        HasDuration,

        /// <summary>Modifies CurrentValue until explicitly removed.</summary>
        Infinite
    }

    /// <summary>
    /// How concurrent applications of the same effect combine (SPEC §9).
    /// Mirrors the schema <c>ExecutionPolicy</c> enum.
    /// </summary>
    public enum ExecutionPolicy
    {
        /// <summary>All instances execute simultaneously; magnitude stacks N times. Schema default.</summary>
        RunInParallel,

        /// <summary>Instances queue and execute one after another.</summary>
        RunInSequence,

        /// <summary>Applications merge into a single instance (earliest start to latest end).</summary>
        RunInMerge
    }

    /// <summary>How a modifier value is computed (SPEC §9). Mirrors the schema <c>MagnitudeDefinition.Type</c>.</summary>
    public enum MagnitudeType
    {
        /// <summary>A static value (optionally curve-scaled by level).</summary>
        ScalableFloat,

        /// <summary>Derived from a backing attribute on the source or target.</summary>
        AttributeBased,

        /// <summary>Computed by a custom calculation class.</summary>
        CustomCalculation,

        /// <summary>Supplied at runtime, keyed by a data tag.</summary>
        SetByCaller
    }

    /// <summary>Which gameplay controller a backing attribute is read from.</summary>
    public enum MagnitudeSource
    {
        Source,
        Target
    }

    /// <summary>
    /// The arithmetic operation a modifier applies to its target attribute (SPEC §9).
    /// Pipeline-step ordering is documented per value and implemented by the Attributes pillar.
    /// </summary>
    public enum ModifierOperation
    {
        /// <summary>Pre-multiply flat additive (pipeline step 2).</summary>
        Add,

        /// <summary>Post-multiply flat additive (pipeline step 7; rare).</summary>
        AddPost,

        /// <summary>Multiplicative bonus aggregated per channel (pipeline step 6). Signed: +0.25 = +25%.</summary>
        Multiply,

        /// <summary>Replaces the computed result (pipeline step 8).</summary>
        Override
    }

    /// <summary>
    /// A magnitude definition: how a single numeric value (a duration or a modifier amount) is
    /// resolved. Mirrors <c>$defs/MagnitudeDefinition</c> of <c>schemas/gameplay_effect.yaml</c>.
    /// </summary>
    [Serializable]
    public sealed class MagnitudeDefinition
    {
        public MagnitudeType Type = MagnitudeType.ScalableFloat;

        /// <summary>Static value for <see cref="MagnitudeType.ScalableFloat"/>.</summary>
        public float Value;

        /// <summary>Curve table reference (ScalableFloat scaled by level).</summary>
        public string Curve;

        /// <summary>Input parameter for curve evaluation.</summary>
        public string CurveInput;

        /// <summary>Attribute to read for <see cref="MagnitudeType.AttributeBased"/>.</summary>
        public string BackingAttribute;

        /// <summary>Which GC the backing attribute is read from.</summary>
        public MagnitudeSource Source = MagnitudeSource.Source;

        /// <summary>Multiplicative coefficient applied to the backing value.</summary>
        public float Coefficient = 1f;

        /// <summary>Value added before multiplication.</summary>
        public float PreMultiplyAdditive;

        /// <summary>Value added after multiplication.</summary>
        public float PostMultiplyAdditive;

        /// <summary>Custom calculation class for <see cref="MagnitudeType.CustomCalculation"/>.</summary>
        public string CalculatorClass;

        /// <summary>Data tag for <see cref="MagnitudeType.SetByCaller"/> lookup.</summary>
        public string DataTag;
    }

    /// <summary>
    /// A single modifier within an effect. Mirrors <c>$defs/Modifier</c>.
    /// </summary>
    [Serializable]
    public sealed class ModifierDefinition
    {
        /// <summary>Target attribute name. Required.</summary>
        public string Attribute;

        /// <summary>Operation to apply. Required.</summary>
        public ModifierOperation Operation;

        /// <summary>How the modifier amount is computed. Required.</summary>
        public MagnitudeDefinition Magnitude;

        /// <summary>
        /// Optional named aggregation channel. Modifiers in the same channel sum together;
        /// modifiers in different channels multiply against each other (SPEC §16.3).
        /// </summary>
        public string Channel;
    }

    /// <summary>Periodic execution settings for a durational effect.</summary>
    [Serializable]
    public sealed class PeriodDefinition
    {
        /// <summary>Time interval (seconds) between periodic executions.</summary>
        public float Period;

        /// <summary>Whether to execute immediately on application.</summary>
        public bool ExecuteOnApplication;
    }

    /// <summary>An ability granted while an effect is active.</summary>
    [Serializable]
    public sealed class GrantedAbilityDefinition
    {
        public string AbilityClass;
        public int Level = 1;
        public string InputID;
        public bool RemoveOnEffectRemoval = true;
    }

    /// <summary>A custom execution (calculation class) attached to an effect.</summary>
    [Serializable]
    public sealed class ExecutionDefinition
    {
        public string CalculatorClass;
    }

    /// <summary>Display metadata for an effect.</summary>
    [Serializable]
    public sealed class GameplayEffectMetadata
    {
        public string DisplayName;
        public string Description;
        public string Icon;
    }

    /// <summary>
    /// Engine-agnostic gameplay-effect definition. One-to-one mapping of
    /// <c>schemas/gameplay_effect.yaml</c> (SPEC §9).
    /// </summary>
    [Serializable]
    public sealed class GameplayEffectDefinition
    {
        /// <summary>Unique effect identifier. Required.</summary>
        public string Name;

        /// <summary>Duration policy. Required.</summary>
        public DurationPolicy DurationPolicy;

        /// <summary>Duration magnitude (for <see cref="DurationPolicy.HasDuration"/>).</summary>
        public MagnitudeDefinition Duration;

        /// <summary>Optional periodic execution settings.</summary>
        public PeriodDefinition Period;

        /// <summary>How concurrent applications combine. Defaults to RunInParallel.</summary>
        public ExecutionPolicy ExecutionPolicy = ExecutionPolicy.RunInParallel;

        /// <summary>Override conflict-resolution priority (highest wins; LIFO on tie).</summary>
        public int Priority;

        /// <summary>Attribute modifiers.</summary>
        public List<ModifierDefinition> Modifiers = new List<ModifierDefinition>();

        /// <summary>Custom executions.</summary>
        public List<ExecutionDefinition> Executions = new List<ExecutionDefinition>();

        /// <summary>Tags granted while this effect is active.</summary>
        public List<string> GrantedTags = new List<string>();

        /// <summary>Tags required on the target for the effect to apply.</summary>
        public List<string> ApplicationRequiredTags = new List<string>();

        /// <summary>Abilities granted while this effect is active.</summary>
        public List<GrantedAbilityDefinition> GrantedAbilities = new List<GrantedAbilityDefinition>();

        /// <summary>Visual/audio cues to trigger.</summary>
        public List<string> GameplayCues = new List<string>();
    }
}
