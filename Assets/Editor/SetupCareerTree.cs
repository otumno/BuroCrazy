// Assets/Editor/SetupCareerTree.cs
// Editor-скрипт для создания 12 заглушек-нод в TreeZone на сцене.
//
// Меню:
//   Tools/Bureau/Setup Career Tree  — создаёт ноды (с подтверждением очистки)
//
// Логика:
//   1. Находит TreeZone (GameObject.Find + поиск по сцене).
//   2. Загружает 12 JobTitleData из Resources/Progression/Jobs/.
//   3. Создаёт/переиспользует префаб Assets/Prefabs/UI/JobNodeUI.prefab.
//   4. С подтверждением очищает TreeZone и создаёт 12 экземпляров.
//   5. Обновляет MapJobPanel.currentJobTitleUI.Refresh() (если MapJobPanel найден).
//   6. SaveAssets + Refresh.
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Scriptables.Progression;
using UI.Effects;
using UI.Map;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public static class SetupCareerTree
{
    private const string LogTag = "[SetupCareerTree]";
    private const string PrefabPath = "Assets/Prefabs/UI/JobNodeUI.prefab";
    private const string TreeZoneName = "TreeZone";
    private const string MapJobPanelName = "MapJobPanel";
    private const string ResourcesPath = "Progression/Jobs";

    [MenuItem("Tools/Bureau/Setup Career Tree")]
    public static void Setup()
    {
        // ----- 1. Найти TreeZone -----
        GameObject treeZone = FindInOpenScene(TreeZoneName);
        if (treeZone == null)
        {
            Debug.LogError($"{LogTag} GameObject '{TreeZoneName}' не найден в открытой сцене. Откройте сцену с MapPanel и попробуйте снова.");
            return;
        }

        // ----- 2. Найти MapJobPanel (опционально) -----
        // Сначала ищем по имени, потом — по компоненту MapPanelUI (если имя не нашлось).
        GameObject mapJobPanel = FindInOpenScene(MapJobPanelName);
        if (mapJobPanel == null)
        {
            var mapPanelUI = Object.FindFirstObjectByType<MapPanelUI>();
            if (mapPanelUI != null)
            {
                mapJobPanel = mapPanelUI.gameObject;
                Debug.Log($"{LogTag} MapJobPanel не найден по имени — используем GameObject с компонентом MapPanelUI: {GetTransformPath(mapJobPanel.transform)}");
            }
            else
            {
                Debug.LogWarning($"{LogTag} MapJobPanel не найден и нет объекта с MapPanelUI. CurrentJobTitleUI не будет создан автоматически.");
            }
        }

        // ----- 3. Загрузить 12 JobTitleData -----
        var jobs = Resources.LoadAll<JobTitleData>(ResourcesPath).ToList();
        if (jobs.Count == 0)
        {
            Debug.LogError($"{LogTag} Не найдены JobTitleData в Resources/{ResourcesPath}/. Сначала запустите Tools/Bureau/Create Job Titles.");
            return;
        }

        // Сортировка: по tierLevel ↑, затем по titleName алфавитно
        jobs = jobs.OrderBy(j => j.tierLevel).ThenBy(j => j.titleName).ToList();
        Debug.Log($"{LogTag} Загружено {jobs.Count} должностей.");

        // ----- 4. Создать/переиспользовать префаб -----
        GameObject nodePrefab = EnsurePrefab();
        if (nodePrefab == null)
        {
            Debug.LogError($"{LogTag} Не удалось создать/загрузить префаб {PrefabPath}.");
            return;
        }

        // ----- 5. Удалить ТОЛЬКО существующие JobNode_* ноды (сохраняя MapJobPanel и прочее) -----
        int removedNodes = 0;
        for (int i = treeZone.transform.childCount - 1; i >= 0; i--)
        {
            Transform child = treeZone.transform.GetChild(i);
            if (child.name.StartsWith("JobNode_"))
            {
                Object.DestroyImmediate(child.gameObject);
                removedNodes++;
            }
        }
        if (removedNodes > 0)
        {
            Debug.Log($"{LogTag} Удалено {removedNodes} старых JobNode_* нод (MapJobPanel сохранён).");
        }

        // ----- 6. Создать 12 экземпляров -----
        var createdNodes = new List<GameObject>();
        foreach (var job in jobs)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(nodePrefab, treeZone.transform);
            instance.name = $"JobNode_{job.jobID}";
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localScale = Vector3.one;

            var nodeUI = instance.GetComponent<JobNodeUI>();
            if (nodeUI != null)
            {
                // Временный обработчик клика — реальный будет в Промпте 4
                nodeUI.Setup(job, OnJobNodeClickedEditor);
                createdNodes.Add(instance);
            }
            else
            {
                Debug.LogWarning($"{LogTag} JobNodeUI не найден на инстансе префаба для {job.jobID}.");
            }
        }

        Debug.Log($"{LogTag} Создано {createdNodes.Count} нод в '{TreeZoneName}'. Префаб: {(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null ? "OK" : "FAIL")}.");

        // ----- 7. Создать/привязать CurrentJobTitleUI в MapJobPanel -----
        Debug.Log($"{LogTag} [step7] mapJobPanel = {(mapJobPanel != null ? GetTransformPath(mapJobPanel.transform) : "<null>")}");

        if (mapJobPanel == null)
        {
            Debug.LogWarning($"{LogTag} mapJobPanel == null — CurrentJobTitleUI не будет создан автоматически. Создайте GameObject с этим именем или с компонентом MapPanelUI.");
        }
        else
        {
            // Найти компонент MapPanelUI: сначала на найденном объекте, потом — поиск по всей сцене
            var mapPanelUI = mapJobPanel.GetComponent<MapPanelUI>();
            if (mapPanelUI == null)
            {
                Debug.LogWarning($"{LogTag} На '{GetTransformPath(mapJobPanel.transform)}' нет компонента MapPanelUI. Ищу MapPanelUI в сцене...");
                mapPanelUI = Object.FindFirstObjectByType<MapPanelUI>();
                if (mapPanelUI != null)
                {
                    Debug.Log($"{LogTag} Найден MapPanelUI на: {GetTransformPath(mapPanelUI.transform)}");
                }
            }
            else
            {
                Debug.Log($"{LogTag} MapPanelUI найден на: {GetTransformPath(mapPanelUI.transform)}");
            }

            if (mapPanelUI == null)
            {
                Debug.LogWarning($"{LogTag} MapPanelUI не найден нигде в сцене. Создайте вручную.");
            }
            else
            {
                var so = new SerializedObject(mapPanelUI);
                var prop = so.FindProperty("currentJobTitleUI");
                Debug.Log($"{LogTag} Поле currentJobTitleUI найдено: {prop != null}");

                CurrentJobTitleUI titleUI = prop != null ? prop.objectReferenceValue as CurrentJobTitleUI : null;
                Debug.Log($"{LogTag} Текущий currentJobTitleUI = {(titleUI != null ? titleUI.gameObject.name : "<null>")}");

                // Если в MapJobPanel нет компонента — создаём дочерний GameObject
                if (titleUI == null)
                {
                    titleUI = EnsureCurrentJobTitleUI(mapJobPanel);
                    Debug.Log($"{LogTag} EnsureCurrentJobTitleUI вернул: {(titleUI != null ? titleUI.gameObject.name : "<null>")}");
                }

                // Обновить текст
                if (titleUI != null)
                {
                    titleUI.Refresh();
                    Debug.Log($"{LogTag} CurrentJobTitleUI привязан и обновлён.");
                }
                else
                {
                    Debug.LogWarning($"{LogTag} Не удалось создать/найти CurrentJobTitleUI. Привяжите вручную в инспекторе.");
                }
            }
        }

        // ----- 8. Пометить сцену грязной и сохранить -----
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"{LogTag} Готово.");
    }

    // ----- Временный обработчик клика (для Editor) -----
    private static void OnJobNodeClickedEditor(JobTitleData job)
    {
        Debug.Log($"{LogTag} Clicked: {job.titleName} ({job.jobID})");
    }

    // ----- Поиск GameObject в открытой сцене (включая неактивные) -----
    private static GameObject FindInOpenScene(string name)
    {
        // GameObject.Find ищет только активные; используем Resources.FindObjectsOfTypeAll
        // но он включает префабы. Безопаснее пройти по корню сцены.
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

    // ----- Получить полный путь трансформа в иерархии -----
    private static string GetTransformPath(Transform t)
    {
        if (t == null) return "<null>";
        if (t.parent == null) return t.name;
        return GetTransformPath(t.parent) + "/" + t.name;
    }

    // ----- Создать или переиспользовать префаб JobNodeUI -----
    private static GameObject EnsurePrefab()
    {
        // Проверяем существование префаба
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (existing != null)
        {
            Debug.Log($"{LogTag} Префаб уже существует: {PrefabPath}");
            return existing;
        }

        // Создаём новый GameObject с компонентами
        GameObject go = new GameObject("JobNodeUI",
            typeof(RectTransform),
            typeof(Image),
            typeof(Button),
            typeof(JobNodeUI),
            typeof(UIButtonJuice));

        // Дочерние объекты
        GameObject icon = CreateChild(go.transform, "Icon", typeof(RectTransform), typeof(Image));
        GameObject title = CreateChild(go.transform, "TitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
        GameObject locked = CreateChild(go.transform, "LockedOverlay", typeof(RectTransform), typeof(Image));

        // Связать ссылки в JobNodeUI через SerializedObject (для сохранения в префабе)
        var jobNodeUI = go.GetComponent<JobNodeUI>();
        var so = new SerializedObject(jobNodeUI);
        so.FindProperty("titleText").objectReferenceValue = title.GetComponent<TextMeshProUGUI>();
        so.FindProperty("bgImage").objectReferenceValue = go.GetComponent<Image>();
        so.FindProperty("selectButton").objectReferenceValue = go.GetComponent<Button>();
        so.FindProperty("lockedOverlay").objectReferenceValue = locked;
        so.FindProperty("iconImage").objectReferenceValue = icon.GetComponent<Image>();
        // frameImage оставляем null — пользователь привяжет вручную если нужно
        so.ApplyModifiedPropertiesWithoutUndo();

        // Минимальные размеры (заглушки — пользователь поправит)
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(120, 120);

        icon.GetComponent<RectTransform>().sizeDelta = new Vector2(80, 80);
        title.GetComponent<RectTransform>().sizeDelta = new Vector2(110, 30);
        title.GetComponent<TextMeshProUGUI>().text = "Title";
        title.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;

        locked.GetComponent<RectTransform>().sizeDelta = new Vector2(120, 120);
        locked.GetComponent<Image>().color = new Color(0, 0, 0, 0.6f);
        locked.SetActive(false); // по умолчанию скрыт

        // Убедимся, что папка Prefabs/UI существует
        EnsureFolder("Assets/Prefabs");
        EnsureFolder("Assets/Prefabs/UI");

        // Сохранить как префаб
        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
        Object.DestroyImmediate(go);

        Debug.Log($"{LogTag} Префаб создан: {PrefabPath}");
        return savedPrefab;
    }

    private static GameObject CreateChild(Transform parent, string name, params System.Type[] components)
    {
        var go = new GameObject(name, components);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localScale = Vector3.one;
        return go;
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = Path.GetDirectoryName(folder).Replace("\\", "/");
        string leaf = Path.GetFileName(folder);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }

    // ----- Создать дочерний GameObject с CurrentJobTitleUI + TMP_Text и привязать к MapPanelUI -----
    private static CurrentJobTitleUI EnsureCurrentJobTitleUI(GameObject mapJobPanel)
    {
        // 1. Поискать существующий компонент CurrentJobTitleUI среди детей mapJobPanel
        var existing = mapJobPanel.GetComponentInChildren<CurrentJobTitleUI>(true);
        if (existing != null) return existing;

        // 2. Создать новый GameObject
        GameObject go = new GameObject("CurrentJobTitlePanel",
            typeof(RectTransform),
            typeof(TextMeshProUGUI),
            typeof(CurrentJobTitleUI));

        go.transform.SetParent(mapJobPanel.transform, false);

        // Минимальные размеры заглушки — пользователь поправит в инспекторе
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(360, 40);

        // Позиционируем в верхней части панели (anchor top-stretch)
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(0.5f, 1);
        rt.anchoredPosition = new Vector2(0, -10);

        // 3. Настроить TMP-текст
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = "Должность: —";
        tmp.fontSize = 18;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.85f, 0.85f, 0.85f, 1f);

        // 4. Привязать titleText → tmp в CurrentJobTitleUI
        var titleUI = go.GetComponent<CurrentJobTitleUI>();
        var so = new SerializedObject(titleUI);
        so.FindProperty("titleText").objectReferenceValue = tmp;
        so.ApplyModifiedPropertiesWithoutUndo();

        Debug.Log($"{LogTag} Создан GameObject 'CurrentJobTitlePanel' в '{MapJobPanelName}'.");

        // 5. Привязать titleUI → MapPanelUI.currentJobTitleUI
        var mapPanelUI = mapJobPanel.GetComponent<MapPanelUI>();
        if (mapPanelUI != null)
        {
            var mapSo = new SerializedObject(mapPanelUI);
            var prop = mapSo.FindProperty("currentJobTitleUI");
            if (prop != null)
            {
                prop.objectReferenceValue = titleUI;
                mapSo.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log($"{LogTag} Привязал currentJobTitleUI → {go.name}.");
            }
        }

        return titleUI;
    }
}
#endif
