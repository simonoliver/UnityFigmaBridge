using System;
using System.Collections.Generic;
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
        /// Attempts to find a suitable mono behaviour to bind
        /// </summary>
        /// <param name="node"></param>
        /// <param name="gameObject"></param>
        private static void BindBehaviourToNode(GameObject gameObject, FigmaImportProcessData importProcessData)
        {
            // Add in any special behaviours driven by name or other rules. If special case, dont add any more behaviours
            bool specialCaseNode=AddSpecialBehavioursToNode(gameObject,importProcessData);
            if (specialCaseNode) return;
            
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

            if (!matchingType.IsSubclassOf(typeof(MonoBehaviour)))
            {
                // Type found but is not a MonoBehaviour, cannot attach");
                return;
            }
            // Make sure it doesnt already have this component attached (this can happen for nested components)
            var attachedBehaviour = gameObject.GetComponent(matchingType);
            if (attachedBehaviour==null) attachedBehaviour=gameObject.AddComponent(matchingType);
            
            // Find all fields for this class, and if inherit from component, look to assign
            BindFieldsForComponent(gameObject, attachedBehaviour);
            
        }

        private static bool AddSpecialBehavioursToNode(GameObject gameObject, FigmaImportProcessData importProcessData)
        {
            if (gameObject.name.ToUpper() == "SAFEAREA")
            {
                // Add in a safe area component for correct resizing
                if (gameObject.GetComponent<SafeArea>() == null)
                {
                    gameObject.AddComponent<SafeArea>();
                    // Also move pivot to top left, to make offset calc a bit easier
                    //FigmaDocumentUtils.SetPivot(gameObject.transform as RectTransform, new Vector2(0,1));
                    return true;
                }
            }

            return false;
        }

        public static void BindFieldsForComponent(GameObject gameObject, Component component)
        {
            var componentType = component.GetType();
            
            // Then check private fields
            FieldInfo[] privateSerializedFields=componentType.GetFields(
                BindingFlags.NonPublic | 
                BindingFlags.Instance);
            List<FieldInfo> allSerializedComponentFields = privateSerializedFields.Where(field => field.GetCustomAttribute(typeof(SerializeField)) != null).ToList();
            
            // And add all public fields
            allSerializedComponentFields.AddRange(componentType.GetFields());
            
            foreach (var field in allSerializedComponentFields)
            {
                var fieldType = field.FieldType;
                // See if there is a child transform with matching name (case insensitive)
                var matchingTransform = GetChildTransformByName(gameObject.transform, field.Name, true,MAX_SEARCH_DEPTH_FOR_TRANSFORMS);
                if (matchingTransform)
                {
                    if (fieldType == typeof(GameObject))
                    {
                        field.SetValue(component,matchingTransform.gameObject);
                    }
                    else if (fieldType.IsSubclassOf(typeof(Component)))
                    {
                        // Try and find a matching component
                        var matchingComponent = matchingTransform.gameObject.GetComponent(fieldType);
                        if (matchingComponent)
                        {
                            // Found matching component - set
                            field.SetValue(component,matchingComponent);
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

            // If this contains an underscore, check the substring after
            // This is to allow matches of fields such as m_ScoreLabel as "ScoreLabel" from figma doc
            if (nameMatch.Contains("_"))
            {
                return CheckNodeNameMatches(transform, nameMatch.Substring(nameMatch.IndexOf("_", StringComparison.Ordinal) + 1),
                    caseInsensitive);
            }
            
            return false;
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
        }
    }
}