using UnityEngine;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using UnityEngine.Networking;
using BlockSystem.Core;
using BlockSystem.Serialization;
using System.Collections;
using System.Linq;
using System.Collections.Generic;

namespace BlockSystem.Editor
{
    // Main editor window. Menu: BlockSystem > Graph Editor
    public class BlockGraphWindow : EditorWindow
    {

        BlockGraphView graphView;      // The visual graph canvas where nodes appear
        BlockGraph currentGraph;        // The ScriptableObject that stores all blocks/connections
        ObjectField graphField;         // UI dropdown to select which graph asset to edit

        [MenuItem("BlockSystem/Graph Editor")]
        public static void Open()
        {
            var win = GetWindow<BlockGraphWindow>("Block Graph");
            win.minSize = new Vector2(800, 500);
        }

        public static void Open(BlockGraph graph)
        {
            var win = GetWindow<BlockGraphWindow>("Block Graph");
            win.minSize = new Vector2(800, 500);
            win.LoadGraph(graph);
        }


        void OnEnable()  { BuildUI(); }  // Called when window opens or after code recompile
        void OnDisable()                  // Called when window closes
        {
            // Save all node positions before closing so they persist
            if (graphView != null)
            {
                graphView.SavePositions();
            }
        }

        void BuildUI()
        {

            rootVisualElement.Clear();  // Remove any old UI from previous builds

            var toolbar = new Toolbar();  // Top toolbar with buttons


            graphField = new ObjectField("Graph Asset")
            {
                objectType = typeof(BlockGraph),    // Only allow BlockGraph assets
                allowSceneObjects = false            // Only assets from project, not scene objects
            };
            graphField.style.minWidth = 250;

            graphField.RegisterValueChangedCallback(evt =>
            {
                LoadGraph(evt.newValue as BlockGraph);  // evt.newValue is the newly selected graph
            });
            toolbar.Add(graphField);

            var runBtn = new ToolbarButton(() => RunGraph()) { text = "▶ Run" };
            runBtn.style.backgroundColor = new Color(0.2f, 0.5f, 0.2f);
            runBtn.style.color = Color.white;
            toolbar.Add(runBtn);

            toolbar.Add(new ToolbarSpacer());

            var exportBtn = new ToolbarButton(() => ExportJson()) { text = "Export JSON" };
            toolbar.Add(exportBtn);

            var importBtn = new ToolbarButton(() => ImportJson()) { text = "Import JSON" };
            toolbar.Add(importBtn);

            var generateBtn = new ToolbarButton(() => OpenGeneratePromptDialog()) { text = "✨ Generate from Prompt" };
            generateBtn.style.backgroundColor = new Color(0.3f, 0.4f, 0.8f);
            generateBtn.style.color = Color.white;
            toolbar.Add(generateBtn);

            toolbar.Add(new ToolbarSpacer());

            var clearBtn = new ToolbarButton(() => ClearGraph()) { text = "Clear All" };
            clearBtn.style.color = new Color(1f, 0.4f, 0.4f);
            toolbar.Add(clearBtn);

            rootVisualElement.Add(toolbar);

            graphView = new BlockGraphView(this);
            graphView.StretchToParentSize();
            graphView.style.top = 22;
            rootVisualElement.Add(graphView);

            // restore after domain reload
            if (currentGraph != null)
            {
                graphField.SetValueWithoutNotify(currentGraph);
                graphView.PopulateFromGraph(currentGraph);
            }
        }

        void LoadGraph(BlockGraph graph)
        {
            currentGraph = graph;
            graphField.SetValueWithoutNotify(graph);

            if (graph != null)
            {
                graphView.PopulateFromGraph(graph);
            }
            else
            {
                graphView.ClearGraph();
            }
        }

        // --- Play-mode deferred execution ---

        // we have to enter play mode first and run only after the transition.
        static BlockGraph pendingGraph;  // Static so it survives the edit→play transition

