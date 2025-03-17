using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;

namespace UnityFigmaBridge.Editor.Extension
{
    /// <summary>
    /// 末尾が一致するオブジェクトに対して、コンポーネントアタッチ用の処理クラスを設定する為のクラス
    /// </summary>
    [CreateAssetMenu(fileName = "CustomComponentAttachSetting", menuName = "FigmaBridge/ComponentAttachSetting")]
    public class CustomComponentAttachSetting : ScriptableObject
    {
        public List<AttachComponentParameter> attachSettingList;
        
        [System.Serializable]
        public class AttachComponentParameter
        {
            [Tooltip("アタッチ対象のオブジェクト名(末尾) 未設定で全て対象となる")]
            public string attachTargetEndName;
            
            [Tooltip("コンポーネントアタッチ処理を行うクラス名(namespace込み)")]
            public string componentAttachClassName;
        }
    }
}