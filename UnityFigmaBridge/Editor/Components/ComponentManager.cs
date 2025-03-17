using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
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
            // Remove from pages
            foreach (var pagePrefab in figmaImportProcessData.PagePrefabs.Where(pagePrefab => pagePrefab!=null))
            {
                RemoveTemporaryNodeComponents(pagePrefab);
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
        public static void GenerateComponentAssetFromNode(Node node, Node parentNode, GameObject nodeGameObject, FigmaImportProcessData figmaImportProcessData)
        {
            // If this is part of a component set (eg a variant), append the name of the component set to the component name
            var nodeName=parentNode is { type: NodeType.COMPONENT_SET } ? $"{parentNode.name}-{node.name}" : node.name;
            var componentCount = figmaImportProcessData.ComponentData.GetComponentNameCount(nodeName);
            var prefabAssetPath = FigmaPaths.GetPathForComponentPrefab(nodeName,componentCount);
            figmaImportProcessData.ComponentData.IncrementComponentNameCount(nodeName,1);
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
            InstantiateComponentsInPrefabSet(figmaImportProcessData.ComponentData.AllComponentPrefabs,figmaImportProcessData,"Connecting nested components");
            // Instantiate components within screens
            InstantiateComponentsInPrefabSet(figmaImportProcessData.ScreenPrefabs,figmaImportProcessData,"Connecting screen components");
            // Instantiate components within pages
            InstantiateComponentsInPrefabSet(figmaImportProcessData.PagePrefabs,figmaImportProcessData,"Connecting page components");
        }

        /// <summary>
        /// Connects a set of components and provides feedback on progress
        /// </summary>
        /// <param name="prefabSet"></param>
        /// <param name="figmaImportProcessData"></param>
        /// <param name="progressTitle"></param>
        private static void InstantiateComponentsInPrefabSet(List<GameObject> prefabSet,FigmaImportProcessData figmaImportProcessData, string progressTitle)
        {
            for (var i = 0; i < prefabSet.Count; i++)
            {
                var targetPrefab = prefabSet[i];
                if (targetPrefab==null) continue;
                EditorUtility.DisplayProgressBar(UnityFigmaBridgeImporter.PROGRESS_BOX_TITLE, $"{progressTitle} {i}/{prefabSet.Count} ", (float)i/prefabSet.Count);
                InstantiateComponentPrefabs(targetPrefab, figmaImportProcessData);
            }
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
            
            // Filter out any that are replacements in prefab instances (we want to skip these)
            var targetPlaceHolderComponents = new List<FigmaComponentNodeMarker>();
            foreach (var t in allPlaceholderComponents)
            {
                var prefabInstanceRoot=PrefabUtility.GetNearestPrefabInstanceRoot(t.gameObject);
                if (prefabInstanceRoot==null) targetPlaceHolderComponents.Add(t);
                else
                {
                    // Debug.Log($"Prefab instance root found for object {t.gameObject.name}, skipping");
                }
            }


            // Track a list of placed and modified components, to allow effective saving
            var modifiedPrefabInstances = new List<GameObject>();
            foreach (var placeholder in targetPlaceHolderComponents)
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
                var nodeData = figmaImportProcessData.NodeLookupDictionary[placeholder.NodeId]; 
                // Get parent node data for the original node
                var parentNodeData =  figmaImportProcessData.NodeLookupDictionary[placeholder.ParentNodeId];
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
            // There are two cases that this would be a substitution - either the component instance itself,
            // or the original component node could have be a substitution (would have an image component that is NOT a FigmaImage)
            // TODO - Optimise and remove need for Image component check
            var existingImageComponent = nodeObject.GetComponent<Image>();
            var isSubstitution = FigmaNodeManager.NodeIsSubstitution(node, figmaImportProcessData) || (existingImageComponent != null && existingImageComponent is not FigmaImage);
            if (!isSubstitution)
            {
                try
                {
                    // Apply properties for this node Object (eg characters to text). Not needed if this is a substitution
                    FigmaNodeManager.ApplyUnityComponentPropertiesForNode(nodeObject, node, figmaImportProcessData);
                }
                catch (Exception e)
                {
                    Debug.LogWarning(
                        $"Exception applying properties for node '{FigmaDataUtils.GetFullPathForNode(node, figmaImportProcessData.SourceFile)}' - {e}",
                        nodeObject);
                }
            }

            // Apply prototype elements for this node as required (such as buttons etc
            PrototypeFlowManager.ApplyPrototypeFunctionalityToNode(node, nodeObject, figmaImportProcessData);
            
            // Apply layout properties to this node as required (eg vertical layout groups etc)
            FigmaLayoutManager.ApplyLayoutPropertiesForNode(nodeObject,node,figmaImportProcessData,out var scrollContentGameObject);
            
            // If this is a substitution, ignore children (as they wont exist) and apply absolute bounds transform (as rotation already applied)
            if (isSubstitution)
            {
                NodeTransformManager.ApplyAbsoluteBoundsFigmaTransform(nodeObject.transform as RectTransform,node,parentNode,true);
                return;
            }
            
            // Setup transform based on node properties
            NodeTransformManager.ApplyFigmaTransform(nodeObject.transform as RectTransform,node,parentNode,true);
            
            // Apply recursively for all children
            if (node.children == null) return;
            
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
                if (childNodeObject != null && childNodeObject.NodeId == childNodeIdComponentRefId)
                {
                    return childTransform.gameObject;
                }
            }

            return null;
        }
    }
}
