using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using Data.Calendar;
using Managers;
using Utilities;
using Scriptables.Audio;
using Gameplay;

public class GameBalanceControlPanel : EditorWindow
{
    private enum BalanceTab
    {
        Roles,          // RoleData
        Actions,        // StaffAction
        XP_Progression, // ActionXPData, Ranks
        Clients,        // Client Prefab & Calendar
        WorldPhysics,   // Puddle, Dirt, Mess
        VisualsCam,     // Transition, Camera, Light
        SpawnTime,      // WaveManager, TimeManager
        UI_Tutorial,    // UI Delays, Tutorial Config
        Global_Audio,   // Mandates, AudioLib, Global Settings
        AI_Balance      // AIBalanceConfig
    }

    private BalanceTab currentTab = BalanceTab.Roles;
    private Vector2 scrollPosition;
    private GUIStyle headerStyle;

    // --- Ассеты (находятся всегда) ---
    private List<RoleData> allRoles = new List<RoleData>();
    private List<StaffAction> allActions = new List<StaffAction>();
    private List<CalendarDay> allCalendars = new List<CalendarDay>();
    private List<RankData> allRanks = new List<RankData>();
    private List<DailyMandates> allMandates = new List<DailyMandates>();
    private SoundLibrary soundLibrary;
    private ActionXPData xpData;

    // --- Объекты (Префабы или Сцена) ---
    private GameObject clientPrefab;
    private GameObject puddlePrefab;
    
    private WaveManager waveManager;
    private TransitionManager transitionManager;
    private LightingManager lightingManager;
    private CameraToggle cameraToggle;
    private MainUIManager mainUiManager;
    private DirtGridManager dirtManager;
    private MessManager messManager;
    private TutorialScreenConfig tutorialConfig;
    private MusicPlayer musicPlayer;
    private NotificationStyleManager notificationStyleManager;
    
    // AI Balance
    private AIBalanceConfig aiConfig;

    [MenuItem("Tools/Ultimate Balance Control Panel")]
    public static void ShowWindow()
    {
        GetWindow<GameBalanceControlPanel>("Ultimate Balance");
    }

    private void OnEnable()
    {
        RefreshData();
        LoadSavedReferences(); 
    }

    private void OnDisable()
    {
        SaveManualReferences(); // Теперь этот метод существует!
    }

    private void OnGUI()
    {
        if (headerStyle == null)
        {
            headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14, alignment = TextAnchor.MiddleCenter, margin = new RectOffset(0,0,10,10) };
        }

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("ULTIMATE BALANCE CONTROL v2.1", headerStyle);
        
        DrawTopControls();

        EditorGUILayout.Space(5);
        
        int columns = 3; 
        string[] tabs = new string[] {
            "Roles (Staff)", "Actions", "XP & Progress",
            "Clients", "World & Physics", "Visuals & Cam",
            "Spawn & Time", "UI & Tutorial", "Global & Audio", "AI Balance"
        };
        
        currentTab = (BalanceTab)GUILayout.SelectionGrid((int)currentTab, tabs, columns);

        EditorGUILayout.Space(10);
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        switch (currentTab)
        {
            case BalanceTab.Roles: DrawRolesTab(); break;
            case BalanceTab.Actions: DrawActionsTab(); break;
            case BalanceTab.XP_Progression: DrawXPTab(); break;
            case BalanceTab.Clients: DrawClientsTab(); break;
            case BalanceTab.WorldPhysics: DrawWorldPhysicsTab(); break;
            case BalanceTab.VisualsCam: DrawVisualsCamTab(); break;
            case BalanceTab.SpawnTime: DrawSpawnTimeTab(); break;
            case BalanceTab.UI_Tutorial: DrawUITutorialTab(); break;
            case BalanceTab.Global_Audio: DrawGlobalAudioTab(); break;
            case BalanceTab.AI_Balance: DrawAIBalanceTab(); break;
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawTopControls()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        if (GUILayout.Button("Сканировать проект (Refresh)", GUILayout.Height(30))) 
        {
            RefreshData();
        }
        GUI.backgroundColor = new Color(0.7f, 1f, 0.7f);
        if (GUILayout.Button("СОХРАНИТЬ ВСЁ НА ДИСК", GUILayout.Height(30))) SaveAllData();
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();
    }

