using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using BlockSystem.Core;
using BlockSystem.Blocks;

namespace BlockSystem.Editor
{
    /// <summary>
    /// Visual node for a single Block. Creates ports, inline field editors,
    /// and dynamic +/- buttons for ParallelBlock/SequenceBlock.
    /// </summary>
    public class BlockNodeView : Node
    {

        Block block;                                                          // The data model this node represents
        Dictionary<string, UnityEditor.Experimental.GraphView.Port> inputPorts = new();   // e.g., "In" → Port UI element
        Dictionary<string, UnityEditor.Experimental.GraphView.Port> outputPorts = new();  // e.g., "Out" → Port UI element
        Dictionary<string, VisualElement> fieldEditors = new();               // e.g., "speed" → FloatField UI


        static BlockIconConfig iconConfig;


        static BlockIconConfig GetIconConfig()
        {
            if (iconConfig == null)
            {
                var guids = AssetDatabase.FindAssets("BlockIconConfig t:BlockIconConfig");
                if (guids.Length > 0)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    iconConfig = AssetDatabase.LoadAssetAtPath<BlockIconConfig>(path);
                }
            }
            return iconConfig;
        }


        // This makes it easy to visually identify block categories in the graph
        static readonly Dictionary<string, Color> categoryColors = new()
        {
            { "Spawn",      new Color(0.3f, 0.6f, 0.9f) },  // Blue for spawning
            { "Move",       new Color(0.2f, 0.8f, 0.4f) },  // Green for transforms
            { "Rotate",     new Color(0.2f, 0.8f, 0.4f) },
            { "Scale",      new Color(0.2f, 0.8f, 0.4f) },
            { "Float",      new Color(0.9f, 0.7f, 0.2f) },
            { "Vector3",    new Color(0.9f, 0.7f, 0.2f) },
            { "Bool",       new Color(0.9f, 0.7f, 0.2f) },
            { "String",     new Color(0.9f, 0.7f, 0.2f) },
            { "Math",       new Color(0.8f, 0.5f, 0.2f) },
            { "Compare",    new Color(0.7f, 0.3f, 0.7f) },
            { "Branch",     new Color(0.7f, 0.3f, 0.7f) },
            { "DebugLog",   new Color(0.5f, 0.5f, 0.5f) },
            { "AI",         new Color(0.9f, 0.3f, 0.5f) },
            { "Delay",      new Color(0.3f, 0.7f, 0.9f) },
            { "Parallel",   new Color(0.9f, 0.5f, 0.1f) },
            { "Sequence",   new Color(0.5f, 0.9f, 0.6f) },
            { "Repeat",     new Color(0.8f, 0.4f, 0.8f) },
            { "ForEachTag", new Color(1.0f, 0.6f, 0.2f) },
        };

        public string BlockId => block.id;

        public BlockNodeView(Block block)
        {
            this.block = block;
            title = block.blockType;
            viewDataKey = block.id;

            // color the title bar
            var color = GetBlockColor(block.blockType);
            titleContainer.style.backgroundColor = color;
            titleContainer.style.borderBottomColor = color * 0.7f;
            titleContainer.style.borderBottomWidth = 2;


            Texture2D iconTexture = null;
            

            var config = GetIconConfig();
            if (config != null)
            {
                var customIcon = config.GetIcon(block.blockType);
                if (customIcon != null)
                {
                    iconTexture = customIcon.texture;  // Use the custom sprite texture
                }
            }
            

            if (iconTexture == null)
            {
                iconTexture = GetBlockIcon(block.blockType);
            }
            
            if (iconTexture != null)
            {
                var icon = new Image { image = iconTexture };
                icon.style.width = 16;
                icon.style.height = 16;
                icon.style.marginRight = 4;
                icon.style.marginLeft = 2;
                titleContainer.Insert(0, icon);  // Insert before title text
            }

            // show the short id
            var idLabel = new Label(block.id);
            idLabel.style.fontSize = 9;
            idLabel.style.color = new Color(1, 1, 1, 0.5f);
            idLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            titleContainer.Add(idLabel);

            BuildPorts();
            BuildFieldEditors();
            BuildDynamicPortControls();

            RefreshExpandedState();
            RefreshPorts();
        }

        Color GetBlockColor(string typeName)
        {
            foreach (var kv in categoryColors)
            {
                if (typeName.Contains(kv.Key))
                    return kv.Value;
            }
            return new Color(0.4f, 0.4f, 0.4f);
        }


