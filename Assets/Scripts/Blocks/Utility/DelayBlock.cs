using System;
using System.Collections;
using UnityEngine;
using BlockSystem.Core;

namespace BlockSystem.Blocks
{
    /// <summary>
    /// How time is measured for the delay duration.
    /// </summary>
    public enum DelayTimeMode
    {
        /// <summary>Respects Time.timeScale (default, affected by slow-mo / pause).</summary>
        GameTime,
        /// <summary>Ignores Time.timeScale — always real wall-clock seconds.</summary>
        Realtime,
        /// <summary>Waits for a fixed number of rendered frames.</summary>
        Frames
    }

    /// <summary>
    /// Pauses execution for a duration, then resumes at the connected Out block.
    /// </summary>
    [Serializable]
    public class DelayBlock : Block
    {
        /// <summary>Seconds (or frames when mode == Frames) to wait.</summary>
        public float duration = 1f;

        /// <summary>Which time domain to measure the wait in.</summary>
        public DelayTimeMode timeMode = DelayTimeMode.GameTime;

        protected override void SetupPorts()
        {
            AddInput("In",    PortType.Flow);
            AddInput("Delay", PortType.Float);
            AddOutput("Out",  PortType.Flow);
        }

        public override void Execute(GraphContext ctx)
        {
            float wait = In<float?>("Delay") ?? duration;
            var outConns = ctx.graph.GetOutputConnections(id, "Out");
            if (outConns.Count == 0) return;

            ctx.IsPaused = true;
            CoroutineRunner.Instance.StartCoroutine(WaitThenResume(wait, outConns[0].toBlockId, ctx));
        }

        IEnumerator WaitThenResume(float wait, string nextBlockId, GraphContext ctx)
        {
            if (Application.isPlaying)
            {
                switch (timeMode)
                {
                    case DelayTimeMode.GameTime:
                        yield return new WaitForSeconds(wait);
                        break;
                    case DelayTimeMode.Realtime:
                        yield return new WaitForSecondsRealtime(wait);
                        break;
                    case DelayTimeMode.Frames:
                        for (int i = 0; i < (int)wait; i++)
                            yield return null;
                        break;
                }
            }

            ctx.Executor.Resume(nextBlockId);
        }
    }
}
