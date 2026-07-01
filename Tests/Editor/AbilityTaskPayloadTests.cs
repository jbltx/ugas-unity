using System.Collections.Generic;
using Jbltx.Ugas.Abilities;
using Jbltx.Ugas.Definitions;
using Jbltx.Ugas.Kernel;
using Jbltx.Ugas.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Jbltx.Ugas.Tests.Editor
{
    /// <summary>
    /// Regression coverage for the harness-eval findings: WaitDelay honors the pack-authored `Duration`
    /// param (F1), and the ApplyEffectToOwner / RemoveEffectFromOwner ability tasks actually run their
    /// payloads via <c>TryActivateAbility</c> (F2), so self-targeted verbs (gather, consume, dispel)
    /// work end-to-end.
    /// </summary>
    [TestFixture]
    public class AbilityTaskPayloadTests
    {
        private readonly List<Object> _spawned = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        private AttributeSetDefinition TestSet()
        {
            var so = ScriptableObject.CreateInstance<AttributeSetDefinition>();
            _spawned.Add(so);
            so.Populate("Test", null, new List<AttributeDefinition>
            {
                new AttributeDefinition { Name = "MaxHealth", DefaultBaseValue = 100f },
                new AttributeDefinition { Name = "Health", DefaultBaseValue = 100f, Max = AttributeBound.FromReference("MaxHealth"), Min = AttributeBound.FromLiteral(0f) },
                new AttributeDefinition { Name = "Materials", DefaultBaseValue = 0f },
            });
            return so;
        }

        private UgasController Owner()
        {
            var go = new GameObject("Owner");
            _spawned.Add(go);
            var gc = go.AddComponent<UgasController>();
            gc.RegisterAttributeSet(new RuntimeAttributeSet(TestSet()));
            return gc;
        }

        private GameplayEffectDefinition Effect(string name, string attribute, float amount, DurationPolicy policy)
        {
            var e = ScriptableObject.CreateInstance<GameplayEffectDefinition>();
            _spawned.Add(e);
            var dur = policy == DurationPolicy.HasDuration ? MagnitudeDefinition.Scalable(1f) : default;
            e.Populate(name, policy, dur, default, ExecutionPolicy.RunInParallel, 0,
                new List<ModifierDefinition> { new ModifierDefinition { Attribute = attribute, Operation = ModifierOp.Add, Magnitude = MagnitudeDefinition.Scalable(amount) } },
                null, null, null);
            return e;
        }

        private GameplayAbilityDefinition AbilityWithTask(string name, string taskType, string effectClass)
        {
            var so = ScriptableObject.CreateInstance<GameplayAbilityDefinition>();
            _spawned.Add(so);
            so.Populate(name, default, new List<AbilityTaskDefinition>
            {
                new AbilityTaskDefinition { Type = taskType, Params = new List<TaskParam> { new TaskParam { Key = "EffectClass", Value = effectClass } } },
            }, null, null);
            return so;
        }

        [Test]
        public void WaitDelay_AcceptsDurationParam()
        {
            var task = AbilityTaskFactory.Create(new AbilityTaskDefinition
            {
                Type = "WaitDelay",
                Params = new List<TaskParam> { new TaskParam { Key = "Duration", Value = "0.5" } },
            });
            task.Activate();

            task.Tick(0.3f);
            Assert.That(task.State, Is.EqualTo(AbilityTaskState.Active), "0.3 < 0.5 → still waiting (Duration was read, not collapsed to 0)");
            task.Tick(0.3f);
            Assert.That(task.State, Is.EqualTo(AbilityTaskState.Completed), "0.6 >= 0.5 → done");
        }

        [Test]
        public void ApplyEffectToOwner_RunsPayloadViaAbilityActivation()
        {
            var owner = Owner();
            owner.RegisterEffect(Effect("GE_AddMaterials", "Materials", 5f, DurationPolicy.Instant));
            owner.GrantAbility(AbilityWithTask("GA_Gather", "ApplyEffectToOwner", "GE_AddMaterials"));

            Assert.That(owner.TryActivateAbility("GA_Gather"), Is.True);
            owner.Tick(0.016f); // runs the ability's tasks
            Assert.That(owner.GetBaseValue("Materials"), Is.EqualTo(5f).Within(1e-4f), "gather payload added materials");
        }

        [Test]
        public void RemoveEffectFromOwner_RunsPayloadViaAbilityActivation()
        {
            var owner = Owner();
            owner.ApplyEffect(Effect("GE_Buff", "MaxHealth", 50f, DurationPolicy.Infinite));
            owner.RecalculateAttributes();
            Assert.That(owner.GetCurrentValue("MaxHealth"), Is.EqualTo(150f).Within(1e-4f), "buff active");

            owner.GrantAbility(AbilityWithTask("GA_Dispel", "RemoveEffectFromOwner", "GE_Buff"));
            Assert.That(owner.TryActivateAbility("GA_Dispel"), Is.True);
            owner.Tick(0.016f);
            Assert.That(owner.GetCurrentValue("MaxHealth"), Is.EqualTo(100f).Within(1e-4f), "dispel payload removed the buff");
        }
    }
}
