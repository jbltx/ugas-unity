using System;
using System.Collections.Generic;
using Jbltx.Ugas.Cues;
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
    /// Active-effect records are pooled to avoid per-application allocations. All three execution
    /// policies are wired: <see cref="ExecutionPolicy.RunInParallel"/> (independent instances),
    /// <see cref="ExecutionPolicy.RunInMerge"/> (stack + refresh duration on the existing instance),
    /// and <see cref="ExecutionPolicy.RunInSequence"/> (queue behind the active instance, promoted on
    /// expiry). Custom Executions (calculation classes) remain stubbed (see TODO in Execute).
    /// </remarks>
    public sealed class GameplayEffectsSystem
    {
        private readonly IUgasRuntime _runtime;
        private readonly List<ActiveGameplayEffect> _active = new List<ActiveGameplayEffect>();
        private readonly Stack<ActiveGameplayEffect> _pool = new Stack<ActiveGameplayEffect>();
        // RunInSequence: instances queued behind the active one, promoted to active on its expiry.
        private readonly List<ActiveGameplayEffect> _pending = new List<ActiveGameplayEffect>();
        private int _nextHandle = 1;

        // Float tolerance for period/duration boundaries: a 0.5s duration decremented by 0.1f five times
        // leaves a tiny positive residual, which would otherwise let a HasDuration effect live one period
        // too long (and fire an extra tick). Above float error, below any realistic sub-ms period.
        private const float Epsilon = 1e-4f;

        /// <summary>Raised when an Instant effect executes or a periodic tick fires (effect, level).</summary>
        public event Action<GameplayEffectDefinition, int> OnEffectExecuted;

        /// <summary>
        /// Raised for each of an effect's <c>GameplayCue.*</c> tags (SPEC §12): Execute on an instant /
        /// periodic execution (handle -1), Add when a durational effect activates, Remove when it ends
        /// (handle = the active-effect handle). Carries tags only — never presentation assets.
        /// </summary>
        public event Action<string, CueNotifyType, int> OnCue;

        public GameplayEffectsSystem(IUgasRuntime runtime)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        }

        public IReadOnlyList<ActiveGameplayEffect> ActiveEffects => _active;

        /// <summary>
        /// Applies an effect. Instant effects mutate base values and return null; HasDuration /
        /// Infinite effects are tracked and the live (pooled) record is returned.
        /// </summary>
        public ActiveGameplayEffect ApplyEffect(GameplayEffectDefinition effect, int level = 1, int instigatorId = -1, IUgasRuntime source = null)
        {
            if (effect == null) throw new ArgumentNullException(nameof(effect));

            if (effect.DurationPolicy == DurationPolicy.Instant)
            {
                Execute(effect, level, source);
                return null;
            }

            // Execution policy (§9.6) governs how re-applying an already-active effect combines.
            var existing = FindActive(effect);
            if (existing != null)
            {
                switch (effect.ExecutionPolicy)
                {
                    case ExecutionPolicy.RunInMerge:
                        // Fold into the existing instance: add a stack and refresh its duration + source.
                        existing.Stacks++;
                        existing.Source = source;
                        if (existing.HasDuration)
                            existing.RemainingDuration = _runtime.ResolveMagnitude(effect.Duration, level, source);
                        if (existing.IsPeriodic && effect.Period.ExecuteOnApplication)
                        {
                            Execute(effect, level, source);
                            existing.ExecutionCount++;
                        }
                        _runtime.RecalculateAttributes();
                        return existing;

                    case ExecutionPolicy.RunInSequence:
                        // Queue behind the active instance; promoted when it ends (see RemoveAt).
                        var queued = NewRecord(effect, level, instigatorId, source);
                        _pending.Add(queued);
                        return queued;

                    // RunInParallel: fall through and add an independent instance.
                }
            }

            return ActivateNew(effect, level, instigatorId, source);
        }

        private ActiveGameplayEffect FindActive(GameplayEffectDefinition effect)
        {
            for (int i = 0; i < _active.Count; i++)
                if (_active[i].Definition == effect) return _active[i];
            return null;
        }

        // Builds a pooled record without adding it to the active set.
        private ActiveGameplayEffect NewRecord(GameplayEffectDefinition effect, int level, int instigatorId, IUgasRuntime source)
        {
            var a = Rent();
            a.Handle = _nextHandle++;
            a.Definition = effect;
            a.Level = level;
            a.InstigatorId = instigatorId;
            a.Source = source;
            if (effect.DurationPolicy == DurationPolicy.HasDuration)
            {
                a.HasDuration = true;
                a.RemainingDuration = _runtime.ResolveMagnitude(effect.Duration, level, source);
            }
            else
            {
                a.HasDuration = false;
                a.RemainingDuration = -1f;
            }
            return a;
        }

        // Adds a record to the active set: grants tags, fires an apply-time periodic tick, recalculates.
        private ActiveGameplayEffect ActivateNew(GameplayEffectDefinition effect, int level, int instigatorId, IUgasRuntime source)
        {
            var active = NewRecord(effect, level, instigatorId, source);
            _active.Add(active);

            var granted = effect.GrantedTags;
            for (int i = 0; i < granted.Count; i++) _runtime.GrantTag(granted[i]);

            if (active.IsPeriodic && effect.Period.ExecuteOnApplication)
            {
                Execute(effect, level, source);
                active.ExecutionCount++;
            }

            // §12: a durational effect starts its looping cues on activation.
            var addCues = effect.GameplayCues;
            for (int i = 0; i < addCues.Count; i++) OnCue?.Invoke(addCues[i], CueNotifyType.Add, active.Handle);

            _runtime.RecalculateAttributes();
            return active;
        }

        /// <summary>Removes an active effect by handle. Returns true if one was removed.</summary>
        public bool RemoveEffect(int handle)
        {
            int idx = _active.FindIndex(a => a.Handle == handle);
            if (idx >= 0) { RemoveAt(idx); return true; }

            // May be a queued (RunInSequence) instance that has not activated yet.
            int p = _pending.FindIndex(a => a.Handle == handle);
            if (p >= 0) { Return(_pending[p]); _pending.RemoveAt(p); return true; }
            return false;
        }

        /// <summary>
        /// Re-applies a persisted active effect (SPEC §14.4 step 2) with resumed timers: the record is
        /// added with its saved RemainingDuration / PeriodElapsed / ExecutionCount / Stacks and its tags
        /// re-granted, then attributes recompute. It does NOT fire ExecuteOnApplication or re-execute the
        /// current period (§14.3.3 rule 2) — the effect resumes exactly where it was captured.
        /// </summary>
        public void RestoreActive(GameplayEffectDefinition effect, int level, bool hasDuration,
            float remainingDuration, float periodElapsed, int executionCount, int stacks)
        {
            if (effect == null) return;

            var a = Rent();
            a.Handle = _nextHandle++;
            a.Definition = effect;
            a.Level = level;
            a.HasDuration = hasDuration;
            a.RemainingDuration = hasDuration ? remainingDuration : -1f;
            a.PeriodElapsed = periodElapsed;
            a.ExecutionCount = executionCount;
            a.Stacks = stacks < 1 ? 1 : stacks;
            _active.Add(a);

            var granted = effect.GrantedTags;
            for (int i = 0; i < granted.Count; i++) _runtime.GrantTag(granted[i]);

            _runtime.RecalculateAttributes();
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
                        Execute(active.Definition, active.Level, active.Source);
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

            var endedDef = active.Definition;
            int endedHandle = active.Handle;
            var granted = endedDef.GrantedTags;
            for (int i = 0; i < granted.Count; i++) _runtime.RemoveGrantedTag(granted[i]);

            // §12: stop the effect's looping cues as it ends (handle captured before the record is pooled).
            var removeCues = endedDef.GameplayCues;
            for (int i = 0; i < removeCues.Count; i++) OnCue?.Invoke(removeCues[i], CueNotifyType.Remove, endedHandle);

            Return(active);

            // RunInSequence: promote the next queued instance of this definition, if any.
            if (endedDef.ExecutionPolicy == ExecutionPolicy.RunInSequence)
            {
                int p = _pending.FindIndex(e => e.Definition == endedDef);
                if (p >= 0)
                {
                    var next = _pending[p];
                    _pending.RemoveAt(p);
                    _active.Add(next);
                    for (int i = 0; i < granted.Count; i++) _runtime.GrantTag(granted[i]);
                    if (next.IsPeriodic && endedDef.Period.ExecuteOnApplication)
                    {
                        Execute(endedDef, next.Level, next.Source);
                        next.ExecutionCount++;
                    }
                }
            }

            _runtime.RecalculateAttributes();
        }

        // Applies an Instant effect's modifiers to base values, then fires side effects. Source-scaled
        // magnitudes (§9.4.2) resolve against the source/instigator when one is supplied.
        private void Execute(GameplayEffectDefinition effect, int level, IUgasRuntime source)
        {
            var mods = effect.Modifiers;
            for (int i = 0; i < mods.Count; i++)
            {
                var mod = mods[i];
                float magnitude = _runtime.ResolveMagnitude(mod.Magnitude, level, source);
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
                        // §5.2/§5.3: an *Instant* Multiply scales the Base Value by (1 + magnitude) — the
                        // same signed-bonus convention as the Current-Value pipeline (e.g. +1.0 doubles,
                        // -0.25 removes 25%). Previously an unhandled no-op. A DURATIONAL effect's Multiply
                        // is a Current-Value modifier (aggregated in RecalculateAttributes); it is NOT written
                        // to the base, including on the periodic ticks of a periodic durational effect (which
                        // execute only Add/AddPost/Override to the base) — otherwise it would double-count
                        // against its own current-value modifier and compound each tick.
                        if (effect.DurationPolicy == DurationPolicy.Instant)
                            _runtime.MultiplyBaseValue(mod.Attribute, magnitude);
                        break;
                }
            }

            // §9.6: run the effect's custom execution calculation, if any, with source/target + a
            // deterministic RNG stream, so stateful/branching math (mitigation, random rolls) applies.
            if (effect.HasExecution && _runtime is UgasController target)
            {
                var calc = target.ResolveExecution(effect.ExecutionClass);
                calc?.Execute(new ExecutionContext { Source = source, Target = target, Level = level, Rng = target.NextExecutionRandom() });
            }

            _runtime.RecalculateAttributes();
            OnEffectExecuted?.Invoke(effect, level);

            // §12: an instant execution or a periodic tick fires burst cues.
            var cues = effect.GameplayCues;
            for (int i = 0; i < cues.Count; i++) OnCue?.Invoke(cues[i], CueNotifyType.Execute, -1);
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
