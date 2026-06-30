using System.Collections.Generic;
using Jbltx.Ugas.Abilities;
using Jbltx.Ugas.Definitions;
using Jbltx.Ugas.Editor;
using Jbltx.Ugas.Kernel;
using Jbltx.Ugas.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Jbltx.Ugas.Tests.Editor
{
    /// <summary>
    /// Integration conformance for the Unity-native <see cref="UgasController"/>: SO definitions →
    /// runtime → §5 aggregation / §8 abilities / §9 effects. Verifies the full managed path,
    /// including the worked Barbarian WeaponDamage = 18.0 driven through the controller and real
    /// effect SOs.
    /// </summary>
    [TestFixture]
    public class UgasControllerTests
    {
        private readonly List<Object> _spawned = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        private UgasController NewController()
        {
            var go = new GameObject("UGAS Test Controller");
            _spawned.Add(go);
            var gc = go.AddComponent<UgasController>(); // Awake builds an empty tag registry + effects system
            return gc;
        }

        private T NewSo<T>() where T : ScriptableObject
        {
            var so = ScriptableObject.CreateInstance<T>();
            _spawned.Add(so);
            return so;
        }

        private AttributeSetDefinition RpgSet()
        {
            var so = NewSo<AttributeSetDefinition>();
            SpecEntityMapper.PopulateAttributeSet(so,
                (Jbltx.Ugas.Editor.Yaml.YamlMapping)Jbltx.Ugas.Editor.Yaml.YamlParser.Parse(SpecData.Read("rpg_attribute_set.yaml.txt")));
            return so;
        }

        private GameplayEffectDefinition Effect(string file)
        {
            var so = NewSo<GameplayEffectDefinition>();
            SpecEntityMapper.PopulateEffect(so,
                (Jbltx.Ugas.Editor.Yaml.YamlMapping)Jbltx.Ugas.Editor.Yaml.YamlParser.Parse(SpecData.Read(file)));
            return so;
        }

        [Test]
        public void WorkedExample_WeaponDamageIs18_ThroughController()
        {
            var gc = NewController();
            gc.RegisterAttributeSet(new RuntimeAttributeSet(RpgSet()));
            gc.FindAttribute("Strength").BaseValue = 50f;

            gc.ApplyEffect(Effect("rpg_effect_mainstat_strength.yaml.txt"));  // x(1 + 0.01*50) MainStat
            gc.ApplyEffect(Effect("rpg_effect_weapon_firesword.yaml.txt"));   // x(1 + 0.20) DamageBonuses
            gc.RecalculateAttributes();

            Assert.That(gc.GetCurrentValue("WeaponDamage"), Is.EqualTo(18f).Within(1e-4f));
        }

        [Test]
        public void Clamp_ResolvesAttributeReference_HealthToMaxHealth()
        {
            var gc = NewController();
            gc.RegisterAttributeSet(new RuntimeAttributeSet(RpgSet()));
            gc.FindAttribute("MaxHealth").BaseValue = 200f;
            gc.FindAttribute("Health").BaseValue = 999f;
            gc.RecalculateAttributes();
            Assert.That(gc.GetCurrentValue("Health"), Is.EqualTo(200f));
        }

        [Test]
        public void InstantEffect_ModifiesBaseValue()
        {
            var gc = NewController();
            gc.RegisterAttributeSet(new RuntimeAttributeSet(RpgSet()));
            float before = gc.GetBaseValue("Health");
            gc.ApplyEffect(Effect("rpg_effect_basic_attack_damage.yaml.txt")); // -WeaponDamage(10) Health
            Assert.That(gc.GetBaseValue("Health"), Is.EqualTo(before - 10f).Within(1e-4f));
        }

        [Test]
        public void InfiniteEffect_BecomesActive_GrantsTags()
        {
            var gc = NewController();
            gc.RegisterAttributeSet(new RuntimeAttributeSet(RpgSet()));
            var active = gc.ApplyEffect(Effect("rpg_effect_weapon_firesword.yaml.txt"));
            Assert.That(active, Is.Not.Null);
            Assert.That(gc.Effects.ActiveEffects.Count, Is.EqualTo(1));
            Assert.That(active.IsInfinite, Is.True);
            Assert.That(gc.OwnedTags.HasTag("Item.Type.Sword"), Is.True);
            Assert.That(gc.OwnedTags.HasTag("DamageType.Fire"), Is.True);
        }

        [Test]
        public void HasDurationEffect_ExpiresAndRemovesTags()
        {
            var gc = NewController();
            gc.RegisterAttributeSet(new RuntimeAttributeSet(RpgSet()));
            gc.ApplyEffect(Effect("rpg_effect_regeneration.yaml.txt")); // 5s
            Assert.That(gc.OwnedTags.HasTag("State.Buff.Regenerating"), Is.True);
            gc.Tick(6f);
            Assert.That(gc.Effects.ActiveEffects.Count, Is.EqualTo(0));
            Assert.That(gc.OwnedTags.HasTag("State.Buff.Regenerating"), Is.False);
        }

        [Test]
        public void PeriodicEffect_HealsEachPeriod()
        {
            var gc = NewController();
            gc.RegisterAttributeSet(new RuntimeAttributeSet(RpgSet()));
            // Heal from a damaged state: base Health is now clamped to MaxHealth, so regenerating
            // while already at full would (correctly) gain nothing.
            gc.FindAttribute("MaxHealth").BaseValue = 100f;
            gc.FindAttribute("Health").BaseValue = 50f;
            gc.RecalculateAttributes();
            float start = gc.GetBaseValue("Health");
            gc.ApplyEffect(Effect("rpg_effect_regeneration.yaml.txt")); // +5 Health/s for 5s
            gc.Tick(3.5f); // ticks at t=1,2,3 => +15
            Assert.That(gc.GetBaseValue("Health") - start, Is.EqualTo(15f).Within(1e-4f));
        }

        [Test]
        public void Ability_Lifecycle_GrantActivateEnd()
        {
            var gc = NewController();
            gc.RegisterAttributeSet(new RuntimeAttributeSet(RpgSet()));

            var abilitySo = NewSo<GameplayAbilityDefinition>();
            SpecEntityMapper.PopulateAbility(abilitySo,
                (Jbltx.Ugas.Editor.Yaml.YamlMapping)Jbltx.Ugas.Editor.Yaml.YamlParser.Parse(SpecData.Read("rpg_ability_whirlwind.yaml.txt")));

            var ability = gc.GrantAbility(abilitySo, level: 5);
            Assert.That(ability.State, Is.EqualTo(AbilityState.Granted));

            // Blocked while stunned.
            gc.OwnedTags.AddTag("State.Debuff.Stunned");
            Assert.That(gc.TryActivateAbility("GA_Whirlwind"), Is.False);
            gc.OwnedTags.RemoveTag("State.Debuff.Stunned");

            // Requires State.Combat.
            gc.OwnedTags.AddTag("State.Combat");
            Assert.That(gc.TryActivateAbility("GA_Whirlwind"), Is.True);
            Assert.That(ability.State, Is.EqualTo(AbilityState.Active));

            ability.EndAbility();
            Assert.That(ability.State, Is.EqualTo(AbilityState.Granted));
        }

        [Test]
        public void Backend_ReflectsWhetherDotsIsCompiledIn()
        {
            // UgasBackend reports the compiled-in aggregation backend: DOTS when com.unity.entities is
            // installed (UGAS_DOTS), otherwise the managed path. This assembly mirrors the Runtime's
            // com.unity.entities -> UGAS_DOTS versionDefine so the expectation matches the runtime
            // value in both project configurations (with and without Entities).
#if UGAS_DOTS
            Assert.That(UgasBackend.Active, Is.EqualTo(UgasBackendKind.Dots));
            Assert.That(UgasBackend.DotsAvailable, Is.True);
#else
            Assert.That(UgasBackend.Active, Is.EqualTo(UgasBackendKind.Managed));
            Assert.That(UgasBackend.DotsAvailable, Is.False);
#endif
        }
    }
}
