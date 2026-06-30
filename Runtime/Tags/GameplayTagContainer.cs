using System;
using System.Collections.Generic;

namespace Jbltx.Ugas.Tags
{
    /// <summary>
    /// A reference-counted hierarchical tag container implementing the query semantics of SPEC §7.
    /// </summary>
    /// <remarks>
    /// Per the spec, the container maintains two count maps rather than a simple set:
    /// <list type="bullet">
    /// <item><c>ExplicitTagCounts</c> — grant counts for tags added directly.</item>
    /// <item><c>AllTagCounts</c> — cumulative counts for every explicit tag <i>and</i> its ancestors,
    /// so hierarchical queries are answered in O(1).</item>
    /// </list>
    /// Adding <c>State.Debuff.Stunned.Magic</c> increments the explicit count for that tag and the
    /// "all" count for it plus <c>State.Debuff.Stunned</c>, <c>State.Debuff</c>, and <c>State</c>.
    /// <see cref="OnTagChanged"/> fires only on 0→1 and 1→0 transitions of an explicit tag.
    /// </remarks>
    public sealed class GameplayTagContainer
    {
        private readonly Dictionary<GameplayTag, int> _explicit = new Dictionary<GameplayTag, int>();
        private readonly Dictionary<GameplayTag, int> _all = new Dictionary<GameplayTag, int>();

        /// <summary>Raised when an explicit tag transitions present↔absent. (tag, isPresent).</summary>
        public event Action<GameplayTag, bool> OnTagChanged;

        public GameplayTagContainer() { }

        public GameplayTagContainer(IEnumerable<string> tags)
        {
            if (tags == null) return;
            foreach (var t in tags) AddTag(new GameplayTag(t));
        }

        /// <summary>Number of distinct explicit tags currently present.</summary>
        public int Count => _explicit.Count;

        /// <summary>True when no explicit tags are present.</summary>
        public bool IsEmpty => _explicit.Count == 0;

        /// <summary>All explicit tags currently present.</summary>
        public IEnumerable<GameplayTag> ExplicitTags => _explicit.Keys;

        /// <summary>
        /// Adds one grant of <paramref name="tag"/>, incrementing the explicit count and the "all"
        /// count of the tag and every ancestor. Fires <see cref="OnTagChanged"/> on the 0→1
        /// transition only.
        /// </summary>
        public void AddTag(GameplayTag tag)
        {
            if (!tag.IsValid) return;

            _explicit.TryGetValue(tag, out int prev);
            _explicit[tag] = prev + 1;

            foreach (var ancestor in tag.SelfAndAncestors())
            {
                _all.TryGetValue(ancestor, out int a);
                _all[ancestor] = a + 1;
            }

            if (prev == 0)
            {
                OnTagChanged?.Invoke(tag, true);
            }
        }

        /// <summary>
        /// Removes one grant of <paramref name="tag"/>. Counts never go below zero. Fires
        /// <see cref="OnTagChanged"/> on the 1→0 transition only.
        /// </summary>
        public void RemoveTag(GameplayTag tag)
        {
            if (!tag.IsValid) return;
            if (!_explicit.TryGetValue(tag, out int prev) || prev <= 0) return;

            if (prev == 1) _explicit.Remove(tag);
            else _explicit[tag] = prev - 1;

            foreach (var ancestor in tag.SelfAndAncestors())
            {
                if (_all.TryGetValue(ancestor, out int a))
                {
                    if (a <= 1) _all.Remove(ancestor);
                    else _all[ancestor] = a - 1;
                }
            }

            if (prev == 1)
            {
                OnTagChanged?.Invoke(tag, false);
            }
        }

        /// <summary>Explicit grant count for <paramref name="tag"/> (0 if absent).</summary>
        public int GetTagCount(GameplayTag tag) => _explicit.TryGetValue(tag, out int c) ? c : 0;

        /// <summary>
        /// Hierarchical match: true if <paramref name="tag"/> itself or any descendant of it is
        /// present. (<c>MatchesTag</c> in the spec.)
        /// </summary>
        public bool HasTag(GameplayTag tag) => tag.IsValid && _all.TryGetValue(tag, out int c) && c > 0;

        /// <summary>
        /// Exact match: true only if the exact <paramref name="tag"/> is explicitly present, with
        /// no hierarchy. (<c>MatchesTagExact</c> in the spec.)
        /// </summary>
        public bool HasTagExact(GameplayTag tag) => GetTagCount(tag) > 0;

        /// <summary>True if <i>any</i> tag in <paramref name="tags"/> is present (hierarchical).</summary>
        public bool HasAny(IEnumerable<GameplayTag> tags)
        {
            if (tags == null) return false;
            foreach (var t in tags) if (HasTag(t)) return true;
            return false;
        }

        /// <summary>True if <i>every</i> tag in <paramref name="tags"/> is present (hierarchical).</summary>
        public bool HasAll(IEnumerable<GameplayTag> tags)
        {
            if (tags == null) return true;
            foreach (var t in tags) if (!HasTag(t)) return false;
            return true;
        }

        /// <summary>True if <i>no</i> tag in <paramref name="tags"/> is present (hierarchical).</summary>
        public bool HasNone(IEnumerable<GameplayTag> tags) => !HasAny(tags);

        /// <summary>Convenience overload accepting raw tag strings.</summary>
        public bool HasAny(IEnumerable<string> tags) => HasAny(ToTags(tags));

        /// <summary>Convenience overload accepting raw tag strings.</summary>
        public bool HasAll(IEnumerable<string> tags) => HasAll(ToTags(tags));

        /// <summary>Convenience overload accepting raw tag strings.</summary>
        public bool HasNone(IEnumerable<string> tags) => HasNone(ToTags(tags));

        /// <summary>Resets all counts. Intended for teardown only.</summary>
        public void Clear()
        {
            _explicit.Clear();
            _all.Clear();
        }

        private static IEnumerable<GameplayTag> ToTags(IEnumerable<string> tags)
        {
            if (tags == null) yield break;
            foreach (var s in tags) yield return new GameplayTag(s);
        }
    }
}
