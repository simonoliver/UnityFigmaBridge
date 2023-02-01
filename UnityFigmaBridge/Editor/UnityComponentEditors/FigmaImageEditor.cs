using UnityEditor;
using UnityEditor.UI;
using UnityFigmaBridge.Runtime.UI;

namespace UnityFigmaBridge.Editor.UnityComponentEditors
{
    [CustomEditor(typeof(FigmaImage), false)]
    [CanEditMultipleObjects]
    public class FigmaImageEditor: ImageEditor
    {
        private SerializedProperty m_Shape;
        private SerializedProperty m_FillColor;
        private SerializedProperty m_StrokeColor;
        private SerializedProperty m_StrokeWidth;
        private SerializedProperty m_CornerRadius;
        private SerializedProperty m_Fill;
        private SerializedProperty m_FillGradient;
        private SerializedProperty m_GradientHandlePositions;
        private SerializedProperty m_ImageScaleMode;
        private SerializedProperty m_ImageTransform;
        private SerializedProperty m_ImageScaleFactor;
        private SerializedProperty m_EllipseInnerRadius;
        private SerializedProperty m_EllipseArcAngleRange;
        
        
        
        protected override void OnEnable()
        {
            base.OnEnable();

            m_Shape=serializedObject.FindProperty("m_Shape");
            m_FillColor = serializedObject.FindProperty("m_FillColor");
            m_StrokeColor = serializedObject.FindProperty("m_StrokeColor");
            m_StrokeWidth = serializedObject.FindProperty("m_StrokeWidth");
            m_CornerRadius= serializedObject.FindProperty("m_CornerRadius");
            m_Fill= serializedObject.FindProperty("m_Fill");
            m_FillGradient= serializedObject.FindProperty("m_FillGradient");
            m_GradientHandlePositions= serializedObject.FindProperty("m_GradientHandlePositions");
            m_ImageScaleMode= serializedObject.FindProperty("m_ImageScaleMode");
            m_ImageTransform= serializedObject.FindProperty("m_ImageTransform");
            m_ImageScaleFactor= serializedObject.FindProperty("m_ImageScaleFactor");
            m_EllipseInnerRadius= serializedObject.FindProperty("m_EllipseInnerRadius");
            m_EllipseArcAngleRange= serializedObject.FindProperty("m_EllipseArcAngleRange");
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            EditorGUILayout.PropertyField(m_Shape);
            EditorGUILayout.PropertyField(m_FillColor);
            EditorGUILayout.PropertyField(m_StrokeColor);
            EditorGUILayout.PropertyField(m_StrokeWidth);
            EditorGUILayout.PropertyField(m_CornerRadius);
            EditorGUILayout.PropertyField(m_Fill);
            EditorGUILayout.PropertyField(m_FillGradient);
            EditorGUILayout.PropertyField(m_GradientHandlePositions);
            EditorGUILayout.PropertyField(m_ImageScaleMode);
            EditorGUILayout.PropertyField(m_ImageTransform);
            EditorGUILayout.PropertyField(m_ImageScaleFactor);
            EditorGUILayout.PropertyField(m_EllipseInnerRadius);
            EditorGUILayout.PropertyField(m_EllipseArcAngleRange);
            serializedObject.ApplyModifiedProperties();
        }

    }
}
