using System;
using System.Collections.Generic;
using Jbltx.Ugas.Definitions;
using Jbltx.Ugas.Kernel;
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
            return _abilities.TryGetValue(abilityName, out var ability) && ability.TryActivate(this);
        }

        // ---- Effects ----

        public ActiveGameplayEffect ApplyEffect(GameplayEffectDefinition effect, int level = 1)
        {
            EnsureInitialized();
            return _effects.ApplyEffect(effect, level, _instanceId);
        }

        public bool RemoveEffect(int handle)
        {
            EnsureInitialized();
            return _effects.RemoveEffect(handle);
        }

        // ---- Tick ----

        public void Tick(float deltaSeconds)
        {
            EnsureInitialized();
            _effects.Tick(deltaSeconds);
            // TODO(tasks): tick active ability tasks here (SPEC §10).
        }

        // ---- IUgasRuntime ----

        public float ResolveMagnitude(in MagnitudeDefinition magnitude, int level)
        {
            switch (magnitude.Type)
            {
                case MagnitudeType.ScalableFloat:
                    return magnitude.Value; // TODO: curve scaling by level.

                case MagnitudeType.AttributeBased:
                {
                    var backing = FindAttribute(magnitude.BackingAttribute);
                    float baseVal = backing?.CurrentValue ?? 0f;
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

            foreach (var set in _sets.Values)
            {
                foreach (var attr in set.Attributes)
                {
                    int count = GatherModifiers(attr.Name);
                    RuntimeAttributeSet.ResolveClamp(attr.Definition, resolveRef,
                        out bool hasMin, out float min, out bool hasMax, out float max);

                    attr.CurrentValue = AttributeKernel.Aggregate(
                        attr.BaseValue,
                        new ReadOnlySpan<ModifierSample>(_modBuffer, 0, count),
                        new Span<float>(_channelScratch),
                        hasMin, min, hasMax, max);
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

                    float magnitude = ResolveMagnitude(mod.Magnitude, rec.Level);
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
