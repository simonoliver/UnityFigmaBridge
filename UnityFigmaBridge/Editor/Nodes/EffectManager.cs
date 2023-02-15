using UnityEngine;
using UnityEngine.UI;
using UnityFigmaBridge.Editor.FigmaApi;

namespace UnityFigmaBridge.Editor.Nodes
{
    /// <summary>
    /// Applies effects to nodes where required. 
    /// </summary>
    public static class EffectManager
    {


        /// <summary>
        /// Apply all effects to a given node
        /// </summary>
        /// <param name="nodeGameObject"></param>
        /// <param name="node"></param>
        /// <param name="figmaImportProcessData"></param>
        public static void ApplyAllFigmaEffectsToUnityNode(GameObject nodeGameObject,Node node, 
            FigmaImportProcessData figmaImportProcessData)
        {
            foreach (var effect in node.effects) ApplyEffectToUnityNode(nodeGameObject,node,effect,figmaImportProcessData);
            
        }

        
        /// <summary>
        /// Applies an individual effect gto a unity node
        /// </summary>
        /// <param name="nodeGameObject"></param>
        /// <param name="node"></param>
        /// <param name="effect"></param>
        /// <param name="figmaImportProcessData"></param>
        private static void ApplyEffectToUnityNode(GameObject nodeGameObject,Node node,Effect effect, FigmaImportProcessData figmaImportProcessData)
        {
            switch (effect.type)
            {
                case Effect.EffectType.DROP_SHADOW:
                    switch (node.type)
                    {
                        case NodeType.TEXT:
                            // TMPro doesnt support shadows, this will be done via material preset
                            break;
                        case NodeType.DOCUMENT:
                        case NodeType.CANVAS:
                        case NodeType.FRAME:
                        case NodeType.GROUP:
                        case NodeType.VECTOR:
                        case NodeType.BOOLEAN_OPERATION:
                        case NodeType.STAR:
                        case NodeType.LINE:
                        case NodeType.ELLIPSE:
                        case NodeType.REGULAR_POLYGON:
                        case NodeType.RECTANGLE:
                        case NodeType.SLICE:
                        case NodeType.COMPONENT:
                        case NodeType.COMPONENT_SET:
                        case NodeType.INSTANCE:
                        case NodeType.STICKY:
                        case NodeType.SHAPE_WITH_TEXT:
                        case NodeType.CONNECTOR:
                        default:
                            var shadow = nodeGameObject.AddComponent<Shadow>();
                            shadow.effectDistance = new Vector2(effect.offset.x, -effect.offset.y);
                            shadow.effectColor = FigmaDataUtils.ToUnityColor(effect.color);
                            // TODO - Apply blur radius (will need better shadow implementation)
                            break;
                    }

                    break;
                case Effect.EffectType.INNER_SHADOW:
                case Effect.EffectType.LAYER_BLUR:
                case Effect.EffectType.BACKGROUND_BLUR:
                    // Unsupported
                    break;
            }
        }
        
    }
}
