using System.Collections.Generic;
using Jbltx.Ugas.Definitions;
using Jbltx.Ugas.Editor;
using Jbltx.Ugas.Kernel;
using Jbltx.Ugas.Persistence;
using Jbltx.Ugas.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Jbltx.Ugas.Tests.Editor
{
    /// <summary>
    /// Conformance for GC state persistence (SPEC §14): a snapshot captures attribute base values +
    /// active-effect timers + granted abilities, and restoring onto a fresh controller re-derives the
    /// same current values — with durational effects resuming from their saved remaining time.
    /// </summary>
    [TestFixture]
    public class StatePersistenceTests
    {
        private readonly List<Object> _spawned = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        private AttributeSetDefinition RpgSet()
        {
            var so = ScriptableObject.CreateInstance<AttributeSetDefinition>();
            _spawned.Add(so);
            SpecEntityMapper.PopulateAttributeSet(so,
                (Jbltx.Ugas.Editor.Yaml.YamlMapping)Jbltx.Ugas.Editor.Yaml.YamlParser.Parse(SpecData.Read("rpg_attribute_set.yaml.txt")));
            return so;
        }

        private UgasController Combatant(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            var gc = go.AddComponent<UgasController>();
            gc.RegisterAttributeSet(new RuntimeAttributeSet(RpgSet()));
            gc.FindAttribute("MaxHealth").BaseValue = 100f;
            gc.FindAttribute("Health").BaseValue = 100f;
            gc.RecalculateAttributes();
            return gc;
        }

        private GameplayEffectDefinition Buff(string attribute, float amount, DurationPolicy policy, float duration = 0f)
        {
            var e = ScriptableObject.CreateInstance<GameplayEffectDefinition>();
            _spawned.Add(e);
            var dur = policy == DurationPolicy.HasDuration ? MagnitudeDefinition.Scalable(duration) : default;
            e.Populate("GE_Buff", policy, dur, default, ExecutionPolicy.RunInParallel, 0,
                new List<ModifierDefinition> { new ModifierDefinition { Attribute = attribute, Operation = ModifierOp.Add, Magnitude = MagnitudeDefinition.Scalable(amount) } },
                null, null, null);
            return e;
        }

        private GameplayAbilityDefinition Ability(string name)
        {
            var so = ScriptableObject.CreateInstance<GameplayAbilityDefinition>();
            _spawned.Add(so);
            so.Populate(name, default, new List<AbilityTaskDefinition>(), null, null);
            return so;
        }

        [Test]
        public void Snapshot_RoundTripsAttributes_ActiveEffects_AndAbilities()
        {
            var a = Combatant("A");
            a.FindAttribute("Health").BaseValue = 70f;
            a.RecalculateAttributes();

            a.ApplyEffect(Buff("MaxHealth", 50f, DurationPolicy.Infinite));       // +50 → 150 (persists)
            a.ApplyEffect(Buff("MaxHealth", 20f, DurationPolicy.HasDuration, 5f)); // +20 → 170 for 5s
            a.Tick(2f);                                                            // surge now has 3s left
            a.GrantAbility(Ability("GA_Dash"));
            a.RecalculateAttributes();

            Assert.That(a.GetCurrentValue("MaxHealth"), Is.EqualTo(170f).Within(1e-4f));

            var snapshot = a.CaptureSnapshot();

            // Restore onto a fresh controller with the same attribute sets.
            var b = Combatant("B");
            b.RestoreSnapshot(snapshot);

            Assert.That(b.GetBaseValue("Health"), Is.EqualTo(70f).Within(1e-4f), "base value restored");
            Assert.That(b.GetCurrentValue("MaxHealth"), Is.EqualTo(170f).Within(1e-4f), "both active buffs restored");
            Assert.That(b.GetAbility("GA_Dash"), Is.Not.Null, "granted ability restored");

            // The durational buff resumed with 3s remaining → expires; the infinite one persists.
            b.Tick(3.1f);
            Assert.That(b.GetCurrentValue("MaxHealth"), Is.EqualTo(150f).Within(1e-4f), "durational expired from its resumed timer, infinite persists");
        }
    }
}
