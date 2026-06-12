using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityFigmaBridge.Editor.FigmaApi;

namespace UnityFigmaBridge.Editor.Utils
{
    public static class FigmaPaths
    {
        /// <summary>
        ///  Root folder for assets (can be overridden via settings)
        /// </summary>
        private static string s_FigmaAssetsRootFolder = "Assets/Figma";
        
        public static string FigmaAssetsRootFolder 
        { 
            get => s_FigmaAssetsRootFolder;
            set => s_FigmaAssetsRootFolder = value;
        }
        /// <summary>
        /// Assert folder to store page prefabs
        /// </summary>
        public static string FigmaPagePrefabFolder => $"{FigmaAssetsRootFolder}/Pages";
        /// <summary>
        /// Assert folder to store flowScreen prefabs (root level frames on pages)
        /// </summary>
        public static string FigmaScreenPrefabFolder => $"{FigmaAssetsRootFolder}/Screens";
        /// <summary>
        /// Assert folder to store compoment prefabs
        /// </summary>
        public static string FigmaComponentPrefabFolder => $"{FigmaAssetsRootFolder}/Components";
        /// <summary>
        /// Asset folder to store image fills
        /// </summary>
        public static string FigmaImageFillFolder => $"{FigmaAssetsRootFolder}/ImageFills";
        /// <summary>
        /// Asset folder to store server rendered images
        /// </summary>
        public static string FigmaServerRenderedImagesFolder => $"{FigmaAssetsRootFolder}/ServerRenderedImages";
        
        /// <summary>
        /// Asset folder to store Font material presets
        /// </summary>
        public static string FigmaFontMaterialPresetsFolder => $"{FigmaAssetsRootFolder}/FontMaterialPresets";
        
        /// <summary>
        /// Asset folder to store Font assets (TTF and generated TMP fonts)
        /// </summary>
        public static string FigmaFontsFolder => $"{FigmaAssetsRootFolder}/Fonts";
        
        
        public static string GetPathForImageFill(string imageId)
        {
            return $"{FigmaPaths.FigmaImageFillFolder}/{imageId}.png";
        }
        
        public static string GetPathForServerRenderedImage(string nodeId,
            List<ServerRenderNodeData> serverRenderNodeData)
        {
            var matchingEntry = serverRenderNodeData.FirstOrDefault((node) => node.SourceNode.id == nodeId);
            switch (matchingEntry.RenderType)
            {
                case ServerRenderType.Export:
                    return $"Assets/{matchingEntry.SourceNode.name}.png";
                default:
                    var safeNodeId = FigmaDataUtils.ReplaceUnsafeFileCharactersForNodeId(nodeId);
                    return $"{FigmaPaths.FigmaServerRenderedImagesFolder}/{safeNodeId}.png";
                   
            }
        }

        public static string GetPathForScreenPrefab(Node node,int duplicateCount)
        {
            return $"{FigmaScreenPrefabFolder}/{GetFileNameForNode(node,duplicateCount)}.prefab";
        }
        
        public static string GetPathForPagePrefab(Node node,int duplicateCount)
        {
            return $"{FigmaPagePrefabFolder}/{GetFileNameForNode(node,duplicateCount)}.prefab";
        }
        
        public static string GetPathForComponentPrefab(string nodeName,int duplicateCount)
        {
            // If name already used, create a unique name
            if (duplicateCount > 0) nodeName += $"_{duplicateCount}";
            nodeName = ReplaceUnsafeCharacters(nodeName);
            return $"{FigmaComponentPrefabFolder}/{nodeName}.prefab";
        }
        
        public static string GetFileNameForNode(Node node,int duplicateCount)
        {
            var safeNodeTitle=ReplaceUnsafeCharacters(node.name);
            // If name already used, create a unique name
            if (duplicateCount > 0) safeNodeTitle += $"_{duplicateCount}";
            return safeNodeTitle;
        }

        private static string ReplaceUnsafeCharacters(string inputFilename)
        {
            // We want to trim spaces from start and end of filename, or we'll throw an error
            // We no longer want to use the final "/" character as this might be used by the user
            var safeFilename=inputFilename.Trim();
            return MakeValidFileName(safeFilename);
        }
        
        // From https://www.csharp-console-examples.com/general/c-replace-invalid-filename-characters/
        public static string MakeValidFileName(string name)
        {
            string invalidChars = System.Text.RegularExpressions.Regex.Escape(new string(Path.GetInvalidFileNameChars()));
            invalidChars += ".";
            string invalidRegStr = string.Format(@"([{0}]*\.+$)|([{0}]+)", invalidChars);
 
            return System.Text.RegularExpressions.Regex.Replace(name, invalidRegStr, "_");
        }

