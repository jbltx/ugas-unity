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
    /// Conformance for input buffering (SPEC §11.7): an action pressed while it can't yet activate is
    /// queued and replayed when the block clears — within the buffer window; expired presses are dropped.
    /// </summary>
    [TestFixture]
    public class InputBufferingTests
    {
        private readonly List<Object> _spawned = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        // A player whose Fire ability is gated by State.Alive (initially absent, so activation is blocked),
        // with a buffering input system holding the Fire binding on the mouse button.
        private (UgasController player, DictionaryInputSource source, UgasInputSystem system) BufferedFireSetup(float window)
        {
            var go = new GameObject("Player");
            _spawned.Add(go);
            var player = go.AddComponent<UgasController>();
            player.GrantAbility(Ability("GA_Fire"));

            var router = new UgasInputRouter(player);
            router.RegisterActionSet(Set("OnFoot", new List<string> { "Fire" }, 0, new List<string> { "State.Alive" }, null));
            router.BindAction("Fire", "GA_Fire");

            var source = new DictionaryInputSource();
            var system = new UgasInputSystem(router, source) { Buffering = new InputBufferConfig { Enabled = true, BufferWindow = window, MaxBufferSize = 4 } };
            system.SetTriggerBehavior("Fire", InputTriggerBehavior.OnPressed);
            system.AddBinding(new InputBinding
            {
                Action = "Fire",
                Kind = BindingKind.Simple,
                Inputs = new List<DeviceInput> { new DeviceInput("Mouse", "Mouse.LeftButton") },
            });
            return (player, source, system);
        }

        [Test]
        public void BufferedInput_ReplaysWhenBlockClears()
        {
            var (player, source, system) = BufferedFireSetup(0.5f);

            source.Set("Mouse", "Mouse.LeftButton", 1f);
            system.Update(0f); // OnPressed fires, but not State.Alive → SendInput fails → buffered
            Assert.That(player.GetAbility("GA_Fire").State, Is.Not.EqualTo(AbilityState.Active));

            player.GrantTag("State.Alive"); // block clears
            system.Update(0.1f);            // within window → buffer replays → activates
            Assert.That(player.GetAbility("GA_Fire").State, Is.EqualTo(AbilityState.Active), "buffered press replays when unblocked");
        }

        [Test]
        public void BufferedInput_ExpiresBeyondWindow()
        {
            var (player, source, system) = BufferedFireSetup(0.5f);

            source.Set("Mouse", "Mouse.LeftButton", 1f);
            system.Update(0f);   // buffered
            system.Update(1.0f); // 1.0 > 0.5 window → discarded (still blocked; button held so no re-fire)

            player.GrantTag("State.Alive");
            system.Update(1.1f); // buffer empty → nothing to replay
            Assert.That(player.GetAbility("GA_Fire").State, Is.Not.EqualTo(AbilityState.Active), "expired press is not replayed");
        }

        private GameplayAbilityDefinition Ability(string name)
        {
            var so = ScriptableObject.CreateInstance<GameplayAbilityDefinition>();
            _spawned.Add(so);
            so.Populate(name, default, new List<AbilityTaskDefinition>(), null, null);
            return so;
        }

        private ActionSetDefinition Set(string name, List<string> actions, int priority, List<string> required, List<string> blocked)
        {
            var so = ScriptableObject.CreateInstance<ActionSetDefinition>();
            _spawned.Add(so);
            so.Populate(name, actions, priority, required, blocked, false);
            return so;
        }
    }
}
