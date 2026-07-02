using System.Collections.Generic;
using Jbltx.Ugas.Runtime;
using UnityEngine;

namespace Jbltx.Ugas.Spatial
{
    /// <summary>
    /// A uniform-grid <see cref="ISpatialQueryProvider"/> (SPEC §17.6): anchors are bucketed by
    /// <c>floor(position / cellSize)</c> at <see cref="Rebuild"/> time, so radius queries scan only the
    /// cells overlapping the query bounds rather than every anchor — the sub-linear acceleration §17.6
    /// recommends, behind the same §17.2 interface as <see cref="SimpleSpatialProvider"/>.
    /// </summary>
    /// <remarks>
    /// Results are identical to <see cref="SimpleSpatialProvider"/> for the same inputs — nearest-first
    /// with the same registration-order tie-break — so the two are interchangeable and parity-testable.
    /// Positions are snapshotted at <see cref="Rebuild"/> (driven by <see cref="UgasSpatialWorld.Tick"/>
    /// on the ambient §10.6 cadence); queries between rebuilds see positions as of the last one (§17.6
    /// bounded staleness). <see cref="Nearest"/> is unbounded and scans all anchors — grid ring-expansion
    /// for it is a follow-up. A DOTS/Burst backend is a further, profiling-driven optimization.
    /// </remarks>
    public sealed class GridSpatialProvider : ISpatialQueryProvider
    {
        private readonly float _cellSize;
        private readonly List<UgasController> _anchors = new List<UgasController>();
        private readonly Dictionary<Vector3Int, List<UgasController>> _grid = new Dictionary<Vector3Int, List<UgasController>>();
        private readonly Dictionary<UgasController, int> _order = new Dictionary<UgasController, int>();
        private readonly List<Candidate> _scratch = new List<Candidate>();
        private readonly List<UgasController> _results = new List<UgasController>();

        private readonly struct Candidate
        {
            public readonly UgasController Gc;
            public readonly float SqrDistance;
            public readonly int Order;

            public Candidate(UgasController gc, float sqrDistance, int order)
            {
                Gc = gc;
                SqrDistance = sqrDistance;
                Order = order;
            }
        }

        public GridSpatialProvider(float cellSize = 10f)
        {
            _cellSize = cellSize > 0f ? cellSize : 10f;
        }

        public void Register(UgasController gc)
        {
            if (gc != null && !_anchors.Contains(gc)) _anchors.Add(gc);
        }

        public void Unregister(UgasController gc)
        {
            _anchors.Remove(gc);
        }

        /// <summary>Re-buckets every anchor from its current position (§17.6); also captures registration order for the tie-break.</summary>
        public void Rebuild()
        {
            _grid.Clear();
            _order.Clear();
            for (int i = 0; i < _anchors.Count; i++)
            {
                var gc = _anchors[i];
                if (gc == null) continue;
                _order[gc] = i;
                var cell = CellOf(gc.transform.position);
                if (!_grid.TryGetValue(cell, out var bucket))
                {
                    bucket = new List<UgasController>();
                    _grid[cell] = bucket;
                }
                bucket.Add(gc);
            }
        }

        public float Distance(Vector3 a, Vector3 b) => Vector3.Distance(a, b);

        public IReadOnlyList<UgasController> OverlapSphere(Vector3 center, float radius, in SpatialFilter filter)
        {
            GatherRadius(center, radius, false, default, 0f, filter);
            return _results;
        }

        public IReadOnlyList<UgasController> OverlapCone(Vector3 origin, Vector3 direction, float radius, float halfAngleDeg, in SpatialFilter filter)
        {
            Vector3 dir = direction.sqrMagnitude > 1e-8f ? direction.normalized : Vector3.forward;
            float cosHalfAngle = Mathf.Cos(Mathf.Clamp(halfAngleDeg, 0f, 180f) * Mathf.Deg2Rad);
            GatherRadius(origin, radius, true, dir, cosHalfAngle, filter);
            return _results;
        }

        public IReadOnlyList<UgasController> Nearest(Vector3 center, int count, in SpatialFilter filter)
        {
            _scratch.Clear();
            _results.Clear();
            for (int i = 0; i < _anchors.Count; i++)
            {
                var gc = _anchors[i];
                if (gc == null || !Passes(gc, filter)) continue;
                _scratch.Add(new Candidate(gc, (gc.transform.position - center).sqrMagnitude, i));
            }
            Emit(filter);
            int cap = count > 0 ? count : _results.Count;
            if (_results.Count > cap) _results.RemoveRange(cap, _results.Count - cap);
            return _results;
        }

        // Scans only the cells overlapping the query's bounding box, then culls by radius (+ cone) and filter.
        private void GatherRadius(Vector3 center, float radius, bool cone, Vector3 coneDir, float cosHalfAngle, in SpatialFilter filter)
        {
            _scratch.Clear();
            _results.Clear();

            float sqrRadius = radius * radius;
            var extent = new Vector3(radius, radius, radius);
            Vector3Int min = CellOf(center - extent);
            Vector3Int max = CellOf(center + extent);

            for (int x = min.x; x <= max.x; x++)
            for (int y = min.y; y <= max.y; y++)
            for (int z = min.z; z <= max.z; z++)
            {
                if (!_grid.TryGetValue(new Vector3Int(x, y, z), out var bucket)) continue;
                for (int i = 0; i < bucket.Count; i++)
                {
                    var gc = bucket[i];
                    if (gc == null) continue;

                    Vector3 offset = gc.transform.position - center;
                    float sqrDistance = offset.sqrMagnitude;
                    if (sqrDistance > sqrRadius) continue;
                    if (cone && sqrDistance > 1e-8f && Vector3.Dot(offset, coneDir) < cosHalfAngle * Mathf.Sqrt(sqrDistance)) continue;
                    if (!Passes(gc, filter)) continue;

                    _scratch.Add(new Candidate(gc, sqrDistance, OrderOf(gc)));
                }
            }

            Emit(filter);
        }

        // Sorts the scratch candidates nearest-first (registration-order tie-break) into _results, honoring MaxResults.
        private void Emit(in SpatialFilter filter)
        {
            _scratch.Sort(CompareCandidates);
            int cap = filter.MaxResults > 0 ? filter.MaxResults : _scratch.Count;
            for (int i = 0; i < _scratch.Count && i < cap; i++) _results.Add(_scratch[i].Gc);
        }

        private int OrderOf(UgasController gc) => _order.TryGetValue(gc, out var o) ? o : int.MaxValue;

        private Vector3Int CellOf(Vector3 p) => new Vector3Int(
            Mathf.FloorToInt(p.x / _cellSize),
            Mathf.FloorToInt(p.y / _cellSize),
            Mathf.FloorToInt(p.z / _cellSize));

        private static int CompareCandidates(Candidate a, Candidate b)
        {
            int byDistance = a.SqrDistance.CompareTo(b.SqrDistance);
            return byDistance != 0 ? byDistance : a.Order.CompareTo(b.Order);
        }

        private static bool Passes(UgasController gc, in SpatialFilter filter)
        {
            bool needRequire = filter.RequireTags != null && filter.RequireTags.Count > 0;
            bool needExclude = filter.ExcludeTags != null && filter.ExcludeTags.Count > 0;
            if (!needRequire && !needExclude) return true;

            var tags = gc.OwnedTags;
            if (tags == null) return !needRequire;

            if (needRequire && !tags.HasAllNamed(filter.RequireTags)) return false;
            if (needExclude && tags.HasAnyNamed(filter.ExcludeTags)) return false;
            return true;
        }
    }
}
