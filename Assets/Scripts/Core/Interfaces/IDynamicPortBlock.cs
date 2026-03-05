namespace BlockSystem.Core
{
    /// <summary>
    /// Implemented by blocks that let users add extra output ports at design time.
    /// The graph editor will render a "+" button on any node that implements this
    /// interface, calling <see cref="AddOutputBranch"/> when clicked.
    /// </summary>
    public interface IDynamicPortBlock
    {
        /// <summary>Current number of dynamic output branches.</summary>
        int BranchCount { get; }

        /// <summary>
        /// Appends one new flow-output port and increments <see cref="BranchCount"/>.
        /// </summary>
        void AddOutputBranch();

        /// <summary>
        /// Removes the last flow-output port (minimum 1 must remain).
        /// </summary>
        void RemoveLastOutputBranch();
    }
}
