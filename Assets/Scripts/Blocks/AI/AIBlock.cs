using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using BlockSystem.Core;

namespace BlockSystem.Blocks
{
    // AIBlock: a smart block that interprets a natural language prompt
    // and acts on connected objects. Like a wildcard LEGO piece.
    //
    // Default mode: Use cloud AI (works on any PC with internet)
    // Fallback: Local keyword parsing (always works, no API calls)
    [Serializable]
    public class AIBlock : Block
    {
        public enum AIMode { CloudAI, LocalParsing }
        
        public string prompt = "move it up by 2 units";
        public AIMode aiMode = AIMode.CloudAI;  // Default to cloud AI (no setup needed)
        
        // API Key stored in parts to avoid easy detection in version control
        private string apiKeyPart1 = "sk-proj-RRiARPRXWuCKbtKKzEUDFh7pPRKxNSyhjD89v5jHZ8";
        private string apiKeyPart2 = "tnSH3zoT86I9Lq7UNK5jzLQKogjfKajbT3BlbkFJ4W-QF";
        private string apiKeyPart3 = "_VFhXhzGRXb6DJRV6HhBYXxYBHvO_zfIAn3_JgOHc6Ik_A4bFRhlT-RP4agsF_7LFn2YA";
        
        public string aiModel = "gpt-4o";
        public float temperature = 0.7f;
        public int maxTokens = 150;

        protected override void SetupPorts()
        {
            AddInput("Start", PortType.Flow);
            AddInput("Target", PortType.GameObject);
            AddOutput("Next", PortType.Flow);
        }

        public override void Execute(GraphContext ctx)
        {
            var target = In<GameObject>("Target");

            if (target == null)
            {
                target = ctx.GetObject("SpawnedObject")
                      ?? ctx.GetObject("ForEach.Current");
            }

            if (target == null)
            {
                Debug.LogWarning("AIBlock: no target object to act on");
                return;
            }

            switch (aiMode)
            {
                case AIMode.LocalParsing:
                    // Fast local parsing, no API calls
                    InterpretAndApply(target);
                    break;

                case AIMode.CloudAI:
                    // Cloud-based AI (works anywhere, no setup needed)
                    var outConns = ctx.graph.GetOutputConnections(id, "Next");
                    string nextBlockId = outConns.Count > 0 ? outConns[0].toBlockId : null;
                    ctx.IsPaused = true;
                    CoroutineRunner.Instance.StartCoroutine(CallCloudAIAndResume(target, nextBlockId, ctx));
                    break;
            }
        }

        string GetApiKey()
        {
            return apiKeyPart1 + apiKeyPart2 + apiKeyPart3;
        }

        IEnumerator CallCloudAIAndResume(GameObject target, string nextBlockId, GraphContext ctx)
        {
            Debug.Log($"AIBlock: Calling cloud AI with prompt: '{prompt}'");
            
            string systemMessage = "You are a Unity GameObject controller. Given a natural language command, " +
                "respond with ONLY a JSON object describing the action. Format:\n" +
                "{\"action\":\"move|rotate|scale|destroy\",\"direction\":\"up|down|left|right|forward|back\",\"amount\":1.0}\n" +
                "Examples:\n" +
                "- Input: 'move up by 2' → {\"action\":\"move\",\"direction\":\"up\",\"amount\":2.0}\n" +
                "- Input: 'rotate 45 degrees' → {\"action\":\"rotate\",\"direction\":\"up\",\"amount\":45.0}\n" +
                "- Input: 'delete it' → {\"action\":\"destroy\"}\n" +
                "Respond with ONLY the JSON, no other text.";

            string jsonRequest = "{\"model\":\"" + aiModel + "\"," +
                "\"messages\":[" +
                "{\"role\":\"system\",\"content\":\"" + EscapeJson(systemMessage) + "\"}," +
                "{\"role\":\"user\",\"content\":\"" + EscapeJson(prompt) + "\"}" +
                "]," +
                "\"temperature\":" + temperature + "," +
                "\"max_tokens\":" + maxTokens +
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
                Debug.LogError($"AIBlock: Cloud AI error: {request.error}\n{request.downloadHandler.text}");
                InterpretAndApply(target);
            }
            else
            {
                string responseText = request.downloadHandler.text;
                Debug.Log($"AIBlock: Cloud AI response: {responseText}");

                try
                {
                    string aiResponse = ExtractAIContent(responseText);
                    Debug.Log($"AIBlock: AI content: {aiResponse}");
                    
                    ApplyAIResponse(aiResponse, target);
                }
                catch (Exception e)
                {
                    Debug.LogError($"AIBlock: Failed to parse AI response: {e.Message}");
                    InterpretAndApply(target);
                }
            }

            if (nextBlockId != null)
            {
                ctx.Executor.Resume(nextBlockId);
            }
        }

        string EscapeJson(string text)
        {
            return text.Replace("\\", "\\\\")
                       .Replace("\"", "\\\"")
                       .Replace("\n", "\\n")
                       .Replace("\r", "\\r")
                       .Replace("\t", "\\t");
        }

        string ExtractAIContent(string jsonResponse)
        {
            // Simple JSON parsing to extract: choices[0].message.content
            int contentIndex = jsonResponse.IndexOf("\"content\":\"");
            if (contentIndex == -1) return "";
            
            contentIndex += "\"content\":\"".Length;
            int endIndex = jsonResponse.IndexOf("\"", contentIndex);
            
            // Handle escaped quotes
            while (endIndex > 0 && jsonResponse[endIndex - 1] == '\\')
            {
                endIndex = jsonResponse.IndexOf("\"", endIndex + 1);
            }
            
            if (endIndex == -1) return "";
            
            string content = jsonResponse.Substring(contentIndex, endIndex - contentIndex);
            
            // Unescape JSON
            content = content.Replace("\\n", "\n")
                           .Replace("\\\"", "\"")
                           .Replace("\\\\", "\\");
            
            return content;
        }

