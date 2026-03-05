using System;
using UnityEngine;
using BlockSystem.Core;
using DG.Tweening;

namespace BlockSystem.Blocks
{
    /// <summary>
    /// Rotates a target object by euler angles, with optional tweened animation.
    /// </summary>
    [Serializable]
    public class RotateBlock : Block
    {
        public Vector3 eulerAngles = new Vector3(0, 10, 0);
        public float speed = 20f;
        public string targetName;

        protected override void SetupPorts()
        {
            AddInput("Start",  PortType.Flow);
            AddInput("Target", PortType.GameObject);
            AddInput("Angles", PortType.Vector3);
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
                Debug.LogWarning($"RotateBlock {id}: no target found. Set targetName or connect Target port.");
                return;
            }

            var angles = In<Vector3?>("Angles") ?? eulerAngles;
            var spd = In<float?>("Speed") ?? speed;
            if (spd <= 0f || angles.magnitude < 0.001f)
            {
                target.transform.Rotate(angles);
                return;
            }

            var outConns = ctx.graph.GetOutputConnections(id, "Next");
            string nextId = outConns.Count > 0 ? outConns[0].toBlockId : null;
            float duration = angles.magnitude / spd;

            // Pause flow until tween completion.
            ctx.IsPaused = true;
            target.transform.DORotate(target.transform.eulerAngles + angles, duration, RotateMode.FastBeyond360)
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
