using System;
using UnityEngine;

namespace BlockSystem.Core
{
    // ══════════════════════════════════════════════════════════════════════
    //  Port  —  a single connection point on a block node.
    //
    //  There are two kinds of port:
    //
    //    Flow ports  (PortType.Flow)
    //      These are the white arrow-shaped connectors.  They control the
    //      ORDER in which blocks execute.  Connect an output Flow port of
    //      Block A to the input Flow port of Block B and B will run after A.
    //
    //    Data ports  (everything else: Float, Vector3, Bool, String, GameObject)
    //      These carry actual values between blocks.  For example, a
    //      FloatValue block writes a number to its output float port, and
    //      a MoveBlock reads it from its input float port to know the speed.
    //
    //  The `value` field holds the runtime value during execution.  It is
    //  NOT saved to the JSON file — only the wiring (Connection) is saved.
    //  Values are re-computed each time the graph runs.
    // ══════════════════════════════════════════════════════════════════════

    // Whether a port receives data/flow (Input) or sends it (Output).
    public enum PortDirection { Input, Output }

    // The data type carried by a port.
    // Flow is special — it has no data, it just signals "go next".
    public enum PortType { Flow, Float, Vector3, Bool, String, GameObject }

    [Serializable]
    public class Port
    {
        // Unique id for this port, used internally to match serialized data.
        public string id;

        // The label shown on the node in the editor, e.g. "Speed" or "Target".
        public string name;

        public PortDirection direction;
        public PortType type;

        // The live value during a graph run.
        // Marked [NonSerialized] because values are not saved — they're
        // transient and recalculated every time the graph executes.
        [NonSerialized] public object value;

        public Port(string name, PortDirection direction, PortType type)
        {
            this.id        = Guid.NewGuid().ToString("N").Substring(0, 8);
            this.name      = name;
            this.direction = direction;
            this.type      = type;
        }

        // Read the value as a specific type.
        // Returns default(T) if the value is null or the wrong type,
        // so blocks won't throw if a port is unconnected.
        public T GetValue<T>()
        {
            if (value is T typed)
                return typed;
            return default;
        }

        // Write the value.  Called by GraphRunner.ResolveDataInputs() to
        // copy a value from an upstream output port into this input port.
        public void SetValue(object val)
        {
            value = val;
        }
    }
}
