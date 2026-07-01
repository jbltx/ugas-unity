#if UGAS_DOTS
using System.Collections.Generic;
using System.Linq;
using Jbltx.Ugas.Dots;
using Jbltx.Ugas.Runtime;
using Jbltx.Ugas.Spatial;
using NUnit.Framework;
using UnityEngine;

namespace Jbltx.Ugas.Tests.Dots
{
    /// <summary>
    /// Conformance for the Burst-accelerated <see cref="DotsSpatialProvider"/> (SPEC §17.6): it must
    /// return exactly what the baseline <see cref="SimpleSpatialProvider"/> returns for the same inputs
    /// — same set, same nearest-first + registration-order ordering — so the fast path is a drop-in.
    /// </summary>
    [TestFixture]
    public class DotsSpatialProviderTests
    {
        private readonly List<Object> _spawned = new List<Object>();
        private DotsSpatialProvider _dots;

        [TearDown]
        public void TearDown()
        {
            _dots?.Dispose();
            _dots = null;
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        private UgasController At(string name, Vector3 pos)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            go.transform.position = pos;
            return go.AddComponent<UgasController>();
        }

        private (SimpleSpatialProvider simple, DotsSpatialProvider dots) TwoProviders(params Vector3[] positions)
        {
            var simple = new SimpleSpatialProvider();
            _dots = new DotsSpatialProvider();
            for (int i = 0; i < positions.Length; i++)
            {
                var gc = At("A" + i, positions[i]);
                simple.Register(gc);
                _dots.Register(gc);
            }
            _dots.Rebuild();
            return (simple, _dots);
        }

        private static string[] Names(IEnumerable<UgasController> gcs) => gcs.Select(g => g.name).ToArray();

        [Test]
        public void OverlapSphere_MatchesSimpleProvider()
        {
            var (simple, dots) = TwoProviders(new Vector3(1, 0, 0), new Vector3(4, 0, 0), new Vector3(12, 0, 0), new Vector3(0, 0, 7), new Vector3(25, 0, 0));
            var center = new Vector3(2, 0, 0);
            CollectionAssert.AreEqual(
                Names(simple.OverlapSphere(center, 8f, SpatialFilter.None)),
                Names(dots.OverlapSphere(center, 8f, SpatialFilter.None)));
        }

        [Test]
        public void OverlapCone_MatchesSimpleProvider()
        {
            var (simple, dots) = TwoProviders(new Vector3(5, 0, 0), new Vector3(0, 5, 0), new Vector3(-5, 0, 0), new Vector3(9, 0, 0));
            CollectionAssert.AreEqual(
                Names(simple.OverlapCone(Vector3.zero, Vector3.right, 10f, 45f, SpatialFilter.None)),
                Names(dots.OverlapCone(Vector3.zero, Vector3.right, 10f, 45f, SpatialFilter.None)));
        }

        [Test]
        public void Nearest_MatchesSimpleProvider()
        {
            var (simple, dots) = TwoProviders(new Vector3(3, 0, 0), new Vector3(1, 0, 0), new Vector3(9, 0, 0), new Vector3(14, 0, 0));
            CollectionAssert.AreEqual(
                Names(simple.Nearest(Vector3.zero, 3, SpatialFilter.None)),
                Names(dots.Nearest(Vector3.zero, 3, SpatialFilter.None)));
        }

        [Test]
        public void MaxResults_MatchesSimpleProvider()
        {
            var (simple, dots) = TwoProviders(new Vector3(2, 0, 0), new Vector3(5, 0, 0), new Vector3(8, 0, 0), new Vector3(1, 0, 0));
            var filter = new SpatialFilter { MaxResults = 2 };
            CollectionAssert.AreEqual(
                Names(simple.OverlapSphere(Vector3.zero, 100f, filter)),
                Names(dots.OverlapSphere(Vector3.zero, 100f, filter)));
        }
    }
}
#endif
