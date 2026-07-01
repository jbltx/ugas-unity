using System.Collections.Generic;

namespace Jbltx.Ugas.Input
{
    /// <summary>
    /// Runtime rebinding of input bindings (SPEC §11.8): query an action's bindings, remap a device
    /// input, and reset to defaults. Implementations MUST respect <see cref="InputBinding.Rebindable"/>
    /// and detect conflicts (two bindings sharing a device input).
    /// </summary>
    public interface IInputMapper
    {
        /// <summary>The bindings currently mapped to <paramref name="action"/>.</summary>
        IReadOnlyList<InputBinding> GetBindingsForAction(string action);

        /// <summary>
        /// Rebinds <paramref name="action"/>'s binding from <paramref name="oldInput"/> to
        /// <paramref name="newInput"/>. Returns false if no rebindable binding matches or the new input
        /// conflicts with an existing binding.
        /// </summary>
        bool RemapBinding(string action, DeviceInput oldInput, DeviceInput newInput);

        /// <summary>Restores every binding's device inputs to the defaults captured at construction.</summary>
        void ResetToDefaults();
    }

    /// <summary>
    /// The reference <see cref="IInputMapper"/> over a live list of <see cref="InputBinding"/> (§11.8).
    /// Rebinding mutates the same bindings the <see cref="InputMappingResolver"/> reads, so a remap takes
    /// effect immediately; the defaults captured at construction back <see cref="ResetToDefaults"/>.
    /// Conflicts are refused (an engine MAY instead offer to swap — a UI choice).
    /// </summary>
    /// <remarks>
    /// Persisting/loading remaps to storage (§11.8 Save/Load) is the engine binding's concern; this
    /// mapper operates on the in-memory bindings.
    /// </remarks>
    public sealed class UgasInputRemapper : IInputMapper
    {
        private readonly List<InputBinding> _bindings;
        private readonly Dictionary<InputBinding, List<DeviceInput>> _defaults = new Dictionary<InputBinding, List<DeviceInput>>();

        public UgasInputRemapper(List<InputBinding> bindings)
        {
            _bindings = bindings ?? new List<InputBinding>();
            for (int i = 0; i < _bindings.Count; i++)
            {
                var b = _bindings[i];
                if (b != null) _defaults[b] = new List<DeviceInput>(b.Inputs ?? new List<DeviceInput>());
            }
        }

        public IReadOnlyList<InputBinding> GetBindingsForAction(string action)
        {
            var result = new List<InputBinding>();
            for (int i = 0; i < _bindings.Count; i++)
                if (_bindings[i] != null && _bindings[i].Action == action) result.Add(_bindings[i]);
            return result;
        }

        public bool RemapBinding(string action, DeviceInput oldInput, DeviceInput newInput)
        {
            // Refuse if the new input is already bound anywhere (conflict, §11.8).
            for (int i = 0; i < _bindings.Count; i++)
                if (Uses(_bindings[i], newInput)) return false;

            for (int i = 0; i < _bindings.Count; i++)
            {
                var b = _bindings[i];
                if (b == null || b.Action != action || !b.Rebindable || b.Inputs == null) continue;
                int idx = IndexOf(b.Inputs, oldInput);
                if (idx >= 0)
                {
                    b.Inputs[idx] = newInput;
                    return true;
                }
            }
            return false;
        }

        public void ResetToDefaults()
        {
            for (int i = 0; i < _bindings.Count; i++)
            {
                var b = _bindings[i];
                if (b != null && _defaults.TryGetValue(b, out var def)) b.Inputs = new List<DeviceInput>(def);
            }
        }

        private static bool Uses(InputBinding b, DeviceInput input)
        {
            if (b?.Inputs == null) return false;
            return IndexOf(b.Inputs, input) >= 0;
        }

        private static int IndexOf(List<DeviceInput> inputs, DeviceInput input)
        {
            for (int i = 0; i < inputs.Count; i++)
                if (inputs[i].Device == input.Device && inputs[i].Input == input.Input) return i;
            return -1;
        }
    }
}