    // ==========================================================================================
    // ТАБ: ROLES
    // ==========================================================================================
    private void DrawRolesTab()
    {
        EditorGUILayout.HelpBox("RoleData: Зарплаты, Скорость, Стресс", MessageType.Info);
        foreach (var role in allRoles)
        {
            DrawSerializedObject(role, role.name, (so) => 
            {
                EditorGUILayout.PropertyField(so.FindProperty("moveSpeed"));
				EditorGUILayout.PropertyField(so.FindProperty("animationSpeed"));
                EditorGUILayout.PropertyField(so.FindProperty("baseHiringCost"));
                EditorGUILayout.PropertyField(so.FindProperty("priority"));
                
                EditorGUILayout.LabelField("Специфичные настройки", EditorStyles.boldLabel);
                DrawSpecificProperties(so, "guard_");
                DrawSpecificProperties(so, "clerk_");
                DrawSpecificProperties(so, "worker_");
                DrawSpecificProperties(so, "cashier_");
                DrawSpecificProperties(so, "minIdleWait");
                DrawSpecificProperties(so, "maxIdleWait");
            });
        }
    }

    // ==========================================================================================
    // ТАБ: ACTIONS
    // ==========================================================================================
    private void DrawActionsTab()
    {
        EditorGUILayout.HelpBox("StaffAction: Шансы, Время выполнения, Перезарядка", MessageType.Info);
        var tactical = allActions.Where(a => a.category == ActionCategory.Tactic).ToList();
        var system = allActions.Where(a => a.category == ActionCategory.System).ToList();

        DrawActionList("Тактические (Игрок)", tactical);
        DrawActionList("Системные (Потребности/ИИ)", system);
    }

    private void DrawActionList(string label, List<StaffAction> list)
    {
        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
        foreach (var action in list)
        {
            DrawSerializedObject(action, action.displayName, (so) =>
            {
                EditorGUILayout.PropertyField(so.FindProperty("baseSuccessChance"));
                EditorGUILayout.PropertyField(so.FindProperty("minSuccessChance"));
                EditorGUILayout.PropertyField(so.FindProperty("maxSuccessChance"));
                EditorGUILayout.PropertyField(so.FindProperty("priority"));
                EditorGUILayout.PropertyField(so.FindProperty("actionDuration"));
                EditorGUILayout.PropertyField(so.FindProperty("actionCooldown"));
                EditorGUILayout.PropertyField(so.FindProperty("minRankRequired"));
            });
        }
    }

    // ==========================================================================================
    // ТАБ: XP & PROGRESSION
    // ==========================================================================================
    private void DrawXPTab()
    {
        EditorGUILayout.HelpBox("Настройка получения опыта и рангов.", MessageType.Info);

        DrawSerializedObject(xpData, "Action XP Database", (so) => {
            EditorGUILayout.PropertyField(so.FindProperty("xpEntries"), true);
        });

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Ранги (RankData)", EditorStyles.boldLabel);
        
        var sortedRanks = allRanks.OrderBy(r => r.rankLevel).ThenBy(r => r.associatedRole).ToList();

        foreach (var rank in sortedRanks)
        {
            DrawSerializedObject(rank, $"{rank.associatedRole} - {rank.rankName} (Lvl {rank.rankLevel})", (so) => {
                EditorGUILayout.PropertyField(so.FindProperty("promotionCost"));
                EditorGUILayout.PropertyField(so.FindProperty("salaryMultiplier"));
                EditorGUILayout.PropertyField(so.FindProperty("experienceRequired"));
                EditorGUILayout.PropertyField(so.FindProperty("workPeriodsCount"));
                EditorGUILayout.PropertyField(so.FindProperty("unlockedActions"), true);
            });
        }
    }

