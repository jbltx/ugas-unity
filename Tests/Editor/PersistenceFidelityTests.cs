using System.Collections.Generic;
using Jbltx.Ugas.Definitions;
using Jbltx.Ugas.Kernel;
using Jbltx.Ugas.Persistence;
using Jbltx.Ugas.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Jbltx.Ugas.Tests.Editor
{
    /// <summary>
    /// Regression coverage for the independent persistence/progression eval findings:
    ///   F-PERSIST-1  directly-granted (loose) tags round-trip through a snapshot onto a fresh controller.
    ///   F-PERSIST-2  an effect's instigator/source is persisted, so a source-scaled magnitude (§9.4.2)
    ///                re-derives against the original instigator after restore, not the restoring controller.
    ///   F-PROGRESS-2 RecalculateAttributes resolves a dependency on a LATER-ordered attribute in a single
    ///                call (iterates to a fixed point) — no stale value after e.g. leveling.
    ///   F-PROGRESS-1 a ScalableFloat magnitude with a level curve scales by the applied level.
    /// </summary>
    [TestFixture]
    public class PersistenceFidelityTests
    {
        private readonly List<Object> _spawned = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        private AttributeSetDefinition Set(string name, params AttributeDefinition[] attrs)
        {
            var so = ScriptableObject.CreateInstance<AttributeSetDefinition>();
            _spawned.Add(so);
            so.Populate(name, null, new List<AttributeDefinition>(attrs));
            return so;
        }

        private static AttributeDefinition Stat(string n, float v) =>
            new AttributeDefinition { Name = n, DefaultBaseValue = v, Category = AttributeCategory.Statistic };

        private UgasController Controller(AttributeSetDefinition set)
        {
            var go = new GameObject("gc");
            _spawned.Add(go);
            var gc = go.AddComponent<UgasController>();
            gc.RegisterAttributeSet(new RuntimeAttributeSet(set));
            return gc;
        }

        private GameplayEffectDefinition Effect(string name, DurationPolicy policy, params ModifierDefinition[] mods)
        {
            var e = ScriptableObject.CreateInstance<GameplayEffectDefinition>();
            _spawned.Add(e);
            e.Populate(name, policy, default, default, ExecutionPolicy.RunInParallel, 0, new List<ModifierDefinition>(mods), null, null, null);
            return e;
        }

        // ===================== F-PERSIST-1 — loose tags round-trip =====================
        [Test]
        public void Snapshot_RestoresDirectlyGrantedTags_OntoFreshController()
        {
            var a = Controller(Set("S", Stat("Health", 100f)));
            a.GrantTag("State.Alive");
            a.GrantTag("Class.Barbarian");
            a.GrantTag("Quest.RescueThePrince");
            var snap = a.CaptureSnapshot();

            var b = Controller(Set("S", Stat("Health", 100f))); // fresh controller, as loading a save
            b.RestoreSnapshot(snap);

            Assert.That(b.OwnedTags.HasTag("State.Alive"), Is.True, "directly-granted lifecycle tag restored (was lost before F-PERSIST-1)");
            Assert.That(b.OwnedTags.HasTag("Class.Barbarian"), Is.True, "class tag restored");
            Assert.That(b.OwnedTags.HasTag("Quest.RescueThePrince"), Is.True, "quest flag restored");
        }

        // ===================== F-PERSIST-2 — instigator/source round-trip =====================
        [Test]
        public void Snapshot_RestoresEffectInstigator_SourceScaledMagnitudeReDerivesAgainstSource()
        {
            var source = Controller(Set("Src", Stat("Might", 100f)));
            var target = Controller(Set("Tgt", Stat("Might", 10f), Stat("MaxHealth", 50f)));

            // Infinite buff on target: MaxHealth += SOURCE.Might (AttributeBased, Source-scaled §9.4.2).
            var buff = Effect("GE_SourceBuff", DurationPolicy.Infinite,
                new ModifierDefinition
                {
                    Attribute = "MaxHealth", Operation = ModifierOp.Add,
                    Magnitude = new MagnitudeDefinition { Type = MagnitudeType.AttributeBased, BackingAttribute = "Might", Source = MagnitudeSource.Source, Coefficient = 1f },
                });
            target.ApplyEffect(buff, 1, source);
            Assert.That(target.GetCurrentValue("MaxHealth"), Is.EqualTo(150f).Within(1e-4f), "50 + source Might 100 = 150 pre-save");

            var snap = target.CaptureSnapshot();
            var restored = Controller(Set("Tgt", Stat("Might", 10f), Stat("MaxHealth", 50f))); // fresh; source still alive
            restored.RestoreSnapshot(snap);

            Assert.That(restored.GetCurrentValue("MaxHealth"), Is.EqualTo(150f).Within(1e-4f),
                "source-scaled buff re-derives against the RESTORED instigator (source Might 100 → 150), not the restoring controller's Might 10 (which would give 60)");
        }

        // ===================== F-PROGRESS-2 — recalc resolves a later-ordered dependency =====================
        [Test]
        public void RecalculateAttributes_ResolvesLaterOrderedDependency_InOneRecalc()
        {
            // Derived is declared FIRST, Source LAST. Derived += self.Source (AttributeBased).
            var gc = Controller(Set("S", Stat("Derived", 0f), Stat("Source", 50f)));
            var effect = Effect("GE_Derive", DurationPolicy.Infinite,
                new ModifierDefinition
                {
                    Attribute = "Derived", Operation = ModifierOp.Add,
                    Magnitude = new MagnitudeDefinition { Type = MagnitudeType.AttributeBased, BackingAttribute = "Source", Coefficient = 1f },
                });
            gc.ApplyEffect(effect); // no external source → reads self.Source
            Assert.That(gc.GetCurrentValue("Derived"), Is.EqualTo(50f).Within(1e-4f), "Derived = 0 + Source 50");

            // Change the (later-ordered) Source base, then a SINGLE recalc: Derived must resolve to the new value.
            gc.FindAttribute("Source").BaseValue = 100f;
            gc.RecalculateAttributes();
            Assert.That(gc.GetCurrentValue("Derived"), Is.EqualTo(100f).Within(1e-4f),
                "one recalc resolves the dependency on the later-ordered Source (fixed-point) — not the stale 50 a single forward pass would leave");
        }

        // ===================== F-PROGRESS-1 — ScalableFloat level curve (named-table seam, §9.4.2) =====================
        [Test]
        public void ScalableFloat_LevelCurve_ScalesByAppliedLevel()
        {
            // A registered curve table (an engine seam): level 1 → ×1, level 10 → ×3, linear between.
            System.Func<float, float> powerCurve = lvl => Mathf.Lerp(1f, 3f, Mathf.InverseLerp(1f, 10f, lvl));

            var lo = Controller(Set("S", Stat("Power", 0f))); lo.RegisterCurve("PowerCurve", powerCurve);
            lo.ApplyEffect(CurvedBoon("GE_L1"), 1);
            Assert.That(lo.GetBaseValue("Power"), Is.EqualTo(10f).Within(1e-4f), "level 1: 10 × curve(1)=1 = 10");

            var hi = Controller(Set("S", Stat("Power", 0f))); hi.RegisterCurve("PowerCurve", powerCurve);
            hi.ApplyEffect(CurvedBoon("GE_L10"), 10);
            Assert.That(hi.GetBaseValue("Power"), Is.EqualTo(30f).Within(1e-4f), "level 10: 10 × curve(10)=3 = 30 (curve scales by level; was flat 10 before F-PROGRESS-1)");

            var mid = Controller(Set("S", Stat("Power", 0f))); mid.RegisterCurve("PowerCurve", powerCurve);
            mid.ApplyEffect(CurvedBoon("GE_L5"), 5);
            Assert.That(mid.GetBaseValue("Power"), Is.EqualTo(18.8889f).Within(1e-3f), "level 5 interpolates ≈ 18.89");

            // No curve registered → flat Value (backward compatible; the seam is unwired).
            var flat = Controller(Set("S", Stat("Power", 0f)));
            flat.ApplyEffect(CurvedBoon("GE_Flat"), 10);
            Assert.That(flat.GetBaseValue("Power"), Is.EqualTo(10f).Within(1e-4f), "no curve registered → flat Value 10");
        }

        private GameplayEffectDefinition CurvedBoon(string name) =>
            Effect(name, DurationPolicy.Instant,
                new ModifierDefinition { Attribute = "Power", Operation = ModifierOp.Add, Magnitude = MagnitudeDefinition.ScalableCurved(10f, "PowerCurve", null) });
    }
}
