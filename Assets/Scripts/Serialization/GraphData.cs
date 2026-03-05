using System;
using System.Collections.Generic;
using UnityEngine;

namespace BlockSystem.Serialization
{
    // flat JSON-friendly data structures
    // no polymorphism headaches here - we store block type as string
    // and reconstruct from it

    [Serializable]
    public class GraphData
    {
        public List<BlockData> blocks = new();
        public List<ConnectionData> connections = new();
    }

    [Serializable]
    public class BlockData
    {
        public string id;
        public string type; // class name, used to reconstruct
        public string propertiesJson; // block-specific fields as nested JSON
    }

    [Serializable]
    public class ConnectionData
    {
        public string fromBlockId;
        public string fromPort;
        public string toBlockId;
        public string toPort;
    }
}