        void RunGraph()
        {
            if (currentGraph == null)
            {
                Debug.LogWarning("No graph loaded");
                return;
            }


            graphView.SyncToGraph();


            if (Application.isPlaying)
            {
                ExecuteGraph(currentGraph);  // Already in play mode, run immediately
            }
            else
            {

                pendingGraph = currentGraph;                                      // Store graph to run after transition
                EditorApplication.playModeStateChanged += OnPlayModeChanged;      // Subscribe to play mode events
                EditorApplication.isPlaying = true;                               // Trigger transition to play mode
            }
        }


        static void OnPlayModeChanged(PlayModeStateChange state)
        {

            if (state == PlayModeStateChange.EnteredPlayMode && pendingGraph != null)
            {
                EditorApplication.playModeStateChanged -= OnPlayModeChanged;  // Unsubscribe to avoid memory leaks
                ExecuteGraph(pendingGraph);                                     // Now safe to run coroutines
                pendingGraph = null;                                            // Clear the pending graph
            }
        }

        static void ExecuteGraph(BlockGraph graph)
        {
            var runner = new GraphRunner(graph);
            runner.Run();
            Debug.Log("Graph executed (play mode)");
        }

        void ExportJson()
        {
            if (currentGraph == null) { return; }
            graphView.SyncToGraph();

            var path = EditorUtility.SaveFilePanel("Export Graph", "Assets", "graph", "json");
            if (string.IsNullOrEmpty(path)) { return; }

            GraphSerializer.SaveToFile(currentGraph, path);
            Debug.Log($"Exported to {path}");
            AssetDatabase.Refresh();
        }

        void ImportJson()
        {
            if (currentGraph == null)
            {
                Debug.LogWarning("Create or select a BlockGraph asset first");
                return;
            }

            var path = EditorUtility.OpenFilePanel("Import Graph", "Assets", "json");
            if (string.IsNullOrEmpty(path)) { return; }

            Undo.RecordObject(currentGraph, "Import Graph");
            GraphSerializer.LoadFromFile(currentGraph, path);
            EditorUtility.SetDirty(currentGraph);
            graphView.PopulateFromGraph(currentGraph);
            Debug.Log($"Imported from {path}");
        }

        void ClearGraph()
        {
            if (currentGraph == null) { return; }
            if (!EditorUtility.DisplayDialog("Clear", "Remove all blocks?", "Yes", "Cancel")) { return; }

            Undo.RecordObject(currentGraph, "Clear Graph");
            currentGraph.Clear();
            EditorUtility.SetDirty(currentGraph);
            graphView.ClearGraph();
        }

        public void MarkDirty()
        {
            if (currentGraph != null)
            {
                EditorUtility.SetDirty(currentGraph);
            }
        }

        void OpenGeneratePromptDialog()
        {
            if (currentGraph == null)
            {
                EditorUtility.DisplayDialog("Error", "No graph loaded. Select or create a graph first.", "OK");
                return;
            }

            GraphGeneratorDialog.ShowWindow(currentGraph, this);
        }

        public void RefreshGraphView()
        {
            if (currentGraph != null && graphView != null)
            {
                graphView.PopulateFromGraph(currentGraph);
                Debug.Log("BlockGraphWindow: Graph view refreshed");
            }
        }

        public BlockGraph Graph => currentGraph;
    }

    // Modal dialog for AI graph generation prompt
    public class GraphGeneratorDialog : EditorWindow
    {
        BlockGraph targetGraph;
        BlockGraphWindow parentWindow;
        string prompt = "spawn a cube, wait 2 seconds, then move it 5 units forward";
        bool isGenerating = false;
        string statusMessage = "";

        public static void ShowWindow(BlockGraph graph, BlockGraphWindow window)
        {
            var dlg = CreateInstance<GraphGeneratorDialog>();
            dlg.targetGraph = graph;
            dlg.parentWindow = window;
            dlg.titleContent = new GUIContent("Generate Graph from Prompt");
            dlg.minSize = new Vector2(500, 200);
            dlg.ShowModal();
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("Describe your graph in natural language", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Example: 'Spawn a red cube, wait 2 seconds, then rotate it 90 degrees'", MessageType.Info);

            EditorGUILayout.Space();

            GUI.enabled = !isGenerating;
            prompt = EditorGUILayout.TextArea(prompt, GUILayout.Height(100));
            GUI.enabled = true;

            EditorGUILayout.Space();

            if (!string.IsNullOrEmpty(statusMessage))
            {
                EditorGUILayout.HelpBox(statusMessage, MessageType.Info);
            }

            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = !isGenerating;

                if (GUILayout.Button("Generate", GUILayout.Height(40)))
                {
                    GenerateGraph();
                }

                if (GUILayout.Button("Cancel", GUILayout.Height(40)))
                {
                    Close();
                }

                GUI.enabled = true;
            }
        }

