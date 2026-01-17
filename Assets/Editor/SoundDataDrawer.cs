// Assets/Editor/SoundDataDrawer.cs
using UnityEditor;
using UnityEngine;
using Scriptables.Audio;

[CustomPropertyDrawer(typeof(SoundData))]
public class SoundDataDrawer : PropertyDrawer
{
    // Высота строки в списке
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        // Если элемент свернут - одна строка, если развернут - стандартная высота содержимого
        return EditorGUI.GetPropertyHeight(property, label, true);
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        // Находим свойство 'id' внутри SoundData
        SerializedProperty idProperty = property.FindPropertyRelative("id");

        // Формируем новый заголовок
        string headerLabel = "Sound Data";
        if (idProperty != null)
        {
            // Получаем имя enum элемента
            string enumName = idProperty.enumNames[idProperty.enumValueIndex];
            headerLabel = enumName;
        }

        // Рисуем свойство со стандартным поведением, но с нашим заголовком
        EditorGUI.PropertyField(position, property, new GUIContent(headerLabel), true);
    }
}