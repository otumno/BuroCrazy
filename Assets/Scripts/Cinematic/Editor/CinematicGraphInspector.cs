// === FILE: Assets/Scripts/Cinematic/Editor/CinematicGraphInspector.cs ===
using UnityEngine;
using UnityEditor;

namespace CinematicSystem.Editor
{
    /// <summary>
    /// Кастомный инспектор для CinematicGraph.
    /// Добавляет кнопки для редактирования графа.
    /// </summary>
    [CustomEditor(typeof(CinematicGraph))]
    public class CinematicGraphInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var graph = (CinematicGraph)target;

            // Отображаем стандартные поля
            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Editor Actions", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            // Кнопка открытия в редакторе
            if (GUILayout.Button("Open in Editor", GUILayout.Height(30)))
            {
                CinematicEditorWindow.OpenWindow(graph);
            }

            // Кнопка экспорта в JSON
            if (GUILayout.Button("Export JSON", GUILayout.Height(30)))
            {
                string json = CinematicJSONExporter.Export(graph);
                string path = EditorUtility.SaveFilePanel(
                    "Export Cinematic Graph",
                    "Assets",
                    graph.graphName + ".json",
                    "json");

                if (!string.IsNullOrEmpty(path))
                {
                    System.IO.File.WriteAllText(path, json);
                    Debug.Log($"[CinematicGraph] Экспортирован: {path}");
                }
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();

            // Кнопка импорта из JSON
            if (GUILayout.Button("Import JSON", GUILayout.Height(25)))
            {
                string path = EditorUtility.OpenFilePanel("Import JSON", "Assets", "json");
                if (!string.IsNullOrEmpty(path))
                {
                    string json = System.IO.File.ReadAllText(path);
                    var imported = CinematicJSONImporter.Import(json);
                    if (imported != null)
                    {
                        // Копируем данные
                        EditorUtility.CopySerialized(imported, graph);
                        EditorUtility.SetDirty(graph);
                        Debug.Log($"[CinematicGraph] Импортирован: {path}");
                    }
                }
            }

            // Кнопка валидации
            if (GUILayout.Button("Validate", GUILayout.Height(25)))
            {
                string error;
                if (graph.Validate(out error))
                {
                    EditorUtility.DisplayDialog("Validation", "Граф валиден!", "OK");
                }
                else
                {
                    EditorUtility.DisplayDialog("Validation Error", error, "OK");
                }
            }

            EditorGUILayout.EndHorizontal();

            // Статистика
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Statistics", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Nodes: {graph.allNodes.Count}");
            EditorGUILayout.LabelField($"Start Node: {(graph.startNode ? graph.startNode.nodeName : "None")}");
        }
    }
}