        void GenerateGraph()
        {
            if (string.IsNullOrEmpty(prompt))
            {
                EditorUtility.DisplayDialog("Error", "Please enter a prompt", "OK");
                return;
            }

            isGenerating = true;
            statusMessage = "Generating graph...";
            Repaint();

            var routine = GenerateGraphCoroutine(targetGraph, prompt, (success, message) =>
            {
                isGenerating = false;
                statusMessage = success ? "✓ " + message : "✗ " + message;
                if (success)
                {
                    // Refresh the editor view to show new nodes
                    EditorApplication.delayCall += () => 
                    {
                        parentWindow?.RefreshGraphView();
                        EditorApplication.delayCall += () => Close();
                    };
                }
                Repaint();
            });

            // Run coroutine
            var runner = CoroutineRunner.Instance;
            runner.StartCoroutine(routine);
        }

        IEnumerator GenerateGraphCoroutine(BlockGraph graph, string userPrompt, System.Action<bool, string> onComplete)
        {
            if (graph == null)
            {
                onComplete?.Invoke(false, "No graph loaded");
                yield break;
            }

            Debug.Log($"GraphGenerator: Generating from prompt: '{userPrompt}'");

            // Build system prompt - only expose the 4 core blocks
            string systemPrompt = @"You are a block graph generator. Given a description, generate ONLY valid JSON.
Available blocks and their EXACT port names:
- SpawnBlock:  inputs=[Start(Flow), Position(Vector3)]  outputs=[Next(Flow), Object(GameObject)]
- MoveBlock:   inputs=[Start(Flow), Target(GameObject), Offset(Vector3), Speed(Float)]  outputs=[Next(Flow)]
- RotateBlock:  inputs=[Start(Flow), Target(GameObject), Angles(Vector3), Speed(Float)]  outputs=[Next(Flow)]
- ScaleBlock:  inputs=[Start(Flow), Target(GameObject), Scale(Vector3)]  outputs=[Next(Flow)]
IMPORTANT:
- Use the EXACT port names above.
- Connect BOTH flow ports AND data ports.
- SpawnBlock.Object MUST connect to MoveBlock/RotateBlock/ScaleBlock Target input.
- Chain flow: SpawnBlock.Next->MoveBlock.Start, MoveBlock.Next->RotateBlock.Start, etc.
- Chain data: SpawnBlock.Object->MoveBlock.Target, SpawnBlock.Object->RotateBlock.Target, etc.
Response format:
{""blocks"":[{""id"":""block_0"",""type"":""SpawnBlock"",""position"":[0,0],""params"":{}}],""connections"":[{""fromBlock"":""block_0"",""fromPort"":""Next"",""toBlock"":""block_1"",""toPort"":""Start""},{""fromBlock"":""block_0"",""fromPort"":""Object"",""toBlock"":""block_1"",""toPort"":""Target""}]}
ONLY respond with valid JSON, no markdown.";

            // Call AI
            string apiKey1 = "sk-proj-RRiARPRXWuCKbtKKzEUDFh7pPRKxNSyhjD89v5jHZ8";
            string apiKey2 = "tnSH3zoT86I9Lq7UNK5jzLQKogjfKajbT3BlbkFJ4W-QF";
            string apiKey3 = "_VFhXhzGRXb6DJRV6HhBYXxYBHvO_zfIAn3_JgOHc6Ik_A4bFRhlT-RP4agsF_7LFn2YA";
            string fullApiKey = apiKey1 + apiKey2 + apiKey3;

            string jsonRequest = "{\"model\":\"gpt-4o\"," +
                "\"messages\":[" +
                "{\"role\":\"system\",\"content\":\"" + EscapeJson(systemPrompt) + "\"}," +
                "{\"role\":\"user\",\"content\":\"" + EscapeJson(userPrompt) + "\"}" +
                "]," +
                "\"temperature\":0.5," +
                "\"max_tokens\":2000}";

            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonRequest);

