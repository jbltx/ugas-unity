using System.Collections.Generic;
using System.Linq;
using Jbltx.Ugas.Definitions;
using Jbltx.Ugas.Runtime;
using Jbltx.Ugas.Spatial;
using NUnit.Framework;
using UnityEngine;

namespace Jbltx.Ugas.Tests.Editor
{
    /// <summary>
    /// Conformance for the uniform-grid provider (SPEC §17.6): it must return exactly what the baseline
    /// <see cref="SimpleSpatialProvider"/> returns for the same inputs (parity), reflect moved anchors
    /// after <see cref="GridSpatialProvider.Rebuild"/>, and work as the <see cref="UgasSpatialWorld"/>
    /// index (the world refreshes it each tick).
    /// </summary>
    [TestFixture]
    public class GridSpatialProviderTests
    {
        private readonly List<Object> _spawned = new List<Object>();

        [TearDown]
        public void TearDown()
        {
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

        // Registers the same anchors (same order) with a live and a grid provider; rebuilds the grid.
        private (SimpleSpatialProvider simple, GridSpatialProvider grid) TwoProviders(params Vector3[] positions)
        {
            var simple = new SimpleSpatialProvider();
            var grid = new GridSpatialProvider(10f);
            for (int i = 0; i < positions.Length; i++)
            {
                var gc = At("A" + i, positions[i]);
                simple.Register(gc);
                grid.Register(gc);
            }
            grid.Rebuild();
            return (simple, grid);
        }

        private static string[] Names(IEnumerable<UgasController> gcs) => gcs.Select(g => g.name).ToArray();

        [Test]
        public void OverlapSphere_MatchesSimpleProvider()
        {
            var (simple, grid) = TwoProviders(new Vector3(1, 0, 0), new Vector3(4, 0, 0), new Vector3(12, 0, 0), new Vector3(0, 0, 7), new Vector3(25, 0, 0));
            var center = new Vector3(2, 0, 0);
            CollectionAssert.AreEqual(
                Names(simple.OverlapSphere(center, 8f, SpatialFilter.None)),
                Names(grid.OverlapSphere(center, 8f, SpatialFilter.None)));
        }

        [Test]
        public void OverlapCone_MatchesSimpleProvider()
        {
            var (simple, grid) = TwoProviders(new Vector3(5, 0, 0), new Vector3(0, 5, 0), new Vector3(-5, 0, 0), new Vector3(9, 0, 0));
            CollectionAssert.AreEqual(
                Names(simple.OverlapCone(Vector3.zero, Vector3.right, 10f, 45f, SpatialFilter.None)),
                Names(grid.OverlapCone(Vector3.zero, Vector3.right, 10f, 45f, SpatialFilter.None)));
        }

        [Test]
        public void Nearest_MatchesSimpleProvider()
        {
            var (simple, grid) = TwoProviders(new Vector3(3, 0, 0), new Vector3(1, 0, 0), new Vector3(9, 0, 0), new Vector3(14, 0, 0));
            CollectionAssert.AreEqual(
                Names(simple.Nearest(Vector3.zero, 3, SpatialFilter.None)),
                Names(grid.Nearest(Vector3.zero, 3, SpatialFilter.None)));
        }

        [Test]
        public void MaxResults_MatchesSimpleProvider()
        {
            var (simple, grid) = TwoProviders(new Vector3(2, 0, 0), new Vector3(5, 0, 0), new Vector3(8, 0, 0), new Vector3(1, 0, 0));
            var filter = new SpatialFilter { MaxResults = 2 };
            CollectionAssert.AreEqual(
                Names(simple.OverlapSphere(Vector3.zero, 100f, filter)),
                Names(grid.OverlapSphere(Vector3.zero, 100f, filter)));
        }

        [Test]
        public void Rebuild_ReflectsMovedAnchors()
        {
            var grid = new GridSpatialProvider(10f);
            var gc = At("mover", Vector3.zero);
            grid.Register(gc);
            grid.Rebuild();
            Assert.That(Names(grid.OverlapSphere(Vector3.zero, 3f, SpatialFilter.None)), Is.EqualTo(new[] { "mover" }));

            gc.transform.position = new Vector3(100, 0, 0);
            grid.Rebuild();
            Assert.That(grid.OverlapSphere(Vector3.zero, 3f, SpatialFilter.None), Is.Empty, "no longer near origin");
            Assert.That(Names(grid.OverlapSphere(new Vector3(100, 0, 0), 3f, SpatialFilter.None)), Is.EqualTo(new[] { "mover" }), "found in its new cell");
        }

        [Test]
        public void World_WithGridProvider_RegionGrantsTagsOnTick()
        {
            var world = new UgasSpatialWorld(new GridSpatialProvider(10f));
            var gc = At("actor", new Vector3(2, 0, 0));
            world.Register(gc);

            var region = ScriptableObject.CreateInstance<RegionDefinition>();
            _spawned.Add(region);
            region.Populate("SafeZone", RegionShape.Sphere, 5f, new List<string> { "Zone.Safe" });
            world.AddRegion(region, Vector3.zero);

            world.Tick(); // refreshes the grid, then evaluates the region against it
            Assert.That(gc.OwnedTags.HasTag("Zone.Safe"), Is.True);
        }
    }
}
