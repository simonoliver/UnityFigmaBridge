using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityFigmaBridge.Editor.FigmaApi;

namespace UnityFigmaBridge.Editor.Nodes
{
    public static class NodeTransformManager
    {
        /// <summary>
        /// Applies the transform of a Figma Node to a Unity RectTransform.
        /// </summary>
        /// <param name="targetRectTransform"></param>
        /// <param name="figmaNode"></param>
        /// <param name="figmaParentNode"></param>
        /// <param name="centerPivot"></param>
        public static void ApplyFigmaTransform(RectTransform targetRectTransform, Node figmaNode, Node figmaParentNode,
            bool centerPivot)
        {
            // Default to top left alignment
            targetRectTransform.anchorMin = targetRectTransform.anchorMax = new Vector2(0, 1);
            targetRectTransform.pivot = new Vector2(0, 1);

            if (figmaNode.relativeTransform != null)
            {
                // Apply the "relativeTransform" from Figma Node for translation and rotation
                targetRectTransform.anchoredPosition = new Vector2(figmaNode.relativeTransform[0, 2],
                    -figmaNode.relativeTransform[1, 2]);
                var rotation = Mathf.Rad2Deg *
                               Mathf.Atan2(-figmaNode.relativeTransform[1, 0], figmaNode.relativeTransform[0, 0]);
                targetRectTransform.localRotation = Quaternion.Euler(0, 0, rotation);
            }

            if (figmaNode.relativeTransform[0,0] < 0)
            {
                //horizontal mirror
                targetRectTransform.localScale = new Vector3(-targetRectTransform.localScale.x, targetRectTransform.localScale.y, targetRectTransform.localScale.z);
                targetRectTransform.localRotation = Quaternion.Euler(targetRectTransform.transform.rotation.eulerAngles.x, targetRectTransform.transform.rotation.eulerAngles.y, targetRectTransform.transform.rotation.eulerAngles.z - 180);
            }
            if (figmaNode.relativeTransform[1,1] < 0)
            {
                //vertical mirror
                targetRectTransform.localScale = new Vector3(targetRectTransform.localScale.x, -targetRectTransform.localScale.y, targetRectTransform.localScale.z);

            }

            // Apply the "size" figmaNode from figma to set size
            targetRectTransform.sizeDelta = new Vector2(figmaNode.size.x, figmaNode.size.y);

            //Add a layout element and set its preferred size
            LayoutElement layoutElement = targetRectTransform.gameObject.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = figmaNode.size.x;
            layoutElement.preferredHeight = figmaNode.size.y;

            layoutElement.minHeight = figmaNode.absoluteBoundingBox.height;
            layoutElement.minWidth = figmaNode.absoluteBoundingBox.width;

            // Apply constraints
            // Groups in Figma dont have their own constraints, so to setup effectively, we need to use the first child's constraints
            var useFirstChildForConstraints = figmaNode.type == NodeType.GROUP && figmaNode.children != null &&
                                              figmaNode.children.Length > 0;
            var constraintsSourceNode = useFirstChildForConstraints ? figmaNode.children[0] : figmaNode;

            // Some nodes will not have a constraints node (eg SECTION nodes)
            if (constraintsSourceNode.constraints!=null) ApplyFigmaConstraints(targetRectTransform, constraintsSourceNode, figmaParentNode);
            
            // We'll also use these properties to apply pivot after, where required
            // We disable center pivot for Text nodes, as this creates behaviour different from Figma when autosizing
            if (figmaNode.type==NodeType.TEXT) centerPivot = false;
            if (centerPivot) SetPivot(targetRectTransform, new Vector2(0.5f, 0.5f));
        }
    
