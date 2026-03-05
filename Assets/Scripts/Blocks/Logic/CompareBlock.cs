using System;
using UnityEngine;
using BlockSystem.Core;

namespace BlockSystem.Blocks
{
    /// <summary>Comparison operators available in <see cref="CompareBlock"/>.</summary>
    public enum CompareOp { Equal, NotEqual, Greater, Less, GreaterOrEqual, LessOrEqual }

    /// <summary>
    /// Compares two float inputs and triggers Next only when true.
    /// </summary>
    [Serializable]
    public class CompareBlock : Block
    {
        public CompareOp operation = CompareOp.Equal;
        public float valueA;
        public float valueB;

        protected override void SetupPorts()
        {
            AddInput("Start", PortType.Flow);
            AddInput("A", PortType.Float);
            AddInput("B", PortType.Float);
            AddOutput("Next", PortType.Flow);
        }

        public override void RebuildDynamicPorts()
        {
            inputs.Clear();
            outputs.Clear();
            SetupPorts();
        }

        public override void Execute(GraphContext ctx)
        {
            float a = In<float?>("A") ?? valueA;
            float b = In<float?>("B") ?? valueB;

            bool result = operation switch
            {
                CompareOp.Equal => Mathf.Approximately(a, b),
                CompareOp.NotEqual => !Mathf.Approximately(a, b),
                CompareOp.Greater => a > b,
                CompareOp.Less => a < b,
                CompareOp.GreaterOrEqual => a >= b,
                CompareOp.LessOrEqual => a <= b,
                _ => false
            };

            if (result)
            {
                var connections = ctx.graph.GetOutputConnections(id, "Next");
                if (connections.Count > 0)
                    ctx.nextBlockId = connections[0].toBlockId;
            }
            else
            {
                // Prevent GraphRunner default-flow fallback when condition is false.
                ctx.nextBlockId = string.Empty;
            }
        }
    }
}
