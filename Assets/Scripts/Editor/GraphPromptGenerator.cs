using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEngine.Networking;
using BlockSystem.Core;

namespace BlockSystem.Editor
{
    // GraphPromptGenerator: Takes a natural language prompt and generates a complete block graph
    // Example: "spawn a cube, wait 2 seconds, then move it forward 5 units with speed 2"
    // Output: Creates SpawnBlock → DelayBlock → MoveBlock with proper connections
    public class GraphPromptGenerator
    {
        // API Key split for security (same as AIBlock)
        private string apiKeyPart1 = "sk-proj-RRiARPRXWuCKbtKKzEUDFh7pPRKxNSyhjD89v5jHZ8";
        private string apiKeyPart2 = "tnSH3zoT86I9Lq7UNK5jzLQKogjfKajbT3BlbkFJ4W-QF";
        private string apiKeyPart3 = "_VFhXhzGRXb6DJRV6HhBYXxYBHvO_zfIAn3_JgOHc6Ik_A4bFRhlT-RP4agsF_7LFn2YA";

        string GetApiKey() => apiKeyPart1 + apiKeyPart2 + apiKeyPart3;

        public IEnumerator GenerateGraphFromPrompt(BlockGraph graph, string userPrompt, Action<bool, string> onComplete)
        {
            if (graph == null)
            {
                onComplete?.Invoke(false, "No graph loaded");
                yield break;
            }

            if (string.IsNullOrEmpty(userPrompt))
            {
                onComplete?.Invoke(false, "Prompt cannot be empty");
                yield break;
            }

            Debug.Log($"GraphPromptGenerator: Generating graph from prompt: '{userPrompt}'");

            // Build system prompt with available blocks
            string systemPrompt = BuildSystemPrompt();

            // Call AI to generate graph structure
            yield return CallGeneratorAI(systemPrompt, userPrompt, (success, jsonResponse) =>
            {
                if (!success)
                {
                    onComplete?.Invoke(false, "AI generation failed: " + jsonResponse);
                    return;
                }

                try
                {
                    Debug.Log($"GraphPromptGenerator: AI response: {jsonResponse}");
                    ParseAndCreateGraph(graph, jsonResponse);
                    onComplete?.Invoke(true, "Graph generated successfully!");
                }
                catch (Exception e)
                {
                    onComplete?.Invoke(false, "Failed to parse AI response: " + e.Message);
                }
            });
        }

        string BuildSystemPrompt()
        {
            return @"You are a block graph generator for a visual scripting system. 
Given a user's natural language description, generate a block graph.

Available blocks and their EXACT port names:
- SpawnBlock:  inputs=[Start(Flow), Position(Vector3)]  outputs=[Next(Flow), Object(GameObject)]
- MoveBlock:   inputs=[Start(Flow), Target(GameObject), Offset(Vector3), Speed(Float)]  outputs=[Next(Flow)]
- RotateBlock:  inputs=[Start(Flow), Target(GameObject), Angles(Vector3), Speed(Float)]  outputs=[Next(Flow)]
- ScaleBlock:  inputs=[Start(Flow), Target(GameObject), Scale(Vector3)]  outputs=[Next(Flow)]

IMPORTANT:
- Use the EXACT port names listed above!
- Connect BOTH flow ports AND data ports.
- SpawnBlock.Object MUST connect to MoveBlock/RotateBlock/ScaleBlock Target input.
- Chain flow: SpawnBlock.Next->MoveBlock.Start, MoveBlock.Next->RotateBlock.Start, etc.
- Chain data: SpawnBlock.Object->MoveBlock.Target, SpawnBlock.Object->RotateBlock.Target, etc.
- Always respond with ONLY valid JSON (no markdown, no explanations).

Format your response as:
{
  ""blocks"": [
    {
      ""id"": ""block_0"",
      ""type"": ""SpawnBlock"",
      ""position"": [0, 0],
      ""params"": {
        ""prefabName"": ""Cube""
      }
    },
    {
      ""id"": ""block_1"",
      ""type"": ""DelayBlock"",
      ""position"": [300, 0],
      ""params"": {
        ""duration"": 2.0
      }
    }
  ],
  ""connections"": [
    {
      ""fromBlock"": ""block_0"",
      ""fromPort"": ""Next"",
      ""toBlock"": ""block_1"",
      ""toPort"": ""In""
    }
  ]
}

Rules:
- Value blocks (FloatValue, Vector3Value) have NO flow ports, only data ports
- Connect BOTH flow ports AND data ports
- SpawnBlock.Object output MUST connect to MoveBlock/RotateBlock/ScaleBlock Target input
- Connect blocks sequentially using their flow ports
- Position blocks horizontally (x + 300 for each step)";
        }

