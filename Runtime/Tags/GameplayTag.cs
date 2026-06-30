using System;
using System.Collections.Generic;

namespace Jbltx.Ugas.Tags
{
    /// <summary>
    /// An immutable hierarchical gameplay tag in dot notation (e.g. <c>State.Debuff.Stunned</c>),
    /// per SPEC §7. Two tags are equal when their (case-sensitive) names match.
    /// </summary>
    public readonly struct GameplayTag : IEquatable<GameplayTag>
    {
        /// <summary>The full dot-notation tag name. Empty for the default/none tag.</summary>
        public string Name { get; }

        public GameplayTag(string name)
        {
            Name = name ?? string.Empty;
        }

        /// <summary>True when this tag has no name (the "none" tag).</summary>
        public bool IsValid => !string.IsNullOrEmpty(Name);

        /// <summary>
        /// Enumerates this tag and all of its ancestors, leaf-first. For
        /// <c>State.Debuff.Stunned</c> this yields <c>State.Debuff.Stunned</c>,
        /// <c>State.Debuff</c>, then <c>State</c>.
        /// </summary>
        public IEnumerable<GameplayTag> SelfAndAncestors()
        {
            if (!IsValid) yield break;
            string name = Name;
            yield return new GameplayTag(name);
            int dot;
            while ((dot = name.LastIndexOf('.')) > 0)
            {
                name = name.Substring(0, dot);
                yield return new GameplayTag(name);
            }
        }

        /// <summary>
        /// True when this tag is the given <paramref name="other"/> tag or a descendant of it.
        /// e.g. <c>State.Debuff.Stunned</c> is within <c>State.Debuff</c>.
        /// </summary>
        public bool IsWithin(GameplayTag other)
        {
            if (!IsValid || !other.IsValid) return false;
            if (Name == other.Name) return true;
            return Name.Length > other.Name.Length
                   && Name.StartsWith(other.Name, StringComparison.Ordinal)
                   && Name[other.Name.Length] == '.';
        }

        public bool Equals(GameplayTag other) => string.Equals(Name, other.Name, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is GameplayTag t && Equals(t);
        public override int GetHashCode() => Name == null ? 0 : Name.GetHashCode();
        public override string ToString() => Name;

        public static implicit operator GameplayTag(string name) => new GameplayTag(name);
    }
}
