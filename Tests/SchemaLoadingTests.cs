using Jbltx.Ugas.Schema;
using NUnit.Framework;

namespace Jbltx.Ugas.Tests
{
    /// <summary>
    /// Conformance: the schema-loading layer deserializes the engine-agnostic core schemas from the
    /// genre-pack entity files into populated model objects (SPEC Appendix B + §4–9).
    /// </summary>
    [TestFixture]
    public class SchemaLoadingTests
    {
        [Test]
        public void LoadsRpgAttributeSet_Populated()
        {
            var set = SchemaLoader.AttributeSetFromYaml(TestData.Read("rpg_attribute_set.yaml"));

            Assert.That(set.Name, Is.EqualTo("RPGCoreAttributes"));
            Assert.That(set.Attributes, Has.Count.EqualTo(14));

            var health = set.Attributes.Find(a => a.Name == "Health");
            Assert.That(health, Is.Not.Null);
            Assert.That(health.Category, Is.EqualTo(AttributeCategory.Resource));
            Assert.That(health.Clamping, Is.Not.Null);
            Assert.That(health.Clamping.Min.IsLiteral, Is.True);
            Assert.That(health.Clamping.Min.Literal, Is.EqualTo(0f));
            // Max is an attribute reference (Health clamps to MaxHealth), not a literal.
            Assert.That(health.Clamping.Max.IsLiteral, Is.False);
            Assert.That(health.Clamping.Max.AttributeReference, Is.EqualTo("MaxHealth"));

            var level = set.Attributes.Find(a => a.Name == "Level");
            Assert.That(level.Category, Is.EqualTo(AttributeCategory.Meta));
        }

        [Test]
        public void LoadsInfiniteEffect_WithChannelAndAttributeBasedMagnitude()
        {
            var effect = SchemaLoader.EffectFromYaml(TestData.Read("rpg_effect_mainstat_strength.yaml"));

            Assert.That(effect.Name, Is.EqualTo("GE_MainStat_Strength"));
            Assert.That(effect.DurationPolicy, Is.EqualTo(DurationPolicy.Infinite));
            Assert.That(effect.Modifiers, Has.Count.EqualTo(1));

            var mod = effect.Modifiers[0];
            Assert.That(mod.Attribute, Is.EqualTo("WeaponDamage"));
            Assert.That(mod.Operation, Is.EqualTo(ModifierOperation.Multiply));
            Assert.That(mod.Channel, Is.EqualTo("MainStat"));
            Assert.That(mod.Magnitude.Type, Is.EqualTo(MagnitudeType.AttributeBased));
            Assert.That(mod.Magnitude.BackingAttribute, Is.EqualTo("Strength"));
            Assert.That(mod.Magnitude.Coefficient, Is.EqualTo(0.01f));
        }

        [Test]
        public void LoadsHasDurationEffect_WithPeriod()
        {
            var effect = SchemaLoader.EffectFromYaml(TestData.Read("rpg_effect_regeneration.yaml"));

            Assert.That(effect.DurationPolicy, Is.EqualTo(DurationPolicy.HasDuration));
            Assert.That(effect.Duration, Is.Not.Null);
            Assert.That(effect.Duration.Value, Is.EqualTo(5.0f));
            Assert.That(effect.Period, Is.Not.Null);
            Assert.That(effect.Period.Period, Is.EqualTo(1.0f));
            Assert.That(effect.Period.ExecuteOnApplication, Is.False);
            Assert.That(effect.GrantedTags, Does.Contain("State.Buff.Regenerating"));
        }

        [Test]
        public void LoadsAbility_WithTagsAndTasks()
        {
            var ability = SchemaLoader.AbilityFromYaml(TestData.Read("rpg_ability_whirlwind.yaml"));

            Assert.That(ability.Name, Is.EqualTo("GA_Whirlwind"));
            Assert.That(ability.Cost, Is.EqualTo("GE_WhirlwindCost"));
            Assert.That(ability.Cooldown, Is.EqualTo("GE_WhirlwindCooldown"));
            Assert.That(ability.Tags.ActivationRequiredTags, Does.Contain("State.Combat"));
            Assert.That(ability.Tags.ActivationBlockedTags, Does.Contain("State.Debuff.Stunned"));
            Assert.That(ability.Tasks, Has.Count.EqualTo(3));
            Assert.That(ability.Tasks[0].Type, Is.EqualTo("PlayMontage"));
            Assert.That(ability.Tasks[2].Type, Is.EqualTo("ApplyEffectToActorsInRadius"));
            Assert.That(ability.Tasks[2].Params.ContainsKey("Radius"), Is.True);
        }

        [Test]
        public void LoadsTagRegistry()
        {
            var registry = SchemaLoader.TagRegistryFromYaml(TestData.Read("rpg_tag_registry.yaml"));

            Assert.That(registry.Tags, Is.Not.Empty);
            var physical = registry.Tags.Find(t => t.Tag == "DamageType.Physical");
            Assert.That(physical, Is.Not.Null);
            Assert.That(physical.AllowMultiple, Is.False);
        }

        [Test]
        public void LoadsGameplayController_TolerantOfDurationShorthand()
        {
            var gc = SchemaLoader.ControllerFromYaml(TestData.Read("rpg_gameplay_controller.yaml"));

            Assert.That(gc.OwnerActor.ActorID, Is.EqualTo("Hero_Barbarian_01"));
            Assert.That(gc.AttributeSets, Has.Count.EqualTo(1));
            Assert.That(gc.GrantedAbilities, Has.Count.EqualTo(2));
            Assert.That(gc.ActiveEffects, Has.Count.EqualTo(2));
            // The pack uses `Duration: -1` shorthand for an Infinite active effect.
            Assert.That(gc.ActiveEffects[0].DurationPolicy, Is.EqualTo(DurationPolicy.Infinite));
            Assert.That(gc.OwnedTags, Does.Contain("Class.Barbarian"));
            Assert.That(gc.ReplicationMode, Is.EqualTo(GCReplicationMode.Mixed));
        }

        [Test]
        public void LoadsMultipleGenrePacks()
        {
            // A second genre's attribute set loads with the same loader (engine-agnostic model).
            var puzzle = SchemaLoader.AttributeSetFromYaml(TestData.Read("puzzle_attribute_set.yaml"));
            Assert.That(puzzle.Attributes, Is.Not.Empty);

            var racing = SchemaLoader.EffectFromYaml(TestData.Read("racing_effect_nitro_boost.yaml"));
            Assert.That(racing.DurationPolicy, Is.EqualTo(DurationPolicy.HasDuration));
            Assert.That(racing.Modifiers, Has.Count.EqualTo(2));
        }
    }
}
