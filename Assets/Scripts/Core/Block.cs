using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BlockSystem.Core
{
    /// <summary>
    /// Base class for all nodes in the block graph.
    /// </summary>
    [Serializable]
    public abstract class Block : IBlock
    {
        public string id;
        public string blockType;
        public Vector2 editorPosition;
        [SerializeReference] public List<Port> inputs = new();
        [SerializeReference] public List<Port> outputs = new();

        public string Id => id;
        public string BlockType => blockType;
        public Vector2 EditorPosition { get => editorPosition; set => editorPosition = value; }
        public IReadOnlyList<Port> Inputs => inputs;
        public IReadOnlyList<Port> Outputs => outputs;

        protected Block()
        {
            id = Guid.NewGuid().ToString("N").Substring(0, 8);
            blockType = GetType().Name;
            SetupPorts();
        }

        protected abstract void SetupPorts();

        public virtual void RebuildDynamicPorts() { }

        public void Execute(IGraphContext context)
        {
            Execute((GraphContext)context);
        }

        public abstract void Execute(GraphContext ctx);

        public Port GetInput(string name)  => inputs.FirstOrDefault(p => p.name == name);
        public Port GetOutput(string name) => outputs.FirstOrDefault(p => p.name == name);

        protected Port AddInput(string name, PortType type)
        {
            var port = new Port(name, PortDirection.Input, type);
            inputs.Add(port);
            return port;
        }

        protected Port AddOutput(string name, PortType type)
        {
            var port = new Port(name, PortDirection.Output, type);
            outputs.Add(port);
            return port;
        }

        protected T In<T>(string portName)
        {
            var port = GetInput(portName);
            return port != null ? port.GetValue<T>() : default;
        }

        protected void Out(string portName, object val)
        {
            var port = GetOutput(portName);
            port?.SetValue(val);
        }
    }
}
