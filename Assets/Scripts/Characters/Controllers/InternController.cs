// Assets/Scripts/Characters/Controllers/InternController.cs
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Data.Calendar;
using Managers;
using Managers.Teletype;
using Utilities;
using Characters;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(AgentMover))]
[RequireComponent(typeof(CharacterStateLogger))]
[RequireComponent(typeof(StackHolder))]
public class InternController : StaffController, IServiceProvider
{
    public enum InternState
    {
        Patrolling,
        HelpingConfused,
        ServingFromQueue,
        CoveringDesk,
        GoingToBreak,
        OnBreak,
        GoingToToilet,
        AtToilet,
        ReturningToPatrol,
        Inactive,
        Working,
        TalkingToConfused,
        TakingStackToArchive
    }

    [Header("Настройки стажера")]
    private InternState currentState = InternState.Inactive;
    private ServicePoint coveredServicePoint;
    private Coroutine serviceCoroutine;
    private ClientPathfinding servingClient;

    public EmotionSpriteCollection spriteCollection;
    public StateEmotionMap stateEmotionMap;

    protected override void Awake()
    {
        base.Awake();
        smallTalk = GetComponent<SmallTalkController>();
    }

    // --- ПЕРЕОПРЕДЕЛЕНИЕ СТАРТА СМЕНЫ ---
    public override void StartShift()
    {
        MarkShiftStarted();
        if (WorkShiftMask == 0 || WorkShiftMask == CalendarDayPeriodType.None)
        {
            WorkShiftMask = CalendarDayPeriodType.Morning;
        }

        Debug.Log($"[InternController] {characterName}: Найм. WorkShiftMask={WorkShiftMask}, Текущий период={TimeManager.Instance.GetCurrentPeriodType()}");

        gameObject.SetActive(true);
        hasArrivedToday = false;
        currentState = InternState.Inactive;

        if (systemActionDatabase == null)
        {
            systemActionDatabase = Resources.Load<ActionDatabase>("Databases/ActionDatabase");
        }

        if (agentMover == null)
        {
            agentMover = GetComponent<AgentMover>();
        }

        StartCoroutine(WaitForShiftAndStart());
    }

    private IEnumerator WaitForShiftAndStart()
    {
        var currentPeriod = TimeManager.Instance.GetCurrentPeriodType();
        var periodMatches = (WorkShiftMask & currentPeriod) != 0;

        Debug.Log($"[InternController] {characterName}: Ожидание смены. Период={currentPeriod}, Совпадение={periodMatches}");

        while (!periodMatches)
        {
            yield return new WaitForSeconds(1f);
            currentPeriod = TimeManager.Instance.GetCurrentPeriodType();
            periodMatches = (WorkShiftMask & currentPeriod) != 0;

            if (currentState == InternState.Inactive && periodMatches)
            {
                Debug.Log($"[InternController] {characterName}: Период начался! Начинаю работу.");
                break;
            }
        }

        hasArrivedToday = true;
        currentState = InternState.Patrolling;
        // Используем базовый AI вместо hardcoded логики
        StartCoroutine(AIUpdateLoop());
    }

    private SmallTalkController smallTalk;
    private float stressReactionCooldown = 0f;

