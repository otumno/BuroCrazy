// Assets/Editor/HierarchyEnchancer.cs
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using System.Linq;
using System.Collections.Generic;
using Managers;
using Utilities;
using Gameplay.Documents;
using UI;
using UI.Map;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;
using Gameplay; // <--- ДОБАВЛЕНО: Чтобы видеть OfficeObjectDurability

[InitializeOnLoad]
public class HierarchyEnchancer
{
    // --- ЦВЕТА ---
    private static readonly Color ColManager = new Color(1f, 0.2f, 0.2f, 0.1f);
    private static readonly Color ColUI = new Color(1f, 0.8f, 0.2f, 0.08f);
    private static readonly Color ColLogic = new Color(0.3f, 1f, 0.3f, 0.08f);
    private static readonly Color ColPrefab = new Color(0.3f, 0.5f, 1f, 0.05f);
    private static readonly Color ColFolder = new Color(0.5f, 0.5f, 0.5f, 0.1f); 

    // --- ИКОНКИ ---
    private static readonly Dictionary<System.Type, string> ComponentIcons = new Dictionary<System.Type, string>()
    {
        // === 1. ПЕРСОНАЖИ ===
        { typeof(DirectorAvatarController), "d_account" }, 
        { typeof(StaffController), "d_account" },          
        { typeof(ClientPathfinding), "d_account" },        
        { typeof(GuardMovement), "Shield Icon" },             
        { typeof(ServiceWorkerController), "d_account" }, 

        // === 2. ПРЕДМЕТЫ И ПРОЧНОСТЬ ===
        { typeof(OfficeObjectDurability), "d_Rigidbody2D Icon" },   // Гиря (Разрушаемый объект)
        
        { typeof(ProjectDocumentObject), "d_Favorite Icon" },       
        { typeof(DocumentStack), "d_TextAsset Icon" },              
        { typeof(TrashCan), "d_Sprite Icon" },                      
        { typeof(Puddle), "d_ParticleSystem Icon" },                
        { typeof(SecurityBarrier), "LockIcon" },                    
        { typeof(DoorController), "d_scenevis_visible" },           

        // === 3. ЛОГИКА ===
        { typeof(ServicePoint), "d_FilterByLabel" },                
        { typeof(LimitedCapacityZone), "d_LightProbeGroup Icon" },  
        { typeof(WaitingZone), "d_LightProbeGroup Icon" },            
        { typeof(Waypoint), "d_ToolHandleLocal" },                  
        { typeof(RectZone), "d_BoxCollider2D Icon" },               

        // === 4. МЕНЕДЖЕРЫ ===
        { typeof(ProgressionManager), "d_Settings Icon" },          
        { typeof(TimeManager), "d_Settings Icon" },          
        { typeof(HiringManager), "d_Settings Icon" },          
        { typeof(PlayerWallet), "d_Settings Icon" },                    
        { typeof(AudioManager), "d_AudioSource Icon" },         
        { typeof(SaveLoadManager), "SaveAs" },                      
        { typeof(WaveManager), "d_Settings Icon" },           
        { typeof(DirectorManager), "d_Settings Icon" },          
        { typeof(LightingManager), "d_Light Icon" },                

        // === 5. UI ===
        { typeof(MapPanelUI), "d_SceneAsset Icon" },                
        { typeof(StartOfDayPanel), "d_SceneAsset Icon" },       
        { typeof(TutorialMascot), "d_Help" },                       

        // === 6. СТАНДАРТНЫЕ ===
        { typeof(Camera), "d_Camera Icon" },
        { typeof(Light), "d_Light Icon" },
        { typeof(Light2D), "d_Light Icon" },
        { typeof(AudioSource), "d_AudioSource Icon" },
        { typeof(Grid), "d_Grid Icon" },
        { typeof(Tilemap), "d_Tilemap Icon" },
        { typeof(SpriteRenderer), "d_SpriteRenderer Icon" },
        { typeof(Canvas), "d_Canvas Icon" },
        { typeof(Button), "d_Button Icon" },
        { typeof(Image), "d_Image Icon" },
        { typeof(Text), "d_Text Icon" },
        { typeof(TextMeshProUGUI), "d_Text Icon" }
    };

