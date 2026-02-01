using UnityEngine;
using UnityEditor;
using UnityEditor.UI;
using Utilities;

[CanEditMultipleObjects, CustomEditor(typeof(NonDrawingGraphic), false)]
public class NonDrawingGraphicEditor : GraphicEditor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.PropertyField(base.m_Script, new GUILayoutOption[0]);
        EditorGUI.EndDisabledGroup();
        RaycastControlsGUI();
        serializedObject.ApplyModifiedProperties();
    }
}