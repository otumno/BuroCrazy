// Файл: Assets/Scripts/Characters/Controllers/DirectorAvatarController.cs
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

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

    /// <summary>
    /// Отправляет Директора к его основному рабочему столу (креслу).
    /// </summary>
    public void GoToDesk()
    {
        if (directorChairPoint != null)
        {
            // Находим ближайшую путевую точку к креслу
            var wp = FindNearestWaypointTo(directorChairPoint.position);
            if (wp != null)
            {
                 MoveToWaypoint(wp); // Используем стандартный метод движения к точке
            } else {
                 Debug.LogError("Не найдена путевая точка рядом с directorChairPoint!");
                 // Можно попробовать двигаться напрямую к креслу, но это может вызвать проблемы с навигацией
                 // StartCoroutine(MoveToTargetAndSetState(directorChairPoint.position, DirectorState.AtDesk));
            }
        } else {
             Debug.LogError("DirectorChairPoint не назначен! Невозможно отправить директора к столу.");
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
    private IEnumerator WorkAtStationRoutine(ServicePoint workstation)
    {
         if (workstation == null) {
              Debug.LogError("WorkAtStationRoutine: workstation is null!");
              isManuallyWorking = false; // Сбрасываем флаг, если станция невалидна
              yield break;
         }

        currentWorkstation = workstation;
        // Регистрируем Директора как поставщика услуг на этом столе
        ClientSpawner.AssignServiceProviderToDesk(this, workstation.deskId);
        isManuallyWorking = true; // Устанавливаем флаг ручной работы
         Debug.Log($"[DirectorController] {characterName} назначен на {workstation.name} (ID: {workstation.deskId}). Начало движения...");


        // Двигаемся к точке ожидания сотрудника на этой станции
        if (workstation.clerkStandPoint != null) {
            yield return StartCoroutine(MoveToTargetRoutine(workstation.clerkStandPoint.position)); // <<< ИЗМЕНЕНИЕ: Убран второй аргумент
        } else {
             Debug.LogError($"У workstation {workstation.name} не назначен clerkStandPoint!");
             StopManualWork(false); // Отменяем работу, если точка не найдена
             yield break;
        }


        // Устанавливаем состояние "Работает на станции"
        SetState(DirectorState.WorkingAtStation);
        Debug.Log($"[DirectorController] {characterName} прибыл и работает на {workstation.name}.");

        // Цикл ожидания и обслуживания клиентов
        while (isManuallyWorking) // Продолжаем, пока флаг ручной работы активен
        {
            // Находим зону, к которой относится наша станция
            var zone = ClientSpawner.GetZoneByDeskId(workstation.deskId);
            // Ищем первого клиента, который занял место в этой зоне
            var clientToServe = zone?.GetOccupyingClients().FirstOrDefault();

            // Если есть клиент для обслуживания и мы сейчас не обслуживаем другого
            if (clientToServe != null && clientBeingServed == null)
            {
                 // Проверяем, что клиент действительно ждет обслуживания в этой зоне
                  if (clientToServe.stateMachine != null && clientToServe.stateMachine.GetTargetZone() == zone) {
                    // Запускаем корутину обслуживания этого клиента
                    yield return StartCoroutine(DirectorServiceRoutine(clientToServe));
                 } else {
                      // Клиент есть в зоне, но его цель другая (редкий случай)
                      Debug.LogWarning($"{characterName} видит {clientToServe.name} в зоне {zone.name}, но цель клиента другая ({clientToServe.stateMachine?.GetTargetZone()?.name}).");
                 }

            }

            // Небольшая пауза перед следующей проверкой
            yield return new WaitForSeconds(0.5f);
        }
         Debug.Log($"[DirectorController] {characterName} закончил ручную работу на {workstation?.name}.");
    }

    /// <summary>
    /// Корутина для перемещения Директора к цели с использованием PathfindingUtility.
    /// </summary>
    // <<< ИЗМЕНЕНИЕ: Убран stateAfterArrival >>>
    private IEnumerator MoveToTargetRoutine(Vector2 targetPosition)
    {
        SetState(DirectorState.MovingToPoint); // Устанавливаем состояние "Движется к точке"
        Queue<Waypoint> path = PathfindingUtility.BuildPathTo(transform.position, targetPosition, gameObject);
        if (path != null && path.Count > 0)
        {
            if (agentMover != null)
            {
                agentMover.SetPath(path);
                Debug.Log($"[MoveToTargetRoutine] Движение начато к {targetPosition}. Ожидание завершения..."); // <<< ДОБАВЛЕН ЛОГ
                yield return new WaitUntil(() => agentMover == null || !agentMover.IsMoving());
                Debug.Log($"[MoveToTargetRoutine] Движение к {targetPosition} завершено."); // <<< ДОБАВЛЕН ЛОГ
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
         Debug.Log($"[DirectorController] {characterName} State Change: {currentState} -> {newState}");
        currentState = newState;
        // Обновляем эмоцию через CharacterVisuals, если он есть
        visuals?.SetEmotionForState(newState);
    }

    /// <summary>
    /// Корутина для похода к барьеру и его переключения.
    /// </summary>
    private IEnumerator OperateBarrierRoutine()
    {
        SetUninterruptible(true); // Блокируем другие действия
        var barrier = SecurityBarrier.Instance;
        // Проверяем наличие барьера и точки взаимодействия
        if (barrier == null || barrier.guardInteractionPoint == null)
        {
            Debug.LogError("SecurityBarrier или его guardInteractionPoint не найдены!");
            SetUninterruptible(false); // Снимаем блокировку
            yield break; // Выходим
        }

        // Двигаемся к точке взаимодействия
        yield return StartCoroutine(MoveToTargetRoutine(barrier.guardInteractionPoint.position)); // <<< ИЗМЕНЕНИЕ: Убран второй аргумент
        SetState(DirectorState.MovingToPoint); // Устанавливаем состояние после движения

        // Небольшая пауза для имитации действия
        yield return new WaitForSeconds(1.5f);

        // Переключаем состояние барьера
        barrier.ToggleBarrier();

        SetState(DirectorState.Idle); // Возвращаемся в состояние бездействия
        SetUninterruptible(false); // Снимаем блокировку
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
         // Проверки на null
         if (client == null || client.stateMachine == null || currentWorkstation == null) {
              Debug.LogError($"DirectorServiceRoutine: client ({client?.name}) или currentWorkstation ({currentWorkstation?.name}) равен null!");
              clientBeingServed = null; // Освобождаем директора
              SetState(DirectorState.WorkingAtStation); // Возвращаемся в ожидание
              yield break;
         }

        // Проверяем, не заняты ли мы уже другим клиентом (на всякий случай)
        if (clientBeingServed != null && clientBeingServed != client)
        {
             Debug.LogWarning($"DirectorServiceRoutine: Попытка обслужить {client.name}, когда уже обслуживается {clientBeingServed.name}.");
            yield break; // Выходим, если уже заняты
        }

        clientBeingServed = client; // Запоминаем клиента, которого обслуживаем
        SetState(DirectorState.ServingClient); // Устанавливаем состояние "Обслуживает клиента"
        Debug.Log($"<color=#00FFFF>ДИРЕКТОР:</color> {characterName} начал обслуживание {client.name} (Цель: {client.mainGoal}) на {currentWorkstation.name} (ID: {currentWorkstation.deskId}).");

        int deskId = currentWorkstation.deskId; // Получаем ID стола
        bool jobDone = false; // Флаг успешного завершения основной задачи
        GameObject flyingDoc = null; // Переменная для анимации документа

        // --- Логика для столов Клерков (ID 1 и 2) ---
        if (deskId == 1 || deskId == 2)
        {
            thoughtBubble?.ShowPriorityMessage("Так... посмотрим...", 2f, Color.yellow);
            yield return new WaitForSeconds(1.0f);

            // Определяем нужный тип бланка и сертификата для этого стола
            DocumentType requiredDocType = (deskId == 1) ? DocumentType.Form1 : DocumentType.Form2;
            GameObject requiredPrefab = (deskId == 1) ? form1Prefab : form2Prefab; // Не используется напрямую, но для справки
             GameObject certificatePrefab = (deskId == 1) ? certificate1Prefab : certificate2Prefab;
            DocumentType certificateType = (deskId == 1) ? DocumentType.Certificate1 : DocumentType.Certificate2;
            int serviceCost = (deskId == 1) ? 100 : 250;

            // 1. Проверяем, правильный ли документ у клиента
            if (client.docHolder == null) { // Доп. проверка
                 Debug.LogError($"У клиента {client.name} отсутствует DocumentHolder!");
            }
             else if (client.docHolder.GetCurrentDocumentType() != requiredDocType)
            {
                // Не тот бланк - отправляем клиента за новым
                thoughtBubble?.ShowPriorityMessage("У вас бланк не тот!\nВозьмите другой.", 3f, Color.red);
                yield return new WaitForSeconds(1.5f);
                if (client != null && client.stateMachine != null) client.stateMachine.GoGetFormAndReturn(); // Безопасный вызов
                jobDone = true; // Считаем действие выполненным (клиент отправлен)
                 Debug.Log($" -> Клиент {client.name} отправлен за другим бланком.");
            }
            else // Бланк правильный
            {
                 Debug.Log($" -> У клиента {client.name} правильный бланк ({requiredDocType}). Начинаем обработку...");
                // --- Анимация: Забираем документ у клиента ---
                DocumentHolder clientDocHolder = client.docHolder;
                Transform clientHand = clientDocHolder?.handPoint;
                Transform deskPoint = currentWorkstation.documentPointOnDesk;
                GameObject currentClientDocObject = (clientHand != null && clientHand.childCount > 0) ? clientHand.GetChild(0).gameObject : null;

                if (currentClientDocObject != null && deskPoint != null)
                 {
                    clientDocHolder.SetDocument(DocumentType.None); // Убираем документ из данных
                    DocumentMover mover = currentClientDocObject.AddComponent<DocumentMover>();
                    bool arrived = false;
                    mover.StartMove(deskPoint, () => { arrived = true; });
                    yield return new WaitUntil(() => arrived);
                    flyingDoc = currentClientDocObject; // Запоминаем документ на столе
                      // Проверка, что объект еще существует
                     if (flyingDoc != null) {
                         flyingDoc.transform.SetParent(deskPoint);
                         flyingDoc.transform.localPosition = Vector3.zero;
                         flyingDoc.transform.localRotation = Quaternion.identity;
                          Debug.Log($" -> Документ {flyingDoc.name} перемещен на стол.");
                     } else {
                          Debug.LogWarning($" -> Документ клиента исчез во время перемещения на стол.");
                     }
                } else {
                      Debug.LogWarning($" -> Не удалось анимировать забор документа у {client.name} (объект/точки не найдены).");
                }
                 // --- Конец анимации забора ---

                thoughtBubble?.ShowPriorityMessage("Обрабатываю...", 3f, Color.white);
                yield return new WaitForSeconds(Random.Range(2.5f, 4.0f)); // Время обработки

                 // Проверяем клиента и стол перед выдачей
                 if (client == null || client.stateMachine == null || currentWorkstation == null) {
                     Debug.LogWarning("Клиент или рабочее место исчезли во время обработки документа.");
                     if (flyingDoc != null) Destroy(flyingDoc); // Убираем документ со стола
                     jobDone = false; // Задача не выполнена
                 } else {
                     // --- Анимация: Выдаем сертификат ---
                     if (flyingDoc != null) Destroy(flyingDoc); // Уничтожаем старый документ на столе

                     deskPoint = currentWorkstation.documentPointOnDesk; // Переполучаем на всякий случай
                     clientHand = client.docHolder?.handPoint;

                     if (certificatePrefab != null && deskPoint != null && clientHand != null)
                     {
                         GameObject newCertGO = Instantiate(certificatePrefab, deskPoint.position, deskPoint.rotation);
                         DocumentMover mover = newCertGO.AddComponent<DocumentMover>();
                         bool arrived = false;
                         mover.StartMove(clientHand, () => {
                             // По прибытии вызываем метод у клиента
                              // Проверяем клиента еще раз перед передачей
                              if (client != null && client.docHolder != null) {
                                 client.docHolder.ReceiveTransferredDocument(certificateType, newCertGO);
                                 Debug.Log($" -> Сертификат {newCertGO.name} передан клиенту {client.name}.");
                              } else {
                                   Debug.LogWarning($" -> Клиент {client?.name} исчез перед получением сертификата. Уничтожаем сертификат.");
                                   Destroy(newCertGO); // Уничтожаем, если клиент ушел
                              }
                              arrived = true;
                         });
                         yield return new WaitUntil(() => arrived);
                     } else {
                          // Если анимация невозможна, просто даем документ
                          Debug.LogWarning($" -> Не удалось анимировать выдачу сертификата клиенту {client.name}. Документ выдан без анимации.");
                          if (client != null && client.docHolder != null) client.docHolder.SetDocument(certificateType);
                     }
                     // --- Конец анимации выдачи ---

                      // Выставляем счет, если клиент еще здесь
                      if (client != null && client.stateMachine != null) {
                         client.billToPay += serviceCost;
                         thoughtBubble?.ShowPriorityMessage("Готово! Теперь в кассу.", 3f, Color.green);
                          Debug.Log($" -> Клиент {client.name} отправлен в кассу (Счет: {client.billToPay}).");
                         // Отправляем в кассу
                         client.stateMachine.SetGoal(ClientSpawner.GetCashierZone()?.waitingWaypoint);
                         client.stateMachine.SetState(ClientState.MovingToGoal);
                      }
                     jobDone = true; // Считаем работу выполненной
                 }
            }
        }
         // --- Логика для Регистратуры (ID 0) ---
        else if (deskId == 0)
        {
            thoughtBubble?.ShowPriorityMessage("Смотрю, куда вас направить...", 2f, Color.cyan);
            yield return new WaitForSeconds(Random.Range(1.0f, 2.0f));

            // Проверяем клиента перед направлением
             if (client == null || client.stateMachine == null) {
                 Debug.LogWarning("Клиент исчез во время обработки в регистратуре.");
                 jobDone = false;
             } else {
                 Waypoint destination = null;
                 bool leavingUpset = false;

                 // Определяем пункт назначения
                 if (client.billToPay > 0) { destination = ClientSpawner.GetCashierZone()?.waitingWaypoint; }
                 else
                 {
                     switch (client.mainGoal)
                     {
                         case ClientGoal.PayTax: destination = ClientSpawner.GetCashierZone()?.waitingWaypoint; break;
                         case ClientGoal.GetCertificate1: destination = ClientSpawner.GetDesk1Zone()?.waitingWaypoint; break;
                         case ClientGoal.GetCertificate2: destination = ClientSpawner.GetDesk2Zone()?.waitingWaypoint; break;
                         case ClientGoal.GetArchiveRecord:
                             // Если цель - архив, но у директора нет Action'а
                             thoughtBubble?.ShowPriorityMessage("Архив недоступен.\nИзвините.", 3f, Color.red);
                             yield return new WaitForSeconds(1.5f);
                             destination = ClientSpawner.Instance?.exitWaypoint;
                             client.reasonForLeaving = ClientPathfinding.LeaveReason.Upset;
                             leavingUpset = true; // Ставим флаг для смены состояния
                             break;
                         default: // AskAndLeave, VisitToilet (уже здесь), etc.
                              client.isLeavingSuccessfully = true;
                              client.reasonForLeaving = ClientPathfinding.LeaveReason.Processed;
                             destination = ClientSpawner.Instance?.exitWaypoint;
                              break;
                     }
                 }

                 // Проверяем, что точка назначения найдена
                 if (destination != null) {
                     string destinationName = string.IsNullOrEmpty(destination.friendlyName) ? destination.name : destination.friendlyName;
                     // Показываем сообщение только если не отправляем домой из-за архива
                     if (!leavingUpset) thoughtBubble?.ShowPriorityMessage($"Пройдите к\n'{destinationName}'", 3f, Color.white);

                     // Снимаем с очереди
                     if (client.stateMachine.MyQueueNumber != -1) { ClientQueueManager.Instance?.RemoveClientFromQueue(client); }

                     // Устанавливаем цель и состояние
                     client.stateMachine.SetGoal(destination);
                      client.stateMachine.SetState(leavingUpset ? ClientState.LeavingUpset : ClientState.MovingToGoal);
                      Debug.Log($" -> Клиент {client.name} отправлен к {destinationName} (Состояние: {(leavingUpset ? ClientState.LeavingUpset : ClientState.MovingToGoal)}).");
                 } else {
                      // Если точка не найдена (ошибка конфигурации)
                      thoughtBubble?.ShowPriorityMessage("Не могу вас направить.\nИзвините.", 3f, Color.red);
                      yield return new WaitForSeconds(1.5f);
                      if (client.stateMachine.MyQueueNumber != -1) { ClientQueueManager.Instance?.RemoveClientFromQueue(client); }
                      client.reasonForLeaving = ClientPathfinding.LeaveReason.Upset;
                      client.stateMachine.SetGoal(ClientSpawner.Instance?.exitWaypoint); // Безопасный доступ
                      client.stateMachine.SetState(ClientState.LeavingUpset);
                       Debug.LogError($"Не найдена точка назначения для клиента {client.name} (Цель: {client.mainGoal}). Отправлен домой.");
                 }
                 jobDone = true; // Считаем действие выполненным
             }
        }
        // --- Логика для Кассы (ID -1 или 4) ---
        else if (deskId == -1 || deskId == 4)
        {
             thoughtBubble?.ShowPriorityMessage("Принимаю оплату...", 2f, Color.yellow);
             yield return new WaitForSeconds(Random.Range(2.0f, 3.5f));

              // Проверяем клиента перед оплатой
              if (client == null || client.stateMachine == null) {
                  Debug.LogWarning("Клиент исчез во время ожидания оплаты.");
                  jobDone = false;
              } else {
                  // Назначаем счет за налог, если нужно
                 if (client.billToPay == 0 && client.mainGoal == ClientGoal.PayTax)
                 {
                     client.billToPay = Random.Range(20, 121);
                      Debug.Log($" -> Назначен счет за налог для {client.name}: ${client.billToPay}");
                 }

                 // Обрабатываем оплату
                 if (PlayerWallet.Instance != null && client.billToPay > 0)
                 {
                     // --- Анимация денег ---
                     if (client.moneyPrefab != null && PlayerWallet.Instance.moneyText != null)
                     {
                         GameObject moneyEffect = Instantiate(client.moneyPrefab, client.transform.position + Vector3.up, Quaternion.identity);
                         MoneyMover mover = moneyEffect.GetComponent<MoneyMover>();
                         if (mover != null) mover.StartMove(PlayerWallet.Instance.moneyText.transform);
                         else Destroy(moneyEffect);
                     }
                     // ---

                     PlayerWallet.Instance.AddMoney(client.billToPay, $"Оплата услуги Директором ({client.name})", IncomeType.Official);
                      Debug.Log($" -> Получена оплата от {client.name}: ${client.billToPay}");

                     if (client.paymentSound != null) AudioSource.PlayClipAtPoint(client.paymentSound, transform.position);
                     client.billToPay = 0; // Обнуляем счет
                     jobDone = true; // Считаем оплату успешной
                 } else if (client.billToPay <= 0) {
                      Debug.Log($" -> У клиента {client.name} нет счета для оплаты.");
                      jobDone = true; // Считаем выполненным, т.к. оплаты и не требовалось
                 }

                 // Отправляем клиента на выход
                 client.isLeavingSuccessfully = true;
                 client.reasonForLeaving = ClientPathfinding.LeaveReason.Processed;
                 client.stateMachine.SetGoal(ClientSpawner.Instance?.exitWaypoint); // Безопасный доступ
                 client.stateMachine.SetState(ClientState.Leaving);
                  Debug.Log($" -> Клиент {client.name} отправлен на выход после кассы.");
              }
        }
        else // Неизвестный deskId
        {
             Debug.LogError($"DirectorServiceRoutine: Неизвестный deskId = {deskId} для {currentWorkstation.name}!");
             // Можно отправить клиента домой или просто завершить без jobDone = true
              if (client != null && client.stateMachine != null) {
                 client.reasonForLeaving = ClientPathfinding.LeaveReason.Upset;
                 client.stateMachine.SetGoal(ClientSpawner.Instance?.exitWaypoint);
                 client.stateMachine.SetState(ClientState.LeavingUpset);
              }
        }


        // --- Завершение обслуживания ---
        if (jobDone && currentWorkstation?.documentStack != null)
        {
            // Добавляем "обработанный" документ в стопку на столе, если основная задача была выполнена
            currentWorkstation.documentStack.AddDocumentToStack();
        }

        clientBeingServed = null; // Освобождаем директора для следующего клиента
        SetState(DirectorState.WorkingAtStation); // Возвращаемся в состояние ожидания на станции
         Debug.Log($"[DirectorController] {characterName} завершил обслуживание {client?.name} на {currentWorkstation?.name}.");
    } // Конец DirectorServiceRoutine
    #endregion
} // Конец класса DirectorAvatarController