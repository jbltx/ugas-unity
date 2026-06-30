#if UGAS_DOTS
using Jbltx.Ugas.Dots;
using Jbltx.Ugas.Kernel;
using NUnit.Framework;
using Unity.Entities;

namespace Jbltx.Ugas.Tests.Dots
{
    /// <summary>
    /// DOTS runtime conformance (SPEC §5): the Burst-compiled <see cref="AttributeAggregationSystem"/>
    /// MUST compute the same CurrentValue as the shared <see cref="AttributeKernel"/> — and therefore
    /// the managed <c>UgasController</c> path — for the same inputs. Compiled only when
    /// <c>com.unity.entities</c> is installed (the asmdef has
    /// <c>defineConstraints: ["UNITY_INCLUDE_TESTS", "UGAS_DOTS"]</c>).
    /// </summary>
    [TestFixture]
    public class AttributeAggregationSystemTests
    {
        private World _world;
        private SystemHandle _system;

        [SetUp]
        public void SetUp()
        {
            _world = new World("UGAS DOTS Test World");
            _system = _world.GetOrCreateSystem<AttributeAggregationSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_world is { IsCreated: true }) _world.Dispose();
        }

        // Builds one attribute entity, ticks the system once, and returns the recomputed CurrentValue.
        private float Aggregate(float baseValue, ModifierSample[] modifiers,
            bool hasMin = false, float min = 0f, bool hasMax = false, float max = 0f)
        {
            var entity = UgasDotsConversion.CreateAttributeEntity(
                _world.EntityManager, attributeId: 0, baseValue, modifiers, hasMin, min, hasMax, max);

            // Manually tick this single unmanaged system. (If this API differs in your Entities
            // version, the equivalent is: add it to a SimulationSystemGroup and Update the group.)
            _system.Update(_world.Unmanaged);
            _world.EntityManager.CompleteAllTrackedJobs();

            return _world.EntityManager.GetComponentData<AttributeValue>(entity).CurrentValue;
        }

        [Test]
        public void WorkedExample_WeaponDamageIs18()
        {
            // SPEC §16.3: base 10 × MainStat(1+0.50) × DamageBonuses(1+0.20) = 18.
            // Two Multiply modifiers in DIFFERENT channels (ids 0 and 1) → the channels multiply.
            float result = Aggregate(10f, new[]
            {
                new ModifierSample(ModifierOp.Multiply, 0.50f, channelId: 0),
                new ModifierSample(ModifierOp.Multiply, 0.20f, channelId: 1),
            });
            Assert.That(result, Is.EqualTo(18f).Within(1e-4f));
        }

        [Test]
        public void SameChannelSums_DifferentChannelsMultiply()
        {
            // Same channel: +20% and +20% SUM to ×1.40; another channel +30% multiplies → ×1.82.
            float result = Aggregate(100f, new[]
            {
                new ModifierSample(ModifierOp.Multiply, 0.20f, channelId: 0),
                new ModifierSample(ModifierOp.Multiply, 0.20f, channelId: 0),
                new ModifierSample(ModifierOp.Multiply, 0.30f, channelId: 1),
            });
            Assert.That(result, Is.EqualTo(182f).Within(1e-3f));
        }

        [Test]
        public void FlatAdd_ThenClampToMax()
        {
            // (50 + 80) = 130, clamped to Max 100.
            float result = Aggregate(50f, new[] { new ModifierSample(ModifierOp.Add, 80f) },
                hasMax: true, max: 100f);
            Assert.That(result, Is.EqualTo(100f).Within(1e-4f));
        }

        [Test]
        public void DotsMatchesSharedKernelExactly()
        {
            // The DOTS path and the managed kernel must agree on identical inputs
            // (flat Add, two Multiply channels, and a post-multiply AddPost).
            var mods = new[]
            {
                new ModifierSample(ModifierOp.Add, 5f),
                new ModifierSample(ModifierOp.Multiply, 0.25f, channelId: 0),
                new ModifierSample(ModifierOp.Multiply, 0.10f, channelId: 1),
                new ModifierSample(ModifierOp.AddPost, 3f),
            };
            float expected = AttributeKernel.Aggregate(20f, mods, new float[mods.Length]);
            float dots = Aggregate(20f, mods);
            Assert.That(dots, Is.EqualTo(expected).Within(1e-4f));
        }
    }
}
#endif
