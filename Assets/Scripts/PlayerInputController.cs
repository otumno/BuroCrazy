// Файл: PlayerInputController.cs
using UnityEngine;
using UnityEngine.EventSystems; // Для проверки кликов по UI
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
        // Проверяем нажатие левой кнопки мыши
        if (Input.GetMouseButtonDown(0))
        {
            // Игнорируем клики, если курсор над любым элементом UI
            if (EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            Vector2 clickWorldPosition = mainCamera.ScreenToWorldPoint(Input.mousePosition);
            RaycastHit2D hit = Physics2D.Raycast(clickWorldPosition, Vector2.zero);

            // 1. Проверяем, не кликнули ли мы на стопку документов
            if (hit.collider != null && hit.collider.GetComponent<DocumentStack>() is DocumentStack stack)
            {
                // Отдаем команду Директору забрать документы
                DirectorAvatarController.Instance?.CollectDocuments(stack);
                return; // Выходим, чтобы не было команды на передвижение
            }

            // 2. Если не попали в стопку, проверяем, кликнули ли мы на "землю"
            // Используем OverlapPoint, чтобы проверить, попал ли клик в коллайдер с нужным слоем
            Collider2D groundHit = Physics2D.OverlapPoint(clickWorldPosition, movementLayerMask);
            if (groundHit != null)
            {
					if (clickMarkerPrefab != null)
					{
						Instantiate(clickMarkerPrefab, clickWorldPosition, Quaternion.identity);
					}
				
				// Находим ближайшую к клику точку
                Waypoint nearestWaypoint = FindNearestWaypointTo(clickWorldPosition);
                if (nearestWaypoint != null)
                {
                    // Отдаем команду Директору идти туда
                    DirectorAvatarController.Instance?.MoveToWaypoint(nearestWaypoint);
                }
            }
        }
    }

private Waypoint FindNearestWaypointTo(Vector2 position)
{
    var allWaypoints = FindObjectsByType<Waypoint>(FindObjectsSortMode.None);
    // Теперь директор будет искать ближайшую точку среди ВООБЩЕ ВСЕХ,
    // что позволит вам кликать на его кабинет и архив.
    return allWaypoints
        .OrderBy(wp => Vector2.Distance(position, wp.transform.position))
        .FirstOrDefault();
}
}