using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace UnityFigmaBridge.Runtime.UI
{
    
    /// <summary>
    /// Holds all figma flowScreen data that is needed by UnityUI
    /// </summary>
    [System.Serializable]
    public class FigmaFlowScreen
    {
        /// <summary>
        /// Figma Node ID for this screen (used for lookup)
        /// </summary>
        public string FigmaNodeId;
        /// <summary>
        /// Screen prefab to instantiate
        /// </summary>
        public GameObject FigmaScreenPrefab;
        /// <summary>
        /// Node name (allowing screens to be referenced by name)
        /// </summary>
        public string FigmaScreenName;
        /// <summary>
        /// Parent section Node Id (if part of a section)
        /// </summary>
        public string ParentSectionNodeId;
    }
    
    /// <summary>
    /// Holds all figma flowScreen data that is needed by UnityUI
    /// </summary>
    [System.Serializable]
    public class FigmaSection
    {
        /// <summary>
        /// Figma Node ID for this section
        /// </summary>
        public string FigmaNodeId;
        /// <summary>
        /// Node id of flow start for this section
        /// </summary>
        public string FigmaPrototypeFlowStartNodeId;
        /// <summary>
        /// Node name of flow start for this section
        /// </summary>
        public string FigmaPrototypeFlowStartNodeName;
        /// <summary>
        /// Node name (allowing screens to be referenced by name)
        /// </summary>
        public string FigmaNodeName;
    }
    
    
    /// <summary>
    /// Flow controller for recreating Prototype flow, as defined in a Figma document
    /// </summary>
    public class PrototypeFlowController : MonoBehaviour
    {

        /// <summary>
        /// Event fired when screen changed, allowing for state change as required
        /// </summary>
        public UnityEvent<string,GameObject> OnScreenChanged;
        
        /// <summary>
        /// The content holder rect transform
        /// </summary>
        public RectTransform ScreenParentTransform
        {
            get=>m_ScreenParentTransform;
            set=>m_ScreenParentTransform=value;
        }
        
        /// <summary>
        /// The active transition effect (if exists)
        /// </summary>
        public TransitionEffect TransitionEffect
        {
            get=>m_TransitionEffect;
            set=>m_TransitionEffect=value;
        }

        /// <summary>
        /// The current Figma screen instance
        /// </summary>
        public GameObject CurrentScreenInstance=>m_CurrentScreenInstance;


        /// <summary>
        /// The initial screen id to use for the prototype flow
        /// </summary>
        public string PrototypeFlowInitialScreenId
        {
            get => m_PrototypeFlowInitialScreenId;
            set => m_PrototypeFlowInitialScreenId = value;
        }
        
        
        [SerializeField] private RectTransform m_ScreenParentTransform;
        [SerializeField] private TransitionEffect m_TransitionEffect;
        [SerializeField] private GameObject m_CurrentScreenInstance;
        [SerializeField] private string m_PrototypeFlowInitialScreenId;
        [SerializeField] private List<FigmaFlowScreen> m_Screens = new();
        [SerializeField] private List<FigmaSection> m_Sections = new();

        /// <summary>
        /// Tracks the current screen used in each section - kept consistent when transitioning between sections
        /// </summary>
        private Dictionary<string, string> m_CurrentScreenForSection = new();

        /// <summary>
        /// Called on start
        /// </summary>
        public void Start()
        {
            // If we start with a specific flowScreen, need to invoke event to set up required state
            if (CurrentScreenInstance != null)
                OnScreenChanged?.Invoke(CurrentScreenInstance.name,CurrentScreenInstance);

            // Set each active section screen as the default
            foreach (var section in m_Sections)
                m_CurrentScreenForSection[section.FigmaNodeId] = section.FigmaPrototypeFlowStartNodeId;
            
            // If this is in a section, record this
            RegisterSectionChangeForScreen(m_PrototypeFlowInitialScreenId);
        }
        
        /// <summary>
        /// Returns definition for a given Figma Flow Screen by Node ID
        /// </summary>
        /// <param name="screenNodeId"></param>
        /// <returns></returns>
        private FigmaFlowScreen GetFlowScreenById(string screenNodeId)
        {
            return m_Screens.Find((screen) => screen.FigmaNodeId == screenNodeId);
        }
        
        /// <summary>
        /// Returns definition for a given Figma Section by Node ID
        /// </summary>
        /// <param name="screenNodeId"></param>
        /// <returns></returns>
        private FigmaSection GetSectionById(string sectionNodeId)
        {
            return m_Sections.Find((section) => section.FigmaNodeId == sectionNodeId);
        }

        /// <summary>
        /// Returns definition for a given Figma Flow Screen by Node Name
        /// </summary>
        /// <param name="screenName"></param>
        /// <returns></returns>
        private FigmaFlowScreen GetFlowScreenByName(string screenName)
        {
            return m_Screens.Find((screen) => screen.FigmaScreenName == screenName);
        }

        /// <summary>
        /// Returns the screen set as start of active flow
        /// </summary>
        public FigmaFlowScreen StartFlowScreen => GetFlowScreenById(PrototypeFlowInitialScreenId);

       /// <summary>
       /// Adds a screen definition to the flow
       /// </summary>
       /// <param name="flowScreen"></param>
        public void RegisterFigmaScreen(FigmaFlowScreen flowScreen)
        {
            m_Screens.Add(flowScreen);
        }
       
       /// <summary>
       /// Adds a section definition to the flow
       /// </summary>
       /// <param name="figmaSection"></param>
       public void RegisterFigmaSection(FigmaSection figmaSection)
       {
           m_Sections.Add(figmaSection);
       }
        
        /// <summary>
        /// Clears all currently registered figma screens and destroys any active screen instance
        /// </summary>
        public void ClearFigmaScreens()
        {
            m_Screens.Clear();
            m_Sections.Clear();
            DestroyImmediate(CurrentScreenInstance);
            m_CurrentScreenInstance = null;
        }

        /// <summary>
        /// Play transition to a given screen (defined by Figma Node ID)
        /// </summary>
        /// <param name="screenNodeID"></param>
        public void TransitionToScreenById(string screenNodeID)
        {
            if (m_TransitionEffect == null)
            {
                SetCurrentScreenByNodeId(screenNodeID);
                return;
            }
            m_TransitionEffect.AnimateOut(() =>
            {
                SetCurrentScreenByNodeId(screenNodeID);
                m_TransitionEffect.AnimateIn();
            });
        }
        
        /// <summary>
        /// Play transition to a given screen (defined by Figma Node Name)
        /// </summary>
        /// <param name="screenName"></param>
        public void TransitionToScreenByName(string screenName)
        {
            if (m_TransitionEffect == null)
            {
                SetScreenByName(screenName);
                return;
            }
            m_TransitionEffect.AnimateOut(() =>
            {
                SetScreenByName(screenName);
                m_TransitionEffect.AnimateIn();
            });
        }

        /// <summary>
        /// Set screen by Figma Node id
        /// </summary>
        /// <param name="screenNodeId"></param>
        public void SetCurrentScreenByNodeId(string nodeId)
        {
            // Check if this is a section node
            if (m_CurrentScreenForSection.ContainsKey(nodeId))
            {
                // It's a section so use the active screen for this section
                nodeId = m_CurrentScreenForSection[nodeId];
            }
            
            var screenData = GetFlowScreenById(nodeId);
            if (screenData == null)
            {
                Debug.LogWarning($"Definition for screen or section missing: '{nodeId}'");
                return;
            }
            var screenObject = Instantiate(screenData.FigmaScreenPrefab, m_ScreenParentTransform);
            screenObject.name = screenData.FigmaScreenPrefab.name;
            SetCurrentScreen(screenObject,screenData.FigmaNodeId,false);
        }


        /// <summary>
        /// Set screen by Figma Node Name
        /// </summary>
        /// <param name="screenName"></param>
        public void SetScreenByName(string screenName)
        {
            var screenData = GetFlowScreenByName(screenName);
            if (screenData == null)
            {
                Debug.LogWarning($"Definition for screen missing: '{screenName}'");
                return;
            }
            var newScreen=Instantiate(screenData.FigmaScreenPrefab, m_ScreenParentTransform);
            newScreen.name = screenName;
            SetCurrentScreen(newScreen,screenData.FigmaNodeId,false);
        }
        
        /// <summary>
        /// Sets current screen 
        /// </summary>
        /// <param name="screen"></param>
        /// <param name="fromEditor"></param>
        public void SetCurrentScreen(GameObject screen,string nodeId, bool fromEditor)
        {
            // If in editor (non runtime) mode, destroy immediately 
            if (fromEditor) DestroyImmediate(CurrentScreenInstance);
            else Destroy(CurrentScreenInstance);
            
            // Make sure we clear any active selection in any event system
            if (!fromEditor) EventSystem.current.SetSelectedGameObject(null);

            // Set local ref
            m_CurrentScreenInstance = screen;
            
            // Registers any change to section
            RegisterSectionChangeForScreen(nodeId);
            
            // Ensure screen fills current canvas
            var screenTransform = (RectTransform)m_CurrentScreenInstance.transform;
            screenTransform.anchorMin = Vector2.zero;
            screenTransform.anchorMax = Vector2.one;
            screenTransform.sizeDelta = Vector2.zero;

            // Fire off event
            if (!fromEditor)
            {
                OnScreenChanged?.Invoke(screen.name,screen);
            }
        }
        
        /// <summary>
        /// Registers the current active screen for a given section
        /// </summary>
        /// <param name="screenNodeId"></param>
        private void RegisterSectionChangeForScreen(string screenNodeId)
        {
            var screenData = GetFlowScreenById(screenNodeId);
            if (screenData == null) return;
            if (string.IsNullOrEmpty(screenData.ParentSectionNodeId)) return;
            m_CurrentScreenForSection[screenData.ParentSectionNodeId] = screenNodeId;
        }
    }
}
