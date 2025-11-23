using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement; // Нужно для удобной работы со списками

namespace Editor
{
    public class HierarchyExporter : UnityEditor.Editor
    {
        [MenuItem("Tools/Copy Hierarchy with Components")]
        public static void CopyHierarchy()
        {
            StringBuilder sb = new StringBuilder();
        
            GameObject[] selectedObjects = Selection.gameObjects;

            if (selectedObjects.Length > 0)
            {
                sb.AppendLine("--- SELECTED HIERARCHY (WITH COMPONENTS) ---");
                foreach (GameObject obj in selectedObjects)
                {
                    Traverse(obj.transform, sb, 0);
                }
                sb.AppendLine("--- END ---");
            }
            else
            {
                sb.AppendLine("--- FULL SCENE HIERARCHY (WITH COMPONENTS) ---");
                Scene currentScene = SceneManager.GetActiveScene();
                GameObject[] rootObjects = currentScene.GetRootGameObjects();

                foreach (GameObject obj in rootObjects)
                {
                    Traverse(obj.transform, sb, 0);
                }
                sb.AppendLine("--- END ---");
            }

            GUIUtility.systemCopyBuffer = sb.ToString();
            Debug.Log($"Hierarchy with components copied! ({sb.Length} chars)");
        }

        private static void Traverse(Transform obj, StringBuilder sb, int indentLevel)
        {
            string indent = new string('-', indentLevel * 2); // Отступы тире для визуализации
            string prefabInfo = GetPrefabStatus(obj.gameObject);
        
            // ПОЛУЧЕНИЕ КОМПОНЕНТОВ
            // 1. Берем все компоненты
            // 2. Исключаем Transform (он есть у всех, только захламляет вид)
            // 3. Превращаем в список имен
            var components = obj.GetComponents<Component>()
                .Where(c => c != null) // Защита от "битых" скриптов
                .Where(c => c.GetType() != typeof(Transform)) // Скрываем Transform для чистоты
                .Select(c => c.GetType().Name)
                .ToArray();

            // Формируем строку списка: (Rigidbody, BoxCollider, MyScript)
            string compString = components.Length > 0 ? $"   --> [{string.Join(", ", components)}]" : "";

            // Собираем всё вместе
            sb.AppendLine($"{indent}{obj.name} {prefabInfo}{compString}");

            foreach (Transform child in obj)
            {
                Traverse(child, sb, indentLevel + 1);
            }
        }

        private static string GetPrefabStatus(GameObject go)
        {
            bool isPartOfPrefab = PrefabUtility.IsPartOfPrefabInstance(go);
            if (!isPartOfPrefab) return "";

            bool isRoot = PrefabUtility.IsAnyPrefabInstanceRoot(go);
            if (isRoot)
            {
                GameObject sourcePrefab = PrefabUtility.GetCorrespondingObjectFromSource(go);
                string sourceName = sourcePrefab != null ? sourcePrefab.name : "Unknown";
                return $"[PREFAB: {sourceName}]";
            }
            else
            {
                return "[P-Part]"; // Сократил, чтобы строка не была слишком длинной
            }
        }
    }
}