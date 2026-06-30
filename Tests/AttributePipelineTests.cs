using Jbltx.Ugas.Attributes;
using Jbltx.Ugas.Schema;
using NUnit.Framework;

namespace Jbltx.Ugas.Tests
{
    /// <summary>
    /// Conformance: the dual-value attribute model and the SPEC §5 modifier-aggregation pipeline,
    /// including channel aggregation, Override resolution, and clamping. The headline case
    /// reproduces the RPG pack's worked example (WeaponDamage = 10 × 1.50 × 1.20 = 18.0).
    /// </summary>
    [TestFixture]
    public class AttributePipelineTests
    {
        private static AttributeModifier Add(string attr, float v, string channel = null) =>
            new AttributeModifier(attr, ModifierOperation.Add, v, channel);

        private static AttributeModifier Mul(string attr, float v, string channel = null) =>
            new AttributeModifier(attr, ModifierOperation.Multiply, v, channel);

        [Test]
        public void Add_IsPreMultiplyFlat()
        {
            float result = AttributeAggregator.Aggregate("HP", 100f, new[] { Add("HP", 25f) });
            Assert.That(result, Is.EqualTo(125f));
        }

        [Test]
        public void SameChannel_MultiplyBonuses_Sum()
        {
            // Two +20% in the same channel => x1.40 (not x1.44).
            float result = AttributeAggregator.Aggregate("D", 100f, new[]
            {
                Mul("D", 0.20f, "ChanA"),
                Mul("D", 0.20f, "ChanA")
            });
            Assert.That(result, Is.EqualTo(140f).Within(0.0001f));
        }

        [Test]
        public void DifferentChannels_MultiplyFactors_Multiply()
        {
            // x1.40 and x1.30 => x1.82.
            float result = AttributeAggregator.Aggregate("D", 100f, new[]
            {
                Mul("D", 0.40f, "ChanA"),
                Mul("D", 0.30f, "ChanB")
            });
            Assert.That(result, Is.EqualTo(182f).Within(0.0001f));
        }

        [Test]
        public void UnchanneledMultiply_IsIsolatedSingleton()
        {
            // Two unnamed Multiply modifiers each form their own channel => x1.2 * x1.2 = x1.44.
            float result = AttributeAggregator.Aggregate("D", 100f, new[]
            {
                Mul("D", 0.20f),
                Mul("D", 0.20f)
            });
            Assert.That(result, Is.EqualTo(144f).Within(0.0001f));
        }

        [Test]
        public void Override_HighestPriorityWins()
        {
            var mods = new[]
            {
                new AttributeModifier("D", ModifierOperation.Override, 10f, null, priority: 0, sequence: 1),
                new AttributeModifier("D", ModifierOperation.Override, 99f, null, priority: 100, sequence: 2),
                new AttributeModifier("D", ModifierOperation.Override, 5f, null, priority: -10, sequence: 3),
            };
            float result = AttributeAggregator.Aggregate("D", 100f, mods);
            Assert.That(result, Is.EqualTo(99f));
        }

        [Test]
        public void Override_LastAppliedWinsOnPriorityTie()
        {
            var mods = new[]
            {
                new AttributeModifier("D", ModifierOperation.Override, 10f, null, priority: 5, sequence: 1),
                new AttributeModifier("D", ModifierOperation.Override, 20f, null, priority: 5, sequence: 2),
            };
            float result = AttributeAggregator.Aggregate("D", 100f, mods);
            Assert.That(result, Is.EqualTo(20f)); // higher sequence (later) wins
        }

        [Test]
        public void Clamping_IsAppliedLast()
        {
            float result = AttributeAggregator.Aggregate("HP", 100f, new[] { Add("HP", 1000f) }, min: 0f, max: 150f);
            Assert.That(result, Is.EqualTo(150f));
        }

        [Test]
        public void FullPipeline_OrderIsAddThenMultiplyThenAddPost()
        {
            // (10 + 5) * (1 + 1.0) + 3 = 15 * 2 + 3 = 33
            var mods = new[]
            {
                Add("X", 5f),
                Mul("X", 1.0f, "C"),
                new AttributeModifier("X", ModifierOperation.AddPost, 3f),
            };
            float result = AttributeAggregator.Aggregate("X", 10f, mods);
            Assert.That(result, Is.EqualTo(33f).Within(0.0001f));
        }

        [Test]
        public void WorkedExample_BarbarianWeaponDamageIs18()
        {
            // Mirrors genres/rpg/entities/gameplay_controller.yaml:
            //   base 10  x MainStat(1 + 0.01*50 = 1.50)  x DamageBonuses(1 + 0.20 = 1.20) = 18.0
            var setDef = SchemaLoader.AttributeSetFromYaml(TestData.Read("rpg_attribute_set.yaml"));
            var mainStat = SchemaLoader.EffectFromYaml(TestData.Read("rpg_effect_mainstat_strength.yaml"));
            var fireSword = SchemaLoader.EffectFromYaml(TestData.Read("rpg_effect_weapon_firesword.yaml"));

            var gc = new GameplayController { OwnerActor = new ActorReference { ActorID = "Hero" } };
            gc.RegisterAttributeSet(new AttributeSet(setDef));
            gc.FindAttribute("Strength").BaseValue = 50f;
            gc.ApplyEffect(mainStat);
            gc.ApplyEffect(fireSword);
            gc.RecalculateAttributes();

            Assert.That(gc.FindAttribute("WeaponDamage").CurrentValue, Is.EqualTo(18.0f).Within(0.0001f));
        }

        [Test]
        public void Clamp_ResolvesAttributeReference_HealthToMaxHealth()
        {
            var setDef = SchemaLoader.AttributeSetFromYaml(TestData.Read("rpg_attribute_set.yaml"));
            var gc = new GameplayController { OwnerActor = new ActorReference { ActorID = "Hero" } };
            gc.RegisterAttributeSet(new AttributeSet(setDef));

            // Push Health base above MaxHealth; current value must clamp to MaxHealth's current value.
            gc.FindAttribute("MaxHealth").BaseValue = 200f;
            gc.FindAttribute("Health").BaseValue = 999f;
            gc.RecalculateAttributes();

            Assert.That(gc.FindAttribute("Health").CurrentValue, Is.EqualTo(200f));
        }
    }
}
