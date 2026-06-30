using System;
using System.Collections.Generic;
using UnityEngine;

namespace Jbltx.Ugas.Definitions
{
    /// <summary>A clamp bound: either a literal value or a reference to another attribute by name.</summary>
    [Serializable]
    public struct AttributeBound
    {
        [Tooltip("If true, use Literal; otherwise clamp to the named attribute's current value.")]
        public bool Enabled;

        public bool IsReference;
        public float Literal;
        public string AttributeReference;

        public static AttributeBound Off => default;
        public static AttributeBound FromLiteral(float v) => new AttributeBound { Enabled = true, Literal = v };
        public static AttributeBound FromReference(string name) =>
            new AttributeBound { Enabled = true, IsReference = true, AttributeReference = name };
    }

    /// <summary>An attribute declaration (SPEC §5). Serialized inline inside an attribute set asset.</summary>
    [Serializable]
    public struct AttributeDefinition
    {
        [Tooltip("Unique identifier for this attribute.")]
        public string Name;

        [Tooltip("Initial base value.")]
        public float DefaultBaseValue;

        public AttributeCategory Category;
        public AttributeReplication Replication;

        public AttributeBound Min;
        public AttributeBound Max;

        [Header("Metadata")]
        public string DisplayName;
        [TextArea] public string Description;
        public string UICategory;
    }

    /// <summary>
    /// A named set of related attributes, authored as a Unity asset (SPEC §6). Serialized to
    /// <c>.asset</c> YAML; imported from a spec <c>attribute_set.yaml</c> by the editor importer.
    /// </summary>
    [CreateAssetMenu(menuName = "UGAS/Attribute Set", fileName = "AttributeSetDefinition")]
    public sealed class AttributeSetDefinition : ScriptableObject
    {
        [SerializeField] private string _setName;
        [SerializeField] private List<string> _dependencies = new List<string>();
        [SerializeField] private List<AttributeDefinition> _attributes = new List<AttributeDefinition>();

        [Header("Metadata")]
        [SerializeField] private string _displayName;
        [SerializeField, TextArea] private string _description;

        /// <summary>Unique set identifier.</summary>
        public string SetName => _setName;

        /// <summary>Names of attribute sets this set requires to be present.</summary>
        public IReadOnlyList<string> Dependencies => _dependencies;

        /// <summary>The attributes in this set.</summary>
        public IReadOnlyList<AttributeDefinition> Attributes => _attributes;

        /// <summary>Populates the asset (used by the editor importer).</summary>
        public void Populate(string setName, List<string> dependencies, List<AttributeDefinition> attributes,
            string displayName = null, string description = null)
        {
            _setName = setName;
            _dependencies = dependencies ?? new List<string>();
            _attributes = attributes ?? new List<AttributeDefinition>();
            _displayName = displayName;
            _description = description;
        }
    }
}
