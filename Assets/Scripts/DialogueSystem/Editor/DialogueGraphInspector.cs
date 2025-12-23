// Файл: Assets/Scripts/DialogueSystem/Editor/DialogueGraphInspector.cs
using UnityEditor;
using UnityEngine;
using DialogueSystem.Data;

namespace DialogueSystem.Editor
{
    [CustomEditor(typeof(DialogueGraph))]
    public class DialogueGraphInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            // Получаем ссылку на текущий выбранный граф
            DialogueGraph graph = (DialogueGraph)target;

            GUILayout.Space(10);

            // Рисуем большую кнопку
            GUI.backgroundColor = new Color(0.6f, 1f, 0.6f); // Зеленоватый цвет
            if (GUILayout.Button("Open Visual Editor", GUILayout.Height(40)))
            {
                DialogueEditorWindow.OpenWindow(graph);
            }
            GUI.backgroundColor = Color.white;

            GUILayout.Space(10);

            // Показываем немного статистики
            EditorGUILayout.HelpBox($"Nodes Count: {graph.allNodes.Count}\nStart Node Set: {(graph.startNode != null)}", MessageType.Info);

            GUILayout.Space(10);

            // Рисуем стандартные поля (например, nextDefaultDialogue), но исключаем список нод
            serializedObject.Update();
            
            DrawPropertiesExcluding(serializedObject, "m_Script", "allNodes");
            
            // Если очень нужно посмотреть сырые данные (для отладки), можно раскомментировать:
            // base.OnInspectorGUI();

            serializedObject.ApplyModifiedProperties();
        }
    }
}