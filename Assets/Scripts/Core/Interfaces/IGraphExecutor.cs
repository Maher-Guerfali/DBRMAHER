using System.Collections.Generic;

namespace BlockSystem.Core
{
    /// <summary>
    /// Interface for graph execution engines.
    /// Allows swapping execution strategies (sequential, parallel, async, etc.)
    /// </summary>
    public interface IGraphExecutor
    {
        IGraphContext Context { get; }

        /// <summary>Begin executing the graph from its entry blocks.</summary>
        void Run();

        /// <summary>Destroy any runtime objects created during execution.</summary>
        void Cleanup();

        /// <summary>
        /// Resume a paused chain, continuing from <paramref name="blockId"/>.
        /// Called by DelayBlock after its timer fires.
        /// </summary>
        void Resume(string blockId);

        /// <summary>
        /// Spawn independent execution chains for each given block id.
        /// Used by ParallelBlock, SequenceBlock, ForEachTagBlock, and RepeatBlock.
        /// </summary>
        void RunBranches(IEnumerable<string> blockIds);
    }
}
