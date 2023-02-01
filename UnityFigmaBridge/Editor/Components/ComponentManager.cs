using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityFigmaBridge.Editor.FigmaApi;
using UnityFigmaBridge.Editor.Nodes;
using UnityFigmaBridge.Editor.PrototypeFlow;
using UnityFigmaBridge.Editor.Utils;
using UnityFigmaBridge.Runtime.UI;
using Object = UnityEngine.Object;

namespace UnityFigmaBridge.Editor.Components
{
    public static class ComponentManager
    {
        
       /// <summary>
       /// Remove component placeholders that are used to mark instantiation locations
       /// </summary>
       /// <param name="figmaImportProcessData"></param>
        public static void RemoveAllTemporaryNodeComponents(FigmaImportProcessData figmaImportProcessData)
       {
           // Remove from components (nested)
            foreach (var componentPrefab in figmaImportProcessData.ComponentData.AllComponentPrefabs)
                RemoveTemporaryNodeComponents(componentPrefab);
            
            // Remove from screens
            foreach (var framePrefab in figmaImportProcessData.ScreenPrefabs.Where(framePrefab => framePrefab!=null))
            {
                RemoveTemporaryNodeComponents(framePrefab);
            }
       }

        /// <summary>
        /// Remove all component placeholders from a given prefab object (could be flowScreen or component)
        /// </summary>
        /// <param name="sourcePrefab"></param>
        private static void RemoveTemporaryNodeComponents(GameObject sourcePrefab)
        {
            var assetPath = AssetDatabase.GetAssetPath(sourcePrefab);
            var prefabContents = PrefabUtility.LoadPrefabContents(assetPath);
            var allPlaceholderComponents = prefabContents.GetComponentsInChildren<FigmaNodeObject>();
            foreach (var placeholder in allPlaceholderComponents)
                Object.DestroyImmediate(placeholder);
            // Save
            PrefabUtility.SaveAsPrefabAsset(prefabContents, assetPath);
            // Unload
            PrefabUtility.UnloadPrefabContents(prefabContents);
        }
        
        
        /// <summary>
        /// Creates a component prefab from a given generated node
        /// </summary>
        /// <param name="node"></param>
        /// <param name="nodeGameObject"></param>
        /// <param name="figmaImportProcessData"></param>
        public static void GenerateComponentAssetFromNode(Node node, GameObject nodeGameObject, FigmaImportProcessData figmaImportProcessData)
        {
            var componentCount = figmaImportProcessData.ComponentData.GetComponentNameCount(node.name);
            var prefabAssetPath = FigmaPaths.GetPathForComponentPrefab(node,componentCount);
            figmaImportProcessData.ComponentData.IncrementComponentNameCount(node.name,componentCount);
            var componentPrefab = PrefabUtility.SaveAsPrefabAssetAndConnect(nodeGameObject, prefabAssetPath, InteractionMode.UserAction);
            figmaImportProcessData.ComponentData.RegisterComponentPrefab(node.id,componentPrefab);
        }
        
        /// <summary>
        /// Instantiates all component prefabs in screens and components (for nested component support)
        /// </summary>
        /// <param name="figmaImportProcessData"></param>
        public static void InstantiateAllComponentPrefabs(FigmaImportProcessData figmaImportProcessData)
        {
            // Instantiate components "within" components (nested components)
            foreach (var componentPrefab in figmaImportProcessData.ComponentData.AllComponentPrefabs)
                InstantiateComponentPrefabs(componentPrefab, figmaImportProcessData);
            
            // Instantiate components within screens 
            foreach (var framePrefab in figmaImportProcessData.ScreenPrefabs.Where(framePrefab => framePrefab!=null))
                InstantiateComponentPrefabs(framePrefab, figmaImportProcessData);
        }
        
