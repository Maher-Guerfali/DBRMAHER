using UnityEngine;
using BlockSystem.Core;
using BlockSystem.Serialization;

/// <summary>
/// Runtime entry point for loading and executing a block graph.
/// </summary>
public class GameManager : MonoBehaviour
{
    public BlockGraph graph;
    public string importPath = "Assets/graph_export.json";

    private IGraphExecutor executor;
    private IGraphSerializer serializer = new JsonGraphSerializer();

    void Start()
    {
        if (graph != null)
            RunGraph();
    }

    public void RunGraph()
    {
        if (graph == null) return;

        executor?.Cleanup();
        executor = new GraphRunner(graph);
        executor.Run();
    }

    public void ImportAndRun()
    {
        if (graph == null) return;
        serializer.LoadFromFile(graph, importPath);
        RunGraph();
    }

    void OnDestroy()
    {
        executor?.Cleanup();
    }
}
