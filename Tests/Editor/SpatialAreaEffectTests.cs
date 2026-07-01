using System.Collections.Generic;
using System.Linq;
using Jbltx.Ugas.Definitions;
using Jbltx.Ugas.Editor;
using Jbltx.Ugas.Kernel;
using Jbltx.Ugas.Runtime;
using Jbltx.Ugas.Spatial;
using NUnit.Framework;
using UnityEngine;

namespace Jbltx.Ugas.Tests.Editor
{
    /// <summary>
    /// Conformance for area-of-effect application (SPEC §17.3) via
    /// <see cref="UgasController.ApplyAreaEffect"/>: the target set is resolved by a single §17.2 query
    /// (snapshot), each hit receives the effect through the normal §9 pipeline, <c>MaxTargets</c> caps
    /// after nearest-first ordering, and an AttributeBased radius scales with the caster's stat.
    /// </summary>
    [TestFixture]
    public class SpatialAreaEffectTests
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

        // A combatant at pos with Health = MaxHealth = maxHealth.
        private UgasController Combatant(string name, Vector3 pos, float maxHealth = 100f)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            go.transform.position = pos;
            var gc = go.AddComponent<UgasController>();
            gc.RegisterAttributeSet(new RuntimeAttributeSet(RpgSet()));
            gc.FindAttribute("MaxHealth").BaseValue = maxHealth;
            gc.FindAttribute("Health").BaseValue = maxHealth;
            gc.RecalculateAttributes();
            return gc;
        }

        // An instant effect that adds `amount` to Health, applied over a sphere `radius` (optionally capped).
        private GameplayEffectDefinition AoeHealthDelta(float amount, MagnitudeDefinition radius, int maxTargets = 0)
        {
            var e = ScriptableObject.CreateInstance<GameplayEffectDefinition>();
            _spawned.Add(e);
            e.Populate("AoE Health Delta", DurationPolicy.Instant, default, default, ExecutionPolicy.RunInParallel, 0,
                new List<ModifierDefinition>
                {
                    new ModifierDefinition { Attribute = "Health", Operation = ModifierOp.Add, Magnitude = MagnitudeDefinition.Scalable(amount) },
                },
                null, null, null);
            e.SetArea(new AreaDefinition { Enabled = true, Shape = AreaShape.Sphere, Radius = radius, MaxTargets = maxTargets });
            return e;
        }

        private static string[] Names(IEnumerable<UgasController> gcs) => gcs.Select(g => g.name).ToArray();

        [Test]
        public void ApplyAreaEffect_HitsInRadius_AppliesToEach()
        {
            var caster = Combatant("Caster", Vector3.zero);
            var a = Combatant("A", new Vector3(2, 0, 0));
            var b = Combatant("B", new Vector3(4, 0, 0));
            var c = Combatant("C", new Vector3(9, 0, 0)); // outside radius 5

            var provider = new SimpleSpatialProvider();
            provider.Register(a);
            provider.Register(b);
            provider.Register(c);

            var affected = caster.ApplyAreaEffect(AoeHealthDelta(-30f, MagnitudeDefinition.Scalable(5f)), Vector3.zero, provider);

            Assert.That(Names(affected), Is.EqualTo(new[] { "A", "B" }));
            Assert.That(a.GetBaseValue("Health"), Is.EqualTo(70f).Within(1e-4f));
            Assert.That(b.GetBaseValue("Health"), Is.EqualTo(70f).Within(1e-4f));
            Assert.That(c.GetBaseValue("Health"), Is.EqualTo(100f).Within(1e-4f)); // untouched
        }

        [Test]
        public void ApplyAreaEffect_MaxTargets_CapsNearestFirst()
        {
            var caster = Combatant("Caster", Vector3.zero);
            var a = Combatant("A", new Vector3(2, 0, 0));
            var b = Combatant("B", new Vector3(4, 0, 0));
            var c = Combatant("C", new Vector3(6, 0, 0));

            var provider = new SimpleSpatialProvider();
            provider.Register(a);
            provider.Register(b);
            provider.Register(c);

            // Radius covers all three, but MaxTargets = 2 keeps only the nearest two.
            var affected = caster.ApplyAreaEffect(AoeHealthDelta(-30f, MagnitudeDefinition.Scalable(100f), maxTargets: 2), Vector3.zero, provider);

            Assert.That(Names(affected), Is.EqualTo(new[] { "A", "B" }));
            Assert.That(c.GetBaseValue("Health"), Is.EqualTo(100f).Within(1e-4f)); // capped out
        }

        [Test]
        public void ApplyAreaEffect_AttributeBasedRadius_ScalesWithCasterStat()
        {
            var caster = Combatant("Caster", Vector3.zero);
            caster.FindAttribute("Strength").BaseValue = 5f; // radius := Strength
            caster.RecalculateAttributes();

            var near = Combatant("Near", new Vector3(4, 0, 0)); // within 5
            var far = Combatant("Far", new Vector3(6, 0, 0));   // outside 5

            var provider = new SimpleSpatialProvider();
            provider.Register(near);
            provider.Register(far);

            var radius = new MagnitudeDefinition { Type = MagnitudeType.AttributeBased, BackingAttribute = "Strength", Coefficient = 1f };
            var affected = caster.ApplyAreaEffect(AoeHealthDelta(-10f, radius), Vector3.zero, provider);

            Assert.That(Names(affected), Is.EqualTo(new[] { "Near" }));
            Assert.That(near.GetBaseValue("Health"), Is.EqualTo(90f).Within(1e-4f));
            Assert.That(far.GetBaseValue("Health"), Is.EqualTo(100f).Within(1e-4f));
        }
    }
}