    // ==========================================================================================
    // ТАБ: CLIENTS
    // ==========================================================================================
    private void DrawClientsTab()
    {
        DrawObjectField("Client Prefab", ref clientPrefab);

        if (clientPrefab != null)
        {
            var cp = clientPrefab.GetComponent<ClientPathfinding>();
            DrawComponentSettings(cp, "Настройки Клиента (Patience & Traits)", (so) => {
                EditorGUILayout.PropertyField(so.FindProperty("minPatienceTime"));
                EditorGUILayout.PropertyField(so.FindProperty("maxPatienceTime"));
				EditorGUILayout.PropertyField(so.FindProperty("animationSpeed"));
                EditorGUILayout.PropertyField(so.FindProperty("babushkaFactor"));
                EditorGUILayout.PropertyField(so.FindProperty("suetunFactor"));
                EditorGUILayout.PropertyField(so.FindProperty("prolazaFactor"));
                EditorGUILayout.PropertyField(so.FindProperty("billToPay"));
                EditorGUILayout.PropertyField(so.FindProperty("documentQuality"));
            });
            
            var cmg = clientPrefab.GetComponent("ClientMessGenerator") as Component;
            DrawComponentSettings(cmg, "Генерация Мусора (ClientMessGenerator)", (so) => {
                EditorGUILayout.PropertyField(so.FindProperty("baseTrashChancePerSecond"));
                EditorGUILayout.PropertyField(so.FindProperty("puddleChanceOnUpset"));
            });
        }
    }

    // ==========================================================================================
    // ТАБ: WORLD & PHYSICS
    // ==========================================================================================
    private void DrawWorldPhysicsTab()
    {
        DrawObjectField("Puddle Prefab", ref puddlePrefab);
        if (puddlePrefab != null)
        {
            var puddle = puddlePrefab.GetComponent<Puddle>();
            DrawComponentSettings(puddle, "Настройки Лужи (Prefab)", (so) => {
                EditorGUILayout.PropertyField(so.FindProperty("slipChance"));
            });
        }

        DrawSmartManagerSettings(ref dirtManager, "Сетка Грязи (DirtGridManager)", "DirtGridManager", (so) => {
            EditorGUILayout.PropertyField(so.FindProperty("cellSize"));
            EditorGUILayout.PropertyField(so.FindProperty("trafficThresholds"), true);
        });

        DrawSmartManagerSettings(ref messManager, "Лимиты Мусора (MessManager)", "MessManager", (so) => {
            EditorGUILayout.PropertyField(so.FindProperty("maxTotalMesses"));
        });
    }

    // ==========================================================================================
    // ТАБ: VISUALS & CAM
    // ==========================================================================================
    private void DrawVisualsCamTab()
    {
        DrawSmartManagerSettings(ref transitionManager, "Переходы (TransitionManager)", "TransitionManager", (so) => {
            EditorGUILayout.PropertyField(so.FindProperty("fadeToBlackDuration"));
            EditorGUILayout.PropertyField(so.FindProperty("blackScreenHoldDuration"));
            EditorGUILayout.PropertyField(so.FindProperty("fadeToVisibleDuration"));
            EditorGUILayout.PropertyField(so.FindProperty("leafSpeed"));
            EditorGUILayout.PropertyField(so.FindProperty("numberOfLeaves"));
        });

        DrawSmartManagerSettings(ref cameraToggle, "Камера (CameraToggle)", "CameraManager", (so) => {
            EditorGUILayout.PropertyField(so.FindProperty("moveSpeed"));
        });

        DrawSmartManagerSettings(ref lightingManager, "Свет (LightingManager)", "LightingManager", (so) => {
            EditorGUILayout.PropertyField(so.FindProperty("lightFadeDuration"));
        });
        
        DrawSmartManagerSettings(ref notificationStyleManager, "Стиль Уведомлений", "NotificationStyleManager", (so) => {
            EditorGUILayout.PropertyField(so.FindProperty("inspectorUseEmojiStyle"), new GUIContent("Use Emojis"));
        });
    }

