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

        if (thoughtBubble) thoughtBubble.ShowPriorityMessage("Вышел на смену!", 2f, Color.white);

        Debug.Log($"[InternController] {characterName}: Начал работу. WorkShiftMask={WorkShiftMask}, IsOnDuty={IsOnDuty()}");

        while (IsOnDuty())
        {
            if (thoughtBubble) thoughtBubble.ShowPriorityMessage("На дежурстве", 2f, Color.white);

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
        return currentState == InternState.OnBreak ||
               currentState == InternState.GoingToBreak ||
               currentState == InternState.AtToilet ||
               currentState == InternState.GoingToToilet;
    }

    public override string GetStatusInfo()
    {
        return currentState.ToString();
    }

    public void InitializeFromData(RoleData data)
    {
        if (agentMover != null)
        {
            agentMover.moveSpeed = data.moveSpeed;
            agentMover.priority = data.priority;
        }

        this.spriteCollection = data.spriteCollection;
        this.stateEmotionMap = data.stateEmotionMap;

        if (visuals != null)
        {
            visuals.EquipAccessory(data.accessoryPrefab);
        }
    }

    #region IServiceProvider Implementation

    public bool IsAvailableToServe => GetCurrentState() == InternState.CoveringDesk;

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
        StartCoroutine(InternServiceRoutine(client));
    }

    private IEnumerator InternServiceRoutine(ClientPathfinding client)
    {
        if (client == null || coveredServicePoint == null) yield break;

        int deskId = coveredServicePoint.deskId;

        var durability = coveredServicePoint.GetComponent<Gameplay.OfficeObjectDurability>();
        if (durability != null && !durability.IsUsable())
        {
            thoughtBubble?.ShowPriorityMessage("Стол сломан!\nЯ не могу...", 3f, Color.red);
            yield break;
        }
        float efficiency = (durability != null) ? durability.GetEfficiencyMultiplier() : 1.0f;

        if (deskId == 0)
        {
            thoughtBubble?.ShowPriorityMessage("Попробую помочь...", 2f, Color.yellow);
            yield return new WaitForSeconds(3f / efficiency);

            Waypoint destination = DetermineCorrectGoalForClient(client);

            float errorChance = 0.4f * (1f - skills.pedantry);
            if (Random.value < errorChance)
                thoughtBubble?.ShowPriorityMessage("Ой, кажется, вам\nтуда...", 3f, Color.red);
            else
                thoughtBubble?.ShowPriorityMessage($"Вам к '{(destination != null ? destination.name : "Выход")}'", 3f, Color.white);

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
            thoughtBubble?.ShowPriorityMessage("Принимаю оплату...", 2f, Color.yellow);
            yield return new WaitForSeconds(3f / efficiency);

            if (client.billToPay > 0)
            {
                PlayerWallet.Instance?.AddMoney(client.billToPay, "Оплата (Стажер)");
                if (client.paymentSound != null) AudioSource.PlayClipAtPoint(client.paymentSound, transform.position);
                client.billToPay = 0;
                coveredServicePoint.documentStack?.AddDocumentToStack();
                thoughtBubble?.ShowPriorityMessage("Оплачено!", 2f, Color.green);
            }
            client.isLeavingSuccessfully = true;
            client.reasonForLeaving = ClientPathfinding.LeaveReason.Processed;
            client.stateMachine?.SetGoal(ClientSpawner.Instance?.exitWaypoint);
            client.stateMachine?.SetState(ClientState.Leaving);
        }
        else if (deskId == 1 || deskId == 2)
        {
            thoughtBubble?.ShowPriorityMessage("Так... посмотрим...", 2f, Color.yellow);
            yield return new WaitForSeconds(1.5f / efficiency);

            if (client.docHolder == null)
            {
                thoughtBubble?.ShowPriorityMessage("Ошибка: нет документа!", 3f, Color.red);
                yield break;
            }

            DocumentType requiredDoc = (deskId == 1) ? DocumentType.Form1 : DocumentType.Form2;

            if (client.docHolder.GetCurrentDocumentType() != requiredDoc)
            {
                thoughtBubble?.ShowPriorityMessage("У вас бланк не тот!", 3f, Color.red);
                yield return new WaitForSeconds(2f);
                client.stateMachine?.GoGetFormAndReturn();
            }
            else
            {
                float processingTime = Random.Range(5f, 8f) / efficiency;
                yield return new WaitForSeconds(processingTime);

                client.docHolder.SetDocument(DocumentType.None);
                if (client.stampSound != null) AudioSource.PlayClipAtPoint(client.stampSound, transform.position);
                yield return new WaitForSeconds(1f);

                DocumentType newDocType = (deskId == 1) ? DocumentType.Certificate1 : DocumentType.Certificate2;
                client.docHolder.SetDocument(newDocType);
                client.billToPay += (deskId == 1) ? 100 : 250;

                thoughtBubble?.ShowPriorityMessage("Готово!", 3f, Color.green);
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
            case ClientGoal.VisitToilet: return ClientSpawner.GetToiletZone()?.waitingWaypoint;
            default: return ClientQueueManager.Instance.ChooseNewGoal(client);
        }
    }
    #endregion
}
