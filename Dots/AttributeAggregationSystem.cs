#if UGAS_DOTS
using Jbltx.Ugas.Kernel;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace Jbltx.Ugas.Dots
{
    /// <summary>
    /// DOTS-accelerated, Burst-compiled batched evaluation of the SPEC §5 aggregation pipeline. For
    /// every attribute entity it repacks its modifier buffer and calls the <i>same</i>
    /// <see cref="AttributeKernel"/> the managed <c>UgasController</c> uses, so results are identical
    /// — this path is just faster for large numbers of attributes/effects.
    /// </summary>
    /// <remarks>
    /// This whole assembly is compiled only when <c>com.unity.entities</c> is installed (the asmdef
    /// has <c>defineConstraints: ["UGAS_DOTS"]</c>, satisfied by its own versionDefine). When
    /// Entities is absent the package falls back to the managed path with no compile errors.
    /// </remarks>
    [BurstCompile]
    public partial struct AttributeAggregationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state) { }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Thread the parallel job through state.Dependency so reads of AttributeValue (and the next
            // system update) correctly wait on it; dropping the handle would let consumers race the job.
            state.Dependency = new AggregateJob().ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        public partial struct AggregateJob : IJobEntity
        {
            private void Execute(ref AttributeValue attr, in DynamicBuffer<AttributeModifierElement> mods)
            {
                // Repack the buffer into a span of ModifierSample for the shared kernel.
                var samples = new NativeArray<ModifierSample>(mods.Length, Allocator.Temp);
                for (int i = 0; i < mods.Length; i++) samples[i] = mods[i].Sample;

                // Channel scratch: sized to the modifier count is a safe upper bound on distinct
                // channels for this attribute (channel ids are dense per controller).
                var scratch = new NativeArray<float>(mods.Length, Allocator.Temp);

                attr.CurrentValue = AttributeKernel.Aggregate(
                    attr.BaseValue,
                    samples.AsReadOnlySpan(),
                    scratch.AsSpan(),
                    attr.HasMin, attr.Min,
                    attr.HasMax, attr.Max);

                samples.Dispose();
                scratch.Dispose();
            }
        }
    }
}
#endif
