using UnityEngine;
using UnityEditor;
using System.IO;

namespace CustomEditorTools
{
    public static class ComponentJsonIO
    {
        [MenuItem("CONTEXT/Component/Copy to JSON (Editor)...")]
        static void ExportComponentToJson(MenuCommand command)
        {
            var component = command.context as Component;
            if (component == null) return;

            // Используем EditorJsonUtility - он мощнее и понимает [SerializeReference] и instanceID
            string json = EditorJsonUtility.ToJson(component, true);
            string defaultName = component.GetType().Name + ".json";
            string path = EditorUtility.SaveFilePanel("Export Component to JSON", "", defaultName, "json");

            if (!string.IsNullOrEmpty(path))
            {
                File.WriteAllText(path, json);
                Debug.Log($"Exported {component.GetType().Name} to {path}");
            }
        }

        [MenuItem("CONTEXT/Component/Paste from JSON (Editor)...")]
        static void ImportComponentFromJson(MenuCommand command)
        {
            var component = command.context as Component;
            if (component == null) return;

            string path = EditorUtility.OpenFilePanel("Import Component from JSON", "", "json");
            if (string.IsNullOrEmpty(path)) return;

            if (!File.Exists(path))
            {
                Debug.LogError("File not found!");
                return;
            }

            string json = File.ReadAllText(path);
            Undo.RecordObject(component, "Paste from JSON");

            try
            {
                // EditorJsonUtility корректно восстановит ссылки
                EditorJsonUtility.FromJsonOverwrite(json, component);
                EditorUtility.SetDirty(component);
                Debug.Log($"Imported {component.GetType().Name} from {path}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to import JSON: {e.Message}");
            }
        }
        
        // То же самое для ScriptableObject...
        [MenuItem("CONTEXT/ScriptableObject/Copy to JSON (Editor)...")]
        static void ExportSOToJson(MenuCommand command)
        {
            var so = command.context as ScriptableObject;
            if (so == null) return;
            string json = EditorJsonUtility.ToJson(so, true);
            string path = EditorUtility.SaveFilePanel("Export SO", "", so.name + ".json", "json");
            if (!string.IsNullOrEmpty(path)) File.WriteAllText(path, json);
        }

        [MenuItem("CONTEXT/ScriptableObject/Paste from JSON (Editor)...")]
        static void ImportSOFromJson(MenuCommand command)
        {
            var so = command.context as ScriptableObject;
            if (so == null) return;
            string path = EditorUtility.OpenFilePanel("Import SO", "", "json");
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
            string json = File.ReadAllText(path);
            Undo.RecordObject(so, "Paste JSON");
            EditorJsonUtility.FromJsonOverwrite(json, so);
            EditorUtility.SetDirty(so);
        }
    }
}