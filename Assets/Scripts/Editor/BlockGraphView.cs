using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using BlockSystem.Core;
using BlockSystem.Blocks;

namespace BlockSystem.Editor
{
    /// <summary>
    /// GraphView canvas - handles node display, edge creation, deletion,
    /// and syncs everything back to the BlockGraph data model.
    /// </summary>
    public class BlockGraphView : GraphView
    {

        BlockGraphWindow window;         // Parent window that contains this view
        BlockGraph graph;                // Current graph data (blocks + connections)
        BlockSearchWindow searchWindow;  // Right-click menu for adding blocks

        public BlockGraphView(BlockGraphWindow window)
        {
            this.window = window;


            this.AddManipulator(new ContentZoomer());       // Mouse wheel zoom
            this.AddManipulator(new ContentDragger());      // Middle-click pan
            this.AddManipulator(new SelectionDragger());    // Drag selected nodes
            this.AddManipulator(new RectangleSelector());   // Click-drag box select


            var grid = new GridBackground();
            Insert(0, grid);  // Index 0 = behind everything else
            grid.StretchToParentSize();

            var ss = LoadStyleSheet();
            if (ss != null)
            {
                styleSheets.Add(ss);
            }

            // right-click to add nodes
            searchWindow = ScriptableObject.CreateInstance<BlockSearchWindow>();
            searchWindow.Init(this);
            nodeCreationRequest = ctx =>
            {
                SearchWindow.Open(new SearchWindowContext(ctx.screenMousePosition), searchWindow);
            };


            deleteSelection = (operationName, askUser) =>
            {

                var edgesToRemove = selection.OfType<Edge>().ToList();  // Get all selected edges
                var nodesToRemove = selection.OfType<BlockNodeView>().ToList();  // Get all selected nodes


                foreach (var edge in edgesToRemove)
                {
                    RemoveEdgeFromGraph(edge);  // Data: delete connection from graph.connections
                    RemoveElement(edge);         // UI: remove edge visual
                }

                foreach (var node in nodesToRemove)
                {
                    RemoveBlockFromGraph(node);  // Data: delete block from graph.blocks
                    RemoveElement(node);          // UI: remove node visual
                }

                window.MarkDirty();  // Tell Unity to save the changes
            };

            graphViewChanged = OnGraphChanged;
        }

        StyleSheet LoadStyleSheet()
        {
            var guids = AssetDatabase.FindAssets("BlockGraphStyle t:StyleSheet");
            if (guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                return AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
            }
            return null;
        }

        public override List<UnityEditor.Experimental.GraphView.Port> GetCompatiblePorts(
            UnityEditor.Experimental.GraphView.Port startPort, NodeAdapter nodeAdapter)
        {

            return ports.Where(p =>
                p.direction != startPort.direction        // Input can't connect to input
                && p.node != startPort.node               // Can't connect to same node
                && ArePortsCompatible(startPort, p)       // Type must match
            ).ToList();  // Execute query and return as List
        }

        bool ArePortsCompatible(UnityEditor.Experimental.GraphView.Port a, UnityEditor.Experimental.GraphView.Port b)
        {

            if (a.portType == typeof(FlowPort) && b.portType == typeof(FlowPort))
            {
                return true;
            }

            if (a.portType == typeof(FlowPort) || b.portType == typeof(FlowPort))
            {
                return false;
            }

            return a.portType == b.portType;
        }

        GraphViewChange OnGraphChanged(GraphViewChange change)
        {
            if (change.edgesToCreate != null)
            {
                foreach (var edge in change.edgesToCreate)
                {
                    AddEdgeToGraph(edge);
                }
            }

            if (change.elementsToRemove != null)
            {
                foreach (var elem in change.elementsToRemove)
                {
                    if (elem is Edge edge)
                    {
                        RemoveEdgeFromGraph(edge);
                    }
                    else if (elem is BlockNodeView node)
                    {
                        RemoveBlockFromGraph(node);
                    }
                }
            }

            window.MarkDirty();
            return change;
        }

        // --- Graph data sync ---

