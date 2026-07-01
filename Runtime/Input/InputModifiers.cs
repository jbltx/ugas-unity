using System;
using System.Collections.Generic;
using UnityEngine;

namespace Jbltx.Ugas.Input
{
    /// <summary>The processing step a modifier performs on an input value (SPEC §11.6).</summary>
    public enum InputModifierType
    {
        DeadZone = 0,
        Sensitivity = 1,
        AxisInvert = 2,
        ResponseCurve = 3,
        Clamp = 4,
        Normalize = 5,
        TriggerThreshold = 6,
    }

    /// <summary>DeadZone shape: per-axis, or on the vector's magnitude (SPEC §11.6).</summary>
    public enum DeadZoneShape
    {
        Axial = 0,
        Radial = 1,
    }

    /// <summary>Response-curve shape (SPEC §11.6).</summary>
    public enum ResponseCurveType
    {
        Linear = 0,
        Exponential = 1,
        SCurve = 2,
    }

    /// <summary>
    /// A single reusable input-processing step (SPEC §11.6), authored as data. Only the fields relevant
    /// to <see cref="Type"/> are read. Applied to an input value (Axis1D uses X; Axis2D uses X,Y; Axis3D
    /// uses X,Y,Z; a digital value is 0/1 on X) by <see cref="InputModifiers"/>.
    /// </summary>
    [Serializable]
    public struct InputModifierDefinition
    {
        public InputModifierType Type;

        [Header("DeadZone")]
        public float InnerThreshold;
        public float OuterThreshold;
        public DeadZoneShape DeadZoneShape;

        [Header("Sensitivity / AxisScale")]
        public float Multiplier;

        [Header("AxisInvert")]
        public bool InvertX;
        public bool InvertY;
        public bool InvertZ;

        [Header("ResponseCurve")]
        public ResponseCurveType CurveType;
        public float Exponent;

        [Header("Clamp")]
        public float Min;
        public float Max;

        [Header("TriggerThreshold")]
        public float PressThreshold;
    }

    /// <summary>
    /// The §11.6 modifier pipeline: pure, deterministic transforms applied to raw input values as they
    /// flow toward an action. <see cref="Process"/> runs a list of modifiers in order — the output of
    /// each feeds the next — matching the spec's ordered pipeline (e.g. DeadZone → RadialScaling for a
    /// stick, or Sensitivity for mouse look).
    /// </summary>
    public static class InputModifiers
    {
        /// <summary>Applies each modifier in order to <paramref name="value"/> and returns the result.</summary>
        public static Vector3 Process(IReadOnlyList<InputModifierDefinition> pipeline, Vector3 value)
        {
            if (pipeline == null) return value;
            for (int i = 0; i < pipeline.Count; i++) value = Apply(pipeline[i], value);
            return value;
        }

        /// <summary>Applies one modifier to a value.</summary>
        public static Vector3 Apply(in InputModifierDefinition m, Vector3 value)
        {
            switch (m.Type)
            {
                case InputModifierType.DeadZone:
                    return m.DeadZoneShape == DeadZoneShape.Radial ? RadialDeadZone(value, m.InnerThreshold, m.OuterThreshold)
                                                                   : AxialDeadZone(value, m.InnerThreshold, m.OuterThreshold);

                case InputModifierType.Sensitivity:
                    return value * m.Multiplier;

                case InputModifierType.AxisInvert:
                    return new Vector3(m.InvertX ? -value.x : value.x, m.InvertY ? -value.y : value.y, m.InvertZ ? -value.z : value.z);

                case InputModifierType.ResponseCurve:
                    return new Vector3(Curve(value.x, m.CurveType, m.Exponent), Curve(value.y, m.CurveType, m.Exponent), Curve(value.z, m.CurveType, m.Exponent));

                case InputModifierType.Clamp:
                    return new Vector3(Mathf.Clamp(value.x, m.Min, m.Max), Mathf.Clamp(value.y, m.Min, m.Max), Mathf.Clamp(value.z, m.Min, m.Max));

                case InputModifierType.Normalize:
                    return value.sqrMagnitude > 1e-8f ? value.normalized : value;

                case InputModifierType.TriggerThreshold:
                    return value.magnitude > m.PressThreshold ? new Vector3(1f, 0f, 0f) : Vector3.zero;

                default:
                    return value;
            }
        }

        // Per-axis dead zone: |a| below inner → 0, above outer → ±1, remapped linearly between.
        private static Vector3 AxialDeadZone(Vector3 v, float inner, float outer)
            => new Vector3(DeadZone1D(v.x, inner, outer), DeadZone1D(v.y, inner, outer), DeadZone1D(v.z, inner, outer));

        // Radial dead zone on the vector's magnitude, preserving direction.
        private static Vector3 RadialDeadZone(Vector3 v, float inner, float outer)
        {
            float mag = v.magnitude;
            if (mag <= 1e-8f) return Vector3.zero;
            float scaled = DeadZone1D(mag, inner, outer);
            return v * (scaled / mag);
        }

        private static float DeadZone1D(float value, float inner, float outer)
        {
            float sign = Mathf.Sign(value);
            float a = Mathf.Abs(value);
            if (a <= inner) return 0f;
            if (a >= outer) return sign;
            float range = Mathf.Max(outer - inner, 1e-6f);
            return sign * ((a - inner) / range);
        }

        private static float Curve(float x, ResponseCurveType type, float exponent)
        {
            float sign = Mathf.Sign(x);
            float a = Mathf.Abs(x);
            switch (type)
            {
                case ResponseCurveType.Exponential:
                    return sign * Mathf.Pow(a, exponent <= 0f ? 1f : exponent);
                case ResponseCurveType.SCurve:
                    return sign * (a * a * (3f - 2f * a)); // smoothstep on |x|
                default:
                    return x; // Linear
            }
        }
    }
}
