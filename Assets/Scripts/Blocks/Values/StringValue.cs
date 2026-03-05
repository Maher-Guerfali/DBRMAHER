using System;
using BlockSystem.Core;

namespace BlockSystem.Blocks
{
    /// <summary>
    /// Pure data node — outputs a constant <see cref="string"/> value.
    /// Has no flow ports; executed automatically before flow blocks run.
    /// Connect its <b>Value</b> output to any string input on another block.
    /// </summary>
    [Serializable]
    public class StringValue : Block
    {
        public string value = "";

        protected override void SetupPorts()
        {
            AddOutput("Value", PortType.String);
        }

        public override void Execute(GraphContext ctx)
        {
            Out("Value", value);
        }
    }
}
