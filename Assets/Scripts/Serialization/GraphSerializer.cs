using BlockSystem.Core;

namespace BlockSystem.Serialization
{
    public static class GraphSerializer
    {
        static IGraphSerializer defaultSerializer = new JsonGraphSerializer(prettyPrint: true);

        public static void SetDefaultSerializer(IGraphSerializer serializer)
        {
            defaultSerializer = serializer;
        }

        public static string ToJson(BlockGraph graph, bool prettyPrint = true)
        {
            var serializer = new JsonGraphSerializer(prettyPrint);
            return serializer.Serialize(graph);
        }

        public static void FromJson(BlockGraph graph, string json)
        {
            defaultSerializer.Deserialize(graph, json);
        }

        public static void SaveToFile(BlockGraph graph, string path)
        {
            defaultSerializer.SaveToFile(graph, path);
        }

        public static void LoadFromFile(BlockGraph graph, string path)
        {
            defaultSerializer.LoadFromFile(graph, path);
        }
    }
}
