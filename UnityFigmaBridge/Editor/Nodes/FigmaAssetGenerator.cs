using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityFigmaBridge.Editor.Components;
using UnityFigmaBridge.Editor.FigmaApi;
using UnityFigmaBridge.Editor.PrototypeFlow;
using UnityFigmaBridge.Editor.Utils;
using UnityFigmaBridge.Runtime.UI;
using Object = UnityEngine.Object;

namespace UnityFigmaBridge.Editor.Nodes
{
    /// <summary>
    /// Root generator for figma file/document. Constructs a native UI system and all assets
    /// </summary>
    public static class FigmaAssetGenerator
    {
        /// <summary>
        /// Builds a native unity UI given input figma data
        /// </summary>
        /// <param name="rootCanvas">Root canvas for generation</param>
        /// <param name="figmaImportProcessData"></param>
        public static void BuildFigmaFile(Canvas rootCanvas, FigmaImportProcessData figmaImportProcessData)
        {
            // Save prefab for each page
            var downloadPageIdList = figmaImportProcessData.SelectedPagesForImport.Select(p => p.id).ToList();
            
            // Cycle through all pages and create
            var createdPages = new List<(Node,GameObject)>();
            foreach (var figmaCanvasNode in figmaImportProcessData.SourceFile.document.children)
            {
                bool includedPageObject = downloadPageIdList.Contains(figmaCanvasNode.id);
                EditorUtility.DisplayProgressBar(UnityFigmaBridgeImporter.PROGRESS_BOX_TITLE, $"Generating Page {figmaCanvasNode.name} ", 0);
                var pageGameObject = BuildFigmaPage(figmaCanvasNode, rootCanvas.transform as RectTransform, figmaImportProcessData,includedPageObject);
                createdPages.Add((figmaCanvasNode,pageGameObject));
            }
            
            // Save prefab for each page
            for (var i = 0; i < createdPages.Count; i++)
            {
                if (!downloadPageIdList.Contains(createdPages[i].Item1.id)) continue;
               SaveFigmaPageAsPrefab(createdPages[i].Item1, createdPages[i].Item2,figmaImportProcessData);
            }
            
            // Destroy all page objects
            foreach (var createdPage in createdPages)
            {
                Object.DestroyImmediate(createdPage.Item2);
            }
            
            // Instantiate all components
            ComponentManager.InstantiateAllComponentPrefabs(figmaImportProcessData);

            // Remove all temporary components that were created along the way
            ComponentManager.RemoveAllTemporaryNodeComponents(figmaImportProcessData);
            
            // At the very end, we want to apply figmaNode behaviour where required
            BehaviourBindingManager.BindBehaviours(figmaImportProcessData);
        }


        /// <summary>
        /// Builds an individual page (Canvas object in Figma API)
        /// </summary>
        /// <param name="pageNode"></param>
        /// <param name="parentTransform"></param>
        /// <param name="figmaImportProcessData"></param>
        /// <param name="includedPageObject"></param>
        /// <returns></returns>
        private static GameObject BuildFigmaPage(Node pageNode, RectTransform parentTransform,
            FigmaImportProcessData figmaImportProcessData, bool includedPageObject)
        {
           
            var pageGameObject = new GameObject(pageNode.name, typeof(RectTransform));
            var pageTransform = pageGameObject.transform as RectTransform;
            pageTransform.SetParent(parentTransform, false);
            
            // Setup transform for page
            pageTransform.pivot = new Vector2(0, 1);
            pageTransform.anchorMin = pageTransform.anchorMax = new Vector2(0, 1); // Top left
            
            // Generate all child nodes. 
            foreach (var childNode in pageNode.children)
            {
               
                if (CheckNodeValidForGeneration(childNode,figmaImportProcessData))
                    BuildFigmaNode(childNode, pageTransform, pageNode, 0, figmaImportProcessData, includedPageObject, false );
            }

            return pageGameObject;
        }

        private static bool CheckNodeValidForGeneration(Node node, FigmaImportProcessData figmaImportProcessData)
        {
            return figmaImportProcessData.Settings.GenerateNodesMarkedForExport || node.exportSettings == null || node.exportSettings.Length == 0;
        }


