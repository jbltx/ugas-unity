using Jbltx.Ugas.Abilities;
using Jbltx.Ugas.Attributes;
using Jbltx.Ugas.Effects;
using Jbltx.Ugas.Persistence;
using Jbltx.Ugas.Schema;
using NUnit.Framework;

namespace Jbltx.Ugas.Tests
{
    /// <summary>
    /// Conformance scaffold for the Effects and Abilities pillars (SPEC §8, §9) and §14 persistence.
    /// Implemented behaviour is asserted directly; behaviour that depends on still-stubbed internals
    /// is captured as <c>[Ignore]</c> placeholders so the harness compiles and documents intent.
    /// </summary>
    [TestFixture]
    public class EffectsAndAbilitiesTests
    {
        private GameplayController NewRpgController(out UgasContentRegistry content)
        {
            var setDef = SchemaLoader.AttributeSetFromYaml(TestData.Read("rpg_attribute_set.yaml"));
            content = new UgasContentRegistry();
            content.Add(setDef);
            content.Add(SchemaLoader.EffectFromYaml(TestData.Read("rpg_effect_mainstat_strength.yaml")));
            content.Add(SchemaLoader.EffectFromYaml(TestData.Read("rpg_effect_weapon_firesword.yaml")));
            content.Add(SchemaLoader.EffectFromYaml(TestData.Read("rpg_effect_regeneration.yaml")));
            content.Add(SchemaLoader.AbilityFromYaml(TestData.Read("rpg_ability_whirlwind.yaml")));

            var gc = new GameplayController { OwnerActor = new ActorReference { ActorID = "Hero" } };
            gc.RegisterAttributeSet(new AttributeSet(setDef));
            return gc;
        }

        // ---- Effects: implemented behaviour ----

        [Test]
        public void InstantEffect_ModifiesBaseValue()
        {
            var gc = NewRpgController(out _);
            float before = gc.FindAttribute("Health").BaseValue;

            var damage = SchemaLoader.EffectFromYaml(TestData.Read("rpg_effect_basic_attack_damage.yaml"));
            // Backing attribute WeaponDamage defaults to 10; coefficient -1 => -10 Health.
            gc.ApplyEffect(damage);

            Assert.That(gc.FindAttribute("Health").BaseValue, Is.EqualTo(before - 10f).Within(0.0001f));
        }

        [Test]
        public void InfiniteEffect_BecomesActive_AndGrantsTags()
        {
            var gc = NewRpgController(out var content);
            var fireSword = content.GetEffect("GE_Weapon_FireSword");

            var active = gc.ApplyEffect(fireSword);

            Assert.That(active, Is.Not.Null);
            Assert.That(gc.Effects.ActiveEffects, Has.Count.EqualTo(1));
            Assert.That(active.IsInfinite, Is.True);
            Assert.That(gc.OwnedTags.HasTag("Item.Type.Sword"), Is.True);
            Assert.That(gc.OwnedTags.HasTag("DamageType.Fire"), Is.True);
        }

        [Test]
        public void HasDurationEffect_ExpiresAfterDuration()
        {
            var gc = NewRpgController(out var content);
            var regen = content.GetEffect("GE_Regeneration"); // 5s duration

            gc.ApplyEffect(regen);
            Assert.That(gc.Effects.ActiveEffects, Has.Count.EqualTo(1));
            Assert.That(gc.OwnedTags.HasTag("State.Buff.Regenerating"), Is.True);

            gc.Tick(6f); // advance past the 5s duration

            Assert.That(gc.Effects.ActiveEffects, Is.Empty, "effect expired");
            Assert.That(gc.OwnedTags.HasTag("State.Buff.Regenerating"), Is.False, "granted tag removed on expiry");
        }

        [Test]
        public void PeriodicEffect_ExecutesEachPeriod()
        {
            var gc = NewRpgController(out var content);
            var regen = content.GetEffect("GE_Regeneration"); // +5 Health every 1s for 5s
            float startHealth = gc.FindAttribute("Health").BaseValue;

            gc.ApplyEffect(regen);
            gc.Tick(3.5f); // expect 3 periodic executions at t=1,2,3

            float healed = gc.FindAttribute("Health").BaseValue - startHealth;
            Assert.That(healed, Is.EqualTo(15f).Within(0.0001f), "3 ticks x +5 Health");
        }

