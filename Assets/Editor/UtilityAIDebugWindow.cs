// Assets/Editor/UtilityAIDebugWindow.cs
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

public class UtilityAIDebugWindow : EditorWindow
{
    private List<StaffController> allStaff = new List<StaffController>();
    private StaffController selectedStaff;
    private Vector2 scrollPosition;
    
    [MenuItem("Bureau/Utility AI Debug")]
    public static void ShowWindow()
    {
        GetWindow<UtilityAIDebugWindow>("AI Debug");
    }

    private void OnGUI()
    {
        GUILayout.Label("Отладка AI Сотрудников", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // Кнопка обновления списка
        if (GUILayout.Button("Обновить список сотрудников"))
        {
            RefreshStaffList();
        }

        EditorGUILayout.Space();

        // Выбор сотрудника
        if (allStaff.Count > 0)
        {
            var staffNames = allStaff.Select(s => s.characterName).ToArray();
            var staffOptions = new List<string> { "Выберите сотрудника..." };
            staffOptions.AddRange(staffNames);
            
            int currentIndex = selectedStaff != null ? allStaff.IndexOf(selectedStaff) + 1 : 0;
            int newIndex = EditorGUILayout.Popup("Сотрудник:", currentIndex, staffOptions.ToArray());
            
            if (newIndex > 0 && newIndex <= allStaff.Count)
            {
                selectedStaff = allStaff[newIndex - 1];
            }
        }
        else
        {
            EditorGUILayout.LabelField("Сотрудники не найдены", EditorStyles.helpBox);
        }

        EditorGUILayout.Space();

        // Отображение данных мозга
        if (selectedStaff != null)
        {
            DrawStaffInfo();
        }
    }

    private void RefreshStaffList()
    {
        allStaff.Clear();
        
        // Ищем все объекты с компонентом StaffController
        var staffObjects = FindObjectsOfType<StaffController>();
        allStaff.AddRange(staffObjects);
        
        // Обновляем выбор
        if (selectedStaff != null && !allStaff.Contains(selectedStaff))
        {
            selectedStaff = null;
        }
    }

    private Vector2 actionsScrollPos;

    private void DrawStaffInfo()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField($"=== {selectedStaff.characterName} ===", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"Роль: {selectedStaff.role}");
        EditorGUILayout.LabelField($"Состояние: {(selectedStaff.currentExecutor != null ? "Выполняет действие" : "Свободен")}");
        
        // Текущее действие
        if (selectedStaff.currentExecutor != null && selectedStaff.currentExecutor.actionData != null)
        {
            EditorGUILayout.LabelField($"Текущее: {selectedStaff.currentExecutor.actionData.displayName}", EditorStyles.helpBox);
        }

        EditorGUILayout.Space();

        // Brain dump данные - НОВАЯ ВЕРСИЯ
        DrawBrainDump();

        EditorGUILayout.Space();

        // Дополнительная информация
        EditorGUILayout.LabelField("--- Состояние ---", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"Энергия: {selectedStaff.energy:F1}");
        EditorGUILayout.LabelField($"Стресс: {selectedStaff.GetCurrentStress():F2}");
        EditorGUILayout.LabelField($"На перерыве: {selectedStaff.isOnBreak}");
    }

    private void DrawBrainDump()
    {
        EditorGUILayout.BeginVertical("box");
        GUILayout.Label("ОЦЕНКА ДЕЙСТВИЙ (UTILITY AI)", EditorStyles.boldLabel);

        if (selectedStaff.currentBrainDump == null || selectedStaff.currentBrainDump.Count == 0)
        {
            EditorGUILayout.HelpBox("Сотрудник еще не думал (или список пуст).", MessageType.Info);
            
            // Кнопка принудительного вызова AI
            if (GUILayout.Button("Принудительно вызвать TryPickAction"))
            {
                // Используем рефлексию для вызова protected метода
                var method = typeof(StaffController).GetMethod("TryPickAction",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                method?.Invoke(selectedStaff, null);
            }
            
            EditorGUILayout.EndVertical();
            return;
        }

        var sortedDump = selectedStaff.currentBrainDump
            .OrderByDescending(d => d.ConditionsMet)
            .ThenByDescending(d => d.Score)
            .ToList();

        actionsScrollPos = EditorGUILayout.BeginScrollView(actionsScrollPos, GUILayout.Height(300));

        // Шапка
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Действие", EditorStyles.boldLabel, GUILayout.Width(160));
        GUILayout.Label("Статус", EditorStyles.boldLabel, GUILayout.Width(130));
        GUILayout.Label("Вес", EditorStyles.boldLabel, GUILayout.Width(50));
        EditorGUILayout.EndHorizontal();

        foreach (var data in sortedDump)
        {
            EditorGUILayout.BeginHorizontal();
            
            // Если displayName пустое, берем имя файла ассета
            string displayName = string.IsNullOrEmpty(data.ActionName) ? $"[{data.AssetName}]" : data.ActionName;
            GUILayout.Label(displayName, GUILayout.Width(160));
            
            // Цветной статус (Градация!)
            Color statusColor = Color.gray;
            if (data.ConditionsMet) statusColor = Color.green;
            else if (!string.IsNullOrEmpty(data.StatusMessage) && data.StatusMessage.Contains("%")) statusColor = new Color(0.8f, 0.6f, 0.2f); // Оранжевый для "В процессе"
            else if (!string.IsNullOrEmpty(data.StatusMessage) && data.StatusMessage.Contains("КРИТИЧНО")) statusColor = Color.red;
            
            GUI.contentColor = statusColor;
            GUILayout.Label(data.StatusMessage, EditorStyles.boldLabel, GUILayout.Width(130));
            GUI.contentColor = Color.white;
            
            // Вес (если доступно)
            GUI.color = data.ConditionsMet ? Color.cyan : new Color(0.3f, 0.3f, 0.3f);
            string scoreText = data.ConditionsMet ? data.Score.ToString("F1") : "---";
            GUILayout.Label(scoreText, EditorStyles.boldLabel, GUILayout.Width(50));
            GUI.color = Color.white;
            
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private void Update()
    {
        // Обновляем данные в режиме игры
        if (EditorApplication.isPlaying)
        {
            Repaint();
        }
    }
}
