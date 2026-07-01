using System.Collections.Generic;
using System.Linq;
using Jbltx.Ugas.Cues;
using Jbltx.Ugas.Definitions;
using Jbltx.Ugas.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Jbltx.Ugas.Tests.Editor
{
    /// <summary>
    /// Conformance for gameplay cues (SPEC §12): an effect's <c>GameplayCue.*</c> tags surface as
    /// Execute (instant/periodic) / Add (durational apply) / Remove (durational expiry) notifications
    /// on the controller, and the <see cref="UgasCueManager"/> dispatches them to per-tag handlers. The
    /// runtime raises tags only — it never touches presentation assets (§12.5).
    /// </summary>
    [TestFixture]
    public class CueTests
    {
        private readonly List<Object> _spawned = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        private UgasController Actor()
        {
            var go = new GameObject("Actor");
            _spawned.Add(go);
            return go.AddComponent<UgasController>();
        }

        private GameplayEffectDefinition CueEffect(string cueTag, DurationPolicy policy, float duration = 0f)
        {
            var e = ScriptableObject.CreateInstance<GameplayEffectDefinition>();
            _spawned.Add(e);
            var dur = policy == DurationPolicy.HasDuration ? MagnitudeDefinition.Scalable(duration) : default;
            e.Populate("GE_Cue", policy, dur, default, ExecutionPolicy.RunInParallel, 0,
                new List<ModifierDefinition>(), null, null, new List<string> { cueTag });
            return e;
        }

        [Test]
        public void InstantEffect_FiresBurstCue()
        {
            var gc = Actor();
            var events = new List<GameplayCueEvent>();
            gc.OnGameplayCue += e => events.Add(e);

            gc.ApplyEffect(CueEffect("GameplayCue.Impact.Fire", DurationPolicy.Instant));

            Assert.That(events, Has.Count.EqualTo(1));
            Assert.That(events[0].CueTag, Is.EqualTo("GameplayCue.Impact.Fire"));
            Assert.That(events[0].Type, Is.EqualTo(CueNotifyType.Execute));
            Assert.That(events[0].Target, Is.SameAs(gc));
        }

        [Test]
        public void DurationalEffect_FiresAddOnApply_RemoveOnExpiry()
        {
            var gc = Actor();
            var events = new List<GameplayCueEvent>();
            gc.OnGameplayCue += e => events.Add(e);

            gc.ApplyEffect(CueEffect("GameplayCue.Status.Burning", DurationPolicy.HasDuration, 1f));
            Assert.That(events.Select(e => e.Type), Does.Contain(CueNotifyType.Add), "looping cue starts on apply");

            gc.Tick(1.1f); // past the 1s duration → the effect ends
            var last = events[events.Count - 1];
            Assert.That(last.Type, Is.EqualTo(CueNotifyType.Remove), "looping cue stops on expiry");
            Assert.That(last.CueTag, Is.EqualTo("GameplayCue.Status.Burning"));
        }

        [Test]
        public void CueManager_DispatchesOnlyRegisteredTags()
        {
            var gc = Actor();
            var manager = new UgasCueManager();
            int fireHits = 0;
            manager.Register("GameplayCue.Impact.Fire", _ => fireHits++);
            manager.Attach(gc);

            gc.ApplyEffect(CueEffect("GameplayCue.Impact.Fire", DurationPolicy.Instant));
            Assert.That(fireHits, Is.EqualTo(1), "registered tag dispatched");

            gc.ApplyEffect(CueEffect("GameplayCue.Impact.Ice", DurationPolicy.Instant));
            Assert.That(fireHits, Is.EqualTo(1), "unregistered tag ignored");
        }
    }
}
