// Файл: Assets/Editor/UtilityAIDebugWindow.cs
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Characters;

public class UtilityAIDebugWindow : EditorWindow
{
    private StaffController selectedStaff;
    private ClientPathfinding selectedClient;
    
    // Скроллы для списков
    private Vector2 staffScrollPos;
    private Vector2 clientScrollPos;
    private Vector2 actionsScrollPos;
    private Vector2 diaryScrollPos; 
    private Vector2 clientDetailsScrollPos;

    private int selectedTab = 0;
    private readonly string[] tabs = { "🌐 Общий Обзор", "👔 Мозг (Персонал)", "👥 Анализ (Клиенты)" };

    [MenuItem("Tools/AI Toolset/🧠 Utility AI Debugger")]
    public static void ShowWindow()
    {
        GetWindow<UtilityAIDebugWindow>("Utility AI");
    }

    private void OnInspectorUpdate()
    {
        if (Application.isPlaying) Repaint();
    }

    private void OnGUI()
    {
        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Запустите игру, чтобы начать мониторинг.", MessageType.Info);
            return;
        }

        EditorGUILayout.Space();
        selectedTab = GUILayout.Toolbar(selectedTab, tabs, GUILayout.Height(30));
        EditorGUILayout.Space();

        if (selectedTab == 0) DrawOverviewMode();
        else if (selectedTab == 1) DrawStaffDetailedMode();
        else if (selectedTab == 2) DrawClientDetailedMode();
    }

    // ============================================================================
    // РЕЖИМ 1: ОБЩИЙ ОБЗОР И ДАМПЫ
    // ============================================================================
    private void DrawOverviewMode()
    {
        // --- ПАНЕЛЬ ДАМПОВ ---
        EditorGUILayout.BeginVertical("box");
        GUILayout.Label("💾 ЭКСПОРТ ДАННЫХ (ДЛЯ AI)", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        
        GUI.backgroundColor = new Color(0.6f, 0.8f, 1f);
        if (GUILayout.Button("📋 ДАМП БРИГАДЫ", GUILayout.Height(30))) CopyStaffStateToClipboard();
        
        GUI.backgroundColor = new Color(0.8f, 0.6f, 1f);
        if (GUILayout.Button("📋 ДАМП КЛИЕНТОВ", GUILayout.Height(30))) CopyClientsStateToClipboard();
        
        GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
        if (GUILayout.Button("📋 ДАМП ОФИСА (ВСЁ)", GUILayout.Height(30))) CopyFullStateToClipboard();
        
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space();
        
        EditorGUILayout.BeginHorizontal();

        // --- КОЛОНКА ПЕРСОНАЛА ---
        EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(position.width / 2f - 5));
        GUILayout.Label("👔 ПЕРСОНАЛ", EditorStyles.boldLabel);
        
        var allStaff = FindObjectsByType<StaffController>(FindObjectsSortMode.None).ToList();
        staffScrollPos = EditorGUILayout.BeginScrollView(staffScrollPos);
        
        foreach (var staff in allStaff)
        {
            EditorGUILayout.BeginVertical(EditorStyles.textArea);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(staff.characterName, EditorStyles.boldLabel, GUILayout.Width(120));
            GUILayout.Label(staff.currentRole.ToString(), GUILayout.Width(100));
            EditorGUILayout.EndHorizontal();

            string state = staff.GetCurrentStateName();
            string task = staff.currentAction != null ? staff.currentAction.displayName : "БЕЗДЕЛЬЕ";
            Color taskColor = staff.currentAction != null ? new Color(0.2f, 0.8f, 0.2f) : Color.yellow;
            if (staff.IsOnBreak()) taskColor = Color.cyan;

            EditorGUILayout.LabelField($"Статус: {state}");
            GUI.contentColor = taskColor;
            EditorGUILayout.LabelField($"Задача: {task}", EditorStyles.boldLabel);
            GUI.contentColor = Color.white;

            if (GUILayout.Button("Исследовать мозг", EditorStyles.miniButton))
            {
                selectedStaff = staff;
                selectedTab = 1; 
            }
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();

        // --- КОЛОНКА КЛИЕНТОВ ---
        EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(position.width / 2f - 5));
        GUILayout.Label("👥 КЛИЕНТЫ", EditorStyles.boldLabel);
        
        var allClients = FindObjectsByType<ClientPathfinding>(FindObjectsSortMode.None).ToList();
        clientScrollPos = EditorGUILayout.BeginScrollView(clientScrollPos);
        
        foreach (var client in allClients)
        {
            if (client == null || client.stateMachine == null) continue;
            var state = client.stateMachine.GetCurrentState();

            Color bgColor = GUI.backgroundColor;
            if (state == ClientState.Confused) GUI.backgroundColor = new Color(1f, 0.8f, 0.2f); 
            if (state == ClientState.Enraged) GUI.backgroundColor = new Color(1f, 0.4f, 0.4f); 

            EditorGUILayout.BeginVertical(EditorStyles.textArea);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(client.name, EditorStyles.boldLabel, GUILayout.Width(120));
            GUILayout.Label(client.mainGoal.ToString(), GUILayout.Width(100));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField($"Состояние: {state}");
            DrawProgressBar("", client.PatienceHeat, GetHeatColor(client.PatienceHeat));

            if (GUILayout.Button("Анализ клиента", EditorStyles.miniButton))
            {
                selectedClient = client;
                selectedTab = 2; 
            }
            EditorGUILayout.EndVertical();
            GUI.backgroundColor = bgColor; 
        }
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();

        EditorGUILayout.EndHorizontal();
    }

    // ============================================================================
    // РЕЖИМ 2: МОЗГ ПЕРСОНАЛА
    // ============================================================================
    private void DrawStaffDetailedMode()
    {
        var allStaff = FindObjectsByType<StaffController>(FindObjectsSortMode.None).ToList();
        if (allStaff.Count == 0) return;

        string[] staffNames = allStaff.Select(s => $"{s.characterName} ({s.currentRole})").ToArray();
        int currentIndex = selectedStaff != null ? allStaff.IndexOf(selectedStaff) : 0;
        if (currentIndex < 0) currentIndex = 0;
        selectedStaff = allStaff[EditorGUILayout.Popup("Сотрудник:", currentIndex, staffNames)];

        if (selectedStaff == null) return;

        EditorGUILayout.Space();
        DrawCurrentStatus();
        EditorGUILayout.Space();
        DrawVitals();
        EditorGUILayout.Space();
        DrawBrainDump();
        EditorGUILayout.Space();
        DrawDiary();
    }

    // ============================================================================
    // РЕЖИМ 3: АНАЛИЗ КЛИЕНТОВ
    // ============================================================================
    private void DrawClientDetailedMode()
    {
        var allClients = FindObjectsByType<ClientPathfinding>(FindObjectsSortMode.None).ToList();
        if (allClients.Count == 0)
        {
            EditorGUILayout.HelpBox("В офисе нет клиентов.", MessageType.Info);
            return;
        }

        string[] clientNames = allClients.Select(c => $"{c.name} ({c.mainGoal})").ToArray();
        int currentIndex = selectedClient != null ? allClients.IndexOf(selectedClient) : 0;
        if (currentIndex < 0) currentIndex = 0;
        selectedClient = allClients[EditorGUILayout.Popup("Клиент:", currentIndex, clientNames)];

        if (selectedClient == null || selectedClient.stateMachine == null) return;

        clientDetailsScrollPos = EditorGUILayout.BeginScrollView(clientDetailsScrollPos);

        // --- БАЗОВАЯ ИНФО ---
        EditorGUILayout.BeginVertical("box");
        GUILayout.Label("📝 АНКЕТА КЛИЕНТА", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Имя/Тип:", selectedClient.name);
        EditorGUILayout.LabelField("Полная цель:", selectedClient.mainGoal.ToString());
        EditorGUILayout.LabelField("Номер талона:", selectedClient.stateMachine.MyQueueNumber > 0 ? selectedClient.stateMachine.MyQueueNumber.ToString() : "Нет талона");
        
        string assignedWorker = selectedClient.stateMachine.MyServiceProvider != null 
            ? ((MonoBehaviour)selectedClient.stateMachine.MyServiceProvider).name 
            : "Никто не назначен";
        EditorGUILayout.LabelField("Назначенный работник:", assignedWorker);
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space();

        // --- СОСТОЯНИЕ И ПРОГРЕСС ---
        EditorGUILayout.BeginVertical("box");
        GUILayout.Label("📊 ПРОГРЕСС И ПСИХИКА", EditorStyles.boldLabel);
        
        float progress = selectedClient.stateMachine.GetNormalizedProgress();
        DrawProgressBar("Прогресс услуги", progress, Color.cyan);
        DrawProgressBar("Уровень бешенства (Стресс)", selectedClient.PatienceHeat, GetHeatColor(selectedClient.PatienceHeat));
        
        EditorGUILayout.LabelField($"Текущее состояние ИИ: {selectedClient.stateMachine.GetCurrentState()}", EditorStyles.boldLabel);
        
        string targetZone = selectedClient.stateMachine.GetTargetZone() != null ? selectedClient.stateMachine.GetTargetZone().name : "Общий зал";
        EditorGUILayout.LabelField($"Текущая Зона: {targetZone}");
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space();

        // --- "ДНЕВНИК" ШАГОВ ---
        EditorGUILayout.BeginVertical("box");
        GUILayout.Label("🗺️ МАРШРУТНЫЙ ЛИСТ", EditorStyles.boldLabel);
        
        string currentStep = "Ожидание действий...";
        string nextStep = "Неизвестно";

        var state = selectedClient.stateMachine.GetCurrentState();
        
        // Эвристика для отображения шагов
        if (state == ClientState.MovingToGoal || state == ClientState.MovingToRegistrarImpolite)
        {
            currentStep = "Идет к точке: " + (selectedClient.stateMachine.GetCurrentGoal() != null ? selectedClient.stateMachine.GetCurrentGoal().name : "???");
            nextStep = "Занять очередь / Подойти к столу";
        }
        else if (state == ClientState.AtWaitingArea || state == ClientState.SittingInWaitingArea)
        {
            currentStep = "Ждет вызова по талону";
            nextStep = "Подойти к окну обслуживания по вызову";
        }
        else if (state == ClientState.AtRegistration)
        {
            currentStep = "Общается с регистратором";
            nextStep = "Получить направление и пойти к профильному окну";
        }
        else if (state == ClientState.AtDesk1 || state == ClientState.AtDesk2 || state == ClientState.InsideLimitedZone)
        {
            currentStep = "Обслуживается у клерка";
            nextStep = selectedClient.billToPay > 0 ? "Пойти в кассу для оплаты" : "Покинуть офис (Успех)";
        }
        else if (state == ClientState.AtCashier || state == ClientState.GoingToCashier)
        {
            currentStep = "Находится на кассе (Оплата)";
            nextStep = "Покинуть офис (Успех)";
        }
        else if (state == ClientState.Confused)
        {
            currentStep = "СБИЛСЯ С ПУТИ (Confused)";
            nextStep = "Попытаться найти новую цель или разозлиться";
        }
        else if (state == ClientState.Leaving || state == ClientState.LeavingUpset || state == ClientState.Enraged)
        {
            currentStep = $"Покидает офис. Причина: {selectedClient.reasonForLeaving}";
            nextStep = "Уничтожение объекта (Despawn)";
        }

        GUI.contentColor = Color.yellow;
        EditorGUILayout.LabelField("► СЕЙЧАС:", currentStep, EditorStyles.boldLabel);
        GUI.contentColor = new Color(0.6f, 0.6f, 0.6f);
        EditorGUILayout.LabelField("▷ ДАЛЕЕ:", nextStep);
        GUI.contentColor = Color.white;

        EditorGUILayout.EndVertical();

        EditorGUILayout.EndScrollView();
    }

    // ============================================================================
    // МЕТОДЫ ОТРИСОВКИ ДЕТАЛЕЙ ПЕРСОНАЛА
    // ============================================================================
    private void DrawCurrentStatus()
    {
        EditorGUILayout.BeginVertical("box");
        GUILayout.Label("🎯 ТЕКУЩЕЕ СОСТОЯНИЕ", EditorStyles.boldLabel);
        
        EditorGUILayout.LabelField("Роль:", selectedStaff.currentRole.ToString());
        EditorGUILayout.LabelField("Состояние:", selectedStaff.GetCurrentStateName());
        
        if (selectedStaff.currentAction != null)
        {
            GUI.contentColor = new Color(0.2f, 0.9f, 0.2f);
            EditorGUILayout.LabelField("Текущее действие:", selectedStaff.currentAction.displayName, EditorStyles.boldLabel);
            GUI.contentColor = Color.white;
        }
        else
        {
            EditorGUILayout.LabelField("Действие:", "Бездействие", EditorStyles.boldLabel);
        }
        
        EditorGUILayout.LabelField("Подзадача:", selectedStaff.CurrentSubStatus ?? "Нет");
        
        EditorGUILayout.EndVertical();
    }

    private void DrawVitals()
    {
        EditorGUILayout.BeginVertical("box");
        GUILayout.Label("❤️ ЖИЗНЕННЫЕ ПОКАЗАТЕЛИ", EditorStyles.boldLabel);
        
        // StaffController использует прямые поля, а не структуру vitals
        float energyNorm = selectedStaff.energy / 100f;
        float stressNorm = selectedStaff.stress / 100f;
        float moraleNorm = selectedStaff.morale / 100f;
        
        DrawProgressBar("Энергия", energyNorm, GetHeatColor(1f - energyNorm));
        DrawProgressBar("Стресс", stressNorm, GetHeatColor(stressNorm));
        DrawProgressBar("Настроение (Morale)", moraleNorm, GetHeatColor(1f - moraleNorm));
        
        EditorGUILayout.LabelField($"Энергия: {selectedStaff.energy:F1}% | Стресс: {selectedStaff.stress:F1}% | Morale: {selectedStaff.morale:F1}%", EditorStyles.boldLabel);
        
        EditorGUILayout.EndVertical();
    }

    private void DrawProgressBar(string label, float value, Color color)
    {
        Rect rect = EditorGUILayout.GetControlRect(false, 16f);
        EditorGUI.ProgressBar(rect, value, $"{label}: {(value * 100f):F0}%");
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width * value, rect.height), new Color(color.r, color.g, color.b, 0.4f));
    }

    private void DrawBrainDump()
    {
        EditorGUILayout.BeginVertical("box");
        GUILayout.Label("🧠 РЕЗУЛЬТАТЫ ДУМАНИЯ", EditorStyles.boldLabel);
        
        // Собираем все доступные действия (активные + из базы данных)
        var allActions = new List<StaffAction>();
        if (selectedStaff.activeActions != null)
            allActions.AddRange(selectedStaff.activeActions);
        
        // Добавляем системные действия из базы
        if (selectedStaff.systemActionDatabase != null && selectedStaff.systemActionDatabase.allActions != null)
            allActions.AddRange(selectedStaff.systemActionDatabase.allActions);
        
        if (allActions.Count == 0)
        {
            EditorGUILayout.LabelField("Нет доступных действий");
            EditorGUILayout.EndVertical();
            return;
        }

        actionsScrollPos = EditorGUILayout.BeginScrollView(actionsScrollPos, GUILayout.Height(200));
        
        // Сортируем действия по утилите
        var sortedActions = allActions.OrderByDescending(a =>
        {
            try { return a.AreConditionsMet(selectedStaff) ? a.CalculateUtility(selectedStaff) : 0f; }
            catch { return 0f; }
        }).ToList();
        
        EditorGUILayout.BeginVertical();
        foreach (var action in sortedActions)
        {
            if (action == null) continue;
            
            bool conditionsMet = false;
            float utility = 0f;
            try
            {
                conditionsMet = action.AreConditionsMet(selectedStaff);
                if (conditionsMet)
                    utility = action.CalculateUtility(selectedStaff);
            }
            catch { conditionsMet = false; }
            
            string info = "";
            try { info = action.GetDebugInfo(selectedStaff); } catch {}
            
            Color bgColor = GUI.backgroundColor;
            if (action == selectedStaff.currentAction)
            {
                GUI.backgroundColor = new Color(0.2f, 0.8f, 0.2f, 0.3f);
            }
            else if (!conditionsMet)
            {
                GUI.backgroundColor = new Color(0.3f, 0.3f, 0.3f, 0.3f);
            }
            
            string status = conditionsMet ? $"Утилита: {utility:F1}" : "Условия не выполнены";
            
            EditorGUILayout.BeginHorizontal("box");
            GUILayout.Label(action.displayName, EditorStyles.boldLabel, GUILayout.Width(150));
            GUILayout.Label(status, GUILayout.Width(130));
            
            if (!string.IsNullOrEmpty(info))
            {
                GUILayout.Label($"({info})", EditorStyles.miniLabel);
            }
            
            EditorGUILayout.EndHorizontal();
            GUI.backgroundColor = bgColor;
        }
        
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndScrollView();
        
        EditorGUILayout.EndVertical();
    }

    private void DrawDiary()
    {
        EditorGUILayout.BeginVertical("box");
        GUILayout.Label("📓 ДНЕВНИК ЗАДАЧ", EditorStyles.boldLabel);
        
        // Здесь можно добавить логику отображения дневника задач, если она есть
        // Пока просто показываем текущее действие
        
        if (selectedStaff.currentAction != null)
        {
            EditorGUILayout.LabelField("Выполняется:", selectedStaff.currentAction.displayName);
            
            if (selectedStaff.currentExecutor != null)
            {
                var executor = selectedStaff.currentExecutor;
                var actionType = executor.GetType().Name;
                EditorGUILayout.LabelField("Executor:", actionType);
            }
        }
        
        EditorGUILayout.EndVertical();
    }

    // ============================================================================
    // МЕТОДЫ ДЛЯ РАБОТЫ С БУФЕРОМ ОБМЕНА
    // ============================================================================
    private void CopyStaffStateToClipboard()
    {
        var allStaff = FindObjectsByType<StaffController>(FindObjectsSortMode.None).ToList();
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== STAFF DUMP ===");
        sb.AppendLine($"Total Staff: {allStaff.Count}\n");

        foreach (var s in allStaff)
        {
            sb.AppendLine($"[{s.characterName}]");
            sb.AppendLine($"  Role: {s.currentRole}");
            sb.AppendLine($"  State: {s.GetCurrentStateName()}");
            sb.AppendLine($"  Task: {(s.currentAction != null ? s.currentAction.displayName : "IDLE")}");
            sb.AppendLine($"  Energy: {s.energy:F1}%");
            sb.AppendLine($"  Stress: {s.stress:F1}%");
            sb.AppendLine($"  Morale: {s.morale:F1}%");
            sb.AppendLine();
        }
        
        GUIUtility.systemCopyBuffer = sb.ToString();
        Debug.Log("Дамп персонала скопирован!");
    }

    private void CopyClientsStateToClipboard()
    {
        var allClients = FindObjectsByType<ClientPathfinding>(FindObjectsSortMode.None).ToList();
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== CLIENTS DUMP ===");
        sb.AppendLine($"Total Clients: {allClients.Count}\n");

        foreach (var c in allClients)
        {
            if (c == null || c.stateMachine == null) continue;
            sb.AppendLine($"[{c.stateMachine.MyQueueNumber}] {c.name}");
            sb.AppendLine($"  Goal: {c.mainGoal}");
            sb.AppendLine($"  State: {c.stateMachine.GetCurrentState()}");
            sb.AppendLine($"  Stress: {c.PatienceHeat * 100f:F1}%");
            sb.AppendLine($"  ReasonForLeaving: {c.reasonForLeaving}");
            sb.AppendLine();
        }
        
        GUIUtility.systemCopyBuffer = sb.ToString();
        Debug.Log("Дамп клиентов скопирован!");
    }

    private void CopyFullStateToClipboard()
    {
        var allStaff = FindObjectsByType<StaffController>(FindObjectsSortMode.None).ToList();
        var allClients = FindObjectsByType<ClientPathfinding>(FindObjectsSortMode.None).ToList();
        
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== FULL OFFICE STATE ===");
        sb.AppendLine($"Timestamp: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Staff: {allStaff.Count}, Clients: {allClients.Count}");
        sb.AppendLine();
        
        // Staff section
        sb.AppendLine("--- STAFF ---");
        foreach (var s in allStaff)
        {
            sb.AppendLine($"[{s.characterName}] {s.currentRole} | State: {s.GetCurrentStateName()} | Task: {(s.currentAction != null ? s.currentAction.displayName : "IDLE")}");
        }
        sb.AppendLine();
        
        // Clients section
        sb.AppendLine("--- CLIENTS ---");
        foreach (var c in allClients)
        {
            if (c == null || c.stateMachine == null) continue;
            sb.AppendLine($"[{c.stateMachine.MyQueueNumber}] {c.name} | Goal: {c.mainGoal} | State: {c.stateMachine.GetCurrentState()}");
        }
        
        GUIUtility.systemCopyBuffer = sb.ToString();
        Debug.Log("Полный дамп офиса скопирован!");
    }

    // ============================================================================
    // ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ
    // ============================================================================
    private Color GetHeatColor(float heat)
    {
        if (heat > 0.8f) return Color.red;
        if (heat > 0.5f) return new Color(1f, 0.5f, 0f);
        return Color.green;
    }
}
