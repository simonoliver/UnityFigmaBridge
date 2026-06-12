using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityFigmaBridge.Runtime.UI;

namespace UnityFigmaBridge.Editor.PrototypeFlow
{
    public static class BehaviourBindingManager
    {


        private const int MAX_SEARCH_DEPTH_FOR_TRANSFORMS = 3;
        
        /// <summary>
        /// Add essential UI components to screens if they're missing (Canvas, CanvasScaler, GraphicRaycaster)
        /// </summary>
        /// <param name="screenGameObject">The screen GameObject to enhance</param>
        /// <param name="screenWidth">Reference width from Figma (default: 1920)</param>
        /// <param name="screenHeight">Reference height from Figma (default: 1080)</param>
        public static void EnhanceScreenWithComponents(GameObject screenGameObject, float screenWidth = 1920f, float screenHeight = 1080f)
        {
            // Add Canvas if missing
            if (screenGameObject.GetComponent<Canvas>() == null)
            {
                var canvas = screenGameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.TexCoord2 | AdditionalCanvasShaderChannels.TexCoord3 | AdditionalCanvasShaderChannels.Normal | AdditionalCanvasShaderChannels.Tangent;
            }
            
            // Add CanvasScaler if missing
            if (screenGameObject.GetComponent<CanvasScaler>() == null)
            {
                var canvasScaler = screenGameObject.AddComponent<CanvasScaler>();
                canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                // Use Figma screen dimensions as reference resolution
                canvasScaler.referenceResolution = new Vector2(screenWidth, screenHeight);
            }
            
            // Add GraphicRaycaster if missing
            if (screenGameObject.GetComponent<GraphicRaycaster>() == null)
            {
                var graphicRaycaster = screenGameObject.AddComponent<GraphicRaycaster>();
                graphicRaycaster.blockingObjects = GraphicRaycaster.BlockingObjects.TwoD;
            }
        }
        
        /// <summary>
        /// Attempts to find a suitable mono behaviour to bind
        /// </summary>
        /// <param name="node"></param>
        /// <param name="gameObject"></param>
        private static void BindBehaviourToNode(GameObject gameObject, FigmaImportProcessData importProcessData)
        {
            var bindingNameSpace = importProcessData.Settings.ScreenBindingNamespace;
            var className = $"{gameObject.name}";
           
            // We'll want to search all assemblies
            var matchingType = GetTypeByName(bindingNameSpace,className);
            if (matchingType == null)
            {
                // No matching type found
                return;
            }
            //Debug.Log($"Matching type found {className}");

            if (!matchingType.IsSubclassOf(typeof(Component)))
            {
                // Type found but is not a Component, cannot attach");
                Debug.Log($"Type found for {gameObject.name} with expected class name {className} in namespace {bindingNameSpace} is not a Component");
                return;
            }
            // Make sure it doesnt already have this component attached (this can happen for nested components)
            var attachedBehaviour = gameObject.GetComponent(matchingType);
            if (attachedBehaviour==null) 
            {
                attachedBehaviour = gameObject.AddComponent(matchingType);
                
                    // Move component to the top of the inspector
                    for (int i = 0; i < gameObject.GetComponents<Component>().Length - 1; i++)
                    {
                        UnityEditorInternal.ComponentUtility.MoveComponentUp(attachedBehaviour);
                    }
                }
            
            // Find all fields for this class, and if inherit from component, look to assign
            BindFieldsForComponent(gameObject, attachedBehaviour);

        }