    // ==========================================================================================
    // ТАБ: SPAWN & TIME
    // ==========================================================================================
    private void DrawSpawnTimeTab()
    {
        DrawSmartManagerSettings(ref waveManager, "Настройки Волн (WaveManager)", "WaveManager", (so) => {
            EditorGUILayout.PropertyField(so.FindProperty("maxClientsOnScene"));
            EditorGUILayout.PropertyField(so.FindProperty("initialSpawnDelay"));
        });

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Календарь (CalendarDay Assets)", EditorStyles.boldLabel);
        foreach (var calendar in allCalendars)
        {
            DrawSerializedObject(calendar, calendar.name, (so) => {
                EditorGUILayout.PropertyField(so.FindProperty("periodSettings"), true);
            });
        }
    }

    // ==========================================================================================
    // ТАБ: UI & TUTORIAL
    // ==========================================================================================
    private void DrawUITutorialTab()
    {
        DrawSmartManagerSettings(ref tutorialConfig, "Тайминги Туториала (Config)", "TutorialScreenConfig", (so) => {
            EditorGUILayout.PropertyField(so.FindProperty("sceneLoadDelay"));
            EditorGUILayout.PropertyField(so.FindProperty("firstEverAppearanceDelay"));
            EditorGUILayout.PropertyField(so.FindProperty("initialHintDelay"));
            EditorGUILayout.PropertyField(so.FindProperty("nextHintDelay"));
            EditorGUILayout.PropertyField(so.FindProperty("idleMessageChangeDelay"));
        });

        DrawSmartManagerSettings(ref mainUiManager, "Main UI Settings", "MainUIManager", (so) => {
            EditorGUILayout.PropertyField(so.FindProperty("splashScreenDwellTime"));
        });
    }

