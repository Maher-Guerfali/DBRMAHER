using System;
using System.Linq;
using UnityEngine;
using BlockSystem.Core;

namespace BlockSystem.Blocks
{
    /// <summary>
    /// Finds every active GameObject in the scene with a given <see cref="tag"/>
    /// and runs the <b>Body</b> chain once per object, injecting the current object
    /// into the context so downstream blocks (e.g. MoveBlock) can target it automatically.
    ///
    /// Ports
    /// ──────
    ///  In       (Flow)       — starts the iteration
    ///  Body     (Flow)       — executed once per matching object
    ///  Current  (GameObject) — the current object in this iteration (data output)
    ///  Complete (Flow)       — fired after all objects have been processed
    /// </summary>
    [Serializable]
    public class ForEachTagBlock : Block
    {
        /// <summary>Tag to search for. Must match exactly (case-sensitive).</summary>
        public string tag = "Untagged";

        /// <summary>
        /// Context key where the current object is registered per iteration.
        /// Downstream blocks can read it with ctx.GetObject(contextKey).
        /// Defaults to "ForEach.Current" — change only if you nest multiple loops.
        /// </summary>
        public string contextKey = "ForEach.Current";

        protected override void SetupPorts()
        {
            AddInput("In", PortType.Flow);

            AddOutput("Body",    PortType.Flow);
            AddOutput("Current", PortType.GameObject);
            AddOutput("Complete", PortType.Flow);
        }

        public override void Execute(GraphContext ctx)
        {
            GameObject[] objects;

            try
            {
                objects = GameObject.FindGameObjectsWithTag(tag);
            }
            catch (UnityException)
            {
                Debug.LogError($"[ForEachTagBlock] Tag '{tag}' is not defined. Add it in the Tag Manager.");
                return;
            }

            if (objects.Length == 0)
                Debug.LogWarning($"[ForEachTagBlock] No active objects found with tag '{tag}'.");

            var bodyConns = ctx.graph.GetOutputConnections(id, "Body");

            foreach (var go in objects)
            {
                // Publish the current object on the data output port so
                // any block reading ctx.GetObject(contextKey) receives it
                Out("Current", go);
                ctx.RegisterObject(contextKey, go);

                var ids = bodyConns.Select(c => c.toBlockId).ToList();
                if (ids.Count > 0)
                    ctx.Executor.RunBranches(ids);
            }

            // After all iterations, redirect flow to Complete.
            // Using NextBlockId lets the parent chain follow the redirect
            // naturally without pause/resume state corruption.
            var completeConn = ctx.graph.GetOutputConnections(id, "Complete").FirstOrDefault();
            if (completeConn != null)
                ctx.NextBlockId = completeConn.toBlockId;
        }
    }
}
