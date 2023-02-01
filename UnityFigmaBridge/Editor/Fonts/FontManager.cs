using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityFigmaBridge.Editor.FigmaApi;
using UnityFigmaBridge.Editor.Utils;
using Color = UnityEngine.Color;
using MathUtils = UnityFigmaBridge.Editor.Utils.MathUtils;

namespace UnityFigmaBridge.Editor.Fonts
{
    
    public class FigmaFontMapEntry
    {
        public string FontFamily;
        public int FontWeight;
        public TMP_FontAsset FontAsset;
        public List<FontMaterialVariation> FontmaterialVariations = new List<FontMaterialVariation>();
    }
    
    
    /// <summary>
    /// Class to map text effects (outline and shadow) to material presets
    /// </summary>
    public class FontMaterialVariation
    {
        public bool OutlineEnabled;
        public Color OutlineColor;
        public float OutlineThickness;
        
        public bool ShadowEnabled;
        public Color ShadowColor;
        public Vector2 ShadowDistance;
        
        public Material MaterialPreset;
       
    }
    
    
    public class FigmaFontMap
    {
        public List<FigmaFontMapEntry> FontMapEntries = new List<FigmaFontMapEntry>();

        public FigmaFontMapEntry GetFontMapping(string fontFamily, int fontWeight)
        {
            return FontMapEntries.FirstOrDefault(fontMapEntry => fontMapEntry.FontFamily == fontFamily && fontMapEntry.FontWeight == fontWeight);
        }
    }
    
    /// <summary>
    /// Functionality to manage fonts, retrive and generate font assets
    /// </summary>
    public static class FontManager
    {
        /// <summary>
        /// Generates a map of fonts found int the document and font to map to
        /// </summary>
        /// <param name="figmaFile"></param>
        /// <param name="enableGoogleFontsDownload"></param>
        /// <returns></returns>
        public static async Task<FigmaFontMap> GenerateFontMapForDocument(FigmaFile figmaFile, bool enableGoogleFontsDownload)
        {
            FigmaFontMap fontMap = new FigmaFontMap();
            var textNodes = new List<Node>();
            FigmaDataUtils.FindAllNodesOfType(figmaFile.document,NodeType.TEXT, textNodes, 0);
            
            var allProjectFontAssets = AssetDatabase.FindAssets($"t:TMP_FontAsset").Select(guid => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid))).ToList();

            // Cycle through each node, to see if we have a match for each
            foreach (var textNode in textNodes)
            {
                var fontFamily = textNode.style.fontFamily;
                var fontWeight = textNode.style.fontWeight;
                var fontMapEntry = fontMap.GetFontMapping(fontFamily, fontWeight);
                if (fontMapEntry != null) continue;
                
                var newFontMapEntry = new FigmaFontMapEntry
                {
                    FontFamily = fontFamily,
                    FontWeight = fontWeight
                };
                fontMap.FontMapEntries.Add(newFontMapEntry);
                if (GoogleFontLibraryManager.CheckFontExistsLocally(fontFamily, fontWeight))
                {
                    newFontMapEntry.FontAsset = GoogleFontLibraryManager.GetFontAsset(fontFamily, fontWeight);
                }
                else if (enableGoogleFontsDownload && GoogleFontLibraryManager.CheckFontAvailableForDownload(fontFamily, fontWeight))
                {
                    var downloadTask = GoogleFontLibraryManager.ImportFont(fontFamily, fontWeight);
                    await downloadTask;
                    if (downloadTask.Result)
                    {
                        // Success
                        newFontMapEntry.FontAsset=GoogleFontLibraryManager.GetFontAsset(fontFamily, fontWeight);
                    }
                }

                if (newFontMapEntry.FontAsset == null)
                    newFontMapEntry.FontAsset = GetClosestFont(allProjectFontAssets,fontFamily,fontWeight);
                
                
                // TODO - We might want to handle generation of material variations here too
            }

