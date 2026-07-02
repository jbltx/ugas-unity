using System.Collections.Generic;
using Jbltx.Ugas.Definitions;
using Jbltx.Ugas.Kernel;
using Jbltx.Ugas.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Jbltx.Ugas.Tests.Editor
{
    /// <summary>
    /// Regression coverage for the RPG-eval finding F2: an Instant effect's <c>Multiply</c> modifier scales
    /// the Base Value by <c>(1 + magnitude)</c> (SPEC §5.2/§5.3) instead of being a silent no-op. Previously
    /// <see cref="GameplayEffectsSystem"/>'s <c>ModifierOp.Multiply</c> branch was an empty TODO.
    /// </summary>
    [TestFixture]
    public class InstantMultiplyTests
    {
        private readonly List<Object> _spawned = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        private UgasController Owner(float weaponDamage)
        {
            var set = ScriptableObject.CreateInstance<AttributeSetDefinition>();
            _spawned.Add(set);
            set.Populate("Test", null, new List<AttributeDefinition>
            {
                new AttributeDefinition { Name = "WeaponDamage", DefaultBaseValue = weaponDamage, Category = AttributeCategory.Statistic },
            });
            var go = new GameObject("Owner");
            _spawned.Add(go);
            var gc = go.AddComponent<UgasController>();
            gc.RegisterAttributeSet(new RuntimeAttributeSet(set));
            return gc;
        }

        private GameplayEffectDefinition InstantMultiply(string name, string attr, float magnitude)
        {
            var e = ScriptableObject.CreateInstance<GameplayEffectDefinition>();
            _spawned.Add(e);
            e.Populate(name, DurationPolicy.Instant, default, default, ExecutionPolicy.RunInParallel, 0,
                new List<ModifierDefinition> { new ModifierDefinition { Attribute = attr, Operation = ModifierOp.Multiply, Magnitude = MagnitudeDefinition.Scalable(magnitude) } },
                null, null, null);
            return e;
        }

        [Test]
        public void InstantMultiply_PositiveMagnitude_ScalesBaseValue()
        {
            var gc = Owner(10f);
            gc.ApplyEffect(InstantMultiply("GE_Double", "WeaponDamage", 1.0f)); // ×(1 + 1.0)
            Assert.That(gc.GetBaseValue("WeaponDamage"), Is.EqualTo(20f).Within(1e-4f), "Instant Multiply +1.0 scales base ×2 → 20 (was a silent no-op before F2)");
            Assert.That(gc.GetCurrentValue("WeaponDamage"), Is.EqualTo(20f).Within(1e-4f), "current tracks the mutated base");
        }

        [Test]
        public void InstantMultiply_NegativeMagnitude_ReducesBaseValue()
        {
            var gc = Owner(20f);
            gc.ApplyEffect(InstantMultiply("GE_Halve", "WeaponDamage", -0.5f)); // ×(1 - 0.5)
            Assert.That(gc.GetBaseValue("WeaponDamage"), Is.EqualTo(10f).Within(1e-4f), "Instant Multiply -0.5 scales base ×0.5 → 10");
        }

        [Test]
        public void InstantMultiply_ZeroMagnitude_IsIdentity()
        {
            var gc = Owner(10f);
            gc.ApplyEffect(InstantMultiply("GE_Noop", "WeaponDamage", 0f)); // ×(1 + 0) = identity
            Assert.That(gc.GetBaseValue("WeaponDamage"), Is.EqualTo(10f).Within(1e-4f), "Instant Multiply 0 is the identity (×1), not a discard");
        }
    }
}
