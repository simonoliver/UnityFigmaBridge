using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using UnityFigmaBridge.Editor.PrototypeFlow;


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
        private static readonly Dictionary<string, IComponentAttachment> InstanceCache = new Dictionary<string, IComponentAttachment>();
        
        
        public static void OnStart()
        {
            // 設定ファイルを読み込む
            setting = AssetDatabase.LoadAssetAtPath<CustomComponentAttachSetting>(CUSTOM_COMPONENT_ATTACH_SETTING_FILE_NAME);
        }
        
        /// <summary>
        /// プレハブ単位の設定適用
        /// </summary>
        public static void ApplySettingPrefab(GameObject prefab)
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
                if (!transform)
                {
                    continue;
                }
                GameObject gameObject = transform.gameObject;
                CustomComponentAttachManager.ApplySettingGameObject(gameObject);
            }
        }

        /// <summary>
        /// ゲームオブジェクト単位の設定適用
        /// </summary>
        public static void ApplySettingGameObject(GameObject gameObject)
        {
            if (!gameObject)
            {
                return;
            }
                
            // プレハブであれば無視
            if (PrefabUtility.GetCorrespondingObjectFromSource(gameObject))
            {
                return;
            }

            if (setting?.attachSettingList == null)
            {
                return;
            }
            
            foreach (var attachSetting in setting.attachSettingList)
            {
                // アタッチの過程で削除されていた場合は抜ける
                if (!gameObject)
                {
                    return;
                }
                    
                var objectName = gameObject.name;

                // 末尾の名称パターンが存在しないか、合致した
                if (string.IsNullOrEmpty(attachSetting.attachTargetEndName) ||
                    objectName.EndsWith(attachSetting.attachTargetEndName))
                {
                    var instance = GetComponentAttachmentInstance(attachSetting.componentAttachClassName);
                    // コンポーネントアタッチ用の関数実行
                    instance?.AttachComponent(gameObject);
                }
            }
        }

        public static void OnEnd()
        {
            InstanceCache.Clear();
        }
        
        /// <summary>
        /// コンポーネントアタッチ用の処理を実行する
        /// </summary>
        /// <param name="gameObject">対象のゲームオブジェクト</param>
        /// <param name="componentAttachmentType">コンポーネントアタッチする為のクラスタイプ</param>
        /// <returns></returns>
        public static void TryAttachComponent(GameObject gameObject, Type componentAttachmentType)
        {
            var instance = GetComponentAttachmentInstance(componentAttachmentType);
            // コンポーネントアタッチ用の関数実行
            instance?.AttachComponent(gameObject);
        }

        /// <summary>
        /// コンポーネントアタッチ用のインスタンスの取得(クラス名指定)
        /// </summary>
        /// <param name="typeName"></param>
        /// <returns></returns>
        private static IComponentAttachment GetComponentAttachmentInstance(string classNameFull)
        {
            // キャッシュから取得
            if (InstanceCache.TryGetValue(classNameFull, out var componentAttachmentInstance))
            {
                return componentAttachmentInstance;
            }
            int lastDotIndex = classNameFull.LastIndexOf('.');
            var nameSpace = classNameFull.Substring(0, lastDotIndex);
            var className = classNameFull.Substring(lastDotIndex + 1);
            var type = BehaviourBindingManager.GetTypeByName(nameSpace, className);
            if (type == null)
            {
                return null;
            }
            
            // なければ生成
            componentAttachmentInstance = (IComponentAttachment)Activator.CreateInstance(type);
            if (componentAttachmentInstance != null)
            {
                InstanceCache.Add(classNameFull, componentAttachmentInstance);
            }
            
            return componentAttachmentInstance;
        }
        
        /// <summary>
        /// コンポーネントアタッチ用のインスタンスの取得(type指定)
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private static IComponentAttachment GetComponentAttachmentInstance(Type type)
        {
            if (type == null)
            {
                return null;
            }
            if (!typeof(IComponentAttachment).IsAssignableFrom(type))
            {
                return null;
            }
            var typeName = type.FullName;
            // キャッシュから取得
            if (InstanceCache.TryGetValue(typeName, out var componentAttachmentInstance))
            {
                return componentAttachmentInstance;
                
            }

            // なければ生成
            componentAttachmentInstance = (IComponentAttachment)Activator.CreateInstance(type);
            if (componentAttachmentInstance != null)
            {
                InstanceCache.Add(typeName, componentAttachmentInstance);
            }

            return componentAttachmentInstance;
        }
    }
}