        /// <summary>
        /// Applies constraints to a given RectTransform based on a given Figma Node
        /// </summary>
        /// <param name="targetRectTransform"></param>
        /// <param name="figmaNode"></param>
        /// <param name="figmaParentNode"></param>
        private static void ApplyFigmaConstraints(RectTransform targetRectTransform, Node figmaNode,Node figmaParentNode)
        {
             // Setup anchor positions 
            (targetRectTransform.anchorMin, targetRectTransform.anchorMax) = AnchorPositionsForFigmaConstraints(figmaNode.constraints);
            
            // We'll need to use the size of the parent node to determine anchor position
            var parentNodeSize = figmaParentNode.size != null ? figmaParentNode.size : new Vector { x = 0, y = 0 };
    
            // TODO - Implement SCALE constraint
            
            // Modify anchor position according to constraint
            var anchoredPosition = targetRectTransform.anchoredPosition;

            anchoredPosition.x += figmaNode.constraints.horizontal switch
            {
                LayoutConstraint.HorizontalLayoutConstraint.CENTER => -parentNodeSize.x * 0.5f,
                LayoutConstraint.HorizontalLayoutConstraint.RIGHT => -parentNodeSize.x,
                _ => 0
            };

            anchoredPosition.y += figmaNode.constraints.vertical switch
            {
                LayoutConstraint.VerticalLayoutConstraint.CENTER => parentNodeSize.y * 0.5f,
                LayoutConstraint.VerticalLayoutConstraint.BOTTOM => parentNodeSize.y,
                _ => 0
            };
            
            targetRectTransform.anchoredPosition = anchoredPosition;

            switch (figmaNode.constraints.horizontal)
            {
                case LayoutConstraint.HorizontalLayoutConstraint.LEFT_RIGHT: 
                case LayoutConstraint.HorizontalLayoutConstraint.SCALE:
                    var sizeDelta = targetRectTransform.sizeDelta;
                    targetRectTransform.sizeDelta = new Vector2(sizeDelta.x-parentNodeSize.x, sizeDelta.y);
                    break;
            }

            switch (figmaNode.constraints.vertical)
            {
                case LayoutConstraint.VerticalLayoutConstraint.TOP_BOTTOM:
                case LayoutConstraint.VerticalLayoutConstraint.SCALE:
                    var sizeDelta = targetRectTransform.sizeDelta;
                    targetRectTransform.sizeDelta = new Vector2(sizeDelta.x, sizeDelta.y - parentNodeSize.y);
                    break;
            }
            
        }

        /// <summary>
        /// Applies the transform of a Figma Node to a Unity RectTransform using absolute position. This is
        /// required for Server rendered images, that have the transform (eg rotation) baked in
        /// </summary>
        /// <param name="targetRectTransform"></param>
        /// <param name="figmaNode"></param>
        /// <param name="figmaParentNode"></param>
        /// <param name="centerPivot"></param>
        public static void ApplyAbsoluteBoundsFigmaTransform(RectTransform targetRectTransform, Node figmaNode,
            Node figmaParentNode, bool centerPivot)
        {
            // Default to top left alignment
            targetRectTransform.anchorMin = targetRectTransform.anchorMax = new Vector2(0, 1);
            targetRectTransform.pivot = new Vector2(0, 1);
            
            // We'll use absolute bounding box size
            targetRectTransform.sizeDelta = new Vector2(figmaNode.absoluteBoundingBox.width, figmaNode.absoluteBoundingBox.height);

            //Add a layout element and set its preferred size
            LayoutElement layoutElement = targetRectTransform.gameObject.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = figmaNode.absoluteBoundingBox.width;
            layoutElement.preferredHeight = figmaNode.absoluteBoundingBox.height;

            layoutElement.minHeight = figmaNode.absoluteBoundingBox.height;
            layoutElement.minWidth = figmaNode.absoluteBoundingBox.width;

            // Position will be relative to parent absoluteBoundingBox (if it exists). Pages have no absoluteBoundingBox so assume pos of 0,0
            var figmaParentNodePosition = figmaParentNode.absoluteBoundingBox != null
                ? new Vector2(figmaParentNode.absoluteBoundingBox.x, figmaParentNode.absoluteBoundingBox.y)
                : Vector2.zero;
            
            targetRectTransform.anchoredPosition=new Vector2(figmaNode.absoluteBoundingBox.x-figmaParentNodePosition.x,
                -(figmaNode.absoluteBoundingBox.y-figmaParentNodePosition.y));
            
            // Some nodes will not have a constraints node (eg SECTION nodes)
            if (figmaNode.constraints!=null) ApplyFigmaConstraints(targetRectTransform, figmaNode, figmaParentNode);
            
            // We'll also use these properties to apply pivot after, where required
            if (centerPivot) SetPivot(targetRectTransform, new Vector2(0.5f, 0.5f));
            
        }
        
