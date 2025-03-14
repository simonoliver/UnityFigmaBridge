using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;


namespace UnityFigmaBridge.Editor.Extension
{
    /// <summary>
    /// カスタムコンポーネントをアタッチする為の管理クラス
    /// </summary>
    public static class CustomComponentAttachManager
    {
        /// <summary>
        /// カスタムコンポーネントアタッチ用のファイルのディレクトリ
        /// </summary>
        private static readonly string CUSTOM_COMPONENT_ATTACH_SETTING_FILE_NAME = "Assets/Figma/Custom/CustomComponentAttachSetting.asset";
        private static CustomComponentAttachSetting setting;
        private static readonly Dictionary<Type, IComponentAttachment> InstanceCache = new Dictionary<Type, IComponentAttachment>();

        public static void OnStart()
        {
            // 設定ファイルを読み込む
            setting = AssetDatabase.LoadAssetAtPath<CustomComponentAttachSetting>(CUSTOM_COMPONENT_ATTACH_SETTING_FILE_NAME);
        }
        
        public static void ApplySetting(GameObject prefab)
        {
            if (setting == null)
            {
                Debug.LogError($"Component attach setting is null.");
                return;
            }
            
            if (setting.attachSettingList.Count <= 0)
            {
                return;
            }
            
            if (prefab == null)
            {
                Debug.LogError($"prefab is null.");
                return;
            }

            var allObjectsTransForm  = prefab.GetComponentsInChildren<Transform>();
            foreach (var transform in allObjectsTransForm)
            {
                GameObject gameObject = transform.gameObject;
                if (!gameObject)
                {
                    continue;
                }
                
                // プレハブであれば無視
                if (PrefabUtility.GetCorrespondingObjectFromSource(gameObject))
                {
                    continue;
                }
                
                foreach (var attachSetting in setting.attachSettingList)
                {
                    // アタッチの過程で削除されていた場合は抜ける
                    if (!gameObject)
                    {
                        break;
                    }
                    
                    var objectName = gameObject.name;

                    // 末尾の名称パターンが存在しないか、合致した
                    if (string.IsNullOrEmpty(attachSetting.attachTargetEndName) ||
                        objectName.EndsWith(attachSetting.attachTargetEndName))
                    {
                        AttachComponent(
                            gameObject,
                            attachSetting.componentAttachClassName);
                        
                    }
                }
            }
        }

        public static void OnEnd()
        {
            InstanceCache.Clear();
        }

        
        private static void AttachComponent(GameObject gameObject, string className)
        {
            Type componentAttachmentType = Type.GetType(className);

            // コンポーネントアタッチ用の基底クラスを継承しているかチェック
            if (typeof(IComponentAttachment).IsAssignableFrom(componentAttachmentType))
            {
                var instance = GetComponentAttachmentInstance(componentAttachmentType);
                // コンポーネントアタッチ用の関数実行
                instance.AttachComponent(gameObject);
            }
        }

        private static IComponentAttachment GetComponentAttachmentInstance(Type type)
        {
            // キャッシュから取得
            if (InstanceCache.TryGetValue(type, out var componentAttachmentInstance))
            {
                return componentAttachmentInstance;
                
            }
            // なければ生成
            componentAttachmentInstance = (IComponentAttachment)Activator.CreateInstance(type);
            InstanceCache.Add(type, componentAttachmentInstance);

            return componentAttachmentInstance;
        }
    }
}