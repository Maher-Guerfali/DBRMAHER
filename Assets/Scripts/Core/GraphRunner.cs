using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BlockSystem.Core
{
    // ══════════════════════════════════════════════════════════════════════
    //  GraphRunner  —  the engine that actually runs your graph.
    //
    //  How execution works, step by step:
    //
    //    1. Run() is called (usually by GameManager on Start).
    //    2. "Value" blocks (pure data nodes with no flow ports) execute first
    //       so their output ports are already populated when flow blocks need
    //       to read them.
    //    3. Runner finds "entry" blocks — flow blocks that have no incoming
    //       flow wire.  These are the starting nodes (like SpawnBlock).
    //    4. For each entry it calls ExecuteChain(), which walks forward
    //       through flow connections one block at a time.
    //    5. Before each block runs, ResolveDataInputs() copies values from
    //       connected output ports into the block's input ports.
    //    6. After Execute() returns, the runner checks:
    //         • ctx.IsPaused  — a block (e.g. Delay) wants to hand control
    //                           back.  Stop the loop; Resume() will restart it
    //                           later from a coroutine.
    //         • ctx.nextBlockId — a block (e.g. Branch) explicitly chose the
    //                           next destination.  Jump there instead of
    //                           following the default flow wire.
    //         • default         — follow the first outgoing flow wire.
    //
    //  Pause / Resume (how DelayBlock works):
    //    Delay sets ctx.IsPaused = true then starts a Unity coroutine.
    //    ExecuteChain sees IsPaused and returns early.
    //    When the timer fires, the coroutine calls ctx.Executor.Resume(blockId),
    //    which resets IsPaused and calls ExecuteChain again from that block.
    //
    //  Branching fan-out (how Parallel / ForEach work):
    //    Those blocks collect a list of "next block" ids and call
    //    ctx.Executor.RunBranches(ids).  That loops the list and starts an
    //    independent ExecuteChain for each id.
    // ══════════════════════════════════════════════════════════════════════
    public class GraphRunner : IGraphExecutor
    {
        readonly BlockGraph graph;
        readonly GraphContext ctx;

        // How many blocks a single chain may visit before we assume there's
        // an infinite loop and bail out with a warning.
        // Raise this if you have intentionally very long chains.
        public int MaxStepsPerChain = 1000;

        public GraphRunner(BlockGraph graph)
        {
            this.graph = graph;
            ctx = new GraphContext { graph = graph, executor = this };
        }

        public IGraphContext Context => ctx;

        // ─────────────────────────────────────────
        //  IGraphExecutor
        // ─────────────────────────────────────────

        public void Run()
        {

            // Value blocks = no flow ports (just data ports like FloatValue, Vector3Value)
            var valueBlocks = graph.blocks
                .Where(b => !b.inputs.Any(p => p.type == PortType.Flow)   // No flow inputs
                         && !b.outputs.Any(p => p.type == PortType.Flow)) // No flow outputs
                .ToList();

            foreach (var vb in valueBlocks)
                vb.Execute(ctx);  // Populate their output ports with data


            // These are the "start" nodes like SpawnBlock
            var entries = graph.GetEntryBlocks();

            if (entries.Count == 0 && graph.blocks.Count > 0)
            {
                // Fallback: blocks that produce flow but receive none
                entries = graph.blocks
                    .Where(b => b.outputs.Any(p => p.type == PortType.Flow)
                             && !b.inputs.Any(p => p.type == PortType.Flow))
                    .ToList();
            }

            foreach (var entry in entries)
                ExecuteChain(entry);
        }

        /// <inheritdoc/>
        public void Resume(string blockId)
        {
            var block = graph.GetBlock(blockId);
            if (block == null)
            {
                Debug.LogWarning($"[GraphRunner] Resume: block '{blockId}' not found.");
                return;
            }

            // No need to reset IsPaused here — ExecuteChain resets it
            // before each block.Execute() call.
            ExecuteChain(block);
        }

        /// <inheritdoc/>
        public void RunBranches(IEnumerable<string> blockIds)
        {
            // Each branch runs as an independent chain.  ExecuteChain resets
            // IsPaused before every block, so branches don't corrupt each
            // other's pause state.
            foreach (var id in blockIds)
            {
                var block = graph.GetBlock(id);
                if (block == null)
                {
                    Debug.LogWarning($"[GraphRunner] RunBranches: block '{id}' not found.");
                    continue;
                }

                ExecuteChain(block);
            }
        }

        /// <inheritdoc/>
        public void Cleanup() => ctx.Clear();

        // ─────────────────────────────────────────
        //  Internals
        // ─────────────────────────────────────────

        void ExecuteChain(Block start)
        {
            var current = start;  // Start at the given block
            int steps = 0;        // Safety counter to prevent infinite loops

            while (current != null && steps < MaxStepsPerChain)
            {
                steps++;


                ResolveDataInputs(current);


                ctx.nextBlockId = null;   // Clear any previous explicit redirect
                ctx.IsPaused = false;     // Assume block won't pause (block sets to true if it does)
                
                current.Execute(ctx);  // Run the block's logic


                // If paused, stop the loop. Block's coroutine will call Resume() later.
                if (ctx.IsPaused)
                    return;


                current = ResolveNextBlock(current);  // Follows ctx.nextBlockId or flow edge
            }

            if (steps >= MaxStepsPerChain)
                Debug.LogWarning($"[GraphRunner] Step limit ({MaxStepsPerChain}) hit — possible infinite loop.");
        }

        /// <summary>
        /// Determines the next block to execute after <paramref name="current"/>.
        /// Respects explicit redirection (ctx.nextBlockId) and normal flow edges.
        /// </summary>
        Block ResolveNextBlock(Block current)
        {

            if (ctx.nextBlockId != null)
                return graph.GetBlock(ctx.nextBlockId);  // Jump to that block


            var flowOut = current.outputs.FirstOrDefault(p => p.type == PortType.Flow);
            if (flowOut == null) return null;  // No flow output = end of chain


            var conn = graph.GetOutputConnections(current.id, flowOut.name).FirstOrDefault();
            return conn != null ? graph.GetBlock(conn.toBlockId) : null;  // Return connected block or null
        }

        /// <summary>
        /// Pulls values from upstream output ports into <paramref name="block"/>'s data inputs.
        /// </summary>
        void ResolveDataInputs(Block block)
        {

            foreach (var input in block.inputs)
            {
                if (input.type == PortType.Flow) continue;  // Skip flow ports (they don't carry data)


                var conn = graph.GetInputConnection(block.id, input.name);
                if (conn == null) continue;  // Nothing connected


                var sourceBlock = graph.GetBlock(conn.fromBlockId);
                var sourcePort = sourceBlock?.GetOutput(conn.fromPortName);
                if (sourcePort != null)
                    input.SetValue(sourcePort.value);  // Copy value into this block's input
            }
        }
    }
}
