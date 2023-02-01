using UnityEngine;

namespace UnityFigmaBridge.Runtime.UI
{
    /// <summary>
    /// Marks a component as ready for replacement with an instantiated prefab
    /// </summary>
    public class FigmaComponentNodeMarker : MonoBehaviour
    {
        public string ComponentId;
        public string NodeId;
        public string ParentNodeId;

        public void Initialise(string nodeId, string parentNodeId, string componentId)
        {
            NodeId = nodeId;
            ParentNodeId = parentNodeId;
            ComponentId = componentId;
        }
    }
}
