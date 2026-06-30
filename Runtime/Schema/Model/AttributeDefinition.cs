using System;

namespace Jbltx.Ugas.Schema
{
    /// <summary>
    /// Attribute category as defined by the core <c>attribute</c> schema (SPEC §5).
    /// </summary>
    public enum AttributeCategory
    {
        /// <summary>A consumable value such as Health or Mana (typically clamped).</summary>
        Resource,

        /// <summary>A computed stat such as Armor or WeaponDamage. The schema default.</summary>
        Statistic,

        /// <summary>A meta value such as incoming Damage, used to feed executions.</summary>
        Meta
    }

    /// <summary>How an attribute is replicated across the network (SPEC §5).</summary>
    public enum ReplicationMode
    {
        None,
        OwnerOnly,
        All
    }

    /// <summary>
    /// Optional [Min, Max] clamping bounds for an attribute. Each bound is either a literal
    /// number or a reference to another attribute by name (e.g. Health clamps to MaxHealth).
    /// Mirrors the <c>Clamping</c> object of the core schema.
    /// </summary>
    [Serializable]
    public sealed class AttributeClamping
    {
        /// <summary>Lower bound. May be null, a numeric literal, or an attribute name.</summary>
        public AttributeBound Min;

        /// <summary>Upper bound. May be null, a numeric literal, or an attribute name.</summary>
        public AttributeBound Max;
    }

    /// <summary>
    /// A clamp bound that is either a literal value or a reference to another attribute.
    /// The schema models this as <c>oneOf: [number, string]</c>.
    /// </summary>
    [Serializable]
    public sealed class AttributeBound
    {
        /// <summary>True when this bound is a literal numeric value.</summary>
        public bool IsLiteral;

        /// <summary>The literal value, valid when <see cref="IsLiteral"/> is true.</summary>
        public float Literal;

        /// <summary>The referenced attribute name, valid when <see cref="IsLiteral"/> is false.</summary>
        public string AttributeReference;

        public static AttributeBound FromLiteral(float value) =>
            new AttributeBound { IsLiteral = true, Literal = value };

        public static AttributeBound FromReference(string attributeName) =>
            new AttributeBound { IsLiteral = false, AttributeReference = attributeName };
    }

    /// <summary>Display/UI metadata attached to an attribute.</summary>
    [Serializable]
    public sealed class AttributeMetadata
    {
        public string DisplayName;
        public string Description;
        public string UICategory;
        public string Icon;
    }

    /// <summary>
    /// Engine-agnostic attribute definition. One-to-one mapping of <c>schemas/attribute.yaml</c>
    /// (also embedded inside <c>attribute_set</c>). This is data only — runtime behaviour lives
    /// in the Attributes pillar (<see cref="Jbltx.Ugas.Attributes"/>).
    /// </summary>
    [Serializable]
    public sealed class AttributeDefinition
    {
        /// <summary>Unique identifier for this attribute. Required.</summary>
        public string Name;

        /// <summary>Initial base value. Required.</summary>
        public float DefaultBaseValue;

        /// <summary>Category. Defaults to <see cref="AttributeCategory.Statistic"/>.</summary>
        public AttributeCategory Category = AttributeCategory.Statistic;

        /// <summary>Optional clamping bounds.</summary>
        public AttributeClamping Clamping;

        /// <summary>Replication mode. Defaults to <see cref="ReplicationMode.All"/>.</summary>
        public ReplicationMode ReplicationMode = ReplicationMode.All;

        /// <summary>Optional display metadata.</summary>
        public AttributeMetadata Metadata;
    }
}
