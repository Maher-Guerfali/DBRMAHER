using System;
using System.Linq;
using UnityEngine;
using BlockSystem.Core;

namespace BlockSystem.Blocks
{
    /// <summary>
    /// Runs the <b>Loop</b> branch N times in sequence, then fires <b>Complete</b>.
    ///
    /// Ports
    /// ──────
    ///  In         (Flow)  — triggers the repeat
    ///  Count      (Float) — optional runtime override of <see cref="count"/>
    ///  Loop       (Flow)  — executed once per iteration
    ///  Iteration  (Float) — 0-based index of the current iteration (data output)
    ///  Complete   (Flow)  — fired once after all iterations finish
    /// </summary>
    [Serializable]
    public class RepeatBlock : Block
    {
        /// <summary>How many times to run the Loop branch (overridable via port).</summary>
        public int count = 3;

        protected override void SetupPorts()
        {
            AddInput("In",    PortType.Flow);
            AddInput("Count", PortType.Float);

            AddOutput("Loop",      PortType.Flow);
            AddOutput("Iteration", PortType.Float);
            AddOutput("Complete",  PortType.Flow);
        }

        public override void Execute(GraphContext ctx)
        {
            int n = Mathf.Max(0, (int)(In<float?>("Count") ?? count));

            var loopConns    = ctx.graph.GetOutputConnections(id, "Loop");
            var completeConn = ctx.graph.GetOutputConnections(id, "Complete").FirstOrDefault();

            for (int i = 0; i < n; i++)
            {
                Out("Iteration", (float)i);

                var ids = loopConns.Select(c => c.toBlockId).ToList();
                if (ids.Count > 0)
                    ctx.Executor.RunBranches(ids);
            }

            // After all iterations, redirect flow to Complete.
            // Using NextBlockId lets the parent ExecuteChain follow the
            // redirect naturally — no need to pause + resume.
            if (completeConn != null)
                ctx.NextBlockId = completeConn.toBlockId;
        }
    }
}
