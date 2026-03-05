using System;
using System.Linq;
using UnityEngine;
using BlockSystem.Core;

namespace BlockSystem.Blocks
{
    /// <summary>
    /// Fans the execution flow out to <b>all</b> connected output branches
    /// simultaneously (in the same frame when using the default synchronous runner).
    ///
    /// Click the "+" button on the node in the graph editor to add more branches.
    ///
    /// Ports
    /// ──────
    ///  In          (Flow)  — starts the fan-out
    ///  Branch 1…N  (Flow)  — independent chains fired at the same time
    /// </summary>
    [Serializable]
    public class ParallelBlock : Block, IDynamicPortBlock
    {
        /// <summary>
        /// Number of output branches. Persisted so deserialization restores the correct ports.
        /// </summary>
        public int branchCount = 2;

        // ── IDynamicPortBlock ──────────────────────

        public int BranchCount => branchCount;

        public void AddOutputBranch()
        {
            branchCount++;
            AddOutput($"Branch {branchCount}", PortType.Flow);
        }

        public void RemoveLastOutputBranch()
        {
            if (branchCount <= 1) return;

            var last = outputs.LastOrDefault(p => p.type == PortType.Flow);
            if (last != null) outputs.Remove(last);
            branchCount--;
        }

        // ── Block ──────────────────────────────────

        protected override void SetupPorts()
        {
            AddInput("In", PortType.Flow);

            for (int i = 1; i <= branchCount; i++)
                AddOutput($"Branch {i}", PortType.Flow);
        }

        // Called after deserialization restores `branchCount`.
        // Clears the ports built by the constructor (which used the default
        // branchCount) and rebuilds them with the real value from JSON.
        public override void RebuildDynamicPorts()
        {
            inputs.Clear();
            outputs.Clear();
            SetupPorts();
        }

        public override void Execute(GraphContext ctx)
        {
            // Collect the first connected block from each branch output
            var nextIds = outputs
                .Where(p => p.type == PortType.Flow)
                .SelectMany(p => ctx.graph.GetOutputConnections(id, p.name))
                .Select(c => c.toBlockId)
                .ToList();

            if (nextIds.Count == 0) return;

            // Fire all branches.  RunBranches resets IsPaused internally
            // before each sub-chain, so they don't corrupt each other.
            ctx.Executor.RunBranches(nextIds);

            // Tell the parent chain to stop — all flow now lives in the branches.
            ctx.IsPaused = true;
        }
    }
}
