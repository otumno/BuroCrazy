// Файл: ThoughtCollectionEditor.cs (ДОЛЖЕН НАХОДИТЬСЯ В ПАПКЕ 'Editor')
using UnityEngine;
using UnityEditor;
using System.IO;

// Этот атрибут говорит Unity, что данный скрипт является кастомным редактором для ThoughtCollection
[CustomEditor(typeof(ThoughtCollection))]
public class ThoughtCollectionEditor : Editor
{
    // --- Вспомогательные классы для парсинга JSON ---
    // Они должны точно соответствовать структуре вашего JSON-файла
    [System.Serializable]
    private class ThoughtTiers_JSON
    {
        public string[] greenTierTexts;
        public string[] yellowTierTexts;
        public string[] orangeTierTexts;
        public string[] redTierTexts;
    }

    [System.Serializable]
    private class ActivityThought_JSON
    {
        public string activityKey;
        public ThoughtTiers_JSON thoughts;
    }

    [System.Serializable]
    private class ThoughtCollection_JSON
    {
        public ActivityThought_JSON[] allThoughts;
    }
    // --- Конец вспомогательных классов ---

    // Переопределяем то, как инспектор для ThoughtCollection будет выглядеть
    public override void OnInspectorGUI()
    {
        // Сначала рисуем стандартный инспектор (чтобы видеть списки мыслей)
        DrawDefaultInspector();

        // Получаем доступ к нашему ScriptableObject
        ThoughtCollection thoughtCollection = (ThoughtCollection)target;

        // Добавляем большую красивую кнопку
        if (GUILayout.Button("Импортировать мысли из JSON", GUILayout.Height(40)))
        {
            ImportThoughts(thoughtCollection);
        }
    }

    private void ImportThoughts(ThoughtCollection collection)
    {
        // Указываем путь к нашему файлу
        string path = Path.Combine(Application.dataPath, "thoughts.json");

        if (File.Exists(path))
        {
            string jsonContent = File.ReadAllText(path);

            // Используем встроенный в Unity JsonUtility для парсинга
            ThoughtCollection_JSON data = JsonUtility.FromJson<ThoughtCollection_JSON>(jsonContent);

            if (data != null && data.allThoughts != null)
            {
                // Очищаем старые данные в ScriptableObject
                collection.allThoughts.Clear();

                // Проходим по всем импортированным мыслям и добавляем их в наш ассет
                foreach (var thoughtData in data.allThoughts)
                {
                    var newActivityThought = new ThoughtCollection.ActivityThought
                    {
                        activityKey = thoughtData.activityKey,
                        thoughts = new ThoughtCollection.ThoughtTiers
                        {
                            greenTierTexts = new System.Collections.Generic.List<string>(thoughtData.thoughts.greenTierTexts),
                            yellowTierTexts = new System.Collections.Generic.List<string>(thoughtData.thoughts.yellowTierTexts),
                            orangeTierTexts = new System.Collections.Generic.List<string>(thoughtData.thoughts.orangeTierTexts),
                            redTierTexts = new System.Collections.Generic.List<string>(thoughtData.thoughts.redTierTexts)
                        }
                    };
                    collection.allThoughts.Add(newActivityThought);
                }

                // Очень важные строки: сохраняем изменения в ассете
                EditorUtility.SetDirty(collection);
                AssetDatabase.SaveAssets();

                Debug.Log($"<color=green>Импорт мыслей из {path} успешно завершен!</color>");
            }
            else
            {
                Debug.LogError("Ошибка парсинга JSON. Проверьте структуру файла thoughts.json.");
            }
        }
        else
        {
            Debug.LogError($"Файл не найден по пути: {path}. Убедитесь, что thoughts.json находится в папке Assets.");
        }
    }
}