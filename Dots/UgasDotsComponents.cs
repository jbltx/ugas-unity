#if UGAS_DOTS
using Jbltx.Ugas.Kernel;
using Unity.Entities;

namespace Jbltx.Ugas.Dots
{
    /// <summary>
    /// ECS component holding one attribute's dual values (SPEC §5). Lives on an attribute entity in
    /// the DOTS-accelerated backend. <see cref="BaseValue"/> is authoritative; <see cref="CurrentValue"/>
    /// is recomputed each evaluation by <see cref="AttributeAggregationSystem"/> via the shared
    /// <see cref="AttributeKernel"/>.
    /// </summary>
    public struct AttributeValue : IComponentData
    {
        /// <summary>Stable attribute id (interned, matches the managed runtime's naming table).</summary>
        public int AttributeId;

        public float BaseValue;
        public float CurrentValue;

        public bool HasMin;
        public float Min;
        public bool HasMax;
        public float Max;
    }

    /// <summary>
    /// A resolved modifier targeting an attribute, stored as a dynamic-buffer element on the
    /// attribute entity. This is the DOTS-side mirror of <see cref="ModifierSample"/> — the Burst job
    /// repacks these into a span and calls the shared kernel, so the math is identical to the managed
    /// path.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct AttributeModifierElement : IBufferElementData
    {
        public ModifierSample Sample;
    }
}
#endif
