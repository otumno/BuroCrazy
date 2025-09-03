// Файл: StateEmotionMapEditor.cs (ДОЛЖЕН ЛЕЖАТЬ В ПАПКЕ EDITOR)
using UnityEngine;
using UnityEditor;
using System;
using System.Linq;

[CustomEditor(typeof(StateEmotionMap))]
public class StateEmotionMapEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // Получаем наш объект StateEmotionMap
        StateEmotionMap map = (StateEmotionMap)target;

        // Рисуем поле для выбора типа персонажа
        EditorGUILayout.PropertyField(serializedObject.FindProperty("targetCharacterType"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Сопоставления 'Состояние -> Эмоция'", EditorStyles.boldLabel);

        // Получаем список состояний на основе выбранного типа персонажа
        string[] stateNames = GetStateNamesForType(map.targetCharacterType);

        // Рисуем список Mappings
        SerializedProperty list = serializedObject.FindProperty("mappings");
        for (int i = 0; i < list.arraySize; i++)
        {
            SerializedProperty element = list.GetArrayElementAtIndex(i);
            SerializedProperty stateNameProp = element.FindPropertyRelative("stateName");
            SerializedProperty emotionProp = element.FindPropertyRelative("emotion");

            EditorGUILayout.BeginHorizontal();

            // --- Магия происходит здесь ---
            if (stateNames != null && stateNames.Length > 0)
            {
                // Находим текущий индекс выбранного состояния
                int currentIndex = Array.IndexOf(stateNames, stateNameProp.stringValue);
                if (currentIndex < 0) currentIndex = 0;

                // Рисуем выпадающий список вместо текстового поля
                int newIndex = EditorGUILayout.Popup(currentIndex, stateNames);
                stateNameProp.stringValue = stateNames[newIndex];
            }
            else
            {
                // Если что-то пошло не так, рисуем обычное текстовое поле
                EditorGUILayout.PropertyField(stateNameProp, GUIContent.none);
            }

            EditorGUILayout.PropertyField(emotionProp, GUIContent.none);
            
            // Кнопка для удаления элемента
            if (GUILayout.Button("-", GUILayout.Width(20)))
            {
                list.DeleteArrayElementAtIndex(i);
            }

            EditorGUILayout.EndHorizontal();
        }

        // Кнопка для добавления нового элемента
        if (GUILayout.Button("Добавить состояние"))
        {
            list.InsertArrayElementAtIndex(list.arraySize);
        }

        // Сохраняем все изменения
        serializedObject.ApplyModifiedProperties();
    }

    private string[] GetStateNamesForType(CharacterType type)
    {
        Type enumType = null;
        switch (type)
        {
            case CharacterType.Client:
                enumType = typeof(ClientState);
                break;
            case CharacterType.Clerk:
                enumType = typeof(ClerkController.ClerkState);
                break;
            case CharacterType.Guard:
                enumType = typeof(GuardMovement.GuardState);
                break;
            case CharacterType.Intern:
                enumType = typeof(InternController.InternState);
                break;
            case CharacterType.ServiceWorker:
                enumType = typeof(ServiceWorkerController.WorkerState);
                break;
        }
        
        if (enumType != null)
        {
            return Enum.GetNames(enumType);
        }
        return null;
    }
}