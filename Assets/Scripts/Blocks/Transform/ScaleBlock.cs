using System;
using UnityEngine;
using DG.Tweening;
using BlockSystem.Core;

namespace BlockSystem.Blocks
{
    /// <summary>
    /// Sets local scale instantly or over a duration.
    /// </summary>
    [Serializable]
    public class ScaleBlock : Block
    {
        public Vector3 scale = new Vector3(0, 10, 0);
        public float duration = 0f;
        public string targetName;

        protected override void SetupPorts()
        {
            AddInput("Start", PortType.Flow);
            AddInput("Target", PortType.GameObject);
            AddInput("Scale", PortType.Vector3);
            AddInput("Duration", PortType.Float);
            AddOutput("Next", PortType.Flow);
        }

        public override void Execute(GraphContext ctx)
        {
            var target = In<GameObject>("Target");
            if (target == null && !string.IsNullOrEmpty(targetName))
                target = ctx.GetObject(targetName);
            if (target == null)
            {
                Debug.LogWarning($"ScaleBlock {id}: no target found. Set targetName or connect Target port.");
                return;
            }

            var s = In<Vector3?>("Scale") ?? scale;
            float dur = In<float?>("Duration") ?? duration;
            if (dur <= 0f)
            {
                target.transform.localScale = s;
                return;
            }

            var outConns = ctx.graph.GetOutputConnections(id, "Next");
            string nextId = outConns.Count > 0 ? outConns[0].toBlockId : null;

            // Pause flow until tween completion.
            ctx.IsPaused = true;
            target.transform.DOScale(s, dur)
                .SetEase(Ease.Linear)
                .OnComplete(() =>
                {
                    ctx.IsPaused = false;
                    if (nextId != null)
                        ctx.Executor.Resume(nextId);
                });
        }
    }
}
