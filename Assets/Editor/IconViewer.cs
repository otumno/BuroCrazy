using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Text;

public class IconViewer : EditorWindow
{
    Vector2 scrollPos;
    List<string> iconNames;
    string searchFilter = "";

    [MenuItem("Window/Developer/Icon Viewer")]
    public static void ShowWindow() => GetWindow<IconViewer>("Icons");

    void OnEnable()
    {
        // Ищем все текстуры, которые являются системными иконками
        iconNames = new List<string>();
        Texture2D[] t = Resources.FindObjectsOfTypeAll<Texture2D>();
        
        foreach (var tex in t) 
        {
            // Фильтруем: имя не пустое И Unity может загрузить это как IconContent
            if (tex.name.Length > 0 && EditorGUIUtility.IconContent(tex.name) != null) 
            {
                // Исключаем совсем мусорные текстуры без имен
                if(tex.name != "Font Texture") 
                    iconNames.Add(tex.name);
            }
        }
        iconNames.Sort();
    }

    void OnGUI()
    {
        GUILayout.BeginHorizontal(EditorStyles.toolbar);
        
        // Кнопка экспорта
        if (GUILayout.Button("📋 COPY ALL NAMES TO CLIPBOARD", EditorStyles.toolbarButton, GUILayout.Width(200)))
        {
            CopyAllToClipboard();
        }
        
        GUILayout.FlexibleSpace();
        
        // Поиск
        GUILayout.Label("Search:", EditorStyles.toolbarButton);
        searchFilter = EditorGUILayout.TextField(searchFilter, EditorStyles.toolbarTextField, GUILayout.Width(200));
        
        GUILayout.EndHorizontal();

        // Отрисовка сетки
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
        float width = 50f;
        float screenWidth = position.width;
        int columns = Mathf.FloorToInt(screenWidth / width);
        if (columns < 1) columns = 1;

        int drawnCount = 0;
        
        // Используем Layout, но хитро, чтобы не рисовать то, что не подходит под фильтр
        List<string> filteredNames = new List<string>();
        if (string.IsNullOrEmpty(searchFilter))
        {
            filteredNames = iconNames;
        }
        else
        {
            string lowFilter = searchFilter.ToLower();
            foreach (var n in iconNames) if (n.ToLower().Contains(lowFilter)) filteredNames.Add(n);
        }

        int total = filteredNames.Count;
        int rows = Mathf.CeilToInt((float)total / columns);

        for (int r = 0; r < rows; r++)
        {
            EditorGUILayout.BeginHorizontal();
            for (int c = 0; c < columns; c++)
            {
                int index = r * columns + c;
                if (index >= total) break;

                var name = filteredNames[index];
                var content = EditorGUIUtility.IconContent(name);
                content.tooltip = name; // При наведении покажет имя

                if (GUILayout.Button(content, GUILayout.Width(width), GUILayout.Height(width)))
                {
                    EditorGUIUtility.systemCopyBuffer = name;
                    Debug.Log($"Copied: {name}");
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();
    }

    void CopyAllToClipboard()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== UNITY ICON DUMP ===");
        foreach (var name in iconNames)
        {
            sb.AppendLine(name);
        }
        GUIUtility.systemCopyBuffer = sb.ToString();
        Debug.Log($"<color=green>Скопировано {iconNames.Count} имен иконок в буфер обмена!</color>");
    }
}