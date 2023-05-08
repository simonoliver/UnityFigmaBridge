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
        private static bool m_DownloadCompleted;

        static Vector2 s_PageScrollPos;
        static Vector2 s_ScreenScrollPos;

        public static List<Node> PageNodes { get; private set; }
        public static List<Node> ScreenNodes { get; private set; }

        private static void ClearList()
        {
            PageNodes = new();
            ScreenNodes = new();
        }
        private static async void DownloadFigmaDocument()
        {
            var requirementsMet = UnityFigmaBridgeImporter.CheckRequirements();
            if (!requirementsMet) return;

            var figmaFile = await UnityFigmaBridgeImporter.DownloadFigmaDocument(s_UnityFigmaBridgeSettings.FileId);
            if (figmaFile == null) return;
            
            PageNodes = FigmaDataUtils.GetPageNodes(figmaFile);
            ScreenNodes = FigmaDataUtils.GetScreenNodes(figmaFile);

            var downloadPageNodeIdList = PageNodes.Select(p => p.id).ToList();
            var downloadScreenNodeIdList = ScreenNodes.Select(s => s.id).ToList();

            var settingsPageDataIdList = s_UnityFigmaBridgeSettings.PageDataList.Select(p => p.Id).ToList();
            var settingsScreenDataIdList = s_UnityFigmaBridgeSettings.ScreenDataList.Select(s => s.Id).ToList();

            var addPageIdList = downloadPageNodeIdList.Except(settingsPageDataIdList);
            var addScreenIdList = downloadScreenNodeIdList.Except(settingsScreenDataIdList);

            foreach (var addPageId in addPageIdList)
            {
                var addNode = PageNodes.FirstOrDefault(p => p.id == addPageId);
                s_UnityFigmaBridgeSettings.PageDataList.Add(new LineData(addNode.name, addNode.id));
            }
            foreach (var addScreenId in addScreenIdList)
            {
                var addNode = ScreenNodes.FirstOrDefault(p => p.id == addScreenId);
                s_UnityFigmaBridgeSettings.ScreenDataList.Add(new LineData(addNode.name, addNode.id));
            }
            
            var deletePageIdList = settingsPageDataIdList.Except(downloadPageNodeIdList);
            var deleteScreenIdList = settingsScreenDataIdList.Except(downloadScreenNodeIdList);

            foreach (var deletePageId in deletePageIdList)
            {
                int index = s_UnityFigmaBridgeSettings.PageDataList.FindIndex(p => p.Id == deletePageId);
                s_UnityFigmaBridgeSettings.PageDataList.RemoveAt(index);
            }
            foreach (var deleteScreenId in deleteScreenIdList)
            {
                int index = s_UnityFigmaBridgeSettings.ScreenDataList.FindIndex(s => s.Id == deleteScreenId);
                s_UnityFigmaBridgeSettings.ScreenDataList.RemoveAt(index);
            }

            EditorUtility.SetDirty(s_UnityFigmaBridgeSettings);
            AssetDatabase.SaveAssetIfDirty(s_UnityFigmaBridgeSettings);

            m_DownloadCompleted = true;
        }

        static void ListCore(string listTitle, IReadOnlyList<LineData> dataList, IReadOnlyList<Node> downloadList, ref Vector2 scrollPos)
        {
            var isSave = false;
            using (new EditorGUILayout.VerticalScope()) {
                GUILayout.Label(listTitle, EditorStyles.boldLabel);
                GUILayout.Space(5);
                using (new EditorGUILayout.HorizontalScope()) {
                    if (GUILayout.Button("Select all", GUILayout.Width(80))) {
                        isSave = true;
                        foreach (var data in dataList) {
                            data.IsChecked = true;
                        }
                    }

                    if (GUILayout.Button("Deselect all", GUILayout.Width(80))) {
                        isSave = true;
                        foreach (var data in dataList) {
                            data.IsChecked = false;
                        }
                    }
                }
                GUILayout.Space(5);

                using (var scrollViewScope = new EditorGUILayout.ScrollViewScope(scrollPos))
                {
                    foreach (var data in dataList) {
                        var isChecked = data.IsChecked;
                        var node = downloadList.FirstOrDefault(p => p.id == data.Id);
                        
                        data.IsChecked = EditorGUILayout.ToggleLeft(node.name, data.IsChecked);
                        if (isChecked != data.IsChecked) {
                            isSave = true;
                        }
                        
                    }
                    scrollPos = scrollViewScope.scrollPosition;
                }

                if (!isSave) return;
                EditorUtility.SetDirty(s_UnityFigmaBridgeSettings);
                AssetDatabase.SaveAssetIfDirty(s_UnityFigmaBridgeSettings);
            }
        }
        
        private static void ShowList()
        {
            GUILayout.Space(20);
            using (new EditorGUILayout.HorizontalScope()) {
                if (0 < s_UnityFigmaBridgeSettings.PageDataList.Count) {
                    ListCore("Select Download Pages", s_UnityFigmaBridgeSettings.PageDataList, PageNodes, ref s_PageScrollPos);
                }
                GUILayout.Space(5);
                if (0 < s_UnityFigmaBridgeSettings.ScreenDataList.Count) {
                    ListCore("Select Download Screens", s_UnityFigmaBridgeSettings.ScreenDataList, ScreenNodes,ref s_ScreenScrollPos);
                }
            }
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            s_UnityFigmaBridgeSettings = UnityFigmaBridgeSettingsProvider.FindUnityBridgeSettingsAsset();
            if (!s_UnityFigmaBridgeSettings.OnlyImportSelectedPages) {
                m_OnlyImportSelectedPages = false;
                m_DownloadCompleted = false;
                return;
            }

            if (m_OnlyImportSelectedPages == false) {
                m_OnlyImportSelectedPages = true;
                ClearList();
                DownloadFigmaDocument();
            }
            
            if (m_DownloadCompleted) ShowList();
        }
    }
}