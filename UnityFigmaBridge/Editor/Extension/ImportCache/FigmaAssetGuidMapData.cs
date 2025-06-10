using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;

namespace UnityFigmaBridge.Editor.Extension.ImportCache
{
    public class FigmaAssetGuidMapData : ScriptableObject
    {
        // TODO：ここは後々 Dictionary にする
        public List<AssetMapEntry> assetEntryDataList = new List<AssetMapEntry>();
        [Serializable]
        public class AssetMapEntry
        {
            public string assetName;
            public string figmaNodeId;
            public string unityAssetGuid;
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

        public void Add(string nodeId, string guid, string assetName)
        {
            var entry = assetEntryDataList.FirstOrDefault(e => e.figmaNodeId == nodeId);
            if (entry == null)
            {
                assetEntryDataList.Add(new AssetMapEntry { figmaNodeId = nodeId, unityAssetGuid = guid, assetName = assetName });
                EditorUtility.SetDirty(this);
            }
            else
            {
                if (!guid.Equals(entry.unityAssetGuid) || !assetName.Equals(entry.assetName))
                {
                    EditorUtility.SetDirty(this);
                    entry.unityAssetGuid = guid;
                    entry.assetName = assetName;
                }
            }
        }
    }
}