    static HierarchyEnchancer()
    {
        EditorApplication.hierarchyWindowItemOnGUI -= HandleHierarchyWindowItemOnGUI;
        EditorApplication.hierarchyWindowItemOnGUI += HandleHierarchyWindowItemOnGUI;
    }

    private static void HandleHierarchyWindowItemOnGUI(int instanceID, Rect selectionRect)
    {
        if (Event.current.type != EventType.Repaint) return;

        GameObject obj = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
        if (obj == null) return;

        // --- 0. РАЗДЕЛИТЕЛЬ ---
        if (obj.name.StartsWith("---"))
        {
            DrawRect(selectionRect, new Color(0.15f, 0.15f, 0.15f, 1f));
            var style = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.7f, 0.7f, 0.7f) } };
            EditorGUI.LabelField(selectionRect, obj.name.Replace("-", "").Trim(), style);
            return;
        }

        // --- 1. ФОН ---
        bool isManager = obj.name.Contains("[SYSTEMS]") || obj.GetComponent<ProgressionManager>() != null;
        
        // ЛОГИКА ПАПКИ: Только если нет других компонентов (кроме Transform)
        bool isFolder = !isManager && obj.transform.childCount > 0 && obj.GetComponents<Component>().Length == 1;

        if (isManager) DrawRect(selectionRect, ColManager);
        else if (obj.GetComponent<Canvas>() != null || obj.name.EndsWith("Panel")) DrawRect(selectionRect, ColUI);
        else if (obj.GetComponent<ServicePoint>() != null || obj.GetComponent<LimitedCapacityZone>() != null) DrawRect(selectionRect, ColLogic);
        else if (isFolder) DrawRect(selectionRect, ColFolder);
        else if (PrefabUtility.IsPartOfAnyPrefab(obj)) DrawRect(selectionRect, ColPrefab);

        // --- 2. ИКОНКИ ---
        float xPos = selectionRect.xMax - 18; 
        var components = obj.GetComponents<Component>();
        HashSet<string> drawnIcons = new HashSet<string>();

        // Папка
        if (isFolder) DrawIconSafe(selectionRect, "d_Folder Icon", ref xPos, drawnIcons);

        foreach (var comp in components)
        {
            if (comp == null) continue;
            System.Type type = comp.GetType();

            // Авто-шестеренка для менеджеров
            if (type.Name.Contains("Manager") && !drawnIcons.Contains("d_Settings Icon"))
            {
                DrawIconSafe(selectionRect, "d_Settings Icon", ref xPos, drawnIcons);
            }

            // Поиск по словарю
            if (ComponentIcons.TryGetValue(type, out string iconName) || 
                ComponentIcons.Any(x => x.Key.IsAssignableFrom(type) && (iconName = x.Value) != null))
            {
                DrawIconSafe(selectionRect, iconName, ref xPos, drawnIcons);
            }
        }
    }

    private static void DrawRect(Rect rect, Color color)
    {
        Rect bgRect = new Rect(rect.x + 16, rect.y, rect.width + 50, rect.height);
        EditorGUI.DrawRect(bgRect, color);
    }

    private static void DrawIconSafe(Rect rect, string iconName, ref float xPos, HashSet<string> drawnCache)
    {
        if (drawnCache.Contains(iconName)) return;

        GUIContent content = null;
        try {
            content = EditorGUIUtility.IconContent(iconName);
        } catch { return; } 

        if (content != null && content.image != null)
        {
            Rect iconRect = new Rect(xPos, rect.y, 16, 16);
            
            var c = GUI.color;
            GUI.color = new Color(1, 1, 1, 0.85f);
            GUI.DrawTexture(iconRect, content.image);
            GUI.color = c;

            xPos -= 18;
            drawnCache.Add(iconName);
        }
    }
}