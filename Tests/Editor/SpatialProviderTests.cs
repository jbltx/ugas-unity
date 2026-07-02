using System.Collections.Generic;
using System.Linq;
using Jbltx.Ugas.Runtime;
using Jbltx.Ugas.Spatial;
using NUnit.Framework;
using UnityEngine;

namespace Jbltx.Ugas.Tests.Editor
{
    /// <summary>
    /// Conformance for the <see cref="SimpleSpatialProvider"/> against the SPEC §17.2 query contract:
    /// distance, radius culling, nearest-first ordering with a deterministic tie-break, result caps,
    /// and §7 tag filters (require / exclude). Uses real <see cref="UgasController"/> anchors so the
    /// live transform + <c>OwnedTags</c> paths are exercised, mirroring the eval-sim harness.
    /// </summary>
    [TestFixture]
    public class SpatialProviderTests
    {
        private readonly List<Object> _spawned = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        private UgasController Anchor(string name, Vector3 pos)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            go.transform.position = pos;
            return go.AddComponent<UgasController>();
        }

        // Four anchors on the X axis: A@0, B@3, C@7, D@20, registered in that order.
        private (SimpleSpatialProvider provider, UgasController a, UgasController b, UgasController c, UgasController d) Scene()
        {
            var a = Anchor("A", new Vector3(0, 0, 0));
            var b = Anchor("B", new Vector3(3, 0, 0));
            var c = Anchor("C", new Vector3(7, 0, 0));
            var d = Anchor("D", new Vector3(20, 0, 0));

            var provider = new SimpleSpatialProvider();
            provider.Register(a);
            provider.Register(b);
            provider.Register(c);
            provider.Register(d);
            return (provider, a, b, c, d);
        }

        private static string[] Names(IEnumerable<UgasController> gcs) => gcs.Select(g => g.name).ToArray();

        [Test]
        public void Distance_IsEuclidean()
        {
            var (provider, a, b, _, _) = Scene();
            Assert.That(provider.Distance(a.transform.position, b.transform.position), Is.EqualTo(3f).Within(1e-4f));
        }

        [Test]
        public void OverlapSphere_CullsByRadius_NearestFirst()
        {
            var (provider, _, _, _, _) = Scene();
            var hits = provider.OverlapSphere(Vector3.zero, 5f, SpatialFilter.None);
            Assert.That(Names(hits), Is.EqualTo(new[] { "A", "B" }));
        }

        [Test]
        public void Nearest_ReturnsNClosest_Ordered()
        {
            var (provider, _, _, _, _) = Scene();
            var hits = provider.Nearest(Vector3.zero, 3, SpatialFilter.None);
            Assert.That(Names(hits), Is.EqualTo(new[] { "A", "B", "C" }));
        }

        [Test]
        public void MaxResults_CapsAfterOrdering()
        {
            var (provider, _, _, _, _) = Scene();
            var hits = provider.OverlapSphere(Vector3.zero, 100f, new SpatialFilter { MaxResults = 2 });
            Assert.That(Names(hits), Is.EqualTo(new[] { "A", "B" }));
        }

        [Test]
        public void RequireTags_FiltersToMatchingAnchors()
        {
            var (provider, _, b, _, d) = Scene();
            b.GrantTag("Faction.Enemy");
            d.GrantTag("Faction.Enemy");

            var hits = provider.OverlapSphere(Vector3.zero, 100f, new SpatialFilter { RequireTags = new[] { "Faction.Enemy" } });
            Assert.That(Names(hits), Is.EqualTo(new[] { "B", "D" }));
        }

        [Test]
        public void ExcludeTags_OmitsMatchingAnchors()
        {
            var (provider, _, b, _, d) = Scene();
            b.GrantTag("Faction.Enemy");
            d.GrantTag("Faction.Enemy");

            // A and C are never initialized (no public call) — exercises the null-OwnedTags guard.
            var hits = provider.OverlapSphere(Vector3.zero, 100f, new SpatialFilter { ExcludeTags = new[] { "Faction.Enemy" } });
            Assert.That(Names(hits), Is.EqualTo(new[] { "A", "C" }));
        }

        [Test]
        public void TagFilter_IsSoundAcrossControllersWithDifferentRegistries()
        {
            // Force divergent intern orders so "Faction.Enemy" gets a DIFFERENT handle id in each
            // registry — the exact condition under which the old handle-based filter silently
            // mismatched across controllers (harness-eval F3).
            var x = Anchor("X", new Vector3(1, 0, 0));
            x.GrantTag("Team.Red");       // interned first here → pushes Faction.Enemy to a higher id
            x.GrantTag("Faction.Enemy");
            var y = Anchor("Y", new Vector3(2, 0, 0));
            y.GrantTag("Faction.Enemy");  // interned first here → a lower id

            Assert.That(x.TagRegistry.Find("Faction.Enemy").Id,
                Is.Not.EqualTo(y.TagRegistry.Find("Faction.Enemy").Id),
                "precondition: the same tag has different handle ids in the two registries");

            var provider = new SimpleSpatialProvider();
            provider.Register(x);
            provider.Register(y);

            // Name-based matching resolves per candidate registry, so both match despite the id divergence.
            var required = provider.OverlapSphere(Vector3.zero, 100f, new SpatialFilter { RequireTags = new[] { "Faction.Enemy" } });
            Assert.That(Names(required), Is.EquivalentTo(new[] { "X", "Y" }), "require-by-name matches both registries");

            var excluded = provider.OverlapSphere(Vector3.zero, 100f, new SpatialFilter { ExcludeTags = new[] { "Faction.Enemy" } });
            Assert.That(excluded, Is.Empty, "exclude-by-name omits both");
        }

        [Test]
        public void OverlapCone_ClipsByAngleAndRadius()
        {
            var onAxis = Anchor("OnAxis", new Vector3(5, 0, 0));  // dead ahead
            var offAxis = Anchor("OffAxis", new Vector3(0, 5, 0)); // 90° to the side
            var behind = Anchor("Behind", new Vector3(-5, 0, 0));  // opposite direction
            var tooFar = Anchor("TooFar", new Vector3(20, 0, 0));  // on axis but out of range

            var provider = new SimpleSpatialProvider();
            provider.Register(onAxis);
            provider.Register(offAxis);
            provider.Register(behind);
            provider.Register(tooFar);

            // Cone from origin along +X, radius 10, half-angle 45°.
            var hits = provider.OverlapCone(Vector3.zero, Vector3.right, 10f, 45f, SpatialFilter.None);
            Assert.That(Names(hits), Is.EqualTo(new[] { "OnAxis" }));
        }
    }
}