        /// <summary>
        /// Build an individual Figma Node - this can be of any type, eg FRAME, RECTANGLE, ELLIPSE, TEXT. Frames at depth 0 are treated as screens 
        /// </summary>
        /// <param name="figmaNode">The source figma node</param>
        /// <param name="parentTransform">The parent transform figmaNode</param>
        /// <param name="parentFigmaNode">The parent figma node</param>
        /// <param name="nodeRecursionDepth">Depth of recursion</param>
        /// <param name="figmaImportProcessData"></param>
        /// <param name="includedPageObject"></param>
        /// <param name="withinComponentDefinition"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        private static GameObject BuildFigmaNode(Node figmaNode, RectTransform parentTransform,  Node parentFigmaNode,
            int nodeRecursionDepth, FigmaImportProcessData figmaImportProcessData,bool includedPageObject, bool withinComponentDefinition)
        {

            // Create a gameObject for this figma node and parent to parent transform
            var nodeGameObject = new GameObject(figmaNode.name, typeof(RectTransform));
            nodeGameObject.transform.SetParent(parentTransform, false);
            var nodeRectTransform = nodeGameObject.transform as RectTransform;
            
            // In some cases we want nodes to be substituted a server-rendered bitmap. Check to see if this is needed
            var matchingServerRenderEntry = figmaImportProcessData.ServerRenderNodes.FirstOrDefault((testNode) => testNode.SourceNode.id == figmaNode.id);
            
            // Apply transform. For server render entries, use absolute bounding box
            if (matchingServerRenderEntry!=null) NodeTransformManager.ApplyAbsoluteBoundsFigmaTransform(nodeRectTransform, figmaNode, parentFigmaNode,nodeRecursionDepth >0);
            else NodeTransformManager.ApplyFigmaTransform(nodeRectTransform, figmaNode, parentFigmaNode,nodeRecursionDepth >0);
            
            // Add on a figmaNode to store the reference to the FIGMA figmaNode id
            nodeGameObject.AddComponent<FigmaNodeObject>().NodeId=figmaNode.id;

            // If this is a Figma mask object we'll add a mask component (but dont render) 
            if (figmaNode.isMask)
            {
                var mask=nodeGameObject.AddComponent<Mask>();
                mask.showMaskGraphic = false;
            }
            
            // For component instances, we want to check if there is an existing definition
            // If so, we wont create the full node, but mark it with a "component node marker" component
            // At a later stage, we'll replace with an instantiated prefab and apply properties
            if (figmaNode.type == NodeType.INSTANCE)
            {
                if (!figmaImportProcessData.ComponentData.MissingComponentDefinitionsList.Contains(figmaNode.componentId))
                {
                    // Attach a placeholder transform and component which will get replaced on second pass
                   nodeGameObject.AddComponent<FigmaComponentNodeMarker>().Initialise(figmaNode.id, parentFigmaNode.id, figmaNode.componentId);
                   return nodeGameObject;
                }
                // Otherwise we assume we are missing the definition, so just create as normal
            }

            if (figmaNode.type == NodeType.COMPONENT) withinComponentDefinition = true;
            
            if (matchingServerRenderEntry!=null)
            {
                // Attach a simple image node (no need for custom renderer)
                nodeGameObject.AddComponent<Image>().sprite = AssetDatabase.LoadAssetAtPath<Sprite>(FigmaPaths.GetPathForServerRenderedImage(figmaNode.id,figmaImportProcessData.ServerRenderNodes));
                
                // This could be a button, so check for prototype functionality
                PrototypeFlowManager.ApplyPrototypeFunctionalityToNode(figmaNode, nodeGameObject, figmaImportProcessData);

                // If this is a component, we want to generate a prefab, to be used to link to instances later
                if (figmaNode.type == NodeType.COMPONENT)
                    ComponentManager.GenerateComponentAssetFromNode(figmaNode, parentFigmaNode, nodeGameObject, figmaImportProcessData);
                
                return nodeGameObject;
            }
            
            // Create any required unity components for this figmaNode. We separate out application of properties to a separate method
            FigmaNodeManager.CreateUnityComponentsForNode(nodeGameObject, figmaNode,figmaImportProcessData);
            
            // Apply properties for this figmaNode
            FigmaNodeManager.ApplyUnityComponentPropertiesForNode(nodeGameObject,figmaNode,figmaImportProcessData);
            
            // Apply effects for this figmaNode (if there is an effects node. Some dont have this (eg SECTION)
            if (figmaNode.effects!=null) EffectManager.ApplyAllFigmaEffectsToUnityNode(nodeGameObject,figmaNode,figmaImportProcessData);
            
            // Apply layout properties to this node as required (eg vertical layout groups etc). This also implements scrolling
            FigmaLayoutManager.ApplyLayoutPropertiesForNode(nodeGameObject,figmaNode,figmaImportProcessData,out var scrollContentGameObject);
            
            // Build children for this node, if they exist
            if (figmaNode.children != null)
            {
                // We'll track any active masking when building child nodes, as masked nodes need to be parented
                Mask activeMaskObject=null;
                foreach (var childNode in figmaNode.children)
                {
                    var childGameObject = BuildFigmaNode(childNode, nodeRectTransform, figmaNode,
                        nodeRecursionDepth + 1, figmaImportProcessData,includedPageObject, withinComponentDefinition);
                    if (childGameObject == null) continue;
                    // Check if this object has a mask component. If so, set as the active mask component
                    var childGameObjectMask = childGameObject.GetComponent<Mask>();
                    if (childGameObjectMask != null) activeMaskObject = childGameObjectMask;
                    else
                    {
                        // If there is a current mask object Parent this object to the current mask object and keep current transition
                        if (activeMaskObject != null) childGameObject.transform.SetParent(activeMaskObject.transform, true);
                        // If there is an active scroll content object (generated by layout properties), parent to that object instead
                        if (scrollContentGameObject!=null) childGameObject.transform.SetParent(scrollContentGameObject.transform, true);
                    }
                }
            }
            
            // For a final step on creation of scroll content. If the node doesnt use layout, we need to resize to fit all the generated children
            if (scrollContentGameObject != null && figmaNode.layoutMode == Node.LayoutMode.NONE)
            {
                // We do this by calculating the merged bounds of all child nodes
                var boundsRect = NodeTransformManager.GetRelativeBoundsForAllChildNodes(figmaNode);
                ((RectTransform)scrollContentGameObject.transform).sizeDelta = new Vector2(boundsRect.xMax, boundsRect.yMax);
            }
            
            // Apply prototype elements for this figmaNode
            // This needs to be done AFTER children as some children will be needed for some button variations
            PrototypeFlowManager.ApplyPrototypeFunctionalityToNode(figmaNode ,nodeGameObject, figmaImportProcessData);

            switch (figmaNode.type)
            {
                // If the parent is either a canvas or section, treat as a flowScreen and create a prefab. Only do this if it's on a generated page
                case NodeType.FRAME:
                    if (includedPageObject && FigmaDataUtils.IsScreenNode(figmaNode,parentFigmaNode))
                    {
                        SaveFigmaScreenAsPrefab(figmaNode, parentFigmaNode, nodeRectTransform, figmaImportProcessData);
                    }
                    break;
                // For the originally defined components, save as a prefab to be used for later instantiation
                case NodeType.COMPONENT:
                    ComponentManager.GenerateComponentAssetFromNode(figmaNode, parentFigmaNode, nodeGameObject, figmaImportProcessData);
                    break;
                case NodeType.SECTION:
                    RegisterFigmaSection(figmaNode, figmaImportProcessData);
                    break;
            }

            // If this node is visible, mark the game object is inactive
            if (!figmaNode.visible) nodeGameObject.SetActive(false);
            
            return nodeGameObject;
        }


