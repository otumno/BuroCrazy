using UnityEngine;
using UnityEditor;
using Data;

namespace Editor
{
    public class ClientGreetingDatabaseCreator : EditorWindow
    {
        [MenuItem("Bureau/Create Client Greeting Database")]
        public static void Create()
        {
            // Проверяем, существует ли база
            var existing = Resources.Load<ClientGreetingDatabase>("ClientGreetingDatabase");
            if (existing != null)
            {
                Debug.LogWarning("ClientGreetingDatabase уже существует в Resources!");
                Selection.activeObject = existing;
                return;
            }

            // Создаём новую базу
            var database = ScriptableObject.CreateInstance<ClientGreetingDatabase>();
            database.name = "ClientGreetingDatabase";

            // Путь для сохранения
            string path = "Assets/Resources/ClientGreetingDatabase.asset";

            // Создаём директорию если нужно
            if (!System.IO.Directory.Exists(System.IO.Path.GetDirectoryName(path)))
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            }

            // Сохраняем
            AssetDatabase.CreateAsset(database, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Создано: {path}");
            Selection.activeObject = database;
        }
    }
}
