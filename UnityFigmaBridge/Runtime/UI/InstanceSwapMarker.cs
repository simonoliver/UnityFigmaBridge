using UnityEngine;

namespace UnityFigmaBridge.Runtime.UI
{
    /// <summary>
    /// 置き換え用のマーカー
    /// </summary>
    public class InstanceSwapMarker : MonoBehaviour
    {
        /// <summary>
        /// 置き換え対象の名前
        /// </summary>
        public string targetName;
        /// <summary>
        /// 置き換え先のプレハブ
        /// </summary>
        public GameObject replacementPrefab;
    }

    /// <summary>
    /// 削除対象のインスタンスを示すマーカー
    /// </summary>
    public class DeleteInstanceMarker : MonoBehaviour
    {
    }

}