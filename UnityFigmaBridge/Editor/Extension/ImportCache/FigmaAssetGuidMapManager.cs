using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace UnityFigmaBridge.Editor.Extension.ImportCache
{
    public static class FigmaAssetGuidMapManager
    {
        public static string ComponentCacheDataDir = "Assets/Figma/Custom/Cache/";
        public enum AssetType
        {
            Component,
        }

        public static Dictionary<string, FigmaAssetGuidMapData> _mapContenar = new Dictionary<string, FigmaAssetGuidMapData>();

        public static FigmaAssetGuidMapData CreateMap(AssetType assetType)
        {
            var path = ComponentCacheDataDir + assetType.ToString() + ".asset";
            if (_mapContenar.TryGetValue(path, out var map))
            {
                _mapContenar.TryAdd(path, map);
                return map;
            }

            map = AssetDatabase.LoadAssetAtPath<FigmaAssetGuidMapData>(path);
            if (map)
            {
                map.Initialize(); // 作成済みのファイルをロードした時だけ初期化
                _mapContenar.TryAdd(path, map);
                return map;
            }
            
            map = ScriptableObject.CreateInstance<FigmaAssetGuidMapData>();
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
            foreach (var map in _mapContenar)
            {
                map.Value.FinalizeMap();
            }
            AssetDatabase.SaveAssets();
            _mapContenar.Clear();
        }
    }
}