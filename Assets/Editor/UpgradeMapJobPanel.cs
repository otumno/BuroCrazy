// Assets/Editor/UpgradeMapJobPanel.cs
// Editor-скрипт для доработки префаба MapJobPanel и привязки
// ссылок в MapJobInfoPanelUI + MapPanelUI.
//
// Меню: Tools/Bureau/Upgrade Map Job Panel
//
// Что делает:
//   1. Находит GameObject "MapJobPanel" в открытой сцене.
//   2. Добавляет компонент MapJobInfoPanelUI (если нет).
//   3. Создаёт/находит дочерние TMP-объекты:
//      Title Text, Description Text (уже существуют — не трогает),
//      CaseTitleText, CorrectPathText, AlternatePathText.
//   4. Создаёт две кнопки CorrectPathButton и AlternatePathButton
//      с дочерними TMP Label, используя спрайт Button1.png как визуал.
//   5. Привязывает все ссылки в MapJobInfoPanelUI.
//   6. Добавляет JobTreeConnectionVisualizer на TreeZone.
//   7. Привязывает treeZone и jobInfoPanelUI в MapPanelUI.
//   8. Сохраняет сцену.
//
// Идемпотентен: повторный запуск не создаёт дубликатов.
#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using Scriptables.Progression;
using UI.Map;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public static class UpgradeMapJobPanel
{
    private const string LogTag = "[UpgradeMapJobPanel]";
    private const string MapJobPanelName = "MapJobPanel";
    private const string TreeZoneName = "TreeZone";
    private const string MapPanelName = "MapPanel";
    private const string ButtonSpritePath = "Assets/Sprites/UI/Button1.png";

    // Имена дочерних объектов, которые мы создаём/ищем
    private const string N_TitleText = "Title Text";
    private const string N_DescriptionText = "Description Text";
    private const string N_CostText = "Cost Text";
    private const string N_Button = "Button";

    private const string N_CaseTitleText = "CaseTitleText";
    private const string N_CorrectPathText = "CorrectPathText";
    private const string N_AlternatePathText = "AlternatePathText";

    private const string N_CorrectPathButton = "CorrectPathButton";
    private const string N_AlternatePathButton = "AlternatePathButton";
    private const string N_ButtonLabel = "Label";

    [MenuItem("Tools/Bureau/Upgrade Map Job Panel")]
    public static void Upgrade()
    {
        var activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid() || !activeScene.isLoaded)
        {
            Debug.LogError($"{LogTag} Нет открытой сцены.");
            return;
        }

        // ----- 1. Найти MapJobPanel -----
        GameObject mapJobPanel = FindInOpenScene(MapJobPanelName);
        if (mapJobPanel == null)
        {
            Debug.LogError($"{LogTag} GameObject '{MapJobPanelName}' не найден в открытой сцене.");
            return;
        }
        Debug.Log($"{LogTag} Найден: {GetTransformPath(mapJobPanel.transform)}");

        // ----- 2. Добавить MapJobInfoPanelUI -----
        var infoUI = mapJobPanel.GetComponent<MapJobInfoPanelUI>();
        if (infoUI == null)
        {
            infoUI = mapJobPanel.AddComponent<MapJobInfoPanelUI>();
            Debug.Log($"{LogTag} Добавлен компонент MapJobInfoPanelUI.");
        }

        // Загрузить спрайт кнопки один раз
        Sprite buttonSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ButtonSpritePath);

        // ----- 3. Найти/создать TMP-тексты -----
        var titleText = FindOrCreateTMP(mapJobPanel.transform, N_TitleText);
        var descriptionText = FindOrCreateTMP(mapJobPanel.transform, N_DescriptionText);
        // N_CostText не трогаем — оставляем как есть (используется в старой логике)
        var caseTitleText = FindOrCreateTMP(mapJobPanel.transform, N_CaseTitleText);
        var correctPathText = FindOrCreateTMP(mapJobPanel.transform, N_CorrectPathText);
        var alternatePathText = FindOrCreateTMP(mapJobPanel.transform, N_AlternatePathText);

        // ----- 4. Найти существующий Button (для копирования стиля) -----
        GameObject existingButton = FindInOpenSceneFrom(mapJobPanel.transform, N_Button);
        Image sourceButtonImage = existingButton != null ? existingButton.GetComponent<Image>() : null;
        Sprite baseSprite = sourceButtonImage != null ? sourceButtonImage.sprite : buttonSprite;

        // ----- 5. Найти/создать кнопки -----
        var correctPathBtnObj = FindOrCreateButton(
            mapJobPanel.transform, N_CorrectPathButton, baseSprite, sourceButtonImage);
        var alternatePathBtnObj = FindOrCreateButton(
            mapJobPanel.transform, N_AlternatePathButton, baseSprite, sourceButtonImage);

        var correctPathLabel = correctPathBtnObj.GetComponentsInChildren<TextMeshProUGUI>(true)
            .FirstOrDefault(t => t.gameObject.name == N_ButtonLabel);
        var alternatePathLabel = alternatePathBtnObj.GetComponentsInChildren<TextMeshProUGUI>(true)
            .FirstOrDefault(t => t.gameObject.name == N_ButtonLabel);

        // ----- 6. Привязать ссылки в MapJobInfoPanelUI -----
        var soInfo = new SerializedObject(infoUI);
        soInfo.FindProperty("titleText").objectReferenceValue = titleText;
        soInfo.FindProperty("descriptionText").objectReferenceValue = descriptionText;
        soInfo.FindProperty("caseTitleText").objectReferenceValue = caseTitleText;
        soInfo.FindProperty("correctPathText").objectReferenceValue = correctPathText;
        soInfo.FindProperty("alternatePathText").objectReferenceValue = alternatePathText;
        soInfo.FindProperty("correctPathButton").objectReferenceValue = correctPathBtnObj.GetComponent<Button>();
        soInfo.FindProperty("correctPathButtonLabel").objectReferenceValue = correctPathLabel;
        soInfo.FindProperty("alternatePathButton").objectReferenceValue = alternatePathBtnObj.GetComponent<Button>();
        soInfo.FindProperty("alternatePathButtonLabel").objectReferenceValue = alternatePathLabel;
        soInfo.ApplyModifiedPropertiesWithoutUndo();
        Debug.Log($"{LogTag} Привязаны ссылки в MapJobInfoPanelUI.");

        // ----- 7. Добавить JobTreeConnectionVisualizer на TreeZone -----
        GameObject treeZone = FindInOpenScene(TreeZoneName);
        if (treeZone != null)
        {
            var visualizers = treeZone.GetComponents<JobTreeConnectionVisualizer>();
            if (visualizers.Length == 0)
            {
                var v = treeZone.AddComponent<JobTreeConnectionVisualizer>();
                Debug.Log($"{LogTag} Добавлен JobTreeConnectionVisualizer на '{TreeZoneName}' ({GetTransformPath(treeZone.transform)}).");
            }
            else if (visualizers.Length > 1)
            {
                // Удалить дубликаты (если случайно добавлено несколько)
                for (int i = 1; i < visualizers.Length; i++)
                {
                    Object.DestroyImmediate(visualizers[i]);
                    Debug.Log($"{LogTag} Удалён дубликат JobTreeConnectionVisualizer #{i}.");
                }
            }
            else
            {
                Debug.Log($"{LogTag} JobTreeConnectionVisualizer уже есть на '{TreeZoneName}' — OK.");
            }
        }
        else
        {
            Debug.LogWarning($"{LogTag} '{TreeZoneName}' не найден — JobTreeConnectionVisualizer не добавлен.");
        }

        // ----- 8. Привязать treeZone и jobInfoPanelUI в MapPanelUI -----
        MapPanelUI mapPanelUI = FindMapPanelUI();
        if (mapPanelUI != null)
        {
            var soPanel = new SerializedObject(mapPanelUI);
            var propTree = soPanel.FindProperty("treeZone");
            if (propTree != null && treeZone != null)
            {
                propTree.objectReferenceValue = treeZone.transform;
                soPanel.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log($"{LogTag} Привязан treeZone в MapPanelUI.");
            }
            var propInfo = soPanel.FindProperty("jobInfoPanelUI");
            if (propInfo != null && infoUI != null)
            {
                propInfo.objectReferenceValue = infoUI;
                soPanel.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log($"{LogTag} Привязан jobInfoPanelUI в MapPanelUI.");
            }
        }
        else
        {
            Debug.LogWarning($"{LogTag} MapPanelUI не найден в сцене.");
        }

        // ----- 9. Сохранить -----
        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorUtility.SetDirty(infoUI);
        if (mapJobPanel != null) EditorUtility.SetDirty(mapJobPanel);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"{LogTag} Готово. MapJobPanel доработан: добавлены TMP/кнопки/визуализатор.");
    }

    /// <summary>
    /// Автоматическая раскладка JobNode_* по тиру (tierLevel).
    /// tier 0 — вверху, tier 8 — внизу. Внутри одного тира — горизонтальный ряд.
    /// Вызывайте вручную через меню.
    /// </summary>
    [MenuItem("Tools/Bureau/Auto Layout Career Tree")]
    public static void AutoLayout()
    {
        var treeZone = FindInOpenScene(TreeZoneName);
        if (treeZone == null)
        {
            Debug.LogError($"{LogTag} TreeZone не найден.");
            return;
        }

        var byTier = new SortedDictionary<int, List<Transform>>();
        var nodes = treeZone.GetComponentsInChildren<JobNodeUI>(includeInactive: true);
        foreach (var n in nodes)
        {
            if (n == null || n.jobData == null) continue;
            if (!byTier.TryGetValue(n.jobData.tierLevel, out var list))
            {
                list = new List<Transform>();
                byTier[n.jobData.tierLevel] = list;
            }
            list.Add(n.transform);
        }

        // Сортируем внутри тира по jobID для стабильного порядка
        foreach (var kvp in byTier)
        {
            kvp.Value.Sort((a, b) =>
            {
                var na = a.GetComponent<JobNodeUI>();
                var nb = b.GetComponent<JobNodeUI>();
                return string.Compare(na?.jobData?.jobID, nb?.jobData?.jobID);
            });
        }

        const float xSpacing = 150f;
        const float ySpacing = -130f; // ноды идут сверху вниз
        Vector2 origin = new Vector2(-300, 250);

        foreach (var kvp in byTier)
        {
            int count = kvp.Value.Count;
            float rowWidth = (count - 1) * xSpacing;
            for (int i = 0; i < count; i++)
            {
                var t = kvp.Value[i];
                t.localPosition = new Vector3(
                    origin.x + i * xSpacing - rowWidth * 0.5f,
                    origin.y + kvp.Key * ySpacing,
                    0f);
                EditorUtility.SetDirty(t);
            }
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log($"{LogTag} Раскладка применена: {nodes.Length} нод по {byTier.Count} тирам.");
    }

    // ----- Найти существующий TMP среди детей или создать новый -----
    private static TextMeshProUGUI FindOrCreateTMP(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name)
            {
                var existing = child.GetComponent<TextMeshProUGUI>();
                if (existing != null) return existing;
            }
        }
        return CreateTMP(parent, name);
    }

    private static TextMeshProUGUI CreateTMP(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localScale = Vector3.one;

        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(200, 40);

        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = name;
        tmp.fontSize = 14;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.color = new Color(0.9f, 0.9f, 0.9f, 1f);

        Debug.Log($"{LogTag} Создан TMP: {GetTransformPath(go.transform)}");
        return tmp;
    }

    // ----- Найти существующую кнопку среди детей или создать новую -----
    private static GameObject FindOrCreateButton(Transform parent, string name, Sprite baseSprite, Image sourceImage)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name)
            {
                var btn = child.GetComponent<Button>();
                if (btn != null) return child.gameObject;
            }
        }
        return CreateButton(parent, name, baseSprite, sourceImage);
    }

    private static GameObject CreateButton(Transform parent, string name, Sprite baseSprite, Image sourceImage)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localScale = Vector3.one;

        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(200, 50);

        var img = go.GetComponent<Image>();
        img.sprite = baseSprite;
        // Скопировать цвет с sourceImage (если есть), иначе белый
        img.color = sourceImage != null ? sourceImage.color : Color.white;

        var btn = go.GetComponent<Button>();
        // Скопировать targetGraphic, transition и sprite state
        if (sourceImage != null)
        {
            btn.targetGraphic = img;
            btn.transition = sourceImage != null && sourceImage.gameObject.GetComponent<Button>() != null
                ? Selectable.Transition.SpriteSwap
                : Selectable.Transition.ColorTint;
        }

        // Дочерний Label (TMP)
        var labelGo = new GameObject(N_ButtonLabel, typeof(RectTransform), typeof(TextMeshProUGUI));
        labelGo.transform.SetParent(go.transform, false);
        labelGo.transform.localPosition = Vector3.zero;
        labelGo.transform.localScale = Vector3.one;

        var labelRt = labelGo.GetComponent<RectTransform>();
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.sizeDelta = Vector2.zero;
        labelRt.anchoredPosition = Vector2.zero;

        var labelTmp = labelGo.GetComponent<TextMeshProUGUI>();
        labelTmp.text = name.Contains("Correct") ? "Правильный путь" : "Обходной путь";
        labelTmp.fontSize = 18;
        labelTmp.alignment = TextAlignmentOptions.Center;
        labelTmp.color = Color.black;

        Debug.Log($"{LogTag} Создана кнопка: {GetTransformPath(go.transform)}");
        return go;
    }

    // ----- Поиск MapPanelUI в сцене -----
    private static MapPanelUI FindMapPanelUI()
    {
        return Object.FindFirstObjectByType<MapPanelUI>();
    }

    // ----- Поиск GameObject по имени в открытой сцене (включая неактивные) -----
    private static GameObject FindInOpenScene(string name)
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded) return null;

        foreach (var root in scene.GetRootGameObjects())
        {
            var found = FindRecursive(root.transform, name);
            if (found != null) return found.gameObject;
        }
        return null;
    }

    private static Transform FindRecursive(Transform parent, string name)
    {
        if (parent.name == name) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            var f = FindRecursive(parent.GetChild(i), name);
            if (f != null) return f;
        }
        return null;
    }

    // Поиск среди прямых (или вложенных) детей указанного parent
    private static GameObject FindInOpenSceneFrom(Transform parent, string name)
    {
        var found = FindRecursive(parent, name);
        return found != null ? found.gameObject : null;
    }

    // Получить полный путь трансформа в иерархии
    private static string GetTransformPath(Transform t)
    {
        if (t == null) return "<null>";
        if (t.parent == null) return t.name;
        return GetTransformPath(t.parent) + "/" + t.name;
    }
}
#endif
