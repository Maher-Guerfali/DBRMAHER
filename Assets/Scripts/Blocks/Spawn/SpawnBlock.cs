using System;
using UnityEngine;
using BlockSystem.Core;

namespace BlockSystem.Blocks
{
    /// <summary>Supported primitive shapes that SpawnBlock can create.</summary>
    public enum PrimitiveShape { Cube, Sphere, Cylinder }

    /// <summary>
    /// Creates a primitive GameObject (cube, sphere, or cylinder) at a given
    /// position and registers it in the context so downstream blocks (Move,
    /// Rotate, Scale) can find it by name.
    ///
    /// Ports
    /// ──────
    ///  Start    (Flow)       — triggers the spawn
    ///  Position (Vector3)    — optional override for <see cref="position"/>
    ///  Next     (Flow)       — fires after the object is created
    ///  Object   (GameObject) — reference to the newly spawned object
    /// </summary>
    [Serializable]
    public class SpawnBlock : Block
    {
        public PrimitiveShape shape = PrimitiveShape.Cube;
        public Vector3 position = Vector3.zero;

        /// <summary>
        /// Optional: drag a GameObject from the hierarchy to use its position
        /// as the spawn point. If null, falls back to <see cref="position"/> (0,0,0).
        /// </summary>
        public GameObject positionReference;

        public string objectName = "SpawnedObject";

        protected override void SetupPorts()
        {
            AddInput("Start", PortType.Flow);
            AddInput("Position", PortType.Vector3);
            AddOutput("Next", PortType.Flow);
            AddOutput("Object", PortType.GameObject);
        }

        public override void Execute(GraphContext ctx)
        {
            // Priority: connected port > reference GameObject's position > inspector field (default 0,0,0)
            var pos = In<Vector3?>("Position")
                      ?? (positionReference != null ? positionReference.transform.position : position);

            PrimitiveType prim = shape switch
            {
                PrimitiveShape.Cube => PrimitiveType.Cube,
                PrimitiveShape.Sphere => PrimitiveType.Sphere,
                PrimitiveShape.Cylinder => PrimitiveType.Cylinder,
                _ => PrimitiveType.Cube
            };

            var go = GameObject.CreatePrimitive(prim);
            go.name = objectName;
            go.transform.position = pos;

            // register by both block id and object name so other blocks can find it
            ctx.RegisterObject(id, go);
            ctx.RegisterObject(objectName, go);
            Out("Object", go);
        }
    }
}
