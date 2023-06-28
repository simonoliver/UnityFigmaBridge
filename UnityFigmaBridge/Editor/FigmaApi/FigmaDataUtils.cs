using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnityFigmaBridge.Editor.FigmaApi
{
    /// <summary>
    /// Utilities to convert from Figma data types to Unity data types, and query Figma Data structures
    /// </summary>
    public static class FigmaDataUtils
    {
        
        /// <summary>
        /// Converts from Figma Paint Fill Color to Unity color
        /// </summary>
        /// <param name="paint"></param>
        /// <returns></returns>
        public static UnityEngine.Color GetUnityFillColor(Paint paint)
        {
            // Make sure 
            if(paint != null && !paint.visible) return new UnityEngine.Color(0,0,0,0);
            return paint?.color == null ? new UnityEngine.Color(1,1,1,paint?.opacity ?? 1) : new UnityEngine.Color(paint.color.r, paint.color.g, paint.color.b, paint.color.a*paint.opacity);
        }

        /// <summary>
        /// Create a Unity Gradient from Figma gradient
        /// </summary>
        /// <param name="fill"></param>
        /// <returns></returns>
        public static Gradient ToUnityGradient(Paint fill)
        {
            var figmaGradientStops = fill.gradientStops;
            
            // Create array of keys for gradient color and alpha
            var unityColorKeys = new GradientColorKey[figmaGradientStops.Length];
            var unityAlphaKeys = new GradientAlphaKey[figmaGradientStops.Length];

            // Cycle through figma gradient and convert keys to Unity
            for (var i = 0; i < figmaGradientStops.Length; i++)
            {
                unityColorKeys[i].color = ToUnityColor(figmaGradientStops[i].color);
                unityColorKeys[i].time = figmaGradientStops[i].position;
                unityAlphaKeys[i].alpha = figmaGradientStops[i].color.a;
                unityAlphaKeys[i].time=figmaGradientStops[i].position;
            }

            // Create new Unity gradient
            var gradient = new Gradient
            {
                mode = GradientMode.Blend
            };
            gradient.SetKeys(unityColorKeys, unityAlphaKeys);
            return gradient;
        }
        
        /// <summary>
        /// Convert Figma Vector2 to Unity Vector2
        /// </summary>
        /// <param name="vector"></param>
        /// <returns></returns>
        public static Vector2 ToUnityVector(Vector vector)
        {
            return new Vector2(vector.x, vector.y);
        }

        /// <summary>
        /// Convert Figma Color to Unity Color
        /// </summary>
        /// <param name="color"></param>
        /// <returns></returns>
        public static UnityEngine.Color ToUnityColor(Color color)
        {
            return new UnityEngine.Color(color.r, color.g, color.b, color.a);
        }

        /// <summary>
        /// Convert to array of Unity Vector3
        /// </summary>
        /// <param name="inputArray"></param>
        /// <returns></returns>
        public static Vector3[] ToUnityVector3Array(float[,] inputArray)
        {
            var length=inputArray.GetLength(0);
            var outputArray = new Vector3[length];
            for (var i = 0; i < length; i++)
            {
                outputArray[i] = new Vector3(inputArray[i,0], inputArray[i,1], inputArray[i,2]);
            }
            return outputArray;
        }
        
        
        /// <summary>
        /// Create a fast-lookup dictionary
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public static Dictionary<string, Node> BuildNodeLookupDictionary(FigmaFile file)
        {
            var dictionary = new Dictionary<string, Node>();
            PopulateDictionaryWithNodes(dictionary, file.document);
            return dictionary;
        }

        /// <summary>
        /// Recursively populate dictionary with all nodes in a figma file
        /// </summary>
        /// <param name="dictionary"></param>
        /// <param name="node"></param>
        private static void PopulateDictionaryWithNodes(Dictionary<string, Node> dictionary, Node node)
        {
            dictionary[node.id] = node;
            if (node.children == null) return;
            foreach (var childNode in node.children)
            {
                PopulateDictionaryWithNodes(dictionary, childNode);
            }
        }

        /// <summary>
        /// Searches a Figma file to find a specific figmaNode
        /// Note - this is slow, so avoid if possible
        /// </summary>
        /// <param name="file"></param>
        /// <param name="nodeId"></param>
        /// <returns></returns>
        public static Node GetFigmaNodeWithId(FigmaFile file, string nodeId)
        {
            return GetFigmaNodeInChildren(file.document,nodeId);
        }


      

        /// <summary>
        /// Find a specific figmaNode within figma figmaNode tree (recursive)
        /// </summary>
        /// <param name="node"></param>
        /// <param name="nodeId"></param>
        /// <returns></returns>
        public static Node GetFigmaNodeInChildren(Node node,string nodeId)
        {
            if (node.children == null) return null;
            foreach (var childNode in node.children)
            {
                if (childNode.id == nodeId) return childNode;
                var nodeFoundInChildren = GetFigmaNodeInChildren(childNode, nodeId);
                if (nodeFoundInChildren != null) return nodeFoundInChildren;
            }
            // Not found
            return null;
        }

        /// <summary>
        /// Returns the full hierarchical path for a given node in a document - helpful for debugging
        /// </summary>
        /// <param name="node"></param>
        /// <param name="figmaFile"></param>
        /// <returns></returns>
        public static string GetFullPathForNode(Node node,FigmaFile figmaFile)
        {
            var pathStack = new Stack<string>();
            var found=GetPathForNodeRecursive(figmaFile.document, node,pathStack );
            return string.Join("/",pathStack.Reverse());
        }
        
        /// <summary>
        /// Recursively search for a node in a figma file and push/pop to stack to track heirarchy
        /// </summary>
        /// <param name="searchNode"></param>
        /// <param name="targetNode"></param>
        /// <param name="pathStack"></param>
        /// <returns></returns>
        private static bool GetPathForNodeRecursive(Node searchNode, Node targetNode, Stack<string> pathStack)
        {
            pathStack.Push(searchNode.name);
            if (searchNode == targetNode) return true;
            if (searchNode.children != null)
            {
                foreach (var childNode in searchNode.children)
                {
                    if (GetPathForNodeRecursive(childNode, targetNode, pathStack)) return true;
                }
            }
            pathStack.Pop(); // Not found, remove from stack
            return false;
        }
       
        
        
        /// <summary>
        /// Replace any characters that are invlid for saving
        /// </summary>
        /// <param name="NodeId"></param>
        /// <returns></returns>
        public static string ReplaceUnsafeFileCharactersForNodeId (string NodeId)
        {
            return NodeId.Replace(":", "_");
        }


        /// <summary>
        /// Get all figma fills from within a figma file
        /// </summary>
        /// <param name="file"></param>
        /// <param name="downloadPageIdList"></param>
        /// <returns></returns>
        public static List<string> GetAllImageFillIdsFromFile(FigmaFile file, List<string> downloadPageIdList)
        {
            var imageFillIdList = new List<string>();
            foreach (var page in file.document.children)
            {
                var includedPage=downloadPageIdList.Contains(page.id);
                GetAllImageFillIdsForNode(page, imageFillIdList,0,includedPage,false);
            }
            return imageFillIdList;
        }

        /// <summary>
        /// Recursive function looking for image fill IDs for a given figmaNode
        /// </summary>
        /// <param name="node"></param>
        /// <param name="imageFillList"></param>
        /// <param name="recursiveDepth"></param>
        /// <param name="includedPage"></param>
        /// <param name="withinComponentDefinition"></param>
        private static void GetAllImageFillIdsForNode(Node node, List<string> imageFillList,int recursiveDepth,
            bool includedPage, bool withinComponentDefinition )
        {
            // We want to ignore random images placed on the root not in frames as they might be simple reference images
            var ignoreNodeFill = recursiveDepth <=1 && node.type != NodeType.FRAME && node.type != NodeType.COMPONENT;
            // We'll also ignore if this page is not included in the download list and we're not within a component definition
            if (!includedPage && !withinComponentDefinition) ignoreNodeFill = true;
            if (node.fills != null && !ignoreNodeFill)
            {
                foreach (var fill in node.fills)
                {
                    if (fill == null || fill.type != Paint.PaintType.IMAGE) continue;
                    if (string.IsNullOrEmpty(fill.imageRef)) continue;
                    var imageRefId = fill.imageRef;
                    if (!imageFillList.Contains(imageRefId)) imageFillList.Add(imageRefId);
                }
            }

            // If this is a component, mark it as so for all children (to ensure included)
            if (node.type == NodeType.COMPONENT) withinComponentDefinition = true;
            
            //  Recursively cycle through all children
            if (node.children == null) return;
            foreach (var childNode in node.children)
                GetAllImageFillIdsForNode(childNode, imageFillList,recursiveDepth+1,includedPage,withinComponentDefinition);
            
        }
        
        /// <summary>
        /// Recursively search for nodes of a specific type
        /// </summary>
        /// <param name="node"></param>
        /// <param name="nodeType"></param>
        /// <param name="nodeList"></param>
        /// <param name="nodeDepth"></param>
        public static void FindAllNodesOfType(Node node,NodeType nodeType,List<Node> nodeList,
            int nodeDepth)
        {
            if (node.type == nodeType)  nodeList.Add(node);
            if (node.children == null) return;

            foreach (var childNode in node.children)
                FindAllNodesOfType(childNode, nodeType,nodeList,nodeDepth+1);
        }

        /// <summary>
        /// Finds all components of a specific iD
        /// </summary>
        /// <param name="node"></param>
        /// <param name="componentId"></param>
        /// <param name="nodeList"></param>
        /// <param name="nodeDepth"></param>
        private static void FindAllComponentInstances(Node node,string componentId,List<Node> nodeList, int nodeDepth)
        {
            if (node.type == NodeType.INSTANCE && node.componentId==componentId)  nodeList.Add(node);
            if (node.children == null) return;

            foreach (var childNode in node.children)
                FindAllComponentInstances(childNode, componentId,nodeList,nodeDepth+1);
        }


        /// <summary>
        /// Find all nodes within a document that we need to render server-side
        /// </summary>
        /// <param name="file">Figma document</param>
        /// <param name="missingComponentIds"></param>
        /// <param name="downloadPageIdList"></param>
        /// <returns>List of figmaNode IDs to replace</returns>
        public static List<ServerRenderNodeData> FindAllServerRenderNodesInFile(FigmaFile file,
            List<string> missingComponentIds, List<string> downloadPageIdList)
        {
            var renderSubstitutionNodeList = new List<ServerRenderNodeData>();
            // Process each canvas
            foreach (var page in file.document.children)
            {
                var isSelectedPage=downloadPageIdList.Contains(page.id);
                AddRenderSubstitutionsForFigmaNode(page, renderSubstitutionNodeList, 0,missingComponentIds,isSelectedPage,false);
            }

            return renderSubstitutionNodeList;
        }

        /// <summary>
        /// Recursively search a given figmaNode to identify those for server rendering
        /// </summary>
        /// <param name="figmaNode"></param>
        /// <param name="substitutionNodeList"></param>
        /// <param name="recursiveNodeDepth"></param>
        /// <param name="missingComponentIds"></param>
        /// <param name="isSelectedPage"></param>
        /// <param name="withinComponentDefinition"></param>
        private static void AddRenderSubstitutionsForFigmaNode(Node figmaNode,
            List<ServerRenderNodeData> substitutionNodeList, int recursiveNodeDepth, List<string> missingComponentIds,
            bool isSelectedPage,bool withinComponentDefinition)
        {
            // Instances will already be defined by original prefab (eg that may already be rendered). Also dont attempt to render invisible nodes
            if (figmaNode.type == NodeType.INSTANCE && !missingComponentIds.Contains(figmaNode.componentId) || !figmaNode.visible) return;
            
            // Top level frames should be checked for server-side rendering
            if ((isSelectedPage || withinComponentDefinition) && recursiveNodeDepth==1 && figmaNode.exportSettings!=null && figmaNode.exportSettings.Length > 0)
            {
                Debug.Log($"Found figmaNode with export! Node {figmaNode.name}");
                substitutionNodeList.Add( new ServerRenderNodeData
                {
                    RenderType = ServerRenderType.Export,
                    SourceNode = figmaNode
                });
                return;
            }

            
            if ((isSelectedPage || withinComponentDefinition) && GetNodeSubstitutionStatus(figmaNode,recursiveNodeDepth))
            {
                substitutionNodeList.Add( new ServerRenderNodeData
                {
                    RenderType = ServerRenderType.Substitution,
                    SourceNode = figmaNode
                });
                return;
            }
            
            if (figmaNode.children == null) return;

            // If this is a component, we want to ensure we include all server render components within (even if the page is ignored)
            if (figmaNode.type == NodeType.COMPONENT) withinComponentDefinition = true;
            
            foreach (var childNode in figmaNode.children)
                AddRenderSubstitutionsForFigmaNode(childNode, substitutionNodeList,recursiveNodeDepth+1,missingComponentIds,isSelectedPage,withinComponentDefinition);
            
        }

        /// <summary>
        /// Defines whether a given figma node should be substituted with server-side render
        /// </summary>
        /// <param name="node"></param>
        /// <param name="recursiveNodeDepth"></param>
        /// <returns></returns>
        private static bool GetNodeSubstitutionStatus(Node node,int recursiveNodeDepth)
        {
            // We never substitute screens or pages
            if (node.type == NodeType.CANVAS) return false;
            if (recursiveNodeDepth <=1 && node.type== NodeType.FRAME) return false;
            
            // If a given node has the word "render", mark for rendering
            if (node.name.ToLower().Contains("render")) return true;

            // Some types we always render server-side. This may change if we support native vector rendering
            switch (node.type)
            {
                case NodeType.VECTOR:
                case NodeType.BOOLEAN_OPERATION:
                    return true;
            }

            // The pattern we identify for server side vector rendering is:
            // * At least one sub nodes of type vector
            // * Only containing sub nodes of type VECTOR, FRAME, GROUP, COMPONENT, INSTANCE
            var validNodeTypesForVectorRender = new NodeType[] { NodeType.VECTOR, NodeType.GROUP, NodeType.FRAME, NodeType.COMPONENT, NodeType.INSTANCE };
            var nodeTypeCount = new int[validNodeTypesForVectorRender.Length];
            var onlyValidNodeTypesFound =
                GetNodeChildrenExclusivelyOfTypes(node, validNodeTypesForVectorRender, nodeTypeCount);
            
            if (onlyValidNodeTypesFound && nodeTypeCount[0]>0) return true;
            
            return false;
        }

        /// <summary>
        /// Tests whether a given node only has children of a specific type
        /// </summary>
        /// <param name="node"></param>
        /// <param name="nodeTypes"></param>
        /// <param name="nodeTypeCount"></param>
        /// <returns></returns>
        private static bool GetNodeChildrenExclusivelyOfTypes(Node node, NodeType[] nodeTypes,int[] nodeTypeCount)
        {
            // If this doesnt match return false
            if (!nodeTypes.Contains(node.type)) return false;
            
            // Increment count for matching node type for this node
            for (var i = 0; i < nodeTypes.Length; i++)
            {
                if (node.type == nodeTypes[i]) nodeTypeCount[i]++;
            }

            if (node.children == null) return true;
            foreach (var childNode in node.children)
            {
                var isMatching = GetNodeChildrenExclusivelyOfTypes(childNode, nodeTypes, nodeTypeCount);
                if (!isMatching) return false;
            }
            return true;
        }

        /// <summary>
        /// Finds all component IDs that are used in the figma file, that dont have a matching definition
        /// </summary>
        /// <returns></returns>
        public static List<string> FindMissingComponentDefinitions(FigmaFile file)
        {
            return (from componentKeyPair in file.components select componentKeyPair.Key into componentId let foundNode = GetFigmaNodeWithId(file, componentId) where foundNode == null select componentId).ToList();
        }

        /// <summary>
        /// Finds all missing components and 
        /// </summary>
        /// <param name="figmaFile"></param>
        /// <param name="missingComponentDefinitionList"></param>
        public static void ReplaceMissingComponents(FigmaFile figmaFile, List<string> missingComponentDefinitionList)
        {
            foreach (var componentId in missingComponentDefinitionList)
            {
                var allInstances = new List<Node>();
                FindAllComponentInstances(figmaFile.document, componentId, allInstances, 0);
                if (allInstances.Count==0) continue;
                var firstInstance = allInstances[0];
                firstInstance.type = NodeType.COMPONENT;
                // Remap all other instances to use this component
                for (var i = 1; i < allInstances.Count; i++)
                {
                    allInstances[i].componentId = firstInstance.id;
                }
            }
        }

        /// <summary>
        /// Finds Flow Starting Point id, from first page where one found
        /// </summary>
        /// <param name="sourceFile"></param>
        /// <returns></returns>
        public static string FindPrototypeFlowStartScreenId(FigmaFile sourceFile)
        {
            foreach (var canvasNode in sourceFile.document.children)
            {
                if (canvasNode.flowStartingPoints.Length>0) return canvasNode.flowStartingPoints[0].nodeId;
            }

            return string.Empty;
        }

        /// <summary>
        /// Lists all prototype flow starting points in a given Figma file
        /// </summary>
        /// <param name="sourceFile"></param>
        /// <returns></returns>
        public static List<string> GetAllPrototypeFlowStartingPoints(FigmaFile sourceFile)
        {
            var allFlowStartingPoints = new List<string>();
            foreach (var canvasNode in sourceFile.document.children)
            {
                if (canvasNode.flowStartingPoints == null) continue;
                allFlowStartingPoints.AddRange(canvasNode.flowStartingPoints.Select(flowStartingPoint => flowStartingPoint.nodeId));
            }
            return allFlowStartingPoints;
        }
        
        /// <summary>
        /// Lists all Page Nodes in a given Figma file
        /// </summary>
        /// <param name="sourceFile"></param>
        /// <returns></returns>
        public static List<Node> GetPageNodes(FigmaFile sourceFile)
        {
            var pageNodes = new List<Node>();
            foreach (var canvasNode in sourceFile.document.children)
            {
                pageNodes.Add(canvasNode);
            }
            return pageNodes;
        }

        private static void SearchScreenNodes(Node node, Node parentNode, List<Node> screenNodes)
        {
            if (IsScreenNode(node,parentNode))
            {
                screenNodes.Add(node);
            }

            if (node.children == null) return;

            foreach (var childNode in node.children)
            {
                SearchScreenNodes(childNode, node, screenNodes);
            }
        }

        /// <summary>
        /// Lists all Screen Nodes in a given Figma file
        /// </summary>
        /// <param name="sourceFile"></param>
        /// <returns></returns>
        public static List<Node> GetScreenNodes(FigmaFile sourceFile)
        {
            var screenNodes = new List<Node>();
            foreach (var node in sourceFile.document.children)
            {
                SearchScreenNodes(node, null, screenNodes);
            }
            return screenNodes;
        }

        /// <summary>
        /// Check for Node is Screen Node
        /// </summary>
        public static bool IsScreenNode(Node node, Node parentNode)
        {
            if (node.type != NodeType.FRAME) return false;
            if (parentNode == null) return false;
            if (parentNode is { type: NodeType.CANVAS or NodeType.SECTION }) return true;
            return false;
        }
    }
}
