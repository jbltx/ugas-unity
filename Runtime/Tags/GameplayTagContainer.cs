using System;
using System.Collections.Generic;

namespace Jbltx.Ugas.Tags
{
    /// <summary>
    /// A reference-counted hierarchical tag container over interned <see cref="GameplayTag"/>
    /// handles (SPEC §7). Re-homes the validated spec query semantics onto integer handles for fast,
    /// allocation-light queries.
    /// </summary>
    /// <remarks>
    /// Two count maps are maintained (both keyed by interned id):
    /// <list type="bullet">
    /// <item><c>explicit</c> — grant counts for tags added directly;</item>
    /// <item><c>all</c> — cumulative counts for each explicit tag <i>and</i> its registry-declared
    /// ancestors, so <see cref="HasTag"/> is O(1).</item>
    /// </list>
    /// <see cref="OnTagChanged"/> fires only on 0→1 and 1→0 transitions of an explicit tag.
    /// </remarks>
    public sealed class GameplayTagContainer
    {
        private readonly GameplayTagRegistryRuntime _registry;
        private readonly Dictionary<int, int> _explicit = new Dictionary<int, int>();
        private readonly Dictionary<int, int> _all = new Dictionary<int, int>();

        /// <summary>Raised when an explicit tag transitions present↔absent. (tag, isPresent).</summary>
        public event Action<GameplayTag, bool> OnTagChanged;

        public GameplayTagContainer(GameplayTagRegistryRuntime registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        /// <summary>The registry whose handles this container uses.</summary>
        public GameplayTagRegistryRuntime Registry => _registry;

        /// <summary>Number of distinct explicit tags currently present.</summary>
        public int Count => _explicit.Count;

        /// <summary>True when no explicit tags are present.</summary>
        public bool IsEmpty => _explicit.Count == 0;

        /// <summary>All explicit tags currently present.</summary>
        public IEnumerable<GameplayTag> ExplicitTags
        {
            get { foreach (var id in _explicit.Keys) yield return new GameplayTag(id); }
        }

        // ---- Mutation ----

        /// <summary>Adds one grant of <paramref name="tag"/>; increments self + ancestor "all" counts.</summary>
        public void AddTag(GameplayTag tag)
        {
            if (!tag.IsValid) return;

            _explicit.TryGetValue(tag.Id, out int prev);
            _explicit[tag.Id] = prev + 1;

            Increment(tag.Id);
            foreach (int ancestor in _registry.GetAncestors(tag)) Increment(ancestor);

            if (prev == 0) OnTagChanged?.Invoke(tag, true);
        }

        /// <summary>Removes one grant of <paramref name="tag"/>; counts never go below zero.</summary>
        public void RemoveTag(GameplayTag tag)
        {
            if (!tag.IsValid) return;
            if (!_explicit.TryGetValue(tag.Id, out int prev) || prev <= 0) return;

            if (prev == 1) _explicit.Remove(tag.Id);
            else _explicit[tag.Id] = prev - 1;

            Decrement(tag.Id);
            foreach (int ancestor in _registry.GetAncestors(tag)) Decrement(ancestor);

            if (prev == 1) OnTagChanged?.Invoke(tag, false);
        }

        /// <summary>Convenience: resolve <paramref name="name"/> against the registry and add it.</summary>
        public void AddTag(string name) => AddTag(_registry.Resolve(name));

        /// <summary>Convenience: resolve <paramref name="name"/> against the registry and remove it.</summary>
        public void RemoveTag(string name) => RemoveTag(_registry.Find(name));

        /// <summary>Resets all counts. Teardown only.</summary>
        public void Clear()
        {
            _explicit.Clear();
            _all.Clear();
        }

        // ---- Queries ----

        /// <summary>Explicit grant count for <paramref name="tag"/> (0 if absent).</summary>
        public int GetTagCount(GameplayTag tag) =>
            tag.IsValid && _explicit.TryGetValue(tag.Id, out int c) ? c : 0;

        /// <summary>Hierarchical match: true if <paramref name="tag"/> or any descendant is present.</summary>
        public bool HasTag(GameplayTag tag) =>
            tag.IsValid && _all.TryGetValue(tag.Id, out int c) && c > 0;

        /// <summary>Exact match: true only if the exact <paramref name="tag"/> is explicitly present.</summary>
        public bool HasTagExact(GameplayTag tag) => GetTagCount(tag) > 0;

        /// <summary>Hierarchical match by name.</summary>
        public bool HasTag(string name) => HasTag(_registry.Find(name));

        /// <summary>Exact match by name.</summary>
        public bool HasTagExact(string name) => HasTagExact(_registry.Find(name));

        /// <summary>True if <i>any</i> tag is present (hierarchical).</summary>
        public bool HasAny(IReadOnlyList<GameplayTag> tags)
        {
            if (tags == null) return false;
            for (int i = 0; i < tags.Count; i++) if (HasTag(tags[i])) return true;
            return false;
        }

        /// <summary>True if <i>every</i> tag is present (hierarchical).</summary>
        public bool HasAll(IReadOnlyList<GameplayTag> tags)
        {
            if (tags == null) return true;
            for (int i = 0; i < tags.Count; i++) if (!HasTag(tags[i])) return false;
            return true;
        }

        /// <summary>True if <i>no</i> tag is present (hierarchical).</summary>
        public bool HasNone(IReadOnlyList<GameplayTag> tags) => !HasAny(tags);

        /// <summary>
        /// True if <i>any</i> of the named tags is present (hierarchical). Each name is resolved against
        /// THIS container's own registry, so the check is sound regardless of which registry produced the
        /// list — unlike the handle overloads, whose ids are only meaningful within a single registry.
        /// </summary>
        public bool HasAnyNamed(IReadOnlyList<string> names)
        {
            if (names == null) return false;
            for (int i = 0; i < names.Count; i++) if (HasTag(names[i])) return true;
            return false;
        }

        /// <summary>True if <i>every</i> named tag is present (hierarchical), resolved against this container's registry.</summary>
        public bool HasAllNamed(IReadOnlyList<string> names)
        {
            if (names == null) return true;
            for (int i = 0; i < names.Count; i++) if (!HasTag(names[i])) return false;
            return true;
        }

        private void Increment(int id)
        {
            _all.TryGetValue(id, out int a);
            _all[id] = a + 1;
        }

        private void Decrement(int id)
        {
            if (_all.TryGetValue(id, out int a))
            {
                if (a <= 1) _all.Remove(id);
                else _all[id] = a - 1;
            }
        }
    }
}
