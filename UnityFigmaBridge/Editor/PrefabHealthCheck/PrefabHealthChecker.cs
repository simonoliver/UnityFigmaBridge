using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityFigmaBridge.Editor.Utils;

namespace UnityFigmaBridge.Editor.PrefabHealthCheck
{
    /// <summary>
    /// Core scanning logic for prefab health checks.
    /// Detects Figma variants, missing references, and orphaned variants.
    ///
    /// Can be used as an API for scripting and automation:
    ///   var result = PrefabHealthChecker.RunFullScan();
    ///   var variants = PrefabHealthChecker.ScanFigmaVariants();
    ///   var missing  = PrefabHealthChecker.ScanMissingReferences();
    ///   var orphaned = PrefabHealthChecker.ScanOrphanedVariants();
    /// </summary>
    public static class PrefabHealthChecker
    {
        /// <summary>
        /// Run all health checks and return the combined result.
        /// </summary>
        /// <param name="onProgress">Optional callback (message, 0-1 progress).</param>
        public static PrefabHealthCheckResult RunFullScan(Action<string, float> onProgress = null)
        {
            var result = new PrefabHealthCheckResult();

            try
            {
                onProgress?.Invoke("Collecting Figma source prefabs...", 0f);
                var figmaSourcePaths = CollectFigmaSourcePrefabs();

                onProgress?.Invoke("Collecting all project prefabs...", 0.05f);
                var allPrefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });

                // Phase 1: Find Figma variants
                onProgress?.Invoke("Scanning for Figma variants...", 0.1f);
                FindFigmaVariants(allPrefabGuids, figmaSourcePaths, result, onProgress, 0.1f, 0.4f);

                // Phase 2: Detect missing references
                onProgress?.Invoke("Scanning for missing references...", 0.4f);
                FindMissingReferences(allPrefabGuids, result, onProgress, 0.4f, 0.75f);

                // Phase 3: Detect orphaned variants
                onProgress?.Invoke("Scanning for orphaned variants...", 0.75f);
                FindOrphanedVariants(allPrefabGuids, result, onProgress, 0.75f, 1f);

                result.IsComplete = true;
                onProgress?.Invoke("Scan complete.", 1f);
            }
            catch (Exception e)
            {
                Debug.LogError($"[PrefabHealthCheck] Scan failed: {e}");
            }

