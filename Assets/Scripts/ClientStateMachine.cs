using UnityEngine;
using System.Collections;
using System.Linq;
using Managers;
using Utilities;
using Characters;

[RequireComponent(typeof(ClientMessGenerator))]
[RequireComponent(typeof(ClientActionExecutor))]
[RequireComponent(typeof(ClientFeedbackController))]
[RequireComponent(typeof(ActionDiary))]
public class ClientStateMachine : MonoBehaviour
{
    // --- Components ---
    private ClientPathfinding parent;
    private CharacterStateLogger logger;
    private ActionDiary actionDiary;
    
    // --- Modules ---
    private ClientMessGenerator messGenerator;
    private ClientActionExecutor actionExecutor;
    private ClientFeedbackController feedbackController;

    [Header("Settings")]
    [SerializeField] private float zonePatienceTime = 15f;
    [SerializeField] private float rageDuration = 10f;

    // --- State Data ---
    private ClientState currentState = ClientState.Spawning;
    public ClientState GetCurrentState() => currentState;

    private Waypoint currentGoal, previousGoal;
    public Waypoint GetCurrentGoal() => currentGoal;
    
    private LimitedCapacityZone targetZone; // Зона, в которую пытаемся войти или уже вошли
    public LimitedCapacityZone GetTargetZone() => targetZone;
    
    private Waypoint occupiedWaypoint = null; // Место, которое мы физически заняли (стул/стойка)
    private LimitedCapacityZone zoneToReturnTo; // Для возврата за бланком
    
    // --- ИСПРАВЛЕНИЕ: Вернули переменную seatTarget ---
    private Transform seatTarget; 

    // --- Service Data ---
    private int myQueueNumber = -1;
    public int MyQueueNumber => myQueueNumber;
    private IServiceProvider myServiceProvider;
    public IServiceProvider MyServiceProvider => myServiceProvider;

    // --- Routine Handles ---
    private Coroutine mainActionCoroutine;
    private Coroutine zonePatienceCoroutine;

    public void Initialize(ClientPathfinding p)
    {
        parent = p;
        logger = GetComponent<CharacterStateLogger>();
        actionDiary = GetComponent<ActionDiary>();

        // Init Modules
        messGenerator = GetComponent<ClientMessGenerator>();
        messGenerator.Initialize(parent);

        actionExecutor = GetComponent<ClientActionExecutor>();
        actionExecutor.Initialize(parent);

        feedbackController = GetComponent<ClientFeedbackController>();
        feedbackController.Initialize(parent);

        if (parent.movement == null) { enabled = false; return; }

        StartCoroutine(MainLogicLoop());
		
		StartCoroutine(GlobalPatienceMonitor());
    }
	
	private IEnumerator GlobalPatienceMonitor()
    {
        float checkInterval = 0.2f; // Проверяем 5 раз в секунду
        var wait = new WaitForSeconds(checkInterval);
        
        while (true)
        {
            yield return wait;

            if (currentState == ClientState.Grumbling) continue;

            // Если уходим - стоп
            if (IsLeavingState(currentState) || currentState == ClientState.PassedRegistration ||
                currentState == ClientState.Enraged || currentState == ClientState.Confused) yield break;

            // 1. Базовая скорость накопления зависит от состояния
            float currentMultiplier = parent.stressMod_Standing; // База (1.0)

            switch (currentState)
            {
                case ClientState.SittingInWaitingArea:
                    currentMultiplier = parent.stressMod_Sitting; // Сидит (0.5)
                    break;

                // Состояния "При деле" (почти не злится)
                case ClientState.AtRegistration: 
                case ClientState.AtDesk1:        
                case ClientState.AtDesk2:
                case ClientState.AtCashier:
                case ClientState.InsideLimitedZone: 
                case ClientState.MovingToSeat:      
                case ClientState.ReturningToWait:   
                case ClientState.WaitingForDocument: 
                    currentMultiplier = parent.stressMod_BusyOrServed; // (0.1)
                    break;
                
                case ClientState.MovingToGoal:
                    // Если идет к врачу - спокоен, если просто в очередь - стрессует
                    currentMultiplier = (targetZone != null) ? parent.stressMod_BusyOrServed : parent.stressMod_Standing;
                    break;
            }

            // 1.5 Наглецы закипают быстрее!
            if (parent.isQueueJumper)
            {
                float jumperMult = Gameplay.AIBalanceConfig.Instance != null ? Gameplay.AIBalanceConfig.Instance.jumperStressMultiplier : 2.5f;
                currentMultiplier *= jumperMult;
            }

            // 2. Проверка окружения (Мусор) - только если стоит и ждет
            if (currentState == ClientState.AtWaitingArea || currentState == ClientState.SittingInWaitingArea)
            {
                if (MessManager.Instance != null)
                {
                    // Упрощенная проверка: берем ближайшие объекты грязи
                    int nearbyMessCount = MessManager.Instance.GetSortedMessList(transform.position)
                        .Take(5) // Проверяем только 5 ближайших
                        .Count(m => m != null && Vector2.Distance(transform.position, m.transform.position) < 3f);

                    if (nearbyMessCount > 0)
                    {
                        float messPenalty = nearbyMessCount * parent.stressAdd_NearbyMess;
                        if (parent.suetunFactor > 0.5f) messPenalty *= 1.5f; // Суетуны ненавидят грязь
                        currentMultiplier += messPenalty;
                    }
                }
            }

            // 3. Применяем стресс
            parent.AddStress(checkInterval * currentMultiplier);

            // 4. Проверка перехода в Grumbling (промежуточное недовольство)
            if (currentState != ClientState.Grumbling && parent.canGrumble &&
                parent.PatienceHeat >= parent.GetEffectiveGrumblingThreshold() && parent.PatienceHeat < 1.0f)
            {
                Debug.Log($"<color=yellow>[ClientStateMachine]</color> {parent.name}: Начинает ворчать ({parent.PatienceHeat:P0})!");
                SetState(ClientState.Grumbling);
                yield break;
            }

            // 5. Проверка срыва (100%)
            if (parent.PatienceHeat >= 1.0f)
            {
                Debug.Log($"<color=red>[ClientStateMachine]</color> {parent.name}: Терпение лопнуло (100%)!");
                HandlePatienceExhausted();
                yield break;
            }
        }
    }
	
