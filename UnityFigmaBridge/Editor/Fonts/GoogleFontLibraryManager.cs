using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using UnityFigmaBridge.Editor.Utils;

namespace UnityFigmaBridge.Editor.Fonts
{
    
    // Using font list from https://github.com/jonathantneal/google-fonts-complete
    
    [Serializable]
    public class GoogleFontUrlList
    {
        public string eot;
        public string svg;
        public string ttf;
        public string woff;
        public string woff2;
    }
    
    [Serializable]
    public class GoogleFontVariant
    {
        public GoogleFontUrlList url;
    }

    [Serializable]
    public class GoogleFontVariantList
    {
        public Dictionary<string, GoogleFontVariant> normal;
    }
    
    [Serializable]
    public class GoogleFontDefinition
    {
        public string category;
        public string lastModified;
        public string[] subsets;
        public GoogleFontVariantList variants;
    }
    
    
    public static class GoogleFontLibraryManager
    {
        private static Dictionary<string, GoogleFontDefinition> s_FontDefinitions;
        
        public static void LoadFontData()
        {
            // If already loaded, ignore
            if (s_FontDefinitions != null) return;
            var fontDataFile = AssetDatabase.LoadAssetAtPath("Packages/com.simonoliver.unityfigma/UnityFigmaBridge/Assets/google-fonts.json", typeof(TextAsset)) as TextAsset;
            Debug.Log($"Font data loaded {fontDataFile.text.Length}");
            s_FontDefinitions = JsonConvert.DeserializeObject<Dictionary<string, GoogleFontDefinition>>(fontDataFile.text);
            Debug.Log($"Fonts found {s_FontDefinitions.Count}");
        }
        
        /// <summary>
        /// Path to a TTF font downloaded from Google Fonts
        /// </summary>
        /// <param name="fontName"></param>
        /// <param name="fontWeight"></param>
        /// <returns></returns>
        public static string PathToTtfFont(string fontName, int fontWeight)
        {
            return $"{FigmaPaths.FigmaFontsFolder}/{CombinedFontName(fontName,fontWeight)}.ttf";
        }
        
        /// <summary>
        /// Path to a Text Mesh Pro font generated from a Google Font
        /// </summary>
        /// <param name="fontName"></param>
        /// <param name="fontWeight"></param>
        /// <returns></returns>
        public static string PathToTmpFont(string fontName, int fontWeight)
        {
            return $"{FigmaPaths.FigmaFontsFolder}/{CombinedFontName(fontName,fontWeight)}_SDF.asset";
        }

        /// <summary>
        /// Check to see if a matching font exists
        /// </summary>
        /// <param name="fontName"></param>
        /// <param name="fontWeight"></param>
        /// <returns></returns>
        public static bool CheckFontExistsLocally(string fontName, int fontWeight)
        {
            return File.Exists(PathToTmpFont(fontName, fontWeight));
        }


        public static TMP_FontAsset GetFontAsset(string fontName, int fontWeight)
        {
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(PathToTmpFont(fontName, fontWeight));
        }

        public static string CombinedFontName(string fontName, int fontWeight)
        {
            return $"{fontName}_{fontWeight}";
        }

        public static async Task<bool> ImportFont(string fontName, int fontWeight)
        {
            // Ensure font data loaded
            LoadFontData();
            var fontDownloadUrl = GetFontUrl(fontName,fontWeight);
            if (string.IsNullOrEmpty(fontDownloadUrl))
            {
                Debug.Log("Cant download font as not found");
                return false;
            }

            Debug.Log($"Downloading font {fontName} at url {fontDownloadUrl}");
            
            
            var webRequest = UnityWebRequest.Get(fontDownloadUrl);
            await webRequest.SendWebRequest();

            if (!(webRequest.result is UnityWebRequest.Result.Success))
            {
                Debug.LogWarning($"Error downloading font {webRequest.error}");
                return false;
            }
            
            Debug.Log($"Received font file  - size {webRequest.downloadHandler.data.Length}");


            var fontFilePath = PathToTtfFont(fontName, fontWeight);
            File.WriteAllBytes(fontFilePath,webRequest.downloadHandler.data);
            
            AssetDatabase.ImportAsset(fontFilePath);
            AssetDatabase.Refresh();

            var ttfFontAsset = AssetDatabase.LoadAssetAtPath<Font>(fontFilePath);
            if (ttfFontAsset == null)
            {
                Debug.LogError("Problem loading downloaded font");
                return false;
            }
            
            // Generate font
            // Some info here
            // https://forum.unity.com/threads/generate-font-asset-via-script.1057043/
            // And reference from TMPro_FontAssetCreatorWindow
            
            var tmpFontAsset=TMP_FontAsset.CreateFontAsset(ttfFontAsset);
            var tmpFilePath = PathToTmpFont(fontName, fontWeight);
            
            AssetDatabase.CreateAsset(tmpFontAsset, tmpFilePath);

            tmpFontAsset.material.name = $"{CombinedFontName(fontName, fontWeight)} Atlas Material";
            tmpFontAsset.atlasTexture.name = $"{CombinedFontName(fontName, fontWeight)} Atlas";
            
            AssetDatabase.AddObjectToAsset(tmpFontAsset.material, tmpFontAsset);
            AssetDatabase.AddObjectToAsset(tmpFontAsset.atlasTexture, tmpFontAsset);
            
            EditorUtility.SetDirty(tmpFontAsset);

            AssetDatabase.SaveAssets();

            // Add basic characters
            TextMeshProFontUtils.AddBasicCharacterSetToFont(tmpFontAsset);
            
            EditorUtility.SetDirty(tmpFontAsset);
            AssetDatabase.SaveAssets();

            return true;
        }


        private static string GetFontUrl(string fontName, int fontWeight)
        {
            LoadFontData();
            var fontDefinition = GetFontDefinition(fontName);
            if (fontDefinition == null)
            {
                Debug.LogWarning($"No matching font {fontName}");
                return string.Empty;
            }

            if (fontDefinition.variants == null)
            {
                Debug.LogWarning($"No variant data for font {fontName}");
                return string.Empty;
            }
            if (fontDefinition.variants.normal == null) return string.Empty;
            if (!fontDefinition.variants.normal.ContainsKey(fontWeight.ToString()))
            {
                Debug.LogWarning($"No matching weight {fontWeight} for font {fontName}");
                return string.Empty;
            }
            var variant = fontDefinition.variants.normal[fontWeight.ToString()];
            return variant.url.ttf;
        }
        

        
        private static GoogleFontDefinition GetFontDefinition(string fontName)
        {
            LoadFontData();
            // get case insensitive match
            return (from fontKeyPair in s_FontDefinitions where string.Equals(fontKeyPair.Key, fontName, StringComparison.CurrentCultureIgnoreCase) select fontKeyPair.Value).FirstOrDefault();
        }

        /// <summary>
        /// Checks through our table of google fonts to see if we have a match
        /// </summary>
        /// <param name="fontFamily"></param>
        /// <param name="fontWeight"></param>
        /// <returns></returns>
        public static bool CheckFontAvailableForDownload(string fontFamily, int fontWeight)
        {
            LoadFontData();
            return !string.IsNullOrEmpty(GetFontUrl(fontFamily, fontWeight));
        }
    }
}
