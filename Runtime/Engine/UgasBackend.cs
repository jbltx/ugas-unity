namespace Jbltx.Ugas.Runtime
{
    /// <summary>Which aggregation backend is compiled into this build.</summary>
    public enum UgasBackendKind
    {
        /// <summary>The managed MonoBehaviour path (always available).</summary>
        Managed,

        /// <summary>The DOTS/Burst-accelerated ECS path (when com.unity.entities is installed).</summary>
        Dots
    }

    /// <summary>
    /// Reports the active aggregation backend. The DOTS path is selected automatically when the
    /// package is compiled with <c>com.unity.entities</c> present (which defines <c>UGAS_DOTS</c>);
    /// otherwise the managed path is used. Both call the same <see cref="Kernel.AttributeKernel"/>,
    /// so behaviour is identical — only performance differs.
    /// </summary>
    public static class UgasBackend
    {
        /// <summary>True when the DOTS-accelerated backend is compiled in.</summary>
        public static bool DotsAvailable
        {
#if UGAS_DOTS
            get => true;
#else
            get => false;
#endif
        }

        /// <summary>True when Burst is available for the managed path's hot loops.</summary>
        public static bool BurstAvailable
        {
#if UGAS_BURST
            get => true;
#else
            get => false;
#endif
        }

        /// <summary>The kind of backend in effect.</summary>
        public static UgasBackendKind Active => DotsAvailable ? UgasBackendKind.Dots : UgasBackendKind.Managed;
    }
}
