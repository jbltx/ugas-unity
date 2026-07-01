using Jbltx.Ugas.Definitions;
using Jbltx.Ugas.Runtime;

namespace Jbltx.Ugas.Abilities
{
    /// <summary>
    /// Applies an effect to the ability's owner (SPEC §10.3): the payload of self-targeted verbs like
    /// gather (grant materials), consume (restore a need), or buff-self. Resolves its <c>EffectClass</c>
    /// against the instigator's effect registry and applies it, with the owner as the source.
    /// </summary>
    public sealed class ApplyEffectToOwnerTask : AbilityTaskBase
    {
        private readonly UgasController _owner;
        private readonly GameplayEffectDefinition _effect;
        private readonly int _level;

        public override string Type => "ApplyEffectToOwner";

        public ApplyEffectToOwnerTask(UgasController owner, GameplayEffectDefinition effect, int level, float tickInterval = 0f, int priority = 0)
            : base(tickInterval, priority)
        {
            _owner = owner;
            _effect = effect;
            _level = level;
        }

        protected override void OnTick(float step)
        {
            if (_owner != null && _effect != null) _owner.ApplyEffect(_effect, _level, _owner);
            Complete();
        }
    }

    /// <summary>
    /// Removes an active effect from the ability's owner by effect class (SPEC §10.3): the payload of
    /// verbs that clear a state — dropping a stance, cancelling a channel, ending a self-applied buff.
    /// Removes the first active effect whose definition name matches; a no-op if none is active.
    /// </summary>
    public sealed class RemoveEffectFromOwnerTask : AbilityTaskBase
    {
        private readonly UgasController _owner;
        private readonly string _effectName;

        public override string Type => "RemoveEffectFromOwner";

        public RemoveEffectFromOwnerTask(UgasController owner, string effectName, float tickInterval = 0f, int priority = 0)
            : base(tickInterval, priority)
        {
            _owner = owner;
            _effectName = effectName;
        }

        protected override void OnTick(float step)
        {
            if (_owner != null) _owner.RemoveEffectByName(_effectName);
            Complete();
        }
    }
}
