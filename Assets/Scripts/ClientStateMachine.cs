using UnityEngine;
using System.Collections;
using System.Linq;
using Managers;
using Utilities;

[RequireComponent(typeof(ClientMessGenerator))]
[RequireComponent(typeof(ClientActionExecutor))]
[RequireComponent(typeof(ClientFeedbackController))]
public class ClientStateMachine : MonoBehaviour
{
    // --- Components ---
    private ClientPathfinding parent;
    private CharacterStateLogger logger;
    
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

        // Init Modules
        messGenerator = GetComponent<ClientMessGenerator>();
        messGenerator.Initialize(parent);

        actionExecutor = GetComponent<ClientActionExecutor>();
        actionExecutor.Initialize(parent);

        feedbackController = GetComponent<ClientFeedbackController>();
        feedbackController.Initialize(parent);

        if (parent.movement == null) { enabled = false; return; }

        StartCoroutine(MainLogicLoop());
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
                zonePatienceCoroutine = StartCoroutine(PatienceMonitorForZone(targetZone));
                
                // Делегируем сложную логику входа экзекьютору
                yield return StartCoroutine(actionExecutor.EnterZoneRoutine(targetZone));
                break;

            // Группа состояний "Ожидание обслуживания"
            case ClientState.AtRegistration:
            case ClientState.AtCashier:
            case ClientState.InsideLimitedZone:
            case ClientState.AtDesk1:
            case ClientState.AtDesk2:
                // Пассивное ожидание. Сотрудник сам пнет нас дальше.
                yield return StartCoroutine(WaitForServiceRoutine());
                break;

            case ClientState.SittingInWaitingArea:
            case ClientState.AtWaitingArea:
                ClientQueueManager.Instance.StartPatienceTimer(parent);
                // Если стоим - можем слоняться
                if (currentState == ClientState.AtWaitingArea) 
                    yield return StartCoroutine(MillAroundRoutine());
                else 
                    yield return new WaitUntil(() => currentState != ClientState.SittingInWaitingArea);
                break;

            case ClientState.Confused:
                yield return StartCoroutine(ConfusedRoutine());
                break;

            case ClientState.Enraged:
                yield return StartCoroutine(EnragedRoutine());
                break;
            
            case ClientState.WaitingForDocument:
                feedbackController.UpdateStateVisuals(currentState, parent.reasonForLeaving);
                yield return new WaitUntil(() => currentState != ClientState.WaitingForDocument);
                break;

            case ClientState.PassedRegistration:
                yield return StartCoroutine(PassedRegistrationRoutine());
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
        
