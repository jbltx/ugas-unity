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
    /// Regression coverage for the fighting-eval finding F1: the §8.6/§8.7 inter-ability relationships
    /// <c>CancelAbilitiesWithTags</c> and <c>BlockAbilitiesWithTags</c> are now ENFORCED by
    /// <see cref="UgasController.TryActivateAbility"/>. A fighter's combo cancel (a heavy cancels a light
    /// parked in its recovery) and its commitment lockout (a super blocks the player's own normals while
    /// active) both work. Before the fix these fields were parsed + imported but never read by the runtime,
    /// so both mechanics silently no-oped. Matching is by exact tag identity within the owner's single
    /// registry (sound handle equality, cf. §7 / F3).
    /// </summary>
    [TestFixture]
    public class AbilityRelationshipTests
    {
        private readonly List<Object> _spawned = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        private UgasController Owner()
        {
            var so = ScriptableObject.CreateInstance<AttributeSetDefinition>();
            _spawned.Add(so);
            so.Populate("Test", null, new List<AttributeDefinition>
            {
                new AttributeDefinition { Name = "Health", DefaultBaseValue = 100f },
            });
            var go = new GameObject("Owner");
            _spawned.Add(go);
            var gc = go.AddComponent<UgasController>();
            gc.RegisterAttributeSet(new RuntimeAttributeSet(so));
            return gc;
        }

        // An ability that parks in a long WaitDelay (stays Active), carrying §8 relationship tags.
        private GameplayAbilityDefinition Ability(string name, List<string> abilityTags,
            List<string> cancelWith = null, List<string> blockWith = null, string hold = "100.0")
        {
            var so = ScriptableObject.CreateInstance<GameplayAbilityDefinition>();
            _spawned.Add(so);
            var tags = new AbilityTagSet
            {
                AbilityTags = abilityTags ?? new List<string>(),
                CancelAbilitiesWithTags = cancelWith ?? new List<string>(),
                BlockAbilitiesWithTags = blockWith ?? new List<string>(),
            };
            so.Populate(name, tags, new List<AbilityTaskDefinition>
            {
                new AbilityTaskDefinition { Type = "WaitDelay", Params = new List<TaskParam> { new TaskParam { Key = "Duration", Value = hold } } },
            }, null, null);
            return so;
        }

        [Test]
        public void CancelAbilitiesWithTags_CancelsMatchingActiveAbility_OnActivation()
        {
            var owner = Owner();
            owner.GrantAbility(Ability("GA_Light", new List<string> { "Ability.Type.Light" }));
            owner.GrantAbility(Ability("GA_Heavy", new List<string> { "Ability.Type.Heavy" }, cancelWith: new List<string> { "Ability.Type.Light" }));
            var light = owner.GetAbility("GA_Light");
            var heavy = owner.GetAbility("GA_Heavy");

            Assert.That(owner.TryActivateAbility("GA_Light"), Is.True);
            owner.Tick(0.05f);
            Assert.That(light.State, Is.EqualTo(AbilityState.Active), "light is parked in its recovery");

            Assert.That(owner.TryActivateAbility("GA_Heavy"), Is.True, "heavy activates during the light's recovery");
            Assert.That(light.State, Is.EqualTo(AbilityState.Granted),
                "heavy's CancelAbilitiesWithTags [Ability.Type.Light] cancelled the light — the combo cancel");
            Assert.That(heavy.State, Is.EqualTo(AbilityState.Active), "heavy is now the active move");
        }

        [Test]
        public void CancelAbilitiesWithTags_LeavesNonMatchingActiveAbilityAlone()
        {
            var owner = Owner();
            owner.GrantAbility(Ability("GA_Block", new List<string> { "Ability.Type.Block" }));
            owner.GrantAbility(Ability("GA_Heavy", new List<string> { "Ability.Type.Heavy" }, cancelWith: new List<string> { "Ability.Type.Light" }));
            var block = owner.GetAbility("GA_Block");

            Assert.That(owner.TryActivateAbility("GA_Block"), Is.True);
            owner.Tick(0.05f);
            Assert.That(owner.TryActivateAbility("GA_Heavy"), Is.True);
            Assert.That(block.State, Is.EqualTo(AbilityState.Active),
                "heavy cancels only Ability.Type.Light — the block (Ability.Type.Block) is untouched");
        }

        [Test]
        public void BlockAbilitiesWithTags_BlocksTaggedAbility_WhileActive_ThenUnblocks()
        {
            var owner = Owner();
            owner.GrantAbility(Ability("GA_Special", new List<string> { "Ability.Type.Special" }, blockWith: new List<string> { "Ability.Type.Light" }));
            owner.GrantAbility(Ability("GA_Light", new List<string> { "Ability.Type.Light" }));
            var special = owner.GetAbility("GA_Special");
            var light = owner.GetAbility("GA_Light");

            Assert.That(owner.TryActivateAbility("GA_Special"), Is.True);
            owner.Tick(0.05f);
            Assert.That(owner.TryActivateAbility("GA_Light"), Is.False,
                "the super's BlockAbilitiesWithTags locks out the light while the super is active");
            Assert.That(light.State, Is.EqualTo(AbilityState.Granted), "the light did not activate");

            special.CancelAbility(); // the super ends
            Assert.That(owner.TryActivateAbility("GA_Light"), Is.True, "once the super ends, the light unblocks");
        }

        [Test]
        public void UnrelatedAbilities_CoexistWithoutFalseCancellation()
        {
            var owner = Owner();
            owner.GrantAbility(Ability("GA_A", new List<string> { "Ability.Type.A" }));
            owner.GrantAbility(Ability("GA_B", new List<string> { "Ability.Type.B" }));
            var a = owner.GetAbility("GA_A");
            var b = owner.GetAbility("GA_B");

            Assert.That(owner.TryActivateAbility("GA_A"), Is.True);
            Assert.That(owner.TryActivateAbility("GA_B"), Is.True);
            Assert.That(a.State, Is.EqualTo(AbilityState.Active), "no relationship tags → A keeps running");
            Assert.That(b.State, Is.EqualTo(AbilityState.Active), "B runs alongside A — no false cancellation");
        }
    }
}
