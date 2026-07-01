using System.Collections.Generic;
using UnityEngine;

namespace Jbltx.Ugas.Definitions
{
    /// <summary>The value an input action carries (SPEC §11.2).</summary>
    public enum InputValueType
    {
        Digital = 0, // boolean on/off
        Axis1D = 1,  // single float
        Axis2D = 2,  // 2-component vector
        Axis3D = 3,  // 3-component vector
    }

    /// <summary>When an action fires from its underlying input (SPEC §11.2).</summary>
    public enum InputTriggerBehavior
    {
        OnPressed = 0,   // zero → nonzero (default)
        OnReleased = 1,  // nonzero → zero
        WhileHeld = 2,   // every frame while nonzero
        OnTap = 3,       // press then release within a threshold
        OnDoubleTap = 4, // two taps within a window
    }

    /// <summary>
    /// A named, semantic input intent (SPEC §11.2) — the logical "what" (e.g. <c>Fire</c>), decoupled
    /// from any device's "how". The <see cref="Name"/> is what an ability's InputID resolves to (exact
    /// string match, §11.2), so gameplay binds to actions, never to hardware.
    /// </summary>
    [CreateAssetMenu(menuName = "UGAS/Input Action", fileName = "InputActionDefinition")]
    public sealed class InputActionDefinition : ScriptableObject
    {
        [SerializeField] private string _actionName;
        [SerializeField] private InputValueType _valueType = InputValueType.Digital;
        [SerializeField] private InputTriggerBehavior _triggerBehavior = InputTriggerBehavior.OnPressed;

        [Tooltip("When true (default), consumes the underlying input so lower-priority actions on the same input don't fire.")]
        [SerializeField] private bool _consumeInput = true;

        [Tooltip("Tags for bulk operations, e.g. Input.Type.Combat so an effect can suppress a whole category.")]
        [SerializeField] private List<string> _actionTags = new List<string>();

        public string Name => _actionName;
        public InputValueType ValueType => _valueType;
        public InputTriggerBehavior TriggerBehavior => _triggerBehavior;
        public bool ConsumeInput => _consumeInput;
        public IReadOnlyList<string> ActionTags => _actionTags;

        /// <summary>Populates the asset (editor importer / authoring / tests).</summary>
        public void Populate(string actionName, InputValueType valueType, InputTriggerBehavior triggerBehavior,
            bool consumeInput = true, List<string> actionTags = null)
        {
            _actionName = actionName;
            _valueType = valueType;
            _triggerBehavior = triggerBehavior;
            _consumeInput = consumeInput;
            _actionTags = actionTags ?? new List<string>();
        }
    }
}
