using System.Collections.Generic;
using Jbltx.Ugas.Runtime;
using UnityEngine;

namespace Jbltx.Ugas.Spatial
{
    /// <summary>
    /// The spatial query contract (SPEC §17.2): the engine seam that answers distance / overlap /
    /// nearest queries over the spatially-present controllers registered with it. UGAS defines this
    /// contract; the implementation supplies the index. This package ships a baseline
    /// <see cref="SimpleSpatialProvider"/>; a uniform-grid / DOTS-accelerated provider is the optional
    /// follow-up (§17.6), swappable behind this interface.
    /// </summary>
    /// <remarks>
    /// A controller is a spatial anchor (§17.1) only while registered; its position is read live from
    /// its transform. Results are ordered nearest-first with a deterministic tie-break (§17.2 rule 3),
    /// so queries are reproducible for predicted spatial gameplay (§17.7).
    /// </remarks>
    public interface ISpatialQueryProvider
    {
        /// <summary>Registers a controller as spatially present (an anchor, §17.1).</summary>
        void Register(UgasController gc);

        /// <summary>Removes a controller from the spatial index.</summary>
        void Unregister(UgasController gc);

        /// <summary>Straight-line distance between two world points, in engine units.</summary>
        float Distance(Vector3 a, Vector3 b);

        /// <summary>
        /// Registered controllers whose position lies within <paramref name="radius"/> of
        /// <paramref name="center"/> and match <paramref name="filter"/>, nearest-first.
        /// </summary>
        IReadOnlyList<UgasController> OverlapSphere(Vector3 center, float radius, in SpatialFilter filter);

        /// <summary>
        /// Registered controllers within a cone from <paramref name="origin"/> along
        /// <paramref name="direction"/> — inside <paramref name="radius"/> and within
        /// <paramref name="halfAngleDeg"/> of the axis — matching <paramref name="filter"/>, nearest-first.
        /// </summary>
        IReadOnlyList<UgasController> OverlapCone(Vector3 origin, Vector3 direction, float radius, float halfAngleDeg, in SpatialFilter filter);

        /// <summary>The <paramref name="count"/> nearest matching controllers to <paramref name="center"/>, nearest-first.</summary>
        IReadOnlyList<UgasController> Nearest(Vector3 center, int count, in SpatialFilter filter);
    }
}
