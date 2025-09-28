// Файл: Scripts/Characters/Controllers/ClerkController.cs --- ПОЛНАЯ ОБНОВЛЕННАЯ ВЕРСИЯ ---
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(AgentMover))]
[RequireComponent(typeof(CharacterStateLogger))]
[RequireComponent(typeof(StackHolder))]
public class ClerkController : StaffController
{
    public enum ClerkState { Working, GoingToBreak, OnBreak, ReturningToWork, GoingToToilet, AtToilet, Inactive, StressedOut, GoingToArchive, AtArchive }
    public enum ClerkRole { Regular, Cashier, Registrar, Archivist }

    [Header("Настройки клерка")]
    public ClerkRole role = ClerkRole.Regular;
    public ServicePoint assignedServicePoint;

    // <<< --- ИЗМЕНЕНИЕ: Все поля [SerializeField] и public УДАЛЕНЫ отсюда --- >>>
    // Теперь это просто внутренние переменные, которые заполняются из RoleData
    private float timeInToilet;
    private float clientArrivalTimeout;
    private float maxStress;
    private float stressGainPerClient;
    private float stressReliefRate;

    private ClerkState currentState = ClerkState.Inactive;
    private bool isWaitingForClient = false;
    private StackHolder stackHolder;
    private Waypoint[] allWaypoints;

    public override bool IsOnBreak()
    {
        return currentState == ClerkState.OnBreak ||
               currentState == ClerkState.GoingToBreak || 
               currentState == ClerkState.AtToilet || 
               currentState == ClerkState.GoingToToilet ||
               currentState == ClerkState.StressedOut;
    }

    public ClerkState GetCurrentState() => currentState;

    public override string GetStatusInfo()
    {
        switch (currentState)
        {
            case ClerkState.Working: return role == ClerkRole.Archivist ? "Работает в архиве" : $"Работает: {assignedServicePoint?.name}";
            case ClerkState.OnBreak: return "На перерыве";
            case ClerkState.AtToilet: return "В туалете";
            case ClerkState.StressedOut: return "СОРВАЛСЯ!";
            case ClerkState.GoingToArchive: return "Несет документы в архив";
            case ClerkState.Inactive: return "Вне смены";
            default: return currentState.ToString();
        }
    }
    
    // <<< --- ИЗМЕНЕНИЕ: Главный метод инициализации --- >>>
    public void InitializeFromData(RoleData data)
    {
        var mover = GetComponent<AgentMover>();
        if (mover != null)
        {
            mover.moveSpeed = data.moveSpeed;
            mover.priority = data.priority;
            mover.idleSprite = data.idleSprite;
            mover.walkSprite1 = data.walkSprite1;
            mover.walkSprite2 = data.walkSprite2;
        }
        
        this.spriteCollection = data.spriteCollection;
        this.stateEmotionMap = data.stateEmotionMap;
        this.visuals?.EquipAccessory(data.accessoryPrefab);

        this.timeInToilet = data.clerk_timeInToilet;
        this.clientArrivalTimeout = data.clerk_clientArrivalTimeout;
        this.maxStress = data.clerk_maxStress;
        this.stressGainPerClient = data.clerk_stressGainPerClient;
        this.stressReliefRate = data.clerk_stressReliefRate;
    }
    
    public void ServiceComplete()
    {
        isWaitingForClient = false;
        // <<< ИСПРАВЛЕНО: currentStress заменен на currentFrustration >>>
        currentFrustration += stressGainPerClient;
        if (assignedServicePoint != null && assignedServicePoint.documentStack != null)
        {
            if (role == ClerkRole.Cashier || role == ClerkRole.Regular)
            {
                assignedServicePoint.documentStack.AddDocumentToStack();
            }
        }
    }
    
    protected override void Awake()
    {
        base.Awake();
        stackHolder = GetComponent<StackHolder>();
        allWaypoints = FindObjectsByType<Waypoint>(FindObjectsSortMode.None);
    }

