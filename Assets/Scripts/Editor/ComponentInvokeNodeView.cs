using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using BlockSystem.Core;
using BlockSystem.Blocks;

namespace BlockSystem.Editor
{
    /// <summary>
    /// Custom node view for ComponentInvokeBlock with dynamic component/method dropdowns.
    /// When you select a GameObject, it automatically populates available components and their methods.
    /// </summary>
    public class ComponentInvokeNodeView : BlockNodeView
    {
        ComponentInvokeBlock invokeBlock;
        DropdownField componentDropdown;
        DropdownField methodDropdown;
        VisualElement parameterFieldsContainer;
        

        Dictionary<string, MethodInfo> methodCache = new Dictionary<string, MethodInfo>();

        public ComponentInvokeNodeView(Block block) : base(block)
        {
            invokeBlock = block as ComponentInvokeBlock;
            if (invokeBlock == null)
            {
                Debug.LogError($"[ComponentInvokeNodeView] Block is not a ComponentInvokeBlock: {block.GetType().Name}");
                return;
            }

            Debug.Log($"[ComponentInvokeNodeView] Creating custom UI for ComponentInvokeBlock");

            BuildComponentUI();
        }

        void BuildComponentUI()
        {
            try
            {
                Debug.Log("[ComponentInvokeNodeView.BuildComponentUI] Starting...");
                
                var container = new VisualElement();
                container.style.paddingLeft = 8;
                container.style.paddingRight = 8;
                container.style.paddingTop = 4;
                container.style.paddingBottom = 4;
                container.style.marginBottom = 8;
                container.style.minHeight = 120;


                var objectLabel = new Label("Target GameObject");
                objectLabel.style.fontSize = 11;
                container.Add(objectLabel);
                
                var objectField = new ObjectField()
                {
                    objectType = typeof(GameObject),
                    value = invokeBlock.targetObject
                };
                objectField.style.marginBottom = 4;
                objectField.RegisterValueChangedCallback(evt =>
                {
                    invokeBlock.targetObject = evt.newValue as GameObject;
                    RefreshComponentList();
                });
                container.Add(objectField);


                componentDropdown = new DropdownField("Component", new List<string> { "Select GameObject first" }, 0);
                componentDropdown.style.marginBottom = 8;
                componentDropdown.style.minHeight = 20;
                componentDropdown.RegisterValueChangedCallback(evt =>
                {
                    invokeBlock.componentTypeName = evt.newValue;
                    RefreshMethodList();
                });
                container.Add(componentDropdown);


                methodDropdown = new DropdownField("Method", new List<string> { "Select Component first" }, 0);
                methodDropdown.style.marginBottom = 8;
                methodDropdown.style.minHeight = 20;
                methodDropdown.RegisterValueChangedCallback(evt =>
                {
                    invokeBlock.methodName = evt.newValue;
                    RefreshParameterUI();
                });
                container.Add(methodDropdown);


                parameterFieldsContainer = new VisualElement();
                parameterFieldsContainer.style.marginTop = 8;
                parameterFieldsContainer.style.paddingLeft = 4;
                parameterFieldsContainer.style.borderLeftWidth = 2;
                parameterFieldsContainer.style.borderLeftColor = new Color(0.5f, 0.7f, 1f, 0.5f);
                container.Add(parameterFieldsContainer);


                if (invokeBlock.targetObject != null)
                {
                    RefreshComponentList();
                }

                extensionContainer.Add(container);
                Debug.Log("[ComponentInvokeNodeView.BuildComponentUI] UI added successfully!");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ComponentInvokeNodeView.BuildComponentUI] Error: {ex.Message}\n{ex.StackTrace}");
            }
        }

        void RefreshComponentList()
        {
            if (invokeBlock.targetObject == null)
            {
                componentDropdown.choices = new List<string> { "No GameObject selected" };
                componentDropdown.index = 0;
                methodDropdown.choices = new List<string> { "Select Component first" };
                methodDropdown.index = 0;
                return;
            }


            var components = invokeBlock.targetObject.GetComponents<Component>();
            var componentNames = components
                .Where(c => c != null)  // Skip destroyed components
                .Select(c => c.GetType().Name)
                .Distinct()
                .OrderBy(name => name)
                .ToList();

            if (componentNames.Count == 0)
            {
                componentNames.Add("No components found");
            }

            componentDropdown.choices = componentNames;
            

            if (!string.IsNullOrEmpty(invokeBlock.componentTypeName) && componentNames.Contains(invokeBlock.componentTypeName))
            {
                componentDropdown.value = invokeBlock.componentTypeName;
                RefreshMethodList();
            }
            else
            {
                componentDropdown.index = 0;
                invokeBlock.componentTypeName = componentNames[0];
                RefreshMethodList();
            }
        }

