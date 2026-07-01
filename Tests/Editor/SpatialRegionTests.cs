using System.Collections.Generic;
using Jbltx.Ugas.Definitions;
using Jbltx.Ugas.Runtime;
using Jbltx.Ugas.Spatial;
using NUnit.Framework;
using UnityEngine;

namespace Jbltx.Ugas.Tests.Editor
{
    /// <summary>
    /// Conformance for zones/regions (SPEC §17.4) via <see cref="SpatialRegion"/>: occupancy is a
    /// standing §17.2 query, entering grants the region's tags and leaving removes them, and grants are
    /// §7.2 reference-counted so overlapping regions granting the same tag keep it until a GC leaves
    /// all of them.
    /// </summary>
    [TestFixture]
    public class SpatialRegionTests
    {
        private readonly List<Object> _spawned = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        private UgasController Actor(string name, Vector3 pos)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            go.transform.position = pos;
            return go.AddComponent<UgasController>();
        }

        private RegionDefinition Region(string name, float radius, params string[] grantedTags)
        {
            var so = ScriptableObject.CreateInstance<RegionDefinition>();
            _spawned.Add(so);
            so.Populate(name, RegionShape.Sphere, radius, new List<string>(grantedTags));
            return so;
        }

        [Test]
        public void EnteringRegion_GrantsTag_LeavingRemovesIt()
        {
            var gc = Actor("Actor", new Vector3(2, 0, 0)); // inside radius 5
            var provider = new SimpleSpatialProvider();
            provider.Register(gc);

            var region = new SpatialRegion(Region("Fire", 5f, "Zone.Hazard.Fire"), Vector3.zero);

            region.Evaluate(provider);
            Assert.That(gc.OwnedTags.HasTag("Zone.Hazard.Fire"), Is.True, "entered → granted");
            Assert.That(region.Occupants, Has.Member(gc));

            gc.transform.position = new Vector3(10, 0, 0); // out of radius 5
            region.Evaluate(provider);
            Assert.That(gc.OwnedTags.HasTag("Zone.Hazard.Fire"), Is.False, "left → removed");
            Assert.That(region.Occupants, Has.No.Member(gc));
        }

        [Test]
        public void OverlappingRegions_SameTag_RefCountedUntilLeavingBoth()
        {
            var gc = Actor("Actor", new Vector3(3, 0, 0)); // inside both
            var provider = new SimpleSpatialProvider();
            provider.Register(gc);

            var inner = new SpatialRegion(Region("InnerRally", 5f, "Buff.Rally"), Vector3.zero);
            var outer = new SpatialRegion(Region("OuterRally", 20f, "Buff.Rally"), Vector3.zero);

            inner.Evaluate(provider);
            outer.Evaluate(provider);
            Assert.That(gc.OwnedTags.HasTag("Buff.Rally"), Is.True, "in both");
            Assert.That(gc.OwnedTags.GetTagCount(gc.TagRegistry.Resolve("Buff.Rally")), Is.EqualTo(2), "granted once per region");

            gc.transform.position = new Vector3(10, 0, 0); // out of inner(5), still in outer(20)
            inner.Evaluate(provider);
            outer.Evaluate(provider);
            Assert.That(gc.OwnedTags.HasTag("Buff.Rally"), Is.True, "still granted by outer");

            gc.transform.position = new Vector3(30, 0, 0); // out of both
            inner.Evaluate(provider);
            outer.Evaluate(provider);
            Assert.That(gc.OwnedTags.HasTag("Buff.Rally"), Is.False, "left both → removed");
        }
    }
}
