using System;
using System.Collections.Generic;
using Jbltx.Ugas.Definitions;
using Jbltx.Ugas.Kernel;

namespace Jbltx.Ugas.Runtime
{
    /// <summary>
    /// Manages a controller's gameplay effects (SPEC §9). Instant effects mutate base values
    /// immediately; HasDuration / Infinite effects are tracked as pooled
    /// <see cref="ActiveGameplayEffect"/> records and advanced over time. Re-homes the validated
    /// Instant/periodic/expiry logic onto Unity-native types.
    /// </summary>
    /// <remarks>
    /// Active-effect records are pooled to avoid per-application allocations. The
    /// <see cref="ExecutionPolicy.RunInSequence"/> / <see cref="ExecutionPolicy.RunInMerge"/>
    /// scheduling and custom executions remain stubbed (see TODOs); only
    /// <see cref="ExecutionPolicy.RunInParallel"/> is wired.
    /// </remarks>
    public sealed class GameplayEffectsSystem
    {
        private readonly IUgasRuntime _runtime;
        private readonly List<ActiveGameplayEffect> _active = new List<ActiveGameplayEffect>();
        private readonly Stack<ActiveGameplayEffect> _pool = new Stack<ActiveGameplayEffect>();
        private int _nextHandle = 1;

        // Float tolerance for period/duration boundaries: a 0.5s duration decremented by 0.1f five times
        // leaves a tiny positive residual, which would otherwise let a HasDuration effect live one period
        // too long (and fire an extra tick). Above float error, below any realistic sub-ms period.
        private const float Epsilon = 1e-4f;

        /// <summary>Raised when an Instant effect executes or a periodic tick fires (effect, level).</summary>
        public event Action<GameplayEffectDefinition, int> OnEffectExecuted;

        public GameplayEffectsSystem(IUgasRuntime runtime)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        }

        public IReadOnlyList<ActiveGameplayEffect> ActiveEffects => _active;

        /// <summary>
        /// Applies an effect. Instant effects mutate base values and return null; HasDuration /
        /// Infinite effects are tracked and the live (pooled) record is returned.
        /// </summary>
        public ActiveGameplayEffect ApplyEffect(GameplayEffectDefinition effect, int level = 1, int instigatorId = -1)
        {
            if (effect == null) throw new ArgumentNullException(nameof(effect));

            if (effect.DurationPolicy == DurationPolicy.Instant)
            {
                Execute(effect, level);
                return null;
            }

            // TODO: honor ExecutionPolicy. Only RunInParallel (independent instances) is wired;
            // RunInSequence should queue instances; RunInMerge should fold into one extending duration.
            var active = Rent();
            active.Handle = _nextHandle++;
            active.Definition = effect;
            active.Level = level;
            active.InstigatorId = instigatorId;

            if (effect.DurationPolicy == DurationPolicy.HasDuration)
            {
                active.HasDuration = true;
                active.RemainingDuration = _runtime.ResolveMagnitude(effect.Duration, level);
            }
            else
            {
                active.HasDuration = false;
                active.RemainingDuration = -1f;
            }

            _active.Add(active);

            var granted = effect.GrantedTags;
            for (int i = 0; i < granted.Count; i++) _runtime.GrantTag(granted[i]);

            if (active.IsPeriodic && effect.Period.ExecuteOnApplication)
            {
                Execute(effect, level);
                active.ExecutionCount++;
            }

            _runtime.RecalculateAttributes();
            return active;
        }

        /// <summary>Removes an active effect by handle. Returns true if one was removed.</summary>
        public bool RemoveEffect(int handle)
        {
            int idx = _active.FindIndex(a => a.Handle == handle);
            if (idx < 0) return false;
            RemoveAt(idx);
            return true;
        }

        /// <summary>
        /// Advances all active effects by <paramref name="deltaSeconds"/>: ticks periodic executions
        /// and expires HasDuration effects whose remaining duration reaches zero.
        /// </summary>
        public void Tick(float deltaSeconds)
        {
            // Iterate backwards so expiry-driven removals are index-safe and allocation-free.
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var active = _active[i];

                if (active.IsPeriodic)
                {
                    active.PeriodElapsed += deltaSeconds;
                    float period = active.Definition.Period.Period;
                    while (period > 0f && active.PeriodElapsed >= period - Epsilon)
                    {
                        active.PeriodElapsed -= period;
                        Execute(active.Definition, active.Level);
                        active.ExecutionCount++;
                    }
                }

                if (active.HasDuration)
                {
                    active.RemainingDuration -= deltaSeconds;
                    if (active.RemainingDuration <= Epsilon)
                    {
                        RemoveAt(i);
                    }
                }
            }
        }

        private void RemoveAt(int idx)
        {
            var active = _active[idx];
            _active.RemoveAt(idx);

            var granted = active.Definition.GrantedTags;
            for (int i = 0; i < granted.Count; i++) _runtime.RemoveGrantedTag(granted[i]);

            Return(active);
            _runtime.RecalculateAttributes();
        }

        // Applies an Instant effect's modifiers to base values, then fires side effects.
        private void Execute(GameplayEffectDefinition effect, int level)
        {
            var mods = effect.Modifiers;
            for (int i = 0; i < mods.Count; i++)
            {
                var mod = mods[i];
                float magnitude = _runtime.ResolveMagnitude(mod.Magnitude, level);
                switch (mod.Operation)
                {
                    case ModifierOp.Add:
                    case ModifierOp.AddPost:
                        _runtime.AddToBaseValue(mod.Attribute, magnitude);
                        break;
                    case ModifierOp.Override:
                        _runtime.SetBaseValue(mod.Attribute, magnitude);
                        break;
                    case ModifierOp.Multiply:
                        // TODO: define exact Instant-Multiply semantics against BaseValue.
                        break;
                }
            }

            // TODO: run custom Executions (calculation classes) here.

            _runtime.RecalculateAttributes();
            OnEffectExecuted?.Invoke(effect, level);
        }

        private ActiveGameplayEffect Rent()
        {
            var e = _pool.Count > 0 ? _pool.Pop() : new ActiveGameplayEffect();
            e.Reset();
            return e;
        }

        private void Return(ActiveGameplayEffect e)
        {
            e.Reset();
            _pool.Push(e);
        }
    }
}