	private void HandlePatienceExhausted()
	   {
	       // ЗАПРЕТ УХОДА ВО ВРЕМЯ ОБСЛУЖИВАНИЯ
	       if (IsInsideZoneState(currentState))
	       {
	           // Клиент в процессе обслуживания — показываем гнев, но не меняем состояние
	           Debug.Log($"[ClientStateMachine] {parent.name}: Терпение на исходе, но в процессе обслуживания!");
	           return;
	       }
	       
	       StopAllActionCoroutines();

	       if (Random.value < 0.4f) // 40% шанс скандала
	       {
	           SetState(ClientState.Enraged);
	       }
	       else
	       {
	           parent.reasonForLeaving = ClientPathfinding.LeaveReason.Upset;
	           SetGoal(ClientSpawner.Instance.exitWaypoint);
	           SetState(ClientState.LeavingUpset);
	       }
	   }
	

    public IEnumerator MainLogicLoop()
    {
        yield return new WaitForSeconds(0.1f);
        while (true)
        {
            if (parent.notification != null) parent.notification.UpdateNotification();
            
            // Если текущее действие завершилось, запускаем новое в зависимости от состояния
            if (mainActionCoroutine == null)
            {
                mainActionCoroutine = StartCoroutine(HandleCurrentState());
            }
            yield return null;
        }
    }

	public float GetNormalizedProgress()
    {
        // Если уже уходим (успешно) - 100%
        if (currentState == ClientState.Leaving && parent.reasonForLeaving == ClientPathfinding.LeaveReason.Processed) return 1f;
        // Если уходим злыми - прогресс останавливается на том, где был (или 0, как решишь)
        if (currentState == ClientState.Leaving || currentState == ClientState.LeavingUpset) return 0f;

        switch (parent.mainGoal)
        {
            case ClientGoal.AskAndLeave:
                // Пришел -> Регистратура -> Ушел
                if (currentState == ClientState.Spawning || currentState == ClientState.MovingToGoal) return 0.1f;
                if (currentState == ClientState.AtRegistration) return 0.5f; // В процессе разговора
                if (currentState == ClientState.Leaving) return 1f;
                return 0.1f;

            case ClientGoal.GetCertificate1:
            case ClientGoal.GetCertificate2:
                // Пришел(0) -> Регистратура(33) -> Бланк/Ожидание(66) -> Клерк(100)
                
                // Этап 1: До регистрации
                if (currentState == ClientState.Spawning || currentState == ClientState.MovingToGoal && targetZone == null) return 0.0f;
                
                // Этап 2: Регистрация (направление)
                if (currentState == ClientState.AtRegistration || currentState == ClientState.AtLimitedZoneEntrance) return 0.33f;

                // Этап 3: Получение бланка / Ожидание в очереди к клерку
                // Если мы уже прошли регистрацию и сидим ждем или идем за бланком
                if (currentState == ClientState.MovingToSeat || currentState == ClientState.SittingInWaitingArea || 
                    currentState == ClientState.AtWaitingArea || currentState == ClientState.WaitingForDocument) return 0.66f;

                // Этап 4: У стола клерка (или внутри зоны клерка)
                if (currentState == ClientState.AtDesk1 || currentState == ClientState.AtDesk2 || 
                    (currentState == ClientState.InsideLimitedZone && (targetZone == ClientSpawner.GetDesk1Zone() || targetZone == ClientSpawner.GetDesk2Zone()))) 
                    return 0.9f; // Почти готово

                // Этап 5: Касса (это уже 100% выполнения услуги, осталась оплата)
                if (currentState == ClientState.AtCashier || currentState == ClientState.GoingToCashier || parent.billToPay > 0) return 1f;

                return 0.1f;

            case ClientGoal.PayTax:
                // Сразу в кассу: Пришел(0) -> Касса(100)
                if (currentState == ClientState.AtCashier || currentState == ClientState.GoingToCashier) return 1f;
                return 0.1f;
                
            case ClientGoal.GetArchiveRecord:
                 if (currentState == ClientState.WaitingForDocument) return 0.5f;
                 if (parent.billToPay > 0) return 1f;
                 return 0.1f;

            default:
                return 0f;
        }
    }


    // --- ИСПРАВЛЕНИЕ: Вернули метод StopAllActionCoroutines (public, т.к. нужен в ClientPathfinding) ---
    public void StopAllActionCoroutines()
    {
        // 1. Останавливаем логические корутины
        if (mainActionCoroutine != null) StopCoroutine(mainActionCoroutine);
        mainActionCoroutine = null;
        
        if (zonePatienceCoroutine != null) StopCoroutine(zonePatienceCoroutine);
        zonePatienceCoroutine = null;

        // 2. Останавливаем физического исполнителя
        if (actionExecutor != null)
        {
            actionExecutor.StopAllActions();
        }
    }

