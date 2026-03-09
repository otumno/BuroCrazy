// Файл: Assets/Scripts/PlayerInputController.cs
using UnityEngine;
using UnityEngine.EventSystems;
using System.Linq;
using System.Collections;
using Managers;
using UI;

public class PlayerInputController : MonoBehaviour
{
    // --- ДЕБАГ ФЛАГИ ---
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public static bool DebugForceBookkeeping = false; // Чит для открытия бухгалтерии
#endif
    // --------------------
    
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
        // --- ПРОБЕЛ (ПАУЗА) ---
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (MainUIManager.Instance != null && MusicPlayer.Instance != null)
            {
                bool isCurrentlyPaused = Time.timeScale == 0f;
                bool isBlockingUIOpen = MainUIManager.Instance.pauseCount > 0;
                bool isDirectorDeskOpen = StartOfDayPanel.Instance != null && StartOfDayPanel.Instance.gameObject.activeInHierarchy;

                if (!isBlockingUIOpen && !isDirectorDeskOpen)
                {
                    if (isCurrentlyPaused)
                    {
                        Debug.Log("[PlayerInputController] Снимаем ручную паузу");
                        MainUIManager.Instance.ResumeGame();
                        MusicPlayer.Instance.ResumeGameplayMusicFromManualPause();
                    }
                    else
                    {
                        Debug.Log("[PlayerInputController] Ставим ручную паузу");
                        Time.timeScale = 0f;
                        MusicPlayer.Instance.PauseGameplayMusicForManualPause();
                    }
                }
                else
                {
                    Debug.Log($"[PlayerInputController] Пробел игнорируется: UI открыт ({isBlockingUIOpen}) или Стол открыт ({isDirectorDeskOpen})");
                }
            }
        }

        DirectorAvatarController director = DirectorAvatarController.Instance;
    AgentMover directorMover = director?.GetComponent<AgentMover>(); // Безопасно получаем AgentMover

    // Если директор существует, у него есть AgentMover и он сейчас скользит/лежит - игнорируем ввод
    if (directorMover != null && directorMover.IsSlipping)
    {
        return; // Выходим из Update, не обрабатывая клики
    }

        // --- ЧИТ НА ДЕНЬГИ (КЛАВИША M) ---
        if (Input.GetKeyDown(KeyCode.M))
        {
            if (PlayerWallet.Instance != null)
            {
                PlayerWallet.Instance.AddMoney(10000, "Чит-код разработчика", IncomeType.Shadow);
                Debug.Log("<color=green>[ЧИТ]</color> Добавлено $10,000!");
            }
        }
        // ---------------------------------
        
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // --- ДЕБАГ: ОТКРЫТЬ БУХГАЛТЕРИЮ (КЛАВИША B) ---
        if (Input.GetKeyDown(KeyCode.B))
        {
            DebugForceBookkeeping = !DebugForceBookkeeping;
            Debug.Log($"<color=green>[ЧИТ]</color> Бухгалтерия принудительно {(DebugForceBookkeeping ? "ОТКРЫТА" : "ЗАКРЫТА")}!");

            // 1. Принудительно пинаем калькулятор на столе, чтобы он перепроверил статус
            var calc = FindFirstObjectByType<DeskCalculator>(FindObjectsInactive.Include);
            if (calc != null) calc.CheckAvailability();

            // 2. Принудительно ВКЛЮЧАЕМ кнопку UI, чтобы её Update() снова начал работать
            var bookBtn = FindFirstObjectByType<BookkeepingButtonController>(FindObjectsInactive.Include);
            if (bookBtn != null)
            {
                bookBtn.gameObject.SetActive(true);
            }
        }
        // ----------------------------------------------
#endif

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
        MainUIManager.Instance.PushPause();
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