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
    /// Coverage for the WaitInputRelease ability task (SPEC §10.3) wired to the §11 input pillar: a
    /// "hold to aim, release to stop" ability holds its buff while the input is held and drops it on
    /// release. Exercised both against a stub input-state source (the task's completion rule + sequencing)
    /// and end-to-end through the real <see cref="UgasInputSystem"/> (device up/down → release), including
    /// the auto-wiring of the system as the controller's input-state source.
    /// </summary>
    [TestFixture]
    public class WaitInputReleaseTests
    {
        private readonly List<Object> _spawned = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        // A test double for the input layer: reports whichever held-state the test sets.
        private sealed class FakeInput : IInputStateSource
        {
            public bool Held;
            public bool IsActionHeld(string action) => Held;
        }

        private UgasController Controller(string name = "Aimer")
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            return go.AddComponent<UgasController>();
        }

        // GE_ADS: Infinite, no modifiers, grants State.Aiming — a tag-only marker for "the buff is held".
        private GameplayEffectDefinition AdsEffect()
        {
            var e = ScriptableObject.CreateInstance<GameplayEffectDefinition>();
            _spawned.Add(e);
            e.Populate("GE_ADS", DurationPolicy.Infinite, default, default, ExecutionPolicy.RunInParallel, 0,
                null, new List<string> { "State.Aiming" }, null, null);
            return e;
        }

        // GA_Aim = [apply ADS, WaitInputRelease(Aim), remove ADS]: hold-to-aim built entirely from tasks.
        private GameplayAbilityDefinition AimAbility()
        {
            var so = ScriptableObject.CreateInstance<GameplayAbilityDefinition>();
            _spawned.Add(so);
            so.Populate("GA_Aim", default, new List<AbilityTaskDefinition>
            {
                new AbilityTaskDefinition { Type = "ApplyEffectToOwner",    Params = new List<TaskParam> { new TaskParam { Key = "EffectClass", Value = "GE_ADS" } } },
                new AbilityTaskDefinition { Type = "WaitInputRelease",      Params = new List<TaskParam> { new TaskParam { Key = "InputID", Value = "Aim" } } },
                new AbilityTaskDefinition { Type = "RemoveEffectFromOwner", Params = new List<TaskParam> { new TaskParam { Key = "EffectClass", Value = "GE_ADS" } } },
            }, null, null);
            return so;
        }

        private ActionSetDefinition Set(string name, List<string> actions, int priority, List<string> required, List<string> blocked)
        {
            var so = ScriptableObject.CreateInstance<ActionSetDefinition>();
            _spawned.Add(so);
            so.Populate(name, actions, priority, required, blocked, false);
            return so;
        }

        [Test]
        public void WaitInputRelease_HoldsAbility_UntilInputStateReportsReleased()
        {
            var owner = Controller();
            var input = new FakeInput { Held = true };
            owner.InputState = input;
            owner.RegisterEffect(AdsEffect());
            owner.GrantAbility(AimAbility());

            Assert.That(owner.TryActivateAbility("GA_Aim"), Is.True);

            owner.Tick(0.1f); // task0 applies ADS → WaitInputRelease now gates the sequence
            Assert.That(owner.OwnedTags.HasTag("State.Aiming"), Is.True, "ADS held while the input is held");
            owner.Tick(0.1f); // still held → the ability keeps holding
            Assert.That(owner.OwnedTags.HasTag("State.Aiming"), Is.True, "still holding while the input stays down");

            input.Held = false; // player releases the input
            owner.Tick(0.1f);   // WaitInputRelease sees the release → completes → advances
            owner.Tick(0.1f);   // remove-ADS task runs
            Assert.That(owner.OwnedTags.HasTag("State.Aiming"), Is.False, "release drops ADS (the sequence advanced past the wait)");
            Assert.That(owner.GetAbility("GA_Aim").State, Is.EqualTo(AbilityState.Granted), "ability ended after release");
        }

        [Test]
        public void WaitInputRelease_ViaInputSystem_AutoWiresAndReleasesOnDeviceUp()
        {
            var owner = Controller();
            owner.RegisterEffect(AdsEffect());
            owner.GrantAbility(AimAbility());

            var router = new UgasInputRouter(owner);
            router.RegisterActionSet(Set("OnFoot", new List<string> { "Aim" }, 0, new List<string> { "State.Alive" }, null));
            router.BindAction("Aim", "GA_Aim");
            owner.GrantTag("State.Alive");

            var source = new DictionaryInputSource();
            var system = new UgasInputSystem(router, source);
            Assert.That(owner.InputState, Is.SameAs(system), "the input system auto-wires itself as the controller's input-state source");
            system.SetTriggerBehavior("Aim", InputTriggerBehavior.OnPressed);
            system.AddBinding(new InputBinding
            {
                Action = "Aim",
                Kind = BindingKind.Simple,
                Inputs = new List<DeviceInput> { new DeviceInput("Mouse", "Mouse.RightButton") },
            });

            // Press: OnPressed fires → router activates GA_Aim → its first task applies ADS.
            source.Set("Mouse", "Mouse.RightButton", 1f);
            system.Update(0.1f);
            owner.Tick(0.05f);
            Assert.That(owner.GetAbility("GA_Aim").State, Is.EqualTo(AbilityState.Active), "aim activates on press");
            Assert.That(owner.OwnedTags.HasTag("State.Aiming"), Is.True, "ADS held while the button is down");

            // Hold: button stays down across frames → WaitInputRelease keeps waiting, ADS keeps holding.
            system.Update(0.2f); owner.Tick(0.05f);
            Assert.That(owner.OwnedTags.HasTag("State.Aiming"), Is.True, "still holding while the button is down");

            // Release: button up → WaitInputRelease completes → the remove-ADS task runs.
            source.Set("Mouse", "Mouse.RightButton", 0f);
            system.Update(0.3f);
            owner.Tick(0.05f); // WaitInputRelease observes the release → completes → advances
            owner.Tick(0.05f); // remove-ADS task runs
            Assert.That(owner.OwnedTags.HasTag("State.Aiming"), Is.False, "releasing the button drops ADS");
            Assert.That(owner.GetAbility("GA_Aim").State, Is.EqualTo(AbilityState.Granted), "ability ends after release");
        }
    }
}
