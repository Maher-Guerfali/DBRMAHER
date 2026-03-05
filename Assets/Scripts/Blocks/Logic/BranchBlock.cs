using System;
using UnityEngine;
using BlockSystem.Core;

namespace BlockSystem.Blocks
{
    /// <summary>
    /// Branches execution to True or False based on a condition.
    /// </summary>
    [Serializable]
    public class BranchBlock : Block
    {
        public bool conditionFallback;

        protected override void SetupPorts()
        {
            AddInput("Start", PortType.Flow);
            AddInput("Condition", PortType.Bool);
            AddOutput("True", PortType.Flow);
            AddOutput("False", PortType.Flow);
        }

        public override void Execute(GraphContext ctx)
        {
            bool condition = In<bool?>("Condition") ?? conditionFallback;
            string outPort = condition ? "True" : "False";

            var connections = ctx.graph.GetOutputConnections(id, outPort);
            if (connections.Count > 0)
                ctx.nextBlockId = connections[0].toBlockId;
        }
    }
}
