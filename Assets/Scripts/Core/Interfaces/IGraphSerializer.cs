namespace BlockSystem.Core
{
    /// <summary>
    /// Interface for graph serialization/deserialization
    /// Allows supporting multiple formats (JSON, XML, Binary, etc.)
    /// </summary>
    public interface IGraphSerializer
    {
        string Serialize(BlockGraph graph);
        void Deserialize(BlockGraph graph, string data);
        
        void SaveToFile(BlockGraph graph, string path);
        void LoadFromFile(BlockGraph graph, string path);
    }
}
