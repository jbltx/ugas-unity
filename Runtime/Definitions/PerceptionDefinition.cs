using System.Collections.Generic;
using UnityEngine;

namespace Jbltx.Ugas.Definitions
{
    /// <summary>
    /// Authored data for an observer's perception (SPEC §17.5): the sense volume (range + optional
    /// field-of-view), the line-of-sight requirement, the filter of who is sensed, and the awareness
    /// tags granted to the observer while it perceives a target. Composed as a §17.2 query by
    /// <see cref="Jbltx.Ugas.Spatial.Perception"/>. Awareness is always expressed as tags on the
    /// observer — never hidden state (§17.5) — so gameplay reacts through the tag (e.g. a
    /// <c>State.Perceiving</c> tag gates an aggro/chase ability).
    /// </summary>
    [CreateAssetMenu(menuName = "UGAS/Perception", fileName = "PerceptionDefinition")]
    public sealed class PerceptionDefinition : ScriptableObject
    {
        [Tooltip("Maximum sense range; MAY be AttributeBased (e.g. reduced while blinded).")]
        [SerializeField] private MagnitudeDefinition _range = MagnitudeDefinition.Scalable(10f);

        [Tooltip("Forward field-of-view half-angle in degrees; <= 0 = omnidirectional.")]
        [SerializeField] private float _fovHalfAngleDeg;

        [Tooltip("Require unobstructed line-of-sight to sense the target.")]
        [SerializeField] private bool _requireLineOfSight;

        [Tooltip("Only sense GCs owning ALL of these tags (§7 hierarchical); typically the hostile faction. Empty = sense all.")]
        [SerializeField] private List<string> _requireTags = new List<string>();

        [Tooltip("Never sense GCs owning any of these tags.")]
        [SerializeField] private List<string> _excludeTags = new List<string>();

        [Tooltip("Tags granted to the OBSERVER while it perceives at least one target; removed when it perceives none.")]
        [SerializeField] private List<string> _perceivingTags = new List<string>();

        public MagnitudeDefinition Range => _range;
        public float FovHalfAngleDeg => _fovHalfAngleDeg;
        public bool RequireLineOfSight => _requireLineOfSight;
        public IReadOnlyList<string> RequireTags => _requireTags;
        public IReadOnlyList<string> ExcludeTags => _excludeTags;
        public IReadOnlyList<string> PerceivingTags => _perceivingTags;

        /// <summary>Populates the asset (editor importer / authoring / tests).</summary>
        public void Populate(MagnitudeDefinition range, float fovHalfAngleDeg, bool requireLineOfSight,
            List<string> requireTags, List<string> excludeTags, List<string> perceivingTags)
        {
            _range = range;
            _fovHalfAngleDeg = fovHalfAngleDeg;
            _requireLineOfSight = requireLineOfSight;
            _requireTags = requireTags ?? new List<string>();
            _excludeTags = excludeTags ?? new List<string>();
            _perceivingTags = perceivingTags ?? new List<string>();
        }
    }
}
