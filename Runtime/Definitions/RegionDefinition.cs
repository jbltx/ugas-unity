using System.Collections.Generic;
using UnityEngine;

namespace Jbltx.Ugas.Definitions
{
    /// <summary>The volume shape of a region (SPEC §17.4).</summary>
    public enum RegionShape
    {
        Sphere = 0,
        Box = 1,
        Capsule = 2,
    }

    /// <summary>
    /// Authored data for a zone/region (SPEC §17.4): a standing volume that grants Gameplay Tags to the
    /// GCs whose anchor is inside it and removes them on exit. This asset holds the shape + the tags to
    /// grant; the world placement (origin) is supplied at runtime by
    /// <see cref="Jbltx.Ugas.Spatial.SpatialRegion"/>. Gameplay reacts to the granted tags — a
    /// <c>Zone.Hazard.Fire</c> tag gates a burning effect, a <c>Biome.Snow</c> tag gates cold exposure
    /// (§16.2) — never to the region directly (§3.1: state flows through tags and effects).
    /// </summary>
    [CreateAssetMenu(menuName = "UGAS/Region", fileName = "RegionDefinition")]
    public sealed class RegionDefinition : ScriptableObject
    {
        [SerializeField] private string _regionName;
        [SerializeField] private RegionShape _shape = RegionShape.Sphere;

        [Tooltip("Sphere / Capsule radius. Box half-extents & capsule endpoints are a follow-up; the provider resolves spheres today.")]
        [SerializeField] private float _radius = 1f;

        [Tooltip("Tags granted to a GC while its anchor is inside the region; removed on exit (§7.2 ref-counted).")]
        [SerializeField] private List<string> _grantedTags = new List<string>();

        public string RegionName => _regionName;
        public RegionShape Shape => _shape;
        public float Radius => _radius;
        public IReadOnlyList<string> GrantedTags => _grantedTags;

        /// <summary>Populates the asset (editor importer / authoring / tests).</summary>
        public void Populate(string regionName, RegionShape shape, float radius, List<string> grantedTags)
        {
            _regionName = regionName;
            _shape = shape;
            _radius = radius;
            _grantedTags = grantedTags ?? new List<string>();
        }
    }
}