        /// <summary>
        /// Modify the pivot point of a RectTransform, and move to ensure position stays the same
        /// </summary>
        /// <param name="target"></param>
        /// <param name="pivot"></param>
        private static void SetPivot(RectTransform target, Vector2 pivot)
        {
            if (!target) return;
            var offset=pivot - target.pivot;
            offset.Scale(target.rect.size);
            var worldPos= target.position + target.TransformVector(offset);
            target.pivot = pivot;
            target.position = worldPos;
        }

        /// <summary>
        /// Apply anchor positions according to constraints
        /// </summary>
        /// <param name="constraint"></param>
        /// <returns></returns>
        private static (Vector2, Vector2) AnchorPositionsForFigmaConstraints(LayoutConstraint constraint)
        {
            var anchorsX = constraint.horizontal switch
            {
                LayoutConstraint.HorizontalLayoutConstraint.LEFT => new Vector2(0, 0),
                LayoutConstraint.HorizontalLayoutConstraint.RIGHT => new Vector2(1, 1),
                LayoutConstraint.HorizontalLayoutConstraint.CENTER => new Vector2(0.5f, 0.5f),
                LayoutConstraint.HorizontalLayoutConstraint.LEFT_RIGHT => new Vector2(0, 1),
                LayoutConstraint.HorizontalLayoutConstraint.SCALE => new Vector2(0, 1),
                _ => new Vector2(0, 1)
            };

            var anchorsY = constraint.vertical switch
            {
                LayoutConstraint.VerticalLayoutConstraint.TOP => new Vector2(1, 1),
                LayoutConstraint.VerticalLayoutConstraint.BOTTOM => new Vector2(0, 0),
                LayoutConstraint.VerticalLayoutConstraint.CENTER => new Vector2(0.5f, 0.5f),
                LayoutConstraint.VerticalLayoutConstraint.TOP_BOTTOM => new Vector2(0, 1),
                LayoutConstraint.VerticalLayoutConstraint.SCALE => new Vector2(0, 1),
                _ => new Vector2(0, 1)
            };
            return (new Vector2(anchorsX.x, anchorsY.x), new Vector2(anchorsX.y, anchorsY.y));
        }

        /// <summary>
        /// Gets the relative bounding box for the contents of all children. Useful for resizing for scroll content when
        /// bounds are not declared explicitly in the document and auto-layout not used
        /// </summary>
        /// <param name="figmaNode"></param>
        /// <returns></returns>
        public static Rect GetRelativeBoundsForAllChildNodes(Node figmaNode)
        {
            if (figmaNode.children == null) return new Rect();
            var mergedRect = new Rect();
            for (var i = 0; i < figmaNode.children.Length; i++)
            {
                var childNode = figmaNode.children[i];
                var relativePosition = new Vector2(childNode.absoluteBoundingBox.x - figmaNode.absoluteBoundingBox.x,
                    childNode.absoluteBoundingBox.y - figmaNode.absoluteBoundingBox.y);
                var size = new Vector2(childNode.absoluteBoundingBox.width, childNode.absoluteBoundingBox.height);
                
                if (i == 0)
                {
                    mergedRect.xMin = relativePosition.x;
                    mergedRect.xMax = relativePosition.x+size.x;
                    mergedRect.yMin = relativePosition.y;
                    mergedRect.yMax = relativePosition.y+size.y;
                }
                else
                {
                    mergedRect.xMin = Mathf.Min(mergedRect.xMin,relativePosition.x);
                    mergedRect.xMax = Mathf.Max(mergedRect.xMax,relativePosition.x+size.x);
                    mergedRect.yMin = Mathf.Min(mergedRect.yMin,relativePosition.y);
                    mergedRect.yMax = Mathf.Max(mergedRect.yMax,relativePosition.y+size.y);
                }
            }

            return mergedRect;
        }
        
    }
}