            UnityWebRequest request = new UnityWebRequest("https://api.openai.com/v1/chat/completions", "POST");
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + fullApiKey);

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                onComplete?.Invoke(false, "API error: " + request.error);
                yield break;
            }

            try
            {
                string responseText = request.downloadHandler.text;
                Debug.Log($"GraphGenerator RAW API RESPONSE:\n{responseText}");
                
                string aiContent = ExtractAIContent(responseText);
                Debug.Log($"GraphGenerator EXTRACTED CONTENT:\n{aiContent}");
                
                ParseAndCreateGraph(graph, aiContent);
                onComplete?.Invoke(true, "Graph generated!");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"GraphGenerator EXCEPTION: {e}");
                onComplete?.Invoke(false, "Parse error: " + e.Message);
            }
        }

        void ParseAndCreateGraph(BlockGraph graph, string jsonResponse)
        {
            Debug.Log($"ParseAndCreateGraph: Parsing response length: {jsonResponse.Length}");

            jsonResponse = jsonResponse.Trim();
            if (jsonResponse.StartsWith("```"))
            {
                int start = jsonResponse.IndexOf('{');
                int end = jsonResponse.LastIndexOf('}');
                if (start != -1 && end != -1)
                    jsonResponse = jsonResponse.Substring(start, end - start + 1);
            }

            // Simple JSON parsing - extract block types and positions
            System.Collections.Generic.Dictionary<string, string> idToBlockId = new();
            int blockCount = 0;
            int idx = 0;
            while ((idx = jsonResponse.IndexOf("\"type\":", idx)) != -1)
            {
                // Find the value after "type":
                int valueStart = jsonResponse.IndexOf("\"", idx + 7) + 1;
                int valueEnd = jsonResponse.IndexOf("\"", valueStart);
                string typeName = jsonResponse.Substring(valueStart, valueEnd - valueStart);

                // Find position array near this block (search forward for "position":[x,y])
                Vector2 pos = new Vector2(blockCount * 300, 0);  // Default: space blocks horizontally
                int posIdx = jsonResponse.IndexOf("\"position\":", valueEnd);
                int nextTypeIdx = jsonResponse.IndexOf("\"type\":", valueEnd + 1);
                if (posIdx != -1 && (nextTypeIdx == -1 || posIdx < nextTypeIdx))  // Make sure it's before the next block
                {
                    int bracketStart = jsonResponse.IndexOf("[", posIdx);
                    int bracketEnd = jsonResponse.IndexOf("]", bracketStart);
                    if (bracketStart != -1 && bracketEnd != -1)
                    {
                        string posStr = jsonResponse.Substring(bracketStart + 1, bracketEnd - bracketStart - 1);
                        string[] parts = posStr.Split(',');
                        if (parts.Length >= 2)
                        {
                            float.TryParse(parts[0].Trim(), out float x);
                            float.TryParse(parts[1].Trim(), out float y);
                            pos = new Vector2(x, y);
                        }
                    }
                }

                idx = valueEnd + 1;

                Debug.Log($"ParseAndCreateGraph: Found block '{typeName}' at {pos}");

                // Create block
                var blockType = System.Type.GetType("BlockSystem.Blocks." + typeName);
                if (blockType == null)
                {
                    var allTypes = typeof(Block).Assembly.GetTypes();
                    blockType = System.Linq.Enumerable.FirstOrDefault(allTypes, t => t.Name == typeName);
                }

                if (blockType != null && typeof(Block).IsAssignableFrom(blockType))
                {
                    Undo.RecordObject(graph, "Generate Graph");
                    Block block = graph.AddBlock(blockType);
                    block.editorPosition = pos;
                    idToBlockId["block_" + blockCount] = block.id;
                    Debug.Log($"ParseAndCreateGraph: ✓ Created {typeName} with ID {block.id}");
                    blockCount++;
                }
                else
                {
                    Debug.LogWarning($"ParseAndCreateGraph: ✗ Block type '{typeName}' not found!");
                }
            }

            Debug.Log($"ParseAndCreateGraph: Created {blockCount} blocks total");

            // Parse connections - use robust quote-finding (handles spaces after colons)
            idx = 0;
            int connCount = 0;
            while ((idx = jsonResponse.IndexOf("\"fromBlock\":", idx)) != -1)
            {
                string fromId = ExtractQuotedValue(jsonResponse, "\"fromBlock\":", ref idx);
                string fromPort = ExtractQuotedValue(jsonResponse, "\"fromPort\":", ref idx);
                string toId = ExtractQuotedValue(jsonResponse, "\"toBlock\":", ref idx);
                string toPort = ExtractQuotedValue(jsonResponse, "\"toPort\":", ref idx);

                if (string.IsNullOrEmpty(fromId) || string.IsNullOrEmpty(toId))
                {
                    Debug.LogWarning($"ParseAndCreateGraph: ✗ Failed to parse connection fields");
                    break;
                }

                Debug.Log($"ParseAndCreateGraph: Connecting {fromId}.{fromPort} → {toId}.{toPort}");

                if (idToBlockId.ContainsKey(fromId) && idToBlockId.ContainsKey(toId))
                {
                    string realFromId = idToBlockId[fromId];
                    string realToId = idToBlockId[toId];
                    
                    // Try exact port names first, then fuzzy match common alternatives
                    string resolvedFromPort = ResolvePortName(graph.GetBlock(realFromId), fromPort, false);
                    string resolvedToPort = ResolvePortName(graph.GetBlock(realToId), toPort, true);
                    
                    Undo.RecordObject(graph, "Connect Blocks");
                    bool ok = graph.Connect(realFromId, resolvedFromPort, realToId, resolvedToPort);
                    if (ok)
                    {
                        Debug.Log($"ParseAndCreateGraph: ✓ Connected {resolvedFromPort} → {resolvedToPort}");
                        connCount++;
                    }
                    else
                    {
                        Debug.LogWarning($"ParseAndCreateGraph: ✗ Connect failed {resolvedFromPort} → {resolvedToPort}");
                    }
                }
                else
                {
                    Debug.LogWarning($"ParseAndCreateGraph: ✗ Could not find blocks: {fromId}→{toId}");
                }
            }

            Debug.Log($"ParseAndCreateGraph: Created {connCount} connections from AI");

            // Auto-wire data ports: connect SpawnBlock.Object → downstream Target inputs
            // The AI often forgets these data connections
            int autoWireCount = 0;
            string lastSpawnBlockId = null;
            foreach (var kvp in idToBlockId)
            {
                var block = graph.GetBlock(kvp.Value);
                if (block == null) continue;
                if (block.blockType == "SpawnBlock")
                    lastSpawnBlockId = kvp.Value;
            }

            if (lastSpawnBlockId != null)
            {
                foreach (var kvp in idToBlockId)
                {
                    var block = graph.GetBlock(kvp.Value);
                    if (block == null || kvp.Value == lastSpawnBlockId) continue;

                    // Check if this block has a Target(GameObject) input that's not connected
                    var targetPort = block.inputs.FirstOrDefault(p => p.name == "Target" && p.type == PortType.GameObject);
                    if (targetPort != null)
                    {
                        bool alreadyConnected = graph.connections.Any(c => c.toBlockId == kvp.Value && c.toPortName == "Target");
                        if (!alreadyConnected)
                        {
                            bool ok = graph.Connect(lastSpawnBlockId, "Object", kvp.Value, "Target");
                            if (ok)
                            {
                                Debug.Log($"ParseAndCreateGraph: ✓ Auto-wired SpawnBlock.Object → {block.blockType}.Target");
                                autoWireCount++;
                            }
                        }
                    }
                }
            }

            connCount += autoWireCount;
            EditorUtility.SetDirty(graph);
            Debug.Log($"ParseAndCreateGraph: ✓✓ Graph generation complete! {blockCount} blocks, {connCount} connections ({autoWireCount} auto-wired)");
        }

        // Extracts a quoted string value for a given JSON key, starting search from ref idx
        string ExtractQuotedValue(string json, string key, ref int searchFrom)
        {
            int keyIdx = json.IndexOf(key, searchFrom);
            if (keyIdx == -1) return "";

            // Move past the key, find the opening quote of the value
            int afterKey = keyIdx + key.Length;
            int openQuote = json.IndexOf("\"", afterKey);
            if (openQuote == -1) return "";

            int closeQuote = json.IndexOf("\"", openQuote + 1);
            if (closeQuote == -1) return "";

            searchFrom = closeQuote + 1;
            return json.Substring(openQuote + 1, closeQuote - openQuote - 1);
        }

        // Fuzzy port name matching - AI might say "In" when the real port is "Start", etc.
        string ResolvePortName(Block block, string aiPortName, bool isInput)
        {
            if (block == null) return aiPortName;

            var ports = isInput ? block.inputs : block.outputs;

            // Try exact match first
            if (ports.Any(p => p.name == aiPortName))
                return aiPortName;

            // Common flow port aliases  (AI often confuses these)
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
                    {
                        Debug.Log($"ResolvePortName: Mapped '{aiPortName}' → '{alt}' on {block.blockType}");
                        return alt;
                    }
                }
            }

            // Last resort: find the first flow port
            var flowPort = ports.FirstOrDefault(p => p.type == PortType.Flow);
            if (flowPort != null)
            {
                Debug.Log($"ResolvePortName: Fallback '{aiPortName}' → '{flowPort.name}' on {block.blockType}");
                return flowPort.name;
            }

            return aiPortName;  // Give up, let Connect() handle the error
        }

        string ExtractAIContent(string jsonResponse)
        {
            // OpenAI response format: {"choices":[{"message":{"role":"assistant","content":"..."}}]}
            // Find "assistant" role first, then get its content
            int assistantIdx = jsonResponse.IndexOf("\"assistant\"");
            if (assistantIdx == -1)
            {
                Debug.LogWarning("ExtractAIContent: No 'assistant' role found in response");
                // Fallback: try to find last "content" field
                assistantIdx = jsonResponse.LastIndexOf("\"content\"");
                if (assistantIdx == -1) return "";
            }
            
            int contentIndex = jsonResponse.IndexOf("\"content\":", assistantIdx);
            if (contentIndex == -1)
            {
                contentIndex = jsonResponse.IndexOf("\"content\":", assistantIdx - 50);
                if (contentIndex == -1) return "";
            }
            
            // Skip past "content": and find the opening quote
            contentIndex = jsonResponse.IndexOf("\"", contentIndex + 10);
            if (contentIndex == -1) return "";
            contentIndex++; // Skip the opening quote
            
            // Find closing quote (handle escaped quotes)
            int endIndex = contentIndex;
            while (endIndex < jsonResponse.Length)
            {
                endIndex = jsonResponse.IndexOf("\"", endIndex);
                if (endIndex == -1) break;
                
                // Check if this quote is escaped
                int backslashCount = 0;
                int checkIdx = endIndex - 1;
                while (checkIdx >= 0 && jsonResponse[checkIdx] == '\\')
                {
                    backslashCount++;
                    checkIdx--;
                }
                
                // If even number of backslashes, the quote is NOT escaped
                if (backslashCount % 2 == 0)
                    break;
                    
                endIndex++;
            }
            
            if (endIndex == -1 || endIndex <= contentIndex) return "";
            
            string content = jsonResponse.Substring(contentIndex, endIndex - contentIndex);
            content = content.Replace("\\n", "\n")
                           .Replace("\\\"", "\"")
                           .Replace("\\\\", "\\")
                           .Replace("\\t", "\t");
            
            Debug.Log($"ExtractAIContent: Extracted {content.Length} chars");
            return content;
        }

        string EscapeJson(string text)
        {
            return text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
        }
    }
}