    protected override void Start()
    {
        base.Start();
        SetState(ClerkState.Inactive);
    }

    // Update убран, так как логика стресса теперь в базовом классе или по факту действий

    protected override bool CanExecuteActionConditions(ActionType actionType)
{
    switch (actionType)
    {
        case ActionType.ProcessDocument:
            return role == ClerkRole.Registrar && !isWaitingForClient && ClientQueueManager.Instance.CanCallClient(this);
        case ActionType.ArchiveDocument:
            return role == ClerkRole.Archivist && ArchiveManager.Instance.GetStackToProcess().CurrentSize > 0;
        case ActionType.TakeStackToArchive:
            return (role == ClerkRole.Regular || role == ClerkRole.Registrar) && assignedServicePoint.documentStack.IsFull;
    }
    return false;
}

protected override IEnumerator ExecuteDefaultAction()
{
    yield return StartCoroutine(ReturnToWorkRoutine());
}

    protected override IEnumerator ExecuteActionCoroutine(ActionType actionType)
    {
        // ... (этот код остается без изменений) ...
        switch (actionType)
        {
            case ActionType.ProcessDocument:
                yield return StartCoroutine(CallNextClientRoutine());
                break;
            case ActionType.ArchiveDocument:
                yield return StartCoroutine(ProcessArchiveStackRoutine());
                break;
            case ActionType.TakeStackToArchive:
                if (assignedServicePoint != null)
                {
                    yield return StartCoroutine(TakeStackToArchiveRoutine(assignedServicePoint.documentStack));
                }
                break;
        }
    }

    private IEnumerator CallNextClientRoutine()
    {
        isWaitingForClient = true;
        bool clientCalled = ClientQueueManager.Instance.CallNextClient(this);
        if (clientCalled)
        {
            yield return new WaitForSeconds(clientArrivalTimeout);
        }
        else
        {
            yield return new WaitForSeconds(2f);
        }
        isWaitingForClient = false;
        currentAction = null;
    }
    
    private IEnumerator ProcessArchiveStackRoutine()
    {
        DocumentStack stack = ArchiveManager.Instance.GetStackToProcess();
        yield return StartCoroutine(MoveToTarget(stack.transform.position, ClerkState.Working));

        if (stack.TakeOneDocument())
        {
            stackHolder.ShowSingleDocumentSprite();
            ArchiveCabinet cabinet = ArchiveManager.Instance.GetRandomCabinet();
            if (cabinet != null)
            {
                yield return StartCoroutine(MoveToTarget(cabinet.transform.position, ClerkState.Working));
                stackHolder.HideStack();
                ExperienceManager.Instance?.GrantXP(this, ActionType.ArchiveDocument);
                yield return new WaitForSeconds(1f);
            }
        }
        currentAction = null;
    }

    private IEnumerator TakeStackToArchiveRoutine(DocumentStack stack)
    {
        SetState(ClerkState.GoingToArchive);
        ClientSpawner.ReportDeskOccupation(assignedServicePoint.deskId, null);
        
        Transform dropOffPoint = ArchiveManager.Instance.RequestDropOffPoint();
        if (dropOffPoint != null)
        {
            yield return StartCoroutine(MoveToTarget(dropOffPoint.position, ClerkState.AtArchive));
            int takenDocs = stack.TakeEntireStack();
            for (int i = 0; i < takenDocs; i++)
            {
                ArchiveManager.Instance.mainDocumentStack.AddDocumentToStack();
            }
            yield return new WaitForSeconds(1f);
            ArchiveManager.Instance.FreeOverflowPoint(dropOffPoint);
            ExperienceManager.Instance?.GrantXP(this, ActionType.DeliverDocuments);
        }
        currentAction = null;
    }

