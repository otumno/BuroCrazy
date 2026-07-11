// Assets/Editor/DialogueVerboseImporterWindow.cs
using System.IO;
using UnityEditor;
using UnityEngine;
using DialogueSystem.Data;
using DialogueSystem.EditorTools;

namespace DialogueSystem.EditorTools
{
    /// <summary>
    /// Окно импорта/экспорта диалогов в человеко-читаемом JSON (для DeepSeek и т.п.).
    /// </summary>
    public class DialogueVerboseImporterWindow : EditorWindow
    {
        string _jsonInput = "";
        string _targetFolder = "Assets/Scripts/DialogueSystem/DialoguesBase/Generated";
        DialogueGraph _exportGraph;
        Vector2 _scroll;

        [MenuItem("Tools/AI Toolset/Dialogue Importer (Verbose, Stable IDs)")]
        public static void ShowWindow() => GetWindow<DialogueVerboseImporterWindow>("AI Dialogue Verbose");

        void OnGUI()
        {
            EditorGUILayout.LabelField("Verbose Dialogue Importer", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Человеко-читаемый JSON со стабильными nodeID.\n" +
                "Каждая нода идентифицируется по nodeID (например, \"choice_main\"),\n" +
                "связи строятся по этим ID, а не по индексу.\n" +
                "Подходит для round-trip с генератором контента (DeepSeek).",
                MessageType.Info);

            // --- Импорт ---
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Import", EditorStyles.boldLabel);
            _targetFolder = EditorGUILayout.TextField("Target Folder", _targetFolder);
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(220));
            _jsonInput = EditorGUILayout.TextArea(_jsonInput);
            EditorGUILayout.EndScrollView();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Load from file…"))
                {
                    string p = EditorUtility.OpenFilePanel("Open dialogue JSON", Application.dataPath, "json");
                    if (!string.IsNullOrEmpty(p) && File.Exists(p))
                        _jsonInput = File.ReadAllText(p);
                }
                if (GUILayout.Button("Import", GUILayout.Height(28)))
                {
                    TryImport();
                }
            }

            // --- Экспорт ---
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Export", EditorStyles.boldLabel);
            _exportGraph = (DialogueGraph)EditorGUILayout.ObjectField("Source Graph", _exportGraph, typeof(DialogueGraph), false);

            using (new EditorGUI.DisabledScope(_exportGraph == null))
            {
                if (GUILayout.Button("Copy JSON to Clipboard", GUILayout.Height(24)))
                {
                    string json = DialogueVerboseExporter.ExportToJson(_exportGraph);
                    if (!string.IsNullOrEmpty(json))
                    {
                        EditorGUIUtility.systemCopyBuffer = json;
                        Debug.Log($"[DialogueVerboseImporterWindow] JSON скопирован в буфер обмена ({json.Length} символов).");
                    }
                }
                if (GUILayout.Button("Save JSON to File…"))
                {
                    string defaultName = _exportGraph != null ? _exportGraph.name : "Dialogue";
                    string p = EditorUtility.SaveFilePanel("Save dialogue JSON", Application.dataPath, defaultName, "json");
                    if (!string.IsNullOrEmpty(p))
                    {
                        DialogueVerboseExporter.ExportToFile(_exportGraph, p);
                        // Подсветим файл в проекте, если он внутри Assets.
                        if (p.StartsWith(Application.dataPath))
                        {
                            string rel = "Assets" + p.Substring(Application.dataPath.Length);
                            AssetDatabase.Refresh();
                            var obj = AssetDatabase.LoadAssetAtPath<Object>(rel);
                            if (obj != null) EditorGUIUtility.PingObject(obj);
                        }
                    }
                }
            }
        }

        private void TryImport()
        {
            if (string.IsNullOrEmpty(_jsonInput))
            {
                EditorUtility.DisplayDialog("Dialogue Importer", "Введите JSON или загрузите файл.", "OK");
                return;
            }

            try
            {
                var dto = JsonUtility.FromJson<DialogueVerboseImporter.GraphDto>(_jsonInput);
                if (dto == null || string.IsNullOrEmpty(dto.name))
                {
                    EditorUtility.DisplayDialog("Dialogue Importer", "JSON повреждён или не содержит поле name.", "OK");
                    return;
                }
                string assetPath = $"{_targetFolder.TrimEnd('/')}/{dto.name}.asset";
                var graph = DialogueVerboseImporter.Import(_jsonInput, assetPath);
                if (graph != null)
                    EditorGUIUtility.PingObject(graph);
            }
            catch (System.Exception ex)
            {
                EditorUtility.DisplayDialog("Dialogue Importer", "Ошибка импорта:\n" + ex.Message, "OK");
                Debug.LogException(ex);
            }
        }
    }
}
