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
        /// Retrieves and returns the specified component if it already exists. If it does not exist, it is added and returned
        /// </summary>
        /// <param name="T"></param>
        /// <param name="gameObject"></param>
        static T GetOrAddComponent<T>(GameObject gameObject) where T : UnityEngine.Component
        {
            T component = gameObject.GetComponent<T>();
            if (component == null) component = gameObject.AddComponent<T>() as T;
            return component;
        }

        /// <summary>
        /// Applies layout properties for a given node to a gameObject, using Vertical/Horizontal layout groups
        /// </summary>
        /// <param name="nodeGameObject"></param>
        /// <param name="node"></param>
        /// <param name="figmaImportProcessData"></param>
        /// <param name="scrollContentGameObject">Generated scroll content object (if generated)</param>
        public static void ApplyLayoutPropertiesForNode( GameObject nodeGameObject,Node node,
            FigmaImportProcessData figmaImportProcessData,out GameObject scrollContentGameObject)
        {
            // Depending on whether scrolling is applied, we may want to add layout to this object or to the content
            // holder
            
            var targetLayoutObject = nodeGameObject;
            scrollContentGameObject = null;
            
            // Check scrolling requirements
            var implementScrolling = node.type == NodeType.FRAME && node.overflowDirection != Node.OverflowDirection.NONE;
            if (implementScrolling)
            {
                // This Frame implements scrolling, so we need to add in appropriate functionality
                
                // Add in a rect mask to implement clipping
                if (node.clipsContent) GetOrAddComponent<RectMask2D>(nodeGameObject);

                // Create the content clip and parent to this object
                scrollContentGameObject = new GameObject($"{node.name}_ScrollContent", typeof(RectTransform));
                var scrollContentRectTransform = scrollContentGameObject.transform as RectTransform;
                scrollContentRectTransform.pivot = new Vector2(0, 1);
                scrollContentRectTransform.anchorMin = scrollContentRectTransform.anchorMax =new Vector2(0,1);
                scrollContentRectTransform.anchoredPosition=Vector2.zero;
                scrollContentRectTransform.SetParent(nodeGameObject.transform, false);
                
                var scrollRectComponent = GetOrAddComponent<ScrollRect>(nodeGameObject);
                scrollRectComponent.content = scrollContentGameObject.transform as RectTransform;
                scrollRectComponent.horizontal =
                    node.overflowDirection is Node.OverflowDirection.HORIZONTAL_SCROLLING 
                        or Node.OverflowDirection.HORIZONTAL_AND_VERTICAL_SCROLLING;

                scrollRectComponent.vertical =
                    node.overflowDirection is Node.OverflowDirection.VERTICAL_SCROLLING 
                        or Node.OverflowDirection.HORIZONTAL_AND_VERTICAL_SCROLLING;


                // If using layout, we need to use content size fitter to ensure proper sizing for child components
                if (node.layoutMode != Node.LayoutMode.NONE)
                {
                    var contentSizeFitter = GetOrAddComponent<ContentSizeFitter>(scrollContentGameObject);
                    contentSizeFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                    contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                }

                // Apply layout to this content clip
                targetLayoutObject = scrollContentGameObject;
            }
            
            
            // Ignore if layout mode is NONE
            if (node.layoutMode == Node.LayoutMode.NONE) return;
            
            // Remove an existing layout group if it exists
            var existingLayoutGroup = targetLayoutObject.GetComponent<HorizontalOrVerticalLayoutGroup>();
            if (existingLayoutGroup!=null) Object.DestroyImmediate(existingLayoutGroup);
            
            HorizontalOrVerticalLayoutGroup layoutGroup = null;
            
            switch (node.layoutMode)
            {
                case Node.LayoutMode.VERTICAL:
                    layoutGroup=GetOrAddComponent<VerticalLayoutGroup>(targetLayoutObject);
                    break;
                case Node.LayoutMode.HORIZONTAL:
                    layoutGroup=GetOrAddComponent<HorizontalLayoutGroup>(targetLayoutObject);
                    break;
            }
            layoutGroup.padding = new RectOffset(Mathf.RoundToInt(node.paddingLeft), Mathf.RoundToInt(node.paddingRight),
                Mathf.RoundToInt(node.paddingTop), Mathf.RoundToInt(node.paddingBottom));
            layoutGroup.spacing = node.itemSpacing;
            
        }
    }
}