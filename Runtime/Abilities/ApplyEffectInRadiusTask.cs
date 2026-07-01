using System.Collections.Generic;
using Jbltx.Ugas.Definitions;
using Jbltx.Ugas.Runtime;
using Jbltx.Ugas.Spatial;
using Jbltx.Ugas.Tags;
using UnityEngine;

namespace Jbltx.Ugas.Abilities
{
    /// <summary>
    /// The spatial ability task (SPEC §10.3 / §17.3): on its first evaluation it applies an effect to
    /// every actor within a radius of the instigator, skipping anyone owning the ignore tag, then
    /// completes. This is the runtime behind an authored <c>ApplyEffectToActorsInRadius</c> task — e.g.
    /// the Barbarian Whirlwind striking every enemy in range with its damage effect.
    /// </summary>
    /// <remarks>
    /// The target set is a single §17.2 query at execution time (a snapshot); each hit receives the
    /// effect through the normal §9 pipeline. If the instigator has no spatial provider, effect, or the
    /// effect can't be resolved, the task completes as a no-op rather than hanging the ability.
    /// </remarks>
    public sealed class ApplyEffectInRadiusTask : AbilityTaskBase
    {
        private readonly UgasController _instigator;
        private readonly ISpatialQueryProvider _provider;
        private readonly GameplayEffectDefinition _effect;
        private readonly float _radius;
        private readonly string _ignoreTag;
        private readonly int _level;

        public override string Type => "ApplyEffectToActorsInRadius";

        /// <summary>The set affected by the most recent execution (for inspection/tests); empty until it runs.</summary>
        public IReadOnlyList<UgasController> LastAffected { get; private set; } = System.Array.Empty<UgasController>();

        public ApplyEffectInRadiusTask(
            UgasController instigator, ISpatialQueryProvider provider, GameplayEffectDefinition effect,
            float radius, string ignoreTag, int level, float tickInterval = 0f, int priority = 0)
            : base(tickInterval, priority)
        {
            _instigator = instigator;
            _provider = provider;
            _effect = effect;
            _radius = radius;
            _ignoreTag = ignoreTag;
            _level = level;
        }

        protected override void OnTick(float step)
        {
            if (_instigator != null && _provider != null && _effect != null)
            {
                var filter = new SpatialFilter();
                if (!string.IsNullOrEmpty(_ignoreTag))
                {
                    var tag = _instigator.TagRegistry.Resolve(_ignoreTag);
                    if (tag.IsValid) filter.ExcludeTags = new[] { tag };
                }

                var hits = _provider.OverlapSphere(_instigator.transform.position, _radius, filter);
                // Snapshot before applying (§17.3 rule 1): the provider reuses its result buffer.
                var targets = new List<UgasController>(hits);
                for (int i = 0; i < targets.Count; i++) targets[i].ApplyEffect(_effect, _level);
                LastAffected = targets;
            }

            Complete();
        }
    }
}