    // ==========================================================================================
    // ТАБ: GLOBAL & AUDIO
    // ==========================================================================================
    private void DrawGlobalAudioTab()
    {
        EditorGUILayout.HelpBox("Нормы дня и Аудио", MessageType.Info);

        foreach (var mandate in allMandates)
        {
            DrawSerializedObject(mandate, mandate.name, (so) => {
                EditorGUILayout.PropertyField(so.FindProperty("allowedDirectorErrorRate"));
                EditorGUILayout.PropertyField(so.FindProperty("maxArchiveDocumentCount"));
                EditorGUILayout.PropertyField(so.FindProperty("minProcessedClients"));
                EditorGUILayout.PropertyField(so.FindProperty("maxUpsetClients"));
            });
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Аудио", EditorStyles.boldLabel);
        
        DrawSmartManagerSettings(ref musicPlayer, "Музыкальный Плеер", "MusicPlayer", (so) => {
            EditorGUILayout.PropertyField(so.FindProperty("muffledVolume"));
        });

        DrawSerializedObject(soundLibrary, "Библиотека Звуков (SoundLibrary)", (so) => {
            EditorGUILayout.PropertyField(so.FindProperty("sounds"), true);
        });
    }

    // ------------------------------------------------------------------------------------------
    // ЛОГИКА ПОИСКА И СОХРАНЕНИЯ ССЫЛОК
    // ------------------------------------------------------------------------------------------

    private void RefreshData()
    {
        allRoles = FindAssetsByType<RoleData>();
        allActions = FindAssetsByType<StaffAction>();
        allCalendars = FindAssetsByType<CalendarDay>();
        allRanks = FindAssetsByType<RankData>();
        allMandates = FindAssetsByType<DailyMandates>();
        xpData = FindAssetByType<ActionXPData>();
        soundLibrary = FindAssetByType<SoundLibrary>();
        aiConfig = FindAssetByType<AIBalanceConfig>();

        // Объекты обновятся при отрисовке через DrawSmartManagerSettings, если они null
        
        Debug.Log($"[Ultimate Panel] Данные обновлены.");
    }

    private void DrawObjectField(string label, ref GameObject obj)
    {
        EditorGUI.BeginChangeCheck();
        obj = (GameObject)EditorGUILayout.ObjectField(label, obj, typeof(GameObject), false);
        if (EditorGUI.EndChangeCheck())
        {
            SaveManualReference(label, obj);
        }
    }

    private void DrawSmartManagerSettings<T>(ref T component, string label, string prefabName, System.Action<SerializedObject> drawContent) where T : Component
    {
        if (component == null)
        {
            component = FindGlobalManager<T>(prefabName);
        }

        EditorGUI.BeginChangeCheck();
        T newRef = (T)EditorGUILayout.ObjectField(label, component, typeof(T), true); 
        if (EditorGUI.EndChangeCheck())
        {
            component = newRef;
            if (component != null && EditorUtility.IsPersistent(component.gameObject))
            {
                SaveManualReference(prefabName, component.gameObject);
            }
        }

        if (component != null)
        {
            string sourceInfo = EditorUtility.IsPersistent(component.gameObject) ? "[PREFAB]" : "[SCENE]";
            Color infoColor = EditorUtility.IsPersistent(component.gameObject) ? Color.cyan : Color.yellow;
            
            GUI.color = infoColor;
            EditorGUILayout.LabelField($"Editing: {sourceInfo} {component.gameObject.name}", EditorStyles.miniLabel);
            GUI.color = Color.white;

            DrawComponentSettings(component, label, drawContent);
        }
        else
        {
            EditorGUILayout.HelpBox($"{label} не найден. Перетащите префаб или объект со сцены сюда.", MessageType.Warning);
        }
    }

    private T FindGlobalManager<T>(string prefabName) where T : Component
    {
        string savedGuid = EditorPrefs.GetString($"GBCP_{prefabName}", "");
        if (!string.IsNullOrEmpty(savedGuid))
        {
            string path = AssetDatabase.GUIDToAssetPath(savedGuid);
            if (!string.IsNullOrEmpty(path))
            {
                GameObject obj = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (obj != null && obj.GetComponent<T>() != null) return obj.GetComponent<T>();
            }
        }

        GameObject prefab = FindAssetByName<GameObject>(prefabName);
        if (prefab != null && prefab.GetComponent<T>() != null)
        {
            return prefab.GetComponent<T>();
        }

        T sceneObj = FindFirstObjectByType<T>();
        if (sceneObj != null) return sceneObj;

        return null;
    }

    private void SaveManualReference(string key, GameObject obj)
    {
        if (obj != null && EditorUtility.IsPersistent(obj))
        {
            string path = AssetDatabase.GetAssetPath(obj);
            string guid = AssetDatabase.AssetPathToGUID(path);
            EditorPrefs.SetString($"GBCP_{key}", guid);
        }
    }

    // --- ВОТ МЕТОД, КОТОРЫЙ Я ДОБАВИЛ ДЛЯ ИСПРАВЛЕНИЯ ОШИБКИ CS0103 ---
    private void SaveManualReferences()
    {
        SaveManualReference("Client Prefab", clientPrefab);
        SaveManualReference("Puddle Prefab", puddlePrefab);
    }
    // ------------------------------------------------------------------

    private void LoadSavedReferences()
    {
        clientPrefab = LoadRef("Client Prefab");
        puddlePrefab = LoadRef("Puddle Prefab");
    }

    private GameObject LoadRef(string label)
    {
        string guid = EditorPrefs.GetString($"GBCP_{label}", "");
        if (!string.IsNullOrEmpty(guid))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }
        return null;
    }

