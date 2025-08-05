using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityFigmaBridge.Editor.Extension.ImportCache;
using UnityFigmaBridge.Editor.FigmaApi;

namespace UnityFigmaBridge.Editor.Utils
{
    public static class FigmaPaths
    {
        /// <summary>
        ///  Root folder for assets
        /// </summary>
        public static string FigmaAssetsRootFolder = "Assets/Figma";
        /// <summary>
        /// Assert folder to store page prefabs)
        /// </summary>
        public static string FigmaPagePrefabFolder = $"{FigmaAssetsRootFolder}/Pages";
        /// <summary>
        /// Assert folder to store flowScreen prefabs (root level frames on pages)
        /// </summary>
        public static string FigmaScreenPrefabFolder = $"{FigmaAssetsRootFolder}/Screens";
        /// <summary>
        /// Assert folder to store compoment prefabs
        /// </summary>
        public static string FigmaComponentPrefabFolder = $"{FigmaAssetsRootFolder}/Components";
        /// <summary>
        /// Asset folder to store image fills
        /// </summary>
        public static string FigmaImageFillFolder = $"{FigmaAssetsRootFolder}/ImageFills";
        /// <summary>
        /// Asset folder to store server rendered images
        /// </summary>
        public static string FigmaServerRenderedImagesFolder = $"{FigmaAssetsRootFolder}/ServerRenderedImages";
        
        /// <summary>
        /// Asset folder to store Font material presets
        /// </summary>
        public static string FigmaFontMaterialPresetsFolder = $"{FigmaAssetsRootFolder}/FontMaterialPresets";
        
        /// <summary>
        /// Asset folder to store Font assets (TTF and generated TMP fonts)
        /// </summary>
        public static string FigmaFontsFolder = $"{FigmaAssetsRootFolder}/Fonts";
        
        /// <summary>
        /// 拡張で使用するフォルダ
        /// </summary>
        private static string FigmaCustomFolder = $"{FigmaAssetsRootFolder}/Custom";
        
        /// <summary>
        /// バックアップを取るのに使用するフォルダ
        /// </summary>
        private static string FigmaCustomBackupFolder = $"{FigmaCustomFolder}/Backup";

        
        /// <summary>
        /// ImageFillのIDとGUIDを結びつけるデータ
        /// </summary>
        private static FigmaAssetGuidMapData imageAssetGuidMapData;
        private static FigmaAssetGuidMapData ImageAssetGuidMapData
        {
            get
            {
                if (imageAssetGuidMapData == null)
                {
                    imageAssetGuidMapData =
                        FigmaAssetGuidMapManager.GetMap(FigmaAssetGuidMapManager.AssetType.ImageFill);
                }

                return imageAssetGuidMapData;
            }
        }
        public static string GetPathForImageFill(string imageId, string imageName)
        {
            var mapFilePath = ImageAssetGuidMapData?.GetAssetPath(imageId);
            if (string.IsNullOrEmpty(mapFilePath))
            {
                return $"{FigmaPaths.FigmaImageFillFolder}/{imageName}.png";
            }
            return mapFilePath;
        }
        
        public static string GetPathForServerRenderedImage(string nodeId,
            List<ServerRenderNodeData> serverRenderNodeData)
        {
            var matchingEntry = serverRenderNodeData.FirstOrDefault((node) => node.SourceNode.id == nodeId);
            switch (matchingEntry.RenderType)
            {
                case ServerRenderType.Export:
                {
                    var mapFilePath = ImageAssetGuidMapData?.GetAssetPath(matchingEntry.SourceNode.name);
                    if (!string.IsNullOrEmpty(mapFilePath))
                    {
                        return mapFilePath;
                    }
                    return $"Assets/{matchingEntry.SourceNode.name}.png";
                }
                default:
                {
                    var mapFilePath = ImageAssetGuidMapData?.GetAssetPath(nodeId);
                    if (!string.IsNullOrEmpty(mapFilePath))
                    {
                        return mapFilePath;
                    }
                    var safeNodeId = FigmaDataUtils.ReplaceUnsafeFileCharactersForNodeId(nodeId);
                    return $"{FigmaPaths.FigmaServerRenderedImagesFolder}/{safeNodeId}.png";
                }
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

        // バックアップ用のパス生成
        public static string MakeBackupPath(string sourceAssetPath)
        {
            return FigmaCustomBackupFolder + '/' + sourceAssetPath;
        }

        public static void CreateRequiredDirectories()
        {
            
            //  Create directory for pages if required 
            if (!Directory.Exists(FigmaPagePrefabFolder))
            {
                Directory.CreateDirectory(FigmaPagePrefabFolder);
            }

            // Remove existing prefabs for pages
            foreach (var file in new DirectoryInfo(FigmaPagePrefabFolder).GetFiles())
            {
                file.Delete(); 
            }
            
            //  Create directory for flowScreen prefabs if required 
            if (!Directory.Exists(FigmaScreenPrefabFolder))
            {
                Directory.CreateDirectory(FigmaScreenPrefabFolder);
            }
            // Remove existing flowScreen prefabs
            foreach (FileInfo file in  new DirectoryInfo(FigmaScreenPrefabFolder).GetFiles())
            {
                file.Delete(); 
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
        
    }
}