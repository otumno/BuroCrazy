// Файл: Assets/Scripts/Characters/Controllers/DirectorAvatarController.cs
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Managers;
using Utilities;
using Characters;
using UI.Bookkeeping;
using StaffOrders;

[RequireComponent(typeof(AgentMover), typeof(CharacterVisuals), typeof(ThoughtBubbleController))]
public class DirectorAvatarController : StaffController, IServiceProvider
{
    #region Fields and Properties
    public static DirectorAvatarController Instance { get; private set; }

    public enum DirectorState { Idle, MovingToPoint, AtDesk, CarryingDocuments, GoingForDocuments, WorkingAtStation, ServingClient }

    [Header("Ссылки")]
    private StackHolder stackHolder;
    [Header("Настройки Кабинета")]
    public Transform directorChairPoint;

    [Header("Префабы документов (для анимации)")]
    public GameObject form1Prefab;
    public GameObject form2Prefab;
    public GameObject certificate1Prefab;
    public GameObject certificate2Prefab;
	
	[Header("Свет")]
    public GameObject nightLight; // Фонарик для директора
	
	[Header("UI Эффекты")]
	public GameObject processingIconPrefab;
	
	public EmotionSpriteCollection spriteCollection;
	public StateEmotionMap stateEmotionMap;

    private DirectorState currentState = DirectorState.Idle;
    private ServicePoint currentWorkstation;
    private Coroutine workCoroutine;
    private bool isManuallyWorking = false;
    private DocumentStackTransfer documentTransfer;
    private ArchiveManager deliveryArchive;
    private Transform deliveryPoint;
    private System.Action manualServiceCleanup;

    private ClientPathfinding clientBeingServed = null;
    public ClientPathfinding ClientBeingServed => clientBeingServed;
    public ServicePoint CurrentWorkstation => currentWorkstation;
    // Флаг, указывающий, занят ли директор действием, которое нельзя прервать (например, падением)
    public bool IsInUninterruptibleAction { get; private set; } = false;
    // Флаг, указывающий, находится ли директор физически у своего стола
    public bool IsAtDesk { get; private set; } = false;
    
    #endregion