        IEnumerator CallGeneratorAI(string systemPrompt, string userPrompt, Action<bool, string> onComplete)
        {
            string jsonRequest = "{\"model\":\"gpt-4o\"," +
                "\"messages\":[" +
                "{\"role\":\"system\",\"content\":\"" + EscapeJson(systemPrompt) + "\"}," +
                "{\"role\":\"user\",\"content\":\"" + EscapeJson(userPrompt) + "\"}" +
                "]," +
                "\"temperature\":0.5," +
                "\"max_tokens\":2000" +
                "}";

            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonRequest);

            UnityWebRequest request = new UnityWebRequest("https://api.openai.com/v1/chat/completions", "POST");
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + GetApiKey());

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"GraphPromptGenerator: AI error: {request.error}");
                onComplete?.Invoke(false, request.error);
            }
            else
            {
                string responseText = request.downloadHandler.text;
                string aiContent = ExtractAIContent(responseText);
                onComplete?.Invoke(true, aiContent);
            }
        }

        string ExtractAIContent(string jsonResponse)
        {
            // Find "assistant" role first, then get its content
            int assistantIdx = jsonResponse.IndexOf("\"assistant\"");
            if (assistantIdx == -1)
            {
                assistantIdx = jsonResponse.LastIndexOf("\"content\"");
                if (assistantIdx == -1) return "";
            }

            int contentIndex = jsonResponse.IndexOf("\"content\":", assistantIdx);
            if (contentIndex == -1)
            {
                contentIndex = jsonResponse.IndexOf("\"content\":", assistantIdx - 50);
                if (contentIndex == -1) return "";
            }

            contentIndex = jsonResponse.IndexOf("\"", contentIndex + 10);
            if (contentIndex == -1) return "";
            contentIndex++;

            int endIndex = contentIndex;
            while (endIndex < jsonResponse.Length)
            {
                endIndex = jsonResponse.IndexOf("\"", endIndex);
                if (endIndex == -1) break;

                int backslashCount = 0;
                int checkIdx = endIndex - 1;
                while (checkIdx >= 0 && jsonResponse[checkIdx] == '\\')
                {
                    backslashCount++;
                    checkIdx--;
                }

                if (backslashCount % 2 == 0) break;
                endIndex++;
            }

            if (endIndex == -1 || endIndex <= contentIndex) return "";

            string content = jsonResponse.Substring(contentIndex, endIndex - contentIndex);
            content = content.Replace("\\n", "\n")
                           .Replace("\\\"", "\"")
                           .Replace("\\\\", "\\")
                           .Replace("\\t", "\t");
            return content;
        }

        void ParseAndCreateGraph(BlockGraph graph, string jsonResponse)
        {
            // Extract JSON from markdown code blocks if present
            jsonResponse = jsonResponse.Trim();
            if (jsonResponse.StartsWith("```"))
            {
                int start = jsonResponse.IndexOf('{');
                int end = jsonResponse.LastIndexOf('}');
                if (start != -1 && end != -1)
                {
                    jsonResponse = jsonResponse.Substring(start, end - start + 1);
                }
            }

            // Parse blocks
            Dictionary<string, (string type, Vector2 pos, Dictionary<string, string> @params)> blockData = new();
            int blockCount = CountOccurrences(jsonResponse, "\"id\":");

            for (int i = 0; i < blockCount; i++)
            {
                string id = ExtractJsonValue(jsonResponse, "\"id\":", i);
                string type = ExtractJsonValue(jsonResponse, "\"type\":", i);
                string posStr = ExtractJsonValue(jsonResponse, "\"position\":", i);

                Vector2 pos = Vector2.zero;
                if (posStr.Contains("["))
                {
                    string[] parts = posStr.Split(',');
                    if (parts.Length >= 2)
                    {
                        float.TryParse(parts[0].Replace("[", "").Trim(), out float x);
                        float.TryParse(parts[1].Replace("]", "").Trim(), out float y);
                        pos = new Vector2(x, y);
                    }
                }

                blockData[id] = (type, pos, new Dictionary<string, string>());
            }

            // Create blocks in graph
            Dictionary<string, string> idToBlockId = new();
            foreach (var (id, (type, pos, @params)) in blockData)
            {
                Type blockType = GetBlockType(type);
                if (blockType == null)
                {
                    Debug.LogWarning($"GraphPromptGenerator: Unknown block type '{type}'");
                    continue;
                }

                Block block = graph.AddBlock(blockType);
                block.editorPosition = pos;
                idToBlockId[id] = block.id;

                Debug.Log($"GraphPromptGenerator: Created {type} at {pos}");
            }

            // Parse and connect blocks
            int connCount = CountOccurrences(jsonResponse, "\"fromBlock\":");
            for (int i = 0; i < connCount; i++)
            {
                string fromId = ExtractJsonValue(jsonResponse, "\"fromBlock\":", i);
                string fromPort = ExtractJsonValue(jsonResponse, "\"fromPort\":", i);
                string toId = ExtractJsonValue(jsonResponse, "\"toBlock\":", i);
                string toPort = ExtractJsonValue(jsonResponse, "\"toPort\":", i);

                if (idToBlockId.ContainsKey(fromId) && idToBlockId.ContainsKey(toId))
                {
                    string realFromId = idToBlockId[fromId];
                    string realToId = idToBlockId[toId];
                    string resolvedFrom = ResolvePortName(graph.GetBlock(realFromId), fromPort, false);
                    string resolvedTo = ResolvePortName(graph.GetBlock(realToId), toPort, true);
                    
                    bool ok = graph.Connect(realFromId, resolvedFrom, realToId, resolvedTo);
                    Debug.Log($"GraphPromptGenerator: {(ok ? "\u2713" : "\u2717")} {fromId}.{resolvedFrom} \u2192 {toId}.{resolvedTo}");
                }
            }

            EditorUtility.SetDirty(graph);
            Debug.Log($"GraphPromptGenerator: Graph generation complete!");
        }

        // Fuzzy port name matching - AI might say "In" when the real port is "Start", etc.
        string ResolvePortName(Block block, string aiPortName, bool isInput)
        {
            if (block == null) return aiPortName;

            var ports = isInput ? block.inputs : block.outputs;

            if (ports.Any(p => p.name == aiPortName))
                return aiPortName;

            var flowAliases = new Dictionary<string, string[]>
            {
                { "In",    new[] { "Start", "In" } },
                { "Start", new[] { "In", "Start" } },
                { "Next",  new[] { "Out", "Next", "Complete" } },
                { "Out",   new[] { "Next", "Out", "Complete" } },
            };

            if (flowAliases.TryGetValue(aiPortName, out var alternatives))
            {
                foreach (var alt in alternatives)
                {
                    if (ports.Any(p => p.name == alt))
                        return alt;
                }
            }

            var flowPort = ports.FirstOrDefault(p => p.type == PortType.Flow);
            if (flowPort != null) return flowPort.name;

            return aiPortName;
        }

        Type GetBlockType(string typeName)
        {
            // Search through all loaded types for a matching block
            var assembly = typeof(Block).Assembly;
            var blockType = assembly.GetTypes()
                .FirstOrDefault(t => t.Name == typeName && typeof(Block).IsAssignableFrom(t));
            return blockType;
        }

        int CountOccurrences(string text, string pattern)
        {
            int count = 0;
            int index = 0;
            while ((index = text.IndexOf(pattern, index)) != -1)
            {
                count++;
                index += pattern.Length;
            }
            return count;
        }

        string ExtractJsonValue(string json, string key, int occurrence = 0)
        {
            int index = 0;
            for (int i = 0; i <= occurrence; i++)
            {
                index = json.IndexOf(key, index);
                if (index == -1) return "";
                if (i < occurrence) index += key.Length;
            }

            index += key.Length;

            // Skip whitespace and quote
            while (index < json.Length && (json[index] == ' ' || json[index] == '\"' || json[index] == '['))
                index++;

            // Find end
            int endIndex = index;
            bool inString = false;
            bool inArray = false;

            while (endIndex < json.Length)
            {
                if (json[endIndex] == '\"' && json[endIndex - 1] != '\\')
                    inString = !inString;
                if (json[endIndex] == '[')
                    inArray = true;
                if (json[endIndex] == ']')
                    inArray = false;

                if ((json[endIndex] == ',' || json[endIndex] == '}') && !inString && !inArray)
                    break;

                endIndex++;
            }

            string result = json.Substring(index, endIndex - index).Trim();
            return result.Replace("\"", "").Trim();
        }

        string EscapeJson(string text)
        {
            return text.Replace("\\", "\\\\")
                       .Replace("\"", "\\\"")
                       .Replace("\n", "\\n")
                       .Replace("\r", "\\r")
                       .Replace("\t", "\\t");
        }
    }
}
