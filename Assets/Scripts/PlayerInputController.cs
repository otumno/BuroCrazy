// Файл: Assets/Scripts/PlayerInputController.cs
using UnityEngine;
using UnityEngine.EventSystems;
using System.Linq;
using System.Collections;

public class PlayerInputController : MonoBehaviour
{
    [Header("Настройки")]
    public Camera mainCamera;
    public LayerMask movementLayerMask;
    public GameObject clickMarkerPrefab;
	
	[Tooltip("Слой(и), на котором находятся коллайдеры персонала для взаимодействия (правый клик)")]
    public LayerMask staffInteractionLayerMask;

    // --- НАЧАЛО ИЗМЕНЕНИЙ ---
    [Header("Ссылки для взаимодействия")]
    [Tooltip("Перетащите сюда объект ActionConfigPopup из UI")]
    public ActionConfigPopupUI actionConfigPopup;
    // --- КОНЕЦ ИЗМЕНЕНИЙ ---

    void Awake()
    {
        // На случай, если забыли назначить в инспекторе
        if (actionConfigPopup == null)
        {
            actionConfigPopup = FindFirstObjectByType<ActionConfigPopupUI>(FindObjectsInactive.Include);
        }
    }

    void Update()
    {
        DirectorAvatarController director = DirectorAvatarController.Instance;
    AgentMover directorMover = director?.GetComponent<AgentMover>(); // Безопасно получаем AgentMover

    // Если директор существует, у него есть AgentMover и он сейчас скользит/лежит - игнорируем ввод
    if (directorMover != null && directorMover.IsSlipping)
    {
        return; // Выходим из Update, не обрабатывая клики
    }
		
		// --- ЛЕВЫЙ КЛИК (передвижение) ---
        if (Input.GetMouseButtonDown(0))
        {
            if (EventSystem.current.IsPointerOverGameObject()) return;
            if(DirectorAvatarController.Instance != null && DirectorAvatarController.Instance.IsInUninterruptibleAction) return;

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

        // --- ПРАВЫЙ КЛИК (взаимодействие) ---
        if (Input.GetMouseButtonDown(1))
        {
            if (EventSystem.current.IsPointerOverGameObject()) return;
            if(DirectorAvatarController.Instance != null && DirectorAvatarController.Instance.IsInUninterruptibleAction) return;
            
            RaycastHit2D hit = Physics2D.Raycast(
                mainCamera.ScreenToWorldPoint(Input.mousePosition),
                Vector2.zero,
                Mathf.Infinity, // Длина луча (не важна для Raycast с Vector2.zero)
                staffInteractionLayerMask // <<<< ИСПОЛЬЗУЕМ МАСКУ
            );
            if (hit.collider != null)
            {
                StaffController clickedStaff = hit.collider.GetComponentInParent<StaffController>();
                if (clickedStaff != null && !(clickedStaff is DirectorAvatarController))
                {
                    // Мы кликнули на сотрудника!
                    StartCoroutine(DirectorInteractRoutine(clickedStaff));
                }
            }
        }
    }

    // --- НОВАЯ КОРУТИНА ---
    private IEnumerator DirectorInteractRoutine(StaffController targetStaff)
    {
        DirectorAvatarController director = DirectorAvatarController.Instance;
        if (director == null || actionConfigPopup == null) yield break;

        // 1. Отправляем Директора к сотруднику
        // Находим ближайшую к сотруднику точку, чтобы встать рядом, а не в нем самом
        Waypoint targetWaypoint = FindNearestWaypointTo(targetStaff.transform.position); 
        if(targetWaypoint != null)
        {
            director.MoveToWaypoint(targetWaypoint);
        }

        // 2. Ждем, пока Директор дойдет
        yield return new WaitUntil(() => !director.AgentMover.IsMoving());

        // 3. Ставим игру на паузу и открываем UI
        Time.timeScale = 0f;
        actionConfigPopup.OpenForStaff(targetStaff);
    }

    private Waypoint FindNearestWaypointTo(Vector2 position)
    {
        var allWaypoints = FindObjectsByType<Waypoint>(FindObjectsSortMode.None);
        return allWaypoints
             .OrderBy(wp => Vector2.Distance(position, wp.transform.position))
            .FirstOrDefault();
    }
}