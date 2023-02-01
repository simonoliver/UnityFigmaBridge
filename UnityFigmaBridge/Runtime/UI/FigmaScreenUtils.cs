using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UnityFigmaBridge.Runtime.UI
{
    public static class FigmaScreenUtils
    {

        public static TextMeshProUGUI FindScreenTextNode(GameObject rootObject, string targetName)
        {
            var screenObject = FindScreenObject(rootObject, targetName);
            return screenObject == null ? null : screenObject.GetComponent<TextMeshProUGUI>();
        }
        
        public static Button FindScreenButton(GameObject rootObject, string targetName)
        {
            var screenObject = FindScreenObject(rootObject, targetName);
            return screenObject == null ? null : screenObject.GetComponent<Button>();
        }
        /// <summary>
        /// Find a UI element matching a name within a flowScreen, stripping node names. Case insensitive
        /// </summary>
        /// <param name="rootObject"></param>
        /// <param name="targetName"></param>
        /// <returns></returns>
        public static GameObject FindScreenObject(GameObject rootObject, string targetName)
        {
            // Check name matches, case insensitive
            //Debug.Log($"Checking {StripNodesFromName(rootObject.name)}");
            if (StripNodesFromName(rootObject.name).ToLower() == targetName.ToLower()) return rootObject;
            var childNodeCount = rootObject.transform.childCount;
            for (var i = 0; i < childNodeCount; i++)
            {
                var childNode = rootObject.transform.GetChild(i);
                var foundScreenObjectInChild = FindScreenObject(childNode.gameObject, targetName);
                if (foundScreenObjectInChild != null) return foundScreenObjectInChild;
            }

            return null;
        }

        /// <summary>
        /// Strips nodes
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static string StripNodesFromName(string name)
        {
            var firstIndexUnderscore = name.IndexOf('_');
            return (firstIndexUnderscore < 0) ? name : name.Substring(0,firstIndexUnderscore);
        }
    }
}