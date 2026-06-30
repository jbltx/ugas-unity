using System;
using System.IO;
using Jbltx.Ugas.Schema.Yaml;

namespace Jbltx.Ugas.Schema
{
    /// <summary>
    /// Public entry point for loading the engine-agnostic UGAS data model from the genre-pack
    /// <c>entities/*.yaml</c> files (and equivalent JSON). Each method parses raw text into the
    /// typed model defined in <c>Runtime/Schema/Model</c>.
    /// </summary>
    /// <remarks>
    /// The loader consumes the existing core schemas directly — it does not redefine the data
    /// model. YAML parsing uses the package's built-in <see cref="YamlParser"/> (a dependency-free
    /// reader for the regular subset the entity files use). For the authoritative <c>.json</c>
    /// schema variants, callers may instead deserialize with Newtonsoft.Json against these same
    /// model types.
    /// </remarks>
    public static class SchemaLoader
    {
        private static YamlMapping ParseRootMapping(string yaml)
        {
            var node = YamlParser.Parse(yaml);
            if (node is YamlMapping m) return m;
            throw new YamlParseException("Expected a mapping at the document root.");
        }

        // ---- Parse from in-memory text ----

        public static AttributeDefinition AttributeFromYaml(string yaml) =>
            SchemaMapper.MapAttribute(ParseRootMapping(yaml));

        public static AttributeSetDefinition AttributeSetFromYaml(string yaml) =>
            SchemaMapper.MapAttributeSet(ParseRootMapping(yaml));

        public static GameplayTagRegistry TagRegistryFromYaml(string yaml) =>
            SchemaMapper.MapTagRegistry(ParseRootMapping(yaml));

        public static GameplayEffectDefinition EffectFromYaml(string yaml) =>
            SchemaMapper.MapEffect(ParseRootMapping(yaml));

        public static GameplayAbilityDefinition AbilityFromYaml(string yaml) =>
            SchemaMapper.MapAbility(ParseRootMapping(yaml));

        public static GameplayControllerDefinition ControllerFromYaml(string yaml) =>
            SchemaMapper.MapController(ParseRootMapping(yaml));

        // ---- Load from a file path ----

        public static AttributeSetDefinition LoadAttributeSet(string path) =>
            AttributeSetFromYaml(ReadAllText(path));

        public static GameplayTagRegistry LoadTagRegistry(string path) =>
            TagRegistryFromYaml(ReadAllText(path));

        public static GameplayEffectDefinition LoadEffect(string path) =>
            EffectFromYaml(ReadAllText(path));

        public static GameplayAbilityDefinition LoadAbility(string path) =>
            AbilityFromYaml(ReadAllText(path));

        public static GameplayControllerDefinition LoadController(string path) =>
            ControllerFromYaml(ReadAllText(path));

        private static string ReadAllText(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));
            if (!File.Exists(path)) throw new FileNotFoundException($"Entity file not found: {path}", path);
            return File.ReadAllText(path);
        }
    }
}