        public static void BindFieldsForComponent(GameObject gameObject, Component component)
        {
            var componentType = component.GetType();
            
            // Get all fields (public and private)
            FieldInfo[] allFields = componentType.GetFields(
                BindingFlags.Public | 
                BindingFlags.NonPublic | 
                BindingFlags.Instance |
                BindingFlags.IgnoreCase);
            
            // Filter to only serializable fields (Unity serialization rules)
            List<FieldInfo> serializableFields = new List<FieldInfo>();
            
            foreach (var field in allFields)
            {
                // Skip if marked with NonSerialized attribute
                if (field.GetCustomAttribute(typeof(NonSerializedAttribute)) != null)
                    continue;
                
                // Check public fields - they serialize by default (unless NonSerialized)
                if (field.IsPublic)
                {
                    serializableFields.Add(field);
                }
                // Check private/internal fields - they only serialize if marked with SerializeField
                else if (field.GetCustomAttribute(typeof(SerializeField)) != null)
                {
                    serializableFields.Add(field);
                }
            }
            
            // Now bind values to serializable fields
            foreach (var field in serializableFields)
            {
                var fieldType = field.FieldType;
                // See if there is a child transform with matching name (case insensitive)
                var matchingTransform = GetChildTransformByName(gameObject.transform, field.Name, true, MAX_SEARCH_DEPTH_FOR_TRANSFORMS);
                if (matchingTransform)
                {
                    if (fieldType == typeof(GameObject))
                    {
                        field.SetValue(component, matchingTransform.gameObject);
                    }
                    else if (fieldType.IsSubclassOf(typeof(Component)))
                    {
                        // Try and find a matching component
                        var matchingComponent = matchingTransform.gameObject.GetComponent(fieldType);
                        if (matchingComponent)
                        {
                            // Found matching component - set
                            field.SetValue(component, matchingComponent);
                        }
                    }
                }
            }
            
            // Bind methods!
            var methods = componentType.GetMethods().Where(m=>m.GetCustomAttributes(typeof(BindFigmaButtonPress), false).Length > 0)
                .ToArray();

            foreach (var method in methods)
            {
                var buttonPressMethodAttribute = (BindFigmaButtonPress) method.GetCustomAttribute(typeof(BindFigmaButtonPress));
                //Debug.Log($"Attempting to bind method {method.Name} to button {buttonPressMethodAttribute.TargetButtonName}");
                var targetButtonTransform=GetChildTransformByName(gameObject.transform, buttonPressMethodAttribute.TargetButtonName, true,MAX_SEARCH_DEPTH_FOR_TRANSFORMS);
                if (targetButtonTransform != null)
                {
                    // Found matching transform, try and get button
                    var targetButton = targetButtonTransform.GetComponent<Button>();
                    if (targetButton != null)
                    {
                        //Debug.Log($"Found button on object {targetButtonTransform.name}");
                        // Some info here - https://stackoverflow.com/questions/40655089/how-to-add-persistent-listener-to-button-onclick-event-in-unity-editor-script
                        // And here https://stackoverflow.com/questions/47367429/is-it-possible-to-turn-a-string-of-a-function-name-to-a-unityaction
     
                       // Create a delegate for this method on this instance
                       UnityAction action = (UnityAction) Delegate.CreateDelegate(typeof(UnityAction),component, method, true);
                       // Assign this to the target button
                       UnityEventTools.AddPersistentListener(targetButton.onClick, action);
                    }
                }
            }
            
        }

        /// <summary>
        /// Finds a child node (case insensitive)
        /// </summary>
        /// <param name="transform"></param>
        /// <param name="childName"></param>
        /// <param name="caseInsensitive"></param>
        /// <returns></returns>
        private static Transform GetChildTransformByName(Transform transform, string childName,bool caseInsensitive,int depthSearch)
        {
            var numChildren = transform.childCount;
            for (var i = 0; i < numChildren; i++)
            {
                var childTransform = transform.GetChild(i);
                if (CheckNodeNameMatches(childTransform, childName, caseInsensitive)) return childTransform;
            }

            if (depthSearch > 0)
            {
                for (var i = 0; i < numChildren; i++)
                {
                    var childTransform = transform.GetChild(i);
                    var foundInChildNode =
                        GetChildTransformByName(childTransform, childName, caseInsensitive, depthSearch - 1);
                    if (foundInChildNode != null) return foundInChildNode;
                }
            }
            return null;
        }

        private static bool CheckNodeNameMatches(Transform transform, string nameMatch, bool caseInsensitive)
        {
            if (caseInsensitive && transform.name == nameMatch) return true;
            if (!caseInsensitive && String.Equals(transform.name, nameMatch, StringComparison.CurrentCultureIgnoreCase)) return true;

            // Normalize both strings for comparison (remove spaces, dashes, and underscores)
            var normalizedTransformName = NormalizeFieldName(transform.name);
            var normalizedNameMatch = NormalizeFieldName(nameMatch);
            
            if (normalizedTransformName == normalizedNameMatch)
                return true;

            // If this contains an underscore, check the substring after
            // This is to allow matches of fields such as m_ScoreLabel as "ScoreLabel" from figma doc
            if (nameMatch.Contains("_"))
            {
                return CheckNodeNameMatches(transform, nameMatch.Substring(nameMatch.IndexOf("_", StringComparison.Ordinal) + 1),
                    caseInsensitive);
            }
            
            return false;
        }
        
        /// <summary>
        /// Normalize field name by removing spaces, dashes, underscores and converting to lowercase
        /// This allows matching Figma names like "My Label" or "my-label" to C# fields like "MyLabel"
        /// </summary>
        private static string NormalizeFieldName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;
            
            // Remove spaces, dashes, and underscores, then convert to lowercase
            return System.Text.RegularExpressions.Regex.Replace(name, @"[\s\-_]", "").ToLower();
        }
        
        
        