        logger?.LogState(GetStatusInfo());
        feedbackController.UpdateStateVisuals(newState, parent.reasonForLeaving);
    }

    public void SetGoal(Waypoint g) => currentGoal = g;

    // --- Helper Logic ---

    private void DecideInitialGoal()
    {
        targetZone = null;
        if (Random.value < parent.prolazaFactor)
        {
            if (parent.impoliteSound != null) feedbackController.PlaySound(parent.impoliteSound);
            Waypoint impoliteGoal = null;
            switch (parent.mainGoal)
            {
                case ClientGoal.GetCertificate1: impoliteGoal = ClientSpawner.GetDesk1Zone().waitingWaypoint; break;
                case ClientGoal.GetCertificate2: impoliteGoal = ClientSpawner.GetDesk2Zone().waitingWaypoint; break;
                case ClientGoal.PayTax: impoliteGoal = ClientSpawner.GetCashierZone().waitingWaypoint; break;
                case ClientGoal.AskAndLeave: impoliteGoal = ClientSpawner.GetRegistrationZone().insideWaypoints[0]; break;
                case ClientGoal.VisitToilet: impoliteGoal = ClientSpawner.GetToiletZone().waitingWaypoint; break;
            }

            if (impoliteGoal != null)
            {
                SetGoal(impoliteGoal);
                SetState(ClientState.MovingToRegistrarImpolite);
                return;
            }
        }

        Waypoint initialGoal = (parent.mainGoal == ClientGoal.VisitToilet) 
            ? ClientSpawner.GetToiletZone().waitingWaypoint 
            : ClientQueueManager.Instance.ChooseNewGoal(parent);

        if (initialGoal != null)
        {
            SetGoal(initialGoal);
            SetState(ClientState.MovingToGoal);
        }
        else
        {
            SetState(ClientState.Confused);
        }
    }

    private void HandleMovementArrival()
    {
        if (currentState == ClientState.Leaving || currentState == ClientState.LeavingUpset)
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

        Waypoint dest = GetCurrentGoal();
        if (dest == null) { SetState(ClientState.Confused); return; }

        if (dest.isServicePoint)
        {
            var owningZone = dest.GetComponentInParent<LimitedCapacityZone>();
            if (owningZone != null)
            {
                owningZone.ManuallyOccupyWaypoint(dest, parent.gameObject);
                targetZone = owningZone;
                occupiedWaypoint = dest;
                SetState(ClientState.InsideLimitedZone);
                return;
            }
        }

        var newTargetZone = FindObjectsByType<LimitedCapacityZone>(FindObjectsSortMode.None)
            .FirstOrDefault(z => z.waitingWaypoint == dest);
        if (newTargetZone != null)
        {
            targetZone = newTargetZone;
            SetState(ClientState.AtLimitedZoneEntrance);
            return;
        }

        if (ClientQueueManager.Instance.IsWaypointInWaitingZone(dest))
        {
            ClientQueueManager.Instance.JoinQueue(parent);
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

        if (ClientSpawner.Instance.formTable != null && dest == ClientSpawner.Instance.formTable.tableWaypoint)
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
            targetZone.LeaveQueue(parent.gameObject);
            targetZone.ReleaseWaypoint(occupiedWaypoint);
            occupiedWaypoint = null;
        }

        if (IsLeavingState(newState) && myQueueNumber != -1)
        {
            if (newState != ClientState.Leaving || parent.reasonForLeaving != ClientPathfinding.LeaveReason.Processed)
                ClientQueueManager.Instance.ServiceFinishedForNumber(myQueueNumber);
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
        actionExecutor.StopMoving();
        yield return new WaitForSeconds(3f);
        float recoveryChance = 0.3f * (1f - parent.babushkaFactor);
        if (Random.value < recoveryChance)
        {
            SetGoal(previousGoal ?? ClientQueueManager.Instance.ChooseNewGoal(parent));
            SetState(ClientState.MovingToGoal);
        }
    }

    private IEnumerator EnragedRoutine()
    {
        GuardManager.Instance.ReportViolator(parent);
        ClientQueueManager.Instance.AddAngryClient(parent);
        
        for(int i=0; i<5; i++) messGenerator.TrySpawnPuddle();

        var thoughtController = GetComponent<ThoughtBubbleController>();
        if (thoughtController != null)
        {
            thoughtController.StopThinking();
            thoughtController.TriggerCriticalThought($"Client_{ClientState.Enraged}");
        }

        float rageTimer = Time.time + rageDuration;
        while (Time.time < rageTimer)
        {
            Waypoint[] allWaypoints = FindObjectsByType<Waypoint>(FindObjectsSortMode.None);
            if (allWaypoints.Length > 0)
            {
                Waypoint randomTarget = allWaypoints[Random.Range(0, allWaypoints.Length)];
                yield return StartCoroutine(actionExecutor.MoveToGoalRoutine(randomTarget));
            }
            yield return null;
        }

        parent.reasonForLeaving = ClientPathfinding.LeaveReason.Angry;
        SetGoal(ClientSpawner.Instance.exitWaypoint);
        SetState(ClientState.Leaving);
    }

    private IEnumerator MillAroundRoutine()
    {
        ClientQueueManager.Instance.StartPatienceTimer(parent);
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

    private IEnumerator PatienceMonitorForZone(LimitedCapacityZone zone)
    {
        yield return new WaitForSeconds(zonePatienceTime);
        if (currentState == ClientState.AtLimitedZoneEntrance)
        {
            zone.LeaveQueue(parent.gameObject);
            messGenerator.TrySpawnPuddle();
            if (Random.value < 0.5f) SetState(ClientState.Enraged);
            else 
            {
                parent.reasonForLeaving = ClientPathfinding.LeaveReason.Upset;
                SetState(ClientState.LeavingUpset);
            }
        }
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
        return state == ClientState.Leaving || state == ClientState.LeavingUpset || 
               state == ClientState.Confused || state == ClientState.Enraged;
    }

    public string GetStatusInfo()
    {
        if (parent == null) return "No data";
        return $"{currentState} (Goal: {currentGoal?.name})";
    }
}