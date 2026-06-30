namespace Jbltx.Ugas.Kernel
{
    /// <summary>
    /// The arithmetic operation a modifier applies to its target attribute (SPEC §5/§9), expressed
    /// as a plain enum so it is usable from both the managed runtime and Burst-compiled jobs.
    /// </summary>
    /// <remarks>
    /// Pipeline-step ordering is fixed by <see cref="AttributeKernel"/>:
    /// <list type="bullet">
    /// <item><see cref="Add"/> — pre-multiply flat additive (step 2).</item>
    /// <item><see cref="Multiply"/> — multiplicative bonus aggregated per channel (step 6). Signed: +0.25 = +25%.</item>
    /// <item><see cref="AddPost"/> — post-multiply flat additive (step 7; rare).</item>
    /// <item><see cref="Override"/> — replaces the computed result (step 8).</item>
    /// </list>
    /// </remarks>
    public enum ModifierOp : byte
    {
        Add = 0,
        AddPost = 1,
        Multiply = 2,
        Override = 3
    }
}