        public void PopulateFromGraph(BlockGraph graph)
        {
            ClearGraph();  // Remove any existing visuals
            this.graph = graph;

            if (graph == null) { return; }


            // This is needed for the second loop where we create edges
            var nodeMap = new Dictionary<string, BlockNodeView>();  // e.g., "abc123" → BlockNodeView
            

            foreach (var block in graph.blocks)
            {
                // Rebuild ports so migrated blocks (e.g. CompareBlock) always show latest port layout.
                block.RebuildDynamicPorts();

                BlockNodeView nodeView;
                if (block.GetType().Name == "ComponentInvokeBlock")
                {
                    nodeView = new ComponentInvokeNodeView(block);  // Custom UI with dropdowns
                }
                else
                {
                    nodeView = new BlockNodeView(block);  // Standard UI
                }
                
                nodeView.SetPosition(new Rect(block.editorPosition, Vector2.zero));  // Position from saved data
                AddElement(nodeView);  // Add to the graph canvas
                nodeMap[block.id] = nodeView;  // Store for edge creation below
            }


            // We need nodeMap because connections only store IDs, not references
            foreach (var conn in graph.connections)
            {

                if (!nodeMap.TryGetValue(conn.fromBlockId, out var fromNode)) { continue; }  // Skip if not found
                if (!nodeMap.TryGetValue(conn.toBlockId, out var toNode)) { continue; }


                var outPort = fromNode.GetOutputPort(conn.fromPortName);  // e.g., "Out"
                var inPort = toNode.GetInputPort(conn.toPortName);        // e.g., "In"
                if (outPort == null || inPort == null) { continue; }


                var edge = outPort.ConnectTo(inPort);
                AddElement(edge);  // Add to canvas
            }

            schedule.Execute(() => FrameAll()).ExecuteLater(100);
        }

        public void ClearGraph()
        {
            graphElements.ForEach(RemoveElement);
            graph = null;
        }

        public BlockNodeView AddBlockNode(Type blockType, Vector2 position)
        {
            if (graph == null) { return null; }

            Undo.RecordObject(graph, "Add Block");
            var block = graph.AddBlock(blockType);
            block.editorPosition = position;
            EditorUtility.SetDirty(graph);


            BlockNodeView nodeView;
            if (block.GetType().Name == "ComponentInvokeBlock")
            {
                nodeView = new ComponentInvokeNodeView(block);
            }
            else
            {
                nodeView = new BlockNodeView(block);
            }
            
            nodeView.SetPosition(new Rect(position, Vector2.zero));
            AddElement(nodeView);

            window.MarkDirty();
            return nodeView;
        }

        void AddEdgeToGraph(Edge edge)
        {
            if (graph == null) { return; }


            var fromNode = edge.output.node as BlockNodeView;
            var toNode = edge.input.node as BlockNodeView;
            if (fromNode == null || toNode == null) { return; }


            Undo.RecordObject(graph, "Connect");

            graph.Connect(fromNode.BlockId, edge.output.portName, toNode.BlockId, edge.input.portName);
        }

        void RemoveEdgeFromGraph(Edge edge)
        {
            if (graph == null) { return; }

            var fromNode = edge.output.node as BlockNodeView;
            var toNode = edge.input.node as BlockNodeView;
            if (fromNode == null || toNode == null) { return; }

            Undo.RecordObject(graph, "Disconnect");
            graph.Disconnect(fromNode.BlockId, edge.output.portName, toNode.BlockId, edge.input.portName);
        }

        void RemoveBlockFromGraph(BlockNodeView nodeView)
        {
            if (graph == null) { return; }
            Undo.RecordObject(graph, "Remove Block");
            graph.RemoveBlock(nodeView.BlockId);
        }

        public void SavePositions()
        {
            if (graph == null) { return; }

            foreach (var elem in graphElements)
            {
                if (elem is BlockNodeView nodeView)
                {
                    var block = graph.GetBlock(nodeView.BlockId);
                    if (block != null)
                    {
                        block.editorPosition = nodeView.GetPosition().position;
                    }
                }
            }
            EditorUtility.SetDirty(graph);
        }

        public void SyncToGraph()
        {
            SavePositions();

            foreach (var elem in graphElements)
            {
                if (elem is BlockNodeView nodeView)
                {
                    nodeView.SyncToBlock();
                }
            }
        }
    }

    // Marker type so flow ports have a distinct System.Type for compatibility checks
    public class FlowPort { }
}
