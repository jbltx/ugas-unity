using Jbltx.Ugas.Prediction;

namespace Jbltx.Ugas.Runtime
{
    /// <summary>
    /// The stateful/branching math seam for effects (SPEC §9.6): an <c>ExecCalc_*</c> that reads source
    /// and target attributes and applies a computed result, for logic a static modifier can't express —
    /// damage mitigation, conditional bonuses, random rolls. Registered by name on a controller and
    /// invoked when an effect that names it executes.
    /// </summary>
    public interface IExecutionCalculation
    {
        /// <summary>Runs the calculation against <paramref name="context"/>, applying its result to the target.</summary>
        void Execute(ExecutionContext context);
    }

    /// <summary>
    /// The inputs an <see cref="IExecutionCalculation"/> reads and the surface it writes through (SPEC
    /// §9.6/§9.9). Carries the source (instigator) and target runtimes, the effect level, and the
    /// deterministic <see cref="Rng"/> (§9/§13.8.1) — draws advance its monotonic index within this
    /// execution, so a predicted calc and its server replay agree.
    /// </summary>
    public sealed class ExecutionContext
    {
        /// <summary>The effect's source/instigator (may be null for a self-applied effect).</summary>
        public IUgasRuntime Source;

        /// <summary>The controller the effect is applied to.</summary>
        public IUgasRuntime Target;

        /// <summary>The effect level.</summary>
        public int Level = 1;

        /// <summary>The deterministic random stream for this execution (§9 context.RNG).</summary>
        public UgasRandom Rng;

        /// <summary>Per-application SetByCaller magnitudes (§9.4.2), keyed by DataTag; null = none supplied.</summary>
        public System.Collections.Generic.IReadOnlyDictionary<string, float> SetByCaller;

        /// <summary>Reads a SetByCaller value by <paramref name="tag"/>, or <paramref name="fallback"/> if not supplied.</summary>
        public float GetSetByCaller(string tag, float fallback = 0f) =>
            SetByCaller != null && tag != null && SetByCaller.TryGetValue(tag, out var v) ? v : fallback;

        /// <summary>Current value of a source attribute (0 if no source / absent).</summary>
        public float SourceAttribute(string name) => Source != null ? Source.GetCurrentValue(name) : 0f;

        /// <summary>Current value of a target attribute (0 if absent).</summary>
        public float TargetAttribute(string name) => Target != null ? Target.GetCurrentValue(name) : 0f;

        /// <summary>Adds <paramref name="delta"/> to a target attribute's base value (the calc's output).</summary>
        public void AddToTarget(string attribute, float delta) => Target?.AddToBaseValue(attribute, delta);

        /// <summary>Overrides a target attribute's base value.</summary>
        public void SetTarget(string attribute, float value) => Target?.SetBaseValue(attribute, value);
    }
}
