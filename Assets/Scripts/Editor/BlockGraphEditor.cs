// Custom inspector for BlockGraph assets
using UnityEngine;
using UnityEditor;
using BlockSystem.Core;

namespace BlockSystem.Editor
{

    [CustomEditor(typeof(BlockGraph))]
    public class BlockGraphEditor : UnityEditor.Editor
    {

        public override void OnInspectorGUI()
        {
            var graph = (BlockGraph)target;  // "target" is the selected asset

            EditorGUILayout.LabelField("Block Graph", EditorStyles.boldLabel);

            EditorGUILayout.LabelField($"Blocks: {graph.blocks.Count}  |  Connections: {graph.connections.Count}");

            EditorGUILayout.Space(8);


            if (GUILayout.Button("Open Graph Editor", GUILayout.Height(32)))
            {
                BlockGraphWindow.Open(graph);  // Open the visual editor
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                "Double-click or press the button above to open the visual graph editor.\n" +
                "Then right-click on the canvas to add blocks. Drag between ports to connect.",
                MessageType.Info);
        }
    }
}
