using System;
using System.Collections.Generic;

namespace UnityFigmaBridge.Editor.PrefabHealthCheck
{
    /// <summary>
    /// Data model for a Figma variant - a prefab derived from a Figma source prefab
    /// </summary>
    [Serializable]
    public class FigmaVariantInfo
    {
        public string VariantPath;
        public string BasePrefabPath;
        public string BasePrefabName;
    }

    /// <summary>
    /// Represents a single missing object reference within a component
    /// </summary>
    [Serializable]
    public class MissingObjectRef
    {
        public string GameObjectName;
        public string ComponentType;
        public string PropertyPath;
    }

    /// <summary>
    /// Represents a missing nested prefab instance inside a prefab
    /// </summary>
    [Serializable]
    public class MissingNestedPrefab
    {
        public string ParentGameObjectName;
        public string MissingGameObjectName;
    }

    /// <summary>
    /// Data model for a prefab containing missing references (scripts, object refs, or nested prefabs)
    /// </summary>
    [Serializable]
    public class MissingReferenceInfo
    {
        public string PrefabPath;
        public int MissingScriptCount;
        public int MissingPrefabCount;
        public List<MissingObjectRef> MissingObjectRefs = new List<MissingObjectRef>();
        public List<MissingNestedPrefab> MissingNestedPrefabs = new List<MissingNestedPrefab>();

        public int TotalMissingCount => MissingScriptCount + MissingObjectRefs.Count + MissingPrefabCount;
    }

    /// <summary>
    /// Data model for a variant whose base prefab has been deleted
    /// </summary>
    [Serializable]
    public class OrphanedVariantInfo
    {
        public string VariantPath;
        public string MissingBaseGuid;
        public string OriginalBasePath;
    }

    /// <summary>
    /// Container for all health check scan results
    /// </summary>
    [Serializable]
    public class PrefabHealthCheckResult
    {
        public List<FigmaVariantInfo> FigmaVariants = new List<FigmaVariantInfo>();
        public List<MissingReferenceInfo> MissingReferences = new List<MissingReferenceInfo>();
        public List<OrphanedVariantInfo> OrphanedVariants = new List<OrphanedVariantInfo>();
        public bool IsComplete;
    }
}
