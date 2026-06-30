using System;

namespace Jbltx.Ugas.Schema
{
    /// <summary>
    /// Parses the verbatim enum strings used in the UGAS schemas into the C# enums. Unknown values
    /// fall back to the schema default and never throw, so loading is resilient to spec additions.
    /// </summary>
    internal static class SchemaEnums
    {
        public static AttributeCategory ParseCategory(string s) => s switch
        {
            "Resource" => AttributeCategory.Resource,
            "Meta" => AttributeCategory.Meta,
            _ => AttributeCategory.Statistic
        };

        public static ReplicationMode ParseReplicationMode(string s) => s switch
        {
            "None" => ReplicationMode.None,
            "OwnerOnly" => ReplicationMode.OwnerOnly,
            _ => ReplicationMode.All
        };

        public static DurationPolicy ParseDurationPolicy(string s) => s switch
        {
            "Instant" => DurationPolicy.Instant,
            "Infinite" => DurationPolicy.Infinite,
            _ => DurationPolicy.HasDuration
        };

        public static ExecutionPolicy ParseExecutionPolicy(string s) => s switch
        {
            "RunInSequence" => ExecutionPolicy.RunInSequence,
            "RunInMerge" => ExecutionPolicy.RunInMerge,
            _ => ExecutionPolicy.RunInParallel
        };

        public static MagnitudeType ParseMagnitudeType(string s) => s switch
        {
            "AttributeBased" => MagnitudeType.AttributeBased,
            "CustomCalculation" => MagnitudeType.CustomCalculation,
            "SetByCaller" => MagnitudeType.SetByCaller,
            _ => MagnitudeType.ScalableFloat
        };

        public static MagnitudeSource ParseMagnitudeSource(string s) => s switch
        {
            "Target" => MagnitudeSource.Target,
            _ => MagnitudeSource.Source
        };

        public static ModifierOperation ParseModifierOperation(string s) => s switch
        {
            "AddPost" => ModifierOperation.AddPost,
            "Multiply" => ModifierOperation.Multiply,
            "Override" => ModifierOperation.Override,
            _ => ModifierOperation.Add
        };

        public static GCReplicationMode ParseGCReplicationMode(string s) => s switch
        {
            "Minimal" => GCReplicationMode.Minimal,
            "Full" => GCReplicationMode.Full,
            "None" => GCReplicationMode.None,
            _ => GCReplicationMode.Mixed
        };
    }
}
