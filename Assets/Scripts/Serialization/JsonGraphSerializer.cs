using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using BlockSystem.Core;

namespace BlockSystem.Serialization
{
    // ══════════════════════════════════════════════════════════════════════
    //  JsonGraphSerializer  —  saves and loads a BlockGraph as a JSON file.
    //
    //  What it does:
    //    Serialize()   — walks every block and connection in the graph and
    //                    packs them into a simple data structure that Unity's
    //                    JsonUtility can turn into a string.
    //    Deserialize() — does the reverse: parses the JSON, looks up each
    //                    block's class name in BlockRegistry, creates a fresh
    //                    instance, then restores all field values.
    //    SaveToFile()  — Serialize + write to disk.
    //    LoadFromFile()— read from disk + Deserialize.
    //
    //  Block properties are stored as a nested JSON string (propertiesJson).
    //  Because each block subclass can have completely different fields, we
    //  use our own lightweight reflection-based serializer that correctly
    //  handles float, int, bool, string, Vector3, and enum fields.
    //  (Unity's JsonUtility cannot serialize Dictionary<string,object>.)
    //
    // ══════════════════════════════════════════════════════════════════════
    public class JsonGraphSerializer : IGraphSerializer
    {
        bool prettyPrint;

        public JsonGraphSerializer(bool prettyPrint = true)
        {
            this.prettyPrint = prettyPrint;
        }

        public string Serialize(BlockGraph graph)
        {
            var data = new GraphData();

            foreach (var block in graph.blocks)
            {
                var bd = new BlockData
                {
                    id = block.id,
                    type = block.blockType,
                    propertiesJson = SerializeBlockProperties(block)
                };
                data.blocks.Add(bd);
            }

            foreach (var conn in graph.connections)
            {
                data.connections.Add(new ConnectionData
                {
                    fromBlockId = conn.fromBlockId,
                    fromPort = conn.fromPortName,
                    toBlockId = conn.toBlockId,
                    toPort = conn.toPortName
                });
            }

            return JsonUtility.ToJson(data, prettyPrint);
        }

        public void Deserialize(BlockGraph graph, string json)
        {
            var data = JsonUtility.FromJson<GraphData>(json);
            if (data == null) return;

            graph.Clear();

            // deserialize blocks
            var blockMap = new Dictionary<string, Block>();
            foreach (var bd in data.blocks)
            {
                var blockType = BlockRegistry.Get(bd.type);
                if (blockType == null)
                {
                    Debug.LogWarning($"Unknown block type: {bd.type}");
                    continue;
                }

                var block = graph.AddBlock(blockType);
                block.id = bd.id; // preserve original ID
                DeserializeBlockProperties(block, bd.propertiesJson);

                // Dynamic-port blocks (Parallel, Sequence) need to rebuild
                // their ports after deserialization restores fields like
                // branchCount — the constructor ran with default values.
                block.RebuildDynamicPorts();

                blockMap[block.id] = block;
            }

            // deserialize connections
            foreach (var cd in data.connections)
            {
                graph.Connect(cd.fromBlockId, cd.fromPort, cd.toBlockId, cd.toPort);
            }
        }

        public void SaveToFile(BlockGraph graph, string path)
        {
            var json = Serialize(graph);
            File.WriteAllText(path, json);
        }

        public void LoadFromFile(BlockGraph graph, string path)
        {
            if (!File.Exists(path))
            {
                Debug.LogError($"File not found: {path}");
                return;
            }

            var json = File.ReadAllText(path);
            Deserialize(graph, json);
        }

        // ── Block property serialization ────────────────────────────────
        //
        // We can NOT use JsonUtility here because it does not support
        // Dictionary<string, object>.  Instead we build a simple JSON
        // string manually that handles the types the project uses:
        //   float, int, bool, string, Vector3, enums
        //
        // The output looks like:
        //   { "speed": 3.5, "label": "hello", "shape": "Cube" }
        //
        // And the deserializer parses it using a basic tokenizer.
        // ──────────────────────────────────────────────────────────────

        string SerializeBlockProperties(Block block)
        {
            var type = block.GetType();
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);

            var sb = new StringBuilder();
            sb.Append('{');
            bool first = true;

            foreach (var f in fields)
            {
                // Skip base-class fields (id, blockType, editorPosition, ports).
                if (f.DeclaringType == typeof(Block)) continue;
                if (f.FieldType == typeof(List<Port>)) continue;

                if (!first) sb.Append(',');
                first = false;

                sb.Append('"').Append(f.Name).Append("\":");
                var val = f.GetValue(block);
                sb.Append(ValueToJson(f.FieldType, val));
            }

            sb.Append('}');
            return sb.ToString();
        }

        static string ValueToJson(Type fieldType, object val)
        {
            if (val == null) return "null";

            if (fieldType == typeof(float))
                return ((float)val).ToString("R", CultureInfo.InvariantCulture);
            if (fieldType == typeof(int))
                return ((int)val).ToString(CultureInfo.InvariantCulture);
            if (fieldType == typeof(bool))
                return (bool)val ? "true" : "false";
            if (fieldType == typeof(string))
                return "\"" + EscapeJson((string)val) + "\"";
            if (fieldType == typeof(Vector3))
            {
                var v = (Vector3)val;
                return $"{{\"x\":{v.x.ToString("R", CultureInfo.InvariantCulture)}," +
                       $"\"y\":{v.y.ToString("R", CultureInfo.InvariantCulture)}," +
                       $"\"z\":{v.z.ToString("R", CultureInfo.InvariantCulture)}}}";
            }
            if (fieldType.IsEnum)
                return "\"" + val.ToString() + "\"";

            // Fallback: try ToString
            return "\"" + EscapeJson(val.ToString()) + "\"";
        }

