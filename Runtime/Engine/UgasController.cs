using System;
using System.Collections.Generic;
using Jbltx.Ugas.Abilities;
using Jbltx.Ugas.Cues;
using Jbltx.Ugas.Definitions;
using Jbltx.Ugas.Kernel;
using Jbltx.Ugas.Persistence;
using Jbltx.Ugas.Prediction;
using Jbltx.Ugas.Spatial;
using Jbltx.Ugas.Tags;
using UnityEngine;

namespace Jbltx.Ugas.Runtime
{
    /// <summary>
    /// The reference Gameplay Controller (SPEC §4) as a Unity <see cref="MonoBehaviour"/>: the
    /// authoritative container wiring the four pillars. It owns runtime attribute sets, the owned-tag
    /// container, granted abilities, and the effects system, and ticks them from <c>Update</c>.
    /// </summary>
    /// <remarks>
    /// <para>Aggregation uses the shared <see cref="AttributeKernel"/> over reusable buffers, so the
    /// per-frame recompute is allocation-free on the managed path. When the package is compiled with
    /// <c>com.unity.entities</c> present, a DOTS-accelerated provider can take over batched
    /// evaluation (see <c>Jbltx.Ugas.Dots</c>); behaviour is identical, the path is faster.</para>
    /// <para>Assign a <see cref="GameplayControllerConfig"/> in the inspector (authored or imported
    /// from a spec pack) and the controller bootstraps itself in <c>Awake</c>.</para>
    /// </remarks>
    [AddComponentMenu("UGAS/UGAS Controller")]
    [DisallowMultipleComponent]
    public sealed class UgasController : MonoBehaviour, IUgasRuntime
    {
        [Tooltip("Optional config asset; if set, the controller bootstraps from it in Awake.")]
        [SerializeField] private GameplayControllerConfig _config;

        [Tooltip("If true, the controller ticks itself from Update. Disable to drive Tick() manually.")]
        [SerializeField] private bool _tickFromUpdate = true;

        private readonly Dictionary<string, RuntimeAttributeSet> _sets = new Dictionary<string, RuntimeAttributeSet>();
        private readonly Dictionary<string, GameplayAbility> _abilities = new Dictionary<string, GameplayAbility>();
        private readonly ChannelTable _channels = new ChannelTable();

        private GameplayTagRegistryRuntime _tagRegistry;
        private GameplayTagContainer _ownedTags;
        private GameplayEffectsSystem _effects;
        // Unique per-controller source id for effect application (§9). A process-local counter, not
        // Object.GetInstanceID(): that API is deprecated in newer Unity (→ GetEntityId) and we only
        // need a stable unique id here, not Unity's actual instance id.
        private static int _nextInstanceId;
        private int _instanceId;
        private long _modifierSequence;

        // Reusable hot-path buffers (sized lazily; never freed) for kernel aggregation.
        private ModifierSample[] _modBuffer = new ModifierSample[16];
        private float[] _channelScratch = System.Array.Empty<float>();

        public GameplayTagContainer OwnedTags => _ownedTags;

        /// <summary>Fires for each GameplayCue notification this controller raises (SPEC §12); presentation subscribes.</summary>
        public event Action<GameplayCueEvent> OnGameplayCue;
        public GameplayEffectsSystem Effects => _effects;
        public IReadOnlyDictionary<string, RuntimeAttributeSet> AttributeSets => _sets;
        public GameplayTagRegistryRuntime TagRegistry => _tagRegistry;

        private void Awake()
        {
            EnsureInitialized();
            if (_config != null) Bootstrap(_config);
        }

        /// <summary>
        /// Idempotent setup of the controller's runtime systems. Called from Awake, and also from the
        /// public API so the controller is usable before Awake runs — e.g. in EditMode tests or editor
        /// tooling, where AddComponent does not invoke Awake.
        /// </summary>
        private void EnsureInitialized()
        {
            if (_effects != null) return;
            _instanceId = System.Threading.Interlocked.Increment(ref _nextInstanceId);
            _tagRegistry = _config != null && _config.TagRegistry != null
                ? _config.TagRegistry.BuildRuntime()
                : new GameplayTagRegistryRuntime();
            _ownedTags = new GameplayTagContainer(_tagRegistry);
            _effects = new GameplayEffectsSystem(this);
            _effects.OnCue += (tag, type, handle) => OnGameplayCue?.Invoke(new GameplayCueEvent(tag, type, this, handle));
        }