        public static Type GetTypeByName(string nameSpace,string name)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (Type type in assembly.GetTypes())
                {
                    if (String.Equals(type.Name, name, StringComparison.CurrentCultureIgnoreCase))
                    {
                        if (nameSpace.Length > 0)
                        {
                            // If a namespace has been specified and doesnt match, ignore
                            if (!String.Equals(nameSpace, type.Namespace, StringComparison.CurrentCultureIgnoreCase))
                                return null;
                        }
                        return type;
                    }
                }
            }
 
            return null;
        }

        /// <summary>
        /// Get Type by fully qualified name (e.g., "UnityEngine.UI.Button")
        /// </summary>
        private static Type GetTypeByFullName(string fullTypeName)
        {
            if (string.IsNullOrEmpty(fullTypeName))
                return null;

            // First, try using Type.GetType() which works for most built-in types
            var type = Type.GetType(fullTypeName);
            if (type != null)
                return type;

            // If not found, search all loaded assemblies
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType(fullTypeName);
                if (type != null)
                    return type;
            }

            return null;
        }

        /// <summary>
        /// Bind behaviours to every component and flowScreen generated during the process
        /// </summary>
        /// <param name="figmaImportProcessData"></param>
        public static void BindBehaviours(FigmaImportProcessData figmaImportProcessData)
        {
            // Add all components and flowScreen prefabs, to apply behaviours
            var allComponentPrefabsToBindBehaviours = figmaImportProcessData.ComponentData.AllComponentPrefabs;
            allComponentPrefabsToBindBehaviours.AddRange(figmaImportProcessData.ScreenPrefabs);
            
            foreach (var sourcePrefab in allComponentPrefabsToBindBehaviours)
            {
                string prefabAssetPath = AssetDatabase.GetAssetPath(sourcePrefab);
                GameObject instantiatedPrefab = PrefabUtility.LoadPrefabContents(prefabAssetPath);
                BindBehaviourToNodeAndChildren(instantiatedPrefab,figmaImportProcessData);
               
                // Write prefab with changes
                PrefabUtility.SaveAsPrefabAsset(instantiatedPrefab, prefabAssetPath);
                PrefabUtility.UnloadPrefabContents(instantiatedPrefab);
            }
        }

        /// <summary>
        /// Bind behaviour to all nodes within a tree structure 
        /// </summary>
        /// <param name="targetGameObject"></param>
        /// <param name="figmaImportProcessData"></param>
        private static void BindBehaviourToNodeAndChildren(GameObject targetGameObject,FigmaImportProcessData figmaImportProcessData)
        {
           // Apply depth-first application of node behaviours (as assumes parent nodes will want ref to children rather than vice versa)
           var numChildren = targetGameObject.transform.childCount;
           for (var i = 0; i < numChildren; i++)
           {
               // Apply to child nodes first
               var childTransform = targetGameObject.transform.GetChild(i);
               BindBehaviourToNodeAndChildren(childTransform.gameObject, figmaImportProcessData);
           }
           // Finally apply to this node
           BindBehaviourToNode(targetGameObject, figmaImportProcessData);

            // Add in any special behaviours driven by name or other rules. If special case, dont add any more behaviours
            if(figmaImportProcessData.Settings.EnableAddSpecialBehaviours)
            {
                var specialCaseNodes = AddSpecialBehavioursToNode(targetGameObject, figmaImportProcessData);
                if (specialCaseNodes.Count > 0)
                {
                    Debug.Log($"Added special case behaviour to node {targetGameObject.name}: {string.Join(", ", specialCaseNodes.Select(c => c.GetType().Name))}");
                }
            }
        }

        /// <summary>
        /// Apply auto component bindings based on node name filter rules from settings
        /// </summary>
        private static List<Component> AddSpecialBehavioursToNode(GameObject gameObject, FigmaImportProcessData importProcessData)
        {
            var addedComponents = new List<Component>();

            if (importProcessData.Settings.SpecialBehaviourBindings == null || importProcessData.Settings.SpecialBehaviourBindings.Count == 0)
                return addedComponents;

            foreach (var bindingRule in importProcessData.Settings.SpecialBehaviourBindings)
            {
                // Skip if no filter is specified
                if (string.IsNullOrEmpty(bindingRule.NodeNameFilter)) 
                    continue;
                
                // Check if node name contains the filter string (case-insensitive)
                if (!gameObject.name.ToLower(CultureInfo.InvariantCulture).Contains(bindingRule.NodeNameFilter.ToLower(CultureInfo.InvariantCulture)))
                    continue;

                // Try to find the component type using fully qualified name
                var componentType = GetTypeByFullName(bindingRule.ComponentTypeName);
                if (componentType == null)
                {
                    Debug.LogWarning($"type '{bindingRule.ComponentTypeName}' not found for binding rule on node '{gameObject.name}'");
                    continue;
                }

                // Check if component already exists
                var existingComponent = gameObject.GetComponent(componentType);
                if (existingComponent != null)
                {
                    // Component already exists, just track it for field binding
                    addedComponents.Add(existingComponent);
                    continue;
                }

                // Add the component
                try
                {
                    var component = gameObject.AddComponent(componentType);
                    if (component == null)
                    {
                        Debug.LogError($"Failed to add component '{bindingRule.ComponentTypeName}' to '{gameObject.name}' for unknown reasons");
                        continue;
                    }
                    Debug.Log($"Auto-bound component '{bindingRule.ComponentTypeName}' to node '{gameObject.name}' (matched filter: '{bindingRule.NodeNameFilter}')", gameObject);
                    addedComponents.Add(component); 

                    BindFieldsForComponent(gameObject, component);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to add component '{bindingRule.ComponentTypeName}' to '{gameObject.name}': {e.Message}");
                }
            }
            return addedComponents;
        }

    }
}