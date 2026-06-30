#if UGAS_DOTS
using System.Collections;
using Jbltx.Ugas.Dots;
using Jbltx.Ugas.Kernel;
using NUnit.Framework;
using Unity.Entities;
using UnityEngine.TestTools;

namespace Jbltx.Ugas.Tests.Dots.Play
{
    /// <summary>
    /// PlayMode DOTS conformance. The EditMode <c>AttributeAggregationSystemTests</c> create a
    /// throwaway <see cref="World"/> and tick the system by hand — they prove the math. This test
    /// instead verifies the system is actually wired into the player loop: it auto-runs in the
    /// DEFAULT world's <c>SimulationSystemGroup</c> over real frames, with no manual tick. Compiled
    /// only when <c>com.unity.entities</c> is installed (asmdef defineConstraints UGAS_DOTS).
    /// </summary>
    public class AttributeAggregationSystemPlayTests
    {
        [UnityTest]
        public IEnumerator System_AutoRuns_InDefaultWorld_OverRealFrames()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            Assert.That(world, Is.Not.Null, "the default DOTS world should exist in play mode");
            var em = world.EntityManager;

            // base 10 × MainStat(1+0.50) × DamageBonuses(1+0.20) = 18 (SPEC §16.3), via the ECS path —
            // but here recomputed by the auto-ticked system, not a manual Update().
            var entity = UgasDotsConversion.CreateAttributeEntity(em, attributeId: 0, baseValue: 10f,
                new[]
                {
                    new ModifierSample(ModifierOp.Multiply, 0.50f, channelId: 0),
                    new ModifierSample(ModifierOp.Multiply, 0.20f, channelId: 1),
                });

            // Let the player loop advance: the SimulationSystemGroup updates the system each frame.
            yield return null;
            yield return null;

            float current = em.GetComponentData<AttributeValue>(entity).CurrentValue;
            Assert.That(current, Is.EqualTo(18f).Within(1e-4f),
                "AttributeAggregationSystem should have recomputed CurrentValue via the player loop");

            em.DestroyEntity(entity); // the default world persists across tests — clean up.
        }
    }
}
#endif
