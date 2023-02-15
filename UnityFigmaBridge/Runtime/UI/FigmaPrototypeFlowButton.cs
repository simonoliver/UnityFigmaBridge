using UnityEngine;
using UnityEngine.UI;

namespace UnityFigmaBridge.Runtime.UI
{

    [RequireComponent(typeof(Button))]
    public class FigmaPrototypeFlowButton : MonoBehaviour
    {
        /// <summary>
        /// The node id to transition to
        /// </summary>
        public string TargetScreenNodeId
        {
            get => m_TargetScreenNodeId;
            set => m_TargetScreenNodeId = value;
        }

        [SerializeField] private string m_TargetScreenNodeId;
        
        protected void Start()
        {
            // Add a listener for presses - to go to appropriate flowScreen
            GetComponent<Button>().onClick.AddListener(() =>
            {
                // Get prototype flow controller (assumed attached to root canvas)
                var prototypeFlowController =
                    GetComponentInParent<Canvas>().rootCanvas?.GetComponent<PrototypeFlowController>();
                
                if (prototypeFlowController!=null)
                    prototypeFlowController.TransitionToScreenById(TargetScreenNodeId);
            });
        }
    }

}
