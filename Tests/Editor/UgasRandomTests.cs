using Jbltx.Ugas.Prediction;
using NUnit.Framework;

namespace Jbltx.Ugas.Tests.Editor
{
    /// <summary>
    /// Conformance for the deterministic predicted-RNG stream (SPEC §9 context.RNG / §13.8.1): identical
    /// <c>(seed, sub)</c> pairs reproduce the same sequence (so client and server agree and replay is
    /// stable), disjoint sub-streams diverge, and value helpers stay in range.
    /// </summary>
    [TestFixture]
    public class UgasRandomTests
    {
        [Test]
        public void SameSeedAndSub_ProduceIdenticalSequence()
        {
            var a = new UgasRandom(12345, 0);
            var b = new UgasRandom(12345, 0);
            for (int i = 0; i < 16; i++) Assert.That(a.NextUInt64(), Is.EqualTo(b.NextUInt64()));
        }

        [Test]
        public void DifferentSub_ProduceDifferentSequences()
        {
            var a = new UgasRandom(12345, 0);
            var b = new UgasRandom(12345, 1);
            bool anyDifferent = false;
            for (int i = 0; i < 8; i++) if (a.NextUInt64() != b.NextUInt64()) { anyDifferent = true; break; }
            Assert.That(anyDifferent, Is.True, "disjoint sub-streams must not coincide");
        }

        [Test]
        public void Replay_FromSameKey_YieldsSameDraws()
        {
            var first = new UgasRandom(999, 3);
            var drawn = new ulong[5];
            for (int i = 0; i < 5; i++) drawn[i] = first.NextUInt64();

            // Reconciliation replay (§13.5): a fresh stream from the same (seed, sub) reproduces the draws.
            var replay = new UgasRandom(999, 3);
            for (int i = 0; i < 5; i++) Assert.That(replay.NextUInt64(), Is.EqualTo(drawn[i]));
        }

        [Test]
        public void NextFloat_IsInUnitRange()
        {
            var r = new UgasRandom(7);
            for (int i = 0; i < 1000; i++)
            {
                float f = r.NextFloat();
                Assert.That(f, Is.GreaterThanOrEqualTo(0f).And.LessThan(1f));
            }
        }

        [Test]
        public void NextInt_IsInRange()
        {
            var r = new UgasRandom(42);
            for (int i = 0; i < 1000; i++) Assert.That(r.NextInt(5, 10), Is.InRange(5, 9));
        }

        [Test]
        public void DrawIndex_AdvancesPerDraw()
        {
            var r = new UgasRandom(1);
            r.NextUInt64();
            r.NextFloat();
            r.NextInt(0, 3);
            Assert.That(r.DrawIndex, Is.EqualTo(3));
        }

        [Test]
        public void Chance_BoundsAreDeterministic()
        {
            var r = new UgasRandom(1);
            Assert.That(r.Chance(0f), Is.False);
            Assert.That(r.Chance(1f), Is.True);
        }
    }
}
