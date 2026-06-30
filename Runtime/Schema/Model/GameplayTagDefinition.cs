using System;
using System.Collections.Generic;

namespace Jbltx.Ugas.Schema
{
    /// <summary>
    /// A single entry in a gameplay-tag registry. Mirrors one item of the <c>Tags</c> array in
    /// <c>schemas/gameplay_tag.yaml</c> (SPEC §7).
    /// </summary>
    [Serializable]
    public sealed class GameplayTagDefinition
    {
        /// <summary>Hierarchical tag in dot notation (e.g. <c>State.Debuff.Stunned</c>). Required.</summary>
        public string Tag;

        /// <summary>Human-readable description.</summary>
        public string Description;

        /// <summary>Whether multiple instances of this exact tag may be held at once.</summary>
        public bool AllowMultiple;

        /// <summary>Developer-only comment.</summary>
        public string DevComment;
    }

    /// <summary>
    /// A gameplay-tag registry: the declared, valid tag vocabulary for a project or genre pack.
    /// Mirrors the root object of <c>schemas/gameplay_tag.yaml</c>.
    /// </summary>
    [Serializable]
    public sealed class GameplayTagRegistry
    {
        public List<GameplayTagDefinition> Tags = new List<GameplayTagDefinition>();
    }
}
