using System.Collections.Generic;

namespace Jbltx.Ugas.Schema.Yaml
{
    /// <summary>
    /// A minimal parsed YAML value: either a scalar (string), a mapping (ordered key/value), or a
    /// sequence (list). The UGAS genre-pack entity files use a regular YAML subset, so this small
    /// model is sufficient and keeps the package dependency-free for entity loading.
    /// </summary>
    public abstract class YamlNode
    {
        /// <summary>Convenience: this node as a mapping, or null.</summary>
        public YamlMapping AsMapping => this as YamlMapping;

        /// <summary>Convenience: this node as a sequence, or null.</summary>
        public YamlSequence AsSequence => this as YamlSequence;

        /// <summary>Convenience: this node as a scalar, or null.</summary>
        public YamlScalar AsScalar => this as YamlScalar;
    }

    /// <summary>A leaf scalar value, retained as raw text plus a quoted flag for typing decisions.</summary>
    public sealed class YamlScalar : YamlNode
    {
        public string Raw;
        public bool WasQuoted;

        public YamlScalar(string raw, bool wasQuoted)
        {
            Raw = raw;
            WasQuoted = wasQuoted;
        }

        public override string ToString() => Raw;
    }

    /// <summary>An ordered mapping of string keys to child nodes.</summary>
    public sealed class YamlMapping : YamlNode
    {
        public readonly List<string> Keys = new List<string>();
        public readonly Dictionary<string, YamlNode> Children = new Dictionary<string, YamlNode>();

        public void Add(string key, YamlNode value)
        {
            if (!Children.ContainsKey(key))
            {
                Keys.Add(key);
            }
            Children[key] = value;
        }

        public bool TryGet(string key, out YamlNode value) => Children.TryGetValue(key, out value);

        public YamlNode Get(string key) => Children.TryGetValue(key, out var v) ? v : null;

        public bool Has(string key) => Children.ContainsKey(key);
    }

    /// <summary>An ordered sequence of child nodes.</summary>
    public sealed class YamlSequence : YamlNode
    {
        public readonly List<YamlNode> Items = new List<YamlNode>();
    }
}
