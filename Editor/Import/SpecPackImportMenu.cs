using System.IO;
using UnityEditor;
using UnityEngine;

namespace Jbltx.Ugas.Editor
{
    /// <summary>
    /// Editor menu helpers for bringing UGAS spec packs into a project. A spec pack ships its
    /// entities as plain <c>.yaml</c>; since the <see cref="UgasSpecImporter"/> binds the dedicated
    /// <c>.ugasentity</c> extension (to avoid hijacking all YAML), this command copies a chosen
    /// folder's entity files into the project under that extension, where they import into SO assets.
    /// </summary>
    public static class SpecPackImportMenu
    {
        [MenuItem("Assets/UGAS/Import Spec Pack…", false, 2000)]
        public static void ImportSpecPack()
        {
            string sourceDir = EditorUtility.OpenFolderPanel(
                "Select a UGAS spec pack folder (containing entities/*.yaml)", "", "");
            if (string.IsNullOrEmpty(sourceDir)) return;

            // Accept either the pack root or its entities/ subfolder.
            string entitiesDir = Directory.Exists(Path.Combine(sourceDir, "entities"))
                ? Path.Combine(sourceDir, "entities")
                : sourceDir;

            string destDir = ResolveSelectedProjectFolder();
            int imported = 0;

            foreach (string yaml in Directory.GetFiles(entitiesDir, "*.yaml"))
            {
                // Skip input-layer files; this scaffold imports the four-pillar entity kinds.
                string fileName = Path.GetFileName(yaml);
                if (fileName.StartsWith("input_")) continue;

                string baseName = Path.GetFileNameWithoutExtension(yaml);
                string destPath = Path.Combine(destDir, baseName + ".ugasentity");
                File.Copy(yaml, destPath, overwrite: true);
                imported++;
            }

            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("UGAS Spec Pack Import",
                $"Imported {imported} entity file(s) into '{destDir}'.\n\n" +
                "Each was copied as a .ugasentity asset and converted to a ScriptableObject definition.",
                "OK");
        }

        // Returns the currently-selected project folder, or Assets/ if none.
        private static string ResolveSelectedProjectFolder()
        {
            foreach (var obj in Selection.GetFiltered<Object>(SelectionMode.Assets))
            {
                string path = AssetDatabase.GetAssetPath(obj);
                if (!string.IsNullOrEmpty(path))
                {
                    if (Directory.Exists(path)) return path;
                    string dir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir)) return dir;
                }
            }
            return "Assets";
        }
    }
}
