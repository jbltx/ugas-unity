using System;
using System.Collections.Generic;
using Jbltx.Ugas.Definitions;
using Jbltx.Ugas.Runtime;

namespace Jbltx.Ugas.Input
{
    /// <summary>
    /// Routes semantic input actions to ability activation through tag-driven action sets (SPEC §11).
    /// Register the action sets, bind each action's InputID to an ability, then feed input events with
    /// <see cref="SendInput"/>: an action fires only while a set that contains it is active per the
    /// owner's tags (§11.3), and the highest-priority active set wins for a shared action. This is the
    /// Command-pattern seam that lets input schemes switch by context (OnFoot vs InVehicle) without
    /// touching gameplay.
    /// </summary>
    /// <remarks>
    /// This is the semantic layer — actions → ability activation, gated by tag-driven contexts. The
    /// hardware layer (device mappings, the modifier pipeline, buffering, remapping — §11.4–§11.8) is
    /// the engine binding's job and a follow-up; feed this router the already-resolved action names.
    /// </remarks>
    public sealed class UgasInputRouter
    {
        private readonly UgasController _owner;
        private readonly List<ActionSetDefinition> _sets = new List<ActionSetDefinition>();
        private readonly Dictionary<string, string> _actionToAbility = new Dictionary<string, string>();

        public UgasInputRouter(UgasController owner)
        {
            _owner = owner ? owner : throw new ArgumentNullException(nameof(owner));
        }

        /// <summary>The controller this router activates abilities on (§11.3). Lets the input system wire
        /// itself as the owner's input-state source for latent input tasks like WaitInputRelease (§10.3).</summary>
        public UgasController Owner => _owner;

        public void RegisterActionSet(ActionSetDefinition set)
        {
            if (set != null && !_sets.Contains(set)) _sets.Add(set);
        }

        /// <summary>Binds an action's InputID (§11.2) to the ability it activates.</summary>
        public void BindAction(string actionName, string abilityName)
        {
            if (!string.IsNullOrEmpty(actionName) && !string.IsNullOrEmpty(abilityName))
                _actionToAbility[actionName] = abilityName;
        }

        /// <summary>The names of the action sets currently active on the owner (§11.3 ActiveActionSets).</summary>
        public IEnumerable<string> ActiveActionSets
        {
            get { for (int i = 0; i < _sets.Count; i++) if (IsActive(_sets[i])) yield return _sets[i].SetName; }
        }

        /// <summary>
        /// True while a set is active: its tag gate passes AND it is not suppressed by an exclusive set
        /// (§11.3 rule 4). The tag gate is all <c>RequiredTags</c> present and no <c>BlockedTags</c> present.
        /// </summary>
        public bool IsActive(ActionSetDefinition set)
        {
            if (!PassesTags(set)) return false;
            if (set.Exclusive) return true;
            // A non-exclusive set is suppressed by any tag-passing exclusive set at or above its priority.
            for (int i = 0; i < _sets.Count; i++)
            {
                var other = _sets[i];
                if (other != set && other.Exclusive && other.Priority >= set.Priority && PassesTags(other)) return false;
            }
            return true;
        }

        /// <summary>True if the action is contained in at least one currently-active set.</summary>
        public bool IsActionActive(string actionName) => ResolveActiveSet(actionName) != null;

        /// <summary>
        /// Feeds an input event for <paramref name="actionName"/>: if the action is in an active set and
        /// bound to an ability, activates it. Returns whether an ability activated.
        /// </summary>
        public bool SendInput(string actionName)
        {
            if (!IsActionActive(actionName)) return false;
            return _actionToAbility.TryGetValue(actionName, out var ability) && _owner.TryActivateAbility(ability);
        }

        // The highest-priority active set that contains the action, or null (§11.3 rule 3).
        private ActionSetDefinition ResolveActiveSet(string actionName)
        {
            ActionSetDefinition best = null;
            for (int i = 0; i < _sets.Count; i++)
            {
                var s = _sets[i];
                if (!Contains(s.Actions, actionName) || !IsActive(s)) continue;
                if (best == null || s.Priority > best.Priority) best = s;
            }
            return best;
        }

        // Tag gate only (no exclusivity): all RequiredTags present and no BlockedTags present.
        private bool PassesTags(ActionSetDefinition set)
        {
            if (set == null) return false;
            var tags = _owner.OwnedTags;
            var registry = _owner.TagRegistry;
            if (tags == null || registry == null) return set.RequiredTags.Count == 0;

            for (int i = 0; i < set.RequiredTags.Count; i++)
            {
                var t = registry.Find(set.RequiredTags[i]);
                if (!t.IsValid || !tags.HasTag(t)) return false;
            }
            for (int i = 0; i < set.BlockedTags.Count; i++)
            {
                var t = registry.Find(set.BlockedTags[i]);
                if (t.IsValid && tags.HasTag(t)) return false;
            }
            return true;
        }

        private static bool Contains(IReadOnlyList<string> list, string value)
        {
            for (int i = 0; i < list.Count; i++) if (list[i] == value) return true;
            return false;
        }
    }
}
