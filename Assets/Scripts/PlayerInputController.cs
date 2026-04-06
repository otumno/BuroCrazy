using UnityEngine;
using UnityEngine.EventSystems;
using System.Linq;
using System.Collections;
using Managers;
using UI;
using Clinch;

public class PlayerInputController : MonoBehaviour
{
    // --- ДЕБАГ ФЛАГИ ---
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public static bool DebugForceBookkeeping = false;
#endif
    // --------------------
    
    [Header("Настройки")]
    public Camera mainCamera;
    public LayerMask movementLayerMask;
    public GameObject clickMarkerPrefab;
	
    [Tooltip("Слой(и), на котором находятся коллайдеры персонала для взаимодействия (правый клик)")]
    public LayerMask staffInteractionLayerMask;

    [Header("Ссылки для взаимодействия")]
    [Tooltip("Перетащите сюда объект ActionConfigPopup из UI")]
    public ActionConfigPopupUI actionConfigPopup;
    
    // Hover для клинчей
    private Clinch.ClinchTarget _currentHoveredClinch;

    // --- DIABLO MOVEMENT ---
    private bool _isHoldingMouse = false;
    private Waypoint _lastTargetWaypoint;
    // -----------------------

    void Awake()
    {
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
        AgentMover directorMover = director?.GetComponent<AgentMover>();

        // Если директор существует, у него есть AgentMover и он сейчас скользит/лежит - игнорируем ввод
        if (directorMover != null && directorMover.IsSlipping)
        {
            _isHoldingMouse = false; // Сбрасываем удержание при падении
            return; 
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
        
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // --- ДЕБАГ: ОТКРЫТЬ БУХГАЛТЕРИЮ (КЛАВИША B) ---
        if (Input.GetKeyDown(KeyCode.B))
        {
            DebugForceBookkeeping = !DebugForceBookkeeping;
            Debug.Log($"<color=green>[ЧИТ]</color> Бухгалтерия принудительно {(DebugForceBookkeeping ? "ОТКРЫТА" : "ЗАКРЫТА")}!");

            var calc = FindFirstObjectByType<DeskCalculator>(FindObjectsInactive.Include);
            if (calc != null) calc.CheckAvailability();

            var bookBtn = FindFirstObjectByType<BookkeepingButtonController>(FindObjectsInactive.Include);
            if (bookBtn != null)
            {
                bookBtn.gameObject.SetActive(true);
            }
        }
#endif

        // --- HOVER ДЛЯ КЛИНЧЕЙ ---
        if (!EventSystem.current.IsPointerOverGameObject())
        {
            Vector2 mousePos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
            Collider2D[] hoverHits = Physics2D.OverlapCircleAll(mousePos, 0.8f);
            Clinch.ClinchTarget foundClinch = null;

            foreach (var hit in hoverHits)
            {
                if (hit.isTrigger) continue;
                Clinch.ClinchTarget c = hit.GetComponentInParent<Clinch.ClinchTarget>();
                if (c != null && c.IsActive)
                {
                    foundClinch = c;
                    break;
                }
            }

            if (foundClinch != _currentHoveredClinch)
            {
                if (_currentHoveredClinch != null) _currentHoveredClinch.SetHover(false);
                _currentHoveredClinch = foundClinch;
                if (_currentHoveredClinch != null) _currentHoveredClinch.SetHover(true);
            }
        }
        else if (_currentHoveredClinch != null)
        {
            _currentHoveredClinch.SetHover(false);
            _currentHoveredClinch = null;
        }


        // ========================================================
        // --- ЛЕВЫЙ КЛИК (DIABLO-STYLE ДВИЖЕНИЕ ИЛИ КЛИНЧ) ---
        // ========================================================
        
        // 1. Первый клик (нажали кнопку)
        if (Input.GetMouseButtonDown(0))
        {
            if (EventSystem.current.IsPointerOverGameObject()) return;
            if (DirectorAvatarController.Instance != null && DirectorAvatarController.Instance.IsInUninterruptibleAction) return;

            Vector2 clickWorldPosition = mainCamera.ScreenToWorldPoint(Input.mousePosition);

            // Сначала проверяем клик по активному клинчу
            if (TryHandleClinchClick(clickWorldPosition))
            {
                _isHoldingMouse = false; // Клинч обработан, не переходим в режим бега
                return; 
            }

            // Если не кликнули по клинчу - активируем режим удержания
            _isHoldingMouse = true;

            // Спавним маркер клика (только один раз при нажатии)
            Collider2D groundHit = Physics2D.OverlapPoint(clickWorldPosition, movementLayerMask);
            if (groundHit != null && clickMarkerPrefab != null)
            {
                Instantiate(clickMarkerPrefab, clickWorldPosition, Quaternion.identity);
            }
        }

        // 2. Отпустили кнопку
        if (Input.GetMouseButtonUp(0))
        {
            _isHoldingMouse = false;
            _lastTargetWaypoint = null; // Сбрасываем цель, чтобы следующий клик туда же сработал
        }

        // 3. Удержание кнопки (Diablo-style)
        if (_isHoldingMouse && Input.GetMouseButton(0))
        {
            // Перестаем бежать за мышью, если навели на UI
            if (EventSystem.current.IsPointerOverGameObject()) return; 

            if (DirectorAvatarController.Instance != null && DirectorAvatarController.Instance.IsInUninterruptibleAction)
            {
                _isHoldingMouse = false;
                return;
            }

            Vector2 currentMousePos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
            Waypoint nearestWaypoint = FindNearestWaypointTo(currentMousePos);

            // Даем команду идти ТОЛЬКО если ближайший вейпоинт сменился. 
            // Это спасет от перестроения пути каждый кадр и диких дерганий!
            if (nearestWaypoint != null && nearestWaypoint != _lastTargetWaypoint)
            {
                _lastTargetWaypoint = nearestWaypoint;
                DirectorAvatarController.Instance?.MoveToWaypoint(nearestWaypoint);
            }
        }


        // --- ПРАВЫЙ КЛИК (взаимодействие с персоналом) ---
        if (Input.GetMouseButtonDown(1))
        {
            if (EventSystem.current.IsPointerOverGameObject()) return;
            if(DirectorAvatarController.Instance != null && DirectorAvatarController.Instance.IsInUninterruptibleAction) return;
            
            RaycastHit2D hit = Physics2D.Raycast(
                mainCamera.ScreenToWorldPoint(Input.mousePosition),
                Vector2.zero,
                Mathf.Infinity, 
                staffInteractionLayerMask 
            );
            
            if (hit.collider != null)
            {
                StaffController clickedStaff = hit.collider.GetComponentInParent<StaffController>();
                if (clickedStaff != null && !(clickedStaff is DirectorAvatarController))
                {
                    StartCoroutine(DirectorInteractRoutine(clickedStaff));
                }
            }
        }
    }

    private IEnumerator DirectorInteractRoutine(StaffController targetStaff)
    {
        DirectorAvatarController director = DirectorAvatarController.Instance;
        if (director == null || actionConfigPopup == null) yield break;

        Waypoint targetWaypoint = FindNearestWaypointTo(targetStaff.transform.position); 
        if(targetWaypoint != null)
        {
            director.MoveToWaypoint(targetWaypoint);
        }

        yield return new WaitUntil(() => !director.AgentMover.IsMoving());

        MainUIManager.Instance.PushPause();
        actionConfigPopup.OpenForStaff(targetStaff);
    }

    private bool TryHandleClinchClick(Vector2 clickWorldPosition)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(clickWorldPosition, 0.8f);
        foreach (Collider2D hit in hits)
        {
            if (hit.isTrigger) continue;
            Clinch.ClinchTarget clinchTarget = hit.GetComponentInParent<Clinch.ClinchTarget>();
            if (clinchTarget != null && clinchTarget.IsActive)
            {
                if (Managers.AudioManager.Instance != null) Managers.AudioManager.Instance.PlaySound(Scriptables.Audio.SoundID.UI_Click_Default, clinchTarget.transform.position);
                DirectorAvatarController.Instance?.GoAndResolveClinch(clinchTarget);
                return true;
            }
        }
        return false;
    }

    private Waypoint FindNearestWaypointTo(Vector2 position)
    {
        var allWaypoints = FindObjectsByType<Waypoint>(FindObjectsSortMode.None);
        return allWaypoints
             .OrderBy(wp => Vector2.Distance(position, wp.transform.position))
            .FirstOrDefault();
    }
}