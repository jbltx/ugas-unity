using System.Collections.Generic;
using Jbltx.Ugas.Definitions;
using Jbltx.Ugas.Runtime;
using UnityEngine;

namespace Jbltx.Ugas.Spatial
{
    /// <summary>
    /// The engine binding for the World &amp; Spatial pillar (SPEC §17): a single turnkey object that
    /// owns a query provider, tracks the spatially-present controllers, and drives the standing spatial
    /// systems (zones §17.4 and perception §17.5). Register controllers as they spawn, add regions and
    /// observers as authored, then call <see cref="Tick"/> on the ambient §10.6 cadence to reconcile
    /// occupancy and awareness; query the <see cref="Provider"/> (or the AoE helper) for point-in-time
    /// spatial gameplay (§17.2/§17.3).
    /// </summary>
    /// <remarks>
    /// A plain driver, not a MonoBehaviour, so it is deterministic and testable (an engine binding may
    /// wrap it to tick from <c>Update</c>). The default provider is the managed
    /// <see cref="SimpleSpatialProvider"/>; pass a grid/DOTS-accelerated provider to the constructor to
    /// swap it behind the §17.2 interface without touching regions, perception, or gameplay code.
    /// </remarks>
    public sealed class UgasSpatialWorld
    {
        private readonly ISpatialQueryProvider _provider;
        private readonly List<SpatialRegion> _regions = new List<SpatialRegion>();
        private readonly List<Perception> _perceptions = new List<Perception>();

        public UgasSpatialWorld(ISpatialQueryProvider provider = null)
        {
            _provider = provider ?? new SimpleSpatialProvider();
        }

        /// <summary>The query index backing this world (§17.2).</summary>
        public ISpatialQueryProvider Provider => _provider;

        /// <summary>The regions this world evaluates each <see cref="Tick"/>.</summary>
        public IReadOnlyList<SpatialRegion> Regions => _regions;

        /// <summary>The observers this world evaluates each <see cref="Tick"/>.</summary>
        public IReadOnlyList<Perception> Perceptions => _perceptions;

        /// <summary>Marks a controller as spatially present (a §17.1 anchor).</summary>
        public void Register(UgasController gc) => _provider.Register(gc);

        /// <summary>Removes a controller from the spatial index.</summary>
        public void Unregister(UgasController gc) => _provider.Unregister(gc);

        /// <summary>Places a region from a definition at <paramref name="origin"/> and starts evaluating it.</summary>
        public SpatialRegion AddRegion(RegionDefinition definition, Vector3 origin)
        {
            var region = new SpatialRegion(definition, origin);
            _regions.Add(region);
            return region;
        }

        /// <summary>Stops evaluating a region (does not retroactively remove its granted tags).</summary>
        public bool RemoveRegion(SpatialRegion region) => _regions.Remove(region);

        /// <summary>Registers an observer with a perception definition and starts evaluating it.</summary>
        public Perception AddObserver(UgasController observer, PerceptionDefinition definition)
        {
            var perception = new Perception(observer, definition);
            _perceptions.Add(perception);
            return perception;
        }

        /// <summary>Stops evaluating an observer's perception.</summary>
        public bool RemoveObserver(Perception perception) => _perceptions.Remove(perception);

        /// <summary>
        /// Re-evaluates every region (occupancy → tag grants) and every observer's perception
        /// (awareness) against the current world state. Call on the ambient §10.6 spatial cadence.
        /// </summary>
        public void Tick()
        {
            _provider.Rebuild(); // refresh the spatial index from current positions (§17.6); no-op for live providers
            for (int i = 0; i < _regions.Count; i++) _regions[i].Evaluate(_provider);
            for (int i = 0; i < _perceptions.Count; i++) _perceptions[i].Evaluate(_provider);
        }

        /// <summary>
        /// Convenience for §17.3 area application through this world's provider: applies
        /// <paramref name="effect"/> around <paramref name="origin"/> to the matching anchors.
        /// </summary>
        public IReadOnlyList<UgasController> ApplyAreaEffect(
            UgasController instigator, GameplayEffectDefinition effect, Vector3 origin, int level = 1)
            => instigator.ApplyAreaEffect(effect, origin, _provider, level);
    }
}
