using System;

namespace BlockSystem.Core
{
    /// <summary>
    /// A wire between two ports in the graph.  Stores four strings:
    /// which block/port the wire leaves from and which block/port it arrives at.
    /// Connections are serialised as part of the BlockGraph asset and inside JSON exports.
    /// </summary>
    [Serializable]
    public class Connection
    {
        public string fromBlockId;
        public string fromPortName;
        public string toBlockId;
        public string toPortName;

        public Connection() { }

        public Connection(string fromBlockId, string fromPortName, string toBlockId, string toPortName)
        {
            this.fromBlockId = fromBlockId;
            this.fromPortName = fromPortName;
            this.toBlockId = toBlockId;
            this.toPortName = toPortName;
        }
    }
}
