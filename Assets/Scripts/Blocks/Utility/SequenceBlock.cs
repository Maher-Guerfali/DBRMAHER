using System;
using System.Linq;
using UnityEngine;
using BlockSystem.Core;

namespace BlockSystem.Blocks
{
    /// <summary>
    /// Runs multiple named branches <b>one at a time</b>, in top-to-bottom order.
    /// Unlike <see cref="ParallelBlock"/>, Step 1 must finish before Step 2 begins.
    ///
    /// Note: With the default synchronous runner, the entire Step 1 chain executes
    /// to completion (or its first Delay) before Step 2 starts.
    ///
    /// Click the "+" button on the node to add more steps.
    ///
    /// Ports
    /// ──────
    ///  In      (Flow)  — starts the sequence
    ///  Step 1…N (Flow) — executed in order
    /// </summary>
    [Serializable]
    public class SequenceBlock : Block, IDynamicPortBlock
    {
        /// <summary>Number of sequential steps. Persisted for deserialization.</summary>
        public int stepCount = 2;

        // ── IDynamicPortBlock ──────────────────────

        public int BranchCount => stepCount;

        public void AddOutputBranch()
        {
            stepCount++;
            AddOutput($"Step {stepCount}", PortType.Flow);
        }

        public void RemoveLastOutputBranch()
        {
            if (stepCount <= 1) return;

            var last = outputs.LastOrDefault(p => p.type == PortType.Flow);
            if (last != null) outputs.Remove(last);
            stepCount--;
        }

        // ── Block ──────────────────────────────────

        protected override void SetupPorts()
        {
            AddInput("In", PortType.Flow);

            for (int i = 1; i <= stepCount; i++)
                AddOutput($"Step {i}", PortType.Flow);
        }

        // After deserialization sets the real stepCount, clear the default
        // ports and rebuild them so the correct number of steps exist.
        public override void RebuildDynamicPorts()
        {
            inputs.Clear();
            outputs.Clear();
            SetupPorts();
        }

        public override void Execute(GraphContext ctx)
        {
            // Build an ordered list of the first block in each step chain
            var stepIds = outputs
                .Where(p => p.type == PortType.Flow)
                .Select(p => ctx.graph.GetOutputConnections(id, p.name).FirstOrDefault()?.toBlockId)
                .Where(bid => bid != null)
                .ToList();

            if (stepIds.Count == 0) return;

            // RunBranches executes them in order (sequential in the sync runner).
            ctx.Executor.RunBranches(stepIds);

            // Tell the parent chain to stop — flow continues through the steps.
            ctx.IsPaused = true;
        }
    }
}
