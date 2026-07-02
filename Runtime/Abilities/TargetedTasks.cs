using Jbltx.Ugas.Definitions;
using Jbltx.Ugas.Runtime;
using Jbltx.Ugas.Spatial;

namespace Jbltx.Ugas.Abilities
{
    /// <summary>
    /// Applies an effect to a single acquired target (SPEC §10.3, the "spatial / WaitForTarget" family):
    /// the payload of a directed verb like a shot, a bolt, or a single-target debuff. Acquires the
    /// <b>nearest</b> controller within <c>MaxRange</c> of the instigator that matches the tag filter
    /// (e.g. <c>RequireTag: Faction.Hostile</c>), then applies the effect with the instigator as source.
    /// </summary>
    /// <remarks>
    /// This is the engine-agnostic reference model: auto-acquire the nearest matching target via a §17.2
    /// query. A title with real hit resolution (raycast / projectile) supplies the target through an
    /// <c>ExecCalc_HitResolution</c> seam instead; here the nearest-in-range query keeps the verb
    /// deterministically verifiable. Tag matching is by name (sound across registries, §7). Completes on
    /// its first evaluation whether or not a target was found, so the ability never hangs.
    /// </remarks>
    public sealed class ApplyEffectToTargetTask : AbilityTaskBase
    {
        private readonly UgasController _instigator;
        private readonly ISpatialQueryProvider _provider;
        private readonly GameplayEffectDefinition _effect;
        private readonly float _range;
        private readonly string _requireTag;
        private readonly string _ignoreTag;
        private readonly int _level;

        public override string Type => "ApplyEffectToTarget";

        /// <summary>The controller hit by the most recent execution, or null if none matched (for inspection/tests).</summary>
        public UgasController LastTarget { get; private set; }

        public ApplyEffectToTargetTask(
            UgasController instigator, ISpatialQueryProvider provider, GameplayEffectDefinition effect,
            float range, string requireTag, string ignoreTag, int level, float tickInterval = 0f, int priority = 0)
            : base(tickInterval, priority)
        {
            _instigator = instigator;
            _provider = provider;
            _effect = effect;
            _range = range;
            _requireTag = requireTag;
            _ignoreTag = ignoreTag;
            _level = level;
        }

        protected override void OnTick(float step)
        {
            if (_instigator != null && _provider != null && _effect != null && _range > 0f)
            {
                var filter = new SpatialFilter { MaxResults = 1 }; // the single nearest match (§17.2 ordering)
                if (!string.IsNullOrEmpty(_requireTag)) filter.RequireTags = new[] { _requireTag };
                if (!string.IsNullOrEmpty(_ignoreTag)) filter.ExcludeTags = new[] { _ignoreTag };

                var hits = _provider.OverlapSphere(_instigator.transform.position, _range, filter);
                if (hits.Count > 0)
                {
                    LastTarget = hits[0];
                    LastTarget.ApplyEffect(_effect, _level, _instigator); // instigator is the source (§9.4.2)
                }
            }

            Complete();
        }
    }

    /// <summary>
    /// Latent task that pauses the ability until a tag is present on the owner (SPEC §10.3 State-Based /
    /// <c>WaitTagAdded</c>) — e.g. "resume once <c>State.Reloaded</c> appears". Completes immediately if
    /// the tag is already present (§10.3). Matched by name against the owner's registry (§7).
    /// </summary>
    public sealed class WaitTagAddedTask : AbilityTaskBase
    {
        private readonly UgasController _owner;
        private readonly string _tagName;

        public override string Type => "WaitTagAdded";

        public WaitTagAddedTask(UgasController owner, string tagName, float tickInterval = 0f, int priority = 0)
            : base(tickInterval, priority)
        {
            _owner = owner;
            _tagName = tagName;
        }

        protected override void OnTick(float step)
        {
            // No owner or no tag to wait on → nothing to wait for; don't hang the ability.
            if (_owner == null || string.IsNullOrEmpty(_tagName)) { Complete(); return; }
            if (_owner.OwnedTags != null && _owner.OwnedTags.HasTag(_tagName)) Complete();
        }
    }
}