        // You can replace these with custom icons from Assets/Icons/ folder
        Texture2D GetBlockIcon(string typeName)
        {
            // Spawning / Objects
            if (typeName.Contains("Spawn"))
                return EditorGUIUtility.IconContent("d_Prefab Icon").image as Texture2D;

            // Transform operations
            if (typeName.Contains("Move") || typeName.Contains("Rotate") || typeName.Contains("Scale"))
                return EditorGUIUtility.IconContent("d_Transform Icon").image as Texture2D;

            // Logic / Branching
            if (typeName.Contains("Branch") || typeName.Contains("Compare"))
                return EditorGUIUtility.IconContent("d_editicon.sml").image as Texture2D;

            // Flow control
            if (typeName.Contains("Sequence") || typeName.Contains("Parallel"))
                return EditorGUIUtility.IconContent("d_Animation.Play").image as Texture2D;

            // Delay / Time
            if (typeName.Contains("Delay") || typeName.Contains("Repeat"))
                return EditorGUIUtility.IconContent("d_WaitSpin00").image as Texture2D;

            // AI
            if (typeName.Contains("AI"))
                return EditorGUIUtility.IconContent("d_ScriptableObject Icon").image as Texture2D;

            // Components
            if (typeName.Contains("Component"))
                return EditorGUIUtility.IconContent("d_cs Script Icon").image as Texture2D;

            // Debug
            if (typeName.Contains("Debug"))
                return EditorGUIUtility.IconContent("d_console.infoicon").image as Texture2D;

            // Values
            if (typeName.Contains("Value") || typeName.Contains("Float") || typeName.Contains("Vector") || typeName.Contains("Bool") || typeName.Contains("String"))
                return EditorGUIUtility.IconContent("d_SceneViewTools").image as Texture2D;

            // Default: no icon
            return null;
        }

        void BuildPorts()
        {
            // input ports
            foreach (var p in block.inputs)
            {
                var portType = GetSystemType(p.type);
                var capacity = p.type == PortType.Flow
                    ? UnityEditor.Experimental.GraphView.Port.Capacity.Multi
                    : UnityEditor.Experimental.GraphView.Port.Capacity.Single;
                var port = InstantiatePort(Orientation.Horizontal, Direction.Input, capacity, portType);
                port.portName = p.name;
                port.portColor = GetPortColor(p.type);
                inputContainer.Add(port);
                inputPorts[p.name] = port;
            }

            // output ports
            foreach (var p in block.outputs)
            {
                var portType = GetSystemType(p.type);
                var capacity = p.type == PortType.Flow
                    ? UnityEditor.Experimental.GraphView.Port.Capacity.Multi
                    : UnityEditor.Experimental.GraphView.Port.Capacity.Single;
                var port = InstantiatePort(Orientation.Horizontal, Direction.Output, capacity, portType);
                port.portName = p.name;
                port.portColor = GetPortColor(p.type);
                outputContainer.Add(port);
                outputPorts[p.name] = port;
            }
        }

        Type GetSystemType(PortType portType) => portType switch
        {
            PortType.Flow => typeof(FlowPort),
            PortType.Float => typeof(float),
            PortType.Bool => typeof(bool),
            PortType.String => typeof(string),
            PortType.Vector3 => typeof(Vector3),
            PortType.GameObject => typeof(GameObject),
            _ => typeof(object)
        };

        Color GetPortColor(PortType portType) => portType switch
        {
            PortType.Flow => new Color(0.9f, 0.9f, 0.9f),
            PortType.Float => new Color(0.4f, 0.8f, 1f),
            PortType.Bool => new Color(1f, 0.4f, 0.4f),
            PortType.String => new Color(1f, 0.6f, 0.8f),
            PortType.Vector3 => new Color(0.4f, 1f, 0.6f),
            PortType.GameObject => new Color(1f, 0.8f, 0.3f),
            _ => Color.gray
        };

        void BuildFieldEditors()
        {
            var container = new VisualElement();
            container.style.paddingLeft = 8;
            container.style.paddingRight = 8;
            container.style.paddingTop = 4;
            container.style.paddingBottom = 4;


            var type = block.GetType();  // e.g., MoveBlock, SpawnBlock, etc.
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);  // Get public instance fields

            bool hasFields = false;
            foreach (var f in fields)
            {

                if (f.DeclaringType == typeof(Block)) continue;  // Only show block-specific fields
                if (f.FieldType == typeof(List<BlockSystem.Core.Port>)) continue;  // Skip port lists

                var editor = CreateFieldEditor(f);  // Build a UI element for this field
                if (editor != null)
                {
                    container.Add(editor);
                    fieldEditors[f.Name] = editor;  // Store for later reference
                    hasFields = true;
                }
            }

