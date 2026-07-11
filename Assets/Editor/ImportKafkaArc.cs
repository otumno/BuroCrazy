// Assets/Editor/ImportKafkaArc.cs
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using StorySystem.EditorTools;

namespace StorySystem.EditorTools
{
    public static class ImportKafkaArc
    {
        public static void Run()
        {
            string jsonPath = Path.Combine(Application.dataPath, "Editor", "Sample", "kafka_arc.json");
            if (!File.Exists(jsonPath))
            {
                Debug.LogError("Kafka JSON не найден: " + jsonPath);
                EditorApplication.Exit(1);
                return;
            }
            string json = File.ReadAllText(jsonPath);
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string reportPath = Path.Combine(projectRoot, "kafka_import_report.txt");
            string folder = "Assets/Resources/Arcs";

            var report = ArcJsonImporter.ImportJson(json, folder, true);
            File.WriteAllText(reportPath, "===== Kafka IMPORT =====\n" + report, new UTF8Encoding(true));
            Debug.Log("===== Kafka IMPORT =====\n" + report);
            EditorApplication.Exit(0);
        }
    }
}