            return result;
        }

        /// <summary>
        /// Scan only for Figma-derived variant prefabs.
        /// Returns a list of all project prefabs that are variants of a Figma source prefab.
        /// </summary>
        /// <param name="onProgress">Optional callback (message, 0-1 progress).</param>
        public static List<FigmaVariantInfo> ScanFigmaVariants(Action<string, float> onProgress = null)
        {
            var result = new PrefabHealthCheckResult();
            var figmaSourcePaths = CollectFigmaSourcePrefabs();
            var allPrefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
            FindFigmaVariants(allPrefabGuids, figmaSourcePaths, result, onProgress, 0f, 1f);
            return result.FigmaVariants;
        }

        /// <summary>
        /// Scan only for missing references (missing scripts, missing prefabs, missing object refs).
        /// Returns a list of prefabs that contain at least one broken reference.
        /// </summary>
        /// <param name="onProgress">Optional callback (message, 0-1 progress).</param>
        public static List<MissingReferenceInfo> ScanMissingReferences(Action<string, float> onProgress = null)
        {
            var result = new PrefabHealthCheckResult();
            var allPrefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
            FindMissingReferences(allPrefabGuids, result, onProgress, 0f, 1f);
            return result.MissingReferences;
        }

        /// <summary>
        /// Scan only for orphaned variants (variants whose base prefab has been deleted).
        /// Returns a list of broken variant prefabs.
        /// </summary>
        /// <param name="onProgress">Optional callback (message, 0-1 progress).</param>
        public static List<OrphanedVariantInfo> ScanOrphanedVariants(Action<string, float> onProgress = null)
        {
            var result = new PrefabHealthCheckResult();
            var allPrefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
            FindOrphanedVariants(allPrefabGuids, result, onProgress, 0f, 1f);
            return result.OrphanedVariants;
        }

        /// <summary>
        /// Check if the project has any health issues at all (quick boolean check).
        /// Runs a full scan and returns true if any problems were found.
        /// </summary>
        public static bool HasAnyIssues()
        {
            var result = RunFullScan();
            return result.MissingReferences.Count > 0 || result.OrphanedVariants.Count > 0;
        }

        /// <summary>
        /// Collect all prefab asset paths from Figma source folders (Pages, Screens, Components).
        /// </summary>
        public static HashSet<string> CollectFigmaSourcePrefabs()
        {
            var sourcePaths = new HashSet<string>();

            string[] figmaFolders =
            {
                FigmaPaths.FigmaPagePrefabFolder,
                FigmaPaths.FigmaScreenPrefabFolder,
                FigmaPaths.FigmaComponentPrefabFolder
            };

            foreach (var folder in figmaFolders)
            {
                if (!AssetDatabase.IsValidFolder(folder))
                    continue;

                var guids = AssetDatabase.FindAssets("t:Prefab", new[] { folder });
                foreach (var guid in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    sourcePaths.Add(path);
                }
            }

            return sourcePaths;
        }

        /// <summary>
        /// Find all prefabs that are variants of a Figma source prefab
        /// </summary>
        private static void FindFigmaVariants(
            string[] allPrefabGuids,
            HashSet<string> figmaSourcePaths,
            PrefabHealthCheckResult result,
            Action<string, float> onProgress,
            float progressStart,
            float progressEnd)
        {
            for (int i = 0; i < allPrefabGuids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(allPrefabGuids[i]);

                // Skip prefabs that are themselves Figma source prefabs
                if (figmaSourcePaths.Contains(path))
                    continue;

                float progress = Mathf.Lerp(progressStart, progressEnd,
                    (float)i / allPrefabGuids.Length);
                onProgress?.Invoke($"Checking variant: {Path.GetFileName(path)}", progress);

                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null)
                    continue;

                var assetType = PrefabUtility.GetPrefabAssetType(asset);
                if (assetType == PrefabAssetType.NotAPrefab)
                    continue;

                // Check if this is a variant
                if (!PrefabUtility.IsPartOfVariantPrefab(asset))
                    continue;

                var originalSource = PrefabUtility.GetCorrespondingObjectFromOriginalSource(asset);
                if (originalSource == null)
                    continue;

                var basePath = AssetDatabase.GetAssetPath(originalSource);
                if (string.IsNullOrEmpty(basePath))
                    continue;

                // Check if the base prefab is in a Figma folder
                if (figmaSourcePaths.Contains(basePath))
                {
                    result.FigmaVariants.Add(new FigmaVariantInfo
                    {
                        VariantPath = path,
                        BasePrefabPath = basePath,
                        BasePrefabName = Path.GetFileNameWithoutExtension(basePath)
                    });
                }
            }
        }

        /// <summary>
        /// Find all prefabs with missing scripts or missing object references
        /// </summary>
        private static void FindMissingReferences(
            string[] allPrefabGuids,
            PrefabHealthCheckResult result,
            Action<string, float> onProgress,
            float progressStart,
            float progressEnd)
        {
            for (int i = 0; i < allPrefabGuids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(allPrefabGuids[i]);

                float progress = Mathf.Lerp(progressStart, progressEnd,
                    (float)i / allPrefabGuids.Length);
                onProgress?.Invoke($"Checking references: {Path.GetFileName(path)}", progress);

                var info = new MissingReferenceInfo { PrefabPath = path };

                // Load prefab contents for deep inspection
                var prefabRoot = PrefabUtility.LoadPrefabContents(path);
                if (prefabRoot == null)
                    continue;

                try
                {
                    ScanGameObjectForMissing(prefabRoot, info);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                }

                if (info.TotalMissingCount > 0)
                {
                    result.MissingReferences.Add(info);
                }
            }
        }

        /// <summary>
        /// Recursively scan a GameObject and its children for missing scripts,
        /// missing object references, and missing nested prefab instances
        /// </summary>
        private static void ScanGameObjectForMissing(GameObject go, MissingReferenceInfo info)
        {
            // Check for missing scripts on this GameObject
            int missingCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
            info.MissingScriptCount += missingCount;

            // Check for missing nested prefab instances
            // A missing prefab shows up as PrefabAssetType.MissingAsset on the nested instance
            if (PrefabUtility.IsAnyPrefabInstanceRoot(go))
            {
                var nestedType = PrefabUtility.GetPrefabInstanceStatus(go);
                if (nestedType == PrefabInstanceStatus.MissingAsset)
                {
                    info.MissingPrefabCount++;
                    var parentName = go.transform.parent != null ? go.transform.parent.name : "(root)";
                    info.MissingNestedPrefabs.Add(new MissingNestedPrefab
                    {
                        ParentGameObjectName = parentName,
                        MissingGameObjectName = go.name
                    });
                }
            }

            // Check each component for missing object references
            var components = go.GetComponents<Component>();
            foreach (var component in components)
            {
                // A null component means the script is missing
                if (component == null)
                    continue;

                var so = new SerializedObject(component);
                var sp = so.GetIterator();

                while (sp.NextVisible(true))
                {
                    if (sp.propertyType != SerializedPropertyType.ObjectReference)
                        continue;

                    // objectReferenceValue is null but instanceID is non-zero => missing ref
                    if (sp.objectReferenceValue == null &&
                        sp.objectReferenceInstanceIDValue != 0)
                    {
                        info.MissingObjectRefs.Add(new MissingObjectRef
                        {
                            GameObjectName = go.name,
                            ComponentType = component.GetType().Name,
                            PropertyPath = sp.propertyPath
                        });
                    }
                }
            }

            // Recurse into children
            for (int i = 0; i < go.transform.childCount; i++)
            {
                ScanGameObjectForMissing(go.transform.GetChild(i).gameObject, info);
            }
        }

        /// <summary>
        /// Find all variant prefabs whose base prefab has been deleted (orphaned)
        /// </summary>
        private static void FindOrphanedVariants(
            string[] allPrefabGuids,
            PrefabHealthCheckResult result,
            Action<string, float> onProgress,
            float progressStart,
            float progressEnd)
        {
            // Regex to extract m_SourcePrefab guid from YAML
            var guidRegex = new Regex(@"m_SourcePrefab:\s*\{fileID:\s*\d+,\s*guid:\s*([a-f0-9]+)",
                RegexOptions.Compiled);

            for (int i = 0; i < allPrefabGuids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(allPrefabGuids[i]);

                float progress = Mathf.Lerp(progressStart, progressEnd,
                    (float)i / allPrefabGuids.Length);
                onProgress?.Invoke($"Checking orphaned: {Path.GetFileName(path)}", progress);

                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null)
                    continue;

                // Only check variant prefabs
                if (!PrefabUtility.IsPartOfVariantPrefab(asset))
                    continue;

                // Try API approach first
                var originalSource = PrefabUtility.GetCorrespondingObjectFromOriginalSource(asset);
                if (originalSource != null)
                    continue; // Base exists, not orphaned

                // Base is missing - try to extract GUID from YAML for diagnostics
                string missingGuid = "";
                string originalBasePath = "";

                try
                {
                    var fullPath = Path.GetFullPath(path);
                    var yamlContent = File.ReadAllText(fullPath);
                    var match = guidRegex.Match(yamlContent);
                    if (match.Success)
                    {
                        missingGuid = match.Groups[1].Value;
                        // Try to find what path this GUID used to point to
                        originalBasePath = AssetDatabase.GUIDToAssetPath(missingGuid);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[PrefabHealthCheck] Could not read YAML for {path}: {e.Message}");
                }

                result.OrphanedVariants.Add(new OrphanedVariantInfo
                {
                    VariantPath = path,
                    MissingBaseGuid = missingGuid,
                    OriginalBasePath = originalBasePath
                });
            }
        }
    }
}
