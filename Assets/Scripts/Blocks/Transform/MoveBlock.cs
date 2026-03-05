using System;
using UnityEngine;
using BlockSystem.Core;
using DG.Tweening;

namespace BlockSystem.Blocks
{
    /// <summary>
    /// Moves a target object by offset, with optional tweened animation.
    /// </summary>
    [Serializable]
    public class MoveBlock : Block
    {
        public Vector3 offset = new Vector3(0, 10, 0);
        public float speed = 20f;
        public string targetName;

        protected override void SetupPorts()
        {
            AddInput("Start",  PortType.Flow);
            AddInput("Target", PortType.GameObject);
            AddInput("Offset", PortType.Vector3);
            AddInput("Speed",  PortType.Float);
            AddOutput("Next",  PortType.Flow);
        }

        public override void Execute(GraphContext ctx)
        {
            var target = In<GameObject>("Target");
            if (target == null && !string.IsNullOrEmpty(targetName))
                target = ctx.GetObject(targetName);
            if (target == null)
            {
                Debug.LogWarning($"MoveBlock {id}: no target found. Set targetName or connect Target port.");
                return;
            }

            var move = In<Vector3?>("Offset") ?? offset;
            var spd = In<float?>("Speed") ?? speed;
            if (spd <= 0f || move.magnitude < 0.0001f)
            {
                target.transform.position += move;
                return;
            }

            var outConns = ctx.graph.GetOutputConnections(id, "Next");
            string nextId = outConns.Count > 0 ? outConns[0].toBlockId : null;
            float duration = move.magnitude / spd;

            // Pause flow until tween completion.
            ctx.IsPaused = true;
            target.transform.DOMove(target.transform.position + move, duration)
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