        private void Update()
        {
            if (_tickFromUpdate) Tick(Time.deltaTime);
        }

        /// <summary>Bootstraps attribute sets, starting values, granted abilities, and starting tags.</summary>
        public void Bootstrap(GameplayControllerConfig config)
        {
            EnsureInitialized();
            foreach (var setDef in config.AttributeSets)
            {
                if (setDef != null) RegisterAttributeSet(new RuntimeAttributeSet(setDef));
            }

            foreach (var sv in config.StartingAttributeValues)
            {
                if (_sets.TryGetValue(sv.SetName, out var set) && set.TryGet(sv.AttributeName, out var attr))
                {
                    attr.BaseValue = sv.BaseValue;
                }
            }

            foreach (var grant in config.GrantedAbilities)
            {
                if (grant.Ability != null) GrantAbility(grant.Ability, Mathf.Max(1, grant.Level));
            }

            foreach (var tag in config.StartingTags) _ownedTags.AddTag(tag);

            RecalculateAttributes();
        }

        // ---- Attribute sets ----

        public void RegisterAttributeSet(RuntimeAttributeSet set)
        {
            EnsureInitialized();
            foreach (var dep in set.Dependencies)
            {
                if (!_sets.ContainsKey(dep))
                    throw new UgasDependencyException($"AttributeSet '{set.Name}' requires '{dep}', which is not registered.");
            }
            _sets[set.Name] = set;
            // Pre-intern channels referenced by no effects yet; sized on demand during recompute.
            RecalculateAttributes();
        }

        public RuntimeAttribute FindAttribute(string attributeName)
        {
            foreach (var set in _sets.Values)
                if (set.TryGet(attributeName, out var attr)) return attr;
            return null;
        }

        public float GetCurrentValue(string attributeName) => FindAttribute(attributeName)?.CurrentValue ?? 0f;
        public float GetBaseValue(string attributeName) => FindAttribute(attributeName)?.BaseValue ?? 0f;

        // ---- Abilities ----

        public GameplayAbility GrantAbility(GameplayAbilityDefinition ability, int level = 1)
        {
            EnsureInitialized();
            var instance = new GameplayAbility(ability, level);
            instance.Grant(this);
            _abilities[ability.AbilityName] = instance;
            return instance;
        }

        public GameplayAbility GetAbility(string abilityName) =>
            _abilities.TryGetValue(abilityName, out var a) ? a : null;

        public bool TryActivateAbility(string abilityName)
        {
            EnsureInitialized();
            if (!_abilities.TryGetValue(abilityName, out var ability)) return false;

            // §8.7: refuse activation while a currently-active ability blocks this ability's identity tags.
            if (IsBlockedByActiveAbility(ability)) return false;

            if (!ability.TryActivate(this)) return false;

            // §8.6/§8.7: a successful activation cancels every active ability whose identity tags it names
            // (the combo cancel / commitment lockout). Done after activation so a failed activation cancels nothing.
            CancelActiveAbilitiesMatching(ability);
            return true;
        }

        // §8.7 BlockAbilitiesWithTags: true when some other active ability names one of this ability's AbilityTags.
        private bool IsBlockedByActiveAbility(GameplayAbility ability)
        {
            var identity = ability.AbilityTags;
            if (identity.Count == 0) return false;
            foreach (var other in _abilities.Values)
            {
                if (other == ability || other.State != AbilityState.Active) continue;
                if (TagsIntersect(other.BlockAbilitiesWithTags, identity)) return true;
            }
            return false;
        }

        // §8.6 CancelAbilitiesWithTags: cancel every active ability whose AbilityTags the activator names.
        private void CancelActiveAbilitiesMatching(GameplayAbility activator)
        {
            if (activator.CancelAbilitiesWithTags.Count == 0) return;
            List<GameplayAbility> toCancel = null;
            foreach (var other in _abilities.Values)
            {
                if (other == activator || other.State != AbilityState.Active) continue;
                if (TagsIntersect(activator.CancelAbilitiesWithTags, other.AbilityTags))
                    (toCancel ??= new List<GameplayAbility>()).Add(other);
            }
            if (toCancel != null)
                for (int i = 0; i < toCancel.Count; i++) toCancel[i].CancelAbility();
        }

