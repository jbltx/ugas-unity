using System;
using System.Collections.Generic;

namespace Jbltx.Ugas.Schema
{
    /// <summary>
    /// Engine-agnostic attribute-set definition. One-to-one mapping of
    /// <c>schemas/attribute_set.yaml</c> (SPEC §6). Groups related attributes and may declare
    /// dependencies on other sets that must be present.
    /// </summary>
    [Serializable]
    public sealed class AttributeSetDefinition
    {
        /// <summary>Unique set identifier. Required.</summary>
        public string Name;

        /// <summary>Names of attribute sets this set requires to be present. Optional.</summary>
        public List<string> Dependencies = new List<string>();

        /// <summary>The attributes contained in this set. Required, at least one.</summary>
        public List<AttributeDefinition> Attributes = new List<AttributeDefinition>();

        /// <summary>Optional display metadata.</summary>
        public AttributeSetMetadata Metadata;
    }

    /// <summary>Display metadata for an attribute set.</summary>
    [Serializable]
    public sealed class AttributeSetMetadata
    {
        public string DisplayName;
        public string Description;
    }
}
