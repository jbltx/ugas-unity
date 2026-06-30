using System;
using System.Collections.Generic;
using Jbltx.Ugas.Kernel;
using UnityEngine;

namespace Jbltx.Ugas.Definitions
{
    /// <summary>How a single numeric magnitude (a duration or a modifier amount) is resolved (SPEC §9).</summary>
    [Serializable]
    public struct MagnitudeDefinition
    {
        public MagnitudeType Type;

        [Tooltip("Static value for ScalableFloat / fallback for other types.")]
        public float Value;

        [Header("AttributeBased")]
        public string BackingAttribute;
        public MagnitudeSource Source;
        public float Coefficient;
        public float PreMultiplyAdditive;
        public float PostMultiplyAdditive;

        [Header("CustomCalculation / SetByCaller")]
        public string CalculatorClass;
        public string DataTag;

        public static MagnitudeDefinition Scalable(float v) =>
            new MagnitudeDefinition { Type = MagnitudeType.ScalableFloat, Value = v, Coefficient = 1f };
    }

    /// <summary>A single attribute modifier within an effect (SPEC §9).</summary>
    [Serializable]
    public struct ModifierDefinition
    {
        [Tooltip("Target attribute name.")]
        public string Attribute;

        [Tooltip("Operation: Add (pre-mult flat), Multiply (per-channel %), AddPost (post-mult flat), Override.")]
        public ModifierOp Operation;

        public MagnitudeDefinition Magnitude;

        [Tooltip("Optional aggregation channel. Same-channel Multiply bonuses sum; different channels multiply.")]
        public string Channel;
    }

    /// <summary>Periodic execution settings for a durational effect (SPEC §9).</summary>
    [Serializable]
    public struct PeriodDefinition
    {
        [Tooltip("Seconds between periodic executions. 0 = not periodic.")]
        public float Period;

        public bool ExecuteOnApplication;
    }

    /// <summary>
    /// A gameplay effect, authored as a Unity asset (SPEC §9). Serialized to <c>.asset</c> YAML;
    /// imported from a spec <c>gameplay_effect.yaml</c> by the editor importer. The runtime applies
    /// it via <see cref="Jbltx.Ugas.Runtime.GameplayEffectsSystem"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "UGAS/Gameplay Effect", fileName = "GameplayEffectDefinition")]
    public sealed class GameplayEffectDefinition : ScriptableObject
    {
        [SerializeField] private string _effectName;
        [SerializeField] private DurationPolicy _durationPolicy = DurationPolicy.Instant;
        [SerializeField] private MagnitudeDefinition _duration;
        [SerializeField] private PeriodDefinition _period;
        [SerializeField] private ExecutionPolicy _executionPolicy = ExecutionPolicy.RunInParallel;
        [SerializeField] private int _priority;
        [SerializeField] private List<ModifierDefinition> _modifiers = new List<ModifierDefinition>();
        [SerializeField] private List<string> _grantedTags = new List<string>();
        [SerializeField] private List<string> _applicationRequiredTags = new List<string>();
        [SerializeField] private List<string> _gameplayCues = new List<string>();

        public string EffectName => _effectName;
        public DurationPolicy DurationPolicy => _durationPolicy;
        public MagnitudeDefinition Duration => _duration;
        public PeriodDefinition Period => _period;
        public ExecutionPolicy ExecutionPolicy => _executionPolicy;
        public int Priority => _priority;
        public IReadOnlyList<ModifierDefinition> Modifiers => _modifiers;
        public IReadOnlyList<string> GrantedTags => _grantedTags;
        public IReadOnlyList<string> ApplicationRequiredTags => _applicationRequiredTags;
        public IReadOnlyList<string> GameplayCues => _gameplayCues;

        /// <summary>True if this effect has periodic execution configured.</summary>
        public bool IsPeriodic => _period.Period > 0f;

        /// <summary>Populates the asset (used by the editor importer).</summary>
        public void Populate(
            string effectName, DurationPolicy durationPolicy, MagnitudeDefinition duration,
            PeriodDefinition period, ExecutionPolicy executionPolicy, int priority,
            List<ModifierDefinition> modifiers, List<string> grantedTags,
            List<string> applicationRequiredTags, List<string> gameplayCues)
        {
            _effectName = effectName;
            _durationPolicy = durationPolicy;
            _duration = duration;
            _period = period;
            _executionPolicy = executionPolicy;
            _priority = priority;
            _modifiers = modifiers ?? new List<ModifierDefinition>();
            _grantedTags = grantedTags ?? new List<string>();
            _applicationRequiredTags = applicationRequiredTags ?? new List<string>();
            _gameplayCues = gameplayCues ?? new List<string>();
        }
    }
}
