using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityFigmaBridge.Editor.Settings
{

    public class UnityFigmaBridgeSettingsProvider : SettingsProvider
    {
        private GUIStyle m_RedStyle;
        private GUIStyle m_GreenStyle;

        public UnityFigmaBridgeSettingsProvider(string path, SettingsScope scopes, IEnumerable<string> keywords = null)
            : base(path, scopes, keywords)
        {
            m_RedStyle = new GUIStyle(EditorStyles.label);
            m_RedStyle.normal.textColor = UnityEngine.Color.red;
            
            m_GreenStyle= new GUIStyle(EditorStyles.label);
            m_GreenStyle.normal.textColor = UnityEngine.Color.green;
        }


        public static bool IsSettingsAvailable()
        {
            return true;
        }

        private UnityFigmaBridgeSettings unityFigmaBridgeSettingsAsset;

        public override void OnActivate(string searchContext, VisualElement rootElement)
        {

            unityFigmaBridgeSettingsAsset = FindUnityBridgeSettingsAsset();
        }

        /// <summary>
        /// Finds the first (and should be only) matching asset
        /// </summary>
        /// <returns></returns>
        public static UnityFigmaBridgeSettings FindUnityBridgeSettingsAsset()
        {
            var assets = AssetDatabase.FindAssets($"t:{typeof(UnityFigmaBridgeSettings).Name}");
            if (assets == null || assets.Length == 0) return null;
            return AssetDatabase.LoadAssetAtPath<UnityFigmaBridgeSettings>(AssetDatabase.GUIDToAssetPath(assets[0]));
        }

        public override void OnGUI(string searchContext)
        {
            if (unityFigmaBridgeSettingsAsset == null)
            {
                GUILayout.Label("Create Unity Figma Bridge Settings Asset");
                if (GUILayout.Button("Create..."))
                {
                    unityFigmaBridgeSettingsAsset = GenerateUnityFigmaBridgeSettingsAsset();
                }

                return;
            }

            // Use IMGUI to display UI:
            var serializedObject = new SerializedObject(unityFigmaBridgeSettingsAsset);
            SerializedProperty prop = serializedObject.GetIterator();
            if (prop.NextVisible(true))
            {
                do
                {
                    EditorGUILayout.PropertyField(serializedObject.FindProperty(prop.name), true);
                } while (prop.NextVisible(false));
            }

            serializedObject.ApplyModifiedProperties();

            GUILayout.Space(10);
            var (isValid, fileId) = FigmaApi.FigmaApiUtils.GetFigmaDocumentIdFromUrl(unityFigmaBridgeSettingsAsset.DocumentUrl);
            if (!isValid)
            {
                GUILayout.Label($"Invalid Figma Document URL",m_RedStyle);
                return;
            }
            GUILayout.Label($"Valid Figma Document URL - FileID: {fileId}",m_GreenStyle);
        }


        // Register the SettingsProvider
        [SettingsProvider]
        public static SettingsProvider CreateMyCustomSettingsProvider()
        {
            if (IsSettingsAvailable())
            {
                var provider =
                    new UnityFigmaBridgeSettingsProvider("Project/Unity Figma Bridge", SettingsScope.Project);
                return provider;
            }

            return null;
        }

        public static UnityFigmaBridgeSettings GenerateUnityFigmaBridgeSettingsAsset()
        {
            // try create a new version asset.
            var newSettingsAsset = UnityFigmaBridgeSettings.CreateInstance<UnityFigmaBridgeSettings>();

            // Save to the project
            AssetDatabase.CreateAsset(newSettingsAsset, "Assets/UnityFigmaBridgeSettings.asset");
            AssetDatabase.SaveAssets();
            Debug.Log("Generating UnityFigmaBridgeSettings asset", newSettingsAsset);

            return newSettingsAsset;
        }
    }
}