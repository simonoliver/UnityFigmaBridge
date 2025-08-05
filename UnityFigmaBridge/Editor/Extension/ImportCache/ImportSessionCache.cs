using System.Collections.Generic;

namespace UnityFigmaBridge.Editor.Extension.ImportCache
{
    /// <summary>
    /// 1回のインポートで利用するキャッシュデータをまとめておくクラス
    /// </summary>
    public static class ImportSessionCache
    {
        // 画像名取得用コンテナ　(imageRef, 画像のノード名(＝画像名))
        public static Dictionary<string, string> imageNameMap = new Dictionary<string, string>();
        // 画像名重複数保持用コンテナ　(画像名, 画像数)
        public static Dictionary<string, int> imageNameCountMap = new Dictionary<string, int>();

        public static void CacheClear()
        {
            imageNameMap.Clear();
            imageNameCountMap.Clear();
        }
    }
}