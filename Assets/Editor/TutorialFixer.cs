using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using Utilities;
using UnityEngine.UI; // Для поиска Image/Button

public class TutorialFixer : EditorWindow
{
    string jsonString = "";
    TutorialScreenConfig target;

    [MenuItem("Tools/Fix Tutorial Data")]
    public static void ShowWindow()
    {
        GetWindow<TutorialFixer>("Tutorial Fixer");
    }

    void OnGUI()
    {
        GUILayout.Label("1. Выберите объект с TutorialScreenConfig", EditorStyles.boldLabel);
        target = (TutorialScreenConfig)EditorGUILayout.ObjectField("Target Config", target, typeof(TutorialScreenConfig), true);

        GUILayout.Label("2. Вставьте содержимое TutorialScreenConfig_FLAT.json", EditorStyles.boldLabel);
        jsonString = EditorGUILayout.TextArea(jsonString, GUILayout.Height(200));

        if (GUILayout.Button("Восстановить данные") && target != null && !string.IsNullOrEmpty(jsonString))
        {
            RestoreData();
        }

        // Кнопка для попытки найти ссылки
        if (GUILayout.Button("Попытаться найти ссылки по именам") && target != null)
        {
            TryAutoLink();
        }
    }

    void RestoreData()
    {
        FlatConfigWrapper flatData = JsonUtility.FromJson<FlatConfigWrapper>(jsonString);

        if (flatData == null || flatData.contextGroups == null)
        {
            Debug.LogError("Ошибка JSON!");
            return;
        }

        Undo.RecordObject(target, "Restore Tutorial Data");
        target.contextGroups = new List<TutorialContextGroup>();

        foreach (var flatGroup in flatData.contextGroups)
        {
            TutorialContextGroup realGroup = new TutorialContextGroup();
            realGroup.contextID = flatGroup.contextID;
            realGroup.muteTutorial = flatGroup.muteTutorial;
            realGroup.greetingTexts = flatGroup.greetingTexts;
            realGroup.contextIdleTips = flatGroup.contextIdleTips;
            realGroup.helpSpots = flatGroup.helpSpots; 
            
            target.contextGroups.Add(realGroup);
        }

        EditorUtility.SetDirty(target);
        Debug.Log($"Данные восстановлены. Теперь нажмите 'Попытаться найти ссылки'.");
    }

    void TryAutoLink()
    {
        if (target.contextGroups == null) return;

        Undo.RecordObject(target, "Auto Link Tutorial Refs");
        int linkedCount = 0;

        // Ищем все RectTransform на сцене (включая неактивные)
        RectTransform[] allRects = Resources.FindObjectsOfTypeAll<RectTransform>();

        foreach (var group in target.contextGroups)
        {
            // 1. Пытаемся найти Context Panel по ID (например "MainMenu")
            if (group.contextPanel == null && !string.IsNullOrEmpty(group.contextID))
            {
                GameObject found = FindObjectByName(group.contextID);
                if (found != null)
                {
                    group.contextPanel = found;
                    linkedCount++;
                }
            }

            // 2. Пытаемся найти Target Elements для спотов
            if (group.helpSpots != null)
            {
                foreach (var spot in group.helpSpots)
                {
                    if (spot.targetElement == null && !string.IsNullOrEmpty(spot.spotID))
                    {
                        // Ищем объект, имя которого совпадает с spotID (или содержит его часть после "_")
                        // Пример: spotID = "MainMenu_Load", ищем объект "Load" или "Button_Load" или "MainMenu_Load"
                        
                        string searchKey = spot.spotID;
                        // Попробуем упростить имя (взять часть после подчеркивания)
                        string simpleName = spot.spotID.Contains("_") ? spot.spotID.Split('_')[1] : spot.spotID;

                        RectTransform foundRect = FindRectByName(allRects, searchKey); // Точное совпадение
                        if (foundRect == null) foundRect = FindRectByName(allRects, simpleName); // Частичное

                        if (foundRect != null)
                        {
                            spot.targetElement = foundRect;
                            linkedCount++;
                        }
                    }
                }
            }
        }
        
        EditorUtility.SetDirty(target);
        Debug.Log($"Автоматически найдено и привязано {linkedCount} объектов. Остальные придется назначить вручную.");
    }

    GameObject FindObjectByName(string name)
    {
        // Поиск среди всех объектов (медленно, но для эдитора пойдет)
        foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (go.hideFlags != HideFlags.None) continue; // Пропускаем скрытые системные объекты
            if (go.name.Equals(name, System.StringComparison.OrdinalIgnoreCase)) return go;
        }
        return null;
    }

    RectTransform FindRectByName(RectTransform[] list, string name)
    {
        foreach (var rect in list)
        {
            if (rect.gameObject.hideFlags != HideFlags.None) continue;
            // Ищем частичное совпадение для удобства
            if (rect.name.Equals(name, System.StringComparison.OrdinalIgnoreCase)) return rect;
        }
        return null;
    }

    [System.Serializable]
    class FlatConfigWrapper
    {
        public List<TutorialContextGroup> contextGroups;
    }
}