        // Exact-identity intersection of two resolved tag-handle lists. All of a controller's abilities
        // resolve against this controller's registry, so handle equality is sound here (no cross-registry
        // comparison — cf. §7 / finding F3).
        private static bool TagsIntersect(IReadOnlyList<GameplayTag> a, IReadOnlyList<GameplayTag> b)
        {
            if (a == null || b == null || a.Count == 0 || b.Count == 0) return false;
            for (int i = 0; i < a.Count; i++)
                for (int j = 0; j < b.Count; j++)
                    if (a[i] == b[j]) return true;
            return false;
        }

        // ---- Effects ----

        public ActiveGameplayEffect ApplyEffect(GameplayEffectDefinition effect, int level = 1)
            => ApplyEffect(effect, level, null);

        /// <summary>
        /// Applies an effect from a given <paramref name="source"/> (the instigator), so Source-scaled
        /// magnitudes (§9.4.2) resolve against it — e.g. damage scaled by the attacker's WeaponDamage.
        /// A null source resolves everything against this controller.
        /// </summary>
        public ActiveGameplayEffect ApplyEffect(GameplayEffectDefinition effect, int level, IUgasRuntime source)
        {
            EnsureInitialized();
            if (!MeetsApplicationRequirements(effect)) return null; // §9 ApplicationRequiredTags gate
            return _effects.ApplyEffect(effect, level, _instanceId, source);
        }

        // §9: an effect applies only while the owner currently owns every ApplicationRequiredTag
        // (hierarchical, §7). Names resolve non-interning against the owner's registry — an unknown
        // required tag is, by definition, not owned, so the application is refused.
        private bool MeetsApplicationRequirements(GameplayEffectDefinition effect)
        {
            var required = effect.ApplicationRequiredTags;
            if (required == null || required.Count == 0) return true;
            for (int i = 0; i < required.Count; i++)
            {
                var tag = _tagRegistry.Find(required[i]);
                if (!tag.IsValid || !_ownedTags.HasTag(tag)) return false;
            }
            return true;
        }

        public bool RemoveEffect(int handle)
        {
            EnsureInitialized();
            return _effects.RemoveEffect(handle);
        }

        /// <summary>
        /// Removes the first active effect whose definition name matches (§10.3 RemoveEffectFromOwner
        /// payload). Returns false if no such effect is active.
        /// </summary>
        public bool RemoveEffectByName(string effectName)
        {
            EnsureInitialized();
            if (string.IsNullOrEmpty(effectName)) return false;
            var active = _effects.ActiveEffects;
            for (int i = 0; i < active.Count; i++)
                if (active[i].Definition != null && active[i].Definition.EffectName == effectName)
                    return _effects.RemoveEffect(active[i].Handle);
            return false;
        }

        /// <summary>
        /// Applies <paramref name="effect"/> over an area (SPEC §17.3): resolves the target set with a
        /// single §17.2 query about <paramref name="origin"/> at call time (a snapshot), then applies
        /// the effect to each matched controller — each honoring its own §9.6 execution policy exactly
        /// as a single-target application would. Returns the affected set, nearest-first.
        /// </summary>
        /// <remarks>
        /// The <paramref name="provider"/> is the spatial index the caller maintains — the engine
        /// binding owns it, keeping the controller free of global state. The radius resolves through
        /// <see cref="ResolveMagnitude"/> against THIS controller (the instigator), so an AttributeBased
        /// radius scales with the caster's stat (§17.3). Tag filters are matched by name against each
        /// candidate's own registry (§7), so they are sound across registries (see SpatialFilter). A Cone area is
        /// swept about this controller's facing (<c>transform.forward</c>) via <c>OverlapCone</c>.
        /// </remarks>
        public IReadOnlyList<UgasController> ApplyAreaEffect(
            GameplayEffectDefinition effect, Vector3 origin, ISpatialQueryProvider provider, int level = 1)
        {
            EnsureInitialized();
            if (effect == null || provider == null || !effect.HasArea)
                return Array.Empty<UgasController>();

            var area = effect.Area;
            float radius = ResolveMagnitude(area.Radius, level);
            var filter = new SpatialFilter
            {
                // Matched by name against each candidate's own registry (§7) — sound across registries; see SpatialFilter.
                RequireTags = area.RequireTags,
                ExcludeTags = area.ExcludeTags,
                MaxResults = area.MaxTargets,
            };

            var hits = area.Shape == AreaShape.Cone
                ? provider.OverlapCone(origin, transform.forward, radius, area.HalfAngleDeg, filter)
                : provider.OverlapSphere(origin, radius, filter);
            // §17.3 rule 1: snapshot the set before applying — the provider reuses its result buffer,
            // and applying effects must not observe a set mutated mid-iteration. This controller is the
            // source, so Source-scaled magnitudes (e.g. damage from the caster's WeaponDamage) resolve
            // against it, not the target (§9.4.2).
            var targets = new List<UgasController>(hits);
            for (int i = 0; i < targets.Count; i++) targets[i].ApplyEffect(effect, level, this);
            return targets;
        }

