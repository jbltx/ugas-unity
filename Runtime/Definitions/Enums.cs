namespace Jbltx.Ugas.Definitions
{
    /// <summary>Attribute category (SPEC §5).</summary>
    public enum AttributeCategory
    {
        Resource,
        Statistic,
        Meta
    }

    /// <summary>How an attribute replicates across the network (SPEC §5).</summary>
    public enum AttributeReplication
    {
        None,
        OwnerOnly,
        All
    }

    /// <summary>How long a gameplay effect lasts (SPEC §9).</summary>
    public enum DurationPolicy
    {
        Instant,
        HasDuration,
        Infinite
    }

    /// <summary>How concurrent applications of the same effect combine (SPEC §9).</summary>
    public enum ExecutionPolicy
    {
        RunInParallel,
        RunInSequence,
        RunInMerge
    }

    /// <summary>How a modifier/duration magnitude is computed (SPEC §9).</summary>
    public enum MagnitudeType
    {
        ScalableFloat,
        AttributeBased,
        CustomCalculation,
        SetByCaller
    }

    /// <summary>Which gameplay controller a backing attribute is read from (SPEC §9).</summary>
    public enum MagnitudeSource
    {
        Source,
        Target
    }

    /// <summary>GC replication strategy (SPEC §4).</summary>
    public enum ControllerReplication
    {
        Minimal,
        Mixed,
        Full,
        None
    }
}
