// Assets/Editor/HierarchyEnchancer.cs
using UnityEngine;
using UnityEditor;
using Managers;     // Ваши неймспейсы
using Utilities;
using Gameplay.Documents;
using UI.Map;

[InitializeOnLoad]
public class HierarchyEnchancer
{
    // --- ЦВЕТА ---
    private static readonly Color ColManager = new Color(1f, 0.2f, 0.2f, 0.1f);
    private static readonly Color ColUI = new Color(1f, 0.8f, 0.2f, 0.08f);
    private static readonly Color ColLogic = new Color(0.3f, 1f, 0.3f, 0.08f);
    private static readonly Color ColPrefab = new Color(0.3f, 0.5f, 1f, 0.05f);

    static HierarchyEnchancer()
    {
        EditorApplication.hierarchyWindowItemOnGUI -= OnHierarchyGUI;
        EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyGUI;
    }

    private static void OnHierarchyGUI(int instanceID, Rect selectionRect)
    {
        // Оптимизация: Рисуем только при событии Repaint, чтобы не ломать логику инспектора
        if (Event.current.type != EventType.Repaint) return;

        GameObject obj = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
        if (obj == null) return;

        // --- 1. ФОН (Быстрые проверки по имени или одному компоненту) ---
        
        bool isManager = obj.name.Contains("[SYSTEMS]") || obj.GetComponent<ProgressionManager>() != null;
        if (isManager)
        {
            DrawRect(selectionRect, ColManager);
        }
        else if (obj.GetComponent<Canvas>() != null || obj.name.EndsWith("Panel"))
        {
            DrawRect(selectionRect, ColUI);
        }
        else if (obj.GetComponent<ServicePoint>() != null || obj.GetComponent<LimitedCapacityZone>() != null)
        {
            DrawRect(selectionRect, ColLogic);
        }
        else if (PrefabUtility.IsPartOfAnyPrefab(obj))
        {
            DrawRect(selectionRect, ColPrefab);
        }

        // --- 2. ИКОНКИ (Точечные проверки) ---
        // Сдвигаем курсор рисования вправо
        float xPos = selectionRect.xMax - 18; 

        // Проверяем наличие ключевых компонентов и рисуем для них иконки.
        // Порядок важен (справа налево).
        
        // Персонажи / Логика
        CheckAndDraw(obj, typeof(StaffController), "CapsuleCollider2D Icon", ref xPos, selectionRect);
        CheckAndDraw(obj, typeof(ClientPathfinding), "CapsuleCollider2D Icon", ref xPos, selectionRect);
        
        // Объекты
        CheckAndDraw(obj, typeof(ServicePoint), "FilterByLabel", ref xPos, selectionRect); // Бирка
        CheckAndDraw(obj, typeof(ProjectDocumentObject), "Favorite", ref xPos, selectionRect); // Звезда
        CheckAndDraw(obj, typeof(DocumentStack), "TextAsset Icon", ref xPos, selectionRect); // Лист
        CheckAndDraw(obj, typeof(TrashCan), "TreeEditor.Trash", ref xPos, selectionRect); // Мусорка
        
        // Менеджеры
        if (isManager) DrawIcon(selectionRect, "GameManager Icon", ref xPos);
        
        // UI
        CheckAndDraw(obj, typeof(MapPanelUI), "World", ref xPos, selectionRect);
        CheckAndDraw(obj, typeof(TutorialMascot), "Help", ref xPos, selectionRect);
    }

    private static void CheckAndDraw(GameObject obj, System.Type type, string iconName, ref float xPos, Rect rect)
    {
        if (obj.GetComponent(type) != null)
        {
            DrawIcon(rect, iconName, ref xPos);
        }
    }

    private static void DrawRect(Rect rect, Color color)
    {
        Rect bgRect = new Rect(rect.x + 16, rect.y, rect.width + 50, rect.height);
        EditorGUI.DrawRect(bgRect, color);
    }

    private static void DrawIcon(Rect rect, string iconName, ref float xPos)
    {
        var icon = EditorGUIUtility.IconContent(iconName).image;
        if (icon != null)
        {
            Rect iconRect = new Rect(xPos, rect.y, 16, 16);
            
            // Полупрозрачность
            var c = GUI.color;
            GUI.color = new Color(1, 1, 1, 0.8f);
            GUI.DrawTexture(iconRect, icon);
            GUI.color = c;

            xPos -= 18; // Сдвиг для следующей иконки
        }
    }
}