        // ---- Spatial binding + effect registry (for ability tasks, §10/§17) ----

        /// <summary>
        /// The spatial index this controller queries for area/perception gameplay (§17.2). Set by the
        /// engine binding (e.g. from a <see cref="Jbltx.Ugas.Spatial.UgasSpatialWorld"/>); ability tasks
        /// that resolve actors in a radius read it from their instigator.
        /// </summary>
        public ISpatialQueryProvider SpatialProvider { get; set; }

        /// <summary>
        /// The current input-action state for latent input tasks (§10.3 WaitInputRelease). Auto-wired by
        /// <see cref="Jbltx.Ugas.Input.UgasInputSystem"/> on construction (from the router's owner); may be
        /// set directly by the engine binding. Null when no input stack is attached — such tasks then
        /// complete immediately rather than hanging.
        /// </summary>
        public Jbltx.Ugas.Input.IInputStateSource InputState { get; set; }

        private Dictionary<string, GameplayEffectDefinition> _effectsByName;

        /// <summary>Registers an effect by its <see cref="GameplayEffectDefinition.EffectName"/> so tasks can resolve it by name (§10).</summary>
        public void RegisterEffect(GameplayEffectDefinition effect)
        {
            if (effect == null || string.IsNullOrEmpty(effect.EffectName)) return;
            (_effectsByName ??= new Dictionary<string, GameplayEffectDefinition>())[effect.EffectName] = effect;
        }

        /// <summary>Resolves an effect registered via <see cref="RegisterEffect"/>; null if unknown.</summary>
        public GameplayEffectDefinition ResolveEffect(string effectName)
            => _effectsByName != null && effectName != null && _effectsByName.TryGetValue(effectName, out var e) ? e : null;

        private Dictionary<string, IExecutionCalculation> _executions;
        private uint _executionSub;

        // Named curve tables for ScalableFloat magnitudes (§9.4.2 seam); adopter-supplied, like _executions.
        private Dictionary<string, System.Func<float, float>> _curves;

        /// <summary>
        /// Base seed for this controller's deterministic execution RNG (§13.8.1). Set it for reproducible
        /// prediction/tests; when 0 (the default) the process-local instance id is used.
        /// </summary>
        public ulong RandomSeed { get; set; }

        /// <summary>Registers an execution calculation by the name effects reference via <c>ExecutionClass</c> (§9.6).</summary>
        public void RegisterExecution(string name, IExecutionCalculation calculation)
        {
            if (string.IsNullOrEmpty(name) || calculation == null) return;
            (_executions ??= new Dictionary<string, IExecutionCalculation>())[name] = calculation;
        }

        /// <summary>Resolves a registered execution calculation; null if unknown.</summary>
        public IExecutionCalculation ResolveExecution(string name)
            => _executions != null && name != null && _executions.TryGetValue(name, out var c) ? c : null;

        /// <summary>A fresh deterministic RNG stream for the next execution — a disjoint sub-stream per §13.8.1.</summary>
        public UgasRandom NextExecutionRandom()
            => new UgasRandom(RandomSeed != 0 ? RandomSeed : (ulong)_instanceId, _executionSub++);

