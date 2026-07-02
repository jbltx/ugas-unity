using System.Collections.Generic;
using Jbltx.Ugas.Definitions;
using Jbltx.Ugas.Kernel;
using Jbltx.Ugas.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Jbltx.Ugas.Tests.Editor
{
    /// <summary>
    /// Regression coverage for the no-pack deck-builder eval finding F2: a <c>SetByCaller</c> magnitude
    /// (SPEC §9.4.2) resolves to the per-application value the caller passes at <c>ApplyEffect</c> time,
    /// keyed by <c>DataTag</c> — instead of the previous silent no-op (`return magnitude.Value; // TODO`).
    /// This is the idiomatic "many cards share one damage effect, each passing its own base value" pattern.
    /// Resolvable both in a modifier magnitude and inside an ExecCalc (via <c>ExecutionContext.GetSetByCaller</c>).
    /// </summary>
    [TestFixture]
    public class SetByCallerTests
    {
        private readonly List<Object> _spawned = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        private UgasController Owner()
        {
            var set = ScriptableObject.CreateInstance<AttributeSetDefinition>();
            _spawned.Add(set);
            set.Populate("S", null, new List<AttributeDefinition>
            {
                new AttributeDefinition { Name = "Health", DefaultBaseValue = 100f, Category = AttributeCategory.Resource, Min = AttributeBound.FromLiteral(0f) },
            });
            var go = new GameObject("gc");
            _spawned.Add(go);
            var gc = go.AddComponent<UgasController>();
            gc.RegisterAttributeSet(new RuntimeAttributeSet(set));
            return gc;
        }

        private GameplayEffectDefinition SetByCallerDamage(string name, string dataTag, float fallback)
        {
            var e = ScriptableObject.CreateInstance<GameplayEffectDefinition>();
            _spawned.Add(e);
            e.Populate(name, DurationPolicy.Instant, default, default, ExecutionPolicy.RunInParallel, 0,
                new List<ModifierDefinition>
                {
                    new ModifierDefinition { Attribute = "Health", Operation = ModifierOp.Add,
                        Magnitude = new MagnitudeDefinition { Type = MagnitudeType.SetByCaller, DataTag = dataTag, Value = fallback } },
                }, null, null, null);
            return e;
        }

        [Test]
        public void SetByCaller_ModifierMagnitude_UsesCallerSuppliedValue()
        {
            var gc = Owner();
            var dmg = SetByCallerDamage("GE_CardDamage", "Damage.Base", 0f);
            gc.ApplyEffect(dmg, 1, null, new Dictionary<string, float> { { "Damage.Base", -6f } });
            Assert.That(gc.GetBaseValue("Health"), Is.EqualTo(94f).Within(1e-4f), "SetByCaller Damage.Base = -6 → 100 - 6 = 94");
        }

        [Test]
        public void SetByCaller_ShareOneEffect_DifferentCallerValues()
        {
            // The card pattern: one shared effect, each play passes its own base value.
            var a = Owner();
            var b = Owner();
            var dmg = SetByCallerDamage("GE_CardDamage", "Damage.Base", 0f);
            a.ApplyEffect(dmg, 1, null, new Dictionary<string, float> { { "Damage.Base", -6f } });   // Strike
            b.ApplyEffect(dmg, 1, null, new Dictionary<string, float> { { "Damage.Base", -11f } });  // Bash
            Assert.That(a.GetBaseValue("Health"), Is.EqualTo(94f).Within(1e-4f), "same effect, caller -6");
            Assert.That(b.GetBaseValue("Health"), Is.EqualTo(89f).Within(1e-4f), "same effect, caller -11");
        }

        [Test]
        public void SetByCaller_NotSupplied_FallsBackToStaticValue()
        {
            var gc = Owner();
            var dmg = SetByCallerDamage("GE_CardDamage", "Damage.Base", -1f); // fallback -1
            gc.ApplyEffect(dmg, 1, null, null); // no caller map
            Assert.That(gc.GetBaseValue("Health"), Is.EqualTo(99f).Within(1e-4f), "no SetByCaller supplied → falls back to the static Value (-1) → 99");
        }

        [Test]
        public void SetByCaller_ReadableInsideExecCalc()
        {
            var gc = Owner();
            gc.RegisterExecution("ExecCalc_CardHit", new CardHit());
            var e = ScriptableObject.CreateInstance<GameplayEffectDefinition>();
            _spawned.Add(e);
            e.Populate("GE_ExecCard", DurationPolicy.Instant, default, default, ExecutionPolicy.RunInParallel, 0,
                new List<ModifierDefinition>(), null, null, null);
            e.SetExecutionClass("ExecCalc_CardHit");
            gc.ApplyEffect(e, 1, null, new Dictionary<string, float> { { "Damage.Base", 8f } });
            Assert.That(gc.GetBaseValue("Health"), Is.EqualTo(92f).Within(1e-4f), "ExecCalc read SetByCaller Damage.Base=8 via ctx.GetSetByCaller → 100 - 8 = 92");
        }

        private sealed class CardHit : IExecutionCalculation
        {
            public void Execute(ExecutionContext ctx) => ctx.AddToTarget("Health", -ctx.GetSetByCaller("Damage.Base"));
        }
    }
}
