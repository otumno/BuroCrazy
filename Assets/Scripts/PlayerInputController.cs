using UnityEngine;
using UnityEngine.EventSystems;
using System.Linq;

public class PlayerInputController : MonoBehaviour
{
    [Header("Настройки")]
    [Tooltip("Камера для расчета кликов")]
    public Camera mainCamera;
    [Tooltip("Маска слоев, на которые можно кликать для передвижения (например, 'Ground')")]
    public LayerMask movementLayerMask;
    [Tooltip("Префаб маркера, который появляется при клике")]
    public GameObject clickMarkerPrefab;

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }
            
            // <<< FIX: Проверяем, не занят ли Директор >>>
            if(DirectorAvatarController.Instance != null && DirectorAvatarController.Instance.IsInUninterruptibleAction)
            {
                Debug.Log("Нельзя отдать приказ: Директор выполняет важное действие.");
                return;
            }

            Vector2 clickWorldPosition = mainCamera.ScreenToWorldPoint(Input.mousePosition);
            
            Collider2D groundHit = Physics2D.OverlapPoint(clickWorldPosition, movementLayerMask);
            if (groundHit != null)
            {
                if (clickMarkerPrefab != null)
                {
                    Instantiate(clickMarkerPrefab, clickWorldPosition, Quaternion.identity);
                }
                
                Waypoint nearestWaypoint = FindNearestWaypointTo(clickWorldPosition);
                if (nearestWaypoint != null)
                {
                    DirectorAvatarController.Instance?.MoveToWaypoint(nearestWaypoint);
                }
            }
        }
    }

    private Waypoint FindNearestWaypointTo(Vector2 position)
    {
        var allWaypoints = FindObjectsByType<Waypoint>(FindObjectsSortMode.None);
        return allWaypoints
             .OrderBy(wp => Vector2.Distance(position, wp.transform.position))
            .FirstOrDefault();
    }
}