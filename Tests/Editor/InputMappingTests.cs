using System.Collections.Generic;
using Jbltx.Ugas.Input;
using NUnit.Framework;
using UnityEngine;

namespace Jbltx.Ugas.Tests.Editor
{
    /// <summary>
    /// Conformance for §11.4–§11.5 mapping resolution: simple / chord / composite bindings resolve
    /// device-input state to an action value, run through the §11.6 modifier pipeline.
    /// </summary>
    [TestFixture]
    public class InputMappingTests
    {
        [Test]
        public void SimpleBinding_ResolvesInputValue()
        {
            var src = new DictionaryInputSource();
            var binding = new InputBinding
            {
                Action = "Fire",
                Kind = BindingKind.Simple,
                Inputs = new List<DeviceInput> { new DeviceInput("Mouse", "Mouse.LeftButton") },
            };

            src.Set("Mouse", "Mouse.LeftButton", 1f);
            Assert.That(InputMappingResolver.Resolve(binding, src).x, Is.EqualTo(1f).Within(1e-4f));
            src.Set("Mouse", "Mouse.LeftButton", 0f);
            Assert.That(InputMappingResolver.Resolve(binding, src).x, Is.EqualTo(0f).Within(1e-4f));
        }

        [Test]
        public void ChordBinding_FiresOnlyWhenAllInputsActive()
        {
            var src = new DictionaryInputSource();
            var binding = new InputBinding
            {
                Action = "AltFire",
                Kind = BindingKind.Chord,
                Inputs = new List<DeviceInput> { new DeviceInput("Keyboard", "Key.LeftShift"), new DeviceInput("Keyboard", "Key.1") },
            };

            src.Set("Keyboard", "Key.LeftShift", 1f);
            Assert.That(InputMappingResolver.Resolve(binding, src), Is.EqualTo(Vector3.zero), "only one input down");
            src.Set("Keyboard", "Key.1", 1f);
            Assert.That(InputMappingResolver.Resolve(binding, src).x, Is.EqualTo(1f).Within(1e-4f), "both down → fires");
        }

        [Test]
        public void CompositeBinding_ComposesWasdVector()
        {
            var src = new DictionaryInputSource();
            var binding = new InputBinding
            {
                Action = "Move",
                Kind = BindingKind.Composite,
                Up = new DeviceInput("Keyboard", "Key.W"),
                Down = new DeviceInput("Keyboard", "Key.S"),
                Left = new DeviceInput("Keyboard", "Key.A"),
                Right = new DeviceInput("Keyboard", "Key.D"),
            };

            src.Set("Keyboard", "Key.W", 1f);
            src.Set("Keyboard", "Key.D", 1f);
            Assert.That(InputMappingResolver.Resolve(binding, src), Is.EqualTo(new Vector3(1, 1, 0)), "W+D → (right, up)");

            src.Clear();
            src.Set("Keyboard", "Key.A", 1f);
            Assert.That(InputMappingResolver.Resolve(binding, src), Is.EqualTo(new Vector3(-1, 0, 0)), "A → left");
        }

        [Test]
        public void Binding_AppliesItsModifierPipeline()
        {
            var src = new DictionaryInputSource();
            var binding = new InputBinding
            {
                Action = "Look",
                Kind = BindingKind.Simple,
                Inputs = new List<DeviceInput> { new DeviceInput("Mouse", "Mouse.Axis.X") },
                Modifiers = new List<InputModifierDefinition> { new InputModifierDefinition { Type = InputModifierType.Sensitivity, Multiplier = 3f } },
            };

            src.Set("Mouse", "Mouse.Axis.X", 0.5f);
            Assert.That(InputMappingResolver.Resolve(binding, src).x, Is.EqualTo(1.5f).Within(1e-4f), "0.5 × sensitivity 3");
        }

        [Test]
        public void CompositeBinding_WithNormalize_ProducesUnitDiagonal()
        {
            var src = new DictionaryInputSource();
            var binding = new InputBinding
            {
                Action = "Move",
                Kind = BindingKind.Composite,
                Up = new DeviceInput("Keyboard", "Key.W"),
                Down = new DeviceInput("Keyboard", "Key.S"),
                Left = new DeviceInput("Keyboard", "Key.A"),
                Right = new DeviceInput("Keyboard", "Key.D"),
                Modifiers = new List<InputModifierDefinition> { new InputModifierDefinition { Type = InputModifierType.Normalize } },
            };

            src.Set("Keyboard", "Key.W", 1f);
            src.Set("Keyboard", "Key.D", 1f);
            Assert.That(InputMappingResolver.Resolve(binding, src).magnitude, Is.EqualTo(1f).Within(1e-4f), "diagonal normalized to unit length");
        }
    }
}
