// Assets/Editor/ValidateStoryArcsHeadless.cs
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using StorySystem.EditorTools;

namespace StorySystem.EditorTools
{
    /// <summary>
    /// Одноразовый запуск валидации story_arcs.json из командной строки Unity.
    /// Использование:
    ///   Unity -batchmode -nographics -quit -projectPath . \
    ///         -executeMethod StorySystem.EditorTools.ValidateStoryArcsHeadless.Run
    /// </summary>
    public static class ValidateStoryArcsHeadless
    {
        public static void Run()
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string jsonPath = Path.Combine(Application.dataPath, "Editor", "Sample", "story_arcs.json");
            string reportPath = Path.Combine(projectRoot, "story_arcs_validation_report.txt");

            if (!File.Exists(jsonPath))
            {
                Debug.LogError($"[ValidateStoryArcsHeadless] Файл не найден: {jsonPath}");
                EditorApplication.Exit(1);
                return;
            }

            string json = File.ReadAllText(jsonPath);
            var report = ArcJsonImporter.ValidateJson(json);
            string text = report.ToString();

            File.WriteAllText(reportPath, "===== story_arcs.json VALIDATION =====\n" + text, new UTF8Encoding(true));
            Debug.Log("===== story_arcs.json VALIDATION =====\n" + text);
            System.Console.WriteLine("===== story_arcs.json VALIDATION =====");
            System.Console.WriteLine(text);

            EditorApplication.Exit(0);
        }
    }
}