using System;
using System.Collections.Generic;
using Jbltx.Ugas.Definitions;
using UnityEngine;

namespace Jbltx.Ugas.Scene
{
    /// <summary>A base-value override applied to a placed instance after its config defaults (§18.2).</summary>
    [Serializable]
    public sealed class AttributeOverride
    {
        public string Attribute;
        public float BaseValue;
    }

    /// <summary>
    /// A Gameplay Controller instance placed into a scene (SPEC §18.2): a controller config instanced
    /// at a world pose with startup state.
    /// </summary>
    [Serializable]
    public sealed class ScenePlacement
    {
        [Tooltip("The GameplayControllerConfig to instantiate.")]
        public GameplayControllerConfig Controller;

        [Tooltip("Stable identity — the persistence key and the handle for lookups.")]
        public string InstanceId;

        public Vector3 Position;
        public Vector3 EulerRotation;

        [Tooltip("Tags granted to the instance on spawn (§7).")]
        public List<string> StartupTags = new List<string>();

        [Tooltip("Effects applied to the instance on spawn (§9).")]
        public List<GameplayEffectDefinition> StartupEffects = new List<GameplayEffectDefinition>();

        [Tooltip("Base-value overrides applied after the config defaults (§5).")]
        public List<AttributeOverride> AttributeOverrides = new List<AttributeOverride>();

        [Tooltip("Whether the instance spawns active. Default true.")]
        public bool Enabled = true;
    }

    /// <summary>A §17.4 region placed at a world origin within the scene (§18.3).</summary>
    [Serializable]
    public sealed class SceneRegionPlacement
    {
        public RegionDefinition Region;
        public Vector3 Origin;
    }

    /// <summary>A named pose for dynamic spawning at runtime (SPEC §18.4).</summary>
    [Serializable]
    public sealed class SceneSpawnPoint
    {
        public string Name;
        public Vector3 Position;
        public Vector3 EulerRotation;
        public List<string> Tags = new List<string>();
    }

    /// <summary>
    /// Authored scene content (SPEC §18): the placements, regions, and spawn points a
    /// <see cref="Jbltx.Ugas.Scene.SceneLoader"/> instantiates into a world. The gameplay overlay —
    /// which controllers exist, where, and in what starting state — not level geometry.
    /// </summary>
    [CreateAssetMenu(menuName = "UGAS/Scene", fileName = "SceneDefinition")]
    public sealed class SceneDefinition : ScriptableObject
    {
        [SerializeField] private string _sceneName;
        [SerializeField] private List<ScenePlacement> _placements = new List<ScenePlacement>();
        [SerializeField] private List<SceneRegionPlacement> _regions = new List<SceneRegionPlacement>();
        [SerializeField] private List<SceneSpawnPoint> _spawnPoints = new List<SceneSpawnPoint>();

        public string SceneName => _sceneName;
        public IReadOnlyList<ScenePlacement> Placements => _placements;
        public IReadOnlyList<SceneRegionPlacement> Regions => _regions;
        public IReadOnlyList<SceneSpawnPoint> SpawnPoints => _spawnPoints;

        /// <summary>Populates the asset (editor importer / authoring / tests).</summary>
        public void Populate(string sceneName, List<ScenePlacement> placements,
            List<SceneRegionPlacement> regions, List<SceneSpawnPoint> spawnPoints)
        {
            _sceneName = sceneName;
            _placements = placements ?? new List<ScenePlacement>();
            _regions = regions ?? new List<SceneRegionPlacement>();
            _spawnPoints = spawnPoints ?? new List<SceneSpawnPoint>();
        }
    }
}
