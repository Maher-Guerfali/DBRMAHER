using System.Collections.Generic;
using UnityEngine;

namespace BlockSystem.Core
{
    /// <summary>
    /// Interface for executable blocks in the graph
    /// </summary>
    public interface IBlock
    {
        string Id { get; }
        string BlockType { get; }
        Vector2 EditorPosition { get; set; }
        
        IReadOnlyList<Port> Inputs { get; }
        IReadOnlyList<Port> Outputs { get; }
        
        void Execute(IGraphContext context);
        Port GetInput(string name);
        Port GetOutput(string name);
    }
}