        public static void CreateRequiredDirectories(FigmaImportProcessData importProcessData = null)
        {
            
            //  Create directory for pages if required 
            if (!Directory.Exists(FigmaPagePrefabFolder))
            {
                Directory.CreateDirectory(FigmaPagePrefabFolder);
            }
            else if (importProcessData != null)
            {
                // Capture existing page prefab paths before they get replaced
                var pageDir = new DirectoryInfo(FigmaPagePrefabFolder);
                foreach (var file in pageDir.GetFiles())
                {
                    if (file.Extension == ".prefab")
                        importProcessData.OldPagePrefabPaths.Add(file.FullName);
                }
            }
            
            //  Create directory for flowScreen prefabs if required 
            if (!Directory.Exists(FigmaScreenPrefabFolder))
            {
                Directory.CreateDirectory(FigmaScreenPrefabFolder);
            }
            else if (importProcessData != null)
            {
                // Capture existing screen prefab paths before they get replaced
                var screenDir = new DirectoryInfo(FigmaScreenPrefabFolder);
                foreach (FileInfo file in screenDir.GetFiles())
                {
                    if (file.Extension == ".prefab")
                        importProcessData.OldScreenPrefabPaths.Add(file.FullName);
                }
            }
            
            if (!Directory.Exists(FigmaComponentPrefabFolder))
            {
                Directory.CreateDirectory(FigmaComponentPrefabFolder);
            }
            
            //  Create directory for image fills if required 
            if (!Directory.Exists(FigmaImageFillFolder))
            {
                Directory.CreateDirectory(FigmaImageFillFolder);
            }
            
            //  Create directory for server rendered images if required 
            if (!Directory.Exists(FigmaServerRenderedImagesFolder))
            {
                Directory.CreateDirectory(FigmaServerRenderedImagesFolder);
            }

            if (!Directory.Exists(FigmaFontMaterialPresetsFolder))
            {
                Directory.CreateDirectory(FigmaFontMaterialPresetsFolder);
            }
            
            if (!Directory.Exists(FigmaFontsFolder))
            {
                Directory.CreateDirectory(FigmaFontsFolder);
            }
        }
        
        /// <summary>
        /// Delete orphaned prefabs that were in the old list but not in the new list
        /// </summary>
        public static void CleanupOrphanedPrefabs(FigmaImportProcessData importProcessData)
        {
            if (importProcessData == null)
                return;
                
            // Get all current screen prefab paths
            var currentScreenPaths = new HashSet<string>();
            foreach (var screenPrefab in importProcessData.ScreenPrefabs)
            {
                var path = UnityEditor.AssetDatabase.GetAssetPath(screenPrefab);
                if (!string.IsNullOrEmpty(path))
                    currentScreenPaths.Add(System.IO.Path.GetFullPath(path));
            }
            
            // Get all current page prefab paths
            var currentPagePaths = new HashSet<string>();
            foreach (var pagePrefab in importProcessData.PagePrefabs)
            {
                var path = UnityEditor.AssetDatabase.GetAssetPath(pagePrefab);
                if (!string.IsNullOrEmpty(path))
                    currentPagePaths.Add(System.IO.Path.GetFullPath(path));
            }
            
            // Delete screen prefabs that no longer exist in Figma
            foreach (var oldPath in importProcessData.OldScreenPrefabPaths)
            {
                if (!currentScreenPaths.Contains(oldPath))
                {
                    if (System.IO.File.Exists(oldPath))
                    {
                        System.IO.File.Delete(oldPath);
                        var metaPath = oldPath + ".meta";
                        if (System.IO.File.Exists(metaPath))
                            System.IO.File.Delete(metaPath);
                        UnityEngine.Debug.Log($"Deleted orphaned screen prefab: {oldPath}");
                    }
                }
            }
            
            // Delete page prefabs that no longer exist in Figma
            foreach (var oldPath in importProcessData.OldPagePrefabPaths)
            {
                if (!currentPagePaths.Contains(oldPath))
                {
                    if (System.IO.File.Exists(oldPath))
                    {
                        System.IO.File.Delete(oldPath);
                        var metaPath = oldPath + ".meta";
                        if (System.IO.File.Exists(metaPath))
                            System.IO.File.Delete(metaPath);
                        UnityEngine.Debug.Log($"Deleted orphaned page prefab: {oldPath}");
                    }
                }
            }
        }
        
    }
}