    private IEnumerator InternLogicLoop()
    {
        TeletypeManager.Instance?.LogStaffWork(characterName, role.ToString(), isStartShift: true);
        hasArrivedToday = true;
        currentState = InternState.Patrolling;

        if (thoughtBubble) thoughtBubble.ShowPriorityMessage(UI.GameText.Get("staff.shift.start"), 2f, Color.white);

        Debug.Log($"[InternController] {characterName}: Начал работу. WorkShiftMask={WorkShiftMask}, IsOnDuty={IsOnDuty()}");

        while (IsOnDuty())
        {
            if (thoughtBubble) thoughtBubble.ShowPriorityMessage(UI.GameText.Get("intern.on_duty"), 2f, Color.white);

            if (CheckAndHandleBreaks())
            {
                yield return new WaitForSeconds(1f);
                continue;
            }

            if (currentExecutor != null)
            {
                yield return new WaitForSeconds(1f);
                continue;
            }

            if (agentMover != null && agentMover.IsMoving())
            {
                yield return new WaitForSeconds(0.5f);
                continue;
            }

            // --- РАБОТА СТАЖЁРА ---
            var coolerAction = activeActions.FirstOrDefault(a => a is Action_GoToCooler);
            if (coolerAction != null && ((Action_GoToCooler)coolerAction).AreConditionsMet(this))
            {
                Debug.Log($"[InternController] {characterName}: Выполняю кулер");
                ExecuteAction(coolerAction);
                continue;
            }

            var coverAction = activeActions.FirstOrDefault(a => a.actionType == ActionType.CoverClerk || a.actionType == ActionType.CoverRegistrar);
            if (coverAction != null && coverAction.AreConditionsMet(this))
            {
                Debug.Log($"[InternController] {characterName}: Выполняю Cover Desk");
                ExecuteAction(coverAction);
                continue;
            }

            var helpAction = activeActions.FirstOrDefault(a => a.actionType == ActionType.HelpConfusedClient);
            if (helpAction != null && helpAction.AreConditionsMet(this))
            {
                Debug.Log($"[InternController] {characterName}: Выполняю Help Confused");
                ExecuteAction(helpAction);
                continue;
            }

            // Патруль
            var patrolAction = activeActions.FirstOrDefault(a => a.actionType == ActionType.InternPatrol)
                               ?? systemActionDatabase?.allActions.FirstOrDefault(a => a.actionType == ActionType.InternPatrol);

            var points = ScenePointsRegistry.Instance?.internPatrolPoints;
            var validPoints = points?.Where(p => p != null && p.transform != null).ToList();
            bool hasPatrolPoints = validPoints != null && validPoints.Count > 0;

            Debug.Log($"[InternController] {characterName}: Проверка патруля. action={(patrolAction != null)}, points=(всего:{points?.Count ?? 0}, валидных:{validPoints?.Count ?? 0})");

            if (patrolAction != null && hasPatrolPoints)
            {
                Debug.Log($"[InternController] {characterName}: Выполняю патруль через action");
                ExecuteAction(patrolAction);
            }
            else
            {
                SetState(InternState.Patrolling);

                if (!hasPatrolPoints)
                {
                    Debug.LogWarning($"[InternController] {characterName}: Нет точек патруля! action={patrolAction != null}, validPoints={validPoints?.Count ?? 0}");
                    
                    var kitchenPoint = ScenePointsRegistry.Instance?.RequestKitchenPoint();
                    if (kitchenPoint != null)
                    {
                        Debug.Log($"[InternController] {characterName}: Иду на кухню как fallback");
                        SetState(InternState.GoingToBreak);
                        yield return StartCoroutine(MoveToTarget(kitchenPoint.transform.position, InternState.OnBreak));
                        yield return new WaitForSeconds(Random.Range(10f, 20f));
                    }
                    else
                    {
                        Debug.LogError($"[InternController] {characterName}: НЕТ точек патруля И кухня недоступна! Стажёр завис!");
                        yield return new WaitForSeconds(2f);
                    }
                    continue;
                }

                Debug.Log($"[InternController] {characterName}: Патрулирую вручную. Точек: {validPoints.Count}");

                while (IsOnDuty())
                {
                    if (CheckAndHandleBreaks()) break;
                    if (currentExecutor != null) break;

                    var p = validPoints[Random.Range(0, validPoints.Count)];
                    if (p != null && p.transform != null)
                    {
                        Debug.Log($"[InternController] {characterName}: Иду к точке {p.name}");
                        yield return StartCoroutine(MoveToTarget(p.transform.position, InternState.Patrolling));
                        yield return new WaitForSeconds(Random.Range(3f, 7f));
                    }
                    else
                    {
                        Debug.LogWarning($"[InternController] {characterName}: Точка null при патруле");
                        yield return new WaitForSeconds(2f);
                    }
                }
            }

            yield return new WaitForSeconds(0.5f);
        }

        Debug.Log($"[InternController] {characterName}: Смена закончилась (IsOnDuty={IsOnDuty()})");
    }

    public void SetState(InternState newState)
    {
        if (currentState == newState) return;
        currentState = newState;
        logger?.LogState(GetStatusInfo());
        if (visuals != null) visuals.SetEmotionForState(newState);
    }

    public void AssignCoveredWorkstation(ServicePoint point)
    {
        if (point != coveredServicePoint)
        {
            if (serviceCoroutine != null) StopCoroutine(serviceCoroutine);
            serviceCoroutine = null;
            var interruptedClient = servingClient != null ? servingClient : coveredServicePoint?.CurrentClient;
            if (interruptedClient != null && interruptedClient.stateMachine?.MyServiceProvider == this)
            {
                if (coveredServicePoint != null && coveredServicePoint.CurrentClient == interruptedClient)
                    coveredServicePoint.ClearClient();
                interruptedClient.stateMachine.CancelServiceCall(this);
            }
            servingClient = null;
        }
        coveredServicePoint = point;
    }

