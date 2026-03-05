// Right-click context menu to add new blocks to the graph
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using BlockSystem.Core;
using BlockSystem.Blocks;

namespace BlockSystem.Editor
{

    public class BlockSearchWindow : ScriptableObject, ISearchWindowProvider
    {
        BlockGraphView graphView;  // Reference to the graph where we add nodes
        EditorWindow window;       // Needed for screen-to-graph coordinate conversion
        Texture2D indentIcon;      // 1x1 transparent pixel (Unity bug workaround)

        public void Init(BlockGraphView view)
        {
            graphView = view;
            window = EditorWindow.focusedWindow;


            // need a non-null icon or they won't appear. So we create a 1x1 transparent texture.
            indentIcon = new Texture2D(1, 1);
            indentIcon.SetPixel(0, 0, Color.clear);  // Fully transparent
            indentIcon.Apply();  // Upload to GPU
        }

        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext ctx)
        {

            // SearchTreeEntry creates selectable items (level 2)
            var tree = new List<SearchTreeEntry>
            {
                new SearchTreeGroupEntry(new GUIContent("Add Block"), 0),  // Root title


                new SearchTreeGroupEntry(new GUIContent("Objects"), 1),  // Category header
                Entry("Spawn (Cube/Sphere/Cylinder)", typeof(SpawnBlock), 2),  // Selectable item

                // transform
                new SearchTreeGroupEntry(new GUIContent("Transform"), 1),
                Entry("Move", typeof(MoveBlock), 2),
                Entry("Rotate", typeof(RotateBlock), 2),
                Entry("Scale", typeof(ScaleBlock), 2),

                // values
                new SearchTreeGroupEntry(new GUIContent("Values"), 1),
                Entry("Float", typeof(FloatValue), 2),
                Entry("Vector3", typeof(Vector3Value), 2),
                Entry("Bool", typeof(BoolValue), 2),
                Entry("String", typeof(StringValue), 2),
                Entry("Math (+−×÷)", typeof(MathBlock), 2),

                // logic
                new SearchTreeGroupEntry(new GUIContent("Logic"), 1),
                Entry("Compare (> < ==)", typeof(CompareBlock), 2),
                Entry("Branch (If/Else)", typeof(BranchBlock), 2),

                // utility
                new SearchTreeGroupEntry(new GUIContent("Utility"), 1),
                Entry("Debug Log", typeof(DebugLogBlock), 2),
                Entry("Delay (Timer)", typeof(DelayBlock), 2),
                Entry("Parallel (Fan-out)", typeof(ParallelBlock), 2),
                Entry("Sequence (Ordered)", typeof(SequenceBlock), 2),
                Entry("Repeat (Loop N×)", typeof(RepeatBlock), 2),
                Entry("ForEach Tag", typeof(ForEachTagBlock), 2),

                // ai
                new SearchTreeGroupEntry(new GUIContent("AI"), 1),
                Entry("AI Block (LLM)", typeof(AIBlock), 2),
            };


            var componentInvokeType = System.Type.GetType("BlockSystem.Blocks.ComponentInvokeBlock, Assembly-CSharp");
            if (componentInvokeType != null)
            {
                tree.Insert(tree.Count - 3, Entry("Component Invoke (Call Method)", componentInvokeType, 2));
            }

            return tree;
        }

        SearchTreeEntry Entry(string label, Type type, int level)
        {

            return new SearchTreeEntry(new GUIContent(label, indentIcon))
            {
                level = level,
                userData = type  // Attach Type so we know what to create later
            };
        }

        public bool OnSelectEntry(SearchTreeEntry entry, SearchWindowContext ctx)
        {
            var type = entry.userData as Type;  // Get the Type we stored earlier
            if (type == null) return false;     // User clicked a category header, not a block


            if (window == null)
            {
                window = EditorWindow.focusedWindow;
            }
            
            if (window == null) return false;


            // ctx.screenMousePosition is in screen space (pixels from top-left of monitor)
            // window.position.position is the window's screen offset
            // Subtracting gives us position relative to the editor window
            var windowPos = window.position;
            var localPos = ctx.screenMousePosition - windowPos.position;

            graphView.AddBlockNode(type, localPos);  // Create the block at that position
            return true;  // Close the search window
        }
    }
}
