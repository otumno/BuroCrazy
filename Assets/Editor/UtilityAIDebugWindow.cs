// Файл: Assets/Editor/UtilityAIDebugWindow.cs
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public class UtilityAIDebugWindow : EditorWindow
{
    private StaffController selectedStaff;
    
    // Скроллы для списков
    private Vector2 staffScrollPos;
    private Vector2 clientScrollPos;
    private Vector2 actionsScrollPos;
    private Vector2 diaryScrollPos; // Скролл для дневника задач

    private int selectedTab = 0;
    private readonly string[] tabs = { "🌐 Общий Обзор (Офис)", "🧠 Детальный Анализ (ИИ)" };

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
            EditorGUILayout.HelpBox("Запустите игру, чтобы начать мониторинг ИИ и клиентов.", MessageType.Info);
            return;
        }

        EditorGUILayout.Space();
        selectedTab = GUILayout.Toolbar(selectedTab, tabs, GUILayout.Height(30));
        EditorGUILayout.Space();

        if (selectedTab == 0)
        {
            DrawOverviewMode();
        }
        else
        {
            DrawDetailedMode();
        }
    }

    // ============================================================================
    // РЕЖИМ 1: ОБЩИЙ ОБЗОР (РАБОТНИКИ И КЛИЕНТЫ)
    // ============================================================================
    private void DrawOverviewMode()
    {
        // Кнопка для копирования дампа всей бригады
        GUI.backgroundColor = new Color(0.8f, 0.6f, 1f);
        if (GUILayout.Button("📋 СКОПИРОВАТЬ ДНЕВНИКИ ВСЕЙ БРИГАДЫ", GUILayout.Height(35)))
        {
            CopyFullStateToClipboard();
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.Space();
        
        EditorGUILayout.BeginHorizontal();

        // --- ЛЕВАЯ КОЛОНКА (ПЕРСОНАЛ) ---
        EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(position.width / 2f - 5));
        GUILayout.Label("👔 ПЕРСОНАЛ", EditorStyles.boldLabel);
        
        var allStaff = FindObjectsByType<StaffController>(FindObjectsSortMode.None).ToList();
        GUILayout.Label($"Активных сотрудников: {allStaff.Count}", EditorStyles.miniLabel);
        
        staffScrollPos = EditorGUILayout.BeginScrollView(staffScrollPos);
        
        foreach (var staff in allStaff)
        {
            EditorGUILayout.BeginVertical(EditorStyles.textArea);
            
            // Имя и Роль
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(staff.characterName, EditorStyles.boldLabel, GUILayout.Width(120));
            GUILayout.Label(staff.currentRole.ToString(), GUILayout.Width(100));
            EditorGUILayout.EndHorizontal();

            // Статус и Текущая задача
            string state = staff.GetCurrentStateName();
            string task = staff.currentAction != null ? staff.currentAction.displayName : "БЕЗДЕЛЬЕ";
            Color taskColor = staff.currentAction != null ? new Color(0.2f, 0.8f, 0.2f) : Color.yellow;
            if (staff.IsOnBreak()) taskColor = Color.cyan;

            EditorGUILayout.LabelField($"Статус: {state}");
            
            GUI.contentColor = taskColor;
            EditorGUILayout.LabelField($"Задача: {task}", EditorStyles.boldLabel);
            GUI.contentColor = Color.white;

            // Кнопка быстрого перехода в детали
            if (GUILayout.Button("Исследовать мозг", EditorStyles.miniButton))
            {
                selectedStaff = staff;
                selectedTab = 1; // Переключаемся на детальный вид
            }
            
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();

        // --- ПРАВАЯ КОЛОНКА (КЛИЕНТЫ) ---
        EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(position.width / 2f - 5));
        GUILayout.Label("👥 КЛИЕНТЫ", EditorStyles.boldLabel);
        
        var allClients = FindObjectsByType<ClientPathfinding>(FindObjectsSortMode.None).ToList();
        GUILayout.Label($"Клиентов в здании: {allClients.Count}", EditorStyles.miniLabel);

        clientScrollPos = EditorGUILayout.BeginScrollView(clientScrollPos);
        
        foreach (var client in allClients)
        {
            if (client == null || client.stateMachine == null) continue;

            var state = client.stateMachine.GetCurrentState();
            bool isConfused = state == ClientState.Confused;
            bool isEnraged = state == ClientState.Enraged;

            // Подсветка проблемных клиентов
            Color bgColor = GUI.backgroundColor;
            if (isConfused) GUI.backgroundColor = new Color(1f, 0.8f, 0.2f); // Желто-оранжевый
            if (isEnraged) GUI.backgroundColor = new Color(1f, 0.4f, 0.4f); // Красный

            EditorGUILayout.BeginVertical(EditorStyles.textArea);
            
            // Имя и Цель
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(client.name, EditorStyles.boldLabel, GUILayout.Width(120));
            GUILayout.Label(client.mainGoal.ToString(), GUILayout.Width(100));
            EditorGUILayout.EndHorizontal();

            // Состояние
            EditorGUILayout.LabelField($"Состояние: {state}");

            // Стресс (Терпение)
            DrawProgressBar("", client.PatienceHeat, GetHeatColor(client.PatienceHeat));

            // Помощник (если есть)
            if (client.assignedHelper != null)
            {
                GUI.contentColor = Color.cyan;
                EditorGUILayout.LabelField($"Помогает: {client.assignedHelper.characterName}", EditorStyles.boldLabel);
                GUI.contentColor = Color.white;
            }
            else if (isConfused)
            {
                GUI.contentColor = Color.red;
                EditorGUILayout.LabelField("НИКТО НЕ ПОМОГАЕТ!", EditorStyles.boldLabel);
                GUI.contentColor = Color.white;
            }

            EditorGUILayout.EndVertical();
            GUI.backgroundColor = bgColor; // Сброс фона
        }
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();

        EditorGUILayout.EndHorizontal();
    }

    private Color GetHeatColor(float heat)
    {
        if (heat > 0.8f) return Color.red;
        if (heat > 0.5f) return new Color(1f, 0.5f, 0f);
        return Color.green;
    }

    // ============================================================================
    // РЕЖИМ 2: ДЕТАЛЬНЫЙ АНАЛИЗ (МОЗГ)
    // ============================================================================
    private void DrawDetailedMode()
    {
        DrawStaffSelector();

        if (selectedStaff == null)
        {
            EditorGUILayout.HelpBox("Выберите сотрудника для мониторинга.", MessageType.Warning);
            return;
        }

        EditorGUILayout.Space();

        // --- КНОПКА КОПИРОВАНИЯ ---
        GUI.backgroundColor = new Color(0.6f, 0.8f, 1f);
        if (GUILayout.Button("📋 Скопировать дамп для ИИ (в буфер)", GUILayout.Height(30)))
        {
            CopyStateToClipboard();
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.Space();
        // --------------------------

        DrawCurrentStatus();
        EditorGUILayout.Space();
        DrawVitals();
        EditorGUILayout.Space();
        DrawBrainDump();
        EditorGUILayout.Space();
        DrawDiary(); // Дневник задач
    }

    // ============================================================================
    // ОТОБРАЖЕНИЕ ДНЕВНИКА ЗАДАЧ
    // ============================================================================
    private void DrawDiary()
    {
        if (selectedStaff == null) return;
        
        EditorGUILayout.BeginVertical("box");
        
        // Заголовок
        EditorGUILayout.BeginHorizontal();
        GUI.contentColor = new Color(1f, 0.8f, 0.4f);
        GUILayout.Label("📔 ДНЕВНИК ЗАДАЧ", EditorStyles.boldLabel);
        GUI.contentColor = Color.white;
        
        // Статистика
        var diary = selectedStaff.taskDiary;
        int completed = diary != null ? diary.Count(e => e.IsCompleted) : 0;
        int total = diary != null ? diary.Count : 0;
        GUILayout.Label($"({completed}/{total} задач)", EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space();
        
        if (diary == null || diary.Count == 0)
        {
            EditorGUILayout.LabelField("Записей пока нет...", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
            return;
        }
        
        // Скролл для дневника
        diaryScrollPos = EditorGUILayout.BeginScrollView(diaryScrollPos, GUILayout.Height(200));
        
        // Показываем записи в обратном порядке (новые сверху)
        for (int i = diary.Count - 1; i >= 0; i--)
        {
            var entry = diary[i];
            if (entry == null) continue;
            
            EditorGUILayout.BeginHorizontal(EditorStyles.textArea);
            
            // Цвет в зависимости от статуса
            if (entry.IsCompleted)
            {
                // Завершённая задача
                Color completedColor = new Color(0.2f, 0.8f, 0.4f);
                GUI.contentColor = completedColor;
                GUILayout.Label("✓", EditorStyles.boldLabel, GUILayout.Width(20));
                
                string duration = entry.Duration >= 60f
                    ? $"{entry.Duration / 60f:F1} мин"
                    : $"{entry.Duration:F1} сек";
                GUILayout.Label($"{entry.TaskName}", EditorStyles.label, GUILayout.Width(180));
                GUILayout.Label(duration, EditorStyles.miniLabel);
            }
            else
            {
                // Текущая задача (выполняется)
                Color currentColor = new Color(1f, 0.8f, 0.2f);
                GUI.contentColor = currentColor;
                GUILayout.Label("▶", EditorStyles.boldLabel, GUILayout.Width(20));
                GUILayout.Label($"{entry.TaskName}", EditorStyles.boldLabel, GUILayout.Width(180));
                GUILayout.Label("выполняется...", EditorStyles.miniLabel);
            }
            
            GUI.contentColor = Color.white;
            EditorGUILayout.EndHorizontal();
        }
        
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private void DrawStaffSelector()
    {
        var allStaff = FindObjectsByType<StaffController>(FindObjectsSortMode.None).ToList();
        if (allStaff.Count == 0) return;

        string[] staffNames = allStaff.Select(s => $"{s.characterName} ({s.currentRole})").ToArray();
        int currentIndex = selectedStaff != null ? allStaff.IndexOf(selectedStaff) : 0;
        if (currentIndex < 0) currentIndex = 0;

        int newIndex = EditorGUILayout.Popup("Анализ мозга:", currentIndex, staffNames);
        selectedStaff = allStaff[newIndex];
    }

    private void DrawCurrentStatus()
    {
        EditorGUILayout.BeginVertical("box");
        string workspace = selectedStaff.assignedWorkstation != null ? selectedStaff.assignedWorkstation.name : "НЕТ";
        EditorGUILayout.LabelField($"Рабочее место: {workspace}");
        
        string currentTask = selectedStaff.currentAction != null ? selectedStaff.currentAction.displayName : "БЕЗДЕЛЬЕ";
        Color taskColor = selectedStaff.currentAction != null ? new Color(0.2f, 0.8f, 0.2f) : Color.yellow;
        
        GUIStyle taskStyle = new GUIStyle(EditorStyles.label) { fontStyle = FontStyle.Bold };
        taskStyle.normal.textColor = taskColor;
        
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Выполняет:", GUILayout.Width(145));
        EditorGUILayout.LabelField(currentTask, taskStyle);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    private void DrawVitals()
    {
        EditorGUILayout.BeginVertical("box");
        DrawProgressBar("Усталость", 1f - (selectedStaff.energy / 100f), new Color(0.2f, 0.6f, 1f));
        DrawProgressBar("Жажда", 1f - selectedStaff.morale, new Color(0.2f, 0.6f, 1f));
        DrawProgressBar("Туалет", selectedStaff.bladder, new Color(0.8f, 0.8f, 0.2f));
        DrawProgressBar("Стресс", selectedStaff.stress, new Color(0.8f, 0.2f, 0.2f));
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
        GUILayout.Label("ОЦЕНКА ДЕЙСТВИЙ (UTILITY AI)", EditorStyles.boldLabel);

        if (selectedStaff.currentBrainDump == null || selectedStaff.currentBrainDump.Count == 0)
        {
            EditorGUILayout.HelpBox("Сотрудник еще не думал.", MessageType.Info);
            EditorGUILayout.EndVertical();
            return;
        }

        var sortedDump = selectedStaff.currentBrainDump
            .OrderByDescending(d => d.ConditionsMet)
            .ThenByDescending(d => d.Score)
            .ToList();

        actionsScrollPos = EditorGUILayout.BeginScrollView(actionsScrollPos);

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Действие", EditorStyles.boldLabel, GUILayout.Width(160));
        GUILayout.Label("Статус", EditorStyles.boldLabel, GUILayout.Width(130));
        GUILayout.Label("Вес", EditorStyles.boldLabel, GUILayout.Width(50));
        EditorGUILayout.EndHorizontal();

        foreach (var data in sortedDump)
        {
            EditorGUILayout.BeginHorizontal();
            
            string displayName = string.IsNullOrEmpty(data.ActionName) ? $"[{data.AssetName}]" : data.ActionName;
            GUILayout.Label(displayName, GUILayout.Width(160));
            
            Color statusColor = Color.gray;
            if (data.ConditionsMet) statusColor = Color.green;
            else if (data.StatusMessage != null && data.StatusMessage.Contains("%")) statusColor = new Color(0.8f, 0.6f, 0.2f);
            else if (data.StatusMessage != null && data.StatusMessage.Contains("КРИТИЧНО")) statusColor = Color.red;
            
            GUI.contentColor = statusColor;
            GUILayout.Label(data.StatusMessage ?? "???", EditorStyles.boldLabel, GUILayout.Width(130));
            GUI.contentColor = Color.white;
            
            GUI.color = data.ConditionsMet ? Color.cyan : new Color(0.3f, 0.3f, 0.3f);
            string scoreText = data.ConditionsMet ? data.Score.ToString("F1") : "---";
            GUILayout.Label(scoreText, EditorStyles.boldLabel, GUILayout.Width(50));
            GUI.color = Color.white;
            
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    // --- ЛОГИКА КОПИРОВАНИЯ ДАМПА ДЛЯ ChatGPT ---
    private void CopyStateToClipboard()
    {
        if (selectedStaff == null) return;

        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"=== UAD DUMP: {selectedStaff.characterName} ({selectedStaff.currentRole}) ===");
        
        string workspace = selectedStaff.assignedWorkstation != null ? selectedStaff.assignedWorkstation.name : "NONE";
        sb.AppendLine($"Workstation: {workspace}");
        sb.AppendLine($"State Machine: {selectedStaff.GetCurrentStateName()}");
        sb.AppendLine($"Current Task: {(selectedStaff.currentAction != null ? selectedStaff.currentAction.displayName : "IDLE")}");
        
        sb.AppendLine("\n--- VITALS ---");
        sb.AppendLine($"Energy (Fatigue): {selectedStaff.energy:F1}/100");
        sb.AppendLine($"Morale (Thirst): {selectedStaff.morale:P0}");
        sb.AppendLine($"Bladder (Toilet): {selectedStaff.bladder:P0}");
        sb.AppendLine($"Stress: {selectedStaff.stress:F1}");
        
        sb.AppendLine("\n--- BRAIN DUMP (Sorted) ---");
        if (selectedStaff.currentBrainDump != null && selectedStaff.currentBrainDump.Count > 0)
        {
            var sortedDump = selectedStaff.currentBrainDump
                .OrderByDescending(d => d.ConditionsMet)
                .ThenByDescending(d => d.Score)
                .ToList();

            foreach (var d in sortedDump)
            {
                string name = string.IsNullOrEmpty(d.ActionName) ? $"[{d.AssetName}]" : d.ActionName;
                string scoreStr = d.ConditionsMet ? d.Score.ToString("F1") : "N/A";
                string metStr = d.ConditionsMet ? "YES" : "NO ";
                sb.AppendLine($"- [{metStr}] {name,-25} | Score: {scoreStr,-5} | Status: {d.StatusMessage}");
            }
        }
        else
        {
            sb.AppendLine("Brain dump is empty.");
        }
        
        // --- TASK DIARY EXPORT ---
        sb.AppendLine("\n--- TASK DIARY ---");
        if (selectedStaff.taskDiary != null && selectedStaff.taskDiary.Count > 0)
        {
            float totalTime = 0f;
            foreach (var entry in selectedStaff.taskDiary)
            {
                if (entry == null) continue;
                
                string status = entry.IsCompleted ? "DONE" : "ACTIVE";
                string duration = entry.Duration >= 60f
                    ? $"{entry.Duration / 60f:F1} min"
                    : $"{entry.Duration:F1} sec";
                
                sb.AppendLine($"- [{status}] {entry.TaskName,-30} | {duration}");
                
                if (entry.IsCompleted) totalTime += entry.Duration;
            }
            
            float totalMinutes = totalTime / 60f;
            sb.AppendLine($"Total logged time: {totalMinutes:F1} minutes");
        }
        else
        {
            sb.AppendLine("Task diary is empty.");
        }
        // -------------------------

        GUIUtility.systemCopyBuffer = sb.ToString();
        Debug.Log($"<color=green>[UAD]</color> Состояние {selectedStaff.characterName} скопировано в буфер обмена! Нажмите Ctrl+V в чате.");
    }
    
    // ============================================================================
    // ПОЛНЫЙ ДАМП ВСЕЙ БРИГАДЫ
    // ============================================================================
    private void CopyFullStateToClipboard()
    {
        var allStaff = FindObjectsByType<StaffController>(FindObjectsSortMode.None).ToList();
        if (allStaff.Count == 0)
        {
            EditorUtility.DisplayDialog("UAD", "Нет сотрудников в игре!", "OK");
            return;
        }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== FULL OFFICE DUMP ===");
        sb.AppendLine($"Generated: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Total Staff: {allStaff.Count}");
        sb.AppendLine();

        foreach (var staff in allStaff)
        {
            if (staff == null) continue;

            sb.AppendLine($">>> {staff.characterName} ({staff.currentRole})");
            sb.AppendLine($"    Status: {staff.GetCurrentStateName()}");
            sb.AppendLine($"    Energy: {staff.energy:F1}/100 (Fatigue: {100f - staff.energy:F1})");
            sb.AppendLine($"    Morale: {staff.morale:F1}/100 (Thirst: {100f - staff.morale:F1})");
            sb.AppendLine($"    Bladder: {staff.bladder:F1}/100");
            sb.AppendLine($"    Stress: {staff.stress:F1}/100");
            
            // Текущая задача
            string currentTask = staff.currentAction != null
                ? staff.currentAction.displayName
                : "IDLE";
            sb.AppendLine($"    Current Task: {currentTask}");

            // Дневник задач
            sb.AppendLine("    --- Task Diary ---");
            if (staff.taskDiary != null && staff.taskDiary.Count > 0)
            {
                float totalTime = 0f;
                foreach (var entry in staff.taskDiary)
                {
                    if (entry == null) continue;
                    
                    string status = entry.IsCompleted ? "DONE" : "ACTIVE";
                    string duration = entry.Duration >= 60f
                        ? $"{entry.Duration / 60f:F1} min"
                        : $"{entry.Duration:F1} sec";
                    
                    sb.AppendLine($"      [{status}] {entry.TaskName} - {duration}");
                    
                    if (entry.IsCompleted) totalTime += entry.Duration;
                }
                
                float totalMinutes = totalTime / 60f;
                sb.AppendLine($"    Total logged: {totalMinutes:F1} minutes");
            }
            else
            {
                sb.AppendLine("      (empty)");
            }

            // Текущая выполняемая задача (если есть)
            if (staff.CurrentTaskEntry != null && !staff.CurrentTaskEntry.IsCompleted)
            {
                float currentDuration = Time.time - staff.CurrentTaskEntry.StartTime;
                string durationStr = currentDuration >= 60f
                    ? $"{currentDuration / 60f:F1} min"
                    : $"{currentDuration:F1} sec";
                sb.AppendLine($"    >> CURRENT: {staff.CurrentTaskEntry.TaskName} ({durationStr})");
            }

            sb.AppendLine();
        }

        GUIUtility.systemCopyBuffer = sb.ToString();
        Debug.Log($"<color=green>[UAD]</color> Дамп бригады ({allStaff.Count} сотрудников) скопирован в буфер обмена!");
    }
}
