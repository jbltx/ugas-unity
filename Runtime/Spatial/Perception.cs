using System;
using System.Collections.Generic;
using Jbltx.Ugas.Definitions;
using Jbltx.Ugas.Runtime;
using Jbltx.Ugas.Tags;
using UnityEngine;

namespace Jbltx.Ugas.Spatial
{
    /// <summary>
    /// An observer's perception (SPEC §17.5): composes a §17.2 range query (narrowed to a cone when a
    /// field-of-view is set) with an optional line-of-sight test to decide which GCs the observer
    /// senses. Awareness is expressed as tags on the observer — the definition's <c>PerceivingTags</c>
    /// are granted while it perceives at least one target and removed when it perceives none — never as
    /// hidden state (§17.5). Perception is per-observer: A perceiving B does not imply B perceives A.
    /// </summary>
    /// <remarks>
    /// Re-evaluate on the ambient §10.6 cadence (50–100 ms). <see cref="LineOfSightTest"/> is the engine
    /// seam for occlusion (typically a physics raycast); when null, sight is treated as unobstructed,
    /// which keeps the range/FOV logic testable without physics. The observer is never perceived by
    /// itself. Range resolves through the observer's <see cref="UgasController.ResolveMagnitude"/>, so an
    /// AttributeBased range scales with the observer's stats.
    /// </remarks>
    public sealed class Perception
    {
        private readonly UgasController _observer;
        private readonly PerceptionDefinition _definition;
        private readonly HashSet<UgasController> _perceived = new HashSet<UgasController>();
        private bool _perceivingActive;

        /// <summary>Occlusion test <c>(observerPos, targetPos) =&gt; hasLineOfSight</c>. Null = always visible.</summary>
        public Func<Vector3, Vector3, bool> LineOfSightTest;

        public Perception(UgasController observer, PerceptionDefinition definition)
        {
            _observer = observer ? observer : throw new ArgumentNullException(nameof(observer));
            _definition = definition ? definition : throw new ArgumentNullException(nameof(definition));
        }

        public UgasController Observer => _observer;
        public PerceptionDefinition Definition => _definition;

        /// <summary>The GCs currently perceived, as of the last <see cref="Evaluate"/>.</summary>
        public IReadOnlyCollection<UgasController> Perceived => _perceived;

        /// <summary>True while the observer perceives at least one target.</summary>
        public bool IsPerceiving => _perceivingActive;

        /// <summary>
        /// Re-computes the perceived set from the observer's current pose and reconciles the observer's
        /// awareness tags (granted on the first perceived target, removed when none remain).
        /// </summary>
        public void Evaluate(ISpatialQueryProvider provider)
        {
            if (provider == null) return;

            Vector3 eye = _observer.transform.position;
            float range = _observer.ResolveMagnitude(_definition.Range, 1);
            var filter = BuildFilter();

            var hits = _definition.FovHalfAngleDeg > 0f
                ? provider.OverlapCone(eye, _observer.transform.forward, range, _definition.FovHalfAngleDeg, filter)
                : provider.OverlapSphere(eye, range, filter);

            _perceived.Clear();
            for (int i = 0; i < hits.Count; i++)
            {
                var target = hits[i];
                if (target == _observer) continue; // never perceive self (the observer is its own anchor)
                if (_definition.RequireLineOfSight && LineOfSightTest != null &&
                    !LineOfSightTest(eye, target.transform.position)) continue;
                _perceived.Add(target);
            }

            ReconcileAwareness();
        }

        private void ReconcileAwareness()
        {
            bool nowPerceiving = _perceived.Count > 0;
            var tags = _definition.PerceivingTags;

            if (nowPerceiving && !_perceivingActive)
            {
                for (int i = 0; i < tags.Count; i++) _observer.GrantTag(tags[i]);
                _perceivingActive = true;
            }
            else if (!nowPerceiving && _perceivingActive)
            {
                for (int i = 0; i < tags.Count; i++) _observer.RemoveGrantedTag(tags[i]);
                _perceivingActive = false;
            }
        }

        // Tag filters are matched by NAME against each candidate's own registry (§17.2 / §7), so
        // perception stays sound even when observer and targets use different tag registries.
        private SpatialFilter BuildFilter() => new SpatialFilter
        {
            RequireTags = _definition.RequireTags,
            ExcludeTags = _definition.ExcludeTags,
        };
    }
}
