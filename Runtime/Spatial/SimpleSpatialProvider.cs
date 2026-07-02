using System;
using System.Collections.Generic;
using Jbltx.Ugas.Runtime;
using UnityEngine;

namespace Jbltx.Ugas.Spatial
{
    /// <summary>
    /// The baseline <see cref="ISpatialQueryProvider"/> (SPEC §17.2): a correct O(n) linear scan over
    /// registered controllers. Positions are read live from each controller's transform at query time,
    /// so it never holds stale coordinates. Results are ordered nearest-first with a deterministic
    /// tie-break by registration order — reproducible across runs, satisfying §17.7's stability
    /// requirement for predicted target selection.
    /// </summary>
    /// <remarks>
    /// This is the reference behaviour, not the fast path: §17.6 permits a full scan for small worlds
    /// and names a uniform grid / spatial hash as the acceleration structure for large ones. A
    /// grid- and DOTS-accelerated provider is the optional follow-up, swappable behind this same
    /// interface (mirroring the managed↔DOTS <c>AttributeKernel</c> split). Query scratch buffers are
    /// reused, so steady-state queries allocate nothing.
    /// </remarks>
    public sealed class SimpleSpatialProvider : ISpatialQueryProvider
    {
        private readonly List<UgasController> _anchors = new List<UgasController>();
        private readonly List<Candidate> _scratch = new List<Candidate>();
        private readonly List<UgasController> _results = new List<UgasController>();

        private readonly struct Candidate
        {
            public readonly UgasController Gc;
            public readonly float SqrDistance;
            public readonly int Order; // registration index — stable, deterministic tie-break

            public Candidate(UgasController gc, float sqrDistance, int order)
            {
                Gc = gc;
                SqrDistance = sqrDistance;
                Order = order;
            }
        }

        /// <summary>Registered anchors, in registration order. Primarily for tests/inspection.</summary>
        public IReadOnlyList<UgasController> Anchors => _anchors;

        public void Register(UgasController gc)
        {
            if (gc != null && !_anchors.Contains(gc)) _anchors.Add(gc);
        }

        public void Unregister(UgasController gc)
        {
            _anchors.Remove(gc);
        }

        /// <summary>No-op: this provider reads positions live at query time, so there is no index to refresh.</summary>
        public void Rebuild() { }

        public float Distance(Vector3 a, Vector3 b) => Vector3.Distance(a, b);

        public IReadOnlyList<UgasController> OverlapSphere(Vector3 center, float radius, in SpatialFilter filter)
        {
            Gather(center, radius * radius, false, default, 0f, filter);
            return _results;
        }

        public IReadOnlyList<UgasController> OverlapCone(Vector3 origin, Vector3 direction, float radius, float halfAngleDeg, in SpatialFilter filter)
        {
            Vector3 dir = direction.sqrMagnitude > 1e-8f ? direction.normalized : Vector3.forward;
            float cosHalfAngle = Mathf.Cos(Mathf.Clamp(halfAngleDeg, 0f, 180f) * Mathf.Deg2Rad);
            Gather(origin, radius * radius, true, dir, cosHalfAngle, filter);
            return _results;
        }

        public IReadOnlyList<UgasController> Nearest(Vector3 center, int count, in SpatialFilter filter)
        {
            Gather(center, float.PositiveInfinity, false, default, 0f, filter);
            int cap = count > 0 ? count : _results.Count;
            if (_results.Count > cap) _results.RemoveRange(cap, _results.Count - cap);
            return _results;
        }

        // Collects controllers within sqrRadius of center that pass the filter, ordered nearest-first
        // (tie-break by registration order), into the reused _results list. MaxResults is applied last.
        // When cone is true, candidates are additionally clipped to the half-angle about coneDir
        // (a normalized direction): a point is inside when dot(offset, coneDir) >= cos(half) * |offset|,
        // which avoids normalizing each offset. The apex point (offset ~ 0) is always inside.
        private void Gather(Vector3 center, float sqrRadius, bool cone, Vector3 coneDir, float cosHalfAngle, in SpatialFilter filter)
        {
            _scratch.Clear();
            _results.Clear();

            for (int i = 0; i < _anchors.Count; i++)
            {
                var gc = _anchors[i];
                if (gc == null) continue;

                Vector3 offset = gc.transform.position - center;
                float sqrDistance = offset.sqrMagnitude;
                if (sqrDistance > sqrRadius) continue;
                if (cone && sqrDistance > 1e-8f && Vector3.Dot(offset, coneDir) < cosHalfAngle * Mathf.Sqrt(sqrDistance)) continue;
                if (!Passes(gc, filter)) continue;

                _scratch.Add(new Candidate(gc, sqrDistance, i));
            }

            _scratch.Sort(CompareCandidates);

            int cap = filter.MaxResults > 0 ? filter.MaxResults : _scratch.Count;
            for (int i = 0; i < _scratch.Count && i < cap; i++) _results.Add(_scratch[i].Gc);
        }

        private static int CompareCandidates(Candidate x, Candidate y)
        {
            int byDistance = x.SqrDistance.CompareTo(y.SqrDistance);
            return byDistance != 0 ? byDistance : x.Order.CompareTo(y.Order);
        }

        private static bool Passes(UgasController gc, in SpatialFilter filter)
        {
            bool needRequire = filter.RequireTags != null && filter.RequireTags.Count > 0;
            bool needExclude = filter.ExcludeTags != null && filter.ExcludeTags.Count > 0;
            if (!needRequire && !needExclude) return true;

            var tags = gc.OwnedTags;
            if (tags == null) return !needRequire; // no tags: fails RequireTags; passes exclude-only

            if (needRequire && !tags.HasAllNamed(filter.RequireTags)) return false;
            if (needExclude && tags.HasAnyNamed(filter.ExcludeTags)) return false;
            return true;
        }
    }
}
