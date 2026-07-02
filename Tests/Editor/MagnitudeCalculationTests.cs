using System.Collections.Generic;
using Jbltx.Ugas.Definitions;
using Jbltx.Ugas.Kernel;
using Jbltx.Ugas.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Jbltx.Ugas.Tests.Editor
{
    /// <summary>
    /// Regression coverage for the last stubbed magnitude type — <c>CustomCalculation</c> (SPEC §9.4.2, a
    /// Modifier Magnitude Calculator / MMC). Previously <c>ResolveMagnitude</c> returned the static Value
    /// (`// TODO: invoke CalculatorClass`). An MMC named by a magnitude's <c>CalculatorClass</c> now computes
    /// the value from source/target attributes + level + SetByCaller, is read-only, and re-evaluates on each
    /// recompute (so a durational MMC modifier tracks live inputs). Unregistered → falls back to Value.
    /// </summary>
    [TestFixture]
    public class MagnitudeCalculationTests
    {
        private readonly List<Object> _spawned = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        private UgasController Controller()
        {
            var set = ScriptableObject.CreateInstance<AttributeSetDefinition>();
            _spawned.Add(set);
            set.Populate("S", null, new List<AttributeDefinition>
            {
                new AttributeDefinition { Name = "Strength", DefaultBaseValue = 0f, Category = AttributeCategory.Statistic },
                new AttributeDefinition { Name = "MaxHealth", DefaultBaseValue = 100f, Category = AttributeCategory.Statistic },
            });
            var go = new GameObject("gc");
            _spawned.Add(go);
            var gc = go.AddComponent<UgasController>();
            gc.RegisterAttributeSet(new RuntimeAttributeSet(set));
            return gc;
        }

        private GameplayEffectDefinition CustomCalcEffect(string name, DurationPolicy policy, string calcClass, float fallback)
        {
            var e = ScriptableObject.CreateInstance<GameplayEffectDefinition>();
            _spawned.Add(e);
            e.Populate(name, policy, default, default, ExecutionPolicy.RunInParallel, 0,
                new List<ModifierDefinition>
                {
                    new ModifierDefinition { Attribute = "MaxHealth", Operation = ModifierOp.Add,
                        Magnitude = new MagnitudeDefinition { Type = MagnitudeType.CustomCalculation, CalculatorClass = calcClass, Value = fallback } },
                }, null, null, null);
            return e;
        }

        // MMC: source Strength × 2 + level × 10 + SetByCaller("Bonus").
        private sealed class BlendMMC : IMagnitudeCalculation
        {
            public float Calculate(MagnitudeCalculationContext c) =>
                c.SourceAttribute("Strength") * 2f + c.Level * 10f + c.GetSetByCaller("Bonus", 0f);
        }

        // MMC: target's own Strength × 3 (a live, self-scaling durational magnitude).
        private sealed class StrScaleMMC : IMagnitudeCalculation
        {
            public float Calculate(MagnitudeCalculationContext c) => c.TargetAttribute("Strength") * 3f;
        }

        [Test]
        public void CustomCalculation_MMC_ComputesFromSourceLevelAndSetByCaller()
        {
            var source = Controller();
            source.FindAttribute("Strength").BaseValue = 5f; source.RecalculateAttributes();
            var target = Controller(); // MaxHealth 100

            target.RegisterMagnitudeCalculation("MMC_Blend", new BlendMMC());
            target.ApplyEffect(CustomCalcEffect("GE_Blend", DurationPolicy.Instant, "MMC_Blend", 0f),
                3, source, new Dictionary<string, float> { { "Bonus", 7f } });

            Assert.That(target.GetBaseValue("MaxHealth"), Is.EqualTo(147f).Within(1e-4f),
                "MMC = source Str 5 × 2 + level 3 × 10 + SetByCaller Bonus 7 = 47 → MaxHealth 100 + 47 = 147");
        }

        [Test]
        public void CustomCalculation_Unregistered_FallsBackToStaticValue()
        {
            var gc = Controller();
            gc.ApplyEffect(CustomCalcEffect("GE_NoCalc", DurationPolicy.Instant, "MMC_Missing", 12f), 1, null, null);
            Assert.That(gc.GetBaseValue("MaxHealth"), Is.EqualTo(112f).Within(1e-4f),
                "no MMC registered for the class → falls back to the static Value (12) → 100 + 12 = 112");
        }

        [Test]
        public void CustomCalculation_DurationalMMC_ReEvaluatesOnRecompute()
        {
            var gc = Controller();
            gc.RegisterMagnitudeCalculation("MMC_StrScale", new StrScaleMMC());
            gc.FindAttribute("Strength").BaseValue = 5f; gc.RecalculateAttributes();

            gc.ApplyEffect(CustomCalcEffect("GE_StrBuff", DurationPolicy.Infinite, "MMC_StrScale", 0f)); // self, current-value modifier
            Assert.That(gc.GetCurrentValue("MaxHealth"), Is.EqualTo(115f).Within(1e-4f), "Str 5 × 3 = 15 → 100 + 15 = 115");

            gc.FindAttribute("Strength").BaseValue = 10f;
            gc.RecalculateAttributes();
            Assert.That(gc.GetCurrentValue("MaxHealth"), Is.EqualTo(130f).Within(1e-4f),
                "MMC re-evaluated on recompute: Str 10 × 3 = 30 → 100 + 30 = 130 (live magnitude)");
        }
    }
}