        void RefreshMethodList()
        {
            if (invokeBlock.targetObject == null || string.IsNullOrEmpty(invokeBlock.componentTypeName))
            {
                methodDropdown.choices = new List<string> { "No component selected" };
                methodDropdown.index = 0;
                return;
            }


            Component targetComponent = null;
            foreach (var comp in invokeBlock.targetObject.GetComponents<Component>())
            {
                if (comp != null && comp.GetType().Name == invokeBlock.componentTypeName)
                {
                    targetComponent = comp;
                    break;
                }
            }

            if (targetComponent == null)
            {
                methodDropdown.choices = new List<string> { "Component not found" };
                methodDropdown.index = 0;
                return;
            }

            var type = targetComponent.GetType();
            var allMembers = new List<string>();


            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in properties)
            {
                if (prop.CanRead)
                {
                    allMembers.Add($"{prop.Name} (property)");
                }
            }


            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (var field in fields)
            {
                allMembers.Add($"{field.Name} (field)");
            }


            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance);
            foreach (var method in methods)
            {
                if (method.IsSpecialName) continue;

                var paramInfo = method.GetParameters();
                var paramStr = string.Join(", ", paramInfo.Select(p => $"{p.ParameterType.Name}"));
                if (string.IsNullOrEmpty(paramStr))
                    paramStr = "(no params)";
                    
                allMembers.Add($"{method.Name}({paramStr}) (method)");
            }

            allMembers = allMembers.Distinct().OrderBy(m => m).ToList();

            if (allMembers.Count == 0)
            {
                allMembers.Add("No public members found");
            }

            methodDropdown.choices = allMembers;
            
            if (!string.IsNullOrEmpty(invokeBlock.methodName) && allMembers.Contains(invokeBlock.methodName))
            {
                methodDropdown.value = invokeBlock.methodName;
            }
            else
            {
                methodDropdown.index = 0;
                invokeBlock.methodName = allMembers[0];
            }
        }


        void RefreshParameterUI()
        {
            parameterFieldsContainer.Clear();
            
            if (string.IsNullOrEmpty(invokeBlock.methodName) || !methodCache.ContainsKey(invokeBlock.methodName))
            {
                Debug.Log($"[ComponentInvokeNodeView] Method '{invokeBlock.methodName}' not in cache");
                return;
            }

            var method = methodCache[invokeBlock.methodName];
            var parameters = method.GetParameters();
            
            if (parameters.Length == 0)
            {
                return;  // No parameters needed
            }


            var label = new Label("Parameters");
            label.style.fontSize = 11;
            label.style.marginBottom = 4;
            parameterFieldsContainer.Add(label);

            // Ensure parameter values list exists and has space
            if (invokeBlock.parameterValues == null)
                invokeBlock.parameterValues = new List<string>();
            
            while (invokeBlock.parameterValues.Count < parameters.Length)
                invokeBlock.parameterValues.Add("");


            for (int i = 0; i < parameters.Length; i++)
            {
                var param = parameters[i];
                var paramType = param.ParameterType;
                
                VisualElement paramControl = CreateParameterControl(paramType, param.Name, i);
                if (paramControl != null)
                {
                    parameterFieldsContainer.Add(paramControl);
                }
            }
        }


        VisualElement CreateParameterControl(Type paramType, string paramName, int index)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginBottom = 4;

            var label = new Label(paramName);
            label.style.width = 60;
            label.style.fontSize = 10;
            row.Add(label);

            // Ensure we have space in the values list
            if (invokeBlock.parameterValues.Count <= index)
                invokeBlock.parameterValues.Add("");

            int idx = index;  // Capture for closure


            if (paramType == typeof(bool))
            {

                var toggle = new Toggle();
                toggle.value = bool.TryParse(invokeBlock.parameterValues[idx], out var b) ? b : false;
                toggle.RegisterValueChangedCallback(evt =>
                {
                    if (idx < invokeBlock.parameterValues.Count)
                        invokeBlock.parameterValues[idx] = evt.newValue.ToString();
                });
                row.Add(toggle);
            }
            else if (paramType == typeof(int))
            {

                var intField = new IntegerField();
                intField.value = int.TryParse(invokeBlock.parameterValues[idx], out var i) ? i : 0;
                intField.style.flexGrow = 1;
                intField.RegisterValueChangedCallback(evt =>
                {
                    if (idx < invokeBlock.parameterValues.Count)
                        invokeBlock.parameterValues[idx] = evt.newValue.ToString();
                });
                row.Add(intField);
            }
            else if (paramType == typeof(float))
            {

                var floatField = new FloatField();
                floatField.value = float.TryParse(invokeBlock.parameterValues[idx], out var f) ? f : 0f;
                floatField.style.flexGrow = 1;
                floatField.RegisterValueChangedCallback(evt =>
                {
                    if (idx < invokeBlock.parameterValues.Count)
                        invokeBlock.parameterValues[idx] = evt.newValue.ToString();
                });
                row.Add(floatField);
            }
            else if (paramType == typeof(double))
            {

                var doubleField = new DoubleField();
                doubleField.value = double.TryParse(invokeBlock.parameterValues[idx], out var d) ? d : 0d;
                doubleField.style.flexGrow = 1;
                doubleField.RegisterValueChangedCallback(evt =>
                {
                    if (idx < invokeBlock.parameterValues.Count)
                        invokeBlock.parameterValues[idx] = evt.newValue.ToString();
                });
                row.Add(doubleField);
            }
            else if (paramType == typeof(string))
            {

                var textField = new TextField();
                textField.value = invokeBlock.parameterValues[idx] ?? "";
                textField.style.flexGrow = 1;
                textField.RegisterValueChangedCallback(evt =>
                {
                    if (idx < invokeBlock.parameterValues.Count)
                        invokeBlock.parameterValues[idx] = evt.newValue;
                });
                row.Add(textField);
            }
            else
            {

                var textField = new TextField();
                textField.value = invokeBlock.parameterValues[idx] ?? "";
                textField.style.flexGrow = 1;
                var hint = new Label($"({paramType.Name})");
                hint.style.fontSize = 8;
                hint.style.color = new Color(0.7f, 0.7f, 0.7f);
                textField.RegisterValueChangedCallback(evt =>
                {
                    if (idx < invokeBlock.parameterValues.Count)
                        invokeBlock.parameterValues[idx] = evt.newValue;
                });
                row.Add(textField);
                row.Add(hint);
            }

            return row;
        }
    }
}