        /// <summary>
        /// Instantiates prefabs within a given prefab
        /// </summary>
        /// <param name="sourcePrefab"></param>
        /// <param name="figmaImportProcessData"></param>
        private static void InstantiateComponentPrefabs(GameObject sourcePrefab, FigmaImportProcessData figmaImportProcessData)
        {
            var assetPath = AssetDatabase.GetAssetPath(sourcePrefab);
            var prefabContents = PrefabUtility.LoadPrefabContents(assetPath);
            // Get all placeholders within this prefab - these will be replaced
            var allPlaceholderComponents = prefabContents.GetComponentsInChildren<FigmaComponentNodeMarker>();
            // Track a list of placed and modified components, to allow effective saving
            var modifiedPrefabInstances = new List<GameObject>();
            foreach (var placeholder in allPlaceholderComponents)
            {
                var sourceComponentPrefab = figmaImportProcessData.ComponentData.GetComponentPrefab(placeholder.ComponentId);

                if (sourceComponentPrefab == null) continue;
                
                // Instantiate
                var addedReplacementComponent = (GameObject)PrefabUtility.InstantiatePrefab(sourceComponentPrefab,placeholder.transform.parent);
                // Copy transform data
                UnityUiUtils.CloneTransformData(placeholder.transform as RectTransform, addedReplacementComponent.transform as RectTransform);
                // Copy name
                addedReplacementComponent.name = placeholder.name; // Copy original name
                
                // Change node Id to match instantiated version
                var figmaNodeComponent = addedReplacementComponent.GetComponent<FigmaNodeObject>();
                if (figmaNodeComponent == null)
                {
                    Debug.LogWarning("No FigmaNodeObject on component prefab");
                }
                else
                {
                    figmaNodeComponent.NodeId = placeholder.NodeId;
                }
                
                // Copy transform order
                addedReplacementComponent.transform.SetSiblingIndex(placeholder.transform.GetSiblingIndex()); // Put at same order
                // Get the Node data for this component
                var nodeData = FigmaDataUtils.GetFigmaNodeWithId(figmaImportProcessData.SourceFile, placeholder.NodeId); 
                // Get parent node data for the original node
                var parentNodeData = FigmaDataUtils.GetFigmaNodeWithId(figmaImportProcessData.SourceFile, placeholder.ParentNodeId);
                if (nodeData != null)
                {
                    // Recursively apply all properties for this node object (such as text, image fills etc)
                    ApplyFigmaProperties(nodeData, addedReplacementComponent, parentNodeData, figmaImportProcessData);
                }

                // We want to attempt to link this newly placed item to any parent MonoBehaviours that might need it as a field
                // TODO - Optimise (Right now this is called way more often than needed)
                var parentMonoBehaviours = placeholder.transform.parent.gameObject.GetComponents<MonoBehaviour>();
                foreach (var monoBehaviour in parentMonoBehaviours)
                {
                    BehaviourBindingManager.BindFieldsForComponent(placeholder.transform.parent.gameObject,
                        monoBehaviour);
                }

                // Mark as modified for later saving
                modifiedPrefabInstances.Add(addedReplacementComponent);
                Object.DestroyImmediate(placeholder.gameObject); // Remove the placeholder

            }
            // Save prefab and all changes
            try
            {
                // We might have issue with nested elements so need try catch loop
                // TODO - Check for recurisve nested components
                PrefabUtility.SaveAsPrefabAsset(prefabContents, assetPath);
                
                // Apply changes to the instance as modifications
                foreach (var modifiedPrefabInstance in modifiedPrefabInstances)
                {
                    PrefabUtility.RecordPrefabInstancePropertyModifications(modifiedPrefabInstance);
                }
            }
            catch (Exception e)
            {
                Debug.Log($"Issue saving prefab: {e.ToString()}");
            }

            PrefabUtility.UnloadPrefabContents(prefabContents);
        }

        /// <summary>
        /// Recursively apply properties from a node to a given existing GameObject. Used to apply changes to component instances (including nested elements) 
        /// </summary>
        /// <param name="node"></param>
        /// <param name="nodeObject"></param>
        /// <param name="parentNode"></param>
        /// <param name="figmaImportProcessData"></param>
        private static void ApplyFigmaProperties(Node node, GameObject nodeObject,Node parentNode, FigmaImportProcessData figmaImportProcessData)
        {
            // Apply properties for this node Object (eg characters to text)
            FigmaNodeManager.ApplyUnityComponentPropertiesForNode(nodeObject,node,figmaImportProcessData);
            
            // Apply prototype elements for this node as required (such as buttons etc
            PrototypeFlowManager.ApplyPrototypeFunctionalityToNode(node, nodeObject, figmaImportProcessData);
            
            // Setup transform based on node properties
            NodeTransformManager.ApplyFigmaTransform(nodeObject.transform as RectTransform,node,parentNode,true);
            
            // Apply recursively for all children
            if (node.children == null) return;

            // If this is a substitution, ignore children (as they wont exist)
            if (FigmaNodeManager.NodeIsSubstitution(node, figmaImportProcessData)) return;
            
            // Cycle through each Figma child node data for this node
            foreach (var childNode in node.children)
            {
                var matchingChildGameObject = FindMatchingChildForFigmaNode(childNode, nodeObject.transform);
                if (matchingChildGameObject != null)
                {
                    ApplyFigmaProperties(childNode, matchingChildGameObject, node, figmaImportProcessData);

                }
                else
                    Debug.Log($"Applying properties - Could not find child object {childNode.id} name {childNode.name} from parent node id {node.id} in parent transform {nodeObject.name}");
            }
        }

        /// <summary>
        /// Finds a child node with a specific figma node id
        /// </summary>
        /// <param name="childNode"></param>
        /// <param name="parentNodeTransform"></param>
        /// <returns></returns>
        private static GameObject FindMatchingChildForFigmaNode(Node childNode, Transform parentNodeTransform)
        {
            var nodeTransformChildrenCount =parentNodeTransform.childCount;
            var childNodeIdComponentRefId = childNode.id.Split(';').Last();
                    
            for (var childTestIndex = 0; childTestIndex < nodeTransformChildrenCount; childTestIndex++)
            {
                var childTransform = parentNodeTransform.transform.GetChild(childTestIndex);
                var childNodeObject = childTransform.GetComponent<FigmaNodeObject>();
                if (childNodeObject.NodeId == childNodeIdComponentRefId)
                {
                    return childTransform.gameObject;
                }
            }

            return null;
        }
    }
}
