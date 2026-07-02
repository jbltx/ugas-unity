using System.Collections.Generic;
using Jbltx.Ugas.Runtime;
using Jbltx.Ugas.Spatial;
using UnityEngine;

namespace Jbltx.Ugas.Scene
{
    /// <summary>
    /// A loaded scene instance (SPEC §18): the controllers, regions, and spawn points a
    /// <see cref="SceneLoader"/> produced, with lookups by instance id / spawn-point name and an
    /// <see cref="Unload"/> that reverses the load.
    /// </summary>
    public sealed class LoadedScene
    {
        private readonly UgasSpatialWorld _world;
        private readonly List<UgasController> _spawned = new List<UgasController>();
        private readonly Dictionary<string, UgasController> _instances = new Dictionary<string, UgasController>();
        private readonly List<SpatialRegion> _regions = new List<SpatialRegion>();
        private readonly Dictionary<string, SceneSpawnPoint> _spawnPoints = new Dictionary<string, SceneSpawnPoint>();

        public LoadedScene(UgasSpatialWorld world) => _world = world;

        /// <summary>Every spawned instance, in placement order.</summary>
        public IReadOnlyList<UgasController> Instances => _spawned;

        /// <summary>The regions this scene added to the world.</summary>
        public IReadOnlyList<SpatialRegion> Regions => _regions;

        /// <summary>The instance placed under <paramref name="instanceId"/>, or null.</summary>
        public UgasController Instance(string instanceId)
            => instanceId != null && _instances.TryGetValue(instanceId, out var gc) ? gc : null;

        /// <summary>The spawn point named <paramref name="name"/>, or null.</summary>
        public SceneSpawnPoint SpawnPoint(string name)
            => name != null && _spawnPoints.TryGetValue(name, out var sp) ? sp : null;

        internal void AddInstance(string id, UgasController gc)
        {
            _spawned.Add(gc);
            if (!string.IsNullOrEmpty(id)) _instances[id] = gc;
        }

        internal void AddRegion(SpatialRegion region) => _regions.Add(region);

        internal void AddSpawnPoint(SceneSpawnPoint spawnPoint)
        {
            if (spawnPoint != null && !string.IsNullOrEmpty(spawnPoint.Name)) _spawnPoints[spawnPoint.Name] = spawnPoint;
        }

        /// <summary>
        /// Unloads the scene (SPEC §18.3): despawns instances (unregistering them from the world) and
        /// drops the scene's regions. Capture per-instance state via §14 before unloading if it must resume.
        /// </summary>
        public void Unload()
        {
            for (int i = 0; i < _spawned.Count; i++)
            {
                var gc = _spawned[i];
                if (gc == null) continue;
                _world.Unregister(gc);
                // DestroyImmediate off the play loop (EditMode tests / editor tooling); Destroy at runtime.
                // Object.Destroy is deferred and throws in edit mode, so a scene couldn't be torn down there.
                if (Application.isPlaying) Object.Destroy(gc.gameObject);
                else Object.DestroyImmediate(gc.gameObject);
            }
            for (int i = 0; i < _regions.Count; i++) _world.RemoveRegion(_regions[i]);
            _spawned.Clear();
            _instances.Clear();
            _regions.Clear();
            _spawnPoints.Clear();
        }
    }

    /// <summary>
    /// Instantiates a <see cref="SceneDefinition"/> into a <see cref="UgasSpatialWorld"/> (SPEC §18.3):
    /// regions first, then placements in declaration order (each applying startup state in the §18.2
    /// order — attribute overrides → tags → effects — and registering spatially), then spawn points.
    /// </summary>
    public static class SceneLoader
    {
        public static LoadedScene Load(SceneDefinition scene, UgasSpatialWorld world)
        {
            var loaded = new LoadedScene(world);
            if (scene == null || world == null) return loaded;

            // 1. Regions first — a zone is ready before an instance spawns into it (§18.3).
            var regions = scene.Regions;
            for (int i = 0; i < regions.Count; i++)
            {
                var rp = regions[i];
                if (rp != null && rp.Region != null) loaded.AddRegion(world.AddRegion(rp.Region, rp.Origin));
            }

            // 2. Placements in declaration order (§18.3).
            var placements = scene.Placements;
            for (int i = 0; i < placements.Count; i++)
            {
                var p = placements[i];
                if (p == null || !p.Enabled || p.Controller == null) continue;

                var go = new GameObject(string.IsNullOrEmpty(p.InstanceId) ? p.Controller.name : p.InstanceId);
                go.transform.SetPositionAndRotation(p.Position, Quaternion.Euler(p.EulerRotation));
                var gc = go.AddComponent<UgasController>();
                gc.Bootstrap(p.Controller);

                // Startup state order (§18.2 rule 3): attribute overrides → tags → effects.
                var overrides = p.AttributeOverrides;
                for (int o = 0; o < overrides.Count; o++)
                {
                    var attr = gc.FindAttribute(overrides[o].Attribute);
                    if (attr != null) attr.BaseValue = overrides[o].BaseValue;
                }
                gc.RecalculateAttributes();

                var tags = p.StartupTags;
                for (int t = 0; t < tags.Count; t++) gc.GrantTag(tags[t]);

                var effects = p.StartupEffects;
                for (int e = 0; e < effects.Count; e++) if (effects[e] != null) gc.ApplyEffect(effects[e]);

                world.Register(gc);
                loaded.AddInstance(p.InstanceId, gc);
            }

            // 3. Spawn points last — registered, they instantiate nothing (§18.4).
            var spawns = scene.SpawnPoints;
            for (int i = 0; i < spawns.Count; i++) loaded.AddSpawnPoint(spawns[i]);

            return loaded;
        }
    }
}
