using System;
using System.Collections.Generic;
using Jbltx.Ugas.Tags;
using UnityEngine;

namespace Jbltx.Ugas.Definitions
{
    /// <summary>
    /// A declared gameplay-tag vocabulary, authored as a Unity asset (SPEC §7). Serialized to
    /// <c>.asset</c> YAML with a free inspector; imported from a spec <c>tag_registry.yaml</c> by the
    /// editor <c>GameplayTagRegistryImporter</c>. Call <see cref="BuildRuntime"/> to intern the tags
    /// into a <see cref="GameplayTagRegistryRuntime"/> for fast handle-based queries at runtime.
    /// </summary>
    [CreateAssetMenu(menuName = "UGAS/Gameplay Tag Registry", fileName = "GameplayTagRegistry")]
    public sealed class GameplayTagRegistry : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            [Tooltip("Hierarchical tag in dot notation, e.g. State.Debuff.Stunned")]
            public string Tag;

            [TextArea] public string Description;

            [Tooltip("Whether multiple instances of this exact tag may be held at once.")]
            public bool AllowMultiple;
        }

        [SerializeField] private List<Entry> _tags = new List<Entry>();

        /// <summary>The declared tag entries.</summary>
        public IReadOnlyList<Entry> Tags => _tags;

        /// <summary>Replaces the entry list (used by the editor importer).</summary>
        public void SetEntries(List<Entry> entries) => _tags = entries ?? new List<Entry>();

        /// <summary>Interns every declared tag (and its ancestors) into a runtime registry.</summary>
        public GameplayTagRegistryRuntime BuildRuntime()
        {
            var runtime = new GameplayTagRegistryRuntime();
            foreach (var e in _tags)
            {
                if (!string.IsNullOrEmpty(e.Tag)) runtime.Resolve(e.Tag);
            }
            return runtime;
        }
    }
}
