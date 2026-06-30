using System;
using System.Collections.Generic;
using UnityEngine;

namespace Jbltx.Ugas.Definitions
{
    /// <summary>An ability granted to a controller at startup.</summary>
    [Serializable]
    public struct GrantedAbilityConfig
    {
        public GameplayAbilityDefinition Ability;
        [Min(1)] public int Level;

        [Tooltip("Input binding identifier; references an Action Name from the input layer.")]
        public string InputID;
    }

    /// <summary>
    /// Authoring config for a <see cref="Jbltx.Ugas.Runtime.UgasController"/>, as a Unity asset
    /// (SPEC §4). Declares which attribute sets to register, abilities to grant, the tag registry to
    /// use, and the starting owned tags. Imported from a spec <c>gameplay_controller.yaml</c> by the
    /// editor importer (which also captures base values into <see cref="StartingAttributeValues"/>).
    /// </summary>
    [CreateAssetMenu(menuName = "UGAS/Gameplay Controller Config", fileName = "GameplayControllerConfig")]
    public sealed class GameplayControllerConfig : ScriptableObject
    {
        [Serializable]
        public struct StartingAttribute
        {
            public string SetName;
            public string AttributeName;
            public float BaseValue;
        }

        [SerializeField] private GameplayTagRegistry _tagRegistry;
        [SerializeField] private List<AttributeSetDefinition> _attributeSets = new List<AttributeSetDefinition>();
        [SerializeField] private List<GrantedAbilityConfig> _grantedAbilities = new List<GrantedAbilityConfig>();
        [SerializeField] private List<string> _startingTags = new List<string>();
        [SerializeField] private List<StartingAttribute> _startingAttributeValues = new List<StartingAttribute>();
        [SerializeField] private ControllerReplication _replication = ControllerReplication.Mixed;

        [Header("Metadata")]
        [SerializeField] private string _displayName;

        public GameplayTagRegistry TagRegistry => _tagRegistry;
        public IReadOnlyList<AttributeSetDefinition> AttributeSets => _attributeSets;
        public IReadOnlyList<GrantedAbilityConfig> GrantedAbilities => _grantedAbilities;
        public IReadOnlyList<string> StartingTags => _startingTags;
        public IReadOnlyList<StartingAttribute> StartingAttributeValues => _startingAttributeValues;
        public ControllerReplication Replication => _replication;

        public void SetTagRegistry(GameplayTagRegistry registry) => _tagRegistry = registry;
        public void SetAttributeSets(List<AttributeSetDefinition> sets) => _attributeSets = sets ?? new List<AttributeSetDefinition>();

        /// <summary>Populates the asset (used by the editor importer).</summary>
        public void Populate(List<string> startingTags, List<StartingAttribute> startingValues,
            ControllerReplication replication, string displayName)
        {
            _startingTags = startingTags ?? new List<string>();
            _startingAttributeValues = startingValues ?? new List<StartingAttribute>();
            _replication = replication;
            _displayName = displayName;
        }
    }
}
