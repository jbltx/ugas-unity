using System.Collections.Generic;
using Jbltx.Ugas.Input;
using NUnit.Framework;

namespace Jbltx.Ugas.Tests.Editor
{
    /// <summary>
    /// Conformance for input remapping (SPEC §11.8): query an action's bindings, rebind a device input
    /// (respecting <c>Rebindable</c> + conflict detection), and reset to defaults.
    /// </summary>
    [TestFixture]
    public class InputRemappingTests
    {
        private static InputBinding Simple(string action, string device, string input, bool rebindable = true)
            => new InputBinding
            {
                Action = action,
                Kind = BindingKind.Simple,
                Rebindable = rebindable,
                Inputs = new List<DeviceInput> { new DeviceInput(device, input) },
            };

        [Test]
        public void GetBindingsForAction_ReturnsMatching()
        {
            var mapper = new UgasInputRemapper(new List<InputBinding> { Simple("Fire", "Mouse", "Mouse.LeftButton"), Simple("Jump", "Keyboard", "Key.Space") });
            var fire = mapper.GetBindingsForAction("Fire");
            Assert.That(fire, Has.Count.EqualTo(1));
            Assert.That(fire[0].Inputs[0].Input, Is.EqualTo("Mouse.LeftButton"));
        }

        [Test]
        public void RemapBinding_ChangesInput_AndResolverReflectsIt()
        {
            var fire = Simple("Fire", "Mouse", "Mouse.LeftButton");
            var mapper = new UgasInputRemapper(new List<InputBinding> { fire });

            Assert.That(mapper.RemapBinding("Fire", new DeviceInput("Mouse", "Mouse.LeftButton"), new DeviceInput("Keyboard", "Key.F")), Is.True);
            Assert.That(fire.Inputs[0].Input, Is.EqualTo("Key.F"));

            var src = new DictionaryInputSource();
            src.Set("Keyboard", "Key.F", 1f);
            Assert.That(InputMappingResolver.Resolve(fire, src).x, Is.EqualTo(1f).Within(1e-4f), "the rebound input now drives the action");
        }

        [Test]
        public void RemapBinding_RefusesNonRebindable()
        {
            var fixedBinding = Simple("Menu", "Keyboard", "Key.Escape", rebindable: false);
            var mapper = new UgasInputRemapper(new List<InputBinding> { fixedBinding });

            Assert.That(mapper.RemapBinding("Menu", new DeviceInput("Keyboard", "Key.Escape"), new DeviceInput("Keyboard", "Key.M")), Is.False);
            Assert.That(fixedBinding.Inputs[0].Input, Is.EqualTo("Key.Escape"), "fixed binding unchanged");
        }

        [Test]
        public void RemapBinding_RefusesConflict()
        {
            var fire = Simple("Fire", "Mouse", "Mouse.LeftButton");
            var jump = Simple("Jump", "Keyboard", "Key.Space");
            var mapper = new UgasInputRemapper(new List<InputBinding> { fire, jump });

            Assert.That(mapper.RemapBinding("Fire", new DeviceInput("Mouse", "Mouse.LeftButton"), new DeviceInput("Keyboard", "Key.Space")), Is.False, "Key.Space already bound to Jump");
            Assert.That(fire.Inputs[0].Input, Is.EqualTo("Mouse.LeftButton"), "unchanged on conflict");
        }

        [Test]
        public void ResetToDefaults_RestoresOriginalBindings()
        {
            var fire = Simple("Fire", "Mouse", "Mouse.LeftButton");
            var mapper = new UgasInputRemapper(new List<InputBinding> { fire });

            mapper.RemapBinding("Fire", new DeviceInput("Mouse", "Mouse.LeftButton"), new DeviceInput("Keyboard", "Key.F"));
            Assert.That(fire.Inputs[0].Input, Is.EqualTo("Key.F"));

            mapper.ResetToDefaults();
            Assert.That(fire.Inputs[0].Input, Is.EqualTo("Mouse.LeftButton"), "reset restores the default binding");
        }
    }
}
