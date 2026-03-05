using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using BlockSystem.Core;

namespace BlockSystem.Blocks
{
    /// <summary>
    /// Invokes a member on a component attached to a target GameObject.
    /// </summary>
    [Serializable]
    public class ComponentInvokeBlock : Block
    {
        public GameObject targetObject;      // The GameObject with the component
        public string componentTypeName;     // e.g., "Animator", "AudioSource", "MyCustomScript"
        public string methodName;            // e.g., "Play", "SetTrigger", "StartMoving"
        public List<string> parameterValues;

        protected override void SetupPorts()
        {
            AddInput("Start", PortType.Flow);
            AddInput("Target", PortType.GameObject);
            AddOutput("Next", PortType.Flow);
        }

        public override void Execute(GraphContext ctx)
        {
            var target = In<GameObject>("Target");
            if (target == null)
                target = targetObject;

            if (target == null)
            {
                Debug.LogWarning($"[ComponentInvokeBlock {id}] No target GameObject specified.");
                return;
            }

            if (string.IsNullOrEmpty(componentTypeName) || string.IsNullOrEmpty(methodName))
            {
                Debug.LogWarning($"[ComponentInvokeBlock {id}] Component type or method name not set.");
                return;
            }

            Component component = null;
            foreach (var comp in target.GetComponents<Component>())
            {
                if (comp != null && comp.GetType().Name == componentTypeName)
                {
                    component = comp;
                    break;
                }
            }

            if (component == null)
            {
                Debug.LogWarning($"[ComponentInvokeBlock {id}] Component '{componentTypeName}' not found on {target.name}");
                return;
            }

            var memberName = methodName;
            if (memberName.Contains(" ("))
                memberName = memberName.Substring(0, memberName.LastIndexOf(" ("));

            int parenIndex = memberName.IndexOf('(');
            if (parenIndex > 0)
                memberName = memberName.Substring(0, parenIndex);

            var type = component.GetType();
            var paramCount = (parameterValues?.Count ?? 0);

            var allMethods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance);
            var possibleMethods = allMethods.Where(m => m.Name == memberName).ToList();

            if (possibleMethods.Count > 0)
            {
                var noParamMethod = possibleMethods.FirstOrDefault(m => m.GetParameters().Length == 0);
                if (noParamMethod != null && paramCount == 0)
                {
                    try
                    {
                        noParamMethod.Invoke(component, null);
                        Debug.Log($"[ComponentInvokeBlock {id}] Called {componentTypeName}.{memberName}() on {target.name}");
                        return;
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[ComponentInvokeBlock {id}] Error invoking {memberName}: {e.Message}");
                        return;
                    }
                }
                
                foreach (var method in possibleMethods)
                {
                    var parameters = method.GetParameters();
                    if (parameters.Length != paramCount)
                        continue;

                    try
                    {
                        var paramValues = new object[parameters.Length];
                        bool canExecute = true;

                        for (int i = 0; i < parameters.Length; i++)
                        {
                            if (i < (parameterValues?.Count ?? 0))
                            {
                                paramValues[i] = ConvertValue(parameterValues[i], parameters[i].ParameterType);

                                if (paramValues[i] == null && parameters[i].ParameterType.IsValueType)
                                {
                                    Debug.LogWarning($"[ComponentInvokeBlock {id}] Cannot convert '{parameterValues[i]}' to {parameters[i].ParameterType.Name}");
                                    canExecute = false;
                                    break;
                                }
                            }
                            else
                            {
                                paramValues[i] = GetDefaultValue(parameters[i].ParameterType);
                            }
                        }

                        if (!canExecute)
                            continue;

                        method.Invoke(component, paramValues);
                        Debug.Log($"[ComponentInvokeBlock {id}] Called {componentTypeName}.{memberName}() with {paramCount} parameters on {target.name}");
                        return;
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[ComponentInvokeBlock {id}] Method invocation failed: {e.Message}");
                    }
                }
            }

            var property = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.Instance);
            if (property != null && property.CanRead)
            {
                try
                {
                    var value = property.GetValue(component);
                    Debug.Log($"[ComponentInvokeBlock {id}] Read property {componentTypeName}.{memberName} = {value}");
                    return;
                }
                catch (Exception e)
                {
                    Debug.LogError($"[ComponentInvokeBlock {id}] Error reading property {memberName}: {e.Message}");
                    return;
                }
            }

            var field = type.GetField(memberName, BindingFlags.Public | BindingFlags.Instance);
            if (field != null)
            {
                try
                {
                    var value = field.GetValue(component);
                    Debug.Log($"[ComponentInvokeBlock {id}] Read field {componentTypeName}.{memberName} = {value}");
                    return;
                }
                catch (Exception e)
                {
                    Debug.LogError($"[ComponentInvokeBlock {id}] Error reading field {memberName}: {e.Message}");
                    return;
                }
            }

            Debug.LogWarning($"[ComponentInvokeBlock {id}] Member '{memberName}' not found on {componentTypeName}, or parameter types didn't match");
        }

        private object ConvertValue(string stringValue, Type targetType)
        {
            if (stringValue == null || stringValue == "") return GetDefaultValue(targetType);

            if (targetType == typeof(float))
                return float.TryParse(stringValue, out var f) ? f : 0f;
            if (targetType == typeof(int))
                return int.TryParse(stringValue, out var i) ? i : 0;
            if (targetType == typeof(bool))
                return bool.TryParse(stringValue, out var b) ? b : false;
            if (targetType == typeof(string))
                return stringValue;
            if (targetType == typeof(double))
                return double.TryParse(stringValue, out var d) ? d : 0d;
            if (targetType == typeof(long))
                return long.TryParse(stringValue, out var l) ? l : 0L;

            return GetDefaultValue(targetType);
        }

        private object GetDefaultValue(Type type)
        {
            if (type.IsValueType)
                return Activator.CreateInstance(type);
            return null;
        }
    }
}
