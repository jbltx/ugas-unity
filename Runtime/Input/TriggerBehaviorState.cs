using Jbltx.Ugas.Definitions;
using UnityEngine;

namespace Jbltx.Ugas.Input
{
    /// <summary>
    /// Per-action trigger state (SPEC §11.2): turns a stream of input values + timestamps into discrete
    /// "fire" events according to a <see cref="InputTriggerBehavior"/> — press/release edges, hold, tap,
    /// and double-tap. One instance per action; call <see cref="Evaluate"/> each update.
    /// </summary>
    public sealed class TriggerBehaviorState
    {
        private bool _wasActive;
        private float _pressTime;
        private float _lastTapTime;
        private int _tapCount;

        /// <summary>
        /// Advances the state with the action's current <paramref name="value"/> at <paramref name="time"/>
        /// and returns whether the action fires this update for <paramref name="behavior"/>.
        /// </summary>
        public bool Evaluate(float value, float time, InputTriggerBehavior behavior, float tapThreshold, float doubleTapWindow)
        {
            bool active = !Mathf.Approximately(value, 0f);
            bool fired = false;

            switch (behavior)
            {
                case InputTriggerBehavior.OnPressed:
                    fired = active && !_wasActive;
                    break;

                case InputTriggerBehavior.OnReleased:
                    fired = !active && _wasActive;
                    break;

                case InputTriggerBehavior.WhileHeld:
                    fired = active;
                    break;

                case InputTriggerBehavior.OnTap:
                    if (active && !_wasActive) _pressTime = time;                 // press
                    if (!active && _wasActive && time - _pressTime <= tapThreshold) fired = true; // quick release
                    break;

                case InputTriggerBehavior.OnDoubleTap:
                    if (!active && _wasActive) // a release completes a tap
                    {
                        _tapCount = time - _lastTapTime <= doubleTapWindow ? _tapCount + 1 : 1;
                        _lastTapTime = time;
                        if (_tapCount >= 2) { fired = true; _tapCount = 0; }
                    }
                    break;
            }

            _wasActive = active;
            return fired;
        }
    }
}