    // Основная развилка логики
    private IEnumerator HandleCurrentState()
    {
        switch (currentState)
        {
            case ClientState.Spawning:
                DecideInitialGoal();
                break;

            // Аппарат талонов
            case ClientState.MovingToTerminal:
                yield return StartCoroutine(actionExecutor.MoveToGoalRoutine(currentGoal));
                SetState(ClientState.GettingTicket);
                break;
            case ClientState.GettingTicket:
                yield return StartCoroutine(GetTicketRoutine());
                break;

            // Группа состояний "Движение"
            case ClientState.MovingToGoal:
            case ClientState.GoingToCashier:
            case ClientState.Leaving:
            case ClientState.LeavingUpset:
            case ClientState.MovingToRegistrarImpolite:
            case ClientState.ReturningToRegistrar:
                yield return StartCoroutine(actionExecutor.MoveToGoalRoutine(currentGoal));
                HandleMovementArrival(); // Прибыли, решаем что дальше
                break;

            case ClientState.MovingToSeat:
                // Используем seatTarget, который теперь объявлен
                yield return StartCoroutine(actionExecutor.MoveToSeatRoutine(seatTarget));
                SetState(ClientState.SittingInWaitingArea);
                break;

            case ClientState.AtLimitedZoneEntrance:
                // Запускаем мониторинг терпения параллельно
                if (zonePatienceCoroutine != null) StopCoroutine(zonePatienceCoroutine);
                //zonePatienceCoroutine = StartCoroutine(PatienceMonitorForZone(targetZone));
                
                // Делегируем сложную логику входа экзекьютору
                yield return StartCoroutine(actionExecutor.EnterZoneRoutine(targetZone));
                break;

            // Группа состояний "Ожидание обслуживания"
            case ClientState.AtRegistration:
            case ClientState.AtCashier:
            case ClientState.InsideLimitedZone:
            case ClientState.AtDesk1:
            case ClientState.AtDesk2:
                // Спец. логика для туалета
                if (targetZone == ClientSpawner.GetToiletZone())
                {
                    yield return StartCoroutine(HandleToiletVisitRoutine());
                    break;
                }
                // Пассивное ожидание. Сотрудник сам пнет нас дальше.
                yield return StartCoroutine(WaitForServiceRoutine());
                break;

            case ClientState.SittingInWaitingArea:
            case ClientState.AtWaitingArea:
                //ClientQueueManager.Instance.StartPatienceTimer(parent);
                // Если стоим - можем слоняться и разговаривать
                if (currentState == ClientState.AtWaitingArea) 
                    yield return StartCoroutine(MillAroundWithChatRoutine());
                else 
                    yield return StartCoroutine(WaitingInSeatWithChatRoutine());
                break;

            case ClientState.Confused:
                yield return StartCoroutine(ConfusedRoutine());
                break;

            case ClientState.Grumbling:
                yield return StartCoroutine(GrumblingRoutine());
                break;

            case ClientState.Enraged:
                yield return StartCoroutine(EnragedRoutine());
                break;
            
            case ClientState.WaitingForDocument:
                feedbackController.UpdateStateVisuals(currentState, parent.reasonForLeaving);
                yield return new WaitForSeconds(30f);
                if (currentState == ClientState.WaitingForDocument)
                {
                    Debug.LogWarning($"[ClientStateMachine] Клиент {parent.name} слишком долго ждал документ, переход в Confused");
                    SetState(ClientState.Confused);
                }
                break;

            case ClientState.PassedRegistration:
                yield return StartCoroutine(PassedRegistrationRoutine());
                break;

            default:
                Debug.LogWarning($"[ClientStateMachine] Необработанное состояние: {currentState} для клиента {parent.name}");
                SetState(ClientState.Confused);
                break;
        }

        mainActionCoroutine = null;
    }

    // --- Callbacks from Executor ---

    public void OnZoneEntryApproved(Waypoint spot, LimitedCapacityZone zone)
    {
        if (zonePatienceCoroutine != null) StopCoroutine(zonePatienceCoroutine);
        
        targetZone = zone;
        occupiedWaypoint = spot;
        SetGoal(spot);
        
        SetState(ClientState.MovingToGoal);
    }

    // --- State Transition Logic ---

    public void SetState(ClientState newState)
    {
        if (newState == currentState) return;

        CleanupOldState(currentState, newState);
        
        // --- ИСПРАВЛЕНИЕ: Используем централизованный метод остановки ---
        StopAllActionCoroutines();
        
        if (newState == ClientState.Confused)
        {
            previousGoal = currentGoal;
            feedbackController.PlayStateSound(ClientState.Confused);
        }

        currentState = newState;
        
        // --- Логирование в ActionDiary (только важные состояния) ---
        LogImportantStateChange(newState);
        
        logger?.LogState(GetStatusInfo());
        feedbackController.UpdateStateVisuals(newState, parent.reasonForLeaving);
    }

    /// <summary>
    /// Логирует важные изменения состояния клиента в ActionDiary
    /// </summary>
    private void LogImportantStateChange(ClientState newState)
    {
        // Используем безопасный вызов через ?. чтобы избежать NullReference
        switch (newState)
        {
            case ClientState.Spawning:
                actionDiary?.LogEvent("Заспавнен");
                break;
            case ClientState.MovingToTerminal:
                actionDiary?.LogEvent("Идёт к терминалу");
                break;
            case ClientState.GettingTicket:
                actionDiary?.LogEvent("Получает талон");
                break;
            case ClientState.MovingToRegistrarImpolite:
                actionDiary?.LogEvent("Направляется к регистратуре (без очереди)");
                break;
            case ClientState.MovingToGoal:
                actionDiary?.LogEvent("Движется к цели");
                break;
            case ClientState.AtRegistration:
                actionDiary?.LogEvent("Начал обслуживание у регистратора");
                break;
            case ClientState.PassedRegistration:
                actionDiary?.LogEvent("Прошёл регистратуру, получил направление");
                break;
            case ClientState.AtWaitingArea:
                actionDiary?.LogEvent("Вошёл в зону ожидания");
                break;
            case ClientState.SittingInWaitingArea:
                actionDiary?.LogEvent("Сел в зоне ожидания");
                break;
            case ClientState.AtDesk1:
            case ClientState.AtDesk2:
                actionDiary?.LogEvent("Обслуживается у клерка");
                break;
            case ClientState.GoingToCashier:
                actionDiary?.LogEvent("Идёт к кассе");
                break;
            case ClientState.AtCashier:
                actionDiary?.LogEvent("У кассы");
                break;
            case ClientState.AtToilet:
                actionDiary?.LogEvent("В туалете");
                break;
            case ClientState.Confused:
                actionDiary?.LogEvent("СБИЛСЯ С ПУТИ!");
                break;
            case ClientState.Grumbling:
                actionDiary?.LogEvent("Начал ворчать от нетерпения");
                break;
            case ClientState.Enraged:
                actionDiary?.LogEvent("ВЗБЕШЁН!");
                break;
            case ClientState.Leaving:
                // Безопасный доступ к parent.reasonForLeaving (LeaveReason enum)
                string reason = parent != null ? parent.reasonForLeaving.ToString() : "неизвестно";
                actionDiary?.LogEvent($"Покидает офис (причина: {reason})");
                break;
            case ClientState.LeavingUpset:
                actionDiary?.LogEvent("Уходит расстроенный");
                break;
            case ClientState.ReturningToWait:
                actionDiary.LogEvent("Возвращается в зону ожидания");
                break;
            case ClientState.WaitingForDocument:
                actionDiary.LogEvent("Ждёт документ");
                break;
        }
    }

