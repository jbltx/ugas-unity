using System;
using System.Collections.Generic;
using Jbltx.Ugas.Definitions;
using UnityEngine;

namespace Jbltx.Ugas.Input
{
    /// <summary>Input buffering configuration (SPEC §11.7).</summary>
    [System.Serializable]
    public struct InputBufferConfig
    {
        /// <summary>Enable buffering of inputs that couldn't activate yet.</summary>
        public bool Enabled;

        /// <summary>Seconds a buffered input stays retryable before it is discarded.</summary>
        public float BufferWindow;

        /// <summary>Max buffered inputs; 0 = unbounded. When full, the oldest is dropped.</summary>
        public int MaxBufferSize;
    }

    /// <summary>
    /// Read-only access to current input-action state for latent input tasks (SPEC §10.3 / §11.2): e.g.
    /// <c>WaitInputRelease</c> polls this to complete when its action is no longer held. Implemented by
    /// <see cref="UgasInputSystem"/>; a controller exposes one via <c>UgasController.InputState</c>.
    /// </summary>
    public interface IInputStateSource
    {
        /// <summary>True while <paramref name="action"/> resolves to a non-zero value this frame (held).</summary>
        bool IsActionHeld(string action);
    }

    /// <summary>
    /// The top of the §11 input stack: each <see cref="Update"/> it resolves every binding against the
    /// input source (device → value through the §11.6 modifier pipeline, §11.5), aggregates the strongest
    /// value per action, evaluates the action's trigger behavior (§11.2), and routes fired actions to the
    /// <see cref="UgasInputRouter"/> — which gates them by the active tag-driven action sets (§11.3) and
    /// activates the bound ability. Closing the loop from a raw device event to an ability activation.
    /// </summary>
    public sealed class UgasInputSystem : IInputStateSource
    {
        private readonly UgasInputRouter _router;
        private readonly IInputSource _source;
        private readonly List<InputBinding> _bindings = new List<InputBinding>();
        private readonly Dictionary<string, InputTriggerBehavior> _behaviors = new Dictionary<string, InputTriggerBehavior>();
        private readonly Dictionary<string, TriggerBehaviorState> _states = new Dictionary<string, TriggerBehaviorState>();
        private readonly Dictionary<string, float> _actionValues = new Dictionary<string, float>();
        private readonly List<(string action, float time)> _buffer = new List<(string action, float time)>();

        /// <summary>Input buffering (§11.7); disabled by default. A fired action that can't activate is queued and retried.</summary>
        public InputBufferConfig Buffering;

        /// <summary>Max seconds between press and release for an OnTap (§11.2).</summary>
        public float TapThreshold = 0.2f;

        /// <summary>Max seconds between taps for an OnDoubleTap (§11.2).</summary>
        public float DoubleTapWindow = 0.3f;

        public UgasInputSystem(UgasInputRouter router, IInputSource source)
        {
            _router = router ?? throw new ArgumentNullException(nameof(router));
            _source = source ?? throw new ArgumentNullException(nameof(source));
            // Wire this system as the owner's input-state source so latent input tasks (WaitInputRelease,
            // §10.3) can observe action state. Callers may override UgasController.InputState afterward.
            if (_router.Owner != null) _router.Owner.InputState = this;
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
            // Retry buffered inputs first (§11.7): drop expired, replay the rest — a press made while
            // blocked activates as soon as the block clears, within the window.
            if (Buffering.Enabled) DrainBuffer(time);

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
                if (!GetState(pair.Key).Evaluate(pair.Value, time, behavior, TapThreshold, DoubleTapWindow)) continue;
                if (!_router.SendInput(pair.Key) && Buffering.Enabled) Enqueue(pair.Key, time);
            }
        }

        /// <summary>§10.3/§11.2: true while the action resolved to a non-zero value on the last <see cref="Update"/> (held).</summary>
        public bool IsActionHeld(string action) =>
            action != null && _actionValues.TryGetValue(action, out var v) && !Mathf.Approximately(v, 0f);

        // Discards expired buffered inputs, then retries the rest oldest-first, removing the first that
        // activates (§11.7). Unexpired-but-still-blocked inputs remain for the next update.
        private void DrainBuffer(float now)
        {
            for (int i = _buffer.Count - 1; i >= 0; i--)
                if (now - _buffer[i].time > Buffering.BufferWindow) _buffer.RemoveAt(i);

            for (int i = 0; i < _buffer.Count; i++)
                if (_router.SendInput(_buffer[i].action)) { _buffer.RemoveAt(i); break; }
        }

        private void Enqueue(string action, float time)
        {
            if (Buffering.MaxBufferSize > 0 && _buffer.Count >= Buffering.MaxBufferSize) _buffer.RemoveAt(0);
            _buffer.Add((action, time));
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
