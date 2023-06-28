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
        
        private static Vector2 s_PageScrollPos;
        private static Vector2 s_ScreenScrollPos;

        public override void OnInspectorGUI()
        {
            var targetSettingsObject = target as UnityFigmaBridgeSettings;
            var onlyImportPages= targetSettingsObject.OnlyImportSelectedPages;
            var preEditUrl= targetSettingsObject.DocumentUrl;
            base.OnInspectorGUI();
            // If the URL has changed, we want to reset the select pages to off and clear
            if (targetSettingsObject.DocumentUrl != preEditUrl)
            {
                if (targetSettingsObject.OnlyImportSelectedPages)
                {
                    targetSettingsObject.OnlyImportSelectedPages = false;
                    targetSettingsObject.PageDataList.Clear();
                }
            }
            else if (targetSettingsObject.OnlyImportSelectedPages != onlyImportPages)
            {
                if (targetSettingsObject.OnlyImportSelectedPages)
                {
                    // Update pages
                    RefreshPageList(targetSettingsObject);
                }
                else
                {
                    // Reset list
                    targetSettingsObject.PageDataList.Clear();
                }
            }

            if (targetSettingsObject.OnlyImportSelectedPages)
            {
                GUILayout.Space(20);
                var changed = ListPages("Select Pages to import", targetSettingsObject.PageDataList,
                    ref s_PageScrollPos);
                if (changed)
                {
                    EditorUtility.SetDirty(targetSettingsObject);
                    AssetDatabase.SaveAssetIfDirty(targetSettingsObject);
                }
            }
        }


        /// <summary>
        /// Download the document and refresh the page list
        /// </summary>
        /// <param name="settings"></param>
        private static async void RefreshPageList(UnityFigmaBridgeSettings settings)
        {
            // Only refresh pages if we have a valid file
            var requirementsMet = UnityFigmaBridgeImporter.CheckRequirements();
            if (!requirementsMet) return;

            // Retrieve the Figma document
            var figmaFile = await UnityFigmaBridgeImporter.DownloadFigmaDocument(settings.FileId);
            if (figmaFile == null) return;
            
            settings.RefreshForUpdatedPages(figmaFile);

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssetIfDirty(settings);
        }

        /// <summary>
        /// List all pages in the settings file
        /// </summary>
        /// <param name="listTitle"></param>
        /// <param name="dataList"></param>
        /// <param name="scrollPos"></param>
        /// <returns></returns>
        private static bool ListPages(string listTitle, IReadOnlyList<FigmaPageData> dataList, ref Vector2 scrollPos)
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

                return applyChanges;

            }
        }
        
    }
}