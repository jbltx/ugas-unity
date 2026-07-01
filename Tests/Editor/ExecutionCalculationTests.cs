using System.Collections.Generic;
using Jbltx.Ugas.Definitions;
using Jbltx.Ugas.Editor;
using Jbltx.Ugas.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Jbltx.Ugas.Tests.Editor
{
    /// <summary>
    /// Conformance for the §9.6 execution-calculation seam: an effect's <c>ExecutionClass</c> runs a
    /// registered <see cref="IExecutionCalculation"/> with source + target attributes and a
    /// deterministic RNG (§9 context.RNG), applying stateful/branching math a static modifier can't.
    /// </summary>
    [TestFixture]
    public class ExecutionCalculationTests
    {
        private readonly List<Object> _spawned = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        // damage = max(source.WeaponDamage - target.WeaponDamage, 0) — mitigation, no RNG.
        private sealed class MitigationExecution : IExecutionCalculation
        {
            public void Execute(ExecutionContext ctx)
            {
                float dmg = Mathf.Max(ctx.SourceAttribute("WeaponDamage") - ctx.TargetAttribute("WeaponDamage"), 0f);
                ctx.AddToTarget("Health", -dmg);
            }
        }

        // damage = a deterministic roll in [10, 20] from context.RNG.
        private sealed class RandomDamageExecution : IExecutionCalculation
        {
            public void Execute(ExecutionContext ctx) => ctx.AddToTarget("Health", -ctx.Rng.NextInt(10, 21));
        }

        private AttributeSetDefinition RpgSet()
        {
            var so = ScriptableObject.CreateInstance<AttributeSetDefinition>();
            _spawned.Add(so);
            SpecEntityMapper.PopulateAttributeSet(so,
                (Jbltx.Ugas.Editor.Yaml.YamlMapping)Jbltx.Ugas.Editor.Yaml.YamlParser.Parse(SpecData.Read("rpg_attribute_set.yaml.txt")));
            return so;
        }

        private UgasController Combatant(string name, float maxHealth = 100f)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            var gc = go.AddComponent<UgasController>();
            gc.RegisterAttributeSet(new RuntimeAttributeSet(RpgSet()));
            gc.FindAttribute("MaxHealth").BaseValue = maxHealth;
            gc.FindAttribute("Health").BaseValue = maxHealth;
            gc.RecalculateAttributes();
            return gc;
        }

        private GameplayEffectDefinition ExecEffect(string executionClass)
        {
            var e = ScriptableObject.CreateInstance<GameplayEffectDefinition>();
            _spawned.Add(e);
            e.Populate("GE_Exec", DurationPolicy.Instant, default, default, ExecutionPolicy.RunInParallel, 0,
                new List<ModifierDefinition>(), null, null, null);
            e.SetExecutionClass(executionClass);
            return e;
        }

        [Test]
        public void Execution_ReadsSourceAndTarget_AppliesMitigatedDamage()
        {
            var attacker = Combatant("Attacker");
            attacker.FindAttribute("WeaponDamage").BaseValue = 18f;
            attacker.RecalculateAttributes();

            var target = Combatant("Target");
            target.FindAttribute("WeaponDamage").BaseValue = 5f;
            target.RecalculateAttributes();
            target.RegisterExecution("ExecCalc_Mitigation", new MitigationExecution());

            target.ApplyEffect(ExecEffect("ExecCalc_Mitigation"), 1, attacker);

            Assert.That(target.GetBaseValue("Health"), Is.EqualTo(87f).Within(1e-4f), "100 - max(18 - 5, 0)");
        }

        [Test]
        public void Execution_Rng_IsDeterministicGivenSeed()
        {
            var caster = Combatant("Caster");

            var a = Combatant("A");
            a.RandomSeed = 42;
            a.RegisterExecution("ExecCalc_Rand", new RandomDamageExecution());

            var b = Combatant("B");
            b.RandomSeed = 42;
            b.RegisterExecution("ExecCalc_Rand", new RandomDamageExecution());

            a.ApplyEffect(ExecEffect("ExecCalc_Rand"), 1, caster);
            b.ApplyEffect(ExecEffect("ExecCalc_Rand"), 1, caster);

            Assert.That(a.GetBaseValue("Health"), Is.EqualTo(b.GetBaseValue("Health")), "same seed → identical roll");
            Assert.That(a.GetBaseValue("Health"), Is.InRange(80f, 90f), "100 - roll in [10,20]");
        }
    }
}
