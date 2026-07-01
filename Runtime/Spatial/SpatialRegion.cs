using System;
using System.Collections.Generic;
using Jbltx.Ugas.Definitions;
using Jbltx.Ugas.Runtime;
using UnityEngine;

namespace Jbltx.Ugas.Spatial
{
    /// <summary>
    /// A placed region (SPEC §17.4): binds a <see cref="RegionDefinition"/> to a world origin and
    /// tracks occupancy. Each <see cref="Evaluate"/> is a standing §17.2 query — a GC that entered the
    /// volume is granted the region's tags; a GC that left has them removed. Grants go through the §7.2
    /// reference-counted container, so a GC inside two overlapping regions granting the same tag holds
    /// it once per region and keeps it until it leaves both.
    /// </summary>
    /// <remarks>
    /// Re-evaluate on the ambient §10.6 spatial cadence (50–100 ms is enough for entry/exit); an engine
    /// binding MAY instead drive it from trigger-volume callbacks provided the grant/remove semantics
    /// match. Region-granted tags are derived from occupancy and MUST NOT be persisted (§14) — on load,
    /// re-evaluate and the correct tags are re-granted. Current limitations (follow-ups): the region
    /// acts on every spatial GC in range (a §17.2 filter to restrict membership by tag shares the
    /// shared-registry story with area filters); Box/Capsule shapes resolve as a bounding sphere until
    /// the provider gains those overlaps.
    /// </remarks>
    public sealed class SpatialRegion
    {
        private readonly RegionDefinition _definition;
        private readonly HashSet<UgasController> _occupants = new HashSet<UgasController>();
        private readonly List<UgasController> _entered = new List<UgasController>();
        private readonly List<UgasController> _exited = new List<UgasController>();
        private bool _warnedShape;

        public SpatialRegion(RegionDefinition definition, Vector3 origin)
        {
            _definition = definition ? definition : throw new ArgumentNullException(nameof(definition));
            Origin = origin;
        }

        /// <summary>The definition this region was placed from.</summary>
        public RegionDefinition Definition => _definition;

        /// <summary>The region's world placement (its shape's centre); may be moved between evaluations.</summary>
        public Vector3 Origin { get; set; }

        /// <summary>The GCs currently inside the region, as of the last <see cref="Evaluate"/>.</summary>
        public IReadOnlyCollection<UgasController> Occupants => _occupants;

        /// <summary>
        /// Re-computes occupancy against <paramref name="provider"/> and reconciles tag grants: grants
        /// the region's tags to GCs that just entered and removes them from GCs that just left.
        /// </summary>
        public void Evaluate(ISpatialQueryProvider provider)
        {
            if (provider == null) return;

            if (_definition.Shape != RegionShape.Sphere && !_warnedShape)
            {
                _warnedShape = true;
                Debug.LogWarning($"[UGAS] Region '{_definition.RegionName}' shape {_definition.Shape} resolves as a Sphere until the provider gains that overlap.");
            }

            var hits = provider.OverlapSphere(Origin, _definition.Radius, SpatialFilter.None);

            // Diff the current hit set against the previous occupants.
            _entered.Clear();
            for (int i = 0; i < hits.Count; i++)
                if (!_occupants.Contains(hits[i])) _entered.Add(hits[i]);

            _exited.Clear();
            foreach (var occ in _occupants)
                if (!Contains(hits, occ)) _exited.Add(occ);

            var granted = _definition.GrantedTags;

            for (int i = 0; i < _entered.Count; i++)
            {
                var gc = _entered[i];
                _occupants.Add(gc);
                for (int t = 0; t < granted.Count; t++) gc.GrantTag(granted[t]);
            }

            for (int i = 0; i < _exited.Count; i++)
            {
                var gc = _exited[i];
                _occupants.Remove(gc);
                for (int t = 0; t < granted.Count; t++) gc.RemoveGrantedTag(granted[t]);
            }
        }

        private static bool Contains(IReadOnlyList<UgasController> list, UgasController gc)
        {
            for (int i = 0; i < list.Count; i++) if (list[i] == gc) return true;
            return false;
        }
    }
}
