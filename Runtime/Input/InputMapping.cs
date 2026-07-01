using System;
using System.Collections.Generic;
using UnityEngine;

namespace Jbltx.Ugas.Input
{
    /// <summary>
    /// A canonical, engine-agnostic identifier for a physical input (SPEC §11.4): a device (e.g.
    /// <c>Keyboard</c>, <c>Gamepad</c>) plus a dot-notation input (e.g. <c>Key.Space</c>,
    /// <c>Gamepad.LeftStick.X</c>). The engine binding bridges its real events to these identifiers.
    /// </summary>
    [Serializable]
    public struct DeviceInput
    {
        public string Device;
        public string Input;

        public DeviceInput(string device, string input)
        {
            Device = device;
            Input = input;
        }
    }

    /// <summary>Supplies the current value of a canonical <see cref="DeviceInput"/> (engine binding at runtime).</summary>
    public interface IInputSource
    {
        float GetValue(DeviceInput input);
    }

    /// <summary>A dictionary-backed <see cref="IInputSource"/> — the engine binding pushes values in; tests set them directly.</summary>
    public sealed class DictionaryInputSource : IInputSource
    {
        private readonly Dictionary<(string, string), float> _values = new Dictionary<(string, string), float>();

        public void Set(string device, string input, float value) => _values[(device, input)] = value;
        public void Set(DeviceInput input, float value) => _values[(input.Device, input.Input)] = value;
        public void Clear() => _values.Clear();

        public float GetValue(DeviceInput input)
            => _values.TryGetValue((input.Device, input.Input), out var v) ? v : 0f;
    }

    /// <summary>How a binding combines its device inputs (SPEC §11.5).</summary>
    public enum BindingKind
    {
        /// <summary>One device input drives the action.</summary>
        Simple = 0,

        /// <summary>All inputs must be active simultaneously (modifier chords, e.g. Shift+1).</summary>
        Chord = 1,

        /// <summary>Directional slots compose into an axis vector (e.g. WASD → 2D movement).</summary>
        Composite = 2,
    }

    /// <summary>
    /// Binds device inputs to an action (SPEC §11.5). Simple/Chord use <see cref="Inputs"/>; Composite
    /// uses the directional slots. The resolved value is passed through <see cref="Modifiers"/> (§11.6).
    /// </summary>
    [Serializable]
    public sealed class InputBinding
    {
        public string Action;
        public BindingKind Kind = BindingKind.Simple;

        [Tooltip("Simple: the single input. Chord: all must be active.")]
        public List<DeviceInput> Inputs = new List<DeviceInput>();

        [Header("Composite slots (Composite kind)")]
        public DeviceInput Up;
        public DeviceInput Down;
        public DeviceInput Left;
        public DeviceInput Right;
        public DeviceInput Forward;
        public DeviceInput Backward;

        public List<InputModifierDefinition> Modifiers = new List<InputModifierDefinition>();

        [Tooltip("Higher priority wins when a chord and a simple binding share an input (§11.5).")]
        public int Priority;

        [Tooltip("Whether this binding may be remapped at runtime (§11.5).")]
        public bool Rebindable = true;
    }

    /// <summary>
    /// Authored device→action bindings for one action set + platform (SPEC §11.5). The runtime resolves
    /// them each frame against an <see cref="IInputSource"/> via <see cref="InputMappingResolver"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "UGAS/Input Mapping", fileName = "InputMappingDefinition")]
    public sealed class InputMappingDefinition : ScriptableObject
    {
        [SerializeField] private string _actionSet;
        [Tooltip("Platform this mapping applies to; empty = all platforms (§11.5).")]
        [SerializeField] private string _platform;
        [SerializeField] private List<InputBinding> _bindings = new List<InputBinding>();

        public string ActionSet => _actionSet;
        public string Platform => _platform;
        public IReadOnlyList<InputBinding> Bindings => _bindings;

        public void Populate(string actionSet, string platform, List<InputBinding> bindings)
        {
            _actionSet = actionSet;
            _platform = platform;
            _bindings = bindings ?? new List<InputBinding>();
        }
    }

    /// <summary>
    /// Resolves a binding to its action value (SPEC §11.5): composes the device inputs by
    /// <see cref="BindingKind"/>, then runs the §11.6 modifier pipeline. The engine binding calls this
    /// each frame and feeds the resulting value/event to the <see cref="UgasInputRouter"/>.
    /// </summary>
    public static class InputMappingResolver
    {
        public static Vector3 Resolve(InputBinding binding, IInputSource source)
        {
            if (binding == null || source == null) return Vector3.zero;

            Vector3 raw;
            switch (binding.Kind)
            {
                case BindingKind.Simple:
                    raw = new Vector3(binding.Inputs != null && binding.Inputs.Count > 0 ? source.GetValue(binding.Inputs[0]) : 0f, 0f, 0f);
                    break;

                case BindingKind.Chord:
                {
                    bool all = binding.Inputs != null && binding.Inputs.Count > 0;
                    if (all)
                        for (int i = 0; i < binding.Inputs.Count; i++)
                            if (Mathf.Approximately(source.GetValue(binding.Inputs[i]), 0f)) { all = false; break; }
                    raw = all ? new Vector3(1f, 0f, 0f) : Vector3.zero;
                    break;
                }

                case BindingKind.Composite:
                    raw = new Vector3(
                        source.GetValue(binding.Right) - source.GetValue(binding.Left),
                        source.GetValue(binding.Up) - source.GetValue(binding.Down),
                        source.GetValue(binding.Forward) - source.GetValue(binding.Backward));
                    break;

                default:
                    raw = Vector3.zero;
                    break;
            }

            return InputModifiers.Process(binding.Modifiers, raw);
        }
    }
}