        // ---- Tick ----

        public void Tick(float deltaSeconds)
        {
            EnsureInitialized();
            _effects.Tick(deltaSeconds);
            foreach (var ability in _abilities.Values) ability.TickTasks(deltaSeconds);
        }

        // ---- Persistence (§14) ----

        /// <summary>
        /// Captures this controller's persistable state (SPEC §14.2): attribute base values, active
        /// (non-Instant) effects with their resumable timers, and directly-granted abilities. Current
        /// values and owned tags are derived and are NOT captured as authoritative state.
        /// </summary>
        public GCSnapshot CaptureSnapshot()
        {
            EnsureInitialized();
            var snapshot = new GCSnapshot { OwnerActorId = name };

            foreach (var set in _sets.Values)
                foreach (var attr in set.Attributes)
                    snapshot.Attributes.Add(new AttributeState { Set = set.Name, Name = attr.Name, BaseValue = attr.BaseValue });

            var active = _effects.ActiveEffects;
            for (int i = 0; i < active.Count; i++)
            {
                var ae = active[i];
                snapshot.ActiveEffects.Add(new ActiveEffectRecord
                {
                    Effect = ae.Definition,
                    Level = ae.Level,
                    HasDuration = ae.HasDuration,
                    RemainingDuration = ae.RemainingDuration,
                    PeriodElapsed = ae.PeriodElapsed,
                    ExecutionCount = ae.ExecutionCount,
                    Stacks = ae.Stacks,
                    InstigatorId = ae.InstigatorId,
                    Source = ae.Source, // in-memory live source, for source-scaled magnitude re-resolution (§9.4.2 / §14.3.2)
                });
            }

            foreach (var ability in _abilities.Values)
                snapshot.GrantedAbilities.Add(new AbilityGrant { Ability = ability.Definition, Level = ability.Level });

            // Directly-granted (loose) tags: owned explicit tags NOT contributed by an active effect's
            // GrantedTags (§14.2 / §7) — lifecycle, class, faction, quest flags. Effect-granted tags are
            // re-derived from the re-applied effects on restore, so excluding them avoids double-counting a
            // ref-counted tag.
            var effectGranted = new HashSet<string>();
            for (int i = 0; i < active.Count; i++)
            {
                var g = active[i].Definition.GrantedTags;
                for (int j = 0; j < g.Count; j++) effectGranted.Add(g[j]);
            }
            foreach (var tag in _ownedTags.ExplicitTags)
            {
                var tagName = _tagRegistry.GetName(tag);
                if (!string.IsNullOrEmpty(tagName) && !effectGranted.Contains(tagName)) snapshot.OwnedTags.Add(tagName);
            }

            return snapshot;
        }

        /// <summary>
        /// Restores state from a snapshot (SPEC §14.4): base values first, then active effects with
        /// resumed timers, then non-effect abilities, then a single recompute — so current values and
        /// owned tags are rederived. Restore onto a controller with the same attribute sets registered.
        /// </summary>
        public void RestoreSnapshot(GCSnapshot snapshot)
        {
            if (snapshot == null) return;
            EnsureInitialized();

            // 1. Base values (raw — recomputed at step 4; §14.4 step 1).
            for (int i = 0; i < snapshot.Attributes.Count; i++)
            {
                var a = snapshot.Attributes[i];
                var attr = FindAttribute(a.Name);
                if (attr != null) attr.BaseValue = a.BaseValue;
            }

            // 2. Re-apply active effects with resumed timers (§14.4 step 2), rebinding each effect's
            // instigator/source so source-scaled magnitudes (§9.4.2) re-derive against the original
            // instigator rather than the restoring controller (§14.3.2).
            for (int i = 0; i < snapshot.ActiveEffects.Count; i++)
            {
                var r = snapshot.ActiveEffects[i];
                _effects.RestoreActive(r.Effect, r.Level, r.HasDuration, r.RemainingDuration, r.PeriodElapsed, r.ExecutionCount, r.Stacks, r.Source, r.InstigatorId);
            }

            // 3. Re-grant abilities not already present (effect-granted ones come back via step 2).
            for (int i = 0; i < snapshot.GrantedAbilities.Count; i++)
            {
                var g = snapshot.GrantedAbilities[i];
                if (g.Ability != null && GetAbility(g.Ability.AbilityName) == null) GrantAbility(g.Ability, g.Level);
            }

            // 3b. Re-grant the directly-granted (loose) tags (§14.2 / §14.4) — lifecycle, class, faction,
            // quest flags. Effect-granted tags already returned via step 2; these are the disjoint remainder.
            for (int i = 0; i < snapshot.OwnedTags.Count; i++) _ownedTags.AddTag(snapshot.OwnedTags[i]);

            // 4. Recompute derived state (§14.4 step 4).
            RecalculateAttributes();
        }

