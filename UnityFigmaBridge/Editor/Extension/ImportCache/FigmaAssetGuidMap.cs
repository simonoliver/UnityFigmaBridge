using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;

namespace UnityFigmaBridge.Editor.Extension.ImportCache
{
    public class FigmaAssetGuidMap : ScriptableObject
    {
        // TODO：ここは後々 Dictionary にする
        public List<AssetEntryData> assetEntryDataList = new List<AssetEntryData>();
        [Serializable]
        public class AssetEntryData
        {
            public string figmaNodeId;
            public string unityAssetGuid;
            public string assetName;
        }

        public GameObject LoadPrefab(string nodeId)
        {
            var path = GetAssetPath(nodeId);
            if (path == string.Empty) return null;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            return prefab;
        }

        public string GetAssetPath(string nodeId)
        {
            var guid = GetGuidByNodeId(nodeId);
            if (guid == string.Empty) return string.Empty;
            return AssetDatabase.GUIDToAssetPath(guid);
        }
        
        public string GetGuidByNodeId(string nodeId)
        {
            return assetEntryDataList.FirstOrDefault(e => e.figmaNodeId == nodeId)?.unityAssetGuid;
        }

        public void SetMapping(string nodeId, string guid, string assetName)
        {
            var entry = assetEntryDataList.FirstOrDefault(e => e.figmaNodeId == nodeId);
            if (entry == null)
            {
                assetEntryDataList.Add(new AssetEntryData { figmaNodeId = nodeId, unityAssetGuid = guid, assetName = assetName });
                EditorUtility.SetDirty(this);
            }
            else
            {
                entry.unityAssetGuid = guid;
            }
        }
    }
}