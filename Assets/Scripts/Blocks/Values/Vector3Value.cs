using System;
using UnityEngine;
using BlockSystem.Core;

namespace BlockSystem.Blocks
{
    /// <summary>
    /// Pure data node — outputs a constant <see cref="Vector3"/> value.
    /// Has no flow ports; executed automatically before flow blocks run.
    /// Connect its <b>Value</b> output to any Vector3 input on another block.
    /// </summary>
    [Serializable]
    public class Vector3Value : Block
    {
        public Vector3 value;

        protected override void SetupPorts()
        {
            AddOutput("Value", PortType.Vector3);
        }

        public override void Execute(GraphContext ctx)
        {
            Out("Value", (Vector3?)value);
        }
    }
}
