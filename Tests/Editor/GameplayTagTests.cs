using System.Collections.Generic;
using Jbltx.Ugas.Tags;
using NUnit.Framework;

namespace Jbltx.Ugas.Tests.Editor
{
    /// <summary>Conformance for the interned hierarchical tag container (SPEC §7).</summary>
    [TestFixture]
    public class GameplayTagTests
    {
        private GameplayTagRegistryRuntime _reg;
        private GameplayTagContainer _c;

        [SetUp]
        public void Setup()
        {
            _reg = new GameplayTagRegistryRuntime();
            _c = new GameplayTagContainer(_reg);
        }

        private List<GameplayTag> Tags(params string[] names)
        {
            var list = new List<GameplayTag>();
            foreach (var n in names) list.Add(_reg.Resolve(n));
            return list;
        }

        [Test]
        public void HasTag_MatchesSelfAndAncestors_NotSiblings()
        {
            _c.AddTag("State.Debuff.Stunned.Magic");
            Assert.That(_c.HasTag("State.Debuff.Stunned.Magic"), Is.True);
            Assert.That(_c.HasTag("State.Debuff.Stunned"), Is.True);
            Assert.That(_c.HasTag("State.Debuff"), Is.True);
            Assert.That(_c.HasTag("State"), Is.True);
            Assert.That(_c.HasTag("State.Debuff.Stunned.Physical"), Is.False);
        }

        [Test]
        public void HasTagExact_RequiresExact()
        {
            _c.AddTag("State.Debuff.Stunned.Magic");
            Assert.That(_c.HasTagExact("State.Debuff.Stunned.Magic"), Is.True);
            Assert.That(_c.HasTagExact("State.Debuff.Stunned"), Is.False);
        }

        [Test]
        public void HasAny_HasAll_HasNone()
        {
            _c.AddTag("Status.Chilled");
            _c.AddTag("Status.Vulnerable");
            Assert.That(_c.HasAny(Tags("Status.Chilled", "Status.Frozen")), Is.True);
            Assert.That(_c.HasAll(Tags("Status.Chilled", "Status.Vulnerable")), Is.True);
            Assert.That(_c.HasAll(Tags("Status.Chilled", "Status.Frozen")), Is.False);
            Assert.That(_c.HasNone(Tags("Immunity.Physical")), Is.True);
        }

        [Test]
        public void ReferenceCounting_PersistsUntilFinalRemoval()
        {
            _c.AddTag("State.Debuff.Burning");
            _c.AddTag("State.Debuff.Burning");
            Assert.That(_c.GetTagCount(_reg.Resolve("State.Debuff.Burning")), Is.EqualTo(2));
            _c.RemoveTag("State.Debuff.Burning");
            Assert.That(_c.HasTag("State.Debuff.Burning"), Is.True);
            _c.RemoveTag("State.Debuff.Burning");
            Assert.That(_c.HasTag("State.Debuff.Burning"), Is.False);
        }

        [Test]
        public void OnTagChanged_FiresOnlyOnZeroToOneAndOneToZero()
        {
            int added = 0, removed = 0;
            _c.OnTagChanged += (t, present) => { if (present) added++; else removed++; };
            _c.AddTag("A.B");
            _c.AddTag("A.B");
            _c.RemoveTag("A.B");
            _c.RemoveTag("A.B");
            Assert.That(added, Is.EqualTo(1));
            Assert.That(removed, Is.EqualTo(1));
        }

        [Test]
        public void Interning_YieldsStableHandle()
        {
            var a = _reg.Resolve("X.Y.Z");
            var b = _reg.Find("X.Y.Z");
            Assert.That(a.Id, Is.GreaterThanOrEqualTo(0));
            Assert.That(a.Id, Is.EqualTo(b.Id));
        }
    }
}
