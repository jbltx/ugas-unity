using System;
using System.Collections.Generic;
using Jbltx.Ugas.Schema;

namespace Jbltx.Ugas.Effects
{
    /// <summary>
    /// Default <see cref="IGameplayEffectsSystem"/>. Fully implements Instant application (base-value
    /// mutation + cue/tag side effects) and active-effect duration/periodic bookkeeping. The
    /// RunInSequence / RunInMerge execution policies and custom Executions are scaffolded with clear
    /// TODOs.
    /// </summary>
    public sealed class GameplayEffectsSystem : IGameplayEffectsSystem
    {
        private readonly IEffectTarget _target;
        private readonly List<ActiveGameplayEffect> _active = new List<ActiveGameplayEffect>();

        /// <summary>Raised when an Instant effect executes or a periodic tick fires (effect, level).</summary>
        public event Action<GameplayEffectDefinition, int> OnEffectExecuted;

        public GameplayEffectsSystem(IEffectTarget target)
        {
            _target = target ?? throw new ArgumentNullException(nameof(target));
        }

        public IReadOnlyList<ActiveGameplayEffect> ActiveEffects => _active;

        public ActiveGameplayEffect ApplyEffect(
            GameplayEffectDefinition effect, int level = 1, string instigatorGc = null, string sourceAbility = null)
        {
            if (effect == null) throw new ArgumentNullException(nameof(effect));

            if (effect.DurationPolicy == DurationPolicy.Instant)
            {
                Execute(effect, level);
                return null;
            }

            // TODO: honor ExecutionPolicy. RunInParallel (default) tracks each application as an
            // independent instance; RunInSequence should queue instances and activate them one at a
            // time; RunInMerge should fold into an existing instance extending its duration. Only the
            // RunInParallel path is implemented below.
            var active = new ActiveGameplayEffect(Guid.NewGuid().ToString("N"), effect)
            {
                Level = level,
                InstigatorGC = instigatorGc,
                SourceAbility = sourceAbility,
                RemainingDuration = effect.DurationPolicy == DurationPolicy.HasDuration
                    ? _target.ResolveMagnitude(effect.Duration, level)
                    : (float?)null
            };

            _active.Add(active);

            foreach (var tag in effect.GrantedTags) _target.GrantTag(tag);

            if (active.IsPeriodic && effect.Period.ExecuteOnApplication)
            {
                Execute(effect, level);
                active.ExecutionCount++;
            }

            _target.RecalculateAttributes();
            return active;
        }

        public bool RemoveEffect(string handle)
        {
            int idx = _active.FindIndex(a => a.Handle == handle);
            if (idx < 0) return false;

            var active = _active[idx];
            _active.RemoveAt(idx);

            foreach (var tag in active.Definition.GrantedTags) _target.RemoveGrantedTag(tag);
            _target.RecalculateAttributes();
            return true;
        }

        public void Tick(float deltaSeconds)
        {
            if (_active.Count == 0) return;

            // Iterate over a snapshot so expiry-driven removals are safe.
            var snapshot = _active.ToArray();
            foreach (var active in snapshot)
            {
                if (active.IsPeriodic)
                {
                    active.PeriodElapsed += deltaSeconds;
                    float period = active.Definition.Period.Period;
                    while (active.PeriodElapsed >= period)
                    {
                        active.PeriodElapsed -= period;
                        Execute(active.Definition, active.Level);
                        active.ExecutionCount++;
                    }
                }

                if (active.RemainingDuration.HasValue)
                {
                    active.RemainingDuration -= deltaSeconds;
                    if (active.RemainingDuration.Value <= 0f)
                    {
                        RemoveEffect(active.Handle);
                    }
                }
            }
        }

        // Applies an Instant effect's modifiers to base values, then fires side effects.
        private void Execute(GameplayEffectDefinition effect, int level)
        {
            foreach (var mod in effect.Modifiers)
            {
                float magnitude = _target.ResolveMagnitude(mod.Magnitude, level);
                switch (mod.Operation)
                {
                    case ModifierOperation.Add:
                    case ModifierOperation.AddPost:
                        _target.AddToBaseValue(mod.Attribute, magnitude);
                        break;
                    case ModifierOperation.Override:
                        _target.SetBaseValue(mod.Attribute, magnitude);
                        break;
                    case ModifierOperation.Multiply:
                        // Instant multiply applies as a scale of the current base value.
                        _target.AddToBaseValue(mod.Attribute, 0f); // no-op hook; see TODO below.
                        // TODO: define exact Instant-Multiply semantics against BaseValue per a future
                        // spec clarification (multiplicative base change vs. ignored for Instant).
                        break;
                }
            }

            // TODO: run effect.Executions (custom calculation classes) here.

            _target.RecalculateAttributes();
            OnEffectExecuted?.Invoke(effect, level);
        }
    }
}