    public void SetGoal(Waypoint g) => currentGoal = g;

    // --- Helper Logic ---

    private void DecideInitialGoal()
    {
        targetZone = null;
        
        // 1. Проверка на Наглеца (Пролазу)
        float jumperChance = Gameplay.AIBalanceConfig.Instance != null ? Gameplay.AIBalanceConfig.Instance.queueJumperChance : 0.1f;
        if (Random.value < jumperChance)
        {
            parent.isQueueJumper = true;
            parent.GetVisuals()?.SetEmotion(Emotion.Sly);
            // Звук убран отсюда, он проиграется при пересечении EntranceTrigger
            
            LimitedCapacityZone impoliteZone = null;
            switch (parent.mainGoal)
            {
                case ClientGoal.GetCertificate1: impoliteZone = ClientSpawner.GetDesk1Zone(); break;
                case ClientGoal.GetCertificate2: impoliteZone = ClientSpawner.GetDesk2Zone(); break;
                case ClientGoal.PayTax: impoliteZone = ClientSpawner.GetCashierZone(); break;
                case ClientGoal.AskAndLeave: impoliteZone = ClientSpawner.GetRegistrationZone(); break;
                case ClientGoal.VisitToilet: impoliteZone = ClientSpawner.GetToiletZone(); break;
            }

            if (impoliteZone != null && impoliteZone.waitingWaypoint != null)
            {
                // Наглец встает в систему (получит скрытый номер 10000+) и ВЛЕЗАЕТ БЕЗ ОЧЕРЕДИ
                ClientQueueManager.Instance.JoinQueue(parent);
                
                // Устанавливаем локальный номер очереди для наглеца
                if (ClientQueueManager.Instance.queue.TryGetValue(parent, out int queueNum))
                {
                    myQueueNumber = queueNum;
                }
                
                impoliteZone.JumpQueue(parent.gameObject);
                
                SetGoal(impoliteZone.waitingWaypoint);
                SetState(ClientState.MovingToGoal); // Идет стоять над душой у клерка
                
                // Показываем наглое сообщение в бабле
                string[] rudeMessages = {
                    "Мне только спросить!",
                    "Я требую немедленно!",
                    "У меня нет времени ждать!",
                    "Я тут главный!",
                    "Это займёт секунду!"
                };
                string rudeMsg = rudeMessages[Random.Range(0, rudeMessages.Length)];
                parent.ShowThoughtBubble(rudeMsg, 3f);
                
                return;
            }
            else
            {
                // Защита: если зоны нет, он становится обычным клиентом и идет за талоном
                parent.isQueueJumper = false;
            }
        }

        // 2. Честный клиент идет к терминалу
        if (Objects.TicketTerminal.Instance != null && Objects.TicketTerminal.Instance.GetStandWaypoint() != null)
        {
            SetGoal(Objects.TicketTerminal.Instance.GetStandWaypoint());
            SetState(ClientState.MovingToTerminal);
        }
        else
        {
            // Фоллбэк, если терминала нет на сцене
            Waypoint initialGoal = (parent.mainGoal == ClientGoal.VisitToilet)
                ? ClientSpawner.GetToiletZone().waitingWaypoint
                : ClientQueueManager.Instance.ChooseNewGoal(parent);

            if (initialGoal != null)
            {
                SetGoal(initialGoal);
                SetState(ClientState.MovingToGoal);
            }
            else SetState(ClientState.Confused);
        }
    }

    private IEnumerator GetTicketRoutine()
    {
        // Защита от повторного запуска
        if (myQueueNumber != -1)
        {
            Debug.Log($"[GetTicketRoutine] {parent.name}: Уже имеет талон #{myQueueNumber}, выход.");
            yield break;
        }
        
        Debug.Log($"[GetTicketRoutine] {parent.name}: Начинаю получать талон...");
        
        // Клиент тупит у аппарата
        if (Objects.TicketTerminal.Instance != null)
        {
            yield return new WaitForSeconds(Objects.TicketTerminal.Instance.interactionTime);
            if (Objects.TicketTerminal.Instance.printSound != null)
                feedbackController.PlaySound(Objects.TicketTerminal.Instance.printSound);
        }
        else yield return new WaitForSeconds(1f);

        // Получаем талон
        Debug.Log($"[GetTicketRoutine] {parent.name}: JoinQueue...");
        ClientQueueManager.Instance.JoinQueue(parent);
        
        // Устанавливаем локальный номер очереди (同步 с ClientQueueManager)
        if (ClientQueueManager.Instance.queue.TryGetValue(parent, out int queueNum))
        {
            myQueueNumber = queueNum;
        }
        
        Debug.Log($"[GetTicketRoutine] {parent.name}: JoinQueue завершён, queueNumber={myQueueNumber}");

        // Выбираем место для ожидания
        Waypoint nextGoal = (parent.mainGoal == ClientGoal.VisitToilet)
            ? ClientSpawner.GetToiletZone().waitingWaypoint
            : ClientQueueManager.Instance.ChooseNewGoal(parent);
            
        Debug.Log($"[GetTicketRoutine] {parent.name}: nextGoal={nextGoal?.name ?? "NULL"}, mainGoal={parent.mainGoal}");
        
        if (nextGoal != null)
        {
            SetGoal(nextGoal);
            Transform seat = ClientQueueManager.Instance.FindSeatForClient(parent);
            Debug.Log($"[GetTicketRoutine] {parent.name}: seat={seat?.name ?? "NULL"}, standingClients={ClientQueueManager.Instance.standingClients.Count}");
            if (seat != null) GoToSeat(seat);
            else SetState(ClientState.MovingToGoal);
        }
        else
        {
            Debug.LogWarning($"[GetTicketRoutine] {parent.name}: nextGoal == NULL! Переход в Confused!");
            SetState(ClientState.Confused);
        }
    }

