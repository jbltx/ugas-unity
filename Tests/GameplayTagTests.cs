using Jbltx.Ugas.Tags;
using NUnit.Framework;

namespace Jbltx.Ugas.Tests
{
    /// <summary>
    /// Conformance: hierarchical gameplay-tag container query semantics and reference counting
    /// (SPEC §7).
    /// </summary>
    [TestFixture]
    public class GameplayTagTests
    {
        [Test]
        public void HasTag_MatchesSelfAndAncestors_ButNotSiblings()
        {
            var c = new GameplayTagContainer(new[] { "State.Debuff.Stunned.Magic", "Status.Burning" });

            Assert.That(c.HasTag("State.Debuff.Stunned.Magic"), Is.True, "exact");
            Assert.That(c.HasTag("State.Debuff.Stunned"), Is.True, "parent");
            Assert.That(c.HasTag("State.Debuff"), Is.True, "grandparent");
            Assert.That(c.HasTag("State"), Is.True, "root");
            Assert.That(c.HasTag("State.Debuff.Stunned.Physical"), Is.False, "sibling not present");
        }

        [Test]
        public void HasTagExact_RequiresExactTag()
        {
            var c = new GameplayTagContainer(new[] { "State.Debuff.Stunned.Magic" });

            Assert.That(c.HasTagExact("State.Debuff.Stunned.Magic"), Is.True);
            Assert.That(c.HasTagExact("State.Debuff.Stunned"), Is.False, "parent is not exact");
        }

        [Test]
        public void HasAny_HasAll_HasNone()
        {
            var c = new GameplayTagContainer(new[] { "Status.Chilled", "Status.Vulnerable" });

            Assert.That(c.HasAny(new[] { "Status.Chilled", "Status.Frozen" }), Is.True);
            Assert.That(c.HasAll(new[] { "Status.Chilled", "Status.Vulnerable" }), Is.True);
            Assert.That(c.HasAll(new[] { "Status.Chilled", "Status.Frozen" }), Is.False);
            Assert.That(c.HasNone(new[] { "Immunity.Physical", "Immunity.Fire" }), Is.True);
        }

        [Test]
        public void ReferenceCounting_TagPersistsUntilFinalRemoval()
        {
            var c = new GameplayTagContainer();
            c.AddTag("State.Debuff.Burning");
            c.AddTag("State.Debuff.Burning"); // second grant

            Assert.That(c.GetTagCount("State.Debuff.Burning"), Is.EqualTo(2));

            c.RemoveTag("State.Debuff.Burning"); // first expires
            Assert.That(c.HasTag("State.Debuff.Burning"), Is.True, "still present after one removal");
            Assert.That(c.GetTagCount("State.Debuff.Burning"), Is.EqualTo(1));

            c.RemoveTag("State.Debuff.Burning"); // second expires
            Assert.That(c.HasTag("State.Debuff.Burning"), Is.False, "gone after final removal");
        }

        [Test]
        public void OnTagChanged_FiresOnlyOnZeroToOneAndOneToZero()
        {
            var c = new GameplayTagContainer();
            int added = 0, removed = 0;
            c.OnTagChanged += (tag, present) => { if (present) added++; else removed++; };

            c.AddTag("A.B");      // 0 -> 1 : added
            c.AddTag("A.B");      // 1 -> 2 : no event
            c.RemoveTag("A.B");   // 2 -> 1 : no event
            c.RemoveTag("A.B");   // 1 -> 0 : removed

            Assert.That(added, Is.EqualTo(1));
            Assert.That(removed, Is.EqualTo(1));
        }

        [Test]
        public void RemovingAbsentTag_DoesNotUnderflow()
        {
            var c = new GameplayTagContainer();
            Assert.DoesNotThrow(() => c.RemoveTag("Nope"));
            Assert.That(c.GetTagCount("Nope"), Is.EqualTo(0));
        }
    }
}
