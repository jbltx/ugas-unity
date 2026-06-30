using System.Collections.Generic;

namespace Jbltx.Ugas.Runtime
{
    /// <summary>
    /// Interns modifier-channel names to dense int ids for the <see cref="Jbltx.Ugas.Kernel.AttributeKernel"/>.
    /// The kernel addresses channels by index into a scratch buffer, so channel ids are kept compact
    /// (0..Count-1) and the controller can size one reusable scratch buffer of length <see cref="Count"/>.
    /// </summary>
    public sealed class ChannelTable
    {
        private readonly Dictionary<string, int> _ids = new Dictionary<string, int>();

        /// <summary>Number of distinct named channels interned so far.</summary>
        public int Count => _ids.Count;

        /// <summary>
        /// Returns the dense id for a channel name, interning it if new. Null/empty maps to
        /// <see cref="Jbltx.Ugas.Kernel.ModifierSample.NoChannel"/> (an implicit singleton channel).
        /// </summary>
        public int GetOrAdd(string channel)
        {
            if (string.IsNullOrEmpty(channel)) return Kernel.ModifierSample.NoChannel;
            if (_ids.TryGetValue(channel, out int id)) return id;
            id = _ids.Count;
            _ids[channel] = id;
            return id;
        }
    }
}
