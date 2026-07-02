using System.Collections.Generic;

namespace Jbltx.Ugas.Spatial
{
    /// <summary>
    /// Restricts a spatial query's candidate set before distance tests are applied (SPEC §17.2). Tag
    /// tests use the hierarchical semantics of §7 via <see cref="Jbltx.Ugas.Tags.GameplayTagContainer"/>.
    /// </summary>
    /// <remarks>
    /// Tags are matched by <b>name</b>, not by interned handle, and are resolved against each
    /// <i>candidate's own</i> registry at match time. This is deliberate: a <c>GameplayTag</c> handle is a
    /// per-registry index (id N means the Nth tag interned into <i>that</i> registry), so a handle minted
    /// in the querier's registry is meaningless against a candidate that owns a different registry — the
    /// bug the shooter eval surfaced. Matching by name is sound across registries with no shared-registry
    /// requirement.
    /// <para>
    /// Affiliation (Allied/Hostile/…) from §17.2 is deferred: it depends on a title's team model, which
    /// this reference implementation does not prescribe. Express team membership as tags for now
    /// (e.g. <c>Faction.Enemy</c> in <see cref="RequireTags"/>).
    /// </para>
    /// </remarks>
    public struct SpatialFilter
    {
        /// <summary>Candidate must own ALL of these tags, by name (§7, hierarchical). Null/empty = no requirement.</summary>
        public IReadOnlyList<string> RequireTags;

        /// <summary>Candidate must own NONE of these tags, by name.</summary>
        public IReadOnlyList<string> ExcludeTags;

        /// <summary>Hard cap on results (0 = unbounded), applied after nearest-first ordering.</summary>
        public int MaxResults;

        /// <summary>An empty filter that matches every spatial candidate.</summary>
        public static SpatialFilter None => default;

        public static SpatialFilter Require(params string[] tags) => new SpatialFilter { RequireTags = tags };
    }
}
