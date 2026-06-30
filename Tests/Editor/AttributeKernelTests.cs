using System;
using Jbltx.Ugas.Kernel;
using NUnit.Framework;

namespace Jbltx.Ugas.Tests.Editor
{
    /// <summary>
    /// Conformance for the shared SPEC §5 aggregation kernel — the math both the managed runtime and
    /// the DOTS jobs call. Includes the worked Barbarian example (WeaponDamage = 10 × 1.50 × 1.20 = 18).
    /// </summary>
    [TestFixture]
    public class AttributeKernelTests
    {
        private static float Agg(float baseValue, ModifierSample[] mods, int channels,
            bool hasMin = false, float min = 0, bool hasMax = false, float max = 0)
        {
            Span<float> scratch = channels > 0 ? new float[channels] : Span<float>.Empty;
            return AttributeKernel.Aggregate(baseValue, mods, scratch, hasMin, min, hasMax, max);
        }

        [Test]
        public void WorkedExample_WeaponDamageIs18()
        {
            var mods = new[]
            {
                new ModifierSample(ModifierOp.Multiply, 0.50f, 0), // MainStat: 0.01*50
                new ModifierSample(ModifierOp.Multiply, 0.20f, 1), // DamageBonuses: +20%
            };
            Assert.That(Agg(10f, mods, 2), Is.EqualTo(18f).Within(1e-4f));
        }

        [Test]
        public void SameChannelMultipliesSum()
        {
            var mods = new[]
            {
                new ModifierSample(ModifierOp.Multiply, 0.20f, 0),
                new ModifierSample(ModifierOp.Multiply, 0.20f, 0),
            };
            Assert.That(Agg(100f, mods, 1), Is.EqualTo(140f).Within(1e-4f));
        }

        [Test]
        public void DifferentChannelsMultiply()
        {
            var mods = new[]
            {
                new ModifierSample(ModifierOp.Multiply, 0.40f, 0),
                new ModifierSample(ModifierOp.Multiply, 0.30f, 1),
            };
            Assert.That(Agg(100f, mods, 2), Is.EqualTo(182f).Within(1e-4f));
        }

        [Test]
        public void UnchanneledMultipliesAreIsolatedSingletons()
        {
            var mods = new[]
            {
                new ModifierSample(ModifierOp.Multiply, 0.20f, ModifierSample.NoChannel),
                new ModifierSample(ModifierOp.Multiply, 0.20f, ModifierSample.NoChannel),
            };
            Assert.That(Agg(100f, mods, 0), Is.EqualTo(144f).Within(1e-4f));
        }

        [Test]
        public void PipelineOrder_AddThenMultiplyThenAddPost()
        {
            var mods = new[]
            {
                new ModifierSample(ModifierOp.Add, 5f),
                new ModifierSample(ModifierOp.Multiply, 1.0f, 0),
                new ModifierSample(ModifierOp.AddPost, 3f),
            };
            Assert.That(Agg(10f, mods, 1), Is.EqualTo(33f).Within(1e-4f));
        }

        [Test]
        public void Override_HighestPriorityWins()
        {
            var mods = new[]
            {
                new ModifierSample(ModifierOp.Override, 10f, ModifierSample.NoChannel, 0, 1),
                new ModifierSample(ModifierOp.Override, 99f, ModifierSample.NoChannel, 100, 2),
                new ModifierSample(ModifierOp.Override, 5f, ModifierSample.NoChannel, -10, 3),
            };
            Assert.That(Agg(100f, mods, 0), Is.EqualTo(99f).Within(1e-4f));
        }

        [Test]
        public void Override_LastAppliedWinsOnTie()
        {
            var mods = new[]
            {
                new ModifierSample(ModifierOp.Override, 10f, ModifierSample.NoChannel, 5, 1),
                new ModifierSample(ModifierOp.Override, 20f, ModifierSample.NoChannel, 5, 2),
            };
            Assert.That(Agg(100f, mods, 0), Is.EqualTo(20f).Within(1e-4f));
        }

        [Test]
        public void ClampAppliedLast()
        {
            var mods = new[] { new ModifierSample(ModifierOp.Add, 1000f) };
            Assert.That(Agg(100f, mods, 0, hasMin: true, min: 0f, hasMax: true, max: 150f), Is.EqualTo(150f));
        }
    }
}
