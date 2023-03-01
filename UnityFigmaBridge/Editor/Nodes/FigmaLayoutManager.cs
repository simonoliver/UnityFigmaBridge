using UnityEngine;
using UnityEngine.UI;
using UnityFigmaBridge.Editor.FigmaApi;

namespace UnityFigmaBridge.Editor.Nodes
{
    /// <summary>
    /// Manages layout functionality for Figma nodes
    /// </summary>
    public static class FigmaLayoutManager
    {
        /// <summary>
        /// Applies layout properties for a given node to a gameObject, using Vertical/Horizontal layout groups
        /// </summary>
        /// <param name="nodeGameObject"></param>
        /// <param name="node"></param>
        /// <param name="figmaImportProcessData"></param>
        public static void ApplyLayoutPropertiesForNode( GameObject nodeGameObject,Node node,
            FigmaImportProcessData figmaImportProcessData)
        {
            // Ignore if layout mode is NONE
            if (node.layoutMode == Node.LayoutMode.NONE) return;
            
            // Remove an existing layout group if it exists
            var existingLayoutGroup = nodeGameObject.GetComponent<HorizontalOrVerticalLayoutGroup>();
            if (existingLayoutGroup!=null) Object.DestroyImmediate(existingLayoutGroup);
            
            HorizontalOrVerticalLayoutGroup layoutGroup = null;
            
            switch (node.layoutMode)
            {
                case Node.LayoutMode.VERTICAL:
                    layoutGroup=nodeGameObject.AddComponent<VerticalLayoutGroup>();
                    break;
                case Node.LayoutMode.HORIZONTAL:
                    layoutGroup=nodeGameObject.AddComponent<HorizontalLayoutGroup>();
                    break;
            }
            layoutGroup.padding = new RectOffset(Mathf.RoundToInt(node.paddingLeft), Mathf.RoundToInt(node.paddingRight),
                Mathf.RoundToInt(node.paddingTop), Mathf.RoundToInt(node.paddingBottom));
            layoutGroup.spacing = node.itemSpacing;
        }
    }
}