    private IEnumerator ReturnToWorkRoutine()
    {
        if (assignedServicePoint != null)
        {
            SetState(ClerkState.ReturningToWork);
            yield return StartCoroutine(MoveToTarget(assignedServicePoint.clerkStandPoint.position, ClerkState.Working));
            ClientSpawner.ReportDeskOccupation(assignedServicePoint.deskId, this);
        }
        currentAction = null;
    }
    
    public override void GoOnBreak(float duration)
    {
        if (!isOnDuty) return;
        currentAction = StartCoroutine(BreakRoutine(duration));
    }
    
    private IEnumerator BreakRoutine(float duration)
    {
        SetState(ClerkState.GoingToBreak);
        if (assignedServicePoint != null) ClientSpawner.ReportDeskOccupation(assignedServicePoint.deskId, null);
        
        Transform breakSpot = RequestKitchenPoint();
        if (breakSpot != null)
        {
            yield return StartCoroutine(MoveToTarget(breakSpot.position, ClerkState.OnBreak));
            yield return new WaitForSeconds(duration);
            FreeKitchenPoint(breakSpot);
        }
        else
        {
            yield return new WaitForSeconds(duration);
        }
        currentAction = null;
    }

    private IEnumerator MoveToTarget(Vector2 targetPosition, ClerkState stateOnArrival)
    {
        agentMover.SetPath(BuildPathTo(targetPosition));
        yield return new WaitUntil(() => !agentMover.IsMoving());
        SetState(stateOnArrival);
    }

    private void SetState(ClerkState newState)
    {
        if (currentState == newState) return;
        currentState = newState;
        logger?.LogState(GetStatusInfo());
        visuals?.SetEmotionForState(newState);
    }

    protected override Queue<Waypoint> BuildPathTo(Vector2 targetPos)
    {
        // ... (логика поиска пути, которую мы позже вынесем в Utility) ...
        var path = new Queue<Waypoint>();
        if (allWaypoints == null || allWaypoints.Length == 0) return path;
        Waypoint startNode = allWaypoints.Where(wp => wp != null).OrderBy(wp => Vector2.Distance(transform.position, wp.transform.position)).FirstOrDefault();
        Waypoint endNode = allWaypoints.Where(wp => wp != null).OrderBy(wp => Vector2.Distance(targetPos, wp.transform.position)).FirstOrDefault();
        if (startNode == null || endNode == null) return path;
        
        var distances = new Dictionary<Waypoint, float>();
        var previous = new Dictionary<Waypoint, Waypoint>();
        var unvisited = new List<Waypoint>(allWaypoints.Where(wp => wp != null));

        foreach (var wp in unvisited)
        {
            distances[wp] = float.MaxValue;
            previous[wp] = null;
        }
        distances[startNode] = 0;
        
        while (unvisited.Count > 0)
        {
            unvisited.Sort((a,b) => distances[a].CompareTo(distances[b]));
            Waypoint current = unvisited[0];
            unvisited.Remove(current);

            if (current == endNode)
            {
                var pathList = new List<Waypoint>();
                for (Waypoint at = endNode; at != null; at = previous.ContainsKey(at) ? previous[at] : null) { pathList.Add(at); }
                pathList.Reverse();
                path.Clear();
                foreach(var wp in pathList) { path.Enqueue(wp); }
                return path;
            }

            if (current.neighbors == null) continue;
            foreach (var neighbor in current.neighbors)
            {
                if (neighbor == null || (neighbor.forbiddenTags != null && neighbor.forbiddenTags.Contains(gameObject.tag))) continue;
                float alt = distances[current] + Vector2.Distance(current.transform.position, neighbor.transform.position);
                if (distances.ContainsKey(neighbor) && alt < distances[neighbor])
                {
                    distances[neighbor] = alt;
                    previous[neighbor] = current;
                }
            }
        }
        return path;
    }

    // <<< ИСПРАВЛЕНО: методы Get/SetStressValue теперь работают с currentFrustration >>>
    public override float GetStressValue() { return currentFrustration; }
    public override void SetStressValue(float stress) { currentFrustration = stress; }
}