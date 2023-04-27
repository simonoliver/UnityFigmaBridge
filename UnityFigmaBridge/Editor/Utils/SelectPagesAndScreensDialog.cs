using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UnityFigmaBridge.Editor.Utils
{
    /// <summary>
    /// Select Pages and Screens Dialog
    /// </summary>
    public class SelectPagesAndScreensDialog : EditorWindow
    {
        static readonly List<LineData> s_PageDataList = new ();
        static readonly List<LineData> s_ScreenDataList = new ();
        static Vector2 s_PageScrollPos;
        static Vector2 s_ScreenScrollPos;
        Action<IReadOnlyList<LineData>,IReadOnlyList<LineData>> onOk;
        Action onCancel;
        bool IsClickdOk;

        public sealed class LineData
        {
            public string Name { get; }
            public bool IsChecked { get; set; }

            public LineData(string name)
            {
                Name = name;
                IsChecked = true; // default is true
            }
        }

        void RegisterOk(Action<IReadOnlyList<LineData>, IReadOnlyList<LineData>> onOk)
        {
            this.onOk += onOk;
        }

        void RegisterCancel(Action onCancel)
        {
            this.onCancel += onCancel;
        }

        #region OnGUI()
        
        void ListCore(string listTitle, IReadOnlyList<LineData> dataArray, ref Vector2 scrollPos)
        {
            using (new EditorGUILayout.VerticalScope()) {
                GUILayout.Label(listTitle, EditorStyles.boldLabel);
                GUILayout.Space(5);
                using (new EditorGUILayout.HorizontalScope()) {
                    if (GUILayout.Button("Select all", GUILayout.Width(80))) {
                        foreach (var data in dataArray) {
                            data.IsChecked = true;
                        }
                    }

                    if (GUILayout.Button("Deselect all", GUILayout.Width(80))) {
                        foreach (var data in dataArray) {
                            data.IsChecked = false;
                        }
                    }
                }
                GUILayout.Space(5);

                using (var scrollViewScope = new EditorGUILayout.ScrollViewScope(scrollPos)) {
                    foreach (var data in dataArray) {
                        data.IsChecked = EditorGUILayout.ToggleLeft(data.Name, data.IsChecked);
                    }
                    scrollPos = scrollViewScope.scrollPosition;
                }
            }
        }

        void OnGUI()
        {
            minSize = new Vector2(800, 600);
            using (new EditorGUILayout.HorizontalScope()) {
                ListCore("Pages", s_PageDataList, ref s_PageScrollPos);
                GUILayout.Space(5);
                ListCore("Screens", s_ScreenDataList, ref s_ScreenScrollPos);
            }

            using (new EditorGUILayout.HorizontalScope("Box")) {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("OK", GUILayout.Width(80))) {
                    IsClickdOk = true;
                }

                if (GUILayout.Button("Cancel", GUILayout.Width(80))) {
                    onCancel?.Invoke();
                    Close();
                }
            }

            if (IsClickdOk)
            {
                EditorApplication.delayCall += () => {
                    onOk?.Invoke(s_PageDataList, s_ScreenDataList);
                };
                IsClickdOk = false;
                Close();
            }
        }

        #endregion OnGUI()

        #region Show()

        public static void Show
        (
            List<string> pageNames,
            List<string> screenNames,
            Action<IReadOnlyList<LineData>, IReadOnlyList<LineData>> onOk,
            Action onCancel)
        {
            s_PageDataList.Clear();
            s_ScreenDataList.Clear();
            
            foreach (var pageName in pageNames) {
                s_PageDataList.Add(new LineData(pageName));
            }
            foreach (var screenName in screenNames) {
                s_ScreenDataList.Add(new LineData(screenName));
            }

            s_PageScrollPos = Vector2.zero;
            s_ScreenScrollPos = Vector2.zero;

            var window = CreateInstance<SelectPagesAndScreensDialog>();
            window.RegisterOk(onOk);
            window.RegisterCancel(onCancel);
            window.ShowModalUtility();
        }
        #endregion Show()
    }
}