    public InternState GetCurrentState()
    {
        return currentState;
    }

    public override IEnumerator MoveToTarget(Vector2 targetPosition, string stateOnArrival)
    {
        if (System.Enum.TryParse<InternState>(stateOnArrival, out InternState newState))
        {
            if (agentMover != null)
                agentMover.SetPath(PathfindingUtility.BuildPathTo(transform.position, targetPosition, gameObject));

            yield return new WaitUntil(() => agentMover == null || !agentMover.IsMoving());
            SetState(newState);
        }
        else
        {
            yield return base.MoveToTarget(targetPosition, stateOnArrival);
        }
    }

    public IEnumerator MoveToTarget(Vector2 targetPosition, InternState stateOnArrival)
    {
        if (agentMover != null)
            agentMover.SetPath(PathfindingUtility.BuildPathTo(transform.position, targetPosition, gameObject));

        yield return new WaitUntil(() => agentMover == null || !agentMover.IsMoving());
        SetState(stateOnArrival);
    }

    protected override void SetArrivalState(string stateName)
    {
        if (System.Enum.TryParse<InternState>(stateName, out InternState newState))
        {
            SetState(newState);
        }
    }

    public override string GetCurrentStateName()
    {
        return currentState.ToString();
    }

    public override bool IsOnBreak()
    {
        return base.IsOnBreak() || currentState == InternState.OnBreak ||
               currentState == InternState.GoingToBreak ||
               currentState == InternState.AtToilet ||
               currentState == InternState.GoingToToilet;
    }

    public override string GetStatusInfo()
    {
        return currentState.ToString();
    }

    public override void InitializeFromData(RoleData data)
    {
        base.InitializeFromData(data);
        if (data == null) return;
        if (agentMover != null)
        {
            agentMover.moveSpeed = data.moveSpeed;
            agentMover.priority = data.priority;
        }

        this.spriteCollection = data.spriteCollection;
        this.stateEmotionMap = data.stateEmotionMap;

    }

    #region IServiceProvider Implementation

    public bool IsAvailableToServe => gameObject.activeInHierarchy && IsOnDuty() && !IsOnBreak() &&
        coveredServicePoint != null && servingClient == null &&
        (agentMover == null || !agentMover.IsMoving()) && GetCurrentState() == InternState.CoveringDesk;

    public Transform GetClientStandPoint()
    {
        return coveredServicePoint != null ? coveredServicePoint.clientStandPoint.transform : transform;
    }

    public ServicePoint GetWorkstation()
    {
        return coveredServicePoint;
    }

    public void AssignClient(ClientPathfinding client)
    {
        if (!IsAvailableToServe || client == null || !client.CanContinueService) return;
        servingClient = client;
        var routine = StartCoroutine(ServeCoveredClient(client));
        // A broken desk may finish synchronously, before StartCoroutine returns its handle.
        if (servingClient != null) serviceCoroutine = routine;
    }

    private IEnumerator ServeCoveredClient(ClientPathfinding client)
    {
        yield return InternServiceRoutine(client);
        if (coveredServicePoint != null && coveredServicePoint.CurrentClient == client) coveredServicePoint.ClearClient();
        if (client != null && client.CanContinueService && client.stateMachine.MyServiceProvider == this)
            client.stateMachine.CancelServiceCall(this);
        servingClient = null;
        serviceCoroutine = null;
    }

    private IEnumerator InternServiceRoutine(ClientPathfinding client)
    {
        if (client == null || !client.CanContinueService || coveredServicePoint == null) yield break;

        int deskId = coveredServicePoint.deskId;

        var durability = coveredServicePoint.GetComponent<Gameplay.OfficeObjectDurability>();
        if (durability != null && !durability.IsUsable())
        {
            thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("intern.broken"), 3f, Color.red);
            yield break;
        }
        float efficiency = Mathf.Max(.05f, durability != null ? durability.GetEfficiencyMultiplier() : 1.0f);

        if (deskId == 0)
        {
            thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("intern.try_help"), 2f, Color.yellow);
            yield return new WaitForSeconds(3f / efficiency);

            if (client == null || !client.CanContinueService) yield break;

            Waypoint destination = DetermineCorrectGoalForClient(client);

