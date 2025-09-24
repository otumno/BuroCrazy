// Файл: ClerkController.cs (полностью переработанная версия)
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

    [Header("Внешний вид")]
    public EmotionSpriteCollection spriteCollection;
    public StateEmotionMap stateEmotionMap;
    
    [Header("Поведение")]
    public float timeInToilet = 10f;
    public float clientArrivalTimeout = 16f;
    
    [Header("Стресс")]
    public float maxStress = 100f;
    public float stressGainPerClient = 5f;
    public float stressReliefRate = 10f;
    private float currentStress = 0f;

    private ClerkState currentState = ClerkState.Inactive;
    private bool isWaitingForClient = false;
    private StackHolder stackHolder;
    private Waypoint[] allWaypoints;

    // --- ПУБЛИЧНЫЕ МЕТОДЫ ДЛЯ ВНЕШНИХ СИСТЕМ ---

public bool IsOnBreak()
{
    // Клерк считается "на перерыве", если он находится в одном из этих состояний
    return currentState == ClerkState.OnBreak || 
           currentState == ClerkState.GoingToBreak || 
           currentState == ClerkState.AtToilet || 
           currentState == ClerkState.GoingToToilet ||
           currentState == ClerkState.StressedOut; // Стресс - это тоже своего рода "перерыв"
}

    public ClerkState GetCurrentState() => currentState;

    public string GetStatusInfo()
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
    
    public void InitializeFromData(RoleData data)
    {
        var mover = GetComponent<AgentMover>();
        if (mover != null)
        {
            mover.moveSpeed = data.moveSpeed;
            mover.priority = data.priority;
        }
        this.spriteCollection = data.spriteCollection;
        this.stateEmotionMap = data.stateEmotionMap;
        // У клерка нет аксессуаров, так что это поле мы не используем
    }
    
    public void ServiceComplete()
    {
        isWaitingForClient = false;
        currentStress += stressGainPerClient;
        if (assignedServicePoint != null && assignedServicePoint.documentStack != null)
        {
            if (role == ClerkRole.Cashier || role == ClerkRole.Regular)
            {
                assignedServicePoint.documentStack.AddDocumentToStack();
            }
        }
    }
    
    // --- МЕТОДЫ UNITY ---

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

    void Update()
    {
        if (Time.timeScale == 0f || !isOnDuty) return;
        UpdateStress();
    }
    
    // --- "МОЗГ" КЛЕРКА: РЕАЛИЗАЦИЯ МЕТОДОВ ИЗ STAFFCONTROLLER ---

    protected override bool TryExecuteAction(ActionType actionType)
    {
        switch (actionType)
        {
            case ActionType.ProcessDocument:
                if (role == ClerkRole.Registrar && !isWaitingForClient && ClientQueueManager.Instance.CanCallClient(this))
                {
                    currentAction = StartCoroutine(CallNextClientRoutine());
                    return true;
                }
                return false;

            case ActionType.ArchiveDocument:
                if (role == ClerkRole.Archivist && ArchiveManager.Instance.GetStackToProcess().CurrentSize > 0)
                {
                    currentAction = StartCoroutine(ProcessArchiveStackRoutine());
                    return true;
                }
                return false;
            
            case ActionType.TakeStackToArchive:
                if ((role == ClerkRole.Regular || role == ClerkRole.Registrar) && assignedServicePoint.documentStack.IsFull)
                {
                    currentAction = StartCoroutine(TakeStackToArchiveRoutine(assignedServicePoint.documentStack));
                    return true;
                }
                return false;
        }
        return false;
    }

    protected override void ExecuteDefaultAction()
    {
        currentAction = StartCoroutine(ReturnToWorkRoutine());
    }
    
    // --- РЕАЛИЗАЦИЯ ПОВЕДЕНИЯ (КОРУТИНЫ) ---

    private IEnumerator CallNextClientRoutine()
    {
        isWaitingForClient = true;
        bool clientCalled = ClientQueueManager.Instance.CallNextClient(this);
        if (clientCalled)
        {
            yield return new WaitForSeconds(clientArrivalTimeout); // Ждем подхода клиента
        }
        else
        {
            yield return new WaitForSeconds(2f); // Небольшая задержка, если очередь пуста
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
            ExperienceManager.Instance?.GrantXP(this, ActionType.DeliverDocuments); // Используем от стажера
        }
        currentAction = null; // Завершаем, ExecuteDefaultAction вернет нас на место
    }

    private IEnumerator ReturnToWorkRoutine()
    {
        if (role == ClerkRole.Archivist)
        {
            // Архивариус просто ждет на своей точке
            // TODO: нужна точка ожидания для архивариуса
        }
        else if (assignedServicePoint != null)
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
	 // --- ВСПОМОГАТЕЛЬНЫЕ И УНАСЛЕДОВАННЫЕ МЕТОДЫ ---
    
    private void UpdateStress()
    {
        if (currentState == ClerkState.StressedOut) return;
        bool isResting = currentState == ClerkState.OnBreak || currentState == ClerkState.AtToilet || currentState == ClerkState.Inactive;
        
        float finalStressGainRate = 0.5f; // Базовое значение, можно вынести в public поле
        if (skills != null)
        {
            finalStressGainRate *= (1f - skills.softSkills * 0.5f);
        }

        if (isOnDuty && !isResting)
        {
            currentStress += finalStressGainRate * Time.deltaTime;
        }
        else
        {
            currentStress -= stressReliefRate * Time.deltaTime;
        }
        currentStress = Mathf.Clamp(currentStress, 0, maxStress);
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
        LogCurrentState();
        visuals?.SetEmotionForState(newState);
    }
    
    private void LogCurrentState()
    {
        logger?.LogState(GetStatusInfo());
    }

    protected override Queue<Waypoint> BuildPathTo(Vector2 targetPos)
    {
        var path = new Queue<Waypoint>();
        Waypoint startNode = FindNearestVisibleWaypoint();
        Waypoint endNode = FindNearestVisibleWaypoint(targetPos);
        if (startNode == null || endNode == null) return path;

        Dictionary<Waypoint, float> distances = new Dictionary<Waypoint, float>();
        Dictionary<Waypoint, Waypoint> previous = new Dictionary<Waypoint, Waypoint>();
        var queue = new PriorityQueue<Waypoint>();

        foreach (var wp in allWaypoints)
        {
            if (wp != null)
            {
                distances[wp] = float.MaxValue;
                previous[wp] = null;
            }
        }
        
        distances[startNode] = 0;
        queue.Enqueue(startNode, 0);
        
        while (queue.Count > 0)
        {
            Waypoint current = queue.Dequeue();
            if (current == endNode)
            {
                ReconstructPath(previous, endNode, path);
                return path;
            }

            foreach (var neighbor in current.neighbors)
            {
                if (neighbor == null) continue;
                if (neighbor.forbiddenTags != null && neighbor.forbiddenTags.Contains(gameObject.tag)) continue;

                float newDist = distances[current] + Vector2.Distance(current.transform.position, neighbor.transform.position);
                if (distances.ContainsKey(neighbor) && newDist < distances[neighbor])
                {
                    distances[neighbor] = newDist;
                    previous[neighbor] = current;
                    queue.Enqueue(neighbor, newDist);
                }
            }
        }
        return path;
    }

    private void ReconstructPath(Dictionary<Waypoint, Waypoint> previous, Waypoint goal, Queue<Waypoint> path)
    {
        List<Waypoint> pathList = new List<Waypoint>();
        for (Waypoint at = goal; at != null; at = previous.ContainsKey(at) ? previous[at] : null)
        {
            pathList.Add(at);
        }
        pathList.Reverse();
        path.Clear();
        foreach (var wp in pathList)
        {
            path.Enqueue(wp);
        }
    }

    private Waypoint FindNearestVisibleWaypoint(Vector2? position = null)
    {
        Vector2 pos = position ?? (Vector2)transform.position;
        if (allWaypoints == null) return null;
        
        Waypoint bestWaypoint = null;
        float minDistance = float.MaxValue;
        foreach (var wp in allWaypoints)
        {
            if (wp == null) continue;
            float distance = Vector2.Distance(pos, wp.transform.position);
            if (distance < minDistance)
            {
                RaycastHit2D hit = Physics2D.Linecast(pos, wp.transform.position, LayerMask.GetMask("Obstacles"));
                if (hit.collider == null)
                {
                    minDistance = distance;
                    bestWaypoint = wp;
                }
            }
        }
        return bestWaypoint;
    }
    
    private class PriorityQueue<Waypoint>
    {
        private List<KeyValuePair<Waypoint, float>> elements = new List<KeyValuePair<Waypoint, float>>();
        public int Count => elements.Count;
        
        public void Enqueue(Waypoint item, float priority)
        {
            elements.Add(new KeyValuePair<Waypoint, float>(item, priority));
        }
        
        public Waypoint Dequeue()
        {
            int bestIndex = 0;
            for (int i = 0; i < elements.Count; i++)
            {
                if (elements[i].Value < elements[bestIndex].Value)
                {
                    bestIndex = i;
                }
            }
            Waypoint bestItem = elements[bestIndex].Key;
            elements.RemoveAt(bestIndex);
            return bestItem;
        }
    }

    public override float GetStressValue() 
    { 
        return currentStress; 
    }
    
    public override void SetStressValue(float stress) 
    { 
        currentStress = stress; 
    }
}
