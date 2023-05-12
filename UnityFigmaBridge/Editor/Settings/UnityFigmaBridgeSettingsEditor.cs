using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityFigmaBridge.Editor.FigmaApi;

namespace UnityFigmaBridge.Editor.Settings
{
    [CustomEditor(typeof(UnityFigmaBridgeSettings))]
    public sealed class UnityFigmaBridgeSettingsEditor : UnityEditor.Editor
    {
        private static UnityFigmaBridgeSettings s_UnityFigmaBridgeSettings;
        private bool m_OnlyImportSelectedPages;

        private static Vector2 s_PageScrollPos;
        private static Vector2 s_ScreenScrollPos;

        
        
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            s_UnityFigmaBridgeSettings = UnityFigmaBridgeSettingsProvider.FindUnityBridgeSettingsAsset();
            if (!s_UnityFigmaBridgeSettings.OnlyImportSelectedPages) {
                m_OnlyImportSelectedPages = false;
                return;
            }

            if (m_OnlyImportSelectedPages == false) {
                m_OnlyImportSelectedPages = true;
                RefreshPageList();
            }
            
            // Always show list
            GUILayout.Space(20);
            ListCore("Select Download Pages", s_UnityFigmaBridgeSettings.PageDataList, ref s_PageScrollPos);
        }

        
        private static async void RefreshPageList()
        {
            // Only refresh pages if we have a valid file
            var requirementsMet = UnityFigmaBridgeImporter.CheckRequirements();
            if (!requirementsMet) return;

            // Retrieve the Figma document
            var figmaFile = await UnityFigmaBridgeImporter.DownloadFigmaDocument(s_UnityFigmaBridgeSettings.FileId);
            if (figmaFile == null) return;
            
            s_UnityFigmaBridgeSettings.RefreshForUpdatedPages(figmaFile);

            EditorUtility.SetDirty(s_UnityFigmaBridgeSettings);
            AssetDatabase.SaveAssetIfDirty(s_UnityFigmaBridgeSettings);
        }

        private static void ListCore(string listTitle, IReadOnlyList<FigmaPageData> dataList, ref Vector2 scrollPos)
        {
            var applyChanges = false;
            using (new EditorGUILayout.VerticalScope()) {
                GUILayout.Label(listTitle, EditorStyles.boldLabel);
                GUILayout.Space(5);
                using (new EditorGUILayout.HorizontalScope()) {
                    if (GUILayout.Button("Select all", GUILayout.Width(80))) {
                        applyChanges = true;
                        foreach (var data in dataList) {
                            data.Selected = true;
                        }
                    }

                    if (GUILayout.Button("Deselect all", GUILayout.Width(80))) {
                        applyChanges = true;
                        foreach (var data in dataList) {
                            data.Selected = false;
                        }
                    }
                }
                GUILayout.Space(5);

                using (var scrollViewScope = new EditorGUILayout.ScrollViewScope(scrollPos))
                {
                    foreach (var data in dataList) {
                        var isChecked = data.Selected;
                        data.Selected = EditorGUILayout.ToggleLeft(data.Name, data.Selected);
                        if (isChecked != data.Selected) {
                            applyChanges = true;
                        }
                        
                    }
                    scrollPos = scrollViewScope.scrollPosition;
                }

                if (!applyChanges) return;
                EditorUtility.SetDirty(s_UnityFigmaBridgeSettings);
                AssetDatabase.SaveAssetIfDirty(s_UnityFigmaBridgeSettings);
            }
        }
        
    }
}