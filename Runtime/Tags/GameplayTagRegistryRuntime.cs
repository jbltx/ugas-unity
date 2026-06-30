using System.Collections.Generic;

namespace Jbltx.Ugas.Tags
{
    /// <summary>
    /// The runtime tag table that interns dot-notation tag strings to <see cref="GameplayTag"/>
    /// handles and precomputes each tag's ancestor chain (SPEC §7). Built once from a
    /// <see cref="Jbltx.Ugas.Definitions.GameplayTagRegistry"/> asset; thereafter all hierarchy
    /// queries are integer operations.
    /// </summary>
    /// <remarks>
    /// Interning means <c>State.Debuff.Stunned</c> resolves to a stable int id, and the registry
    /// records that its ancestors are <c>State.Debuff</c> and <c>State</c> (also interned). Ancestors
    /// are registered transitively even if a parent was not explicitly declared, so hierarchy queries
    /// are always well-formed.
    /// </remarks>
    public sealed class GameplayTagRegistryRuntime
    {
        private readonly List<string> _names = new List<string>();
        private readonly Dictionary<string, int> _ids = new Dictionary<string, int>();

        // For each tag id, the list of its ancestor ids (parent, grandparent, ... root), excluding self.
        private readonly List<int[]> _ancestors = new List<int[]>();

        /// <summary>Number of interned tags.</summary>
        public int Count => _names.Count;

        /// <summary>Resolves a tag name to a handle, interning it (and its ancestors) if new.</summary>
        public GameplayTag Resolve(string name)
        {
            if (string.IsNullOrEmpty(name)) return GameplayTag.None;
            return new GameplayTag(Intern(name));
        }

        /// <summary>Resolves a name to a handle without interning; returns None if unknown.</summary>
        public GameplayTag Find(string name)
        {
            if (!string.IsNullOrEmpty(name) && _ids.TryGetValue(name, out int id))
                return new GameplayTag(id);
            return GameplayTag.None;
        }

        /// <summary>The dot-notation name for a handle, or null for None/unknown.</summary>
        public string GetName(GameplayTag tag) =>
            tag.IsValid && tag.Id < _names.Count ? _names[tag.Id] : null;

        /// <summary>The interned ancestor ids of a tag (parent → root), excluding the tag itself.</summary>
        public int[] GetAncestors(GameplayTag tag) =>
            tag.IsValid && tag.Id < _ancestors.Count ? _ancestors[tag.Id] : System.Array.Empty<int>();

        private int Intern(string name)
        {
            if (_ids.TryGetValue(name, out int existing)) return existing;

            // Compute the ancestor chain first so parents are interned before this id is assigned.
            var ancestorIds = new List<int>();
            int dot = name.LastIndexOf('.');
            while (dot > 0)
            {
                string parent = name.Substring(0, dot);
                ancestorIds.Add(Intern(parent));
                dot = parent.LastIndexOf('.');
            }

            int id = _names.Count;
            _names.Add(name);
            _ids[name] = id;
            _ancestors.Add(ancestorIds.ToArray());
            return id;
        }
    }
}
