namespace Jbltx.Ugas.Runtime
{
    /// <summary>
    /// A Modifier Magnitude Calculator (MMC — SPEC §9.4.2 <c>CustomCalculation</c>): computes a single
    /// modifier magnitude from source/target attributes, the effect level, and SetByCaller data. Named by
    /// a magnitude's <c>CalculatorClass</c> and registered on a controller via
    /// <see cref="UgasController.RegisterMagnitudeCalculation"/>.
    /// </summary>
    /// <remarks>
    /// Distinct from an <see cref="IExecutionCalculation"/> (§9.6): an MMC is <b>read-only</b> — it returns a
    /// value and applies nothing — whereas an ExecCalc writes results to the target. An MMC MUST be
    /// deterministic given its inputs: it is evaluated on <i>every</i> attribute recompute (§5.3), so it must
    /// not consume randomness or observe external mutable state (contrast the ExecCalc, which runs once per
    /// execution and carries a seeded <c>Rng</c>).
    /// </remarks>
    public interface IMagnitudeCalculation
    {
        /// <summary>Returns the modifier magnitude for the given context. Pure — no side effects.</summary>
        float Calculate(MagnitudeCalculationContext context);
    }

    /// <summary>Read-only inputs an <see cref="IMagnitudeCalculation"/> reads (SPEC §9.4.2). No write surface.</summary>
    public sealed class MagnitudeCalculationContext
    {
        /// <summary>The effect's source/instigator (may be null for a self-applied effect).</summary>
        public IUgasRuntime Source;

        /// <summary>The controller the effect is resolving against.</summary>
        public IUgasRuntime Target;

        /// <summary>The effect level.</summary>
        public int Level = 1;

        /// <summary>Per-application SetByCaller magnitudes (§9.4.2), keyed by DataTag; null = none.</summary>
        public System.Collections.Generic.IReadOnlyDictionary<string, float> SetByCaller;

        /// <summary>Current value of a source attribute (0 if no source / absent).</summary>
        public float SourceAttribute(string name) => Source != null ? Source.GetCurrentValue(name) : 0f;

        /// <summary>Current value of a target attribute (0 if absent).</summary>
        public float TargetAttribute(string name) => Target != null ? Target.GetCurrentValue(name) : 0f;

        /// <summary>Reads a SetByCaller value by <paramref name="tag"/>, or <paramref name="fallback"/> if not supplied.</summary>
        public float GetSetByCaller(string tag, float fallback = 0f) =>
            SetByCaller != null && tag != null && SetByCaller.TryGetValue(tag, out var v) ? v : fallback;
    }
}
