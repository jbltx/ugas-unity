using System.IO;
using Jbltx.Ugas.Definitions;
using Jbltx.Ugas.Editor.Yaml;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Jbltx.Ugas.Editor
{
    /// <summary>
    /// A <see cref="ScriptedImporter"/> that converts a UGAS spec entity file into the matching
    /// runtime ScriptableObject definition at import time (SPEC packs are an import source only).
    /// </summary>
    /// <remarks>
    /// <para>Bound to the <c>.ugasentity</c> extension. Because a ScriptedImporter claims an extension
    /// project-wide, we use a dedicated extension rather than hijacking all <c>.yaml</c> files: copy
    /// or rename a spec entity (e.g. <c>effect_regeneration.yaml</c>) to <c>*.ugasentity</c>, or use
    /// the <c>Assets ▸ UGAS ▸ Import Spec Pack…</c> menu which does the copy for a whole folder.</para>
    /// <para>The file's entity kind (attribute set / effect / ability / tag registry / controller) is
    /// detected from its root keys, so one importer handles every UGAS entity type. YAML parsing and
    /// mapping live entirely in this editor assembly — the runtime never parses YAML.</para>
    /// </remarks>
    [ScriptedImporter(version: 1, ext: "ugasentity")]
    public sealed class UgasSpecImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            string text = File.ReadAllText(ctx.assetPath);

            YamlNode parsed;
            try
            {
                parsed = YamlParser.Parse(text);
            }
            catch (YamlParseException e)
            {
                ctx.LogImportError($"UGAS import failed to parse YAML: {e.Message}");
                CreatePlaceholder(ctx);
                return;
            }

            if (!(parsed is YamlMapping root))
            {
                ctx.LogImportError("UGAS import: expected a mapping at the document root.");
                CreatePlaceholder(ctx);
                return;
            }

            var kind = SpecEntityMapper.Detect(root);
            Object main = kind switch
            {
                SpecEntityKind.AttributeSet => BuildAttributeSet(root),
                SpecEntityKind.GameplayEffect => BuildEffect(root),
                SpecEntityKind.GameplayAbility => BuildAbility(root),
                SpecEntityKind.GameplayTagRegistry => BuildTagRegistry(root),
                SpecEntityKind.GameplayController => BuildController(root),
                _ => null
            };

            if (main == null)
            {
                ctx.LogImportError($"UGAS import: could not determine entity kind for '{Path.GetFileName(ctx.assetPath)}'.");
                CreatePlaceholder(ctx);
                return;
            }

            ctx.AddObjectToAsset("main", main);
            ctx.SetMainObject(main);
        }

        private static Object BuildAttributeSet(YamlMapping root)
        {
            var so = ScriptableObject.CreateInstance<AttributeSetDefinition>();
            SpecEntityMapper.PopulateAttributeSet(so, root);
            so.name = NameOrDefault(so.SetName, "AttributeSet");
            return so;
        }

        private static Object BuildEffect(YamlMapping root)
        {
            var so = ScriptableObject.CreateInstance<GameplayEffectDefinition>();
            SpecEntityMapper.PopulateEffect(so, root);
            so.name = NameOrDefault(so.EffectName, "GameplayEffect");
            return so;
        }

        private static Object BuildAbility(YamlMapping root)
        {
            var so = ScriptableObject.CreateInstance<GameplayAbilityDefinition>();
            SpecEntityMapper.PopulateAbility(so, root);
            so.name = NameOrDefault(so.AbilityName, "GameplayAbility");
            return so;
        }

        private static Object BuildTagRegistry(YamlMapping root)
        {
            var so = ScriptableObject.CreateInstance<GameplayTagRegistry>();
            SpecEntityMapper.PopulateTagRegistry(so, root);
            so.name = "GameplayTagRegistry";
            return so;
        }

        private static Object BuildController(YamlMapping root)
        {
            var so = ScriptableObject.CreateInstance<GameplayControllerConfig>();
            SpecEntityMapper.PopulateController(so, root);
            so.name = "GameplayControllerConfig";
            return so;
        }

        private static void CreatePlaceholder(AssetImportContext ctx)
        {
            // Produce an empty main object so the import "succeeds" with a logged error rather than
            // leaving the asset in a broken state.
            var placeholder = ScriptableObject.CreateInstance<AttributeSetDefinition>();
            placeholder.name = Path.GetFileNameWithoutExtension(ctx.assetPath);
            ctx.AddObjectToAsset("main", placeholder);
            ctx.SetMainObject(placeholder);
        }

        private static string NameOrDefault(string n, string fallback) => string.IsNullOrEmpty(n) ? fallback : n;
    }
}
