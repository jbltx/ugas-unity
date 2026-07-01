using System.Collections.Generic;
using Jbltx.Ugas.Definitions;
using Jbltx.Ugas.Runtime;
using Jbltx.Ugas.Spatial;
using NUnit.Framework;
using UnityEngine;

namespace Jbltx.Ugas.Tests.Editor
{
    /// <summary>
    /// Conformance for perception (SPEC §17.5) via <see cref="Perception"/>: a target is perceived iff
    /// it is inside the sense volume (range, narrowed by field-of-view) AND, when required, has
    /// line-of-sight; awareness is granted as a tag on the observer while it perceives anything and
    /// removed when it perceives nothing; the observer never perceives itself.
    /// </summary>
    [TestFixture]
    public class SpatialPerceptionTests
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

        private PerceptionDefinition Config(float range, float fovHalfAngleDeg, bool requireLos, params string[] perceivingTags)
        {
            var so = ScriptableObject.CreateInstance<PerceptionDefinition>();
            _spawned.Add(so);
            so.Populate(MagnitudeDefinition.Scalable(range), fovHalfAngleDeg, requireLos, null, null, new List<string>(perceivingTags));
            return so;
        }

        [Test]
        public void PerceivesInRange_GrantsAwareness_LosingTargetRemovesIt()
        {
            var observer = Actor("Observer", Vector3.zero);
            var target = Actor("Target", new Vector3(0, 0, 5)); // within range 10

            var provider = new SimpleSpatialProvider();
            provider.Register(observer);
            provider.Register(target);

            var perception = new Perception(observer, Config(10f, 0f, false, "State.Perceiving"));

            perception.Evaluate(provider);
            Assert.That(perception.Perceived, Has.Member(target));
            Assert.That(perception.Perceived, Has.No.Member(observer), "never perceives self");
            Assert.That(perception.IsPerceiving, Is.True);
            Assert.That(observer.OwnedTags.HasTag("State.Perceiving"), Is.True);

            target.transform.position = new Vector3(0, 0, 50); // out of range
            perception.Evaluate(provider);
            Assert.That(perception.Perceived, Is.Empty);
            Assert.That(observer.OwnedTags.HasTag("State.Perceiving"), Is.False);
        }

        [Test]
        public void FieldOfView_ExcludesTargetsOutsideTheCone()
        {
            var observer = Actor("Observer", Vector3.zero); // faces +Z
            var ahead = Actor("Ahead", new Vector3(0, 0, 5));
            var beside = Actor("Beside", new Vector3(5, 0, 0)); // 90° to the side

            var provider = new SimpleSpatialProvider();
            provider.Register(observer);
            provider.Register(ahead);
            provider.Register(beside);

            var perception = new Perception(observer, Config(10f, 45f, false, "State.Perceiving"));
            perception.Evaluate(provider);

            Assert.That(perception.Perceived, Has.Member(ahead));
            Assert.That(perception.Perceived, Has.No.Member(beside));
        }

        [Test]
        public void RequireLineOfSight_OccludedTargetNotPerceived()
        {
            var observer = Actor("Observer", Vector3.zero);
            var target = Actor("Target", new Vector3(0, 0, 5));

            var provider = new SimpleSpatialProvider();
            provider.Register(observer);
            provider.Register(target);

            var perception = new Perception(observer, Config(10f, 0f, true, "State.Perceiving"))
            {
                LineOfSightTest = (from, to) => false, // fully occluded
            };

            perception.Evaluate(provider);
            Assert.That(perception.Perceived, Is.Empty);
            Assert.That(perception.IsPerceiving, Is.False); // never perceived → observer stays uninitialized

            perception.LineOfSightTest = (from, to) => true; // sight lines clear
            perception.Evaluate(provider);
            Assert.That(perception.Perceived, Has.Member(target));
            Assert.That(perception.IsPerceiving, Is.True);
            Assert.That(observer.OwnedTags.HasTag("State.Perceiving"), Is.True); // grant initialized the observer
        }
    }
}