            if (hasFields)
                extensionContainer.Add(container);
        }

        VisualElement CreateFieldEditor(FieldInfo field)
        {

            var val = field.GetValue(block);  // e.g., if field is "speed", val might be 5.0f
            
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;  // Label on left, input on right
            row.style.marginBottom = 2;


            var label = new Label(field.Name);  // e.g., "speed", "tag", "iterations"
            label.style.width = 80;
            label.style.fontSize = 11;
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
            label.style.color = new Color(0.8f, 0.8f, 0.8f);
            row.Add(label);


            if (field.FieldType == typeof(float))
            {
                var input = new FloatField();
                input.value = (float)val;  // Set initial value from block
                input.style.flexGrow = 1;

                input.RegisterValueChangedCallback(e => field.SetValue(block, e.newValue));
                row.Add(input);
            }
            else if (field.FieldType == typeof(int))
            {
                var input = new IntegerField();
                input.value = (int)val;
                input.style.flexGrow = 1;
                input.RegisterValueChangedCallback(e => field.SetValue(block, e.newValue));
                row.Add(input);
            }
            else if (field.FieldType == typeof(bool))
            {
                var input = new Toggle();
                input.value = (bool)val;
                input.RegisterValueChangedCallback(e => field.SetValue(block, e.newValue));
                row.Add(input);
            }
            else if (field.FieldType == typeof(string))
            {
                var input = new TextField();
                input.value = (string)val ?? "";
                input.style.flexGrow = 1;
                input.RegisterValueChangedCallback(e => field.SetValue(block, e.newValue));
                row.Add(input);
            }
            else if (field.FieldType == typeof(Vector3))
            {
                var input = new Vector3Field();
                input.value = (Vector3)val;
                input.style.flexGrow = 1;
                input.RegisterValueChangedCallback(e => field.SetValue(block, e.newValue));
                row.Add(input);
            }
            else if (field.FieldType.IsEnum)
            {
                var input = new EnumField((Enum)val);
                input.style.flexGrow = 1;
                input.RegisterValueChangedCallback(e => field.SetValue(block, e.newValue));
                row.Add(input);
            }
            else
            {
                return null;
            }

            return row;
        }

        /// <summary>
        /// Renders "+ Branch" / "− Branch" buttons on nodes that implement
        /// <see cref="IDynamicPortBlock"/>, allowing designers to add or remove
        /// flow outputs directly in the graph editor.
        /// </summary>
        void BuildDynamicPortControls()
        {

            if (block is not IDynamicPortBlock dynamic) return;  // Pattern matching cast

            var row = new VisualElement();
            row.style.flexDirection  = FlexDirection.Row;   // Buttons side-by-side
            row.style.justifyContent = Justify.Center;      // Center horizontally
            row.style.marginTop      = 4;
            row.style.marginBottom   = 4;


            var addBtn = new Button(() =>
            {
                dynamic.AddOutputBranch();  // Data: adds a new Port to the outputs list
                

                var p = block.outputs[block.outputs.Count - 1];  // Get the port we just added
                var portView = InstantiatePort(
                    Orientation.Horizontal,
                    Direction.Output,
                    UnityEditor.Experimental.GraphView.Port.Capacity.Multi,  // Multiple connections allowed
                    GetSystemType(p.type));
                portView.portName  = p.name;         // e.g., "Branch 2"
                portView.portColor = GetPortColor(p.type);
                outputContainer.Add(portView);       // Add to the node's output area
                outputPorts[p.name] = portView;      // Store in dictionary for later lookup
                RefreshPorts();                      // Recalculate layout
                MarkDirtyRepaint();                  // Force visual update
            })
            { text = "+ Branch" };

            addBtn.style.flexGrow = 1;
            addBtn.style.fontSize = 10;
            row.Add(addBtn);


            var removeBtn = new Button(() =>
            {
                if (dynamic.BranchCount <= 1) return;  // Always keep at least 1 branch
                var last = block.outputs.Count > 0 ? block.outputs[block.outputs.Count - 1] : null;
                if (last == null) return;


                if (outputPorts.TryGetValue(last.name, out var portView))
                {
                    outputContainer.Remove(portView);   // Remove from UI
                    outputPorts.Remove(last.name);      // Remove from dictionary
                }

                dynamic.RemoveLastOutputBranch();  // Remove from data model
                RefreshPorts();                     // Recalculate layout
                MarkDirtyRepaint();                 // Force visual update
            })
            { text = "− Branch" };

            removeBtn.style.flexGrow = 1;
            removeBtn.style.fontSize = 10;
            row.Add(removeBtn);

            extensionContainer.Add(row);
        }

        public UnityEditor.Experimental.GraphView.Port GetInputPort(string name) =>
            inputPorts.TryGetValue(name, out var p) ? p : null;

        public UnityEditor.Experimental.GraphView.Port GetOutputPort(string name) =>
            outputPorts.TryGetValue(name, out var p) ? p : null;

        // push current field values back to the block
        public void SyncToBlock()
        {
            block.editorPosition = GetPosition().position;
            // field editors already write back via callbacks, nothing else to do
        }
    }
}
