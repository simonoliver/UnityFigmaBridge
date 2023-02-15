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
            return paint?.color == null ? UnityEngine.Color.white : new UnityEngine.Color(paint.color.r, paint.color.g, paint.color.b, paint.color.a*paint.opacity);
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
        /// Searches a Figma file to find a specific figmaNode
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
        private static Node GetFigmaNodeInChildren(Node node,string nodeId)
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
        /// <returns></returns>
        public static List<string> GetAllImageFillIdsFromFile(FigmaFile file)
        {
            var imageFillIdList = new List<string>();
            foreach (var childNode in file.document.children)
                GetAllImageFillIdsForNode(childNode, imageFillIdList,0);
            return imageFillIdList;
        }

        /// <summary>
        /// Recursive function looking for image fill IDs for a given figmaNode
        /// </summary>
        /// <param name="node"></param>
        /// <param name="imageFillList"></param>
        /// <param name="recursiveDepth"></param>
        private static void GetAllImageFillIdsForNode(Node node, List<string> imageFillList,int recursiveDepth)
        {
            // We want to ignore random images placed on the root not in frames as they might be simple reference images
            var ignoreNodeFill = recursiveDepth <=1 && node.type != NodeType.FRAME && node.type != NodeType.COMPONENT;
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
            //  Recursively cycle through all children
            if (node.children == null) return;
            foreach (var childNode in node.children)
                GetAllImageFillIdsForNode(childNode, imageFillList,recursiveDepth+1);
            
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
        /// <returns>List of figmaNode IDs to replace</returns>
        public static List<ServerRenderNodeData> FindAllServerRenderNodesInFile(FigmaFile file,List<string> missingComponentIds)
        {
            var renderSubstitutionNodeList = new List<ServerRenderNodeData>();
            // Process each canvas
            foreach (var canvas in file.document.children)
            {
                AddRenderSubstitutionsForFigmaNode(canvas, renderSubstitutionNodeList, 0,missingComponentIds);
            }

            return renderSubstitutionNodeList;
        }

        /// <summary>
        /// Recursively search a given figmaNode to identify those for server rendering
        /// </summary>
        /// <param name="canvas"></param>
        /// <param name="figmaNode"></param>
        /// <param name="substitutionNodeList"></param>
        private static void AddRenderSubstitutionsForFigmaNode(Node figmaNode, List<ServerRenderNodeData> substitutionNodeList,int recursiveNodeDepth,List<string> missingComponentIds)
        {
            // Instances will already be defined by original prefab (eg that may already be rendered). Also dont attempt to render invisible nodes
            if (figmaNode.type == NodeType.INSTANCE && !missingComponentIds.Contains(figmaNode.componentId) || !figmaNode.visible) return;
            
            // Top level frames should be checked for server-side rendering
            if (recursiveNodeDepth==1 && figmaNode.exportSettings!=null && figmaNode.exportSettings.Length > 0)
            {
                Debug.Log($"Found figmaNode with export! Node {figmaNode.name}");
                substitutionNodeList.Add( new ServerRenderNodeData
                {
                    RenderType = ServerRenderType.Export,
                    SourceNode = figmaNode
                });
                return;
            }
            
            if (GetNodeSubstitutionStatus(figmaNode,recursiveNodeDepth))
            {
                substitutionNodeList.Add( new ServerRenderNodeData
                {
                    RenderType = ServerRenderType.Substitution,
                    SourceNode = figmaNode
                });
                return;
            }
            
            if (figmaNode.children == null) return;

            foreach (var childNode in figmaNode.children)
                AddRenderSubstitutionsForFigmaNode(childNode, substitutionNodeList,recursiveNodeDepth+1,missingComponentIds);
            
        }

        /// <summary>
        /// Defines whether a given figma node should be substituted with server-side render
        /// </summary>
        /// <param name="node"></param>
        /// <param name="resursiveNodeDepth"></param>
        /// <returns></returns>
        private static bool GetNodeSubstitutionStatus(Node node,int resursiveNodeDepth)
        {
            // We never substitute screens
            if (resursiveNodeDepth == 0) return false;
            
            // If a given node has the word "render", mark for rendering
            if (node.name.ToLower().Contains("render")) return true;
            
            // Right now Vector type is the only one we always render server-side. This may change if we support native vector rendering
            if (node.type == NodeType.VECTOR) return true;
            
            // If ALL children are of type vector (eg broken up SVGs) then return true. This prevents multiple bitmaps being rendered
            if (GetNodeChildrenExclusivelyOfType(node, NodeType.VECTOR)) return true;
            
            return false;
        }

        /// <summary>
        /// Tests whether a given node only has children of a specific type
        /// </summary>
        /// <param name="node"></param>
        /// <param name="nodeType"></param>
        /// <returns></returns>
        private static bool GetNodeChildrenExclusivelyOfType(Node node, NodeType nodeType)
        {
            if (node.children == null) return false;
            var matchingNodeFound = false;
            foreach (var childNode in node.children)
            {
                if (childNode.type == nodeType) matchingNodeFound = true;
                else return false;
            }
            return matchingNodeFound;
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
        /// Finds start id, from first page where one found
        /// </summary>
        /// <param name="sourceFile"></param>
        /// <returns></returns>
        public static string FindPrototypeFlowStartScreenId(FigmaFile sourceFile)
        {
            foreach (var canvasNode in sourceFile.document.children)
            {
                if (!string.IsNullOrEmpty(canvasNode.prototypeStartNodeID)) return canvasNode.prototypeStartNodeID;
            }

            return string.Empty;
        }
    }
}
