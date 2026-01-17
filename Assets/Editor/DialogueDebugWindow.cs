// Assets/Editor/DialogueDebugWindow.cs
using UnityEngine;
using UnityEditor;
using DialogueSystem.Data;
using Managers;
using System.Linq;

public class DialogueDebugWindow : EditorWindow
{
    private DialogueGraph selectedGraph;
    private Vector2 scrollPos;

    [MenuItem("Tools/AI Toolset/🐛 Dialogue Debugger")]
    public static void ShowWindow()
    {
        GetWindow<DialogueDebugWindow>("Dialogue Debug");
    }

    void OnGUI()
    {
        GUILayout.Label("Мгновенный запуск диалога", EditorStyles.boldLabel);

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Зайдите в Play Mode, чтобы тестировать диалоги!", MessageType.Warning);
            return;
        }

        if (DialogueUIManager.Instance == null)
        {
            EditorGUILayout.HelpBox("DialogueUIManager не найден на сцене!", MessageType.Error);
            return;
        }

        GUILayout.Space(10);
        selectedGraph = (DialogueGraph)EditorGUILayout.ObjectField("Диалог:", selectedGraph, typeof(DialogueGraph), false);

        if (GUILayout.Button("▶ ЗАПУСТИТЬ ДИАЛОГ", GUILayout.Height(40)))
        {
            if (selectedGraph != null)
            {
                Debug.Log($"[Debugger] Запуск диалога: {selectedGraph.name}");
                // Запускаем без привязки к конкретному клиенту (null), 
                // система должна использовать портреты из нод или дефолтные
                DialogueUIManager.Instance.StartDialogue(selectedGraph, null);
            }
        }

        GUILayout.Space(20);
        GUILayout.Label("Все диалоги в проекте:", EditorStyles.boldLabel);
        
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
        string[] guids = AssetDatabase.FindAssets("t:DialogueGraph");
        
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            DialogueGraph graph = AssetDatabase.LoadAssetAtPath<DialogueGraph>(path);
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(graph.name, GUILayout.Width(200));
            if (GUILayout.Button("Play", GUILayout.Width(60)))
            {
                selectedGraph = graph;
                DialogueUIManager.Instance.StartDialogue(graph, null);
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();
    }
}