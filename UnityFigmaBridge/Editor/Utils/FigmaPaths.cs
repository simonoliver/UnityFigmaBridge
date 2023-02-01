using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            return $"{FigmaScreenPrefabFolder}/{GetScreenNameForNode(node,duplicateCount)}.prefab";
        }
        
        public static string GetPathForPagePrefab(Node node)
        {
            return $"{FigmaPagePrefabFolder}/{ReplaceUnsafeCharacters(node.name)}.prefab";
        }
        
        public static string GetPathForComponentPrefab(Node node,int duplicateCount)
        {
            //var safeFilename = $"{node.name}_{FigmaFileUtils.ReplaceUnsafeFileCharactersForNodeId(node.id)}";
            var nodeName = $"{node.name}";
            // If name already used, create a unique name
            if (duplicateCount > 0) nodeName += $"_{duplicateCount}";
            nodeName = ReplaceUnsafeCharacters(nodeName);
            return $"{FigmaComponentPrefabFolder}/{nodeName}.prefab";
        }
        
        public static string GetScreenNameForNode(Node node,int duplicateCount)
        {
            var safeNodeTitle=ReplaceUnsafeCharacters(node.name);
            // If name already used, create a unique name
            if (duplicateCount > 0) safeNodeTitle += $"_{duplicateCount}";
            return safeNodeTitle;
        }

        private static string ReplaceUnsafeCharacters(string inputFilename)
        {
            var safeFilename=inputFilename;
            // Remove path extensions
            var split = safeFilename.Split('/');
            safeFilename = split.Last();
            return MakeValidFileName(safeFilename);
            //safeFilename = safeFilename.Replace(".", "_");
            //safeFilename = safeFilename.Replace(" ", "_");
            //safeFilename = safeFilename.Replace(":", "_");
            //return safeFilename;
        }
        
        // From https://www.csharp-console-examples.com/general/c-replace-invalid-filename-characters/
        public static string MakeValidFileName(string name)
        {
            string invalidChars = System.Text.RegularExpressions.Regex.Escape(new string(System.IO.Path.GetInvalidFileNameChars()));
            string invalidRegStr = string.Format(@"([{0}]*\.+$)|([{0}]+)", invalidChars);
 
            return System.Text.RegularExpressions.Regex.Replace(name, invalidRegStr, "_");
        }

        public static void CreateRequiredDirectories()
        {
            
            //  Create directory for pages if required 
            if (!Directory.Exists(FigmaPagePrefabFolder))
            {
                Directory.CreateDirectory(FigmaPagePrefabFolder);
            }
            // Remove existing prefabs for pagwes
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