using System;

namespace Jbltx.Ugas.Tags
{
    /// <summary>
    /// An interned hierarchical gameplay tag handle (SPEC §7). Wraps a single <see cref="int"/>
    /// index into a <see cref="GameplayTagRegistryRuntime"/>, so tag comparisons are integer
    /// compares rather than string compares, and the handle is a blittable value usable in DOTS
    /// components.
    /// </summary>
    /// <remarks>
    /// A tag handle is only meaningful relative to the registry that issued it. Resolve names to
    /// handles once (at load) via <see cref="GameplayTagRegistryRuntime.Resolve"/>, then pass handles
    /// around. <see cref="None"/> (id -1) is the invalid/none tag.
    /// </remarks>
    public readonly struct GameplayTag : IEquatable<GameplayTag>
    {
        /// <summary>The interned id, or -1 for the none tag.</summary>
        public readonly int Id;

        public GameplayTag(int id) => Id = id;

        /// <summary>The none/invalid tag.</summary>
        public static GameplayTag None => new GameplayTag(-1);

        /// <summary>True when this handle refers to a real registered tag.</summary>
        public bool IsValid => Id >= 0;

        public bool Equals(GameplayTag other) => Id == other.Id;
        public override bool Equals(object obj) => obj is GameplayTag t && Equals(t);
        public override int GetHashCode() => Id;
        public override string ToString() => IsValid ? $"GameplayTag(#{Id})" : "GameplayTag(None)";

        public static bool operator ==(GameplayTag a, GameplayTag b) => a.Id == b.Id;
        public static bool operator !=(GameplayTag a, GameplayTag b) => a.Id != b.Id;
    }
}