        // ---- IUgasRuntime ----

        public float ResolveMagnitude(in MagnitudeDefinition magnitude, int level, IUgasRuntime source = null)
        {
            switch (magnitude.Type)
            {
                case MagnitudeType.ScalableFloat:
                {
                    // §9.4.2: Value × curve(CurveInput) when a named curve table is authored + registered
                    // (RegisterCurve — an engine seam like ExecCalc). No curve authored/registered → flat Value.
                    if (string.IsNullOrEmpty(magnitude.Curve) || _curves == null ||
                        !_curves.TryGetValue(magnitude.Curve, out var curve) || curve == null)
                        return magnitude.Value;
                    float input = string.IsNullOrEmpty(magnitude.CurveInput)
                        ? level
                        : (FindAttribute(magnitude.CurveInput)?.CurrentValue ?? level);
                    return magnitude.Value * curve(input);
                }

                case MagnitudeType.AttributeBased:
                {
                    // Source-scaled magnitudes (§9.4.2) read the instigator; otherwise this controller.
                    float baseVal = (source != null && magnitude.Source == MagnitudeSource.Source)
                        ? source.GetCurrentValue(magnitude.BackingAttribute)
                        : (FindAttribute(magnitude.BackingAttribute)?.CurrentValue ?? 0f);
                    float coeff = magnitude.Coefficient == 0f ? 1f : magnitude.Coefficient;
                    return (baseVal + magnitude.PreMultiplyAdditive) * coeff + magnitude.PostMultiplyAdditive;
                }

                case MagnitudeType.SetByCaller:
                    return magnitude.Value; // TODO: lookup by DataTag.

                case MagnitudeType.CustomCalculation:
                    return magnitude.Value; // TODO: invoke CalculatorClass.

                default:
                    return magnitude.Value;
            }
        }

        /// <summary>
        /// Registers a named curve table (SPEC §9.4.2): a ScalableFloat magnitude whose <c>Curve</c> names
        /// this table resolves to <c>Value × curve(CurveInput)</c>. The curve data is content, so — like the
        /// §9.6 ExecutionCalculation — it is an adopter-supplied seam, not shipped by the runtime.
        /// </summary>
        public void RegisterCurve(string name, System.Func<float, float> curve)
        {
            if (string.IsNullOrEmpty(name) || curve == null) return;
            _curves ??= new Dictionary<string, System.Func<float, float>>();
            _curves[name] = curve;
        }

        public void AddToBaseValue(string attributeName, float delta)
        {
            var attr = FindAttribute(attributeName);
            if (attr != null) { attr.BaseValue += delta; ClampBase(attr); }
        }

        public void SetBaseValue(string attributeName, float value)
        {
            var attr = FindAttribute(attributeName);
            if (attr != null) { attr.BaseValue = value; ClampBase(attr); }
        }

        public void MultiplyBaseValue(string attributeName, float magnitude)
        {
            var attr = FindAttribute(attributeName);
            if (attr != null) { attr.BaseValue *= 1f + magnitude; ClampBase(attr); }
        }

        public void GrantTag(string tag) { EnsureInitialized(); _ownedTags.AddTag(tag); }
        public void RemoveGrantedTag(string tag) { EnsureInitialized(); _ownedTags.RemoveTag(tag); }

