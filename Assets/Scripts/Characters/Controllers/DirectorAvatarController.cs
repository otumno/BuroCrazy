// Файл: Assets/Scripts/Characters/Controllers/DirectorAvatarController.cs
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Managers;
using Utilities;
using Characters;
using UI.Bookkeeping;

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

    private ClientPathfinding clientBeingServed = null;
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
    /// Отдать приказ сотруднику. Директор приказывает выполнить определённое действие.
    /// </summary>
    public void GiveOrder(StaffController target, StaffAction action)
    {
        if (target == null || action == null) return;
        
        // Показываем реакцию директора
        thoughtBubble?.ShowPriorityMessage("Немедленно займись этим!", 2f, Color.red);
        
        // Проигрываем звук (защита от NullReference)
        if (AudioManager.Instance != null)
        {
            // TODO: Заменить на крик директора
            // AudioManager.Instance.PlaySound(...);
        }
        
        // Отдаём приказ сотруднику
        target.ReceiveOrder(action);
        
        Debug.Log($"[MicroManagement] Директор приказал {target.characterName} выполнить: {action.displayName}");
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
    /// Вызывается при старте новой игры, чтобы применить выбор из книги создания директора.
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
        yield return StartCoroutine(MoveToTargetAndSetState(targetWaypoint.transform.position, stateAfterArrival));
        
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
        yield return StartCoroutine(MoveToTargetRoutine(targetPosition)); // Ждем завершения движения
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
            thoughtBubble?.ShowPriorityMessage("Я занят!", 2f, Color.yellow);
            return;
        }
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
    
    yield return StartCoroutine(MoveToTargetRoutine(board.interactionPoint.position));
    
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
        
        yield return StartCoroutine(MoveToTargetRoutine(wp.transform.position));
        
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
         thoughtBubble?.ShowPriorityMessage("Я сейчас занят!", 2f, Color.yellow);
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
    yield return StartCoroutine(MoveToTargetRoutine(targetPosition)); // Ждем завершения движения

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
            thoughtBubble?.ShowPriorityMessage("Здесь всё сломано!\nНе могу работать.", 3f, Color.red);
            isManuallyWorking = false;
            yield break;
        }
        // ------------------------------------

        ClientSpawner.AssignServiceProviderToDesk(this, workstation.deskId);
        isManuallyWorking = true; 
        Debug.Log($"[DirectorController] {characterName} назначен на {workstation.name}.");

        if (workstation.clerkStandPoint != null) {
            yield return StartCoroutine(MoveToTargetRoutine(workstation.clerkStandPoint.position)); 
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
                thoughtBubble?.ShowPriorityMessage("Стол сломался!", 2f, Color.red);
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
                    yield return StartCoroutine(DirectorArchiveRetrieveRoutine());
                    continue;
                }
                else if (ArchiveManager.Instance != null && !ArchiveManager.Instance.mainDocumentStack.IsEmpty)
                {
                    // Архивируем документы со стола
                    yield return StartCoroutine(DirectorArchiveStoreRoutine());
                    continue;
                }
            }
            // -----------------------------------------------------

            // --- PULL-МОДЕЛЬ ДЛЯ РЕГИСТРАТУРЫ (deskId == 0) ---
            if (workstation.deskId == 0)
            {
                if (ClientQueueManager.Instance != null && clientBeingServed == null)
                {
                    var nextInQueue = ClientQueueManager.Instance.queue
                        .Where(kvp => kvp.Key != null && !ClientQueueManager.Instance.currentlyCalledNumbers.Contains(kvp.Value))
                        .OrderBy(kvp => kvp.Value)
                        .FirstOrDefault();

                    var client = nextInQueue.Key;
                    if (client != null)
                    {
                        int ticketNum = nextInQueue.Value;
                        ClientQueueManager.Instance.currentlyCalledNumbers.Add(ticketNum);
                        ClientQueueManager.Instance.clientsAwaitingResponse.Add(ticketNum, Time.time);

                        Waypoint wp = workstation.clientStandPoint != null ? workstation.clientStandPoint : workstation.GetComponentInChildren<Waypoint>();
                        client.stateMachine.GetCalledToSpecificDesk(wp, ticketNum, this);

                        // Ждём прибытия клиента (с таймаутом 15f)
                        float waitTimer = 0f;
                        yield return new WaitUntil(() =>
                        {
                            waitTimer += Time.deltaTime;
                            if (waitTimer >= 15f) return true;
                            var z = ClientSpawner.GetZoneByDeskId(workstation.deskId);
                            return z?.GetOccupyingClients().Contains(client) ?? false;
                        });

                        if (waitTimer >= 15f)
                        {
                            // Клиент не пришёл за 15ф — убираем из вызванных
                            ClientQueueManager.Instance.currentlyCalledNumbers.Remove(ticketNum);
                            ClientQueueManager.Instance.clientsAwaitingResponse.Remove(ticketNum);
                            yield return new WaitForSeconds(0.5f);
                            continue;
                        }

                        // Обслуживаем клиента
                        clientBeingServed = client;
                        yield return StartCoroutine(DirectorServiceRoutine(client));
                        clientBeingServed = null;

                        // --- ИЗНОС: Наносим урон ПОСЛЕ обслуживания ---
                        if (durability != null) durability.Degrade(Random.Range(2f, 4f));
                        if (efficiency < 1.0f) yield return new WaitForSeconds(1.0f);
                    }
                }
            }
            // --- ЛОГИКА КЛЕРКОВ И РЕГИСТРАТОРА ---
            else
            {
                ClientPathfinding clientToServe = workstation.CurrentClient;

                // 1. ЧИСТАЯ PULL-МОДЕЛЬ ДЛЯ РЕГИСТРАТУРЫ (Берем из очереди)
                if (clientToServe == null && workstation.deskId == 0 && ClientQueueManager.Instance != null && ClientQueueManager.Instance.queue.Count > 0)
                {
                    var nextInQueue = ClientQueueManager.Instance.queue
                        .Where(c => c.Key != null && !ClientQueueManager.Instance.currentlyCalledNumbers.Contains(c.Value))
                        .OrderBy(kvp => kvp.Value)
                        .FirstOrDefault();

                    if (nextInQueue.Key != null)
                    {
                        clientToServe = nextInQueue.Key;
                        int ticketNum = nextInQueue.Value;
                        
                        ClientQueueManager.Instance.currentlyCalledNumbers.Add(ticketNum);
                        ClientQueueManager.Instance.clientsAwaitingResponse.Add(ticketNum, Time.time);
                        
                        workstation.AssignClient(clientToServe);

                        string callMsg = ticketNum > 10000 ? "Следующий!" : $"Талон №{ticketNum}, подойдите!";
                        thoughtBubble?.ShowPriorityMessage(callMsg, 3f, Color.green);
                        if (ClientQueueManager.Instance.nextClientSound != null)
                            Managers.AudioManager.Instance?.PlayAudioClip2D(ClientQueueManager.Instance.nextClientSound);

                        Waypoint wp = workstation.clientStandPoint != null ? workstation.clientStandPoint : workstation.GetComponentInChildren<Waypoint>();
                        clientToServe.stateMachine.GetCalledToSpecificDesk(wp, ticketNum, this);
                    }
                }

                // 2. БРОНЕБОЙНАЯ ТЯГА (Ищем неприкаянных клиентов в зоне стола)
                if (clientToServe == null)
                {
                    var zone = ClientSpawner.GetZoneByDeskId(workstation.deskId);
                    if (zone != null)
                    {
                        clientToServe = zone.GetOccupyingClients().FirstOrDefault(c =>
                            c.stateMachine.GetCurrentState() != ClientState.Leaving &&
                            c.stateMachine.GetCurrentState() != ClientState.LeavingUpset &&
                            c.stateMachine.MyServiceProvider == null);
                            
                        if (clientToServe != null)
                        {
                            workstation.AssignClient(clientToServe);
                            thoughtBubble?.ShowPriorityMessage("Эй, вы, подходите!", 2f, Color.green);
                            Waypoint wp = workstation.clientStandPoint != null ? workstation.clientStandPoint : workstation.GetComponentInChildren<Waypoint>();
                            clientToServe.stateMachine.GetCalledToSpecificDesk(wp, clientToServe.stateMachine.MyQueueNumber, this);
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
                        thoughtBubble?.ShowPriorityMessage("В общую очередь, пожалуйста!", 3f, Color.red);
                        clientToServe.ShowThoughtBubble("Извините, шеф...", 2f);
                        clientToServe.isQueueJumper = false;
                        
                        if (Objects.TicketTerminal.Instance != null && Objects.TicketTerminal.Instance.GetStandWaypoint() != null)
                        {
                            clientToServe.stateMachine.SetGoal(Objects.TicketTerminal.Instance.GetStandWaypoint());
                            clientToServe.stateMachine.SetState(ClientState.MovingToTerminal);
                        }
                        else clientToServe.stateMachine.SetState(ClientState.Confused);
                        
                        workstation.ClearClient();
                        yield return new WaitForSeconds(2f);
                        continue; // Ждем следующего
                    }
                    else
                    {
                        if (Random.value < currentCorruption)
                        {
                            thoughtBubble?.ShowPriorityMessage("Ладно, давайте сюда...", 2f, Color.green);
                            if (PlayerWallet.Instance != null) PlayerWallet.Instance.AddMoney(150, "Взятка Директору", IncomeType.Shadow);
                            if (AudioManager.Instance != null) AudioManager.Instance.PlaySound(Scriptables.Audio.SoundID.UI_Money_Income, transform.position);
                        }
                        else
                        {
                            thoughtBubble?.ShowPriorityMessage("Так уж и быть, давайте...", 2f, Color.gray);
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

                        if (workstation.IsClientPhysicallyReady || Vector2.Distance(clientToServe.transform.position, wpPos) < 1.5f)
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
                        thoughtBubble?.ShowPriorityMessage("Следующий!", 2f, Color.red);
                        workstation.ClearClient();
                        if (clientToServe != null && ClientQueueManager.Instance != null)
                        {
                            ClientQueueManager.Instance.RemoveClientFromQueue(clientToServe);
                            clientToServe.stateMachine?.SetState(ClientState.Confused);
                        }
                    }
                    else
                    {
                        clientBeingServed = clientToServe;
                        yield return StartCoroutine(DirectorServiceRoutine(clientToServe));
                        clientBeingServed = null;
                        workstation.ClearClient();

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
        var request = ArchiveRequestManager.Instance.GetNextRequest();
        if (request == null) yield break;

        // 1. Идем к случайному шкафу
        var cabinet = ArchiveManager.Instance.GetRandomCabinet();
        if (cabinet != null)
        {
            SetState(DirectorState.MovingToPoint);
            yield return StartCoroutine(MoveToTargetRoutine(cabinet.transform.position));
        }

        SetState(DirectorState.WorkingAtStation);
        thoughtBubble?.ShowPriorityMessage("Ищу выписку (Архив)...", 2f, Color.yellow);
        
        // Директор ищет быстро (1-2 шага поиска вместо 2-4 как у обычного клерка)
        int searchSteps = Random.Range(1, 3);
        string[] searchThoughts = { "Где же она...", "Так, так, так...", "Пыли-то сколько..." };
        for(int i = 0; i < searchSteps; i++)
        {
            thoughtBubble?.ShowPriorityMessage(searchThoughts[Random.Range(0, searchThoughts.Length)], 1.5f, Color.gray);
            yield return new WaitForSeconds(1.5f);
        }

        thoughtBubble?.ShowPriorityMessage("Нашел!", 1.5f, Color.green);
        stackHolder?.ShowSingleDocumentSprite();
        
        var registrar = request.RequestingRegistrar;
        if (registrar != null)
        {
            // Несем документ регистратору
            SetState(DirectorState.MovingToPoint);
            yield return StartCoroutine(MoveToTargetRoutine(registrar.transform.position));
            
            stackHolder?.HideStack();
            request.IsFulfilled = true;
            
            thoughtBubble?.ShowPriorityMessage("Вот ваша выписка.", 2f, Color.white);
            yield return new WaitForSeconds(1f);
            
            // Возвращаемся за свой стол в архиве
            SetState(DirectorState.MovingToPoint);
            yield return StartCoroutine(MoveToTargetRoutine(currentWorkstation.clerkStandPoint.position));
            SetState(DirectorState.WorkingAtStation);
        }
        else
        {
            stackHolder?.HideStack();
            request.IsFulfilled = true;
            // Если регистратор пропал, просто возвращаемся за свой стол
            SetState(DirectorState.MovingToPoint);
            yield return StartCoroutine(MoveToTargetRoutine(currentWorkstation.clerkStandPoint.position));
            SetState(DirectorState.WorkingAtStation);
        }
    }

    private IEnumerator DirectorArchiveStoreRoutine()
    {
        // Берем один документ из главной кучи
        if (ArchiveManager.Instance.mainDocumentStack.TakeOneDocument())
        {
            // 1. Идем к случайному шкафу
            var cabinet = ArchiveManager.Instance.GetRandomCabinet();
            if (cabinet != null)
            {
                SetState(DirectorState.MovingToPoint);
                yield return StartCoroutine(MoveToTargetRoutine(cabinet.transform.position));
            }

            SetState(DirectorState.WorkingAtStation);
            thoughtBubble?.ShowPriorityMessage("Архивирую...", 1.0f, Color.gray);
            
            // Директор кладет бумаги в шкаф
            stackHolder?.ShowSingleDocumentSprite();
            yield return new WaitForSeconds(1.0f);
            stackHolder?.HideStack();

            // 2. Возвращаемся за свой стол в архиве за следующей партией
            if (currentWorkstation != null && currentWorkstation.clerkStandPoint != null)
            {
                SetState(DirectorState.MovingToPoint);
                yield return StartCoroutine(MoveToTargetRoutine(currentWorkstation.clerkStandPoint.position));
                SetState(DirectorState.WorkingAtStation);
            }
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

        yield return StartCoroutine(MoveToTargetRoutine(barrier.guardInteractionPoint.position));
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
         if (stack == null) {
              Debug.LogError("CollectAndDeliverRoutine: stack is null!");
              yield break;
         }
         if (ArchiveManager.Instance == null) {
              Debug.LogError("CollectAndDeliverRoutine: ArchiveManager не найден!");
              yield break;
        }


        SetUninterruptible(true); // Блокируем другие действия
        SetState(DirectorState.GoingForDocuments); // Устанавливаем состояние
        Debug.Log($"[DirectorController] {characterName} идет за документами к {stack.name}.");

        // Двигаемся к стопке документов
        yield return StartCoroutine(MoveToTargetRoutine(stack.transform.position)); // <<< ИЗМЕНЕНИЕ: Убран второй аргумент
        // Состояние GoingForDocuments остается

        // Проверяем стопку еще раз после прибытия
         if (stack == null) { // Могла быть уничтожена, пока шли
             Debug.LogWarning($"Стопка {stack?.name} исчезла, пока {characterName} шел.");
              SetState(DirectorState.Idle);
              SetUninterruptible(false);
              yield break;
         }

        // Забираем все документы из стопки
        int docCount = stack.TakeEntireStack();
        Debug.Log($" -> Забрано {docCount} документов.");


        if (docCount > 0) // Если были документы
        {
            // Показываем стопку в руках
            stackHolder?.ShowStack(docCount, stack.maxStackSize);
            SetState(DirectorState.CarryingDocuments); // Меняем состояние

            // Запрашиваем точку сброса в архиве
            Transform archivePoint = ArchiveManager.Instance.RequestDropOffPoint();
            if (archivePoint != null)
            {
                 Debug.Log($" -> Идем к точке архива: {archivePoint.name}.");
                // Двигаемся к точке архива
                 yield return StartCoroutine(MoveToTargetRoutine(archivePoint.position)); // <<< ИЗМЕНЕНИЕ: Убран второй аргумент
                 // Состояние CarryingDocuments остается

                // Добавляем документы в главную стопку архива
                 if (ArchiveManager.Instance.mainDocumentStack != null) {
                     for (int i = 0; i < docCount; i++)
                     {
                         ArchiveManager.Instance.mainDocumentStack.AddDocumentToStack();
                     }
                      Debug.Log($" -> {docCount} документов добавлено в mainDocumentStack.");
                 } else {
                      Debug.LogError("mainDocumentStack в ArchiveManager не найден! Документы потеряны.");
                }

                // Прячем стопку в руках
                stackHolder?.HideStack();
                // Освобождаем точку сброса в архиве
                ArchiveManager.Instance.FreeOverflowPoint(archivePoint);
            }
             else {
                  Debug.LogError($"Не найдена точка сброса в архиве для {characterName}! Документы (кол-во: {docCount}) остались в руках?");
                  // Документы останутся "висеть" в руках, т.к. HideStack не вызван
                  // Можно добавить логику возврата документов или сброса stackHolder
                  stackHolder?.HideStack(); // Пытаемся спрятать стопку
             }
        }
        else // Если в стопке не оказалось документов к моменту прихода
        {
             Debug.Log($" -> В стопке {stack.name} не оказалось документов к моменту прихода {characterName}.");
        }

        SetState(DirectorState.Idle); // Возвращаемся в состояние бездействия
        SetUninterruptible(false); // Снимаем блокировку
         Debug.Log($"[DirectorController] {characterName} завершил сбор/доставку документов.");
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
        thoughtBubble?.ShowPriorityMessage("Так, вот этот приказ...", 2f, Color.white);
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
        yield return StartCoroutine(MoveToTargetRoutine(quest.registrarStandPoint.position));
        SetState(DirectorState.WorkingAtStation);
        thoughtBubble?.ShowPriorityMessage("Никого нет в регистратуре...\nЗарегистрирую сам.", 3f, Color.yellow);
        yield return new WaitForSeconds(3f);

        // Ставим печать регистрации
        if (quest.tutorialDoc != null) quest.tutorialDoc.processedByRegistrar = true;
        if (physicalDoc != null) physicalDoc.UpdateVisuals();
        if (Managers.AudioManager.Instance != null) Managers.AudioManager.Instance.PlaySound(Scriptables.Audio.SoundID.UI_Stamp_Approve, transform.position);

        // 3. Идем в Офис
        SetState(DirectorState.MovingToPoint);
        yield return StartCoroutine(MoveToTargetRoutine(quest.officeStandPoint.position));
        SetState(DirectorState.WorkingAtStation);
        thoughtBubble?.ShowPriorityMessage("Ставлю печать клерка.", 2f, Color.gray);
        yield return new WaitForSeconds(2f);
        if (Managers.AudioManager.Instance != null) Managers.AudioManager.Instance.PlaySound(Scriptables.Audio.SoundID.UI_Stamp_Approve, transform.position);

        // 4. Идем в Кассу
        SetState(DirectorState.MovingToPoint);
        yield return StartCoroutine(MoveToTargetRoutine(quest.cashierStandPoint.position));
        SetState(DirectorState.WorkingAtStation);
        thoughtBubble?.ShowPriorityMessage("Сам себе плачу пошлину...", 3f, Color.yellow);
        
        if (Managers.PlayerWallet.Instance != null)
        {
            Managers.PlayerWallet.Instance.AddMoney(-5, "Пошлина (Оформление)");
            if (Managers.AudioManager.Instance != null) Managers.AudioManager.Instance.PlaySound(Scriptables.Audio.SoundID.UI_Money_Income, transform.position);
        }
        yield return new WaitForSeconds(2f);

        // Ставим печать кассы
        if (quest.tutorialDoc != null) quest.tutorialDoc.paidAtCashier = true;
        if (physicalDoc != null) physicalDoc.UpdateVisuals();

        // 5. Идем в Архив
        SetState(DirectorState.MovingToPoint);
        yield return StartCoroutine(MoveToTargetRoutine(quest.archiveStandPoint.position));
        SetState(DirectorState.WorkingAtStation);
        thoughtBubble?.ShowPriorityMessage("И наконец, в архив!", 2f, Color.green);
        
        // Убираем документ
        if (physicalDoc != null) Destroy(physicalDoc.gameObject);
        else stackHolder?.HideStack();

        yield return new WaitForSeconds(2f);

        thoughtBubble?.ShowPriorityMessage("Готово!", 1.5f, Color.green);
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
    private IEnumerator DirectorServiceRoutine(ClientPathfinding client)
    {
        if (client == null || client.stateMachine == null || currentWorkstation == null)
        {
            Debug.LogError($"DirectorServiceRoutine: client ({client?.name}) или currentWorkstation ({currentWorkstation?.name}) равен null!");
            clientBeingServed = null;
            SetState(DirectorState.WorkingAtStation);
            yield break;
        }

        if (clientBeingServed != null && clientBeingServed != client)
        {
            Debug.LogWarning($"DirectorServiceRoutine: Попытка обслужить {client.name}, когда уже обслуживается {clientBeingServed.name}.");
            yield break;
        }

        clientBeingServed = client;
        SetState(DirectorState.ServingClient);
        Debug.Log($"<color=#00FFFF>ДИРЕКТОР:</color> {characterName} начал обслуживание {client.name} (Цель: {client.mainGoal}) на {currentWorkstation.name} (ID: {currentWorkstation.deskId}).");

        int deskId = currentWorkstation.deskId;
        bool jobDone = false;
        GameObject flyingDoc = null;

        // --- Логика для столов Клерков (ID 1 и 2) ---
        if (deskId == 1 || deskId == 2)
        {
            thoughtBubble?.ShowPriorityMessage("Так... посмотрю...", 2f, Color.yellow);
            yield return new WaitForSeconds(1.0f);

            DocumentType requiredDocType = (deskId == 1) ? DocumentType.Form1 : DocumentType.Form2;
            GameObject certificatePrefab = (deskId == 1) ? certificate1Prefab : certificate2Prefab;
            DocumentType certificateType = (deskId == 1) ? DocumentType.Certificate1 : DocumentType.Certificate2;
            int serviceCost = (deskId == 1) ? 100 : 250;

            if (client.docHolder == null)
            {
                Debug.LogError($"У клиента {client.name} отсутствует DocumentHolder!");
            }
            else if (client.docHolder.GetCurrentDocumentType() != requiredDocType)
            {
                thoughtBubble?.ShowPriorityMessage("У вас бланк не тот!\nВозьмите другой.", 3f, Color.red);
                yield return new WaitForSeconds(1.5f);
                client.stateMachine?.GoGetFormAndReturn();
                jobDone = true;
                Debug.Log($" -> Клиент {client.name} отправлен за другим бланком.");
            }
            else
            {
                Debug.Log($" -> У клиента {client.name} правильный бланк ({requiredDocType}). Начинаем обработку...");
                DocumentHolder clientDocHolder = client.docHolder;
                Transform clientHand = clientDocHolder?.handPoint;
                Transform deskPoint = currentWorkstation.documentPointOnDesk;
                GameObject currentClientDocObject = (clientHand != null && clientHand.childCount > 0) ? clientHand.GetChild(0).gameObject : null;

                if (currentClientDocObject != null && deskPoint != null)
                {
                    clientDocHolder.SetDocument(DocumentType.None);
                    DocumentMover mover = currentClientDocObject.AddComponent<DocumentMover>();
                    bool arrived = false;
                    mover.StartMove(deskPoint, () => { arrived = true; });
                    yield return new WaitUntil(() => arrived);
                    flyingDoc = currentClientDocObject;
                    if (flyingDoc != null)
                    {
                        flyingDoc.transform.SetParent(deskPoint);
                        flyingDoc.transform.localPosition = Vector3.zero;
                        flyingDoc.transform.localRotation = Quaternion.identity;
                        Debug.Log($" -> Документ {flyingDoc.name} перемещен на стол.");
                    }
                }

                thoughtBubble?.ShowPriorityMessage("Обрабатываю...", 3f, Color.white);
                float processTime = Random.Range(2.5f, 4.0f);

                if (processingIconPrefab != null)
                {
                    GameObject iconObj = Instantiate(processingIconPrefab);
                    ProcessingAnimationUI animScript = iconObj.GetComponent<ProcessingAnimationUI>();
                    if (animScript != null)
                    {
                        animScript.Play(transform.position, client.transform.position, processTime);
                    }
                }

                yield return new WaitForSeconds(processTime);

                if (client == null || client.stateMachine == null || currentWorkstation == null)
                {
                    Debug.LogWarning("Клиент или рабочее место исчезли во время обработки документа.");
                    if (flyingDoc != null) Destroy(flyingDoc);
                    jobDone = false;
                }
                else
                {
                    if (flyingDoc != null) Destroy(flyingDoc);
                    deskPoint = currentWorkstation.documentPointOnDesk;
                    clientHand = client.docHolder?.handPoint;

                    if (certificatePrefab != null && deskPoint != null && clientHand != null)
                    {
                        GameObject newCertGO = Instantiate(certificatePrefab, deskPoint.position, deskPoint.rotation);
                        DocumentMover mover = newCertGO.AddComponent<DocumentMover>();
                        bool arrived = false;
                        mover.StartMove(clientHand, () => { arrived = true; });
                        yield return new WaitUntil(() => arrived);

                        if (client != null && client.docHolder != null)
                        {
                            client.docHolder.ReceiveTransferredDocument(certificateType, newCertGO);
                            Debug.Log($" -> Сертификат {newCertGO.name} передан клиенту {client.name}.");
                        }
                    }

                    if (client != null && client.stateMachine != null)
                    {
                        client.billToPay += serviceCost;
                        thoughtBubble?.ShowPriorityMessage("Готово! Теперь в кассу.", 3f, Color.green);
                        Debug.Log($" -> Клиент {client.name} отправлен в кассу (Счет: {client.billToPay}).");
                        client.stateMachine.SetGoal(ClientSpawner.GetCashierZone()?.waitingWaypoint);
                        client.stateMachine.SetState(ClientState.MovingToGoal);
                    }
                    jobDone = true;
                }
            }
        }
        // --- Логика для Регистратуры (ID 0) ---
        else if (deskId == 0)
        {
            thoughtBubble?.ShowPriorityMessage("Смотрю, куда вас направить...", 2f, Color.cyan);
            yield return new WaitForSeconds(Random.Range(1.0f, 2.0f));

            if (client == null || client.stateMachine == null)
            {
                Debug.LogWarning("Клиент исчез во время обработки в регистратуре.");
                jobDone = false;
            }
            else
            {
                Waypoint destination = null;
                bool leavingUpset = false;

                if (client.billToPay > 0)
                {
                    destination = ClientSpawner.GetCashierZone()?.waitingWaypoint;
                }
                else
                {
                    switch (client.mainGoal)
                    {
                        case ClientGoal.PayTax:
                            destination = ClientSpawner.GetCashierZone()?.waitingWaypoint;
                            break;
                        case ClientGoal.GetCertificate1:
                            destination = ClientSpawner.GetDesk1Zone()?.waitingWaypoint;
                            break;
                        case ClientGoal.GetCertificate2:
                            destination = ClientSpawner.GetDesk2Zone()?.waitingWaypoint;
                            break;
                        case ClientGoal.GetArchiveRecord:
                            // Директор сам бежит в архив к шкафу!
                            thoughtBubble?.ShowPriorityMessage("Минутку, я мигом в архив!", 2f, Color.cyan);
                            yield return new WaitForSeconds(1.5f);
                            
                            var targetCabinet = ArchiveManager.Instance?.GetRandomCabinet();
                            if (targetCabinet != null)
                            {
                                // Бежим к шкафу
                                SetState(DirectorState.MovingToPoint);
                                yield return StartCoroutine(MoveToTargetRoutine(targetCabinet.transform.position));
                                
                                SetState(DirectorState.WorkingAtStation);
                                thoughtBubble?.ShowPriorityMessage("Где же это дело...", 1.5f, Color.gray);
                                yield return new WaitForSeconds(1.5f);
                                
                                thoughtBubble?.ShowPriorityMessage("Нашел!", 1.0f, Color.green);
                                stackHolder?.ShowSingleDocumentSprite();
                                yield return new WaitForSeconds(1.0f);
                                
                                // Возвращаемся к клиенту за свою стойку регистратуры
                                SetState(DirectorState.MovingToPoint);
                                yield return StartCoroutine(MoveToTargetRoutine(currentWorkstation.clerkStandPoint.position));
                                SetState(DirectorState.ServingClient);
                                
                                stackHolder?.HideStack();
                                client.billToPay += 150;
                                destination = ClientSpawner.GetCashierZone()?.waitingWaypoint;
                                leavingUpset = false;
                            }
                            else
                            {
                                 thoughtBubble?.ShowPriorityMessage("А где архив?!", 2f, Color.red);
                                 destination = ClientSpawner.Instance?.exitWaypoint;
                                 leavingUpset = true;
                            }
                            break;
                        default:
                            client.isLeavingSuccessfully = true;
                            client.reasonForLeaving = ClientPathfinding.LeaveReason.Processed;
                            destination = ClientSpawner.Instance?.exitWaypoint;
                            break;
                    }
                }

                if (destination != null)
                {
                    string destinationName = string.IsNullOrEmpty(destination.friendlyName) ? destination.name : destination.friendlyName;
                    if (!leavingUpset) thoughtBubble?.ShowPriorityMessage($"Пройдите к\n'{destinationName}'", 3f, Color.white);

                    if (client.stateMachine.MyQueueNumber != -1)
                    {
                        ClientQueueManager.Instance?.RemoveClientFromQueue(client);
                    }

                    client.stateMachine.SetGoal(destination);
                    client.stateMachine.SetState(leavingUpset ? ClientState.LeavingUpset : ClientState.MovingToGoal);
                    Debug.Log($" -> Клиент {client.name} отправлен к {destinationName}.");
                }
                else
                {
                    thoughtBubble?.ShowPriorityMessage("Не могу вас направить.\nИзвините.", 3f, Color.red);
                    yield return new WaitForSeconds(1.5f);
                    if (client.stateMachine != null && client.stateMachine.MyQueueNumber != -1)
                    {
                        ClientQueueManager.Instance?.RemoveClientFromQueue(client);
                    }
                    client.reasonForLeaving = ClientPathfinding.LeaveReason.Upset;
                    client.stateMachine?.SetGoal(ClientSpawner.Instance?.exitWaypoint);
                    client.stateMachine?.SetState(ClientState.LeavingUpset);
                    Debug.LogError($"Не найдена точка назначения для клиента {client.name}.");
                }
                jobDone = true;
            }
        }
        // --- Логика для Кассы (ID -1 или 4) ---
        else if (deskId == -1)
        {
            thoughtBubble?.ShowPriorityMessage("Принимаю оплату...", 2f, Color.yellow);
            yield return new WaitForSeconds(Random.Range(2.0f, 3.5f));

            if (client == null || client.stateMachine == null)
            {
                Debug.LogWarning("Клиент исчез во время ожидания оплаты.");
                jobDone = false;
            }
            else
            {
                if (client.billToPay == 0 && client.mainGoal == ClientGoal.PayTax)
                {
                    client.billToPay = Random.Range(20, 121);
                    Debug.Log($" -> Назначен счет за налог для {client.name}: ${client.billToPay}");
                }

                if (PlayerWallet.Instance != null && client.billToPay > 0)
                {
                    if (client.moneyPrefab != null)
                    {
                        GameObject moneyEffect = Instantiate(client.moneyPrefab, client.transform.position + Vector3.up, Quaternion.identity);
                        MoneyMover mover = moneyEffect.GetComponent<MoneyMover>();
                        Transform moneyTarget = currentWorkstation?.moneyTrayPoint ?? currentWorkstation?.transform ?? this.transform;
                        if (mover != null) mover.StartMove(moneyTarget);
                        else Destroy(moneyEffect);
                    }

                    PlayerWallet.Instance.AddMoney(client.billToPay, $"Оплата услуги ({client.name})", IncomeType.Official);
                    Debug.Log($" -> Получена оплата от {client.name}: ${client.billToPay}");

                    if (client.paymentSound != null) AudioSource.PlayClipAtPoint(client.paymentSound, transform.position);
                    client.billToPay = 0;
                    jobDone = true;
                }
                else if (client.billToPay <= 0)
                {
                    Debug.Log($" -> У клиента {client.name} нет счета для оплаты.");
                    jobDone = true;
                }

                client.isLeavingSuccessfully = true;
                client.reasonForLeaving = ClientPathfinding.LeaveReason.Processed;
                client.stateMachine?.SetGoal(ClientSpawner.Instance?.exitWaypoint);
                client.stateMachine?.SetState(ClientState.Leaving);
                Debug.Log($" -> Клиент {client.name} отправлен на выход после кассы.");
            }
        }
        else
        {
            Debug.LogError($"DirectorServiceRoutine: Неизвестный deskId = {deskId} для {currentWorkstation.name}!");
            if (client != null && client.stateMachine != null)
            {
                client.reasonForLeaving = ClientPathfinding.LeaveReason.Upset;
                client.stateMachine?.SetGoal(ClientSpawner.Instance?.exitWaypoint);
                client.stateMachine?.SetState(ClientState.LeavingUpset);
            }
        }

        // --- Завершение обслуживания ---
        if (jobDone && currentWorkstation?.documentStack != null)
        {
            currentWorkstation.documentStack.AddDocumentToStack();
        }

        clientBeingServed = null;
        SetState(DirectorState.WorkingAtStation);
        Debug.Log($"[DirectorController] {characterName} завершил обслуживание {client?.name}.");
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
            thoughtBubble?.ShowPriorityMessage("Не успел...", 2f, Color.gray);
        }
    }
    #endregion
} // Конец класса DirectorAvatarController