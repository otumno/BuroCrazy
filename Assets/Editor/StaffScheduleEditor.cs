using Data.Calendar;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    [CustomEditor(typeof(StaffController), editorForChildClasses: true)]
    public class StaffScheduleEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            
            DrawPropertiesExcluding(serializedObject, "m_Script", "WorkShiftMask");

            StaffController staff = (StaffController)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("График Работы (Flux Mask)", EditorStyles.boldLabel);

            staff.WorkShiftMask = (CalendarDayPeriodType)EditorGUILayout.EnumFlagsField("Смены", staff.WorkShiftMask);

            if (GUI.changed)
            {
                EditorUtility.SetDirty(staff);
            }
            
            serializedObject.ApplyModifiedProperties();
        }
    }
}