using UnityEngine;

namespace UnityFigmaBridge.Runtime.UI
{
    /// <summary>
    /// Temporary representative object for FIGMA nodes to allow them to be matched
    /// When the generation and subtitution process continues
    /// </summary>
    public class FigmaNodeObject : MonoBehaviour
    {
        // Reference to the full FIGMA node id
        public string NodeId;
    }
}