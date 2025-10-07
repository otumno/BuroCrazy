using UnityEngine;
using UnityEditor;
using System.Linq;

[CustomEditor(typeof(GameCalendar))]
public class GameCalendarEditor : Editor
{
    private bool[] periodFoldouts;

    public override void OnInspectorGUI()
    {
        // base.OnInspectorGUI(); // Мы не рисуем стандартный инспектор

        GameCalendar calendar = (GameCalendar)target;

        EditorGUILayout.LabelField("Настройка Игрового Календаря", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        if (calendar.periodSettings == null)
        {
            calendar.periodSettings = new System.Collections.Generic.List<PeriodSettings>();
        }

        if (periodFoldouts == null || periodFoldouts.Length != calendar.periodSettings.Count)
        {
            periodFoldouts = new bool[calendar.periodSettings.Count];
        }

        // Рисуем каждый период в виде сворачиваемого блока
        for (int i = 0; i < calendar.periodSettings.Count; i++)
        {
            var period = calendar.periodSettings[i];
            
            periodFoldouts[i] = EditorGUILayout.Foldout(periodFoldouts[i], $"Период: {period.periodName}", true, EditorStyles.foldoutHeader);

            if (periodFoldouts[i])
            {
                EditorGUI.indentLevel++;
                
                period.periodName = EditorGUILayout.TextField("Название периода", period.periodName);
                
                EditorGUILayout.LabelField("Параметры по дням (День 1-30)", EditorStyles.boldLabel);
                
                // Рисуем поля с кривыми - Unity сделает их красивыми автоматически!
                period.clientCount = EditorGUILayout.CurveField("Кол-во клиентов", period.clientCount);
                period.durationInSeconds = EditorGUILayout.CurveField("Длительность (сек)", period.durationInSeconds);
                period.crowdSpawnCount = EditorGUILayout.CurveField("Размер толпы", period.crowdSpawnCount);
                period.numberOfCrowdsToSpawn = EditorGUILayout.CurveField("Кол-во толп", period.numberOfCrowdsToSpawn);

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Статичные настройки", EditorStyles.boldLabel);
                
                // Рисуем остальные поля как обычно
                EditorGUILayout.PropertyField(serializedObject.FindProperty("periodSettings").GetArrayElementAtIndex(i).FindPropertyRelative("lightingSettings"), true);
                period.panelColor = EditorGUILayout.ColorField("Цвет панели", period.panelColor);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("periodSettings").GetArrayElementAtIndex(i).FindPropertyRelative("lightsToEnableNames"), true);

                EditorGUI.indentLevel--;
            }
        }
        
        EditorGUILayout.Space();
        
        // Кнопки для управления списком
        if (GUILayout.Button("Добавить новый период"))
        {
            calendar.periodSettings.Add(new PeriodSettings());
        }

        if (calendar.periodSettings.Count > 0 && GUILayout.Button("Удалить последний период"))
        {
            calendar.periodSettings.RemoveAt(calendar.periodSettings.Count - 1);
        }

        // Важно: применяем все изменения
        serializedObject.ApplyModifiedProperties();
        if (GUI.changed)
        {
            EditorUtility.SetDirty(calendar);
        }
    }
}