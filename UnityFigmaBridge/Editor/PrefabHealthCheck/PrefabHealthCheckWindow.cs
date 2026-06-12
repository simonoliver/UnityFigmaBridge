using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UnityFigmaBridge.Editor.PrefabHealthCheck
{
    /// <summary>
    /// Editor window for displaying prefab health check results.
    /// Provides three tabs: Figma Variants, Missing References, and Orphaned Variants.
    /// </summary>
    public class PrefabHealthCheckWindow : EditorWindow
    {
        private enum Tab
        {
            FigmaVariants,
            MissingReferences,
            OrphanedVariants
        }

        private Tab _currentTab = Tab.FigmaVariants;
        private PrefabHealthCheckResult _result;
        private bool _isScanning;
        private string _progressMessage = "";
        private float _progressValue;

        private Vector2 _scrollVariants;
        private Vector2 _scrollMissing;
        private Vector2 _scrollOrphaned;

        // Foldout states for missing reference details (keyed by prefab path)
        private readonly Dictionary<string, bool> _missingFoldouts = new Dictionary<string, bool>();

        private static readonly string[] TabLabels =
        {
            "Figma Variants",
            "Missing References",
            "Orphaned Variants"
        };

        [MenuItem("Figma Bridge/Prefab Health Check", priority = 20)]
        public static void ShowWindow()
        {
            var window = GetWindow<PrefabHealthCheckWindow>("Prefab Health Check");
            window.minSize = new Vector2(500, 350);
            window.Show();
        }

        private void OnGUI()
        {
            DrawToolbar();
            DrawTabs();

            if (_isScanning)
            {
                DrawProgressBar();
                return;
            }

            if (_result == null || !_result.IsComplete)
            {
                EditorGUILayout.HelpBox(
                    "Click \"Scan\" to run the prefab health check.",
                    MessageType.Info);
                return;
            }

            switch (_currentTab)
            {
                case Tab.FigmaVariants:
                    DrawFigmaVariantsTab();
                    break;
                case Tab.MissingReferences:
                    DrawMissingReferencesTab();
                    break;
                case Tab.OrphanedVariants:
                    DrawOrphanedVariantsTab();
                    break;
            }
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            GUI.enabled = !_isScanning;
            if (GUILayout.Button("Scan", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                RunScan();
            }

            if (_result != null && _result.IsComplete)
            {
                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
                {
                    RunScan();
                }
            }

            GUI.enabled = true;

            GUILayout.FlexibleSpace();

            // Summary counts
            if (_result != null && _result.IsComplete)
            {
                GUILayout.Label(
                    $"V: {_result.FigmaVariants.Count}  |  M: {_result.MissingReferences.Count}  |  O: {_result.OrphanedVariants.Count}",
                    EditorStyles.toolbarButton);
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawTabs()
        {
            EditorGUILayout.BeginHorizontal();
            _currentTab = (Tab)GUILayout.Toolbar((int)_currentTab, TabLabels);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawProgressBar()
        {
            EditorGUILayout.Space(10);
            var rect = EditorGUILayout.GetControlRect(false, 20);
            EditorGUI.ProgressBar(rect, _progressValue, _progressMessage);
            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox("Scanning in progress...", MessageType.None);
        }

        // ────────────────────────────────────────────────────────────
        // Tab 1: Figma Variants
        // ────────────────────────────────────────────────────────────

        private void DrawFigmaVariantsTab()
        {
            EditorGUILayout.HelpBox(
                "Lists all prefab variants in the project that are derived from a Figma source prefab " +
                "(Pages, Screens, Components). Use this to track which project prefabs depend on Figma assets.",
                MessageType.None);
            EditorGUILayout.Space(2);

            var variants = _result.FigmaVariants;

            if (variants.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No Figma-derived variant prefabs found in the project.",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField($"Found {variants.Count} variant(s) derived from Figma prefabs:",
                EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            _scrollVariants = EditorGUILayout.BeginScrollView(_scrollVariants);

            foreach (var v in variants)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();

                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField("Variant:", v.VariantPath, EditorStyles.miniLabel);
                EditorGUILayout.LabelField("Base:", v.BasePrefabPath, EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();

                if (GUILayout.Button("Select", GUILayout.Width(55), GUILayout.Height(30)))
                {
                    SelectAsset(v.VariantPath);
                }

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndScrollView();
        }

        // ────────────────────────────────────────────────────────────
        // Tab 2: Missing References
        // ────────────────────────────────────────────────────────────

        private void DrawMissingReferencesTab()
        {
            EditorGUILayout.HelpBox(
                "Scans all project prefabs for broken references: missing scripts (deleted/renamed MonoBehaviours), " +
                "missing nested prefabs (deleted prefab instances inside a prefab), and missing object references " +
                "(fields pointing to assets that no longer exist).",
                MessageType.None);
            EditorGUILayout.Space(2);

            var missing = _result.MissingReferences;

            if (missing.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No missing references found in any prefab.",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField($"Found {missing.Count} prefab(s) with missing references:",
                EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            _scrollMissing = EditorGUILayout.BeginScrollView(_scrollMissing);

            foreach (var m in missing)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();

                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField(m.PrefabPath, EditorStyles.miniLabel);

                string summary = "";
                if (m.MissingScriptCount > 0)
                    summary += $"Missing Scripts: {m.MissingScriptCount}  ";
                if (m.MissingPrefabCount > 0)
                    summary += $"Missing Prefabs: {m.MissingPrefabCount}  ";
                if (m.MissingObjectRefs.Count > 0)
                    summary += $"Missing Refs: {m.MissingObjectRefs.Count}";

                EditorGUILayout.LabelField(summary, EditorStyles.boldLabel);
                EditorGUILayout.EndVertical();

                if (GUILayout.Button("Select", GUILayout.Width(55), GUILayout.Height(30)))
                {
                    SelectAsset(m.PrefabPath);
                }

                EditorGUILayout.EndHorizontal();

                // Expandable details for missing nested prefabs and object references
                if (m.MissingNestedPrefabs.Count > 0 || m.MissingObjectRefs.Count > 0)
                {
                    if (!_missingFoldouts.ContainsKey(m.PrefabPath))
                        _missingFoldouts[m.PrefabPath] = false;

                    _missingFoldouts[m.PrefabPath] = EditorGUILayout.Foldout(
                        _missingFoldouts[m.PrefabPath], "Details", true);

                    if (_missingFoldouts[m.PrefabPath])
                    {
                        EditorGUI.indentLevel++;

                        foreach (var mp in m.MissingNestedPrefabs)
                        {
                            EditorGUILayout.LabelField(
                                $"  [Missing Prefab] \"{mp.MissingGameObjectName}\" under \"{mp.ParentGameObjectName}\"",
                                EditorStyles.miniLabel);
                        }

                        foreach (var refInfo in m.MissingObjectRefs)
                        {
                            EditorGUILayout.LabelField(
                                $"  [{refInfo.GameObjectName}] {refInfo.ComponentType}.{refInfo.PropertyPath}",
                                EditorStyles.miniLabel);
                        }

                        EditorGUI.indentLevel--;
                    }
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndScrollView();
        }

        // ────────────────────────────────────────────────────────────
        // Tab 3: Orphaned Variants
        // ────────────────────────────────────────────────────────────

        private void DrawOrphanedVariantsTab()
        {
            EditorGUILayout.HelpBox(
                "Detects variant prefabs whose base (source) prefab has been deleted from the project. " +
                "When a base prefab is removed (e.g. after a Figma re-sync or manual deletion), any variant " +
                "derived from it becomes broken. These orphaned variants should be deleted or re-linked to a new base.",
                MessageType.None);
            EditorGUILayout.Space(2);

            var orphaned = _result.OrphanedVariants;

            if (orphaned.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No orphaned variants found. All variant base prefabs are intact.",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField($"Found {orphaned.Count} orphaned variant(s):",
                EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            _scrollOrphaned = EditorGUILayout.BeginScrollView(_scrollOrphaned);

            foreach (var o in orphaned)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();

                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField("Variant:", o.VariantPath, EditorStyles.miniLabel);

                if (!string.IsNullOrEmpty(o.OriginalBasePath))
                    EditorGUILayout.LabelField("Original base:", o.OriginalBasePath, EditorStyles.miniLabel);

                if (!string.IsNullOrEmpty(o.MissingBaseGuid))
                    EditorGUILayout.LabelField($"Missing GUID: {o.MissingBaseGuid}", EditorStyles.miniLabel);
                else
                    EditorGUILayout.LabelField("Missing GUID: unknown", EditorStyles.miniLabel);

                EditorGUILayout.EndVertical();

                if (GUILayout.Button("Select", GUILayout.Width(55), GUILayout.Height(30)))
                {
                    SelectAsset(o.VariantPath);
                }

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndScrollView();
        }

        // ────────────────────────────────────────────────────────────
        // Helpers
        // ────────────────────────────────────────────────────────────

        private void RunScan()
        {
            _isScanning = true;
            _missingFoldouts.Clear();
            Repaint();

            // Run scan with progress callback
            _result = PrefabHealthChecker.RunFullScan((message, progress) =>
            {
                _progressMessage = message;
                _progressValue = progress;
            });

            _isScanning = false;
            Repaint();
        }

        private static void SelectAsset(string assetPath)
        {
            var obj = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
            if (obj != null)
            {
                Selection.activeObject = obj;
                EditorGUIUtility.PingObject(obj);
            }
            else
            {
                Debug.LogWarning($"[PrefabHealthCheck] Asset not found at path: {assetPath}");
            }
        }
    }
}
