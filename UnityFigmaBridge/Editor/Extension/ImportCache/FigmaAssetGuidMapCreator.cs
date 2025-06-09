using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace UnityFigmaBridge.Editor.Extension.ImportCache
{
    public static class FigmaAssetGuidMapCreator
    {
        public static string ComponentCacheDataDir = "Assets/Figma/Custom/Cache/";
        public enum AssetType
        {
            Component,
        }

        public static Dictionary<string, FigmaAssetGuidMap> _mapContenar = new Dictionary<string, FigmaAssetGuidMap>();

        public static FigmaAssetGuidMap CreateMap(AssetType assetType)
        {
            var path = ComponentCacheDataDir + assetType.ToString() + ".asset";
            if (_mapContenar.TryGetValue(path, out var map))
            {
                return map;
            }
            
            map = ScriptableObject.CreateInstance<FigmaAssetGuidMap>();
            _mapContenar.Add(path, map);
            
            // ディレクトリなければ生成
            if (!Directory.Exists(ComponentCacheDataDir))
            {
                Directory.CreateDirectory(ComponentCacheDataDir);
            }
            
            AssetDatabase.CreateAsset(map, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return map;
        }

        public static void SaveAllMap()
        {
            AssetDatabase.SaveAssets();
            _mapContenar.Clear();
        }
    }
}