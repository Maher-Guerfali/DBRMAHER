using System;
using UnityEngine;
using BlockSystem.Core;

namespace BlockSystem.Blocks
{
    /// <summary>
    /// Writes a message to Unity Console during graph execution.
    /// </summary>
    [Serializable]
    public class DebugLogBlock : Block
    {
        public string message = "Hello from graph";

        protected override void SetupPorts()
        {
            AddInput("Start", PortType.Flow);
            AddInput("Message", PortType.String);
            AddOutput("Next", PortType.Flow);
        }

        public override void Execute(GraphContext ctx)
        {
            var msg = In<string>("Message") ?? message;
            Debug.Log($"[Graph] {msg}");
        }
    }
}
