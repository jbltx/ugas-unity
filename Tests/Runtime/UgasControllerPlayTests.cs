using System.Collections;
using System.Collections.Generic;
using Jbltx.Ugas.Definitions;
using Jbltx.Ugas.Kernel;
using Jbltx.Ugas.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Jbltx.Ugas.Tests.Play
{
    /// <summary>
    /// PlayMode conformance: a <see cref="UgasController"/> MonoBehaviour ticked off the real
    /// player loop (Update) advances active effects over actual frames. SOs are built directly via
    /// their populate APIs (this assembly does not reference the editor importer).
    /// </summary>
    public class UgasControllerPlayTests
    {
        private readonly List<Object> _spawned = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned) if (o != null) Object.Destroy(o);
            _spawned.Clear();
        }

        private AttributeSetDefinition VitalsSet()
        {
            var so = ScriptableObject.CreateInstance<AttributeSetDefinition>();
            _spawned.Add(so);
            so.Populate("Vitals", null, new List<AttributeDefinition>
            {
                new AttributeDefinition { Name = "Health", DefaultBaseValue = 100f, Category = AttributeCategory.Resource }
            });
            return so;
        }

        private GameplayEffectDefinition RegenEffect()
        {
            var so = ScriptableObject.CreateInstance<GameplayEffectDefinition>();
            _spawned.Add(so);
            so.Populate(
                "GE_Regen", DurationPolicy.HasDuration,
                MagnitudeDefinition.Scalable(0.5f), // 0.5s duration (expires within the 0.8s wait below)
                new PeriodDefinition { Period = 0.1f, ExecuteOnApplication = false },
                ExecutionPolicy.RunInParallel, 0,
                new List<ModifierDefinition>
                {
                    new ModifierDefinition
                    {
                        Attribute = "Health",
                        Operation = ModifierOp.Add,
                        Magnitude = MagnitudeDefinition.Scalable(5f)
                    }
                },
                new List<string> { "State.Buff.Regenerating" }, null, null);
            return so;
        }

        [UnityTest]
        public IEnumerator PeriodicEffect_TicksOverRealFrames_ThenExpires()
        {
            var go = new GameObject("UGAS PlayMode Controller");
            _spawned.Add(go);
            var gc = go.AddComponent<UgasController>(); // ticks itself from Update

            gc.RegisterAttributeSet(new RuntimeAttributeSet(VitalsSet()));
            float startHealth = gc.GetBaseValue("Health");

            gc.ApplyEffect(RegenEffect()); // +5 Health every 0.1s for 0.5s
            Assert.That(gc.OwnedTags.HasTag("State.Buff.Regenerating"), Is.True);

            // Let real frames elapse past the 0.5s duration.
            float elapsed = 0f;
            while (elapsed < 0.8f)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            // Effect should have expired and healed in several +5 increments.
            Assert.That(gc.Effects.ActiveEffects.Count, Is.EqualTo(0), "effect expired via Update tick");
            Assert.That(gc.OwnedTags.HasTag("State.Buff.Regenerating"), Is.False);
            Assert.That(gc.GetBaseValue("Health"), Is.GreaterThan(startHealth), "periodic healing applied across frames");
        }
    }
}
