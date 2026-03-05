using UnityEngine;

namespace BlockSystem.Core
{
    /// <summary>
    /// Execution context shared between blocks during graph execution
    /// </summary>
    public interface IGraphContext
    {
        BlockGraph Graph { get; }
        string NextBlockId { get; set; }
        bool IsPaused { get; set; }
        IGraphExecutor Executor { get; }
        
        void RegisterObject(string key, GameObject obj);
        GameObject GetObject(string key);
        void Clear();
    }
}
