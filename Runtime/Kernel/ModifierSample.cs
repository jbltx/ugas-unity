namespace Jbltx.Ugas.Kernel
{
    /// <summary>
    /// A fully-resolved modifier ready for the aggregation kernel: an already-evaluated numeric
    /// magnitude plus the metadata the §5 pipeline needs. This is a blittable value type containing
    /// no managed references, so it can be stored in a <c>NativeArray</c> and read inside a
    /// Burst-compiled job.
    /// </summary>
    /// <remarks>
    /// <para>Channels are <b>interned to ints</b> rather than strings: <see cref="ChannelId"/> is an
    /// index into the registry's channel table (or <see cref="NoChannel"/>). Same-channel
    /// <see cref="ModifierOp.Multiply"/> bonuses sum; different channels multiply; modifiers tagged
    /// <see cref="NoChannel"/> each form their own implicit singleton channel (SPEC §16.3).</para>
    /// <para><see cref="Priority"/> and <see cref="Sequence"/> resolve <see cref="ModifierOp.Override"/>
    /// conflicts: highest priority wins, most-recently-applied (highest sequence) wins on a tie.</para>
    /// </remarks>
    public struct ModifierSample
    {
        /// <summary>Sentinel channel id meaning "no channel" (implicit singleton multiply channel).</summary>
        public const int NoChannel = -1;

        /// <summary>The operation to apply.</summary>
        public ModifierOp Op;

        /// <summary>The resolved numeric magnitude. For <see cref="ModifierOp.Multiply"/> this is the signed bonus.</summary>
        public float Magnitude;

        /// <summary>Interned channel id, or <see cref="NoChannel"/>.</summary>
        public int ChannelId;

        /// <summary>Source-effect priority for Override resolution (higher wins).</summary>
        public int Priority;

        /// <summary>Monotonic application order; LIFO tie-breaker for Override.</summary>
        public long Sequence;

        public ModifierSample(ModifierOp op, float magnitude, int channelId = NoChannel, int priority = 0, long sequence = 0)
        {
            Op = op;
            Magnitude = magnitude;
            ChannelId = channelId;
            Priority = priority;
            Sequence = sequence;
        }
    }
}
