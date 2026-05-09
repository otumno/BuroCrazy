using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class WaypointGizmo : MonoBehaviour
{
#if UNITY_EDITOR
    public static bool ShowGizmos = true;

    private void OnDrawGizmos()
    {
        if (!ShowGizmos) return;
        
        // Рисуем иконку-булавку и имя объекта
        Gizmos.DrawIcon(transform.position, "Waypoint.png", true);
        GUIStyle style = new GUIStyle();
        style.normal.textColor = Color.yellow;
        style.alignment = TextAnchor.MiddleCenter;
        Handles.Label(transform.position + Vector3.up * 0.5f, gameObject.name, style);
    }

    private void Start()
    {
        // В игре скрываем визуальный спрайт (если он есть) и коллайдер
        if (Application.isPlaying)
        {
            var sr = GetComponent<SpriteRenderer>();
            if (sr != null) sr.enabled = false;
            // если есть коллайдер, тоже отключаем (опционально)
            var col = GetComponent<Collider2D>();
            if (col != null) col.enabled = false;
        }
    }
#endif
}

#if UNITY_EDITOR
[InitializeOnLoad]
public class WaypointGizmoToggle
{
    static WaypointGizmoToggle()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }

    static void OnSceneGUI(SceneView sceneView)
    {
        Event e = Event.current;
        if (e != null && e.type == EventType.KeyDown && e.keyCode == KeyCode.W && e.control)
        {
            WaypointGizmo.ShowGizmos = !WaypointGizmo.ShowGizmos;
            e.Use();
            SceneView.RepaintAll();
            Debug.Log("Waypoint Gizmos: " + (WaypointGizmo.ShowGizmos ? "ON" : "OFF"));
        }
    }
}
#endif
