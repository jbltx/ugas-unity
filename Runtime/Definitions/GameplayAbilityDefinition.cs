using System;
using System.Collections.Generic;
using UnityEngine;

namespace Jbltx.Ugas.Definitions
{
    /// <summary>Tag sets governing an ability's activation, blocking, and cancellation (SPEC §8).</summary>
    [Serializable]
    public struct AbilityTagSet
    {
        public List<string> AbilityTags;
        public List<string> BlockedByTags;
        public List<string> BlockAbilitiesWithTags;
        public List<string> CancelAbilitiesWithTags;
        public List<string> ActivationRequiredTags;
        public List<string> ActivationBlockedTags;
        public List<string> ActivationOwnedTags;
    }

    /// <summary>A serialized key/value parameter for an ability task.</summary>
    [Serializable]
    public struct TaskParam
    {
        public string Key;
        public string Value;
    }

    /// <summary>A single ability-task entry (SPEC §10).</summary>
    [Serializable]
    public struct AbilityTaskDefinition
    {
        [Tooltip("Task type identifier, e.g. PlayMontage, WaitGameplayEvent.")]
        public string Type;

        public List<TaskParam> Params;

        [Tooltip("Seconds between ticks; 0 = every frame. Only meaningful for ticking tasks.")]
        public float TickInterval;

        [Tooltip("Tick scheduling priority when the per-frame budget is exhausted; higher ticks first.")]
        public int Priority;
    }

    /// <summary>
    /// A gameplay ability, authored as a Unity asset (SPEC §8). Serialized to <c>.asset</c> YAML;
    /// imported from a spec <c>gameplay_ability.yaml</c> by the editor importer. Cost and cooldown
    /// reference <see cref="GameplayEffectDefinition"/> assets.
    /// </summary>
    [CreateAssetMenu(menuName = "UGAS/Gameplay Ability", fileName = "GameplayAbilityDefinition")]
    public sealed class GameplayAbilityDefinition : ScriptableObject
    {
        [SerializeField] private string _abilityName;
        [SerializeField] private AbilityTagSet _tags;
        [SerializeField] private GameplayEffectDefinition _cost;
        [SerializeField] private GameplayEffectDefinition _cooldown;
        [SerializeField] private List<AbilityTaskDefinition> _tasks = new List<AbilityTaskDefinition>();

        [Header("Provenance")]
        [Tooltip("Original Cost effect name from the spec pack (resolved to an asset at import time).")]
        [SerializeField] private string _costRef;
        [SerializeField] private string _cooldownRef;

        [Header("Metadata")]
        [SerializeField] private string _displayName;
        [SerializeField, TextArea] private string _description;

        public string AbilityName => _abilityName;
        public AbilityTagSet Tags => _tags;
        public GameplayEffectDefinition Cost => _cost;
        public GameplayEffectDefinition Cooldown => _cooldown;
        public IReadOnlyList<AbilityTaskDefinition> Tasks => _tasks;

        /// <summary>Spec-pack reference names, kept for late binding when the asset isn't resolved yet.</summary>
        public string CostRef => _costRef;
        public string CooldownRef => _cooldownRef;

        /// <summary>Populates the asset (used by the editor importer).</summary>
        public void Populate(string abilityName, AbilityTagSet tags, List<AbilityTaskDefinition> tasks,
            string costRef, string cooldownRef, string displayName = null, string description = null)
        {
            _abilityName = abilityName;
            _tags = tags;
            _tasks = tasks ?? new List<AbilityTaskDefinition>();
            _costRef = costRef;
            _cooldownRef = cooldownRef;
            _displayName = displayName;
            _description = description;
        }

        /// <summary>Late-binds the cost/cooldown effect assets (editor importer second pass).</summary>
        public void BindEffects(GameplayEffectDefinition cost, GameplayEffectDefinition cooldown)
        {
            _cost = cost;
            _cooldown = cooldown;
        }
    }
}
