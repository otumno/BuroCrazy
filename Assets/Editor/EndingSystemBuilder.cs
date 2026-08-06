// Assets/Editor/EndingSystemBuilder.cs
// Автоматическое создание всех ассетов, префабов и сценических объектов для системы концовок.
// Запуск: Tools → Bureau → Build Ending System
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using CinematicSystem;
using CinematicSystem.Nodes;
using Data;
using DialogueSystem.Data;
using Managers;
using TMPro;
using UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Characters;

public static class EndingSystemBuilder
{
    // ================== ПУТИ ==================
    private const string RESOURCES_ENDING = "Assets/Resources/EndingSystem";
    private const string RESOURCES_CINEMATIC = "Assets/Resources/CinematicGraphs";
    private const string RESOURCES_DIALOGUES = "Assets/Resources/Dialogues";
    private const string RESOURCES_DATABASES = "Assets/Resources/Databases";

    private const string PATH_ENDING_DB = RESOURCES_ENDING + "/EndingDatabase.asset";
    private const string PATH_INSPECTOR_CINEMATIC = RESOURCES_CINEMATIC + "/InspectorVisit.asset";
    private const string PATH_INSPECTOR_DIALOGUE = RESOURCES_DIALOGUES + "/InspectorDialogue.asset";

    private const string PATH_ENDING_BOOK_PREFAB = RESOURCES_ENDING + "/EndingBook.prefab";
    private const string PATH_DISMISSAL_PREFAB = RESOURCES_ENDING + "/DismissalScreen.prefab";
    private const string PATH_ARC_TITLE_PREFAB = RESOURCES_ENDING + "/ArcTitle.prefab";

    private const string ARCHETYPE_INSPECTOR_MALE = "Assets/Data/Archetypes/Archetype_Inspector_Male.asset";
    private const string ARCHETYPE_INSPECTOR_FEMALE = "Assets/Data/Archetypes/Archetype_Inspector_Female.asset";

    private const string GAME_SCENE_PATH = "Assets/Scenes/GameScene.unity";

    // ================== ENTRY POINT ==================
    [MenuItem("Tools/Bureau/Build Ending System (Full)")]
    public static void BuildAll()
    {
        Debug.Log("========== [EndingSystemBuilder] ЗАПУСК ПОЛНОЙ СБОРКИ ==========");

        EnsureFolder(RESOURCES_ENDING);
        EnsureFolder(RESOURCES_CINEMATIC);
        EnsureFolder(RESOURCES_DIALOGUES);

        var db = BuildEndingDatabase();
        BuildInspectorArchetypes();
        BuildEndingBookPrefab(db);
        BuildDismissalScreenPrefab(db);
        BuildArcTitlePrefab();
        var inspectorDialogue = BuildInspectorDialogueGraph();
        BuildInspectorCinematicGraph(inspectorDialogue);
        BuildScenePoints();
        BuildEndingSystemGameObject(db, inspectorDialogue);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("========== [EndingSystemBuilder] СБОРКА ЗАВЕРШЕНА ==========");
    }

    // Только подмножества (для удобства повторных запусков).
    [MenuItem("Tools/Bureau/Ending System/1. Build Ending Database")]
    public static void BuildDBStep() => BuildEndingDatabase();

    [MenuItem("Tools/Bureau/Ending System/2. Build Inspector Archetypes")]
    public static void BuildArchetypesStep() => BuildInspectorArchetypes();

    [MenuItem("Tools/Bureau/Ending System/3. Build UI Prefabs")]
    public static void BuildUIPrefabsStep()
    {
        EnsureFolder(RESOURCES_ENDING);
        var db = BuildEndingDatabase();
        BuildEndingBookPrefab(db);
        BuildDismissalScreenPrefab(db);
        BuildArcTitlePrefab();
    }

    [MenuItem("Tools/Bureau/Ending System/4. Build Inspector Cinematic")]
    public static void BuildCinematicStep() => BuildInspectorCinematicGraph(BuildInspectorDialogueGraph());

    [MenuItem("Tools/Bureau/Ending System/5. Build Scene Points")]
    public static void BuildScenePointsStep() => BuildScenePoints();

    [MenuItem("Tools/Bureau/Ending System/6. Build ENDING_SYSTEM GameObject")]
    public static void BuildEndingSystemGOStep() => BuildEndingSystemGameObject(BuildEndingDatabase(), BuildInspectorDialogueGraph());