            return fontMap;
        }
        
        
        
        static string StripFontDetailsFromName(TMP_FontAsset fontAsset)
        {
            // By default fonts are added with a hyphen to denote weight variations, so strip everything from hyphen
            var fontName = fontAsset.name.ToLower();
            var hyphenPoint = fontName.IndexOf('-');
            if (hyphenPoint > -1) fontName = fontName.Substring(0, hyphenPoint);
            // Remove any extra keywords
            var stripWords = new string[]
            {
                "sdf",
                "regular",
                "bold",
                "italic",
                " "
            };
            foreach (var stripWord in stripWords)
            {
                fontName= fontName.Replace(stripWord, "");
            }
            return fontName;
        }

        private static TMP_FontAsset GetClosestFont(List<TMP_FontAsset> projectFonts,string fontFamily,int fontWeight)
        {
            var lowestMatchScore = 10000000;
            TMP_FontAsset closestMatch = null;
            
            // Make lower case and strip spaces
            var inputNameLower = fontFamily.ToLower().Replace(" ", "");;
            
            // Use Levenshtein distance to calculate best match from available strings
            foreach (var font in projectFonts)
            {
                var strippedFontName = StripFontDetailsFromName(font);
               
                var newScore = MathUtils.LeventshteinStringDistance(inputNameLower, strippedFontName);
                //Debug.Log($"Checking font name {strippedFontName} vs {inputNameLower} score {newScore}");
                if (newScore < lowestMatchScore)
                {
                    closestMatch = font;
                    lowestMatchScore = newScore;
                }
            }
            return closestMatch;
        }
        
        public static Material GetEffectMaterialPreset(FigmaFontMapEntry fontMapEntry, bool shadow, Color shadowColor,
            Vector2 shadowDistance, bool outline,
            Color outlineColor, float outlineThickness)
        {
            // Do we have a matching material?
            var materialPresets = fontMapEntry.FontmaterialVariations.Count;
            
            
            foreach (var materialPreset in fontMapEntry.FontmaterialVariations)
            {
                bool isMatch = true;
                if (materialPreset.ShadowEnabled != shadow) isMatch = false;
                if (shadow && materialPreset.ShadowColor!=shadowColor) isMatch = false;
                if (shadow && materialPreset.ShadowDistance!=shadowDistance) isMatch = false;
                
                if (materialPreset.OutlineEnabled != outline) isMatch = false;
                if (outline && materialPreset.OutlineColor != outlineColor) isMatch = false;
                if (outline && materialPreset.OutlineThickness != outlineThickness) isMatch = false;

                if (isMatch) return materialPreset.MaterialPreset;
            }
            // No match, create new preset
            var newMaterialPreset = new Material(fontMapEntry.FontAsset.material);
            // We use a modified shader that handles distance from edge better
            newMaterialPreset.shader = Shader.Find("Figma/TextMeshPro");
            
            var materialName = $"{fontMapEntry.FontAsset.name}_variant_{materialPresets}";
            newMaterialPreset.name = materialName;

            newMaterialPreset.SetKeyword(new LocalKeyword(newMaterialPreset.shader,"UNDERLAY_ON"),shadow);
            
            if (shadow)
            {
                newMaterialPreset.SetFloat("_UnderlayOffsetX",0);
                newMaterialPreset.SetFloat("_UnderlayOffsetY",-0.6f);
                newMaterialPreset.SetColor("_UnderlayColor",shadowColor);
            }
            
            newMaterialPreset.SetKeyword(new LocalKeyword(newMaterialPreset.shader,"OUTLINE_ON"),outline);

            if (outline)
            {
                // For now we'll just use a fixed value as this is proportional to font size not a fixed value
                // Note we are using a modified shader to ensure outline is outside
                
                newMaterialPreset.SetFloat("_OutlineWidth",outlineThickness);
                newMaterialPreset.SetColor("_OutlineColor",outlineColor);
            }
            
            AssetDatabase.CreateAsset(newMaterialPreset, $"{FigmaPaths.FigmaFontMaterialPresetsFolder}/{materialName}.mat");

            fontMapEntry.FontmaterialVariations.Add(new FontMaterialVariation
            {
                ShadowEnabled=shadow,
                ShadowColor = shadowColor,
                ShadowDistance = shadowDistance,
                OutlineEnabled = outline,
                OutlineColor = outlineColor,
                OutlineThickness = outlineThickness,
                MaterialPreset = newMaterialPreset
            });
            return newMaterialPreset;
        }
        
    }
}