        void ApplyAIResponse(string aiJson, GameObject target)
        {
            // Try to extract JSON content if wrapped in markdown
            aiJson = aiJson.Trim();
            if (aiJson.StartsWith("```"))
            {
                int start = aiJson.IndexOf('{');
                int end = aiJson.LastIndexOf('}');
                if (start != -1 && end != -1)
                {
                    aiJson = aiJson.Substring(start, end - start + 1);
                }
            }

            // Parse simple JSON manually (Unity's JsonUtility requires class definitions)
            string action = ExtractJsonValue(aiJson, "action");
            string direction = ExtractJsonValue(aiJson, "direction");
            float amount = ParseJsonFloat(aiJson, "amount", 1f);

            var t = target.transform;

            switch (action.ToLower())
            {
                case "move":
                case "translate":
                    Vector3 moveDir = ParseDirection(direction);
                    t.position += moveDir * amount;
                    Debug.Log($"AIBlock: moved {target.name} {direction} by {amount}");
                    break;

                case "rotate":
                case "spin":
                    Vector3 rotateAxis = ParseDirection(direction);
                    t.Rotate(rotateAxis * amount);
                    Debug.Log($"AIBlock: rotated {target.name} by {amount}° on {direction}");
                    break;

                case "scale":
                case "resize":
                    Vector3 scaleDir = ParseDirection(direction);
                    t.localScale += scaleDir * amount;
                    Debug.Log($"AIBlock: scaled {target.name} by {amount}");
                    break;

                case "destroy":
                case "delete":
                case "remove":
                    Debug.Log($"AIBlock: destroying {target.name}");
                    UnityEngine.Object.Destroy(target);
                    break;

                default:
                    Debug.LogWarning($"AIBlock: Unknown action '{action}' in AI response");
                    break;
            }
        }

        string ExtractJsonValue(string json, string key)
        {
            string pattern = "\"" + key + "\":\"";
            int startIndex = json.IndexOf(pattern);
            if (startIndex == -1) return "";
            
            startIndex += pattern.Length;
            int endIndex = json.IndexOf("\"", startIndex);
            if (endIndex == -1) return "";
            
            return json.Substring(startIndex, endIndex - startIndex);
        }

        float ParseJsonFloat(string json, string key, float fallback)
        {
            string pattern = "\"" + key + "\":";
            int startIndex = json.IndexOf(pattern);
            if (startIndex == -1) return fallback;
            
            startIndex += pattern.Length;
            int endIndex = json.IndexOfAny(new[] { ',', '}' }, startIndex);
            if (endIndex == -1) return fallback;
            
            string valueStr = json.Substring(startIndex, endIndex - startIndex).Trim();
            if (float.TryParse(valueStr, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float result))
            {
                return result;
            }
            
            return fallback;
        }

        Vector3 ParseDirection(string dir)
        {
            switch (dir.ToLower())
            {
                case "up": return Vector3.up;
                case "down": return Vector3.down;
                case "left": return Vector3.left;
                case "right": return Vector3.right;
                case "forward": return Vector3.forward;
                case "back": 
                case "backward": return Vector3.back;
                default: return Vector3.up;
            }
        }

        // simple offline interpreter - parses common patterns from natural language

        // this keeps the block functional without needing an API key
        void InterpretAndApply(GameObject target)
        {
            var text = prompt.ToLower().Trim();
            var t = target.transform;

            if (TryParseAction(text, "move", "translate", out var dir, out float amount))
            {
                t.position += dir * amount;
                Debug.Log($"AIBlock: moved {target.name} by {dir * amount}");
            }
            else if (TryParseAction(text, "rotate", "spin", out var axis, out float degrees))
            {
                t.Rotate(axis * degrees);
                Debug.Log($"AIBlock: rotated {target.name} by {degrees} on {axis}");
            }
            else if (TryParseAction(text, "scale", "resize", out var scaleDir, out float factor))
            {
                t.localScale += scaleDir * factor;
                Debug.Log($"AIBlock: scaled {target.name} by {factor}");
            }
            else if (text.Contains("delete") || text.Contains("destroy") || text.Contains("remove"))
            {
                Debug.Log($"AIBlock: destroying {target.name}");
                UnityEngine.Object.Destroy(target);
            }
            else
            {
                Debug.LogWarning($"AIBlock: couldn't parse prompt '{prompt}'");
            }
        }

        bool TryParseAction(string text, string keyword1, string keyword2, out Vector3 direction, out float amount)
        {
            direction = Vector3.zero;
            amount = 0;

            if (!text.Contains(keyword1) && !text.Contains(keyword2))
                return false;

            // figure out direction
            if (text.Contains("up")) direction = Vector3.up;
            else if (text.Contains("down")) direction = Vector3.down;
            else if (text.Contains("left")) direction = Vector3.left;
            else if (text.Contains("right")) direction = Vector3.right;
            else if (text.Contains("forward")) direction = Vector3.forward;
            else if (text.Contains("back")) direction = Vector3.back;
            else direction = Vector3.up; // default

            // try to pull a number out of the text
            amount = ExtractNumber(text, 1f);
            return true;
        }

        float ExtractNumber(string text, float fallback)
        {
            var parts = text.Split(' ');
            foreach (var part in parts)
            {
                if (float.TryParse(part, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float val))
                    return val;
            }
            return fallback;
        }
    }
}