    #region Unity Methods
    protected override void Awake()
    {
        base.Awake(); // Вызываем Awake базового класса StaffController
        // Устанавливаем Singleton Instance
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Debug.LogWarning($"[DirectorAvatarController] Уничтожен дубликат на {gameObject.name}");
            Destroy(gameObject); // Уничтожаем дубликат
            return;
        }
        stackHolder = GetComponent<StackHolder>(); // Получаем компонент для отображения стопки документов
    }

    // === СИСТЕМА МИКРОМЕНЕДЖМЕНТА ===
    /// <summary>
    /// Отдать приказ сотруднику с настройками по умолчанию (используется окном тактики).
    /// </summary>
    public void GiveOrder(StaffController target, StaffAction action)
    {
        if (action == null) return;

        GiveOrder(target, new StaffOrder(action));
    }

    /// <summary>
    /// Отдать приказ сотруднику. Приказ действует сразу, издалека: идти к сотруднику не нужно.
    /// </summary>
    public void GiveOrder(StaffController target, StaffOrder order)
    {
        if (target == null || order == null || order.Action == null) return;

        // Показываем реакцию директора
        thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("director.order_now"), 2f, Color.red);
        
        // Проигрываем звук (защита от NullReference)
        if (AudioManager.Instance != null)
        {
            // TODO: Заменить на крик директора
            // AudioManager.Instance.PlaySound(...);
        }
        
        // Отдаём приказ сотруднику
        target.ReceiveOrder(order);

        Debug.Log($"[MicroManagement] Директор приказал {target.characterName} выполнить: {order.Action.displayName}");
    }

    /// <summary>
    /// Директор не показывает визуальный фидбек (+/-) над собой.
    /// </summary>
    public override void ShowActionEffect(bool success)
    {
        // Пустой метод: Директор — игрок, не показываем +/- над ним
    }

    void Start()
    {
        // Инициализация навыков по умолчанию, если они не назначены
        if (skills == null)
        {
            skills = ScriptableObject.CreateInstance<CharacterSkills>();
            skills.paperworkMastery = 0.8f;
            skills.sedentaryResilience = 0.5f;
            skills.pedantry = 0.9f;
            skills.softSkills = 0.7f;
            skills.corruption = 0.1f;
             Debug.LogWarning($"[DirectorAvatarController] Навыки (Skills) не были назначены. Созданы значения по умолчанию.");
        }

        // Настройка внешнего вида, если все компоненты доступны
        if (visuals != null && spriteCollection != null && stateEmotionMap != null)
        {
            visuals.Setup(gender, spriteCollection, stateEmotionMap);
        }
         else {
             Debug.LogWarning($"[DirectorAvatarController] Не удалось настроить visuals: visuals={visuals != null}, spriteCollection={spriteCollection != null}, stateEmotionMap={stateEmotionMap != null}");
         }
    }

    /// <summary>
    /// Переназначает пол (и опционально коллекцию спрайтов) директора и перерисовывает внешний вид.
    /// Вызывается при старте новой игры (выбор из книги создания директора) и при загрузке сейва (SaveData.gender).
    /// Повторяет путь настройки из Start(), т.к. на момент вызова визуал уже отрисован полом по умолчанию.
    /// </summary>
    public void ApplyAppearance(Enums.Gender newGender, EmotionSpriteCollection newCollection = null)
    {
        gender = newGender;
        if (newCollection != null) spriteCollection = newCollection;

        if (visuals != null && spriteCollection != null && stateEmotionMap != null)
        {
            visuals.Setup(gender, spriteCollection, stateEmotionMap);
        }
    }

    void Update()
    {
        // Проверка нахождения у стола директора
        if (directorChairPoint != null)
        {
            bool currentlyAtDesk = Vector2.Distance(transform.position, directorChairPoint.position) < 0.5f;
            if (currentlyAtDesk && !IsAtDesk) // Если подошли к столу
            {
                IsAtDesk = true;
                // Устанавливаем состояние "За столом", только если не заняты другим важным делом
                if (currentState != DirectorState.WorkingAtStation && currentState != DirectorState.ServingClient &&
                    currentState != DirectorState.CarryingDocuments && currentState != DirectorState.GoingForDocuments)
                {
                     SetState(DirectorState.AtDesk);
                }

                // Ачивка: впервые сел за директорский стол
                Managers.AchievementManager.Instance?.UnlockAchievement("Achv_DirectorStory");
            }
            else if (!currentlyAtDesk && IsAtDesk) // Если отошли от стола
            {
                IsAtDesk = false;
                // Если текущее состояние было "За столом", меняем на "Бездействие"
                if (currentState == DirectorState.AtDesk)
                {
                    SetState(DirectorState.Idle);
                }
            }
        }
    }
    #endregion

    // Директор не ходит на перерывы по расписанию
    public override bool IsOnBreak() => false;

    #region Public Methods
    /// <summary>
    /// Отправляет Директора к указанной путевой точке.
    /// Прерывает текущую ручную работу, если она была.
    /// </summary>
    public void MoveToWaypoint(Waypoint targetWaypoint)
    {
         if (targetWaypoint == null) {
             Debug.LogError("MoveToWaypoint: targetWaypoint is null!");
             return;
         }
        // Если директор вручную работает на станции или обслуживает клиента, останавливаем это
        if (currentState == DirectorState.WorkingAtStation || currentState == DirectorState.ServingClient)
         {
            StopManualWork(false); // Останавливаем работу, но не отходим от стола автоматически
        }
        StopAllCoroutines(); // Прерываем все текущие корутины (включая предыдущее движение)
        StartCoroutine(MoveToTargetAndSetState(targetWaypoint.transform.position, DirectorState.Idle)); // Запускаем движение к новой цели
    }

    /// <summary>
    /// Централизованный механизм перемещения директора с блокировкой управления.
    /// Используется туториалом для перемещений, которые блокируют игрока до прибытия.
    /// </summary>
    /// <param name="targetWaypoint">Целевая путевая точка</param>
    /// <param name="stateAfterArrival">Состояние после прибытия (по умолчанию Idle)</param>
    /// <returns>Coroutine</returns>
    public IEnumerator MoveToAndLock(Waypoint targetWaypoint, DirectorState stateAfterArrival = DirectorState.Idle)
    {
        if (targetWaypoint == null)
        {
            Debug.LogError("[DirectorAvatarController] MoveToAndLock: targetWaypoint is null!");
            yield break;
        }
        
        Debug.Log($"[DirectorAvatarController] MoveToAndLock: Начало движения к {targetWaypoint.name}");
        
        // Блокируем управление
        PlayerInputController inputController = FindFirstObjectByType<PlayerInputController>();
        if (inputController != null)
        {
            inputController.IsCutscenePlaying = true;
        }
        
        // Если директор работает на станции или обслуживает клиента, останавливаем
        if (currentState == DirectorState.WorkingAtStation || currentState == DirectorState.ServingClient)
        {
            StopManualWork(false);
        }
        
        // Останавливаем все текущие корутины движения
        StopAllCoroutines();
        
        // Запускаем движение
        yield return MoveToTargetAndSetState(targetWaypoint.transform.position, stateAfterArrival);
        
        // Разблокируем управление
        if (inputController != null)
        {
            inputController.IsCutscenePlaying = false;
        }
        
        Debug.Log($"[DirectorAvatarController] MoveToAndLock: Завершено, состояние={currentState}");
    }

     /// <summary>
     /// Новая корутина-обертка для установки состояния ПОСЛЕ движения.
    /// </summary>
    private IEnumerator MoveToTargetAndSetState(Vector2 targetPosition, DirectorState stateAfterArrival)
    {
        yield return MoveToTargetRoutine(targetPosition); // Ждем завершения движения
        SetState(stateAfterArrival); // Устанавливаем состояние после
    }

    /// <summary>
    /// Назначает Директора на работу за указанным столом (ServicePoint).
    /// </summary>
    public void StartWorkingAt(ServicePoint workstation)
    {
         if (workstation == null) {
              Debug.LogError("StartWorkingAt: workstation is null!");
              return;
         }
        // Если уже работаем вручную
        if (isManuallyWorking)
        {
            // Если пытаемся начать работу на том же месте, ничего не делаем
            if (currentWorkstation == workstation) return;
            // Если на другом месте - останавливаем предыдущую работу
            StopManualWork(false);
        }
        StopAllCoroutines(); // Останавливаем другие корутины (например, движение)
        workCoroutine = StartCoroutine(WorkAtStationRoutine(workstation)); // Запускаем корутину работы на станции
    }

    /// <summary>
    /// Останавливает ручную работу Директора за столом.
    /// </summary>
    /// <param name="moveAway">Если true, Директор немного отойдет от стола после остановки.</param>
    public void StopManualWork(bool moveAway = true)
    {
        CleanupManualService();
        if (!isManuallyWorking) return; // Если не работали вручную, выходим

        // Останавливаем корутину работы
        if(workCoroutine != null)
        {
            StopCoroutine(workCoroutine);
            workCoroutine = null;
        }
        isManuallyWorking = false; // Снимаем флаг ручной работы
        clientBeingServed = null; // Сбрасываем обслуживаемого клиента

        // Если было назначено рабочее место
        if(currentWorkstation != null)
        {
            var interruptedClient = currentWorkstation.CurrentClient;
            interruptedClient?.stateMachine?.CancelServiceCall(this);
            currentWorkstation.ClearClient(interruptedClient);
            // Снимаем Директора с этого стола в ClientSpawner
            ClientSpawner.UnassignServiceProviderFromDesk(currentWorkstation.deskId);
             Debug.Log($"[DirectorController] {characterName} снят с рабочего места {currentWorkstation.name} (ID: {currentWorkstation.deskId}).");
            // Если нужно отойти от стола
            if (moveAway && currentWorkstation.clerkStandPoint != null) // Добавлена проверка на null
            {
                // Запускаем корутину движения на небольшое расстояние от стола
                StartCoroutine(MoveToTargetAndSetState((Vector2)currentWorkstation.clerkStandPoint.position + Vector2.down * 0.5f, DirectorState.Idle));
            }
            currentWorkstation = null; // Сбрасываем ссылку на рабочее место
        }
        SetState(DirectorState.Idle); // Устанавливаем состояние "Бездействие"
    }

    /// <summary>
    /// Возвращает текущее состояние Директора.
    /// </summary>
    public DirectorState GetCurrentState() => currentState;

     /// <summary>
     /// Устанавливает или снимает флаг не прерываемого действия (например, для падения).
     /// </summary>
     public void SetUninterruptible(bool isUninterruptible)
     {
         IsInUninterruptibleAction = isUninterruptible;
         // Debug.Log($"[DirectorController] IsInUninterruptibleAction установлен в: {isUninterruptible}"); // Optional log
     }

    /// <summary>
    /// Отправляет Директора к барьеру для взаимодействия.
    /// </summary>
    public void GoAndOperateBarrier()
    {
        // Не выполняем, если Директор занят чем-то непрерываемым
        if (IsInUninterruptibleAction)
        {
            thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("director.busy"), 2f, Color.yellow);
            return;
        }
        StopManualWork(false);
        CleanupDocumentDelivery();
        StopAllCoroutines(); // Останавливаем текущие действия
        StartCoroutine(OperateBarrierRoutine()); // Запускаем корутину барьера
    }

    /// <summary>
    /// Отправляет Директора забрать документы из указанной стопки и отнести в архив.
    /// </summary>
    public void CollectDocuments(DocumentStack stack)
    {
         if (stack == null) {
              Debug.LogError("CollectDocuments: stack is null!");
              return;
         }
        // Не выполняем, если Директор не свободен (не Idle и не AtDesk) или стопка пуста
        if ((currentState != DirectorState.Idle && currentState != DirectorState.AtDesk) || stack.IsEmpty)
         {
             string reason = "";
             if (currentState != DirectorState.Idle && currentState != DirectorState.AtDesk) reason = $"Неподходящее состояние ({currentState})";
             else if (stack.IsEmpty) reason = "Стопка пуста";
              Debug.Log($"[DirectorController] Невозможно собрать документы из {stack.name}. Причина: {reason}");
            return;
        }
        StopAllCoroutines(); // Останавливаем текущие действия
        StartCoroutine(CollectAndDeliverRoutine(stack)); // Запускаем корутину сбора
    }
	
	public void GoToNoticeBoard(Gameplay.NoticeBoard board)
{
    if (board == null || board.interactionPoint == null) return;
    
    // Используем MoveToTargetAndSetState (который мы добавили ранее)
    // Но нам нужен кастомный коллбек после прибытия.
    // Поэтому запускаем корутину вручную.
    StartCoroutine(GoToBoardRoutine(board));
}