        [Test]
        [Ignore("pending Effects pillar: RunInSequence / RunInMerge execution-policy scheduling (jbltx/ugas-unity Effects issue)")]
        public void ExecutionPolicy_RunInSequence_QueuesInstances()
        {
            // TODO: when RunInSequence is implemented, applying the same effect twice should queue
            // the second instance and activate it only when the first expires.
        }

        [Test]
        [Ignore("pending Effects pillar: RunInMerge execution-policy scheduling")]
        public void ExecutionPolicy_RunInMerge_ExtendsDuration()
        {
            // TODO: RunInMerge should fold concurrent applications into one instance whose duration
            // spans earliest-start to latest-end.
        }

        // ---- Abilities: implemented lifecycle ----

        [Test]
        public void Ability_StartsGranted_AfterGrant()
        {
            var gc = NewRpgController(out var content);
            var ability = (GameplayAbility)gc.GrantAbility(content.GetAbility("GA_Whirlwind"), level: 5);
            Assert.That(ability.State, Is.EqualTo(AbilityState.Granted));
        }

        [Test]
        public void Ability_Activation_BlockedByBlockedTag()
        {
            var gc = NewRpgController(out var content);
            gc.GrantAbility(content.GetAbility("GA_Whirlwind"), level: 5);

            // Whirlwind requires State.Combat and is blocked by State.Debuff.Stunned.
            gc.OwnedTags.AddTag("State.Debuff.Stunned");
            Assert.That(gc.TryActivateAbility("GA_Whirlwind"), Is.False, "blocked tag prevents activation");
        }

        [Test]
        public void Ability_Activation_Succeeds_WhenRequirementsMet_AndGrantsOwnedTags()
        {
            var gc = NewRpgController(out var content);
            var ability = (GameplayAbility)gc.GrantAbility(content.GetAbility("GA_Whirlwind"), level: 5);

            gc.OwnedTags.AddTag("State.Combat"); // ActivationRequiredTags
            bool activated = gc.TryActivateAbility("GA_Whirlwind");

            Assert.That(activated, Is.True);
            Assert.That(ability.State, Is.EqualTo(AbilityState.Active));
            // ActivationOwnedTags (State.Combat) granted on commit.
            Assert.That(gc.OwnedTags.HasTag("State.Combat"), Is.True);

            ability.EndAbility();
            Assert.That(ability.State, Is.EqualTo(AbilityState.Granted), "returns to Granted after End");
        }

        [Test]
        [Ignore("pending Abilities pillar: ability-task execution (PlayMontage / WaitGameplayEvent / Apply*) per SPEC §10")]
        public void Ability_RunsTasks_OnActivation()
        {
            // TODO: when the task scheduler exists, activating GA_Whirlwind should run PlayMontage ->
            // WaitGameplayEvent -> ApplyEffectToActorsInRadius and apply GE_BasicAttackDamage.
        }

        // ---- Persistence (SPEC §14) ----

        [Test]
        public void Persistence_CaptureThenHydrate_RestoresState()
        {
            var gc = NewRpgController(out var content);
            gc.FindAttribute("Strength").BaseValue = 50f;
            gc.ApplyEffect(content.GetEffect("GE_MainStat_Strength"));
            gc.ApplyEffect(content.GetEffect("GE_Weapon_FireSword"));
            gc.RecalculateAttributes();

            float expectedWd = gc.FindAttribute("WeaponDamage").CurrentValue; // 18.0

            // Capture -> hydrate via the §14 restoration order.
            var snapshot = GameplayControllerPersistence.Capture(gc);
            var restored = GameplayControllerPersistence.Hydrate(snapshot, content);

            Assert.That(restored.FindAttribute("Strength").BaseValue, Is.EqualTo(50f));
            Assert.That(restored.Effects.ActiveEffects, Has.Count.EqualTo(2), "active effects re-applied");
            Assert.That(restored.FindAttribute("WeaponDamage").CurrentValue,
                Is.EqualTo(expectedWd).Within(0.0001f), "current value recomputed from restored modifiers");
            Assert.That(restored.OwnedTags.HasTag("DamageType.Fire"), Is.True, "effect-granted tags reconstructed");
        }
    }
}
