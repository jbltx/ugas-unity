using System.Collections.Generic;
using Jbltx.Ugas.Definitions;
using Jbltx.Ugas.Runtime;
using Jbltx.Ugas.Spatial;
using NUnit.Framework;
using UnityEngine;

namespace Jbltx.Ugas.Tests.Editor
{
    /// <summary>
    /// Integration conformance for <see cref="UgasSpatialWorld"/> (SPEC §17 engine binding): a single
    /// <c>Tick</c> reconciles both zone membership (§17.4) and perception (§17.5) against the shared
    /// provider, and the world's area-effect helper (§17.3) routes through that same provider.
    /// </summary>
    [TestFixture]
    public class SpatialWorldTests
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

        private PerceptionDefinition Perception(float range, params string[] perceivingTags)
        {
            var so = ScriptableObject.CreateInstance<PerceptionDefinition>();
            _spawned.Add(so);
            so.Populate(MagnitudeDefinition.Scalable(range), 0f, false, null, null, new List<string>(perceivingTags));
            return so;
        }

        [Test]
        public void Tick_EvaluatesRegionsAndPerceptionsTogether()
        {
            var world = new UgasSpatialWorld();
            var a = Actor("A", Vector3.zero);
            var b = Actor("B", new Vector3(0, 0, 3));
            world.Register(a);
            world.Register(b);

            world.AddRegion(Region("SafeZone", 5f, "Zone.Safe"), Vector3.zero);
            world.AddObserver(a, Perception(10f, "State.Perceiving"));

            world.Tick();

            Assert.That(a.OwnedTags.HasTag("Zone.Safe"), Is.True, "A inside region");
            Assert.That(b.OwnedTags.HasTag("Zone.Safe"), Is.True, "B inside region");
            Assert.That(a.OwnedTags.HasTag("State.Perceiving"), Is.True, "A perceives B");
        }

        [Test]
        public void ApplyAreaEffect_ThroughWorld_UsesItsProvider()
        {
            var world = new UgasSpatialWorld();
            var caster = Actor("Caster", Vector3.zero);
            var near = Actor("Near", new Vector3(0, 0, 3));
            var far = Actor("Far", new Vector3(0, 0, 30));
            world.Register(near);
            world.Register(far);

            var effect = ScriptableObject.CreateInstance<GameplayEffectDefinition>();
            _spawned.Add(effect);
            effect.Populate("Area Marker", DurationPolicy.Instant, default, default, ExecutionPolicy.RunInParallel, 0,
                new List<ModifierDefinition>(), new List<string>(), null, null);
            effect.SetArea(new AreaDefinition { Enabled = true, Shape = AreaShape.Sphere, Radius = MagnitudeDefinition.Scalable(5f) });

            var affected = world.ApplyAreaEffect(caster, effect, Vector3.zero);

            Assert.That(affected, Has.Count.EqualTo(1));
            Assert.That(affected[0].name, Is.EqualTo("Near")); // Far (30) is outside radius 5
        }
    }
}
