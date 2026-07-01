using System.Collections.Generic;
using UnityEngine;

namespace Jbltx.Ugas.Definitions
{
    /// <summary>
    /// A context-based group of input actions that are active together (SPEC §11.3): the tag-driven
    /// contexts that switch input schemes (e.g. <c>OnFoot</c> vs <c>InVehicle</c>). A set is active
    /// only while the owning GC's tags satisfy <see cref="RequiredTags"/> (all present) and
    /// <see cref="BlockedTags"/> (none present) — the same rules that gate abilities. Higher
    /// <see cref="Priority"/> wins when active sets share an action; an <see cref="Exclusive"/> set
    /// suppresses other non-exclusive sets at or below its priority.
    /// </summary>
    [CreateAssetMenu(menuName = "UGAS/Action Set", fileName = "ActionSetDefinition")]
    public sealed class ActionSetDefinition : ScriptableObject
    {
        [SerializeField] private string _setName;
        [Tooltip("Names of the input actions this set contains (match InputActionDefinition.Name).")]
        [SerializeField] private List<string> _actions = new List<string>();
        [SerializeField] private int _priority;
        [SerializeField] private List<string> _requiredTags = new List<string>();
        [SerializeField] private List<string> _blockedTags = new List<string>();

        [Tooltip("If true, activating this set deactivates other non-exclusive sets at or below its priority (modal contexts).")]
        [SerializeField] private bool _exclusive;

        public string SetName => _setName;
        public IReadOnlyList<string> Actions => _actions;
        public int Priority => _priority;
        public IReadOnlyList<string> RequiredTags => _requiredTags;
        public IReadOnlyList<string> BlockedTags => _blockedTags;
        public bool Exclusive => _exclusive;

        /// <summary>Populates the asset (editor importer / authoring / tests).</summary>
        public void Populate(string setName, List<string> actions, int priority,
            List<string> requiredTags, List<string> blockedTags, bool exclusive = false)
        {
            _setName = setName;
            _actions = actions ?? new List<string>();
            _priority = priority;
            _requiredTags = requiredTags ?? new List<string>();
            _blockedTags = blockedTags ?? new List<string>();
            _exclusive = exclusive;
        }
    }
}