    private void HandleMovementArrival()
    {
        Debug.Log($"[HandleMovementArrival] {parent.name}: currentState={currentState}, currentGoal={currentGoal?.name ?? "NULL"}");
        
        // Бронебойная проверка: если статус ухода ИЛИ цель - это выход из здания
        if (currentState == ClientState.Leaving || currentState == ClientState.LeavingUpset ||
            (currentGoal != null && Managers.ClientSpawner.Instance != null && currentGoal == Managers.ClientSpawner.Instance.exitWaypoint))
        {
            parent.OnClientExit();
            return;
        }
        if (currentState == ClientState.GoingToCashier)
        {
            SetState(ClientState.AtCashier);
            return;
        }
        if (currentState == ClientState.ReturningToRegistrar)
        {
            SetState(ClientState.AtRegistration);
            return;
        }
        // Проверка: если клиент уже получил талон (myQueueNumber != -1), не запускать GetTicketRoutine повторно
        if (currentState == ClientState.MovingToTerminal || (currentGoal != null && currentGoal.name == "TicketStandPoint"))
        {
            if (myQueueNumber == -1) // Ещё не в очереди - идём за талоном
            {
                Debug.Log($"[HandleMovementArrival] {parent.name}: Прибыл к терминалу. Меняю состояние на GettingTicket.");
                SetState(ClientState.GettingTicket);
            }
            else
            {
                Debug.Log($"[HandleMovementArrival] {parent.name}: Уже имеет талон #{myQueueNumber}, пропускаю GetTicketRoutine.");
            }
            return;
        }
        Debug.Log($"[HandleMovementArrival] {parent.name}: currentGoal={currentGoal?.name ?? "NULL"}");
        if (currentGoal == null) {
            Debug.LogWarning($"[HandleMovementArrival] {parent.name}: currentGoal == NULL! Переход в Confused!");
            SetState(ClientState.Confused);
            return;
        }

        // БРОНЕБОЙНЫЙ ПОИСК: Ищем ServicePoint по ссылке, даже если забыли галочку isServicePoint
        var servicePoint = currentGoal.GetComponentInParent<ServicePoint>() ??
                           FindObjectsByType<ServicePoint>(FindObjectsSortMode.None).FirstOrDefault(s => s.clientStandPoint == currentGoal);

        // Если это точка обслуживания - жестко чекинимся
        if (servicePoint != null || currentGoal.isServicePoint)
        {
            var owningZone = currentGoal.GetComponentInParent<LimitedCapacityZone>() ?? servicePoint?.GetComponentInParent<LimitedCapacityZone>();
            if (owningZone != null)
            {
                owningZone.ManuallyOccupyWaypoint(currentGoal, parent.gameObject);
                targetZone = owningZone;
                occupiedWaypoint = currentGoal;
                SetState(ClientState.InsideLimitedZone);

                TryGreetWorkerAtServicePoint(currentGoal);
                return;
            }
        }

        // FIXED: Recognize if the client arrived at an inside spot of a queue zone
        if (targetZone != null && targetZone.insideWaypoints.Contains(currentGoal))
        {
            SetState(ClientState.InsideLimitedZone);
            return;
        }

        // Проверка: это точка входа в LimitedCapacityZone (waitingWaypoint)?
        var newTargetZone = FindObjectsByType<LimitedCapacityZone>(FindObjectsSortMode.None)
            .FirstOrDefault(z => z.waitingWaypoint == currentGoal);
        
        if (newTargetZone != null)
        {
            targetZone = newTargetZone;
            SetState(ClientState.AtLimitedZoneEntrance);
            return; // Немедленно прерываем - они уже перешли в состояние входа в зону!
        }

        // Проверка: это точка ожидания в зоне очереди?
        if (ClientQueueManager.Instance.IsWaypointInWaitingZone(currentGoal))
        {
            if (parent.isQueueJumper)
            {
                // Наглецы не сидят с обычными людьми! Возвращаем их упрямо стоять у стойки.
                if (targetZone != null && targetZone.waitingWaypoint != null)
                {
                    SetGoal(targetZone.waitingWaypoint);
                    SetState(ClientState.MovingToGoal);
                }
                else
                {
                    SetState(ClientState.Confused);
                }
                return;
            }

            // Для обычных клиентов:
            Transform seat = ClientQueueManager.Instance.FindSeatForClient(parent);
            if (seat != null)
            {
                GoToSeat(seat);
            }
            else
            {
                SetState(ClientState.AtWaitingArea);
            }
            return;
        }

        // Проверка: это стол с формой?
        if (ClientSpawner.Instance.formTable != null && currentGoal == ClientSpawner.Instance.formTable.tableWaypoint)
        {
            StartCoroutine(GetFormFromTableRoutine());
            return;
        }

        SetState(ClientState.Confused);
    }

