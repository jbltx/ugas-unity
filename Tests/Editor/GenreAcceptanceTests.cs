using System.Collections.Generic;
using System.Linq;
using Jbltx.Ugas.Definitions;
using Jbltx.Ugas.Editor;
using Jbltx.Ugas.Kernel;
using Jbltx.Ugas.Runtime;
using Jbltx.Ugas.Spatial;
using NUnit.Framework;
using UnityEngine;

namespace Jbltx.Ugas.Tests.Editor
{
    /// <summary>
    /// Per-genre acceptance scenarios — the harness success oracle (roadmap #17). Each scenario
    /// exercises a genre's signature mechanic end to end against the live runtime and asserts an exact
    /// result; green-across ≙ "this kind of game works". These are the runnable form of the §16 case
    /// studies plus the §17 spatial scenarios, driven the same way the `ugas-harness` eval-sim drives
    /// them. Category "Acceptance" so the suite can be run on its own.
    /// </summary>
    [TestFixture]
    [Category("Acceptance")]
    public class GenreAcceptanceTests
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

        private GameplayEffectDefinition Effect(string file)
        {
            var so = ScriptableObject.CreateInstance<GameplayEffectDefinition>();
            _spawned.Add(so);
            SpecEntityMapper.PopulateEffect(so,
                (Jbltx.Ugas.Editor.Yaml.YamlMapping)Jbltx.Ugas.Editor.Yaml.YamlParser.Parse(SpecData.Read(file)));
            return so;
        }

