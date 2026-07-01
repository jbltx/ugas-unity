using System;
using System.Collections.Generic;
using Jbltx.Ugas.Definitions;

namespace Jbltx.Ugas.Input
{
    /// <summary>
    /// The top of the §11 input stack: each <see cref="Update"/> it resolves every binding against the
    /// input source (device → value through the §11.6 modifier pipeline, §11.5), aggregates the strongest
    /// value per action, evaluates the action's trigger behavior (§11.2), and routes fired actions to the
    /// <see cref="UgasInputRouter"/> — which gates them by the active tag-driven action sets (§11.3) and
    /// activates the bound ability. Closing the loop from a raw device event to an ability activation.
    /// </summary>
    public sealed class UgasInputSystem
    {
        private readonly UgasInputRouter _router;
        private readonly IInputSource _source;
        private readonly List<InputBinding> _bindings = new List<InputBinding>();
        private readonly Dictionary<string, InputTriggerBehavior> _behaviors = new Dictionary<string, InputTriggerBehavior>();
        private readonly Dictionary<string, TriggerBehaviorState> _states = new Dictionary<string, TriggerBehaviorState>();
        private readonly Dictionary<string, float> _actionValues = new Dictionary<string, float>();

        /// <summary>Max seconds between press and release for an OnTap (§11.2).</summary>
        public float TapThreshold = 0.2f;

        /// <summary>Max seconds between taps for an OnDoubleTap (§11.2).</summary>
        public float DoubleTapWindow = 0.3f;

        public UgasInputSystem(UgasInputRouter router, IInputSource source)
        {
            _router = router ?? throw new ArgumentNullException(nameof(router));
            _source = source ?? throw new ArgumentNullException(nameof(source));
        }

        public void AddBinding(InputBinding binding)
        {
            if (binding != null) _bindings.Add(binding);
        }

        /// <summary>Sets an action's trigger behavior (§11.2); defaults to OnPressed when unset.</summary>
        public void SetTriggerBehavior(string action, InputTriggerBehavior behavior)
        {
            if (!string.IsNullOrEmpty(action)) _behaviors[action] = behavior;
        }

        /// <summary>
        /// Resolves all bindings, evaluates each action's trigger, and routes fired actions to the router.
        /// Call once per input frame with a monotonic <paramref name="time"/> (seconds).
        /// </summary>
        public void Update(float time)
        {
            // Aggregate the strongest resolved value per action this frame (multiple bindings may map one action).
            _actionValues.Clear();
            for (int i = 0; i < _bindings.Count; i++)
            {
                var binding = _bindings[i];
                if (binding == null || string.IsNullOrEmpty(binding.Action)) continue;
                float value = InputMappingResolver.Resolve(binding, _source).magnitude;
                if (!_actionValues.TryGetValue(binding.Action, out var current) || value > current)
                    _actionValues[binding.Action] = value;
            }

            foreach (var pair in _actionValues)
            {
                var behavior = _behaviors.TryGetValue(pair.Key, out var b) ? b : InputTriggerBehavior.OnPressed;
                if (GetState(pair.Key).Evaluate(pair.Value, time, behavior, TapThreshold, DoubleTapWindow))
                    _router.SendInput(pair.Key);
            }
        }

        private TriggerBehaviorState GetState(string action)
        {
            if (!_states.TryGetValue(action, out var state))
            {
                state = new TriggerBehaviorState();
                _states[action] = state;
            }
            return state;
        }
    }
}
