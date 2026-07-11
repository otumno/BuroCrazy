// Assets/Editor/ImportStoryArcsHeadless.cs
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using StorySystem.EditorTools;

namespace StorySystem.EditorTools
{
    /// <summary>
    /// Полный импорт story_arcs.json через batchmode без открытия UI.
    /// Использование:
    ///   Unity -batchmode -nographics -quit -projectPath . \
    ///         -executeMethod StorySystem.EditorTools.ImportStoryArcsHeadless.Run
    /// </summary>
    public static class ImportStoryArcsHeadless
    {
        public static void Run()
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string jsonPath = Path.Combine(Application.dataPath, "Editor", "Sample", "story_arcs.json");
            string reportPath = Path.Combine(projectRoot, "story_arcs_import_report.txt");

            if (!File.Exists(jsonPath))
            {
                Debug.LogError($"[ImportStoryArcsHeadless] Файл не найден: {jsonPath}");
                EditorApplication.Exit(1);
                return;
            }

            string json = File.ReadAllText(jsonPath);
            var report = ArcJsonImporter.ImportJson(json, "Assets/Resources/Arcs", true);
            string text = report.ToString();

            File.WriteAllText(reportPath, "===== story_arcs.json IMPORT =====\n" + text, new UTF8Encoding(true));
            Debug.Log("===== story_arcs.json IMPORT =====\n" + text);
            System.Console.WriteLine("===== story_arcs.json IMPORT =====");
            System.Console.WriteLine(text);

            EditorApplication.Exit(0);
        }
    }
}