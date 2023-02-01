using UnityEngine;
using UnityEngine.EventSystems;

namespace UnityFigmaBridge.Runtime.UI
{
  
    /// <summary>
    /// Applies device safe area to this RectTransform
    /// </summary>
    public class SafeArea : UIBehaviour
    {
        private float SafeAreaTopMargin => (Screen.height-Screen.safeArea.yMax)/CanvasScaleFactor;
        private float SafeAreaBottomMargin => (Screen.safeArea.yMin)/CanvasScaleFactor;

        private float SafeAreaLeftMargin => (Screen.safeArea.xMin)/CanvasScaleFactor;
        private float SafeAreaRightMargin => (Screen.width-Screen.safeArea.xMax)/CanvasScaleFactor;

        private Canvas m_OwnerCanvas;

        private float CanvasScaleFactor => OwnerCanvas.scaleFactor;
        
        private Canvas OwnerCanvas
        {
            get
            {
                if (m_OwnerCanvas == true) return m_OwnerCanvas;
                m_OwnerCanvas=GetComponentInParent<Canvas>().rootCanvas;
                return m_OwnerCanvas;
            }
        }


        protected override void Start()
        {
            OnRectTransformDimensionsChange();
        }

        protected override void OnRectTransformDimensionsChange()
        {
            if (OwnerCanvas == null)
            {
                Debug.LogWarning("Can't find root canvas for this element");
                return;
            }

            Debug.Log($"Margins L{SafeAreaLeftMargin},R{SafeAreaRightMargin} - Top {SafeAreaTopMargin}, Bottom {SafeAreaBottomMargin}");
            var rectTransform = transform as RectTransform;
            rectTransform.sizeDelta=new Vector2(-SafeAreaLeftMargin-SafeAreaRightMargin,-SafeAreaTopMargin-SafeAreaBottomMargin);
            
            // Centered
            rectTransform.anchoredPosition=new Vector2((SafeAreaLeftMargin-SafeAreaRightMargin)*0.5f,-(SafeAreaTopMargin-SafeAreaBottomMargin)*0.5f);

            // TL
            //rectTransform.anchoredPosition=new Vector2(SafeAreaLeftMargin,-SafeAreaTopMargin);
        }


    }
}