            float errorChance = 0.4f * (1f - skills.pedantry);
            if (Random.value < errorChance)
                thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("intern.uncertain"), 3f, Color.red);
            else
                thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("intern.directions", destination != null ? CharacterActivityOverlay.PlaceName(destination) : UI.GameText.Get("place.exit")), 3f, Color.white);

            if (client.stateMachine != null)
            {
                if (client.stateMachine.MyQueueNumber != -1)
                    ClientQueueManager.Instance?.RemoveClientFromQueue(client);
                if (destination != null)
                {
                    client.stateMachine.SetGoal(destination);
                    client.stateMachine.SetState(ClientState.MovingToGoal);
                }
            }
        }
        else if (deskId == -1)
        {
            thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("intern.payment"), 2f, Color.yellow);
            yield return new WaitForSeconds(3f / efficiency);

            if (client == null || !client.CanContinueService || PlayerWallet.Instance == null) yield break;
            if (client.billToPay == 0 && client.mainGoal == ClientGoal.PayTax) client.billToPay = Random.Range(20, 121);
            if (client.billToPay > 0)
            {
                int bill = client.billToPay;
                int walletBefore = PlayerWallet.Instance.GetCurrentMoney();
                PlayerWallet.Instance?.AddMoney(client.billToPay, UI.GameText.Get("ledger.intern_payment"));
                if (client.paymentSound != null) AudioSource.PlayClipAtPoint(client.paymentSound, transform.position);
                client.billToPay = 0;
                Debug.Log($"[ClientServiceTrace] Оплата стажёру: клиент={client.name}, счёт={bill}, кошелёк={walletBefore}->{PlayerWallet.Instance.GetCurrentMoney()}");
                coveredServicePoint.documentStack?.AddDocumentToStack();
                thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("staff.project.paid"), 2f, Color.green);
            }
            client.isLeavingSuccessfully = true;
            client.reasonForLeaving = ClientPathfinding.LeaveReason.Processed;
            client.stateMachine?.SetGoal(ClientSpawner.Instance?.exitWaypoint);
            client.stateMachine?.SetState(ClientState.Leaving);
        }
        else if (deskId == 1 || deskId == 2)
        {
            thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("intern.review"), 2f, Color.yellow);
            yield return new WaitForSeconds(1.5f / efficiency);

            if (client == null || !client.CanContinueService) yield break;

            if (client.docHolder == null)
            {
                thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("intern.missing"), 3f, Color.red);
                yield break;
            }

            DocumentType requiredDoc = (deskId == 1) ? DocumentType.Form1 : DocumentType.Form2;

            if (client.docHolder.GetCurrentDocumentType() != requiredDoc)
            {
                thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("intern.wrong_form"), 3f, Color.red);
                yield return new WaitForSeconds(2f);
                client.stateMachine?.GoGetFormAndReturn();
            }
            else
            {
                float processingTime = Random.Range(5f, 8f) / efficiency;
                yield return new WaitForSeconds(processingTime);

                if (client == null || !client.CanContinueService) yield break;
                if (client.stampSound != null) AudioSource.PlayClipAtPoint(client.stampSound, transform.position);
                yield return new WaitForSeconds(1f);
                if (client == null || !client.CanContinueService || client.docHolder == null) yield break;

                DocumentType newDocType = (deskId == 1) ? DocumentType.Certificate1 : DocumentType.Certificate2;
                client.docHolder.SetDocument(newDocType);
                client.billToPay += (deskId == 1) ? 100 : 250;

                thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("staff.action.done"), 3f, Color.green);
                client.stateMachine?.SetGoal(ClientSpawner.GetCashierZone()?.waitingWaypoint);
                client.stateMachine?.SetState(ClientState.MovingToGoal);

                coveredServicePoint.documentStack?.AddDocumentToStack();
            }
        }

        if (durability != null) durability.Degrade(Random.Range(3f, 6f));
    }

    private Waypoint DetermineCorrectGoalForClient(ClientPathfinding client)
    {
        if (client.billToPay > 0) return ClientSpawner.GetCashierZone()?.waitingWaypoint;
        switch (client.mainGoal)
        {
            case ClientGoal.PayTax: return ClientSpawner.GetCashierZone()?.waitingWaypoint;
            case ClientGoal.GetCertificate1: return ClientSpawner.GetDesk1Zone()?.waitingWaypoint;
            case ClientGoal.GetCertificate2: return ClientSpawner.GetDesk2Zone()?.waitingWaypoint;
            case ClientGoal.DirectorApproval:
            case ClientGoal.DirectorAudience: return ClientSpawner.Instance?.directorReceptionZone?.waitingWaypoint;
            case ClientGoal.VisitToilet: return ClientSpawner.GetToiletZone()?.waitingWaypoint;
            default: return ClientQueueManager.Instance.ChooseNewGoal(client);
        }
    }
    #endregion
}
