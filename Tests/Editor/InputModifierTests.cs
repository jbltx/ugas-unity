using System.Collections.Generic;
using Jbltx.Ugas.Input;
using NUnit.Framework;
using UnityEngine;

namespace Jbltx.Ugas.Tests.Editor
{
    /// <summary>
    /// Conformance for the §11.6 input modifier pipeline: each modifier's transform and the ordered
    /// pipeline (the output of one step feeds the next).
    /// </summary>
    [TestFixture]
    public class InputModifierTests
    {
        [Test]
        public void DeadZone_Radial_ClampsInnerAndRemapsBetween()
        {
            var m = new InputModifierDefinition { Type = InputModifierType.DeadZone, DeadZoneShape = DeadZoneShape.Radial, InnerThreshold = 0.2f, OuterThreshold = 0.9f };
            Assert.That(InputModifiers.Apply(m, new Vector3(0.1f, 0, 0)).magnitude, Is.EqualTo(0f).Within(1e-4f), "below inner → zero");
            Assert.That(InputModifiers.Apply(m, new Vector3(1.5f, 0, 0)).magnitude, Is.EqualTo(1f).Within(1e-4f), "above outer → unit");
            Assert.That(InputModifiers.Apply(m, new Vector3(0.55f, 0, 0)).x, Is.EqualTo(0.5f).Within(1e-3f), "midpoint remaps to 0.5");
        }

        [Test]
        public void Sensitivity_Scales()
        {
            var m = new InputModifierDefinition { Type = InputModifierType.Sensitivity, Multiplier = 2.5f };
            Assert.That(InputModifiers.Apply(m, new Vector3(2, -1, 0)), Is.EqualTo(new Vector3(5, -2.5f, 0)));
        }

        [Test]
        public void AxisInvert_NegatesFlaggedAxes()
        {
            var m = new InputModifierDefinition { Type = InputModifierType.AxisInvert, InvertY = true };
            Assert.That(InputModifiers.Apply(m, new Vector3(1, 1, 1)), Is.EqualTo(new Vector3(1, -1, 1)));
        }

        [Test]
        public void ResponseCurve_Exponential_ShapesMagnitudeAndKeepsSign()
        {
            var m = new InputModifierDefinition { Type = InputModifierType.ResponseCurve, CurveType = ResponseCurveType.Exponential, Exponent = 2f };
            Assert.That(InputModifiers.Apply(m, new Vector3(0.5f, 0, 0)).x, Is.EqualTo(0.25f).Within(1e-4f));
            Assert.That(InputModifiers.Apply(m, new Vector3(-0.5f, 0, 0)).x, Is.EqualTo(-0.25f).Within(1e-4f));
        }

        [Test]
        public void Clamp_BoundsEachAxis()
        {
            var m = new InputModifierDefinition { Type = InputModifierType.Clamp, Min = -1f, Max = 1f };
            Assert.That(InputModifiers.Apply(m, new Vector3(2, -3, 0.5f)), Is.EqualTo(new Vector3(1, -1, 0.5f)));
        }

        [Test]
        public void Normalize_ProducesUnitLength()
        {
            var m = new InputModifierDefinition { Type = InputModifierType.Normalize };
            Assert.That(InputModifiers.Apply(m, new Vector3(3, 4, 0)).magnitude, Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void TriggerThreshold_Digitizes()
        {
            var m = new InputModifierDefinition { Type = InputModifierType.TriggerThreshold, PressThreshold = 0.5f };
            Assert.That(InputModifiers.Apply(m, new Vector3(0.6f, 0, 0)), Is.EqualTo(new Vector3(1, 0, 0)));
            Assert.That(InputModifiers.Apply(m, new Vector3(0.4f, 0, 0)), Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void Pipeline_AppliesModifiersInOrder()
        {
            var pipeline = new List<InputModifierDefinition>
            {
                new InputModifierDefinition { Type = InputModifierType.DeadZone, DeadZoneShape = DeadZoneShape.Radial, InnerThreshold = 0.2f, OuterThreshold = 1.0f },
                new InputModifierDefinition { Type = InputModifierType.Sensitivity, Multiplier = 2f },
            };
            // 0.6 → deadzone (0.6-0.2)/0.8 = 0.5 → ×2 = 1.0
            Assert.That(InputModifiers.Process(pipeline, new Vector3(0.6f, 0, 0)).x, Is.EqualTo(1.0f).Within(1e-3f));
        }
    }
}