private IEnumerator GoToBoardRoutine(Gameplay.NoticeBoard board)
{
    SetUninterruptible(true);
    SetState(DirectorState.MovingToPoint);
    
    yield return MoveToTargetRoutine(board.interactionPoint.position);
    
    SetState(DirectorState.Idle);
    SetUninterruptible(false);
    
    // Открываем UI
    board.OpenUI();
}

    /// <summary>
    /// Отправляет Директора к его основному рабочему столу (креслу).
    /// </summary>
    public void GoToDesk()
    {
        if (directorChairPoint == null)
        {
            Debug.LogError("DirectorChairPoint не назначен! Невозможно отправить директора к столу.");
            return;
        }

        var targetWaypoint = FindNearestWaypointTo(directorChairPoint.position);
        if (targetWaypoint == null)
        {
            Debug.LogError("Не найдена путевая точка рядом с directorChairPoint!");
            return;
        }

        // Директор уже идёт к столу, не перестраиваем маршрут снова.
        if (currentState == DirectorState.MovingToPoint && agentMover?.DestinationWaypoint == targetWaypoint)
        {
            Debug.Log("Директор уже идёт к столу, не перестраиваем маршрут снова");
            return;
        }

        StartCoroutine(GoToDeskRoutine(targetWaypoint));
    }

    private IEnumerator GoToDeskRoutine(Waypoint wp)
    {
        SetUninterruptible(true);
        SetState(DirectorState.MovingToPoint);
        
        yield return MoveToTargetRoutine(wp.transform.position);
        
        SetState(DirectorState.AtDesk);
        SetUninterruptible(false);

        // Открываем UI стола директора
        var deskPanel = FindFirstObjectByType<StartOfDayPanel>(FindObjectsInactive.Include);
        if (deskPanel != null)
        {
            MainUIManager.Instance.PushPause();
            
            // Используем UIWindowAnimator для анимации
            var animator = deskPanel.GetComponent<UIWindowAnimator>();
            if (animator != null)
            {
                animator.Open();
            }
            else
            {
                // Фоллбэк если нет аниматора
                deskPanel.gameObject.SetActive(true);
                var cg = deskPanel.GetComponent<CanvasGroup>();
                if (cg != null)
                {
                    cg.alpha = 1f;
                    cg.interactable = true;
                    cg.blocksRaycasts = true;
                }
            }
            
            if (MusicPlayer.Instance != null) MusicPlayer.Instance.PlayDirectorsOfficeTheme();
        }
    }

    /// <summary>
    /// Мгновенно перемещает Директора в указанную позицию.
    /// </summary>
    public void TeleportTo(Vector3 position)
    {
        transform.position = position;
        // Может потребоваться сброс пути в AgentMover, если он двигался
        agentMover?.Stop();
        // Можно добавить обновление IsAtDesk здесь, если телепорт к столу
        // ForceSetAtDeskState(Vector2.Distance(position, directorChairPoint.position) < 0.5f);
    }

    /// <summary>Ends the previous day's actions before returning to the director's desk.</summary>
    public void ReturnToDeskAfterDayEnd()
    {
        if (directorChairPoint == null) return;
        StopManualWork(false);
        // Stopping the mover alone lets old arrival coroutines resume at the new position.
        PrepareForRoleChange();
        CleanupDocumentDelivery();
        workCoroutine = null;
        SetUninterruptible(agentMover != null && agentMover.IsSlipping);
        SetState(DirectorState.Idle);
        TeleportTo(directorChairPoint.position);
        var body = GetComponent<Rigidbody2D>();
        if (body != null)
        {
            body.position = directorChairPoint.position;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }
        ForceSetAtDeskState(true);
        Debug.Log("[EndOfDaySequence] Директор вернулся к столу; прежние действия отменены.");
    }

    private void CleanupDocumentDelivery()
    {
        documentTransfer?.Dispose();
        documentTransfer = null;
        if (deliveryArchive != null && deliveryPoint != null) deliveryArchive.FreeOverflowPoint(deliveryPoint);
        deliveryArchive = null;
        deliveryPoint = null;
        stackHolder?.HideStack();
    }

    private void CleanupManualService()
    {
        var cleanup = manualServiceCleanup;
        manualServiceCleanup = null;
        cleanup?.Invoke();
    }

    private void OnDisable()
    {
        StopManualWork(false);
        CleanupDocumentDelivery();
    }


    /// <summary>
    /// Принудительно устанавливает флаг IsAtDesk и соответствующее состояние.
    /// Используется, например, после телепортации к столу.
    /// </summary>
    public void ForceSetAtDeskState(bool atDesk)
    {
        IsAtDesk = atDesk;
        if (atDesk)
        {
           // Устанавливаем AtDesk, только если не заняты важной работой
            if (currentState != DirectorState.WorkingAtStation && currentState != DirectorState.ServingClient &&
                currentState != DirectorState.CarryingDocuments && currentState != DirectorState.GoingForDocuments)
            {
                 SetState(DirectorState.AtDesk);
            }
        }
        else
        {
            // Если принудительно убираем со стола и были в состоянии AtDesk, переходим в Idle
            if (currentState == DirectorState.AtDesk)
            {
                SetState(DirectorState.Idle);
            }
        }
    }

    public IEnumerator GoAndViewBookkeeping(Vector2 targetPosition, BookkeepingPanelUI bookkeepingPanelToShow)
{
    // 1. Проверка на непрерываемое действие (падение и т.д.)
    if (IsInUninterruptibleAction)
    {
         thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("director.busy_now"), 2f, Color.yellow);
         Debug.Log("[Director] Не могу идти к бухгалтерии, занят непрерываемым действием.");
         yield break;
    }

    // 2. Прерываем ТЕКУЩУЮ РУЧНУЮ РАБОТУ, если она была.
    // НЕ вызываем StopAllCoroutines() здесь, чтобы не прервать саму себя.
    if (currentState == DirectorState.WorkingAtStation || currentState == DirectorState.ServingClient)
    {
        Debug.Log("[Director] Прерываю ручную работу...");
        StopManualWork(false); // Останавливаем работу, но не отходим
        // Ждем один кадр, чтобы StopManualWork успел отработать, если он запускает корутины
        yield return null;
    }

    Debug.Log($"[Director] Получена команда идти к столу бухгалтерии ({targetPosition}).");

    // 3. Запускаем движение
    Debug.Log("[Director] Начинаю движение к столу бухгалтерии...");
    yield return MoveToTargetRoutine(targetPosition); // Ждем завершения движения

    // 4. Устанавливаем состояние Idle ПОСЛЕ движения
    SetState(DirectorState.Idle);
    Debug.Log("[Director] Прибыл к столу бухгалтерии и установил состояние Idle.");

    // 5. Открываем панель
    if (bookkeepingPanelToShow != null)
    {
        Debug.Log("[Director] Вызов bookkeepingPanelToShow.Show()...");
        bookkeepingPanelToShow.Show();
    }
    else
    {
        Debug.LogError("Панель бухгалтерии (BookkeepingPanelUI) не была передана в корутину GoAndViewBookkeeping!");
    }
}
    #endregion

    #region Private Coroutines & Methods
    /// <summary>
    /// Корутина для работы Директора на назначенной станции (ServicePoint).
    /// </summary>
    /// <summary>
    /// Корутина для работы Директора на назначенной станции (ServicePoint).
    /// </summary>
    private IEnumerator WorkAtStationRoutine(ServicePoint workstation)
    {
         if (workstation == null) {
              Debug.LogError("WorkAtStationRoutine: workstation is null!");
              isManuallyWorking = false;
              yield break;
         }

        currentWorkstation = workstation;
        
        // --- ИЗНОС: Проверка перед началом ---
        var durability = workstation.GetComponent<Gameplay.OfficeObjectDurability>();
        if (durability != null && !durability.IsUsable())
        {
            thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("director.broken_station"), 3f, Color.red);
            isManuallyWorking = false;
            yield break;
        }
        // ------------------------------------

        ClientSpawner.AssignServiceProviderToDesk(this, workstation.deskId);
        isManuallyWorking = true; 
        Debug.Log($"[DirectorController] {characterName} назначен на {workstation.name}.");

        if (workstation.clerkStandPoint != null) {
            yield return MoveToTargetRoutine(workstation.clerkStandPoint.position); 
        } else {
             StopManualWork(false);
             yield break;
        }

        SetState(DirectorState.WorkingAtStation);

        while (isManuallyWorking) 
        {
            // --- ИЗНОС: Периодическая проверка и Эффективность ---
            if (durability != null && !durability.IsUsable())
            {
                thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("director.desk_broke"), 2f, Color.red);
                StopManualWork(true); // Автоматически встаем и отходим
                yield break;
            }
            float efficiency = (durability != null) ? durability.GetEfficiencyMultiplier() : 1.0f;
            // -----------------------------------------------------

            // --- ЛОГИКА АРХИВАРИУСА (deskId 3) ---
            if (workstation.deskId == 3)
            {
                if (ArchiveRequestManager.Instance != null && ArchiveRequestManager.Instance.HasPendingRequests())
                {
                    yield return DirectorArchiveRetrieveRoutine();
                    continue;
                }
                else if (ArchiveManager.Instance != null && ArchiveManager.Instance.GetStackToProcess() != null)
                {
                    // Архивируем документы со стола
                    yield return DirectorArchiveStoreRoutine();
                    continue;
                }
            }
            // -----------------------------------------------------

            // Registration and walk-in desks share reservation, arrival and cancellation handling.
            {
                ClientPathfinding clientToServe = workstation.CurrentClient;

                // 1. ЧИСТАЯ PULL-МОДЕЛЬ ДЛЯ РЕГИСТРАТУРЫ (Берем из очереди)
                if (clientToServe == null && workstation.deskId == 0 && ClientQueueManager.Instance != null && ClientQueueManager.Instance.queue.Count > 0)
                {
                    var nextInQueue = ClientQueueManager.Instance.queue
                        .Where(c => ClientQueueManager.Instance.CanCallForRegistration(c.Key, c.Value))
                        .OrderBy(kvp => kvp.Value)
                        .FirstOrDefault();

                    if (nextInQueue.Key != null)
                    {
                        clientToServe = nextInQueue.Key;
                        int ticketNum = nextInQueue.Value;
                        if (!clientToServe.stateMachine.TryGetCalledToSpecificDesk(workstation.clientStandPoint, ticketNum, this))
                        {
                            yield return new WaitForSeconds(0.5f);
                            continue;
                        }
                        
                        ClientQueueManager.Instance.currentlyCalledNumbers.Add(ticketNum);
                        ClientQueueManager.Instance.clientsAwaitingResponse[ticketNum] = Time.time;
                        

                        string callMsg = ticketNum > 10000 ? UI.GameText.Get("staff.registration.next") : UI.GameText.Get("staff.registration.call_ticket", ticketNum);
                        thoughtBubble?.ShowPriorityMessage(callMsg, 3f, Color.green);
                        if (ClientQueueManager.Instance.nextClientSound != null)
                            Managers.AudioManager.Instance?.PlayAudioClip2D(ClientQueueManager.Instance.nextClientSound);

                    }
                }

                // 2. БРОНЕБОЙНАЯ ТЯГА (Ищем неприкаянных клиентов в зоне стола)
                if (clientToServe == null)
                {
                    var zone = ClientSpawner.GetZoneByDeskId(workstation.deskId);
                    if (zone != null)
                    {
                        clientToServe = zone.GetOccupyingClients().FirstOrDefault(c =>
                            c != null && c.CanContinueService && c.stateMachine.GetCurrentGoal() == workstation.clientStandPoint &&
                            (c.stateMachine.AssignedServicePoint == null || c.stateMachine.AssignedServicePoint == workstation) &&
                            c.stateMachine.MyServiceProvider == null);
                            
                        if (clientToServe != null)
                        {
                            thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("staff.registration.walk_in"), 2f, Color.green);
                            Waypoint wp = workstation.clientStandPoint != null ? workstation.clientStandPoint : workstation.GetComponentInChildren<Waypoint>();
                            if (!clientToServe.stateMachine.TryGetCalledToSpecificDesk(wp, clientToServe.stateMachine.MyQueueNumber, this)) clientToServe = null;
                        }
                    }
                }

                // 3. ЛОГИКА НАГЛЕЦОВ (QUEUE JUMPERS)
                if (clientToServe != null && clientToServe.isQueueJumper)
                {
                    float currentSoftSkills = (skills != null) ? skills.softSkills : 0.5f;
                    float currentCorruption = (skills != null) ? skills.corruption : 0.0f;
                    
                    if (Random.value < currentSoftSkills)
                    {
                        thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("staff.registration.join_queue"), 3f, Color.red);
                        clientToServe.ShowThoughtBubble(UI.GameText.Get("client.director.apology"), 2f);
                        clientToServe.isQueueJumper = false;
                        
                        if (Objects.TicketTerminal.Instance != null && Objects.TicketTerminal.Instance.GetStandWaypoint() != null)
                        {
                            clientToServe.stateMachine.SetGoal(Objects.TicketTerminal.Instance.GetStandWaypoint());
                            clientToServe.stateMachine.SetState(ClientState.MovingToTerminal);
                        }
                        else clientToServe.stateMachine.SetState(ClientState.Confused);
                        
                        workstation.ClearClient(clientToServe);
                        yield return new WaitForSeconds(2f);
                        continue; // Ждем следующего
                    }
                    else
                    {
                        if (Random.value < currentCorruption)
                        {
                            thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("director.accept_bribe"), 2f, Color.green);
                            if (PlayerWallet.Instance != null) PlayerWallet.Instance.AddMoney(150, UI.GameText.Get("ledger.director_bribe"), IncomeType.Shadow);
                            if (AudioManager.Instance != null) AudioManager.Instance.PlaySound(Scriptables.Audio.SoundID.UI_Money_Income, transform.position);
                        }
                        else
                        {
                            thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("director.accept_jumper"), 2f, Color.gray);
                        }
                        clientToServe.isQueueJumper = false; // Снимаем флаг наглеца
                    }
                }

                // 4. ЖДЕМ КЛИЕНТА (С ТАЙМАУТОМ)
                if (clientToServe != null && clientBeingServed == null)
                {
                    float waitTimer = 0f;
                    bool clientArrived = false;
                    Vector2 wpPos = workstation.clientStandPoint != null ? (Vector2)workstation.clientStandPoint.transform.position : (Vector2)workstation.transform.position;

                    while (waitTimer < 15f && isManuallyWorking)
                    {
                        if (clientToServe == null || clientToServe.stateMachine == null || workstation.CurrentClient != clientToServe) break;

                        if (workstation.IsClientPhysicallyReady && clientToServe.CanContinueService)
                        {
                            clientArrived = true;
                            // --- ИСПРАВЛЕНИЕ: Принудительно останавливаем клиента у стола ---
                            clientToServe.stateMachine.StopAllActionCoroutines();
                            clientToServe.GetComponent<AgentMover>()?.Stop();
                            clientToServe.stateMachine.SetState(ClientState.InsideLimitedZone);
                            // ---------------------------------------------------------------
                            break;
                        }

                        var state = clientToServe.stateMachine.GetCurrentState();
                        if (state == ClientState.Leaving || state == ClientState.LeavingUpset || state == ClientState.Confused || state == ClientState.Enraged)
                            break;

                        waitTimer += 0.5f;
                        yield return new WaitForSeconds(0.5f);
                    }

                    if (!clientArrived)
                    {
                        thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("staff.registration.next"), 2f, Color.red);
                        workstation.ClearClient(clientToServe);
                        if (clientToServe != null && ClientQueueManager.Instance != null)
                        {
                            clientToServe.stateMachine?.CancelServiceCall(this);
                        }
                    }
                    else
                    {
                        clientBeingServed = clientToServe;
                        yield return DirectorServiceRoutine(clientToServe);
                        clientBeingServed = null;
                        workstation.ClearClient(clientToServe);

                        if (durability != null) durability.Degrade(Random.Range(1f, 2f));
                    }
                }
            }

            yield return new WaitForSeconds(0.5f);
        }
         Debug.Log($"[DirectorController] {characterName} закончил ручную работу на {workstation?.name}.");
    }

    // --- НОВАЯ СИСТЕМА АРХИВА ДЛЯ ДИРЕКТОРА ---
    private IEnumerator DirectorArchiveRetrieveRoutine()
    {
        var manager = ArchiveRequestManager.Instance;
        var request = manager != null ? manager.GetNextRequest() : null;
        if (request == null) yield break;
        System.Action cleanup = () =>
        {
            if (!request.IsFulfilled) request.IsTaken = false;
            stackHolder?.HideStack();
        };
        manualServiceCleanup = cleanup;
        try
        {
            var cabinet = ArchiveManager.Instance != null ? ArchiveManager.Instance.GetRandomCabinet() : null;
            if (cabinet == null) yield break;
            yield return MoveToTargetRoutine(cabinet.transform.position);
            if (!request.IsActive || cabinet == null || Vector2.Distance(transform.position, cabinet.transform.position) > 1f) yield break;
            SetState(DirectorState.WorkingAtStation);
            thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("staff.archive.searching"), 2f, Color.yellow);
            float remaining = Random.Range(1, 3) * 1.5f;
            while (remaining > 0 && request.IsActive)
            {
                remaining -= Time.deltaTime;
                yield return null;
            }
            if (!request.IsActive || currentWorkstation == null || currentWorkstation.clerkStandPoint == null) yield break;
            stackHolder?.ShowSingleDocumentSprite();
            // Fulfilled records wait at the archive desk; registrar and delegated courier both collect there.
            yield return MoveToTargetRoutine(currentWorkstation.clerkStandPoint.position);
            if (!request.IsActive || currentWorkstation == null || currentWorkstation.clerkStandPoint == null ||
                Vector2.Distance(transform.position, currentWorkstation.clerkStandPoint.position) > 1f) yield break;
            manager?.FulfillRequest(request);
            thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("staff.archive.record_ready"), 2f, Color.white);
        }
        finally
        {
            cleanup();
            if (manualServiceCleanup == cleanup) manualServiceCleanup = null;
            if (isManuallyWorking) SetState(DirectorState.WorkingAtStation);
        }
    }

    private IEnumerator DirectorArchiveStoreRoutine()
    {
        var source = ArchiveManager.Instance != null ? ArchiveManager.Instance.GetStackToProcess() : null;
        var cabinet = ArchiveManager.Instance != null ? ArchiveManager.Instance.GetRandomCabinet() : null;
        if (source == null || cabinet == null) yield break;
        var sourceDesk = source.GetComponentInParent<ServicePoint>();
        var pickup = sourceDesk != null && sourceDesk.internCollectionPoint != null ? sourceDesk.internCollectionPoint : source.transform;
        yield return MoveToTargetRoutine(pickup.position);
        if (source == null || pickup == null || Vector2.Distance(transform.position, pickup.position) > 1f) yield break;
        var transfer = new DocumentStackTransfer(source, 1);
        System.Action cleanup = () => { transfer.Dispose(); stackHolder?.HideStack(); };
        manualServiceCleanup = cleanup;
        try
        {
            if (transfer.Count == 0) yield break;
            stackHolder?.ShowSingleDocumentSprite();
            yield return MoveToTargetRoutine(cabinet.transform.position);
            if (cabinet == null || Vector2.Distance(transform.position, cabinet.transform.position) > 1f) yield break;
            SetState(DirectorState.WorkingAtStation);
            thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("staff.archive.filing"), 1f, Color.gray);
            yield return new WaitForSeconds(1f);
            if (cabinet == null) yield break;
            transfer.Archive();
        }
        finally
        {
            cleanup();
            if (manualServiceCleanup == cleanup) manualServiceCleanup = null;
        }
        if (currentWorkstation != null && currentWorkstation.clerkStandPoint != null)
        {
            yield return MoveToTargetRoutine(currentWorkstation.clerkStandPoint.position);
            SetState(DirectorState.WorkingAtStation);
        }
    }

    /// <summary>
    /// Корутина для перемещения Директора к цели с использованием PathfindingUtility.
    /// </summary>
    // <<< ИЗМЕНЕНИЕ: Убран stateAfterArrellation >>>
    private IEnumerator MoveToTargetRoutine(Vector2 targetPosition)
    {
        SetState(DirectorState.MovingToPoint); // Устанавливаем состояние "Движется к точке"
        Queue<Waypoint> path = PathfindingUtility.BuildPathTo(transform.position, targetPosition, gameObject);
        if (path != null && path.Count > 0)
        {
            if (agentMover != null)
            {
                agentMover.SetPath(path);
                yield return new WaitUntil(() => agentMover == null || !agentMover.IsMoving());
            } else {
                 Debug.LogError($"AgentMover не найден на {gameObject.name}! Невозможно двигаться.");
            }
        } else {
             Debug.LogWarning($"Не удалось построить путь для {characterName} к {targetPosition}. Возможно, цель недостижима.");
        }
        // <<< ИЗМЕНЕНИЕ: УДАЛЕНА УСТАНОВКА СОСТОЯНИЯ >>>
    }

    /// <summary>
    /// Устанавливает новое состояние Директора и обновляет его эмоцию.
    /// </summary>
    public void SetState(DirectorState newState)
    {
        if (currentState == newState) return; // Не меняем, если состояние то же самое
         // Debug.Log($"[DirectorController] {characterName} State Change: {currentState} -> {newState}");
        currentState = newState;
        // Обновляем эмоцию через CharacterVisuals, если он есть
        visuals?.SetEmotionForState(newState);
    }

    /// <summary>
    /// Корутина для похода к барьеру и его переключения.
    /// </summary>
    private IEnumerator OperateBarrierRoutine()
    {
        SetUninterruptible(true);
        var barrier = Managers.GuardManager.Instance.securityBarrier;
        if (barrier == null || barrier.guardInteractionPoint == null)
        {
            Debug.LogError("SecurityBarrier или его guardInteractionPoint не найдены!");
            SetUninterruptible(false);
            yield break;
        }

        yield return MoveToTargetRoutine(barrier.guardInteractionPoint.position);
        SetState(DirectorState.MovingToPoint);

        if (agentMover != null && agentMover.IsSlipping)
        {
            Debug.Log($"[DirectorController] {characterName} упал по пути к двери, операция отменена.");
            SetState(DirectorState.Idle);
            SetUninterruptible(false);
            yield break;
        }

        yield return new WaitForSeconds(1.5f);

        if (agentMover != null && agentMover.IsSlipping)
        {
            Debug.Log($"[DirectorController] {characterName} упал, операция отменена.");
            SetState(DirectorState.Idle);
            SetUninterruptible(false);
            yield break;
        }

        barrier.ToggleBarrier();
        
        // Разблокируем достижение "Первый раз открыл дверь"
        if (AchievementManager.Instance != null)
        {
            AchievementManager.Instance.UnlockAchievement("OPEN_FIRST_DOOR");
        }

        SetState(DirectorState.Idle);
        SetUninterruptible(false);
    }

    /// <summary>
    /// Корутина для сбора документов со стола и доставки их в архив.
    /// </summary>
    private IEnumerator CollectAndDeliverRoutine(DocumentStack stack)
    {
        var archive = ArchiveManager.Instance;
        if (stack == null || archive == null) yield break;
        SetUninterruptible(true);
        SetState(DirectorState.GoingForDocuments);
        try
        {
            yield return MoveToTargetRoutine(stack.transform.position);
            if (stack == null || Vector2.Distance(transform.position, stack.transform.position) > 1f)
                yield break;

            deliveryArchive = archive;
            deliveryPoint = archive.RequestDropOffPoint();
            if (deliveryPoint == null) yield break;

            // Preserve the actual papers and their save identities. Taking a numeric count
            // destroyed them, lost overflow, and left nothing to restore on a night teleport.
            documentTransfer = new DocumentStackTransfer(stack, stack.OrdinaryDocumentCount);
            if (documentTransfer.Count == 0) yield break;
            stackHolder?.ShowStack(documentTransfer.Count, stack.maxStackSize);
            SetState(DirectorState.CarryingDocuments);
            yield return MoveToTargetRoutine(deliveryPoint.position);
            if (deliveryPoint == null || Vector2.Distance(transform.position, deliveryPoint.position) > 1f)
                yield break;

            int delivered = documentTransfer.DeliverTo(archive.GetDropOffStack(deliveryPoint));
            Debug.Log($"[DirectorDocumentTrace] Доставлено в архив: {delivered}; возвращено на исходный стол: {documentTransfer.Count}.");
        }
        finally
        {
            // Explicit cleanup in ReturnToDeskAfterDayEnd/OnDisable also covers Unity
            // coroutine cancellation, which must not depend on iterator disposal.
            CleanupDocumentDelivery();
            SetState(DirectorState.Idle);
            SetUninterruptible(agentMover != null && agentMover.IsSlipping);
        }
    }

    /// <summary>
    /// Находит ближайшую путевую точку к указанной позиции.
    /// </summary>
    private Waypoint FindNearestWaypointTo(Vector2 position)
    {
         Waypoint[] allWaypoints = FindObjectsByType<Waypoint>(FindObjectsSortMode.None);
         if (allWaypoints == null || allWaypoints.Length == 0) {
               Debug.LogError("На сцене нет Waypoint'ов!");
               return null;
         }
          // Используем Linq для поиска ближайшей точки, исключая null
        return allWaypoints
            .Where(wp => wp != null)
            .OrderBy(wp => Vector2.Distance(position, wp.transform.position))
            .FirstOrDefault();
    }

    /// <summary>
    /// Оценивает суммарную длину маршрута по вейпоинтам от startPos до waypointPos.
    /// Используется для сравнения стоимости двух альтернативных подходов в клинче.
    /// </summary>
    private float EstimatePathLength(Vector2 startPos, Vector2 waypointPos)
    {
        var path = Utilities.PathfindingUtility.BuildPathTo(startPos, waypointPos, gameObject);
        if (path == null || path.Count == 0) return float.PositiveInfinity;

        float total = 0f;
        Vector2 prev = startPos;
        foreach (var wp in path)
        {
            if (wp == null) continue;
            total += Vector2.Distance(prev, wp.transform.position);
            prev = wp.transform.position;
        }
        // Дотягивание от последнего вейпоинта до целевой точки (если она не совпадает с вейпоинтом)
        total += Vector2.Distance(prev, waypointPos);
        return total;
    }

    /// <summary>
    /// Автоматическая катсцена туториала - директор самостоятельно оформляет приказ.
    /// </summary>
    public IEnumerator TutorialAutoTourRoutine(Managers.TutorialBureaucracyQuest quest)
    {
        if (quest == null) yield break;
        
        SetUninterruptible(true);

        SetState(DirectorState.AtDesk);
        thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("director.register_order"), 2f, Color.white);
        yield return new WaitForSeconds(1.5f);
        
        // --- ОЧИЩАЕМ СТОЛ И СПАВНИМ ДОКУМЕНТ В РУКЕ ---
        if (quest.directorInboxStack != null) quest.directorInboxStack.TakeEntireStack();

        Gameplay.Documents.ProjectDocumentObject physicalDoc = null;
        if (quest.innerDocPrefab != null)
        {
            // Надежный способ: берем трансформ от спрайта StackInHands, он точно двигается с телом
            Transform hand = (stackHolder != null && stackHolder.stackSpriteRenderer != null)
                ? stackHolder.stackSpriteRenderer.transform
                : transform;
            
            GameObject docGO = Instantiate(quest.innerDocPrefab, hand);
            
            // Жестко выставляем координаты относительно руки (сдвигаем по Z к камере, чтобы не прятался за спрайт)
            docGO.transform.localPosition = new Vector3(0, 0f, -0.1f);
            docGO.transform.localRotation = Quaternion.identity;
            docGO.transform.localScale = Vector3.one;
            
            // Обновляем слои, чтобы папка рисовалась поверх Директора
            SpriteRenderer[] renderers = docGO.GetComponentsInChildren<SpriteRenderer>();
            foreach (var sr in renderers)
            {
                sr.sortingOrder = 30000;
            }
            
            physicalDoc = docGO.GetComponent<Gameplay.Documents.ProjectDocumentObject>();
            if (physicalDoc != null && quest.tutorialDoc != null)
            {
                quest.tutorialDoc.signedByDirector = true;
                physicalDoc.Initialize(quest.tutorialDoc);
            }
            
            // Гасим дефолтную белую заглушку, чтобы она не торчала из-под префаба
            if (stackHolder != null && stackHolder.stackSpriteRenderer != null)
            {
                stackHolder.stackSpriteRenderer.enabled = false;
            }
        }
        else
        {
            stackHolder?.ShowSingleDocumentSprite();
        }
        // --------------------------------

        SetState(DirectorState.CarryingDocuments);

        // 2. Идем в Регистратуру
        SetState(DirectorState.MovingToPoint);
        yield return MoveToTargetRoutine(quest.registrarStandPoint.position);
        SetState(DirectorState.WorkingAtStation);
        thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("director.registrar_absent"), 3f, Color.yellow);
        yield return new WaitForSeconds(3f);

        // Ставим печать регистрации
        if (quest.tutorialDoc != null) quest.tutorialDoc.processedByRegistrar = true;
        if (physicalDoc != null) physicalDoc.UpdateVisuals();
        if (Managers.AudioManager.Instance != null) Managers.AudioManager.Instance.PlaySound(Scriptables.Audio.SoundID.UI_Stamp_Approve, transform.position);

        // 3. Идем в Офис
        SetState(DirectorState.MovingToPoint);
        yield return MoveToTargetRoutine(quest.officeStandPoint.position);
        SetState(DirectorState.WorkingAtStation);
        thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("director.stamp_document"), 2f, Color.gray);
        yield return new WaitForSeconds(2f);
        if (Managers.AudioManager.Instance != null) Managers.AudioManager.Instance.PlaySound(Scriptables.Audio.SoundID.UI_Stamp_Approve, transform.position);

        // 4. Идем в Кассу
        SetState(DirectorState.MovingToPoint);
        yield return MoveToTargetRoutine(quest.cashierStandPoint.position);
        SetState(DirectorState.WorkingAtStation);
        thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("director.pay_fee"), 3f, Color.yellow);
        
        if (Managers.PlayerWallet.Instance != null)
        {
            Managers.PlayerWallet.Instance.AddMoney(-5, UI.GameText.Get("ledger.registration_fee"));
            if (Managers.AudioManager.Instance != null) Managers.AudioManager.Instance.PlaySound(Scriptables.Audio.SoundID.UI_Money_Income, transform.position);
        }
        yield return new WaitForSeconds(2f);

        // Ставим печать кассы
        if (quest.tutorialDoc != null) quest.tutorialDoc.paidAtCashier = true;
        if (physicalDoc != null) physicalDoc.UpdateVisuals();

        // 5. Идем в Архив
        SetState(DirectorState.MovingToPoint);
        yield return MoveToTargetRoutine(quest.archiveStandPoint.position);
        SetState(DirectorState.WorkingAtStation);
        thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("director.finally_archive"), 2f, Color.green);
        
        // Убираем документ
        if (physicalDoc != null) Destroy(physicalDoc.gameObject);
        else stackHolder?.HideStack();

        yield return new WaitForSeconds(2f);

        thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("staff.action.done"), 1.5f, Color.green);
        yield return new WaitForSeconds(1.5f);

        SetState(DirectorState.Idle);
        SetUninterruptible(false);
        
        quest.CompleteQuest();
    }
    #endregion

    #region IServiceProvider Implementation & Director Service Logic

    // Реализация интерфейса IServiceProvider
    // Директор доступен для обслуживания, если он вручную работает на станции,
    // не занят обслуживанием другого клиента и находится в состоянии WorkingAtStation.
    public bool IsAvailableToServe => isManuallyWorking && clientBeingServed == null && currentState == DirectorState.WorkingAtStation;

    // Возвращает точку, где должен стоять клиент при обслуживании Директором.
    public Transform GetClientStandPoint() => currentWorkstation?.clientStandPoint?.transform; // Безопасный доступ

    // Возвращает текущее рабочее место (ServicePoint), где работает Директор.
    public ServicePoint GetWorkstation() => currentWorkstation;

    // Метод для назначения клиента Директору (вызывается извне, но логика теперь в WorkAtStationRoutine).
    public void AssignClient(ClientPathfinding client)
    {
        // Логика перенесена в WorkAtStationRoutine.
        // Этот метод может быть вызван ClientQueueManager, но Директор сам найдет клиента.
         Debug.LogWarning($"[DirectorController] AssignClient вызван для {client?.name}, но Директор сам выбирает клиента в WorkAtStationRoutine.");
    }

    /// <summary>
    /// Корутина, моделирующая процесс обслуживания клиента Директором на разных типах столов.
    /// </summary>
    private bool CanContinueManualService(ClientPathfinding client, ServicePoint desk)
    {
        if (!isManuallyWorking || desk == null || desk != currentWorkstation || desk.clientStandPoint == null ||
            client == null || !client.CanContinueService || desk.CurrentClient != client) return false;
        var assignment = client.stateMachine.AssignedServicePoint;
        return (assignment == desk || (assignment == null && client.stateMachine.GetCurrentGoal() == desk.clientStandPoint)) &&
               Vector2.Distance(client.transform.position, desk.clientStandPoint.transform.position) <= 1.5f;
    }

    private IEnumerator WaitForManualService(ClientPathfinding client, ServicePoint desk, float duration)
    {
        while (duration > 0f && CanContinueManualService(client, desk))
        {
            duration -= Time.deltaTime;
            yield return null;
        }
    }

    private IEnumerator DirectorServiceRoutine(ClientPathfinding client)
    {
        var desk = currentWorkstation;
        if (!CanContinueManualService(client, desk)) yield break;
        clientBeingServed = client;
        SetState(DirectorState.ServingClient);
        bool jobDone = false, committed = false, borrowed = false, cleaned = false;
        var holder = client.docHolder;
        var originalType = holder != null ? holder.GetCurrentDocumentType() : DocumentType.None;
        var originalData = holder != null ? holder.GetCurrentDocumentData() : null;
        GameObject incoming = null, outgoing = null, processing = null;
        System.Action cleanup = () =>
        {
            if (cleaned) return;
            cleaned = true;
            if (incoming != null) { incoming.GetComponent<DocumentMover>()?.CancelMove(); Destroy(incoming); }
            if (outgoing != null)
            {
                outgoing.GetComponent<DocumentMover>()?.CancelMove();
                if (!committed) Destroy(outgoing);
            }
            if (borrowed && !committed && holder != null)
            {
                if (originalData != null) holder.SetDocument(originalData);
                else holder.SetDocument(originalType);
            }
            if (processing != null) Destroy(processing);
            stackHolder?.HideStack();
            if (desk != null && client != null && desk.CurrentClient == client)
            {
                client.stateMachine?.CancelServiceCall(this);
                desk.ClearClient(client);
            }
        };
        // StopCoroutine does not guarantee disposal of nested iterators in Unity.
        // StopManualWork and OnDisable therefore invoke the same idempotent cleanup explicitly.
        manualServiceCleanup = cleanup;
        try
        {
            if (desk.deskId == 1 || desk.deskId == 2)
            {
                var cashier = ClientSpawner.GetCashierZone()?.waitingWaypoint;
                if (holder == null || cashier == null) yield break;
                var form = desk.deskId == 1 ? DocumentType.Form1 : DocumentType.Form2;
                var certificate = desk.deskId == 1 ? DocumentType.Certificate1 : DocumentType.Certificate2;
                thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("staff.service.checking"), 2f, Color.yellow);
                yield return WaitForManualService(client, desk, 1f);
                if (!CanContinueManualService(client, desk)) yield break;
                if (holder.GetCurrentDocumentType() != form)
                {
                    thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("staff.service.wrong_form"), 3f, Color.red);
                    yield return WaitForManualService(client, desk, 1.5f);
                    if (!CanContinueManualService(client, desk)) yield break;
                    client.stateMachine.GoGetFormAndReturn();
                    jobDone = true;
                    yield break;
                }
                var point = desk.documentPointOnDesk;
                if (point != null)
                {
                    incoming = holder.DetachDocument();
                    borrowed = true;
                    if (incoming != null)
                    {
                        bool arrived = false;
                        var mover = incoming.GetComponent<DocumentMover>() ?? incoming.AddComponent<DocumentMover>();
                        mover.StartMove(point, () => arrived = true);
                        float deadline = Time.time + 30f;
                        yield return new WaitUntil(() => arrived || incoming == null || point == null ||
                            !CanContinueManualService(client, desk) || Time.time >= deadline);
                        if (!arrived || incoming == null || point == null || !CanContinueManualService(client, desk)) yield break;
                        incoming.transform.SetParent(point, false);
                        incoming.transform.localPosition = Vector3.zero;
                    }
                }
                float duration = Random.Range(2.5f, 4f);
                if (processingIconPrefab != null)
                {
                    processing = Instantiate(processingIconPrefab);
                    processing.GetComponent<ProcessingAnimationUI>()?.Play(transform.position, client.transform.position, duration);
                }
                yield return WaitForManualService(client, desk, duration);
                if (!CanContinueManualService(client, desk) || holder == null || cashier == null) yield break;
                var prefab = holder.GetPrefabForType(certificate) ?? (desk.deskId == 1 ? certificate1Prefab : certificate2Prefab);
                if (prefab != null && point != null && holder.handPoint != null)
                {
                    outgoing = Instantiate(prefab, point.position, point.rotation);
                    bool arrived = false;
                    var mover = outgoing.GetComponent<DocumentMover>() ?? outgoing.AddComponent<DocumentMover>();
                    mover.StartMove(holder.handPoint, () => arrived = true);
                    float deadline = Time.time + 30f;
                    yield return new WaitUntil(() => arrived || outgoing == null || holder == null || holder.handPoint == null ||
                        !CanContinueManualService(client, desk) || Time.time >= deadline);
                    if (!arrived || outgoing == null || holder == null || !CanContinueManualService(client, desk)) yield break;
                    holder.ReceiveTransferredDocument(certificate, outgoing);
                }
                else holder.SetDocument(certificate);
                client.billToPay += desk.deskId == 1 ? 100 : 250;
                committed = true;
                thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("staff.service.go_to_cashier"), 3f, Color.green);
                client.stateMachine.SetGoal(cashier);
                client.stateMachine.SetState(ClientState.MovingToGoal);
                jobDone = true;
            }
            else if (desk.deskId == 0)
            {
                thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("staff.registration.checking"), 2f, Color.cyan);
                yield return WaitForManualService(client, desk, Random.Range(1f, 2f));
                if (!CanContinueManualService(client, desk)) yield break;
                Waypoint destination;
                if (client.billToPay > 0 || client.mainGoal == ClientGoal.PayTax)
                    destination = ClientSpawner.GetCashierZone()?.waitingWaypoint;
                else if (client.mainGoal == ClientGoal.GetCertificate1)
                    destination = ClientSpawner.GetDesk1Zone()?.waitingWaypoint;
                else if (client.mainGoal == ClientGoal.GetCertificate2)
                    destination = ClientSpawner.GetDesk2Zone()?.waitingWaypoint;
                else if (client.mainGoal == ClientGoal.DirectorApproval || client.mainGoal == ClientGoal.DirectorAudience)
                    destination = ClientSpawner.Instance?.directorReceptionZone?.waitingWaypoint;
                else if (client.mainGoal == ClientGoal.VisitToilet)
                    destination = ClientSpawner.GetToiletZone()?.waitingWaypoint;
                else if (client.mainGoal == ClientGoal.GetArchiveRecord)
                {
                    destination = ClientSpawner.GetCashierZone()?.waitingWaypoint;
                    var cabinet = ArchiveManager.Instance != null ? ArchiveManager.Instance.GetRandomCabinet() : null;
                    if (cabinet == null || desk.clerkStandPoint == null || destination == null) yield break;
                    client.stateMachine.SetState(ClientState.WaitingForDocument);
                    thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("staff.archive.requesting"), 2f, Color.cyan);
                    SetState(DirectorState.MovingToPoint);
                    yield return MoveToTargetRoutine(cabinet.transform.position);
                    if (cabinet == null || !CanContinueManualService(client, desk) ||
                        Vector2.Distance(transform.position, cabinet.transform.position) > 1f) yield break;
                    stackHolder?.ShowSingleDocumentSprite();
                    yield return WaitForManualService(client, desk, 3f);
                    if (!CanContinueManualService(client, desk) || desk.clerkStandPoint == null) yield break;
                    yield return MoveToTargetRoutine(desk.clerkStandPoint.position);
                    if (!CanContinueManualService(client, desk) || desk.clerkStandPoint == null ||
                        Vector2.Distance(transform.position, desk.clerkStandPoint.position) > 1f) yield break;
                    stackHolder?.HideStack();
                    SetState(DirectorState.ServingClient);
                    client.billToPay += 150;
                }
                else destination = ClientSpawner.Instance?.exitWaypoint;
                if (destination == null) yield break;
                string destinationName = string.IsNullOrEmpty(destination.friendlyName) ? destination.name : destination.friendlyName;
                thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("staff.registration.direction", destinationName), 3f, Color.white);
                ClientQueueManager.Instance?.RemoveClientFromQueue(client);
                if (destination == ClientSpawner.Instance?.exitWaypoint)
                {
                    client.isLeavingSuccessfully = true;
                    client.reasonForLeaving = ClientPathfinding.LeaveReason.Processed;
                }
                client.stateMachine.SetGoal(destination);
                client.stateMachine.SetState(ClientState.MovingToGoal);
                jobDone = true;
            }
            else if (desk.deskId == -1)
            {
                yield return WaitForManualService(client, desk, Random.Range(2f, 3.5f));
                if (!CanContinueManualService(client, desk) || PlayerWallet.Instance == null) yield break;
                if (client.billToPay == 0 && client.mainGoal == ClientGoal.PayTax) client.billToPay = Random.Range(20, 121);
                int bill = client.billToPay;
                if (bill <= 0) yield break;
                if (client.moneyPrefab != null)
                {
                    var effect = Instantiate(client.moneyPrefab, client.transform.position + Vector3.up, Quaternion.identity);
                    var mover = effect.GetComponent<MoneyMover>();
                    if (mover != null) mover.StartMove(desk.moneyTrayPoint != null ? desk.moneyTrayPoint : desk.transform);
                    else Destroy(effect);
                }
                int before = PlayerWallet.Instance.GetCurrentMoney();
                PlayerWallet.Instance.AddMoney(bill, UI.GameText.Get("ledger.service_payment", client.name), IncomeType.Official);
                client.billToPay = 0;
                Debug.Log($"[ClientServiceTrace] Director payment: client={client.name}, bill={bill}, wallet={before}->{PlayerWallet.Instance.GetCurrentMoney()}");
                if (client.paymentSound != null) AudioSource.PlayClipAtPoint(client.paymentSound, transform.position);
                client.isLeavingSuccessfully = true;
                client.reasonForLeaving = ClientPathfinding.LeaveReason.Processed;
                client.stateMachine.SetGoal(ClientSpawner.Instance?.exitWaypoint);
                client.stateMachine.SetState(ClientState.Leaving);
                jobDone = true;
            }
        }
        finally
        {
            cleanup();
            if (manualServiceCleanup == cleanup) manualServiceCleanup = null;
            if (jobDone && desk != null && desk.documentStack != null) desk.documentStack.AddDocumentToStack();
            if (clientBeingServed == client) clientBeingServed = null;
            if (isManuallyWorking && currentWorkstation == desk) SetState(DirectorState.WorkingAtStation);
            Debug.Log($"[ClientServiceTrace] Director service complete: client={client?.name}, success={jobDone}");
        }
    }

    /// <summary>
    /// Отправляет Директора к указанной мишени клинча и открывает UI мини-игры.
    /// </summary>
    /// <param name="target">Мишень клинча для разрешения</param>
    public void GoAndResolveClinch(Clinch.ClinchTarget target)
    {
        if (target == null) return;
        if (IsInUninterruptibleAction)
        {
            Debug.LogWarning("[DirectorAvatarController] GoAndResolveClinch: Директор занят!");
            return;
        }
        StartCoroutine(GoAndResolveClinchRoutine(target));
    }

    private IEnumerator GoAndResolveClinchRoutine(Clinch.ClinchTarget target)
    {
        if (target == null) yield break;

        SetUninterruptible(true);
        SetState(DirectorState.MovingToPoint);

        // 1. УМНЫЙ ПОИСК ЦЕЛИ (Чтобы не обегать столы)
        // Сравниваем длину маршрута до clerkStandPoint (рабочее место) и до ближайшего
        // вейпоинта рядом с самим клиентом — идём туда, куда быстрее.
        Vector3 destination = target.transform.position;
        Vector3 pathfindingTarget = target.transform.position;

        var client = target.GetComponent<ClientPathfinding>();
        var staff = target.GetComponent<StaffController>();

        ServicePoint relatedDesk = null;

        if (client != null && client.stateMachine != null && client.stateMachine.MyServiceProvider != null)
        {
            relatedDesk = client.stateMachine.MyServiceProvider.GetWorkstation();
        }
        else if (staff != null)
        {
            relatedDesk = staff.assignedWorkstation;
        }

        // Считаем «стоимость» двух вариантов подхода: до рабочего места и до ближайшей точки у клиента.
        // Учитываем и длину пути по вейпоинтам, и финальную «честную» дистанцию от конечного узла до цели.
        float deskCost = float.PositiveInfinity;
        float clientCost = float.PositiveInfinity;

        if (relatedDesk != null && relatedDesk.clerkStandPoint != null)
        {
            deskCost = EstimatePathLength(transform.position, relatedDesk.clerkStandPoint.position)
                       + Vector2.Distance(relatedDesk.clerkStandPoint.position, target.transform.position);
        }

        var nearestToClient = FindNearestWaypointTo(target.transform.position);
        if (nearestToClient != null)
        {
            clientCost = EstimatePathLength(transform.position, nearestToClient.transform.position)
                         + Vector2.Distance(nearestToClient.transform.position, target.transform.position);
        }

        if (deskCost < clientCost)
        {
            // Быстрее добраться до рабочего места — идём через clerkStandPoint
            destination = relatedDesk != null && relatedDesk.clerkStandPoint != null
                ? relatedDesk.clerkStandPoint.position
                : target.transform.position;
            pathfindingTarget = destination;
        }
        else if (nearestToClient != null)
        {
            // Быстрее подойти почти к самому клиенту — идём к ближайшему вейпоинту рядом с ним
            destination = nearestToClient.transform.position;
            pathfindingTarget = target.transform.position;
        }
        else
        {
            // Фоллбэк: ни маршрут до стола, ни маршрут к клиенту не доступны
            pathfindingTarget = destination;
        }

        // 2. ДВИЖЕНИЕ И ПРЕРЫВАНИЕ ПО ДИСТАНЦИИ
        if (agentMover != null)
        {
            var path = Utilities.PathfindingUtility.BuildPathTo(transform.position, pathfindingTarget, gameObject);
            agentMover.SetPath(path);
        }

        float interactRange = 1.5f;

        // Ждем, пока двигаемся, но бьем по тормозам, если подошли достаточно близко
        while (target != null && target.IsActive && agentMover != null && agentMover.IsMoving())
        {
            if (Vector2.Distance(transform.position, target.transform.position) <= interactRange)
            {
                agentMover.Stop();
                break;
            }
            yield return null;
        }

        SetState(DirectorState.Idle);
        SetUninterruptible(false);

        // 3. ПРОВЕРКА И ОТКРЫТИЕ UI
        // Даем небольшой запас (+0.5f), чтобы не промахнуться из-за погрешностей остановки физики
        if (target != null && target.IsActive && Vector2.Distance(transform.position, target.transform.position) <= interactRange + 0.5f)
        {
            target.OpenUI();
        }
        else if (target != null && !target.IsActive)
        {
            thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("director.too_late"), 2f, Color.gray);
        }
    }
    #endregion
} // Конец класса DirectorAvatarController
