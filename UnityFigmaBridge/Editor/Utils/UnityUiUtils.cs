using UnityEngine;

namespace UnityFigmaBridge.Editor.Utils
{
    public static class UnityUiUtils
    {
        public static RectTransform CreateRectTransform(string name, Transform parentTransform)
        {
            var newObject = new GameObject(name);
            var newTransform=newObject.AddComponent<RectTransform>();
            newTransform.SetParent(parentTransform,false);
            SetTransformFullStretch(newTransform);
            return newTransform;
        }

        public static void SetTransformFullStretch(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.anchoredPosition=Vector2.zero;
            rectTransform.sizeDelta=Vector2.zero;
        }
        
        public static void CloneTransformData(RectTransform source, RectTransform destination)
        {
            destination.anchorMin = source.anchorMin;
            destination.anchorMax = source.anchorMax;
            destination.anchoredPosition = source.anchoredPosition;
            destination.sizeDelta = source.sizeDelta;
            destination.localRotation = source.localRotation;
        }
    }
}