        static string EscapeJson(string s)
        {
            if (s == null) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"")
                    .Replace("\n", "\\n").Replace("\r", "\\r")
                    .Replace("\t", "\\t");
        }

        // ── Deserialization ──────────────────────────────────────────────
        //
        // Parses a flat JSON object produced by SerializeBlockProperties,
        // matches keys to the block's public fields, and converts each
        // value to the correct C# type.

        void DeserializeBlockProperties(Block block, string json)
        {
            if (string.IsNullOrEmpty(json) || json == "{}") return;

            var type = block.GetType();
            var pairs = ParseJsonObject(json);

            foreach (var kv in pairs)
            {
                var field = type.GetField(kv.Key, BindingFlags.Public | BindingFlags.Instance);
                if (field == null || field.DeclaringType == typeof(Block)) continue;

                try
                {
                    field.SetValue(block, ConvertJsonValue(kv.Value, field.FieldType));
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[JsonGraphSerializer] Failed to set {type.Name}.{kv.Key}: {e.Message}");
                }
            }
        }

        static object ConvertJsonValue(string raw, Type target)
        {
            raw = raw.Trim();

            if (raw == "null") return null;

            if (target == typeof(float))
                return float.Parse(raw, CultureInfo.InvariantCulture);
            if (target == typeof(int))
                return (int)float.Parse(raw, CultureInfo.InvariantCulture);
            if (target == typeof(bool))
                return raw == "true";
            if (target == typeof(string))
                return UnescapeJsonString(raw);
            if (target == typeof(Vector3))
                return ParseVector3(raw);
            if (target.IsEnum)
                return Enum.Parse(target, UnescapeJsonString(raw));

            // Fallback — try Convert
            return Convert.ChangeType(UnescapeJsonString(raw), target, CultureInfo.InvariantCulture);
        }

        static string UnescapeJsonString(string s)
        {
            // Strip surrounding quotes if present
            if (s.Length >= 2 && s[0] == '"' && s[s.Length - 1] == '"')
                s = s.Substring(1, s.Length - 2);
            return s.Replace("\\n", "\n").Replace("\\r", "\r")
                    .Replace("\\t", "\t").Replace("\\\"", "\"")
                    .Replace("\\\\", "\\");
        }

        static Vector3 ParseVector3(string json)
        {
            // Expected format: {"x":1.0,"y":2.0,"z":3.0}
            var inner = ParseJsonObject(json);
            float x = 0, y = 0, z = 0;
            if (inner.TryGetValue("x", out var xs)) x = float.Parse(xs, CultureInfo.InvariantCulture);
            if (inner.TryGetValue("y", out var ys)) y = float.Parse(ys, CultureInfo.InvariantCulture);
            if (inner.TryGetValue("z", out var zs)) z = float.Parse(zs, CultureInfo.InvariantCulture);
            return new Vector3(x, y, z);
        }

        // ── Minimal JSON object parser ──────────────────────────────────
        //
        // Splits a flat JSON object string into key/value pairs.
        // Handles nested objects (like Vector3) as raw substrings.
        // We don't pull in a whole JSON library for this — the format
        // is entirely under our control.

        static Dictionary<string, string> ParseJsonObject(string json)
        {
            var result = new Dictionary<string, string>();
            json = json.Trim();
            if (json.Length < 2 || json[0] != '{') return result;

            int i = 1; // skip opening brace

            while (i < json.Length)
            {
                SkipWhitespace(json, ref i);
                if (i >= json.Length || json[i] == '}') break;
                if (json[i] == ',') { i++; continue; }

                // key
                string key = ReadString(json, ref i);
                SkipWhitespace(json, ref i);
                if (i < json.Length && json[i] == ':') i++; // skip colon
                SkipWhitespace(json, ref i);

                // value (could be string, number, bool, null, or nested object)
                string value = ReadValue(json, ref i);
                result[key] = value;
            }

            return result;
        }

        static void SkipWhitespace(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }

        static string ReadString(string s, ref int i)
        {
            if (i >= s.Length || s[i] != '"') return "";
            i++; // skip opening quote
            var sb = new StringBuilder();
            while (i < s.Length)
            {
                if (s[i] == '\\' && i + 1 < s.Length)
                {
                    sb.Append(s[i]);
                    sb.Append(s[i + 1]);
                    i += 2;
                }
                else if (s[i] == '"')
                {
                    i++; // skip closing quote
                    break;
                }
                else
                {
                    sb.Append(s[i]);
                    i++;
                }
            }
            return sb.ToString();
        }

        static string ReadValue(string s, ref int i)
        {
            if (i >= s.Length) return "";

            if (s[i] == '"')
            {
                // String value — return with quotes so converter knows the type
                int start = i;
                i++; // skip opening quote
                while (i < s.Length)
                {
                    if (s[i] == '\\' && i + 1 < s.Length) { i += 2; continue; }
                    if (s[i] == '"') { i++; break; }
                    i++;
                }
                return s.Substring(start, i - start);
            }

            if (s[i] == '{')
            {
                // Nested object — capture the whole substring including braces
                int depth = 0;
                int start = i;
                while (i < s.Length)
                {
                    if (s[i] == '{') depth++;
                    else if (s[i] == '}') { depth--; if (depth == 0) { i++; break; } }
                    i++;
                }
                return s.Substring(start, i - start);
            }

            // Number, bool, null
            {
                int start = i;
                while (i < s.Length && s[i] != ',' && s[i] != '}' && !char.IsWhiteSpace(s[i]))
                    i++;
                return s.Substring(start, i - start);
            }
        }
    }
}
