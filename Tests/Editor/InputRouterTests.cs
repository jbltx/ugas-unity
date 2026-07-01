using System.Collections.Generic;
using Jbltx.Ugas.Abilities;
using Jbltx.Ugas.Definitions;
using Jbltx.Ugas.Input;
using Jbltx.Ugas.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Jbltx.Ugas.Tests.Editor
{
    /// <summary>
    /// Conformance for the §11 input layer via <see cref="UgasInputRouter"/>: a semantic action routes
    /// to its bound ability only while a tag-driven action set that contains it is active (§11.3), and
    /// an exclusive set suppresses lower-priority contexts (§11.3 rule 4).
    /// </summary>
    [TestFixture]
    public class InputRouterTests
    {
        private readonly List<Object> _spawned = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        private UgasController Player()
        {
            var go = new GameObject("Player");
            _spawned.Add(go);
            return go.AddComponent<UgasController>();
        }

        private GameplayAbilityDefinition Ability(string name)
        {
            var so = ScriptableObject.CreateInstance<GameplayAbilityDefinition>();
            _spawned.Add(so);
            so.Populate(name, default, new List<AbilityTaskDefinition>(), null, null); // no tags/cost/cooldown → activates freely
            return so;
        }

        private ActionSetDefinition Set(string name, List<string> actions, int priority, List<string> required, List<string> blocked, bool exclusive = false)
        {
            var so = ScriptableObject.CreateInstance<ActionSetDefinition>();
            _spawned.Add(so);
            so.Populate(name, actions, priority, required, blocked, exclusive);
            return so;
        }

        // A player with GA_Fire + GA_Reload granted and an OnFoot set (needs State.Alive, blocked by State.InVehicle).
        private (UgasController player, UgasInputRouter router) OnFootPlayer()
        {
            var player = Player();
            player.GrantAbility(Ability("GA_Fire"));
            player.GrantAbility(Ability("GA_Reload"));

            var router = new UgasInputRouter(player);
            router.RegisterActionSet(Set("OnFoot", new List<string> { "Fire", "Reload" }, 0, new List<string> { "State.Alive" }, new List<string> { "State.InVehicle" }));
            router.BindAction("Fire", "GA_Fire");
            router.BindAction("Reload", "GA_Reload");
            return (player, router);
        }

        [Test]
        public void SendInput_InActiveContext_ActivatesBoundAbility()
        {
            var (player, router) = OnFootPlayer();
            player.GrantTag("State.Alive"); // OnFoot now active

            Assert.That(router.IsActionActive("Fire"), Is.True);
            Assert.That(router.SendInput("Fire"), Is.True, "routed to GA_Fire");
            Assert.That(player.GetAbility("GA_Fire").State, Is.EqualTo(AbilityState.Active));
        }

        [Test]
        public void IsActionActive_MissingRequiredTag_IsFalse()
        {
            var (_, router) = OnFootPlayer(); // no State.Alive granted → OnFoot inactive
            Assert.That(router.IsActionActive("Fire"), Is.False);
            Assert.That(router.SendInput("Fire"), Is.False);
        }

        [Test]
        public void IsActionActive_BlockedContext_IsFalse()
        {
            var (player, router) = OnFootPlayer();
            player.GrantTag("State.Alive");
            player.GrantTag("State.InVehicle"); // OnFoot lists this as a BlockedTag
            Assert.That(router.IsActionActive("Fire"), Is.False);
        }

        [Test]
        public void ExclusiveSet_SuppressesLowerPriorityContext()
        {
            var player = Player();
            player.GrantAbility(Ability("GA_Fire"));
            player.GrantAbility(Ability("GA_Honk"));

            var router = new UgasInputRouter(player);
            // OnFoot has no BlockedTags here, so only exclusivity (not a tag block) can suppress it.
            router.RegisterActionSet(Set("OnFoot", new List<string> { "Fire" }, 0, new List<string> { "State.Alive" }, null));
            router.RegisterActionSet(Set("InVehicle", new List<string> { "Honk" }, 10, new List<string> { "State.InVehicle" }, null, exclusive: true));
            router.BindAction("Fire", "GA_Fire");
            router.BindAction("Honk", "GA_Honk");

            player.GrantTag("State.Alive");
            Assert.That(router.IsActionActive("Fire"), Is.True, "OnFoot active on foot");

            player.GrantTag("State.InVehicle"); // exclusive InVehicle (pri 10) now suppresses OnFoot (pri 0)
            Assert.That(router.IsActionActive("Honk"), Is.True, "vehicle context active");
            Assert.That(router.IsActionActive("Fire"), Is.False, "exclusive set suppresses the lower-priority OnFoot");
            Assert.That(router.ActiveActionSets, Does.Contain("InVehicle").And.Not.Contains("OnFoot"));
        }
    }
}
