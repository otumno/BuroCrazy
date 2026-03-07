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
        EditorGUILayout.LabelField("--- Brain Dump (Последнее решение) ---", EditorStyles.boldLabel);

        // Brain dump данные
        if (selectedStaff.currentBrainDump != null && selectedStaff.currentBrainDump.Count > 0)
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(300));
            
            // Сортируем по убыванию score
            var sorted = selectedStaff.currentBrainDump.OrderByDescending(d => d.Score).ToList();
            
            foreach (var data in sorted)
            {
                EditorGUILayout.BeginHorizontal();
                
                // Цвет в зависимости от условий
                GUIStyle labelStyle = new GUIStyle(EditorStyles.label);
                if (!data.ConditionsMet)
                {
                    labelStyle.normal.textColor = Color.gray;
                    EditorGUILayout.LabelField($"❌ {data.ActionName}", labelStyle);
                }
                else if (data.Score <= 0)
                {
                    labelStyle.normal.textColor = Color.yellow;
                    EditorGUILayout.LabelField($"⚠️ {data.ActionName}", labelStyle);
                }
                else
                {
                    labelStyle.normal.textColor = Color.green;
                    EditorGUILayout.LabelField($"✅ {data.ActionName}", labelStyle);
                }
                
                EditorGUILayout.LabelField($"Score: {data.Score:F1}", GUILayout.Width(80));
                EditorGUILayout.LabelField($"Conditions: {(data.ConditionsMet ? "✓" : "✗")}", GUILayout.Width(80));
                
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUILayout.EndScrollView();
        }
        else
        {
            EditorGUILayout.LabelField("Нет данных (мозг пуст)", EditorStyles.helpBox);
            
            // Кнопка принудительного вызова AI
            if (GUILayout.Button("Принудительно вызвать TryPickAction"))
            {
                // Используем рефлексию для вызова protected метода
                var method = typeof(StaffController).GetMethod("TryPickAction", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                method?.Invoke(selectedStaff, null);
            }
        }

        EditorGUILayout.Space();

        // Дополнительная информация
        EditorGUILayout.LabelField("--- Состояние ---", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"Энергия: {selectedStaff.energy:F1}");
        EditorGUILayout.LabelField($"Стресс: {selectedStaff.GetCurrentStress():F2}");
        EditorGUILayout.LabelField($"На перерыве: {selectedStaff.isOnBreak}");
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