    private void CleanupOldState(ClientState oldState, ClientState newState)
    {
        bool wasInsideZone = IsInsideZoneState(oldState);
        bool willBeOutsideZone = newState != ClientState.InsideLimitedZone;

        if (wasInsideZone && willBeOutsideZone && targetZone != null)
        {
            targetZone.LeaveQueue(parent?.gameObject);
            targetZone.ReleaseWaypoint(occupiedWaypoint);
            occupiedWaypoint = null;
        }

        if (IsLeavingState(newState) && myQueueNumber != -1)
        {
            if (newState != ClientState.Leaving || parent?.reasonForLeaving != ClientPathfinding.LeaveReason.Processed)
                ClientQueueManager.Instance?.ServiceFinishedForNumber(myQueueNumber);
        }
    }

    // --- Action Routines ---

    private IEnumerator GetFormFromTableRoutine()
    {
        yield return new WaitForSeconds(Random.Range(2f, 4f));
        DocumentType docToGet = (parent.mainGoal == ClientGoal.GetCertificate1) ? DocumentType.Form1 : DocumentType.Form2;
        if (docToGet != DocumentType.None) parent.docHolder.SetDocument(docToGet);

        if (zoneToReturnTo != null && zoneToReturnTo.waitingWaypoint != null)
        {
            SetGoal(zoneToReturnTo.waitingWaypoint);
            zoneToReturnTo = null;
        }
        else
        {
            SetGoal(ClientQueueManager.Instance.ChooseNewGoal(parent));
        }
        SetState(ClientState.MovingToGoal);
    }

    private IEnumerator ConfusedRoutine()
    {
        Debug.Log($"[ConfusedRoutine] {parent.name}: Вошёл в Confused! previousGoal={previousGoal?.name ?? "NULL"}");
        
        actionExecutor.StopMoving();

        // 1. Показываем мысли потеряшки
        string[] confusedThoughts = {
            "Так куда это мне надо было?",
            "Где я?",
            "Ой, а я за кем занимал?",
            "Что-то я запамятовал...",
            "Голова кругом от этих кабинетов...",
            "Кажется, я заблудился..."
        };
        parent.ShowThoughtBubble(confusedThoughts[Random.Range(0, confusedThoughts.Length)], 3f);

        // Ждем 3 секунды, давая стажеру шанс перехватить клиента
        yield return new WaitForSeconds(3f);

        // 2. Кидаем кубик на память (бабушки забывают чаще)
        float recoveryChance = 0.5f - (parent.babushkaFactor * 0.3f);
        Debug.Log($"[ConfusedRoutine] {parent.name}: Шанс вспомнить = {recoveryChance:P0}");
        
        Waypoint newGoal = null;

        if (Random.value < recoveryChance && previousGoal != null)
        {
            // Вспомнил!
            parent.ShowThoughtBubble("А, точно, вспомнил!", 2f);
            newGoal = previousGoal;
        }
        else
        {
            // Не вспомнил! Идет обратно в зал ожидания
            parent.ShowThoughtBubble("Придется всё начинать сначала...", 2.5f);
            newGoal = ClientQueueManager.Instance.ChooseNewGoal(parent);
        }
        
        yield return new WaitForSeconds(1.5f);

        if (newGoal != null)
        {
            SetGoal(newGoal);
            SetState(ClientState.MovingToGoal);
        }
        else
        {
            // Если даже в зал вернуться не смог - обижается и уходит
            parent.reasonForLeaving = ClientPathfinding.LeaveReason.Upset;
            SetGoal(ClientSpawner.Instance.exitWaypoint);
            SetState(ClientState.LeavingUpset);
        }
    }

    private IEnumerator GrumblingRoutine()
    {
        var thoughtController = GetComponent<ThoughtBubbleController>();
        var archetype = parent.GetComponent<CharacterVisuals>()?.currentArchetype;

        if (thoughtController != null && parent.ShouldShowGrumble())
        {
            string grumbleText = parent.GetGrumblingText(archetype);
            thoughtController.ShowPriorityMessage(grumbleText, 2f, Color.yellow);
        }

        float checkInterval = 0.5f;
        while (currentState == ClientState.Grumbling)
        {
            yield return new WaitForSeconds(checkInterval);

            if (parent.PatienceHeat >= 1.0f)
            {
                if (Random.value < 0.4f)
                {
                    SetState(ClientState.Enraged);
                }
                else
                {
                    parent.reasonForLeaving = ClientPathfinding.LeaveReason.Upset;
                    SetGoal(ClientSpawner.Instance.exitWaypoint);
                    SetState(ClientState.LeavingUpset);
                }
                yield break;
            }

            if (parent.ShouldShowGrumble() && thoughtController != null)
            {
                string grumbleText = parent.GetGrumblingText(archetype);
                thoughtController.ShowPriorityMessage(grumbleText, 2f, Color.yellow);
            }
        }
    }

    private IEnumerator EnragedRoutine()
    {
        GuardManager.Instance?.ReportViolator(parent.gameObject);
        ClientQueueManager.Instance?.AddAngryClient(parent);
        
        // МУСОРИМ БУМАГАМИ ПРИ СРЫВЕ
        for(int i = 0; i < 5; i++)
        {
            if (parent.trashPrefabs != null && parent.trashPrefabs.Count > 0)
            {
                GameObject trash = parent.trashPrefabs[Random.Range(0, parent.trashPrefabs.Count)];
                Vector3 spawnPos = transform.position + (Vector3)Random.insideUnitCircle * 1.5f;
                Instantiate(trash, spawnPos, Quaternion.identity);
            }
        }

        var thoughtController = GetComponent<ThoughtBubbleController>();
        if (thoughtController != null) thoughtController.StopThinking();

        float rageTimer = Time.time + rageDuration;
        while (Time.time < rageTimer)
        {
            if (Random.value < 0.05f && thoughtController != null)
            {
                string[] rageShouts = { "БЕЗОБРАЗИЕ!", "Я БУДУ ЖАЛОВАТЬСЯ!", "ВЫ ВСЕ УВОЛЕНЫ!", "РРРАААА!" };
                thoughtController.ShowPriorityMessage(rageShouts[Random.Range(0, rageShouts.Length)], 2f, Color.red);
            }

            if (Random.value < 0.02f && parent.trashPrefabs != null && parent.trashPrefabs.Count > 0)
            {
                Instantiate(parent.trashPrefabs[Random.Range(0, parent.trashPrefabs.Count)], transform.position, Quaternion.identity);
            }

            Waypoint[] allWaypoints = FindObjectsByType<Waypoint>(FindObjectsSortMode.None);
            if (allWaypoints.Length > 0)
            {
                Waypoint randomTarget = allWaypoints[Random.Range(0, allWaypoints.Length)];
                yield return StartCoroutine(actionExecutor.MoveToGoalRoutine(randomTarget));
            }
            yield return null;
        }

        parent.reasonForLeaving = ClientPathfinding.LeaveReason.Angry;
        SetGoal(ClientSpawner.Instance?.exitWaypoint);
        SetState(ClientState.Leaving);
    }