    private void SaveAllData()
    {
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Ultimate Panel] Все данные сохранены на диск.");
    }

    // --- Standard Helpers ---

    private void DrawSerializedObject(Object obj, string label, System.Action<SerializedObject> drawContent)
    {
        if (obj == null) return;
        SerializedObject so = new SerializedObject(obj);
        so.Update();

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        bool foldout = EditorPrefs.GetBool(obj.GetInstanceID().ToString(), false);
        EditorGUI.BeginChangeCheck();
        foldout = EditorGUILayout.Foldout(foldout, label, true);
        if (EditorGUI.EndChangeCheck()) EditorPrefs.SetBool(obj.GetInstanceID().ToString(), foldout);

        if (foldout)
        {
            EditorGUI.indentLevel++;
            drawContent(so);
            if (GUILayout.Button("Ping Asset", GUILayout.Width(100))) EditorGUIUtility.PingObject(obj);
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndVertical();
        if (so.ApplyModifiedProperties()) EditorUtility.SetDirty(obj);
    }

    private void DrawComponentSettings<T>(T component, string label, System.Action<SerializedObject> drawContent) where T : Component
    {
        if (component == null) return;
        DrawSerializedObject(component, label, drawContent);
    }

    private void DrawSpecificProperties(SerializedObject so, string prefix)
    {
        SerializedProperty prop = so.GetIterator();
        bool enter = true;
        while (prop.NextVisible(enter))
        {
            enter = false;
            if (prop.name.StartsWith(prefix)) EditorGUILayout.PropertyField(prop, true);
        }
    }

    private List<T> FindAssetsByType<T>() where T : Object
    {
        List<T> assets = new List<T>();
        string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) assets.Add(asset);
        }
        return assets;
    }

    private T FindAssetByType<T>() where T : Object
    {
        var list = FindAssetsByType<T>();
        return list.Count > 0 ? list[0] : null;
    }

    private T FindAssetByName<T>(string name) where T : Object
    {
        string[] guids = AssetDatabase.FindAssets($"{name} t:{typeof(T).Name}");
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<T>(path);
        }
        return null;
    }

    // ==========================================================================================
    // ТАБ: AI BALANCE
    // ==========================================================================================
    private void DrawAIBalanceTab()
    {
        if (aiConfig == null)
        {
            EditorGUILayout.HelpBox("AIBalanceConfig не найден. Создайте ассет в Resources/Databases/", MessageType.Warning);
            if (GUILayout.Button("Создать AIBalanceConfig"))
            {
                aiConfig = CreateAsset<AIBalanceConfig>("Databases/AIBalanceConfig");
            }
            return;
        }

        DrawSerializedObject(aiConfig, "Настройки Utility AI", (so) => {
            // Метаболизм
            EditorGUILayout.LabelField("=== МЕТАБОЛИЗМ (Дельты в секунду) ===", EditorStyles.boldLabel);
            DrawProperty(so, "baseEnergyLoss");
            DrawProperty(so, "baseBladderGain");
            DrawProperty(so, "baseMoraleLoss");
            DrawProperty(so, "baseStressGain");
            
            EditorGUILayout.Space();
            
            // Веса Utility AI
            EditorGUILayout.LabelField("=== ВЕСА UTILITY AI ===", EditorStyles.boldLabel);
            DrawProperty(so, "pedantrySortBonus");
            DrawProperty(so, "masteryWorkMultiplier");
            DrawProperty(so, "softSkillsHelpBonus");
            DrawProperty(so, "corruptionCashierBonus");
            DrawProperty(so, "stressHomeWeight");
            DrawProperty(so, "messStressMultiplier");
        });

        if (GUI.changed)
        {
            EditorUtility.SetDirty(aiConfig);
        }
    }

    private T CreateAsset<T>(string path) where T : ScriptableObject
    {
        T asset = CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, $"Assets/Resources/{path}.asset");
        AssetDatabase.SaveAssets();
        RefreshData();
        return asset;
    }

    private void DrawProperty(SerializedObject so, string propertyName)
    {
        EditorGUILayout.PropertyField(so.FindProperty(propertyName));
    }
}