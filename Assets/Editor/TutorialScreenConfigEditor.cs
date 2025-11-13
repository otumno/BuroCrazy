// Файл: Assets/Editor/TutorialScreenConfigEditor.cs (ФИКС ДЛЯ 'CLASS')
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

[CustomEditor(typeof(TutorialScreenConfig))]
public class TutorialScreenConfigEditor : Editor
{
    private SerializedProperty screenID;
    private SerializedProperty sceneLoadDelay;
    private SerializedProperty firstEverAppearanceDelay;
    private SerializedProperty initialHintDelay;
    private SerializedProperty nextHintDelay;
    private SerializedProperty idleMessageChangeDelay;
    private SerializedProperty contextGroups;
    private SerializedProperty idleSpots;
    private SerializedProperty idleTips;

    private static Dictionary<string, bool> contextGroupFoldouts = new Dictionary<string, bool>();

    private void OnEnable()
    {
        screenID = serializedObject.FindProperty("screenID");
        sceneLoadDelay = serializedObject.FindProperty("sceneLoadDelay");
        firstEverAppearanceDelay = serializedObject.FindProperty("firstEverAppearanceDelay");
        initialHintDelay = serializedObject.FindProperty("initialHintDelay");
        nextHintDelay = serializedObject.FindProperty("nextHintDelay");
        idleMessageChangeDelay = serializedObject.FindProperty("idleMessageChangeDelay");
        contextGroups = serializedObject.FindProperty("contextGroups");
        idleSpots = serializedObject.FindProperty("idleSpots");
        idleTips = serializedObject.FindProperty("idleTips");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update(); 

        EditorGUILayout.LabelField("Основная Конфигурация", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(screenID);
        EditorGUILayout.Space();

        EditorGUILayout.LabelField("Настройки Задержек (в секундах)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(sceneLoadDelay);
        EditorGUILayout.PropertyField(firstEverAppearanceDelay);
        EditorGUILayout.PropertyField(initialHintDelay);
        EditorGUILayout.PropertyField(nextHintDelay);
        EditorGUILayout.PropertyField(idleMessageChangeDelay);
        EditorGUILayout.Space();

        EditorGUILayout.LabelField("Места 'Отдыха' (Idle)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(idleSpots, true);
        EditorGUILayout.PropertyField(idleTips, true);
        EditorGUILayout.Space();
        EditorGUILayout.Separator();

        DrawContextGroupsList();

        serializedObject.ApplyModifiedProperties(); 
    }

    private void DrawContextGroupsList()
    {
        EditorGUILayout.LabelField("Контекстные Подсказки", EditorStyles.boldLabel);
        
        EditorGUI.indentLevel++;

        for (int i = 0; i < contextGroups.arraySize; i++)
        {
            SerializedProperty groupElement = contextGroups.GetArrayElementAtIndex(i);
            
            // --- РЕШЕНИЕ ПРОБЛЕМЫ 'NullReferenceException' ---
            // Проверяем, не 'null' ли сам элемент (managedReferenceValue)
            if (groupElement.managedReferenceValue == null)
            {
                EditorGUILayout.HelpBox($"Элемент {i} пуст (null).", MessageType.Error);
                if (GUILayout.Button($"Удалить пустой элемент {i}"))
                {
                    contextGroups.DeleteArrayElementAtIndex(i);
                }
                continue; 
            }
            // --- КОНЕЦ РЕШЕНИЯ ---

            string elementPath = groupElement.propertyPath;
            if (!contextGroupFoldouts.ContainsKey(elementPath))
            {
                contextGroupFoldouts[elementPath] = false; 
            }

            SerializedProperty contextID = groupElement.FindPropertyRelative("contextID");
            SerializedProperty contextPanel = groupElement.FindPropertyRelative("contextPanel");
            
            string foldoutLabel = string.IsNullOrEmpty(contextID.stringValue) 
                ? $"Контекст {i}" 
                : contextID.stringValue;
            
            if (contextPanel.objectReferenceValue != null)
            {
                foldoutLabel += $" ({contextPanel.objectReferenceValue.name})";
            }

            contextGroupFoldouts[elementPath] = EditorGUILayout.Foldout(contextGroupFoldouts[elementPath], foldoutLabel, true, EditorStyles.foldoutHeader);

            if (contextGroupFoldouts[elementPath])
            {
                EditorGUI.indentLevel++;
                
                // Рисуем ВСЕ поля из TutorialContextGroup
                EditorGUILayout.PropertyField(groupElement.FindPropertyRelative("contextID"));
                EditorGUILayout.PropertyField(groupElement.FindPropertyRelative("contextPanel"));
                EditorGUILayout.PropertyField(groupElement.FindPropertyRelative("muteTutorial"));
                
                EditorGUILayout.PropertyField(groupElement.FindPropertyRelative("greetingTexts"), true);
                EditorGUILayout.PropertyField(groupElement.FindPropertyRelative("greetingEmotion"));
                
                EditorGUILayout.PropertyField(groupElement.FindPropertyRelative("greetingPointerSprite"));
                EditorGUILayout.PropertyField(groupElement.FindPropertyRelative("greetingPointerOffset"));
                EditorGUILayout.PropertyField(groupElement.FindPropertyRelative("greetingPointerRotation"));
                
                EditorGUILayout.PropertyField(groupElement.FindPropertyRelative("contextIdleTips"), true);
                EditorGUILayout.PropertyField(groupElement.FindPropertyRelative("contextIdleSpots"), true);
                EditorGUILayout.PropertyField(groupElement.FindPropertyRelative("helpSpots"), true);

                EditorGUI.indentLevel--;
            }

            if (GUILayout.Button($"Удалить Контекст '{foldoutLabel}'", GUILayout.Height(20)))
            {
                contextGroupFoldouts.Remove(elementPath); 
                contextGroups.DeleteArrayElementAtIndex(i);
                break; 
            }
            EditorGUILayout.Space(5); 
        }
        
        EditorGUI.indentLevel--;

        // --- ИСПРАВЛЕННАЯ КНОПКА "ДОБАВИТЬ" (которая чинит NullRef) ---
        if (GUILayout.Button("Добавить новый Контекст"))
        {
            contextGroups.InsertArrayElementAtIndex(contextGroups.arraySize);
            // Получаем только что добавленный (последний) элемент
            SerializedProperty newElement = contextGroups.GetArrayElementAtIndex(contextGroups.arraySize - 1);
            // Принудительно присваиваем ему новый экземпляр (вместо null)
            newElement.managedReferenceValue = new TutorialContextGroup();
        }
    }
}