    private IEnumerator MillAroundRoutine()
    {
        //ClientQueueManager.Instance.StartPatienceTimer(parent);
        while (currentState == ClientState.AtWaitingArea)
        {
            Transform freeSeat = ClientQueueManager.Instance.FindSeatForClient(parent);
            if (freeSeat != null)
            {
                GoToSeat(freeSeat);
                yield break;
            }
            Waypoint randomPoint = ClientQueueManager.Instance.ChooseNewGoal(parent);
            if (randomPoint != null)
            {
                SetGoal(randomPoint);
                yield return StartCoroutine(actionExecutor.MoveToGoalRoutine(randomPoint));
            }
            yield return new WaitForSeconds(Random.Range(2f, 4f));
        }
    }

    private IEnumerator MillAroundWithChatRoutine()
    {
        float lastChatAttempt = 0f;
        const float CHAT_INTERVAL = 5f;

        while (currentState == ClientState.AtWaitingArea)
        {
            // Пытаемся поговорить с соседями
            if (Time.time - lastChatAttempt > CHAT_INTERVAL)
            {
                parent.TryChatWithNearbyClients();
                lastChatAttempt = Time.time;
            }

            Transform freeSeat = ClientQueueManager.Instance.FindSeatForClient(parent);
            if (freeSeat != null)
            {
                GoToSeat(freeSeat);
                yield break;
            }
            Waypoint randomPoint = ClientQueueManager.Instance.ChooseNewGoal(parent);
            if (randomPoint != null)
            {
                SetGoal(randomPoint);
                yield return StartCoroutine(actionExecutor.MoveToGoalRoutine(randomPoint));
            }
            yield return new WaitForSeconds(Random.Range(2f, 4f));
        }
    }

    private IEnumerator WaitingInSeatWithChatRoutine()
    {
        float lastChatAttempt = 0f;
        const float CHAT_INTERVAL = 5f;

        while (currentState == ClientState.SittingInWaitingArea)
        {
            // Пытаемся поговорить с соседями
            if (Time.time - lastChatAttempt > CHAT_INTERVAL)
            {
                parent.TryChatWithNearbyClients();
                lastChatAttempt = Time.time;
            }

            yield return new WaitForSeconds(2f);
        }
    }
    
    private IEnumerator PassedRegistrationRoutine() 
    { 
        if (myQueueNumber != -1) ClientQueueManager.Instance.RemoveClientFromQueue(parent); 
        
        if (parent.billToPay > 0) { 
            SetGoal(ClientSpawner.GetCashierZone().waitingWaypoint); 
            SetState(ClientState.MovingToGoal); 
            yield break; 
        } 
        
        DocumentType docType = parent.docHolder.GetCurrentDocumentType(); 
        ClientGoal goal = parent.mainGoal; 
        Waypoint nextGoal = null; 
        ClientState nextState = ClientState.MovingToGoal; 
        
        switch (goal) { 
            case ClientGoal.PayTax: 
                nextGoal = ClientSpawner.Instance.exitWaypoint; 
                parent.isLeavingSuccessfully = true; 
                parent.reasonForLeaving = ClientPathfinding.LeaveReason.Processed; 
                break; 
            case ClientGoal.GetCertificate1: 
                nextGoal = (docType == DocumentType.Form1) ? ClientSpawner.GetDesk1Zone().waitingWaypoint : ClientSpawner.Instance.exitWaypoint; 
                break; 
            case ClientGoal.GetCertificate2: 
                nextGoal = (docType == DocumentType.Form2) ? ClientSpawner.GetDesk2Zone().waitingWaypoint : ClientSpawner.Instance.exitWaypoint; 
                break; 
            default: 
                nextGoal = ClientSpawner.Instance.exitWaypoint; 
                parent.isLeavingSuccessfully = true; 
                parent.reasonForLeaving = ClientPathfinding.LeaveReason.Processed; 
                break; 
        } 
        
        if (nextGoal == ClientSpawner.Instance.exitWaypoint) { 
            nextState = ClientState.Leaving; 
            if (parent.reasonForLeaving == ClientPathfinding.LeaveReason.Normal) { 
                parent.reasonForLeaving = ClientPathfinding.LeaveReason.Upset; 
            } 
        } 
        SetGoal(nextGoal); 
        SetState(nextState); 
        yield return null; 
    }
    
    private IEnumerator WaitForServiceRoutine()
    {
        yield return new WaitUntil(() => !IsInsideZoneState(currentState));
    }

    // --- Public Utility Methods (API) ---

    public void GetCalledToSpecificDesk(Waypoint destination, int queueNumber, IServiceProvider provider)
    {
        // --- ИСПРАВЛЕНИЕ: Используем централизованный метод остановки ---
        StopAllActionCoroutines();
        
        ClientQueueManager.Instance.OnClientLeavesWaitingZone(parent);
        myQueueNumber = queueNumber;
        myServiceProvider = provider;
        
        // --- Логирование вызова клиента к стойке ---
        actionDiary?.LogEvent($"Вызван к окну №{queueNumber}");
        
        SetGoal(destination);
        SetState(ClientState.MovingToGoal);
    }

