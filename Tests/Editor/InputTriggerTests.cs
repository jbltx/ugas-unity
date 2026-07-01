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
    /// Conformance for §11.2 trigger behaviors (edge/hold/tap detection) and the §11 input dispatcher
    /// that closes the loop: a device press resolved through mappings + the modifier pipeline fires the
    /// action's trigger and routes it to the (tag-gated) router, activating the bound ability.
    /// </summary>
    [TestFixture]
    public class InputTriggerTests
    {
        private readonly List<Object> _spawned = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        private const float Tap = 0.2f;
        private const float DoubleTap = 0.3f;

        [Test]
        public void OnPressed_FiresOnRisingEdgeOnce()
        {
            var s = new TriggerBehaviorState();
            Assert.That(s.Evaluate(0f, 0.0f, InputTriggerBehavior.OnPressed, Tap, DoubleTap), Is.False);
            Assert.That(s.Evaluate(1f, 0.1f, InputTriggerBehavior.OnPressed, Tap, DoubleTap), Is.True, "rising edge");
            Assert.That(s.Evaluate(1f, 0.2f, InputTriggerBehavior.OnPressed, Tap, DoubleTap), Is.False, "held → no re-fire");
        }

        [Test]
        public void OnReleased_FiresOnFallingEdge()
        {
            var s = new TriggerBehaviorState();
            s.Evaluate(1f, 0.0f, InputTriggerBehavior.OnReleased, Tap, DoubleTap);
            Assert.That(s.Evaluate(0f, 0.1f, InputTriggerBehavior.OnReleased, Tap, DoubleTap), Is.True);
        }

        [Test]
        public void WhileHeld_FiresEveryActiveFrame()
        {
            var s = new TriggerBehaviorState();
            Assert.That(s.Evaluate(1f, 0.0f, InputTriggerBehavior.WhileHeld, Tap, DoubleTap), Is.True);
            Assert.That(s.Evaluate(1f, 0.1f, InputTriggerBehavior.WhileHeld, Tap, DoubleTap), Is.True);
            Assert.That(s.Evaluate(0f, 0.2f, InputTriggerBehavior.WhileHeld, Tap, DoubleTap), Is.False);
        }

        [Test]
        public void OnTap_FiresOnQuickRelease_NotOnSlow()
        {
            var quick = new TriggerBehaviorState();
            quick.Evaluate(1f, 0.0f, InputTriggerBehavior.OnTap, Tap, DoubleTap);
            Assert.That(quick.Evaluate(0f, 0.1f, InputTriggerBehavior.OnTap, Tap, DoubleTap), Is.True, "release within threshold");

            var slow = new TriggerBehaviorState();
            slow.Evaluate(1f, 1.0f, InputTriggerBehavior.OnTap, Tap, DoubleTap);
            Assert.That(slow.Evaluate(0f, 1.5f, InputTriggerBehavior.OnTap, Tap, DoubleTap), Is.False, "held too long → not a tap");
        }

        [Test]
        public void OnDoubleTap_FiresOnSecondTapWithinWindow()
        {
            var s = new TriggerBehaviorState();
            s.Evaluate(1f, 0.00f, InputTriggerBehavior.OnDoubleTap, Tap, DoubleTap);
            Assert.That(s.Evaluate(0f, 0.05f, InputTriggerBehavior.OnDoubleTap, Tap, DoubleTap), Is.False, "first tap");
            s.Evaluate(1f, 0.15f, InputTriggerBehavior.OnDoubleTap, Tap, DoubleTap);
            Assert.That(s.Evaluate(0f, 0.20f, InputTriggerBehavior.OnDoubleTap, Tap, DoubleTap), Is.True, "second tap within window");
        }

        [Test]
        public void Update_DevicePress_ActivatesBoundAbilityThroughRouter()
        {
            var go = new GameObject("Player");
            _spawned.Add(go);
            var player = go.AddComponent<UgasController>();
            player.GrantAbility(Ability("GA_Fire"));

            var router = new UgasInputRouter(player);
            router.RegisterActionSet(Set("OnFoot", new List<string> { "Fire" }, 0, new List<string> { "State.Alive" }, null));
            router.BindAction("Fire", "GA_Fire");
            player.GrantTag("State.Alive");

            var source = new DictionaryInputSource();
            var system = new UgasInputSystem(router, source);
            system.SetTriggerBehavior("Fire", InputTriggerBehavior.OnPressed);
            system.AddBinding(new InputBinding
            {
                Action = "Fire",
                Kind = BindingKind.Simple,
                Inputs = new List<DeviceInput> { new DeviceInput("Mouse", "Mouse.LeftButton") },
            });

            system.Update(0f); // button up
            Assert.That(player.GetAbility("GA_Fire").State, Is.Not.EqualTo(AbilityState.Active));

            source.Set("Mouse", "Mouse.LeftButton", 1f); // press
            system.Update(0.1f);
            Assert.That(player.GetAbility("GA_Fire").State, Is.EqualTo(AbilityState.Active), "press → OnPressed → router → ability");
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
