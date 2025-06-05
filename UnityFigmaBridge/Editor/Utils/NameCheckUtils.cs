using UnityFigmaBridge.Editor.FigmaApi;

namespace UnityFigmaBridge.Editor.Utils
{
    /// <summary>
    /// カスタム命名ルールチェック用のクラス
    /// </summary>
    public static class NameCheckUtils
    {
        /// <summary>
        /// 9Sliceの対象かどうか
        /// </summary>
        /// <param name="node">Figmaノード</param>
        public static bool Is9Slice(this Node node)
        {
            return node.name.EndsWith("_9s");
        }
        
        /// <summary>
        /// ダミーかどうか　ダミーの場合は自身も子オブジェクトも生成しない
        /// </summary>
        /// <param name="node">Figmaノード</param>
        public static bool IsDummyNode(this Node node)
        {
            return node.name.Equals("DummyNode");
        }
    }
}