        /// <summary>
        /// Recomputes every attribute's current value through the §5 kernel from the active-effect
        /// modifiers. Allocation-free: modifiers for each attribute are gathered into a reused buffer
        /// and aggregated via <see cref="AttributeKernel"/>.
        /// </summary>
        public void RecalculateAttributes()
        {
            EnsureInitialized();
            // Intern every channel referenced by active effects up front, so the scratch buffer is
            // sized to cover all channel ids before aggregation (a channel id >= scratch length would
            // otherwise be misread by the kernel as an implicit singleton channel).
            PreInternChannels();
            EnsureChannelScratch();
            var resolveRef = (Func<string, float?>)ResolveAttributeRef;

            // AttributeBased magnitudes (§9.4.2) and attribute-reference clamp bounds (§5.4) read OTHER
            // attributes' current values, so a single forward pass is order-dependent: a value derived from
            // an attribute declared later would read a stale current. Iterate the whole recompute to a fixed
            // point — repeat until no current value changes — so derived attributes resolve regardless of
            // declaration order. Capped to bound cyclic dependencies (best-effort: the last pass wins).
            const int MaxPasses = 8;
            const float Eps = 1e-5f;
            bool changed = true;
            for (int pass = 0; pass < MaxPasses && changed; pass++)
            {
                changed = false;
                foreach (var set in _sets.Values)
                {
                    foreach (var attr in set.Attributes)
                    {
                        int count = GatherModifiers(attr.Name);
                        RuntimeAttributeSet.ResolveClamp(attr.Definition, resolveRef,
                            out bool hasMin, out float min, out bool hasMax, out float max);

                        float next = AttributeKernel.Aggregate(
                            attr.BaseValue,
                            new ReadOnlySpan<ModifierSample>(_modBuffer, 0, count),
                            new Span<float>(_channelScratch),
                            hasMin, min, hasMax, max);

                        if (Mathf.Abs(next - attr.CurrentValue) > Eps) changed = true;
                        attr.CurrentValue = next;
                    }
                }
            }
        }

        private float? ResolveAttributeRef(string name)
        {
            var attr = FindAttribute(name);
            return attr != null ? attr.CurrentValue : (float?)null;
        }

        // Clamps an attribute's BASE value to its resolved [min,max] (incl. attribute-reference bounds
        // like Health -> MaxHealth). Instant/periodic modifiers mutate base directly (SPEC §9.2), so
        // without this an over-heal banks base above the max while only the derived current clamps.
        private void ClampBase(RuntimeAttribute attr)
        {
            RuntimeAttributeSet.ResolveClamp(attr.Definition, ResolveAttributeRef,
                out bool hasMin, out float min, out bool hasMax, out float max);
            float v = attr.BaseValue;
            if (hasMin && v < min) v = min;
            if (hasMax && v > max) v = max;
            attr.BaseValue = v;
        }

        // Fills _modBuffer with resolved modifiers for one attribute; returns the count.
        private int GatherModifiers(string attributeName)
        {
            int count = 0;
            var active = _effects.ActiveEffects;
            for (int e = 0; e < active.Count; e++)
            {
                var rec = active[e];
                var mods = rec.Definition.Modifiers;
                for (int m = 0; m < mods.Count; m++)
                {
                    var mod = mods[m];
                    if (mod.Attribute != attributeName) continue;

                    float magnitude = ResolveMagnitude(mod.Magnitude, rec.Level, rec.Source);
                    int channelId = _channels.GetOrAdd(mod.Channel);

                    for (int s = 0; s < rec.Stacks; s++)
                    {
                        if (count >= _modBuffer.Length) Array.Resize(ref _modBuffer, _modBuffer.Length * 2);
                        _modBuffer[count++] = new ModifierSample(
                            mod.Operation, magnitude, channelId, rec.Definition.Priority, _modifierSequence++);
                    }
                }
            }
            return count;
        }

        private void PreInternChannels()
        {
            var active = _effects.ActiveEffects;
            for (int e = 0; e < active.Count; e++)
            {
                var mods = active[e].Definition.Modifiers;
                for (int m = 0; m < mods.Count; m++)
                {
                    if (mods[m].Operation == ModifierOp.Multiply)
                        _channels.GetOrAdd(mods[m].Channel);
                }
            }
        }

        private void EnsureChannelScratch()
        {
            if (_channelScratch.Length < _channels.Count)
                _channelScratch = new float[Mathf.Max(_channels.Count, 4)];
        }
    }
}