        private UgasController Combatant(string name, Vector3 pos, float maxHealth = 100f)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            go.transform.position = pos;
            var gc = go.AddComponent<UgasController>();
            gc.RegisterAttributeSet(new RuntimeAttributeSet(RpgSet()));
            gc.FindAttribute("MaxHealth").BaseValue = maxHealth;
            gc.FindAttribute("Health").BaseValue = maxHealth;
            gc.RecalculateAttributes();
            return gc;
        }

        // A self-contained melee-hit effect: instant, subtracts `damage` from Health, applied over a
        // sphere (the whirlwind's ApplyEffectToActorsInRadius), sparing anyone tagged Immunity.Physical.
        private GameplayEffectDefinition WhirlwindHit(float damage, float radius)
        {
            var e = ScriptableObject.CreateInstance<GameplayEffectDefinition>();
            _spawned.Add(e);
            e.Populate("GE_WhirlwindHit", DurationPolicy.Instant, default, default, ExecutionPolicy.RunInParallel, 0,
                new List<ModifierDefinition>
                {
                    new ModifierDefinition { Attribute = "Health", Operation = ModifierOp.Add, Magnitude = MagnitudeDefinition.Scalable(-damage) },
                },
                null, null, null);
            e.SetArea(new AreaDefinition
            {
                Enabled = true,
                Shape = AreaShape.Sphere,
                Radius = MagnitudeDefinition.Scalable(radius),
                ExcludeTags = new List<string> { "Immunity.Physical" }, // whirlwind's IgnoreTargetsWithTag
            });
            return e;
        }

        private static string[] Names(IEnumerable<UgasController> gcs) => gcs.Select(g => g.name).ToArray();

        /// <summary>
        /// ARPG signature (§15.3/§16.3 Barbarian Whirlwind): a melee AoE strikes every enemy in radius
        /// for weapon damage, ignoring the physically-immune and anyone out of range. Exercises the
        /// §17.3 area application, §17.2 nearest-first ordering, and §7 exclude-tag filter together.
        /// </summary>
        [Test]
        public void Arpg_Whirlwind_StrikesEnemiesInRadius_SkipsImmuneAndDistant()
        {
            var barbarian = Combatant("Barbarian", Vector3.zero);
            var goblinA = Combatant("GoblinA", new Vector3(3, 0, 0));   // in radius
            var goblinB = Combatant("GoblinB", new Vector3(4, 0, 0));   // in radius
            var golem = Combatant("Golem", new Vector3(2, 0, 0));       // in radius but immune
            golem.GrantTag("Immunity.Physical");                        // sole tag → deterministic id parity with the filter
            var straggler = Combatant("Straggler", new Vector3(20, 0, 0)); // out of radius

            var world = new UgasSpatialWorld();
            world.Register(goblinA);
            world.Register(goblinB);
            world.Register(golem);
            world.Register(straggler);

            var struck = world.ApplyAreaEffect(barbarian, WhirlwindHit(18f, 5f), Vector3.zero);

            Assert.That(Names(struck), Is.EqualTo(new[] { "GoblinA", "GoblinB" }), "hits in-radius, non-immune enemies nearest-first");
            Assert.That(goblinA.GetBaseValue("Health"), Is.EqualTo(82f).Within(1e-4f));
            Assert.That(goblinB.GetBaseValue("Health"), Is.EqualTo(82f).Within(1e-4f));
            Assert.That(golem.GetBaseValue("Health"), Is.EqualTo(100f).Within(1e-4f), "physically immune → spared");
            Assert.That(straggler.GetBaseValue("Health"), Is.EqualTo(100f).Within(1e-4f), "out of range → spared");
        }

        /// <summary>
        /// Self-eval: prove the acceptance oracle has teeth. The correct scenario yields the expected
        /// number; a seeded fault (a damage effect that forgot its magnitude) yields a different one, so
        /// the same assertion that passes the correct build would reject the faulty one. A loop that can
        /// never fail is not verifying anything. (This project's eval-sim already caught real runtime
        /// bugs — base clamping and periodic timing — so the method demonstrably works.)
        /// </summary>
        [Test]
        public void SelfEval_SeededDamageFault_IsCaughtByOracle()
        {
            const float ExpectedHealthAfterHit = 82f; // 100 - 18

            // Correct build: the oracle passes.
            var caster = Combatant("Caster", Vector3.zero);
            var target = Combatant("Target", new Vector3(3, 0, 0));
            var world = new UgasSpatialWorld();
            world.Register(target);
            world.ApplyAreaEffect(caster, WhirlwindHit(18f, 5f), Vector3.zero);
            Assert.That(target.GetBaseValue("Health"), Is.EqualTo(ExpectedHealthAfterHit).Within(1e-4f), "correct build meets the oracle");

            // Seeded fault: the damage magnitude was left at zero. The very same oracle must reject it.
            var faultyCaster = Combatant("FaultyCaster", Vector3.zero);
            var faultyTarget = Combatant("FaultyTarget", new Vector3(3, 0, 0));
            var faultyWorld = new UgasSpatialWorld();
            faultyWorld.Register(faultyTarget);
            faultyWorld.ApplyAreaEffect(faultyCaster, WhirlwindHit(0f, 5f), Vector3.zero); // fault: 0 damage
            Assert.That(faultyTarget.GetBaseValue("Health"), Is.Not.EqualTo(ExpectedHealthAfterHit).Within(1e-4f),
                "seeded fault must diverge from the oracle — the acceptance check catches it");
        }

        private RegionDefinition RegionDef(string name, float radius, params string[] grantedTags)
        {
            var so = ScriptableObject.CreateInstance<RegionDefinition>();
            _spawned.Add(so);
            so.Populate(name, RegionShape.Sphere, radius, new List<string>(grantedTags));
            return so;
        }

        private UgasController Vehicle(string name, Vector3 pos, float maxSpeed)
        {
            var set = ScriptableObject.CreateInstance<AttributeSetDefinition>();
            _spawned.Add(set);
            set.Populate("Racing", null, new List<AttributeDefinition>
            {
                new AttributeDefinition { Name = "MaxSpeed", DefaultBaseValue = maxSpeed, Min = AttributeBound.FromLiteral(0f) },
            });

            var go = new GameObject(name);
            _spawned.Add(go);
            go.transform.position = pos;
            var gc = go.AddComponent<UgasController>();
            gc.RegisterAttributeSet(new RuntimeAttributeSet(set));
            return gc;
        }

        /// <summary>
        /// Racing signature (§16.2 biome effects): a surface region grants a biome tag to the vehicle
        /// inside it (§17.4), and that tag gates a speed penalty (§9 ApplicationRequiredTags). On mud the
        /// slow takes hold; on dry road the same effect is refused because the tag is absent. Ties zones →
        /// tags → tag-gated effects together.
        /// </summary>
        [Test]
        public void Racing_MudBiomeZone_TagGatesSpeedPenalty()
        {
            var car = Vehicle("Car", Vector3.zero, 100f);

            // While Biome.Mud is owned, MaxSpeed -40 (infinite until removed / refused).
            var mud = ScriptableObject.CreateInstance<GameplayEffectDefinition>();
            _spawned.Add(mud);
            mud.Populate("GE_MudSlow", DurationPolicy.Infinite, default, default, ExecutionPolicy.RunInParallel, 0,
                new List<ModifierDefinition> { new ModifierDefinition { Attribute = "MaxSpeed", Operation = ModifierOp.Add, Magnitude = MagnitudeDefinition.Scalable(-40f) } },
                null, new List<string> { "Biome.Mud" }, null); // ApplicationRequiredTags: Biome.Mud

            var world = new UgasSpatialWorld();
            world.Register(car);
            world.AddRegion(RegionDef("MudPatch", 5f, "Biome.Mud"), Vector3.zero);

            world.Tick();
            Assert.That(car.OwnedTags.HasTag("Biome.Mud"), Is.True, "inside the mud region");
            Assert.That(car.ApplyEffect(mud), Is.Not.Null, "gate passes while tagged");
            car.RecalculateAttributes();
            Assert.That(car.GetCurrentValue("MaxSpeed"), Is.EqualTo(60f).Within(1e-4f), "mud slows the car");

            car.transform.position = new Vector3(50, 0, 0); // dry road
            world.Tick();
            Assert.That(car.OwnedTags.HasTag("Biome.Mud"), Is.False, "left the mud region");
            Assert.That(car.ApplyEffect(mud), Is.Null, "the §9 gate refuses the slow off-mud");
        }

        /// <summary>
        /// Platformer signature (hazard volume): a spike region grants a hazard tag (§17.4) that gates a
        /// periodic damage effect (§9 ApplicationRequiredTags), so standing in the spikes ticks health
        /// down. Ties zones → tags → gated periodic effects.
        /// </summary>
        [Test]
        public void Platformer_SpikeHazardZone_DamagesOccupantEachPeriod()
        {
            var player = Combatant("Player", Vector3.zero); // RPG set, Health 100

            var spikes = ScriptableObject.CreateInstance<GameplayEffectDefinition>();
            _spawned.Add(spikes);
            spikes.Populate("GE_SpikeDamage", DurationPolicy.HasDuration, MagnitudeDefinition.Scalable(10f),
                new PeriodDefinition { Period = 0.5f, ExecuteOnApplication = false },
                ExecutionPolicy.RunInParallel, 0,
                new List<ModifierDefinition> { new ModifierDefinition { Attribute = "Health", Operation = ModifierOp.Add, Magnitude = MagnitudeDefinition.Scalable(-10f) } },
                null, new List<string> { "Zone.Hazard.Spikes" }, null); // gated on the hazard tag

            var world = new UgasSpatialWorld();
            world.Register(player);
            world.AddRegion(RegionDef("SpikePit", 5f, "Zone.Hazard.Spikes"), Vector3.zero);

            world.Tick();
            Assert.That(player.OwnedTags.HasTag("Zone.Hazard.Spikes"), Is.True, "standing in the spikes");
            Assert.That(player.ApplyEffect(spikes), Is.Not.Null, "gate passes inside the hazard");

            player.Tick(0.5f); // period 1: -10 -> 90
            player.Tick(0.5f); // period 2: -10 -> 80
            Assert.That(player.GetBaseValue("Health"), Is.EqualTo(80f).Within(1e-4f), "two ticks of spike damage");
        }

        /// <summary>
        /// The full ARPG chain (§8 → §10.3 → §17.3): activating the Barbarian's whirlwind ability runs
        /// its authored ApplyEffectToActorsInRadius task, which queries the instigator's spatial provider
        /// and applies the hit effect to every enemy in radius — sparing the physically immune and the
        /// out-of-range. Proves abilities drive spatial gameplay, not just manual ApplyAreaEffect calls.
        /// </summary>
        [Test]
        public void Arpg_WhirlwindAbility_ActivationDrivesAreaEffect()
        {
            var barbarian = Combatant("Barbarian", Vector3.zero);

            // The authored source-scaled hit: Health -= the ATTACKER's WeaponDamage (§9.4.2), not the target's.
            var basicAttack = Effect("rpg_effect_basic_attack_damage.yaml.txt");
            barbarian.RegisterEffect(basicAttack);
            barbarian.FindAttribute("WeaponDamage").BaseValue = 18f;
            barbarian.RecalculateAttributes();

            var world = new UgasSpatialWorld();
            barbarian.SpatialProvider = world.Provider; // engine binding hands the instigator its provider

            var goblinA = Combatant("GoblinA", new Vector3(3, 0, 0));
            var goblinB = Combatant("GoblinB", new Vector3(4, 0, 0));
            var golem = Combatant("Golem", new Vector3(2, 0, 0));
            golem.GrantTag("Immunity.Physical");
            var straggler = Combatant("Straggler", new Vector3(20, 0, 0));
            // Give the goblins their own WeaponDamage (5) so the result proves source-scaling: with the
            // attacker's 18 they land on 82, not the 95 the target's own 5 would produce.
            goblinA.FindAttribute("WeaponDamage").BaseValue = 5f; goblinA.RecalculateAttributes();
            goblinB.FindAttribute("WeaponDamage").BaseValue = 5f; goblinB.RecalculateAttributes();
            world.Register(goblinA);
            world.Register(goblinB);
            world.Register(golem);
            world.Register(straggler);

            // Author GA_Whirlwind with its signature task (matches rpg_ability_whirlwind.yaml.txt).
            var whirlwind = ScriptableObject.CreateInstance<GameplayAbilityDefinition>();
            _spawned.Add(whirlwind);
            whirlwind.Populate("GA_Whirlwind", default, new List<AbilityTaskDefinition>
            {
                new AbilityTaskDefinition
                {
                    Type = "ApplyEffectToActorsInRadius",
                    Params = new List<TaskParam>
                    {
                        new TaskParam { Key = "Radius", Value = "5" },
                        new TaskParam { Key = "EffectClass", Value = "GE_BasicAttackDamage" },
                        new TaskParam { Key = "IgnoreTargetsWithTag", Value = "Immunity.Physical" },
                    },
                },
            }, null, null);

            barbarian.GrantAbility(whirlwind);
            Assert.That(barbarian.TryActivateAbility("GA_Whirlwind"), Is.True, "no cost/cooldown/requirement → activates");
            barbarian.Tick(0.016f); // ticks the ability's tasks → the AoE fires

            Assert.That(goblinA.GetBaseValue("Health"), Is.EqualTo(82f).Within(1e-4f));
            Assert.That(goblinB.GetBaseValue("Health"), Is.EqualTo(82f).Within(1e-4f));
            Assert.That(golem.GetBaseValue("Health"), Is.EqualTo(100f).Within(1e-4f), "physically immune → spared");
            Assert.That(straggler.GetBaseValue("Health"), Is.EqualTo(100f).Within(1e-4f), "out of range → spared");
        }
    }
}
