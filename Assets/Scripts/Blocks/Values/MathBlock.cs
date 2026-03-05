using System;
using UnityEngine;
using BlockSystem.Core;

namespace BlockSystem.Blocks
{
    /// <summary>Arithmetic operations available in <see cref="MathBlock"/>.</summary>
    public enum MathOp { Add, Subtract, Multiply, Divide }

    /// <summary>
    /// Pure data node — performs basic arithmetic on two float inputs.
    /// Division by zero returns 0 instead of crashing.
    ///
    /// Ports
    /// ──────
    ///  A      (Float) — left-hand operand
    ///  B      (Float) — right-hand operand
    ///  Result (Float) — output of the chosen <see cref="operation"/>
    /// </summary>
    [Serializable]
    public class MathBlock : Block
    {
        public MathOp operation = MathOp.Add;
        public float valueA;
        public float valueB;

        protected override void SetupPorts()
        {
            AddInput("A", PortType.Float);
            AddInput("B", PortType.Float);
            AddOutput("Result", PortType.Float);
        }

        public override void Execute(GraphContext ctx)
        {
            float a = In<float?>("A") ?? valueA;
            float b = In<float?>("B") ?? valueB;

            float result = operation switch
            {
                MathOp.Add => a + b,
                MathOp.Subtract => a - b,
                MathOp.Multiply => a * b,
                MathOp.Divide => Mathf.Approximately(b, 0) ? 0 : a / b,
                _ => 0
            };

            Out("Result", result);
        }
    }
}
