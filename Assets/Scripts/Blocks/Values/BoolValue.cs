using System;
using BlockSystem.Core;

namespace BlockSystem.Blocks
{
    /// <summary>
    /// Pure data node — outputs a constant <see cref="bool"/> value.
    /// Has no flow ports; executed automatically before flow blocks run.
    /// Connect its <b>Value</b> output to any bool input (e.g. BranchBlock).
    /// </summary>
    [Serializable]
    public class BoolValue : Block
    {
        public bool value;

        protected override void SetupPorts()
        {
            AddOutput("Value", PortType.Bool);
        }

        public override void Execute(GraphContext ctx)
        {
            Out("Value", value);
        }
    }
}