    [MenuItem("Tools/Bureau/Ending System/Validate Build")]
    public static void ValidateBuild()
    {
        Debug.Log("========== [EndingSystemBuilder] ВАЛИДАЦИЯ ==========");
        int errors = 0;

        // EndingDatabase
        var db = AssetDatabase.LoadAssetAtPath<EndingDatabase>(PATH_ENDING_DB);
        if (db == null)
        {
            Debug.LogError($"[Validate] ❌ EndingDatabase не найден: {PATH_ENDING_DB}");
            errors++;
        }
        else
        {
            Debug.Log($"[Validate] ✅ EndingDatabase: {db.endings.Count} концовок");
            foreach (var entry in db.endings)
            {
                if (entry == null || string.IsNullOrEmpty(entry.endingID))
                {
                    Debug.LogError("[Validate] ❌ В EndingDatabase есть пустые entries");
                    errors++;
                }
                else
                {
                    Debug.Log($"[Validate]   • {entry.endingID} → '{entry.displayName}' (dismissal={entry.isDismissal})");
                }
            }
        }

        // Inspector архетипы
        var arcMale = AssetDatabase.LoadAssetAtPath<ClientArchetype>(ARCHETYPE_INSPECTOR_MALE);
        var arcFemale = AssetDatabase.LoadAssetAtPath<ClientArchetype>(ARCHETYPE_INSPECTOR_FEMALE);
        if (arcMale == null) { Debug.LogError($"[Validate] ❌ {ARCHETYPE_INSPECTOR_MALE} не найден"); errors++; }
        else Debug.Log($"[Validate] ✅ {arcMale.archetypeID}");
        if (arcFemale == null) { Debug.LogError($"[Validate] ❌ {ARCHETYPE_INSPECTOR_FEMALE} не найден"); errors++; }
        else Debug.Log($"[Validate] ✅ {arcFemale.archetypeID}");

        // Префабы
        CheckPrefab(PATH_ENDING_BOOK_PREFAB, "EndingBookUI", ref errors);
        CheckPrefab(PATH_DISMISSAL_PREFAB, "DismissalScreenUI", ref errors);
        CheckPrefab(PATH_ARC_TITLE_PREFAB, "ArcTitleDisplay", ref errors);

        // Cinematic Graph
        var cg = AssetDatabase.LoadAssetAtPath<CinematicGraph>(PATH_INSPECTOR_CINEMATIC);
        if (cg == null) { Debug.LogError($"[Validate] ❌ {PATH_INSPECTOR_CINEMATIC} не найден"); errors++; }
        else Debug.Log($"[Validate] ✅ InspectorVisit ({cg.allNodes.Count} нод, startNode={cg.startNode?.GetType().Name})");

        // Dialogue Graph
        var dg = AssetDatabase.LoadAssetAtPath<DialogueGraph>(PATH_INSPECTOR_DIALOGUE);
        if (dg == null) { Debug.LogError($"[Validate] ❌ {PATH_INSPECTOR_DIALOGUE} не найден"); errors++; }
        else Debug.Log($"[Validate] ✅ InspectorDialogue ({dg.allNodes.Count} нод)");

        // Scene points
        var scenePath = SceneManager.GetActiveScene().path;
        if (scenePath == GAME_SCENE_PATH)
        {
            bool foundStand = false, foundDoor = false;
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (go.name == "InspectorStandPoint") foundStand = true;
                if (go.name == "DoorSpawnPoint") foundDoor = true;
            }
            if (!foundStand) { Debug.LogError("[Validate] ❌ InspectorStandPoint не найден на GameScene"); errors++; }
            else Debug.Log("[Validate] ✅ InspectorStandPoint");
            if (!foundDoor) { Debug.LogError("[Validate] ❌ DoorSpawnPoint не найден на GameScene"); errors++; }
            else Debug.Log("[Validate] ✅ DoorSpawnPoint");

            // [ENDING_SYSTEM]
            bool foundSys = false;
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (go.name == "[ENDING_SYSTEM]")
                {
                    foundSys = true;
                    var em = go.GetComponent<EndingManager>();
                    var tm = go.GetComponent<TraitManager>();
                    Debug.Log($"[Validate] ✅ [ENDING_SYSTEM] (EndingManager={em != null}, TraitManager={tm != null})");
                    if (em != null)
                    {
                        var so = new SerializedObject(em);
                        var dbProp = so.FindProperty("endingDatabase");
                        Debug.Log($"[Validate]   EndingManager.endingDatabase = {(dbProp != null && dbProp.objectReferenceValue != null ? dbProp.objectReferenceValue.name : "NULL")}");
                    }
                }
            }
            if (!foundSys) { Debug.LogError("[Validate] ❌ [ENDING_SYSTEM] не найден"); errors++; }
        }
        else
        {
            Debug.LogWarning($"[Validate] ⚠ Активная сцена '{scenePath}' ≠ GameScene. Откройте GameScene для полной проверки.");
        }

        Debug.Log($"========== [EndingSystemBuilder] ВАЛИДАЦИЯ ЗАВЕРШЕНА: {errors} ошибок ==========");
    }

    private static void CheckPrefab(string path, string componentName, ref int errors)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) { Debug.LogError($"[Validate] ❌ Префаб не найден: {path}"); errors++; return; }
        var hasComp = prefab.GetComponent(componentName) != null;
        if (!hasComp) { Debug.LogError($"[Validate] ❌ Префаб {path} не содержит компонент {componentName}"); errors++; }
        else Debug.Log($"[Validate] ✅ {path} (содержит {componentName})");
    }

    [MenuItem("Tools/Bureau/Ending System/Clean Build (Delete All)")]
    public static void CleanAll()
    {
        if (!EditorUtility.DisplayDialog("Очистка Ending System",
            "Удалить ВСЕ ассеты Ending System (Database, префабы, графы)? " +
            "Точки на сцене и [ENDING_SYSTEM] GameObject будут сохранены.",
            "Да, удалить", "Отмена"))
        {
            return;
        }

        DeleteIfExists(PATH_ENDING_DB);
        DeleteIfExists(PATH_ENDING_BOOK_PREFAB);
        DeleteIfExists(PATH_DISMISSAL_PREFAB);
        DeleteIfExists(PATH_ARC_TITLE_PREFAB);
        DeleteIfExists(PATH_INSPECTOR_CINEMATIC);
        DeleteIfExists(PATH_INSPECTOR_DIALOGUE);
        DeleteIfExists(ARCHETYPE_INSPECTOR_MALE);
        DeleteIfExists(ARCHETYPE_INSPECTOR_FEMALE);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[EndingSystemBuilder] Очистка завершена.");
    }

    private static void DeleteIfExists(string path)
    {
        if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) != null)
        {
            AssetDatabase.DeleteAsset(path);
            Debug.Log($"[EndingSystemBuilder] Удалено: {path}");
        }
    }

    // ================== FOLDERS ==================
    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = Path.GetDirectoryName(path).Replace("\\", "/");
        var name = Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(parent))
        {
            Debug.LogError($"[EndingSystemBuilder] Родительская папка не существует: {parent}");
            return;
        }
        AssetDatabase.CreateFolder(parent, name);
    }

    // ================== 1. ENDING DATABASE ==================
    private static EndingDatabase BuildEndingDatabase()
    {
        EnsureFolder(RESOURCES_ENDING);

        var db = AssetDatabase.LoadAssetAtPath<EndingDatabase>(PATH_ENDING_DB);
        if (db == null)
        {
            db = ScriptableObject.CreateInstance<EndingDatabase>();
            AssetDatabase.CreateAsset(db, PATH_ENDING_DB);
            Debug.Log($"[EndingSystemBuilder] Создан: {PATH_ENDING_DB}");
        }
        else
        {
            Debug.Log($"[EndingSystemBuilder] EndingDatabase уже существует — обновляю записи.");
        }

        db.endings = new List<EndingEntry>
        {
            new EndingEntry
            {
                endingID = TraitManager.TRAIT_LAW,
                displayName = "Железная рука",
                description = "Вы наводите порядок железной рукой. Бюро стало образцом дисциплины и послушания.\n\nФинал карьеры: Закон превыше всего.",
                isDismissal = false
            },
            new EndingEntry
            {
                endingID = TraitManager.TRAIT_EMPATHY,
                displayName = "Душа бюро",
                description = "Вы — воплощение сострадания. Под вашим началом Бюро стало местом, где каждому посетителю помогают.\n\nФинал карьеры: Доброта побеждает.",
                isDismissal = false
            },
            new EndingEntry
            {
                endingID = TraitManager.TRAIT_MASK,
                displayName = "Безликий",
                description = "Никто не знает, кто вы на самом деле. Ваши решения всегда принимаются без следа.\n\nФинал карьеры: Тень Бюро.",
                isDismissal = false
            },
            new EndingEntry
            {
                endingID = TraitManager.TRAIT_AMBITION,
                displayName = "Карьерист",
                description = "Ваш главный мотив — власть. Вы использовали Бюро как трамплин для собственного продвижения.\n\nФинал карьеры: Вершина достигнута.",
                isDismissal = false
            },
            new EndingEntry
            {
                endingID = TraitManager.TRAIT_DISMISSAL,
                displayName = "Отстранение",
                description = "Внимание! Директор отстранён от должности за превышение допустимого количества ошибок.\n\nБюро закрыто на переаттестацию.",
                isDismissal = true
            }
        };

        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssetIfDirty(db);
        return db;
    }

    // ================== 2. INSPECTOR ARCHETYPES ==================
    private static void BuildInspectorArchetypes()
    {
        EnsureFolder("Assets/Data/Archetypes");

        var officialMale = AssetDatabase.LoadAssetAtPath<ClientArchetype>("Assets/Data/Archetypes/Archetype_Official_Male.asset");
        var officialFemale = AssetDatabase.LoadAssetAtPath<ClientArchetype>("Assets/Data/Archetypes/Archetype_Official_Female.asset");

        CreateInspectorArchetype(officialMale, ARCHETYPE_INSPECTOR_MALE, "Inspector_Male", 1);
        CreateInspectorArchetype(officialFemale, ARCHETYPE_INSPECTOR_FEMALE, "Inspector_Female", 0);

        // Добавляем в ArchetypeDatabase
        var dbPath = RESOURCES_DATABASES + "/ArchetypeDatabase.asset";
        var db = AssetDatabase.LoadAssetAtPath<ArchetypeDatabase>(dbPath);
        if (db == null)
        {
            Debug.LogWarning($"[EndingSystemBuilder] ArchetypeDatabase не найден по {dbPath}. Пропускаю регистрацию.");
            return;
        }
        if (db.allArchetypes == null) db.allArchetypes = new List<ClientArchetype>();

        var inspectorMale = AssetDatabase.LoadAssetAtPath<ClientArchetype>(ARCHETYPE_INSPECTOR_MALE);
        var inspectorFemale = AssetDatabase.LoadAssetAtPath<ClientArchetype>(ARCHETYPE_INSPECTOR_FEMALE);

        AddArchetypeToList(db.allArchetypes, inspectorMale, "Inspector_Male");
        AddArchetypeToList(db.allArchetypes, inspectorFemale, "Inspector_Female");

        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssetIfDirty(db);
        Debug.Log("[EndingSystemBuilder] Inspector архетипы добавлены в ArchetypeDatabase.");
    }

    private static void CreateInspectorArchetype(ClientArchetype source, string path, string archetypeID, int gender)
    {
        var existing = AssetDatabase.LoadAssetAtPath<ClientArchetype>(path);
        if (existing != null)
        {
            Debug.Log($"[EndingSystemBuilder] Архетип уже существует: {path}");
            return;
        }

        if (source == null)
        {
            Debug.LogWarning($"[EndingSystemBuilder] Source Official архетип не найден, создаю минимальный Inspector.");
            var newOne = ScriptableObject.CreateInstance<ClientArchetype>();
            newOne.groupID = "Inspector";
            newOne.archetypeID = archetypeID;
            newOne.displayName = "Инспектор";
            newOne.gender = gender;
            newOne.useGenderSpecificLines = false;
            newOne.patience = 999f;
            newOne.speedMultiplier = 1f;
            AssetDatabase.CreateAsset(newOne, path);
            EditorUtility.SetDirty(newOne);
            return;
        }

        // Клонируем через CopySerialized (глубокое копирование полей ScriptableObject).
        var clone = ScriptableObject.CreateInstance<ClientArchetype>();
        EditorUtility.CopySerialized(source, clone);
        clone.groupID = "Inspector";
        clone.archetypeID = archetypeID;
        clone.displayName = $"Инспектор ({(gender == 1 ? "М" : "Ж")})";
        clone.gender = gender;
        clone.patience = 999f;
        clone.satisfactionChance = 1f;
        clone.grumblingThreshold = 999f;
        clone.grumblingFrequency = 0f;
        AssetDatabase.CreateAsset(clone, path);
        EditorUtility.SetDirty(clone);
        Debug.Log($"[EndingSystemBuilder] Создан архетип: {path}");
    }

    private static void AddArchetypeToList(List<ClientArchetype> list, ClientArchetype archetype, string id)
    {
        if (archetype == null) return;
        foreach (var item in list)
        {
            if (item != null && item.archetypeID == id) return;
        }
        list.Add(archetype);
    }

    // ================== 3. ENDING BOOK PREFAB ==================
    private static void BuildEndingBookPrefab(EndingDatabase db)
    {
        EnsureFolder(RESOURCES_ENDING);

        GameObject root = new GameObject("EndingBook",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster),
            typeof(CanvasGroup),
            typeof(EndingBookUI));

        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        var canvasGroup = root.GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = true;

        // Фон
        var bg = CreateUIChild(root.transform, "Background");
        var bgImage = bg.AddComponent<Image>();
        bgImage.color = new Color(0.08f, 0.08f, 0.12f, 0.97f);
        StretchToParent(bg.GetComponent<RectTransform>());

        // Иллюстрация (выкл по умолчанию)
        var illustration = CreateUIChild(bg.transform, "Illustration");
        var illustrationRect = illustration.GetComponent<RectTransform>();
        illustrationRect.anchorMin = new Vector2(0.5f, 1f);
        illustrationRect.anchorMax = new Vector2(0.5f, 1f);
        illustrationRect.pivot = new Vector2(0.5f, 1f);
        illustrationRect.anchoredPosition = new Vector2(0f, -40f);
        illustrationRect.sizeDelta = new Vector2(400f, 400f);
        var illustrationImage = illustration.AddComponent<Image>();
        illustrationImage.preserveAspect = true;
        illustrationImage.enabled = false;

        // Заголовок
        var titleGO = CreateUIChild(bg.transform, "Title");
        var titleRect = titleGO.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -30f);
        titleRect.sizeDelta = new Vector2(-80f, 80f);
        var titleText = titleGO.AddComponent<TextMeshProUGUI>();
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.fontSize = 48f;
        titleText.text = "ИТОГИ ВАШЕЙ КАРЬЕРЫ";
        titleText.color = new Color(0.95f, 0.85f, 0.6f);

        // Тело
        var bodyGO = CreateUIChild(bg.transform, "Body");
        var bodyRect = bodyGO.GetComponent<RectTransform>();
        bodyRect.anchorMin = new Vector2(0f, 0f);
        bodyRect.anchorMax = new Vector2(1f, 1f);
        bodyRect.offsetMin = new Vector2(80f, 200f);
        bodyRect.offsetMax = new Vector2(-80f, -130f);
        var bodyText = bodyGO.AddComponent<TextMeshProUGUI>();
        bodyText.alignment = TextAlignmentOptions.Top;
        bodyText.fontSize = 28f;
        bodyText.text = "";
        bodyText.color = Color.white;

        // Кнопка выхода
        var exitGO = CreateUIChild(bg.transform, "ExitButton");
        var exitRect = exitGO.GetComponent<RectTransform>();
        exitRect.anchorMin = new Vector2(0.5f, 0f);
        exitRect.anchorMax = new Vector2(0.5f, 0f);
        exitRect.pivot = new Vector2(0.5f, 0f);
        exitRect.anchoredPosition = new Vector2(0f, 50f);
        exitRect.sizeDelta = new Vector2(360f, 80f);
        var exitImage = exitGO.AddComponent<Image>();
        exitImage.color = new Color(0.25f, 0.5f, 0.25f);
        var exitButton = exitGO.AddComponent<Button>();
        exitButton.targetGraphic = exitImage;
        var exitLabelGO = CreateUIChild(exitGO.transform, "Label");
        var exitLabelRect = exitLabelGO.GetComponent<RectTransform>();
        StretchToParent(exitLabelRect);
        var exitLabel = exitLabelGO.AddComponent<TextMeshProUGUI>();
        exitLabel.alignment = TextAlignmentOptions.Center;
        exitLabel.fontSize = 32f;
        exitLabel.text = "Выйти в меню";
        exitLabel.color = Color.white;
        exitGO.SetActive(false);

        // Привязка к скрипту
        var ui = root.GetComponent<EndingBookUI>();
        ui.canvasGroup = canvasGroup;
        ui.backgroundImage = bgImage;
        ui.illustrationImage = illustrationImage;
        ui.pageTitleText = titleText;
        ui.pageBodyText = bodyText;
        ui.exitButton = exitButton;
        ui.exitButtonText = exitLabel;

        root.SetActive(false);

        SaveAsPrefab(root, PATH_ENDING_BOOK_PREFAB);
        Debug.Log($"[EndingSystemBuilder] Создан префаб: {PATH_ENDING_BOOK_PREFAB}");
    }

    // ================== 4. DISMISSAL SCREEN PREFAB ==================
    private static void BuildDismissalScreenPrefab(EndingDatabase db)
    {
        EnsureFolder(RESOURCES_ENDING);

        GameObject root = new GameObject("DismissalScreen",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster),
            typeof(CanvasGroup),
            typeof(DismissalScreenUI));

        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        var canvasGroup = root.GetComponent<CanvasGroup>();
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = true;

        var bg = CreateUIChild(root.transform, "Background");
        var bgImage = bg.AddComponent<Image>();
        bgImage.color = new Color(0.15f, 0.05f, 0.05f, 0.97f);
        StretchToParent(bg.GetComponent<RectTransform>());

        var titleGO = CreateUIChild(bg.transform, "Title");
        var titleRect = titleGO.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -80f);
        titleRect.sizeDelta = new Vector2(-120f, 100f);
        var titleText = titleGO.AddComponent<TextMeshProUGUI>();
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.fontSize = 56f;
        titleText.text = "ВНИМАНИЕ";
        titleText.color = new Color(1f, 0.4f, 0.4f);

        var messageGO = CreateUIChild(bg.transform, "Message");
        var messageRect = messageGO.GetComponent<RectTransform>();
        messageRect.anchorMin = new Vector2(0f, 0f);
        messageRect.anchorMax = new Vector2(1f, 1f);
        messageRect.offsetMin = new Vector2(120f, 220f);
        messageRect.offsetMax = new Vector2(-120f, -220f);
        var messageText = messageGO.AddComponent<TextMeshProUGUI>();
        messageText.alignment = TextAlignmentOptions.Center;
        messageText.fontSize = 32f;
        messageText.text = "Директор отстранён от должности за превышение допустимого количества ошибок.\n\nБюро закрыто на переаттестацию.";
        messageText.color = Color.white;

        var reloadBtn = CreateButton(bg.transform, "ReloadButton", "Загрузить", new Vector2(-220f, 50f));
        var menuBtn = CreateButton(bg.transform, "MenuButton", "Главное меню", new Vector2(220f, 50f));

        var ui = root.GetComponent<DismissalScreenUI>();
        ui.canvasGroup = canvasGroup;
        ui.backgroundImage = bgImage;
        ui.messageText = messageText;
        ui.reloadButton = reloadBtn.button;
        ui.mainMenuButton = menuBtn.button;
        ui.reloadButtonText = reloadBtn.label;
        ui.mainMenuButtonText = menuBtn.label;

        root.SetActive(false);

        SaveAsPrefab(root, PATH_DISMISSAL_PREFAB);
        Debug.Log($"[EndingSystemBuilder] Создан префаб: {PATH_DISMISSAL_PREFAB}");
    }

    // ================== 5. ARC TITLE PREFAB ==================
    private static void BuildArcTitlePrefab()
    {
        EnsureFolder(RESOURCES_ENDING);

        GameObject root = new GameObject("ArcTitle",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster),
            typeof(CanvasGroup),
            typeof(ArcTitleDisplay));

        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999;

        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        var canvasGroup = root.GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        var bg = CreateUIChild(root.transform, "Background");
        var bgImage = bg.AddComponent<Image>();
        bgImage.color = new Color(0f, 0f, 0f, 0.4f);
        var bgRect = bg.GetComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0f, 0.4f);
        bgRect.anchorMax = new Vector2(1f, 0.6f);
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        var titleGO = CreateUIChild(bg.transform, "Title");
        var titleRect = titleGO.GetComponent<RectTransform>();
        StretchToParent(titleRect);
        var titleText = titleGO.AddComponent<TextMeshProUGUI>();
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.fontSize = 64f;
        titleText.text = "Название арки";
        titleText.color = new Color(0.95f, 0.85f, 0.6f);
        titleText.fontStyle = FontStyles.Bold;

        var ui = root.GetComponent<ArcTitleDisplay>();
        ui.canvasGroup = canvasGroup;
        ui.titleText = titleText;

        root.SetActive(false);

        SaveAsPrefab(root, PATH_ARC_TITLE_PREFAB);
        Debug.Log($"[EndingSystemBuilder] Создан префаб: {PATH_ARC_TITLE_PREFAB}");
    }

    // ================== 6. INSPECTOR CINEMATIC GRAPH ==================
    private static void BuildInspectorCinematicGraph(DialogueGraph inspectorDialogue)
    {
        EnsureFolder(RESOURCES_CINEMATIC);

        var graph = AssetDatabase.LoadAssetAtPath<CinematicGraph>(PATH_INSPECTOR_CINEMATIC);
        if (graph != null)
        {
            Debug.Log($"[EndingSystemBuilder] CinematicGraph уже существует: {PATH_INSPECTOR_CINEMATIC}");
            return;
        }

        graph = ScriptableObject.CreateInstance<CinematicGraph>();
        graph.graphID = "InspectorVisit";
        graph.graphName = "Визит Инспектора (день 30)";
        graph.isBackground = false;

        AssetDatabase.CreateAsset(graph, PATH_INSPECTOR_CINEMATIC);

        // Узлы: Start → Move Director → Spawn Inspector → Move Inspector → Call Dialogue → End
        var startNode = graph.CreateNode<StartNode>();
        startNode.characterID = "Director";
        startNode.nextNode = null; // заполним после создания Move

        var moveDirector = graph.CreateNode<MoveToNode>();
        moveDirector.characterID = "Director";
        moveDirector.targetKey = "DirectorChairPoint";
        moveDirector.speed = -1f;
        moveDirector.waitForCompletion = true;
        moveDirector.usePathfinding = true;

        var spawnInspector = graph.CreateNode<SpawnCharacterNode>();
        spawnInspector.archetypeID = "Inspector_Male";
        spawnInspector.spawnPointKey = "DoorSpawnPoint";
        spawnInspector.targetKeyForReference = "Inspector";
        spawnInspector.forcedGoal = ClientGoal.AskAndLeave;

        var moveInspector = graph.CreateNode<MoveToNode>();
        moveInspector.characterID = "Inspector";
        moveInspector.targetKey = "InspectorStandPoint";
        moveInspector.speed = -1f;
        moveInspector.waitForCompletion = true;
        moveInspector.usePathfinding = true;

        var callDialogue = graph.CreateNode<CallDialogueNode>();
        callDialogue.dialogueGraph = inspectorDialogue;

        var endNode = graph.CreateNode<EndNode>();

        // Связи
        startNode.nextNode = moveDirector;
        moveDirector.nextNode = spawnInspector;
        spawnInspector.nextNode = moveInspector;
        moveInspector.nextNode = callDialogue;
        callDialogue.nextNode = endNode;

        graph.startNode = startNode;

        graph.RebuildLinksFromNodes();
        EditorUtility.SetDirty(graph);
        AssetDatabase.SaveAssetIfDirty(graph);
        Debug.Log($"[EndingSystemBuilder] Создан CinematicGraph: {PATH_INSPECTOR_CINEMATIC}");
    }

    // ================== 7. INSPECTOR DIALOGUE GRAPH ==================
    private static DialogueGraph BuildInspectorDialogueGraph()
    {
        EnsureFolder(RESOURCES_DIALOGUES);

        var graph = AssetDatabase.LoadAssetAtPath<DialogueGraph>(PATH_INSPECTOR_DIALOGUE);
        if (graph != null)
        {
            Debug.Log($"[EndingSystemBuilder] DialogueGraph уже существует: {PATH_INSPECTOR_DIALOGUE}");
            return graph;
        }

        graph = ScriptableObject.CreateInstance<DialogueGraph>();
        AssetDatabase.CreateAsset(graph, PATH_INSPECTOR_DIALOGUE);

        // Узлы диалога (DialogueSystem.Data.* — избегаем конфликта с CinematicSystem.Nodes.StartNode)
        var start = ScriptableObject.CreateInstance<DialogueSystem.Data.StartNode>();
        start.dialogueType = DialogueType.World;
        start.name = "Inspector_Start";
        AddDialogueSubAsset(graph, start);

        var intro = ScriptableObject.CreateInstance<PhraseNode>();
        intro.speakerID = "Inspector";
        intro.text = "Здравствуйте. Я пришёл подвести итоги вашей работы в Бюро.";
        intro.name = "Inspector_Intro";
        AddDialogueSubAsset(graph, intro);

        var purpose = ScriptableObject.CreateInstance<PhraseNode>();
        purpose.speakerID = "Inspector";
        purpose.text = "Что ж, посмотрим, какой след вы оставили.";
        purpose.name = "Inspector_Purpose";
        AddDialogueSubAsset(graph, purpose);

        // Вопрос при ничьей (ChoiceNode → EventNode(AddTraitPoint) → PhraseNode → EndNode)
        var tieQuestion = ScriptableObject.CreateInstance<ChoiceNode>();
        tieQuestion.queryText = "Какой путь был для вас важнее?";
        tieQuestion.options = new List<ChoiceNode.ChoiceOption>
        {
            MakeChoiceOption("Закон и порядок", TraitManager.TRAIT_LAW),
            MakeChoiceOption("Сострадание к людям", TraitManager.TRAIT_EMPATHY),
            MakeChoiceOption("Холодный расчёт", TraitManager.TRAIT_MASK),
            MakeChoiceOption("Личные амбиции", TraitManager.TRAIT_AMBITION)
        };
        tieQuestion.name = "Inspector_TieChoice";
        AddDialogueSubAsset(graph, tieQuestion);

        var phraseConclusion = ScriptableObject.CreateInstance<PhraseNode>();
        phraseConclusion.speakerID = "Inspector";
        phraseConclusion.text = "Так я и думал… Что ж, ваша карьера в Бюро подошла к концу.";
        phraseConclusion.name = "Inspector_Conclusion";
        AddDialogueSubAsset(graph, phraseConclusion);

        var end = ScriptableObject.CreateInstance<EndNode>();
        end.outcome = DialogueSystem.Data.EndNode.DialogueOutcome.LeaveUpset;
        end.stressModifier = 0f;
        end.name = "Inspector_End";
        AddDialogueSubAsset(graph, end);

        // Связи
        start.nextNode = intro;
        intro.nextNode = purpose;
        purpose.nextNode = tieQuestion;
        // Каждый вариант выбора → EventNode(AddTraitPoint) → phraseConclusion → End
        for (int i = 0; i < tieQuestion.options.Count; i++)
        {
            var option = tieQuestion.options[i];
            var eventNode = ScriptableObject.CreateInstance<EventNode>();
            eventNode.eventType = DialogueSystem.Data.EventNode.EventType.AddTraitPoint;
            eventNode.flagKey = TraitKeysByName(option.text);
            eventNode.intValue = 1;
            eventNode.name = $"Inspector_AddTrait_{i}";
            AddDialogueSubAsset(graph, eventNode);

            var afterEvent = ScriptableObject.CreateInstance<PhraseNode>();
            afterEvent.speakerID = "Inspector";
            afterEvent.text = GetTraitReaction(option.text);
            afterEvent.name = $"Inspector_Reaction_{i}";
            AddDialogueSubAsset(graph, afterEvent);

            eventNode.nextNode = afterEvent;
            afterEvent.nextNode = phraseConclusion;
            option.nextNode = eventNode;
        }
        phraseConclusion.nextNode = end;

        graph.startNode = start;
        EditorUtility.SetDirty(graph);
        DialogueGraph.FlushPendingSubassets();
        AssetDatabase.SaveAssetIfDirty(graph);
        Debug.Log($"[EndingSystemBuilder] Создан DialogueGraph: {PATH_INSPECTOR_DIALOGUE}");
        return graph;
    }

    private static string TraitKeysByName(string optionText)
    {
        if (optionText.Contains("Закон")) return TraitManager.TRAIT_LAW;
        if (optionText.Contains("Сострадание")) return TraitManager.TRAIT_EMPATHY;
        if (optionText.Contains("расчёт")) return TraitManager.TRAIT_MASK;
        if (optionText.Contains("амбиции")) return TraitManager.TRAIT_AMBITION;
        return TraitManager.TRAIT_LAW;
    }

    private static string GetTraitReaction(string optionText)
    {
        if (optionText.Contains("Закон")) return "Закон превыше всего. Запомню.";
        if (optionText.Contains("Сострадание")) return "Сострадание — редкий дар.";
        if (optionText.Contains("расчёт")) return "Холодный расчёт. Понимаю.";
        if (optionText.Contains("амбиции")) return "Амбиции — двигатель прогресса.";
        return "Принято.";
    }

    private static ChoiceNode.ChoiceOption MakeChoiceOption(string text, string _)
    {
        return new ChoiceNode.ChoiceOption
        {
            text = text,
            nextNode = null
        };
    }

    private static void AddDialogueSubAsset(DialogueGraph graph, DialogueNode node)
    {
        graph.allNodes.Add(node);
        AssetDatabase.AddObjectToAsset(node, graph);
        EditorUtility.SetDirty(node);
    }

    // ================== 8. SCENE POINTS ==================
    private static void BuildScenePoints()
    {
        // Открываем GameScene аддитивно, чтобы не потерять текущую сцену.
        var currentScenePath = SceneManager.GetActiveScene().path;

        Scene gameScene;
        if (currentScenePath == GAME_SCENE_PATH)
        {
            gameScene = SceneManager.GetActiveScene();
        }
        else
        {
            gameScene = EditorSceneManager.OpenScene(GAME_SCENE_PATH, OpenSceneMode.Single);
        }

        // Найти существующие точки
        GameObject inspectorStand = null;
        GameObject doorSpawn = null;
        foreach (var go in gameScene.GetRootGameObjects())
        {
            if (go.name == "InspectorStandPoint") inspectorStand = go;
            if (go.name == "DoorSpawnPoint") doorSpawn = go;
        }

        if (inspectorStand == null)
        {
            inspectorStand = new GameObject("InspectorStandPoint");
            inspectorStand.transform.position = new Vector3(-4f, -12.6f, 0f);
            SceneManager.MoveGameObjectToScene(inspectorStand, gameScene);
            Debug.Log("[EndingSystemBuilder] Создан InspectorStandPoint.");
        }

        if (doorSpawn == null)
        {
            doorSpawn = new GameObject("DoorSpawnPoint");
            // Ставим у входа в кабинет Директора (грубая оценка)
            doorSpawn.transform.position = new Vector3(8f, -10f, 0f);
            SceneManager.MoveGameObjectToScene(doorSpawn, gameScene);
            Debug.Log("[EndingSystemBuilder] Создан DoorSpawnPoint.");
        }

        // Регистрируем в SceneObjectRegistry
        var registry = Object.FindFirstObjectByType<SceneObjectRegistry>();
        if (registry == null)
        {
            Debug.LogWarning("[EndingSystemBuilder] SceneObjectRegistry не найден на сцене.");
        }
        else
        {
            registry.Register("InspectorStandPoint", inspectorStand);
            registry.Register("DoorSpawnPoint", doorSpawn);
            EditorUtility.SetDirty(registry);
            Debug.Log("[EndingSystemBuilder] Точки зарегистрированы в SceneObjectRegistry.");
        }

        // Сохраняем сцену
        EditorSceneManager.MarkSceneDirty(gameScene);
        EditorSceneManager.SaveScene(gameScene);
    }

    // ================== 9. ENDING_SYSTEM GAMEOBJECT ==================
    private static void BuildEndingSystemGameObject(EndingDatabase db, DialogueGraph inspectorDialogue)
    {
        var currentScenePath = SceneManager.GetActiveScene().path;
        Scene gameScene;
        if (currentScenePath == GAME_SCENE_PATH)
        {
            gameScene = SceneManager.GetActiveScene();
        }
        else
        {
            gameScene = EditorSceneManager.OpenScene(GAME_SCENE_PATH, OpenSceneMode.Single);
        }

        GameObject root = null;
        foreach (var go in gameScene.GetRootGameObjects())
        {
            if (go.name == "[ENDING_SYSTEM]") { root = go; break; }
        }

        if (root == null)
        {
            root = new GameObject("[ENDING_SYSTEM]");
            SceneManager.MoveGameObjectToScene(root, gameScene);
        }

        // На [ENDING_SYSTEM] размещаем только менеджеры.
        // UI-классы (EndingBookUI/DismissalScreenUI/ArcTitleDisplay) живут в префабах
        // и инстанцируются из Resources по требованию через GetOrCreate().
        var traitManager = root.GetComponent<TraitManager>() ?? root.AddComponent<TraitManager>();
        var endingManager = root.GetComponent<EndingManager>() ?? root.AddComponent<EndingManager>();

        // Привязка приватных полей endingDatabase / inspectorDialogue через SerializedObject
        var so = new SerializedObject(endingManager);
        var dbProp = so.FindProperty("endingDatabase");
        if (dbProp != null) dbProp.objectReferenceValue = db;
        var graphProp = so.FindProperty("inspectorDialogue");
        if (graphProp != null) graphProp.objectReferenceValue = inspectorDialogue;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(root);
        EditorSceneManager.MarkSceneDirty(gameScene);
        EditorSceneManager.SaveScene(gameScene);
        Debug.Log("[EndingSystemBuilder] GameObject [ENDING_SYSTEM] создан/обновлён с TraitManager и EndingManager.");
    }

    // ================== UI HELPERS ==================
    private static GameObject CreateUIChild(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void StretchToParent(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private struct ButtonRefs { public Button button; public TextMeshProUGUI label; }

    private static ButtonRefs CreateButton(Transform parent, string name, string label, Vector2 anchoredPos)
    {
        var go = CreateUIChild(parent, name);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(360f, 80f);
        var image = go.AddComponent<Image>();
        image.color = new Color(0.25f, 0.4f, 0.55f);
        var button = go.AddComponent<Button>();
        button.targetGraphic = image;

        var labelGO = CreateUIChild(go.transform, "Label");
        var labelRect = labelGO.GetComponent<RectTransform>();
        StretchToParent(labelRect);
        var labelText = labelGO.AddComponent<TextMeshProUGUI>();
        labelText.alignment = TextAlignmentOptions.Center;
        labelText.fontSize = 30f;
        labelText.text = label;
        labelText.color = Color.white;

        return new ButtonRefs { button = button, label = labelText };
    }

    private static void SaveAsPrefab(GameObject root, string path)
    {
        // Удаляем старый префаб, если есть
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null)
        {
            AssetDatabase.DeleteAsset(path);
        }

        // Сбрасываем Instance UI-классов, чтобы Awake на временном GO не оставлял
        // ссылку на уничтожаемый объект.
        var bookUi = root.GetComponent<EndingBookUI>();
        if (bookUi != null) EndingBookUI.ResetStaticInstanceForBuild();
        var dismissal = root.GetComponent<DismissalScreenUI>();
        if (dismissal != null) DismissalScreenUI.ResetStaticInstanceForBuild();
        var title = root.GetComponent<ArcTitleDisplay>();
        if (title != null) ArcTitleDisplay.ResetStaticInstanceForBuild();

        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        AssetDatabase.ImportAsset(path);
    }
}
#endif
