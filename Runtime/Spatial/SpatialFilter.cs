using System.Collections.Generic;
using Jbltx.Ugas.Tags;

namespace Jbltx.Ugas.Spatial
{
    /// <summary>
    /// Restricts a spatial query's candidate set before distance tests are applied (SPEC §17.2). Tag
    /// tests use the hierarchical semantics of §7 via <see cref="GameplayTagContainer"/>.
    /// </summary>
    /// <remarks>
    /// Affiliation (Allied/Hostile/…) from §17.2 is deferred: it depends on a title's team model, which
    /// this reference implementation does not prescribe. Express team membership as tags for now
    /// (e.g. <c>Faction.Enemy</c> in <see cref="RequireTags"/>).
    /// </remarks>
    public struct SpatialFilter
    {
        /// <summary>Candidate must own ALL of these tags (§7, hierarchical). Null/empty = no requirement.</summary>
        public IReadOnlyList<GameplayTag> RequireTags;

        /// <summary>Candidate must own NONE of these tags.</summary>
        public IReadOnlyList<GameplayTag> ExcludeTags;

        /// <summary>Hard cap on results (0 = unbounded), applied after nearest-first ordering.</summary>
        public int MaxResults;

        /// <summary>An empty filter that matches every spatial candidate.</summary>
        public static SpatialFilter None => default;

        public static SpatialFilter Require(params GameplayTag[] tags) => new SpatialFilter { RequireTags = tags };
    }
}
