using Jbltx.Ugas.Definitions;
using Jbltx.Ugas.Editor;
using Jbltx.Ugas.Editor.Yaml;
using Jbltx.Ugas.Kernel;
using NUnit.Framework;
using UnityEngine;

namespace Jbltx.Ugas.Tests.Editor
{
    /// <summary>
    /// Conformance for the spec-pack importer: real UGAS genre entities parse and map into the
    /// runtime ScriptableObject definitions (SPEC §5–§9). Exercises <see cref="SpecEntityMapper"/>
    /// directly (the importer's core) so no AssetDatabase round-trip is required.
    /// </summary>
    [TestFixture]
    public class SpecImportTests
    {
        private static YamlMapping Parse(string fileName) =>
            (YamlMapping)YamlParser.Parse(SpecData.Read(fileName));

        [Test]
        public void Detects_AllEntityKinds()
        {
            Assert.That(SpecEntityMapper.Detect(Parse("rpg_attribute_set.yaml.txt")), Is.EqualTo(SpecEntityKind.AttributeSet));
            Assert.That(SpecEntityMapper.Detect(Parse("rpg_effect_regeneration.yaml.txt")), Is.EqualTo(SpecEntityKind.GameplayEffect));
            Assert.That(SpecEntityMapper.Detect(Parse("rpg_ability_whirlwind.yaml.txt")), Is.EqualTo(SpecEntityKind.GameplayAbility));
            Assert.That(SpecEntityMapper.Detect(Parse("rpg_tag_registry.yaml.txt")), Is.EqualTo(SpecEntityKind.GameplayTagRegistry));
            Assert.That(SpecEntityMapper.Detect(Parse("rpg_gameplay_controller.yaml.txt")), Is.EqualTo(SpecEntityKind.GameplayController));
        }

        [Test]
        public void ImportsAttributeSet_Populated()
        {
            var so = ScriptableObject.CreateInstance<AttributeSetDefinition>();
            SpecEntityMapper.PopulateAttributeSet(so, Parse("rpg_attribute_set.yaml.txt"));

            Assert.That(so.SetName, Is.EqualTo("RPGCoreAttributes"));
            Assert.That(so.Attributes.Count, Is.EqualTo(14));

            AttributeDefinition health = default;
            foreach (var a in so.Attributes) if (a.Name == "Health") health = a;
            Assert.That(health.Category, Is.EqualTo(AttributeCategory.Resource));
            Assert.That(health.Min.Enabled && !health.Min.IsReference && health.Min.Literal == 0f, Is.True);
            Assert.That(health.Max.Enabled && health.Max.IsReference && health.Max.AttributeReference == "MaxHealth", Is.True);
        }

        [Test]
        public void ImportsEffect_WithChannelAndAttributeBasedMagnitude()
        {
            var so = ScriptableObject.CreateInstance<GameplayEffectDefinition>();
            SpecEntityMapper.PopulateEffect(so, Parse("rpg_effect_mainstat_strength.yaml.txt"));

            Assert.That(so.EffectName, Is.EqualTo("GE_MainStat_Strength"));
            Assert.That(so.DurationPolicy, Is.EqualTo(DurationPolicy.Infinite));
            Assert.That(so.Modifiers.Count, Is.EqualTo(1));
            var mod = so.Modifiers[0];
            Assert.That(mod.Attribute, Is.EqualTo("WeaponDamage"));
            Assert.That(mod.Operation, Is.EqualTo(ModifierOp.Multiply));
            Assert.That(mod.Channel, Is.EqualTo("MainStat"));
            Assert.That(mod.Magnitude.Type, Is.EqualTo(MagnitudeType.AttributeBased));
            Assert.That(mod.Magnitude.BackingAttribute, Is.EqualTo("Strength"));
            Assert.That(mod.Magnitude.Coefficient, Is.EqualTo(0.01f));
        }

        [Test]
        public void ImportsEffect_HasDurationWithPeriod()
        {
            var so = ScriptableObject.CreateInstance<GameplayEffectDefinition>();
            SpecEntityMapper.PopulateEffect(so, Parse("rpg_effect_regeneration.yaml.txt"));

            Assert.That(so.DurationPolicy, Is.EqualTo(DurationPolicy.HasDuration));
            Assert.That(so.Duration.Value, Is.EqualTo(5f));
            Assert.That(so.IsPeriodic, Is.True);
            Assert.That(so.Period.Period, Is.EqualTo(1f));
            Assert.That(so.GrantedTags, Does.Contain("State.Buff.Regenerating"));
        }

        [Test]
        public void ImportsAbility_WithTagsAndTasks()
        {
            var so = ScriptableObject.CreateInstance<GameplayAbilityDefinition>();
            SpecEntityMapper.PopulateAbility(so, Parse("rpg_ability_whirlwind.yaml.txt"));

            Assert.That(so.AbilityName, Is.EqualTo("GA_Whirlwind"));
            Assert.That(so.CostRef, Is.EqualTo("GE_WhirlwindCost"));
            Assert.That(so.CooldownRef, Is.EqualTo("GE_WhirlwindCooldown"));
            Assert.That(so.Tags.ActivationRequiredTags, Does.Contain("State.Combat"));
            Assert.That(so.Tags.ActivationBlockedTags, Does.Contain("State.Debuff.Stunned"));
            Assert.That(so.Tasks.Count, Is.EqualTo(3));
            Assert.That(so.Tasks[0].Type, Is.EqualTo("PlayMontage"));
        }

        [Test]
        public void ImportsTagRegistry_AndBuildsRuntime()
        {
            var so = ScriptableObject.CreateInstance<GameplayTagRegistry>();
            SpecEntityMapper.PopulateTagRegistry(so, Parse("rpg_tag_registry.yaml.txt"));

            Assert.That(so.Tags.Count, Is.GreaterThan(0));
            var runtime = so.BuildRuntime();
            var fire = runtime.Find("DamageType.Fire");
            Assert.That(fire.IsValid, Is.True);
            // Ancestor interning: DamageType is reachable as an ancestor of DamageType.Fire.
            Assert.That(runtime.Find("DamageType").IsValid, Is.True);
        }

        [Test]
        public void ImportsControllerConfig_CapturesStartingValues()
        {
            var so = ScriptableObject.CreateInstance<GameplayControllerConfig>();
            SpecEntityMapper.PopulateController(so, Parse("rpg_gameplay_controller.yaml.txt"));

            Assert.That(so.StartingTags, Does.Contain("Class.Barbarian"));
            Assert.That(so.Replication, Is.EqualTo(ControllerReplication.Mixed));

            float strength = 0f;
            foreach (var sv in so.StartingAttributeValues)
                if (sv.AttributeName == "Strength") strength = sv.BaseValue;
            Assert.That(strength, Is.EqualTo(50f));
        }
    }
}
