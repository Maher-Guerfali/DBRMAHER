using System;
using BlockSystem.Core;

namespace BlockSystem.Blocks
{
    /// <summary>
    /// Pure data node — outputs a constant <see cref="float"/> value.
    /// Has no flow ports; executed automatically before flow blocks run.
    /// Connect its <b>Value</b> output to any float input on another block.
    /// </summary>
    [Serializable]
    public class FloatValue : Block
    {
        public float value;

        protected override void SetupPorts()
        {
            AddOutput("Value", PortType.Float);
        }

        public override void Execute(GraphContext ctx)
        {
            Out("Value", value);
        }
    }
}
