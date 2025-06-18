using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;

namespace UnityFigmaBridge.Editor.Extension.ImportCache
{
    public class FigmaAssetGuidMapData : ScriptableObject
    {
        public List<AssetMapEntry> assetEntryDataList = new List<AssetMapEntry>();
        private Dictionary<string, (string guid, string assetName)> _assetMap = new Dictionary<string, (string guid, string assetName)>();// 処理用
        
        [Serializable]
        public class AssetMapEntry
        {
            public string assetName;
            public string figmaNodeId;
            public string unityAssetGuid;
        }

        public void Initialize()
        {
            _assetMap.Clear();
            foreach (var entry in assetEntryDataList)
            {
                if (!string.IsNullOrEmpty(entry.figmaNodeId))
                {
                    _assetMap[entry.figmaNodeId] = (entry.unityAssetGuid, entry.assetName);
                }
            }
        }

        public void FinalizeMap()
        {
            assetEntryDataList = _assetMap.Select(m => new AssetMapEntry
            {
                figmaNodeId = m.Key,
                unityAssetGuid = m.Value.guid,
                assetName = m.Value.assetName
            }).ToList();
        }

        public bool AssetExists(string key)
        {
            var assetPath = GetAssetPath(key);
            if (assetPath == string.Empty)
            {
                return false;
            }
            bool fileExists = File.Exists(assetPath);
            // 存在しない場合はパスをリセットする
            if (!fileExists)
            {
                _assetMap.Remove(key);
            }
            
            return fileExists;
        }
        
        public string GetAssetPath(string nodeId)
        {
            var guid = GetGuidByNodeId(nodeId);
            if (guid == string.Empty) return string.Empty;
            return AssetDatabase.GUIDToAssetPath(guid);
        }
        
        public string GetGuidByNodeId(string nodeId)
        {
            if (_assetMap.TryGetValue(nodeId, out var value))
            {
                return value.guid;
            }
            return string.Empty;
        }

        public void Add(string nodeId, string guid, string assetName)
        {
            if (!_assetMap.TryGetValue(nodeId, out var entryValue))
            {
                _assetMap.Add(nodeId, (guid, assetName));
                EditorUtility.SetDirty(this);
            }
            
            if (!guid.Equals(entryValue.guid) || !assetName.Equals(entryValue.assetName))
            {
                entryValue.guid = guid;
                entryValue.assetName = assetName;
                EditorUtility.SetDirty(this);
            }
        }

        public void SetDirty()
        {
            EditorUtility.SetDirty(this);
        }
    }
}