        /// <summary>
        /// Create a flowScreen prefab from a generated figma asset
        /// </summary>
        /// <param name="node"></param>
        /// <param name="parentNode"></param>
        /// <param name="screenRectTransform"></param>
        /// <param name="figmaImportProcessData"></param>
        private static void SaveFigmaScreenAsPrefab(Node node, Node parentNode,RectTransform screenRectTransform, FigmaImportProcessData figmaImportProcessData)
        {
            var screenNameCount = figmaImportProcessData.ScreenPrefabNameCounter.TryGetValue(node.name, out var value)
                ? value : 0;
            
            // Increment count to ensure no naming collisions
            figmaImportProcessData.ScreenPrefabNameCounter[node.name] = screenNameCount + 1;
            
            // We want prefab to be stored with a default position, so reset and restore
            var current = screenRectTransform.anchoredPosition;
            screenRectTransform.anchoredPosition = Vector2.zero;
            // Write prefab
            var screenPrefab = PrefabUtility.SaveAsPrefabAssetAndConnect(screenRectTransform.gameObject,
                    FigmaPaths.GetPathForScreenPrefab(node,screenNameCount), InteractionMode.UserAction);
            // Restore original position
            screenRectTransform.anchoredPosition = current;

            // If we are building the prototype flow, add this to the current flowScreen controller
            if (figmaImportProcessData.Settings.BuildPrototypeFlow)
            {
                figmaImportProcessData.PrototypeFlowController.RegisterFigmaScreen(new FigmaFlowScreen
                {
                    FigmaScreenPrefab = screenPrefab,
                    FigmaNodeId = node.id,
                    FigmaScreenName = FigmaPaths.GetFileNameForNode(node, screenNameCount),
                    // Store the section that this is part of (if applicable)
                    ParentSectionNodeId = parentNode is { type: NodeType.SECTION } ? parentNode.id : string.Empty
                });
            }
            
            figmaImportProcessData.ScreenPrefabs.Add(screenPrefab);
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="node"></param>
        /// <param name="pageGameObject"></param>
        /// <param name="figmaImportProcessData"></param>
        private static void SaveFigmaPageAsPrefab(Node node, GameObject pageGameObject, FigmaImportProcessData figmaImportProcessData)
        {
           
            var pageNameCount = figmaImportProcessData.PagePrefabNameCounter.TryGetValue(node.name, out var value)
                ? value : 0;
            
            // Increment count to ensure no naming collisions
            figmaImportProcessData.PagePrefabNameCounter[node.name] = pageNameCount + 1;

            var pagePrefab = PrefabUtility.SaveAsPrefabAssetAndConnect(pageGameObject,
                FigmaPaths.GetPathForPagePrefab(node,pageNameCount),InteractionMode.UserAction);
            figmaImportProcessData.PagePrefabs.Add(pagePrefab);
        }





        /// <summary>
        /// Registers a figma section. This is needed for flow controller to properly transition between sections
        /// </summary>
        /// <param name="node"></param>
        /// <param name="figmaImportProcessData"></param>
        private static void RegisterFigmaSection(Node node, FigmaImportProcessData figmaImportProcessData)
        {
            if (figmaImportProcessData.Settings.BuildPrototypeFlow)
            {
                
                // We want to find the default start point for this section
                var prototypeFlowStartNodeId = string.Empty;
                var prototypeFlowStartNodeName = string.Empty;
                // Search through all start points and see if they are a child of this section
                foreach (var flowStartId in figmaImportProcessData.PrototypeFlowStartPoints)
                {
                    var matchingNode = FigmaDataUtils.GetFigmaNodeInChildren(node, flowStartId);
                    if (matchingNode == null) continue;
                    // We've found a match
                    prototypeFlowStartNodeId = flowStartId;
                    prototypeFlowStartNodeName = matchingNode.name;
                }
                
                figmaImportProcessData.PrototypeFlowController.RegisterFigmaSection(new FigmaSection
                {
                    FigmaNodeId = node.id,
                    FigmaPrototypeFlowStartNodeId = prototypeFlowStartNodeId,
                    FigmaPrototypeFlowStartNodeName= prototypeFlowStartNodeName,
                    FigmaNodeName = node.name,
                });
            }
        }
    }

}