    public void DecideToVisitToilet()
    {
        if (currentState == ClientState.AtWaitingArea || currentState == ClientState.SittingInWaitingArea)
        {
            StopAllActionCoroutines();
            ClientQueueManager.Instance.OnClientLeavesWaitingZone(parent);
            previousGoal = currentGoal;
            SetGoal(ClientSpawner.GetToiletZone().waitingWaypoint);
            SetState(ClientState.MovingToGoal);
        }
    }

    private IEnumerator HandleToiletVisitRoutine()
    {
        if (targetZone == null || occupiedWaypoint == null)
        {
            SetState(ClientState.Confused);
            yield break;
        }

        float toiletDuration = Random.Range(3f, 6f);
        yield return new WaitForSeconds(toiletDuration);

        var messGenerator = GetComponent<ClientMessGenerator>();
        if (messGenerator != null && Random.value < 0.3f)
        {
            messGenerator.TrySpawnPuddle();
        }

        if (targetZone != null && occupiedWaypoint != null)
        {
            targetZone.ReleaseWaypoint(occupiedWaypoint);
        }
        targetZone = null;
        occupiedWaypoint = null;

        bool shouldReturnToPrevious = previousGoal != null && parent.mainGoal != ClientGoal.VisitToilet;
        
        if (shouldReturnToPrevious)
        {
            SetGoal(previousGoal);
            previousGoal = null;
            SetState(ClientState.MovingToGoal);
        }
        else
        {
            SetGoal(ClientSpawner.Instance.exitWaypoint);
            parent.isLeavingSuccessfully = true;
            parent.reasonForLeaving = ClientPathfinding.LeaveReason.Processed;
            SetState(ClientState.Leaving);
        }
    }

    public void GoGetFormAndReturn()
    {
        zoneToReturnTo = targetZone;
        parent.hasBeenSentForRevision = true;
        if (targetZone != null) targetZone.ReleaseWaypoint(occupiedWaypoint);
        SetGoal(ClientSpawner.Instance.formTable.tableWaypoint);
        SetState(ClientState.MovingToGoal);
    }
    
    public void GoToSeat(Transform seat)
    {
        StopAllActionCoroutines();
        // --- ИСПРАВЛЕНИЕ: Используем переменную seatTarget, которая теперь есть ---
        seatTarget = seat; 
        SetState(ClientState.MovingToSeat);
    }
    
    public void GetHelpFromIntern(Waypoint newGoal)
    {
        if (parent.helpedByInternSound != null) feedbackController.PlaySound(parent.helpedByInternSound);
        StopAllActionCoroutines();
        if (newGoal != null)
        {
            ClientQueueManager.Instance.RemoveClientFromQueue(parent);
            SetGoal(newGoal);
            if (newGoal == ClientSpawner.Instance.exitWaypoint) SetState(ClientState.Leaving);
            else SetState(ClientState.MovingToGoal);
        }
    }

    // --- Helpers ---
    private bool IsInsideZoneState(ClientState state)
    {
        return state == ClientState.InsideLimitedZone || state == ClientState.AtLimitedZoneEntrance || 
               state == ClientState.AtRegistration || state == ClientState.AtDesk1 || 
               state == ClientState.AtDesk2 || state == ClientState.AtCashier;
    }
    
    private bool IsLeavingState(ClientState state)
    {
        return state == ClientState.Leaving || state == ClientState.LeavingUpset;
    }

    private bool hasEnteredBuilding = false;

    public void OnEnteredBuilding()
    {
        if (hasEnteredBuilding) return;
        hasEnteredBuilding = true;

        // 1. БАЗОВЫЙ ЗВУК ПОЯВЛЕНИЯ (Для всех клиентов)
        if (parent != null && parent.spawnSound != null && feedbackController != null)
        {
            feedbackController.PlaySound(parent.spawnSound);
        }

        if (parent.isQueueJumper)
        {
            // 2. ДОП. ЗВУК НАГЛЕЦА И БАБЛ
            if (parent != null && parent.impoliteSound != null && feedbackController != null)
            {
                feedbackController.PlaySound(parent.impoliteSound);
            }
            
            var thoughtController = GetComponent<ThoughtBubbleController>();
            if (thoughtController != null) thoughtController.ShowPriorityMessage("Я только спросить!", 3.5f, Color.red);
        }
        else
        {
            // 3. ПРИВЕТСТВИЕ НОРМАЛЬНОГО КЛИЕНТА
            if (parent != null) parent.ShowSpawnThought();
        }
    }

    public string GetStatusInfo()
    {
        if (parent == null) return "No data";
        return $"{currentState} (Goal: {currentGoal?.name})";
    }

    // --- Система приветствий ---
    private void TryGreetWorkerAtServicePoint(Waypoint destPoint)
    {
        if (parent == null) return;

        ServicePoint targetDesk = null;
        if (myServiceProvider != null) targetDesk = myServiceProvider.GetWorkstation();

        // Ищем стол напрямую через вейпоинт (самый надежный способ)
        if (targetDesk == null)
        {
            targetDesk = destPoint.GetComponentInParent<ServicePoint>() ??
                         FindObjectsByType<ServicePoint>(FindObjectsSortMode.None).FirstOrDefault(d => d.clientStandPoint == destPoint);
        }

        if (targetDesk != null)
        {
            if (targetDesk.CurrentClient != parent) targetDesk.AssignClient(parent); // Наглый клиент садится сам
            
            targetDesk.SetClientReady(); // ГОВОРИМ СТОЛУ "Я ПРИШЕЛ!"

            var worker = targetDesk.GetAssignedStaff();
            if (worker != null)
            {
                parent.TryGreetStaff(worker);
            }
        }
        else
        {
            Debug.LogWarning($"[ClientStateMachine] Клиент {parent.name} пришел на точку {destPoint.name}, но ServicePoint не найден!");
        }
    }
}