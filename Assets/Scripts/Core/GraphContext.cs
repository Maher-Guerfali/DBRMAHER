using System.Collections.Generic;
using UnityEngine;

namespace BlockSystem.Core
{
    // ══════════════════════════════════════════════════════════════════════
    //  GraphContext  —  the shared "blackboard" every block can read/write
    //                   while the graph is running.
    //
    //  One context is created per GraphRunner.Run() call and lives until
    //  Cleanup() is called (usually when the scene ends or the graph
    //  is re-run).  Think of it like a whiteboard in a room — every block
    //  in the same run looks at the same board.
    //
    //  What blocks typically use it for:
    //    • ctx.GetObject("Enemy")   — find a GameObject that was spawned
    //                                  earlier in the same run.
    //    • ctx.RegisterObject(...)  — store a spawned object so later
    //                                  blocks (Move, Rotate, etc.) can find it.
    //    • ctx.nextBlockId          — set this inside Execute() to tell the
    //                                  runner which block to go to next
    //                                  (used by BranchBlock for if/else).
    //    • ctx.IsPaused             — set this to true inside Execute() to
    //                                  tell the runner to stop the current
    //                                  chain (used by Delay, Parallel, etc.).
    //    • ctx.Executor.Resume(...) — call this from a coroutine to restart
    //                                  the chain after a pause (used by Delay).
    // ══════════════════════════════════════════════════════════════════════
    public class GraphContext : IGraphContext
    {
        // GameObjects registered during this run.  Keyed by a readable name
        // like "Player" or "ForEach.Current" so blocks can find each other's
        // results without needing direct references.
        public Dictionary<string, GameObject> spawnedObjects = new();

        // The graph that is currently executing — blocks use this to
        // look up connections and neighbouring blocks.
        public BlockGraph graph;

        // Blocks that want to redirect flow (like BranchBlock) write the
        // destination block's id here before returning from Execute().
        // The runner checks this after every block and jumps if it is set.
        public string nextBlockId;

        // A reference back to the runner, so blocks inside coroutines
        // (DelayBlock) can call Resume() after their timer fires.
        public IGraphExecutor executor;

        // ── IGraphContext interface ───────────────────────────────────────

        public BlockGraph Graph    => graph;
        public string NextBlockId  { get => nextBlockId; set => nextBlockId = value; }
        public IGraphExecutor Executor => executor;

        // When true the runner will stop the current chain immediately
        // after Execute() returns.  Reset to false before resuming.
        public bool IsPaused { get; set; }

        // ── Object registry ───────────────────────────────────────────────


        // Example: SpawnBlock calls RegisterObject("Enemy", spawnedCube)
        //          MoveBlock later calls GetObject("Enemy") to find that cube
        public void RegisterObject(string key, GameObject go)
        {
            spawnedObjects[key] = go;  // Dictionary automatically replaces if key exists
        }


        // Falls back to GameObject.Find(key) if nothing was registered
        // This lets you reference scene objects that weren't spawned by the graph
        public GameObject GetObject(string key)
        {

            if (spawnedObjects.TryGetValue(key, out var go))
                return go;  // Found in registry


            return GameObject.Find(key);  // Slower but works for scene objects
        }


        // Called automatically by GraphRunner.Cleanup() when graph finishes
        public void Clear()
        {
            foreach (var kv in spawnedObjects)
            {
                if (kv.Value != null)  // Check if not already destroyed
                    Object.Destroy(kv.Value);  // Remove from scene
            }
            spawnedObjects.Clear();  // Empty the dictionary
        }
    }
}
