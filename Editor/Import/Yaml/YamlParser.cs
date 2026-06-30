using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Jbltx.Ugas.Editor.Yaml
{
    /// <summary>Thrown when YAML input uses a construct outside the supported subset.</summary>
    public sealed class YamlParseException : Exception
    {
        public YamlParseException(string message) : base(message) { }
    }

    /// <summary>
    /// A small, dependency-free parser for the regular YAML subset used by the UGAS genre-pack
    /// entity files. It supports:
    /// <list type="bullet">
    /// <item>block mappings (<c>key: value</c>) with 2-space-style indentation,</item>
    /// <item>block sequences (<c>- item</c>), including <c>- key: value</c> maps inside sequences,</item>
    /// <item>scalar values (bare or single/double quoted), <c>#</c> comments, and blank lines.</item>
    /// </list>
    /// It deliberately rejects anchors, aliases, flow collections, multi-document streams, and
    /// block scalars (<c>|</c>/<c>&gt;</c>) so unsupported input fails loudly rather than silently
    /// mis-parsing. For the authoritative JSON schema variants, use Newtonsoft directly instead.
    /// </summary>
    public static class YamlParser
    {
        private sealed class Line
        {
            public int Indent;
            public string Content;
            public int Number;
        }

        /// <summary>Parse a YAML document into a <see cref="YamlNode"/> tree.</summary>
        public static YamlNode Parse(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));

            var lines = Preprocess(text);
            if (lines.Count == 0)
            {
                return new YamlMapping();
            }

            int index = 0;
            return ParseBlock(lines, ref index, lines[0].Indent);
        }

        private static List<Line> Preprocess(string text)
        {
            var result = new List<Line>();
            var rawLines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int i = 0; i < rawLines.Length; i++)
            {
                string raw = rawLines[i];
                if (raw.IndexOf('\t') >= 0)
                {
                    throw new YamlParseException($"Tab indentation is not supported (line {i + 1}).");
                }

                int indent = 0;
                while (indent < raw.Length && raw[indent] == ' ') indent++;

                string content = raw.Substring(indent);

                // Skip blank lines, full-line comments, and document markers.
                if (content.Length == 0) continue;
                if (content[0] == '#') continue;
                if (content == "---" || content == "...") continue;

                content = StripInlineComment(content).TrimEnd();
                if (content.Length == 0) continue;

                result.Add(new Line { Indent = indent, Content = content, Number = i + 1 });
            }
            return result;
        }

        // Strips an unquoted trailing "# comment". Respects single/double quotes so '#' inside a
        // quoted scalar is preserved.
        private static string StripInlineComment(string s)
        {
            bool inSingle = false, inDouble = false;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '\'' && !inDouble) inSingle = !inSingle;
                else if (c == '"' && !inSingle) inDouble = !inDouble;
                else if (c == '#' && !inSingle && !inDouble)
                {
                    // A comment must be preceded by whitespace (or start of line).
                    if (i == 0 || s[i - 1] == ' ')
                    {
                        return s.Substring(0, i);
                    }
                }
            }
            return s;
        }

        // Parses a block (mapping or sequence) whose entries share the given indent.
        private static YamlNode ParseBlock(List<Line> lines, ref int index, int indent)
        {
            if (index >= lines.Count) return new YamlMapping();

            bool isSequence = lines[index].Content.StartsWith("- ") || lines[index].Content == "-";
            return isSequence
                ? (YamlNode)ParseSequence(lines, ref index, indent)
                : ParseMapping(lines, ref index, indent);
        }

        private static YamlMapping ParseMapping(List<Line> lines, ref int index, int indent)
        {
            var map = new YamlMapping();
            while (index < lines.Count)
            {
                var line = lines[index];
                if (line.Indent < indent) break;
                if (line.Indent > indent)
                {
                    throw new YamlParseException(
                        $"Unexpected indentation at line {line.Number}: expected {indent}, found {line.Indent}.");
                }

                int colon = FindKeyColon(line.Content);
                if (colon < 0)
                {
                    throw new YamlParseException(
                        $"Expected 'key: value' mapping entry at line {line.Number}: '{line.Content}'.");
                }

                string key = Unquote(line.Content.Substring(0, colon).Trim());
                string rest = line.Content.Substring(colon + 1).Trim();
                index++;

                if (rest.Length > 0)
                {
                    map.Add(key, new YamlScalar(StripQuotes(rest, out bool q), q));
                }
                else
                {
                    // Nested block: must be more-indented, or an equally-indented sequence
                    // (YAML allows a sequence under a key at the same indent as the key).
                    if (index < lines.Count && lines[index].Indent > indent)
                    {
                        int childIndent = lines[index].Indent;
                        map.Add(key, ParseBlock(lines, ref index, childIndent));
                    }
                    else if (index < lines.Count && lines[index].Indent == indent &&
                             (lines[index].Content.StartsWith("- ") || lines[index].Content == "-"))
                    {
                        map.Add(key, ParseSequence(lines, ref index, indent));
                    }
                    else
                    {
                        // Key with no value -> null mapping.
                        map.Add(key, new YamlScalar(string.Empty, false));
                    }
                }
            }
            return map;
        }

        private static YamlSequence ParseSequence(List<Line> lines, ref int index, int indent)
        {
            var seq = new YamlSequence();
            while (index < lines.Count)
            {
                var line = lines[index];
                if (line.Indent < indent) break;
                if (line.Indent > indent)
                {
                    throw new YamlParseException(
                        $"Unexpected indentation at line {line.Number} inside sequence.");
                }
                if (!(line.Content.StartsWith("- ") || line.Content == "-")) break;

                string itemContent = line.Content == "-" ? string.Empty : line.Content.Substring(2).Trim();

                if (itemContent.Length == 0)
                {
                    // Block item on following lines.
                    index++;
                    if (index < lines.Count && lines[index].Indent > indent)
                    {
                        seq.Items.Add(ParseBlock(lines, ref index, lines[index].Indent));
                    }
                    else
                    {
                        seq.Items.Add(new YamlScalar(string.Empty, false));
                    }
                    continue;
                }

                int colon = FindKeyColon(itemContent);
                if (colon >= 0)
                {
                    // Inline-started mapping: "- key: value" (possibly with more keys indented below).
                    int virtualIndent = indent + 2;
                    var map = new YamlMapping();

                    string key = Unquote(itemContent.Substring(0, colon).Trim());
                    string rest = itemContent.Substring(colon + 1).Trim();
                    index++;

                    if (rest.Length > 0)
                    {
                        map.Add(key, new YamlScalar(StripQuotes(rest, out bool q), q));
                    }
                    else if (index < lines.Count && lines[index].Indent > virtualIndent)
                    {
                        map.Add(key, ParseBlock(lines, ref index, lines[index].Indent));
                    }
                    else if (index < lines.Count && lines[index].Indent == virtualIndent &&
                             (lines[index].Content.StartsWith("- ") || lines[index].Content == "-"))
                    {
                        map.Add(key, ParseSequence(lines, ref index, virtualIndent));
                    }
                    else
                    {
                        map.Add(key, new YamlScalar(string.Empty, false));
                    }

                    // Continuation keys of the same inline map are indented to virtualIndent.
                    ParseMappingInto(map, lines, ref index, virtualIndent);
                    seq.Items.Add(map);
                }
                else
                {
                    // Scalar sequence item.
                    seq.Items.Add(new YamlScalar(StripQuotes(itemContent, out bool q), q));
                    index++;
                }
            }
            return seq;
        }

        // Appends additional same-indent mapping entries into an existing map.
        private static void ParseMappingInto(YamlMapping map, List<Line> lines, ref int index, int indent)
        {
            while (index < lines.Count)
            {
                var line = lines[index];
                if (line.Indent != indent) break;
                if (line.Content.StartsWith("- ") || line.Content == "-") break;

                int colon = FindKeyColon(line.Content);
                if (colon < 0) break;

                string key = Unquote(line.Content.Substring(0, colon).Trim());
                string rest = line.Content.Substring(colon + 1).Trim();
                index++;

                if (rest.Length > 0)
                {
                    map.Add(key, new YamlScalar(StripQuotes(rest, out bool q), q));
                }
                else if (index < lines.Count && lines[index].Indent > indent)
                {
                    map.Add(key, ParseBlock(lines, ref index, lines[index].Indent));
                }
                else if (index < lines.Count && lines[index].Indent == indent &&
                         (lines[index].Content.StartsWith("- ") || lines[index].Content == "-"))
                {
                    map.Add(key, ParseSequence(lines, ref index, indent));
                }
                else
                {
                    map.Add(key, new YamlScalar(string.Empty, false));
                }
            }
        }

        // Finds the colon that separates a mapping key from its value, ignoring colons inside
        // quotes and requiring the colon to be followed by EOL or whitespace.
        private static int FindKeyColon(string s)
        {
            bool inSingle = false, inDouble = false;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '\'' && !inDouble) inSingle = !inSingle;
                else if (c == '"' && !inSingle) inDouble = !inDouble;
                else if (c == ':' && !inSingle && !inDouble)
                {
                    if (i + 1 >= s.Length || s[i + 1] == ' ') return i;
                }
            }
            return -1;
        }

        private static string Unquote(string s) => StripQuotes(s, out _);

        // Removes surrounding single/double quotes if present, reporting whether the value was quoted.
        private static string StripQuotes(string s, out bool wasQuoted)
        {
            wasQuoted = false;
            if (s.Length >= 2)
            {
                char first = s[0], last = s[s.Length - 1];
                if (first == '"' && last == '"')
                {
                    wasQuoted = true;
                    return UnescapeDouble(s.Substring(1, s.Length - 2));
                }
                if (first == '\'' && last == '\'')
                {
                    wasQuoted = true;
                    return s.Substring(1, s.Length - 2).Replace("''", "'");
                }
            }
            return s;
        }

        private static string UnescapeDouble(string s)
        {
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '\\' && i + 1 < s.Length)
                {
                    char n = s[++i];
                    switch (n)
                    {
                        case 'n': sb.Append('\n'); break;
                        case 't': sb.Append('\t'); break;
                        case 'r': sb.Append('\r'); break;
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        default: sb.Append(n); break;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        // ---- Scalar typing helpers used by the mappers ----

        /// <summary>Interprets a scalar node as a string (null/empty for the YAML null sentinels).</summary>
        public static string AsString(YamlNode node)
        {
            if (node is YamlScalar s)
            {
                if (!s.WasQuoted && (s.Raw == "null" || s.Raw == "~" || s.Raw.Length == 0)) return null;
                return s.Raw;
            }
            return null;
        }

        /// <summary>Interprets a scalar node as a float using invariant culture.</summary>
        public static float AsFloat(YamlNode node, float fallback = 0f)
        {
            if (node is YamlScalar s &&
                float.TryParse(s.Raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
            {
                return f;
            }
            return fallback;
        }

        /// <summary>Interprets a scalar node as an int using invariant culture.</summary>
        public static int AsInt(YamlNode node, int fallback = 0)
        {
            if (node is YamlScalar s &&
                int.TryParse(s.Raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
            {
                return i;
            }
            return fallback;
        }

        /// <summary>Interprets a scalar node as a bool (<c>true</c>/<c>false</c>, case-insensitive).</summary>
        public static bool AsBool(YamlNode node, bool fallback = false)
        {
            if (node is YamlScalar s)
            {
                if (string.Equals(s.Raw, "true", StringComparison.OrdinalIgnoreCase)) return true;
                if (string.Equals(s.Raw, "false", StringComparison.OrdinalIgnoreCase)) return false;
            }
            return fallback;
        }

        /// <summary>Reads a sequence-of-scalars node into a string list (empty if absent).</summary>
        public static List<string> AsStringList(YamlNode node)
        {
            var result = new List<string>();
            if (node is YamlSequence seq)
            {
                foreach (var item in seq.Items)
                {
                    var v = AsString(item);
                    if (v != null) result.Add(v);
                }
            }
            return result;
        }
    }
}
