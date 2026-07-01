using System.Collections.Generic;
using Jbltx.Ugas.Definitions;
using Jbltx.Ugas.Editor;
using Jbltx.Ugas.Runtime;
using Jbltx.Ugas.Scene;
using Jbltx.Ugas.Spatial;
using NUnit.Framework;
using UnityEngine;

namespace Jbltx.Ugas.Tests.Editor
{
    /// <summary>
    /// Conformance for scene composition (SPEC §18) via <see cref="SceneLoader"/>: loading a
    /// SceneDefinition spawns each placement at its pose from its controller config, applies startup
    /// state (attribute overrides → tags), registers regions before placements so occupants receive
    /// their zone tags on the first tick, and exposes instances + spawn points by name.
    /// </summary>
    [TestFixture]
    public class SceneLoaderTests
    {
        private readonly List<Object> _spawned = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        private AttributeSetDefinition RpgSet()
        {
            var so = ScriptableObject.CreateInstance<AttributeSetDefinition>();
            _spawned.Add(so);
            SpecEntityMapper.PopulateAttributeSet(so,
                (Jbltx.Ugas.Editor.Yaml.YamlMapping)Jbltx.Ugas.Editor.Yaml.YamlParser.Parse(SpecData.Read("rpg_attribute_set.yaml.txt")));
            return so;
        }

        private GameplayControllerConfig Config(string name, AttributeSetDefinition set)
        {
            var cfg = ScriptableObject.CreateInstance<GameplayControllerConfig>();
            _spawned.Add(cfg);
            cfg.SetAttributeSets(new List<AttributeSetDefinition> { set });
            cfg.Populate(new List<string>(), new List<GameplayControllerConfig.StartingAttribute>(), ControllerReplication.Mixed, name);
            return cfg;
        }

        [Test]
        public void Load_SpawnsPlacements_AppliesStartupState_ActivatesRegions()
        {
            var rpg = RpgSet();
            var playerConfig = Config("Player", rpg);
            var enemyConfig = Config("Enemy", rpg);

            var hazard = ScriptableObject.CreateInstance<RegionDefinition>();
            _spawned.Add(hazard);
            hazard.Populate("LavaPit", RegionShape.Sphere, 5f, new List<string> { "Zone.Hazard.Fire" });

            var scene = ScriptableObject.CreateInstance<SceneDefinition>();
            _spawned.Add(scene);
            scene.Populate("Arena",
                new List<ScenePlacement>
                {
                    new ScenePlacement { Controller = playerConfig, InstanceId = "player-1", Position = Vector3.zero, StartupTags = new List<string> { "Faction.Player" } },
                    new ScenePlacement
                    {
                        Controller = enemyConfig, InstanceId = "enemy-1", Position = new Vector3(3, 0, 0),
                        StartupTags = new List<string> { "Faction.Enemy" },
                        AttributeOverrides = new List<AttributeOverride> { new AttributeOverride { Attribute = "Health", BaseValue = 60f } },
                    },
                },
                new List<SceneRegionPlacement> { new SceneRegionPlacement { Region = hazard, Origin = Vector3.zero } },
                new List<SceneSpawnPoint> { new SceneSpawnPoint { Name = "Respawn", Position = new Vector3(0, 0, -8) } });

            var world = new UgasSpatialWorld();
            var loaded = SceneLoader.Load(scene, world);
            foreach (var gc in loaded.Instances) _spawned.Add(gc.gameObject); // register for teardown

            var player = loaded.Instance("player-1");
            var enemy = loaded.Instance("enemy-1");
            Assert.That(player, Is.Not.Null);
            Assert.That(enemy, Is.Not.Null);

            Assert.That(player.transform.position, Is.EqualTo(Vector3.zero));
            Assert.That(enemy.transform.position, Is.EqualTo(new Vector3(3, 0, 0)));
            Assert.That(player.OwnedTags.HasTag("Faction.Player"), Is.True);
            Assert.That(enemy.OwnedTags.HasTag("Faction.Enemy"), Is.True);
            Assert.That(enemy.GetBaseValue("Health"), Is.EqualTo(60f).Within(1e-4f), "attribute override applied after config defaults");
            Assert.That(loaded.SpawnPoint("Respawn"), Is.Not.Null);

            // Regions loaded before placements → one tick grants the hazard tag to both occupants (§18.3).
            world.Tick();
            Assert.That(player.OwnedTags.HasTag("Zone.Hazard.Fire"), Is.True);
            Assert.That(enemy.OwnedTags.HasTag("Zone.Hazard.Fire"), Is.True);
        }
    }
}
