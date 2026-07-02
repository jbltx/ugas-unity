#if UGAS_DOTS
using System;
using System.Collections.Generic;
using Jbltx.Ugas.Runtime;
using Jbltx.Ugas.Spatial;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Jbltx.Ugas.Dots
{
    /// <summary>
    /// A Burst-accelerated <see cref="ISpatialQueryProvider"/> (SPEC §17.6): anchor positions are packed
    /// into a native array at <see cref="Rebuild"/>, and each overlap query runs a <c>[BurstCompile]</c>
    /// job that culls them geometrically in tight, GC-free, vectorised code. A managed post-step then
    /// applies the §7 tag filter and orders results — nearest-first with the same registration-order
    /// tie-break as <see cref="SimpleSpatialProvider"/>, so the two are interchangeable and parity-tested.
    /// </summary>
    /// <remarks>
    /// The whole assembly compiles only when <c>com.unity.entities</c> is present (asmdef
    /// <c>defineConstraints: ["UGAS_DOTS"]</c>), so the managed grid/simple providers remain the default
    /// with no hard dependency. This provider owns a persistent <see cref="NativeArray{T}"/>: dispose it
    /// (it implements <see cref="IDisposable"/>) when done. Its win is at large anchor counts; below that
    /// the managed <see cref="GridSpatialProvider"/> is simpler and comparable.
    /// </remarks>
    public sealed class DotsSpatialProvider : ISpatialQueryProvider, IDisposable
    {
        private readonly List<UgasController> _anchors = new List<UgasController>();
        private NativeArray<float3> _positions;
        private int _count;

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

        public void Register(UgasController gc)
        {
            if (gc != null && !_anchors.Contains(gc)) _anchors.Add(gc);
        }

        public void Unregister(UgasController gc)
        {
            _anchors.Remove(gc);
        }

        /// <summary>Packs anchors' current positions into the native array (§17.6 refresh point).</summary>
        public void Rebuild()
        {
            EnsureCapacity(_anchors.Count);
            _count = _anchors.Count;
            for (int i = 0; i < _count; i++)
            {
                var gc = _anchors[i];
                _positions[i] = gc != null ? (float3)gc.transform.position : new float3(float.PositiveInfinity);
            }
        }

        public float Distance(Vector3 a, Vector3 b) => Vector3.Distance(a, b);

        public IReadOnlyList<UgasController> OverlapSphere(Vector3 center, float radius, in SpatialFilter filter)
        {
            RunQuery(center, radius * radius, false, default, 0f, filter, int.MaxValue);
            return _results;
        }

        public IReadOnlyList<UgasController> OverlapCone(Vector3 origin, Vector3 direction, float radius, float halfAngleDeg, in SpatialFilter filter)
        {
            float3 dir = math.lengthsq((float3)direction) > 1e-8f ? math.normalize((float3)direction) : new float3(0, 0, 1);
            float cosHalfAngle = math.cos(math.radians(math.clamp(halfAngleDeg, 0f, 180f)));
            RunQuery(origin, radius * radius, true, dir, cosHalfAngle, filter, int.MaxValue);
            return _results;
        }

        public IReadOnlyList<UgasController> Nearest(Vector3 center, int count, in SpatialFilter filter)
        {
            RunQuery(center, float.PositiveInfinity, false, default, 0f, filter, count);
            return _results;
        }

        // Burst-culls the native positions, then applies the filter + ordering managed-side.
        private void RunQuery(Vector3 center, float sqrRadius, bool cone, float3 coneDir, float cosHalfAngle, in SpatialFilter filter, int nearestCount)
        {
            _scratch.Clear();
            _results.Clear();
            if (_count == 0) return;

            var hits = new NativeList<int>(Allocator.TempJob);
            var hitDist = new NativeList<float>(Allocator.TempJob);
            try
            {
                new CullJob
                {
                    Positions = _positions,
                    Count = _count,
                    Center = center,
                    SqrRadius = sqrRadius,
                    Cone = cone,
                    ConeDir = coneDir,
                    CosHalfAngle = cosHalfAngle,
                    Hits = hits,
                    HitDist = hitDist,
                }.Run();

                for (int i = 0; i < hits.Length; i++)
                {
                    int idx = hits[i];
                    var gc = _anchors[idx];
                    if (gc == null || !Passes(gc, filter)) continue;
                    _scratch.Add(new Candidate(gc, hitDist[i], idx));
                }
            }
            finally
            {
                hits.Dispose();
                hitDist.Dispose();
            }

            _scratch.Sort(CompareCandidates);

            int cap = filter.MaxResults > 0 ? filter.MaxResults : _scratch.Count;
            if (nearestCount > 0 && nearestCount < cap) cap = nearestCount;
            for (int i = 0; i < _scratch.Count && i < cap; i++) _results.Add(_scratch[i].Gc);
        }

        private void EnsureCapacity(int n)
        {
            if (_positions.IsCreated && _positions.Length >= n) return;
            if (_positions.IsCreated) _positions.Dispose();
            _positions = new NativeArray<float3>(math.max(n, 8), Allocator.Persistent);
        }

        public void Dispose()
        {
            if (_positions.IsCreated) _positions.Dispose();
        }

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

        [BurstCompile]
        private struct CullJob : IJob
        {
            [ReadOnly] public NativeArray<float3> Positions;
            public int Count;
            public float3 Center;
            public float SqrRadius;
            public bool Cone;
            public float3 ConeDir;
            public float CosHalfAngle;
            public NativeList<int> Hits;
            public NativeList<float> HitDist;

            public void Execute()
            {
                for (int i = 0; i < Count; i++)
                {
                    float3 offset = Positions[i] - Center;
                    float sqrDistance = math.lengthsq(offset);
                    if (sqrDistance > SqrRadius) continue;
                    if (Cone && sqrDistance > 1e-8f && math.dot(offset, ConeDir) < CosHalfAngle * math.sqrt(sqrDistance)) continue;
                    Hits.Add(i);
                    HitDist.Add(sqrDistance);
                }
            }
        }
    }
}
#endif
