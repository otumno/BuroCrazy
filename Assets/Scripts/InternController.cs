// Файл: InternController.cs
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(AgentMover))]
[RequireComponent(typeof(CharacterStateLogger))]
[RequireComponent(typeof(StackHolder))]
public class InternController : StaffController
{
    public enum InternState { Patrolling, HelpingConfused, ServingFromQueue, CoveringDesk, GoingToBreak, OnBreak, GoingToToilet, AtToilet, ReturningToPatrol, Inactive, Working, TalkingToConfused, TakingStackToArchive }
    
    [Header("Настройки стажера")]
    private InternState currentState = InternState.Inactive;

    [Header("Внешний вид")]
    [Tooltip("Укажите пол для выбора правильных спрайтов")]
    public Gender gender;
    private CharacterVisuals visuals;

    [Header("Основные параметры")]
    public List<Transform> patrolPoints;
    
    [Header("Параметры поведения")]
    public float helpCheckInterval = 2f;
    public float chanceToServeFromQueue = 0.1f;
    public float chanceToCoverDesk = 0.5f;
    public float chanceToGoToToilet = 0.008f;
    public float timeInToilet = 4f;
    [Tooltip("Через сколько секунд принудительно сбросить задачу, если стажер завис")]
    public float taskTimeout = 30f;
    
    private static List<ClerkController> clerksBeingCovered = new List<ClerkController>();
    private Waypoint[] allWaypoints;
    private Transform currentPatrolTarget;
    private float helpTimer = 0f;
    private ClientPathfinding helpTarget = null;
    private ClerkController clerkToCover = null;
    private float timeInCurrentTask = 0f;
    private StackHolder stackHolder;
    
    protected override void Awake()
    {
        base.Awake();
        visuals = GetComponent<CharacterVisuals>();
        allWaypoints = FindObjectsByType<Waypoint>(FindObjectsSortMode.None);
        stackHolder = GetComponent<StackHolder>(); 
    }

    protected override void Start()
    {
        base.Start();
        visuals?.Setup(gender);
        currentState = InternState.Inactive;
        LogCurrentState(currentState);
    }
    
    public override void StartShift() 
    { 
        if (isOnDuty) return;
        if (currentAction != null) StopCoroutine(currentAction); 
        currentAction = StartCoroutine(StartShiftRoutine());
    }

    public override void EndShift() 
    { 
        if (!isOnDuty) return;
        if (currentAction != null) StopCoroutine(currentAction); 
        currentAction = StartCoroutine(EndShiftRoutine());
    }

    void Update()
    {
        if (!isOnDuty || Time.timeScale == 0f) { if (agentMover != null) agentMover.Stop();
            return; }

        helpTimer += Time.deltaTime;
        if (helpTimer >= helpCheckInterval)
        {
            helpTimer = 0f;
            LookForSomeoneToHelp();
        }

        bool isInteractiveTask = currentState == InternState.HelpingConfused ||
                                 currentState == InternState.ServingFromQueue ||
                                 currentState == InternState.CoveringDesk ||
                                 currentState == InternState.Working ||
                                 currentState == InternState.TakingStackToArchive ||
                                 currentState == InternState.TalkingToConfused;
        if (isInteractiveTask)
        {
            timeInCurrentTask += Time.deltaTime;
            if (timeInCurrentTask > taskTimeout)
            {
                ForceReset("Таймаут интерактивной задачи");
            }
        }
    }

    private void SetState(InternState newState)
    {
        if (currentState == newState) return;
        currentState = newState;
        LogCurrentState(newState);
        timeInCurrentTask = 0f;

        visuals?.SetEmotionForState(newState);
    }

    private void LogCurrentState(InternState state) { logger?.LogState(state.ToString());
    }
    public InternState GetCurrentState() => currentState;

    private void ForceReset(string reason)
    {
        Debug.LogWarning($"Стажер {gameObject.name} принудительно сброшен. Причина: {reason}");
        if (currentAction != null)
        {
            StopCoroutine(currentAction);
        }
        
        currentAction = StartCoroutine(ReturnToPatrolRoutine());
    }

    private void LookForSomeoneToHelp()
    {
        if (currentState != InternState.Patrolling) return;
        
        DocumentStack[] allStacks = FindObjectsByType<DocumentStack>(FindObjectsSortMode.None);
        DocumentStack stackToHelp = allStacks
            .Where(s => s != null && !s.IsEmpty && (ArchiveManager.Instance == null || s != ArchiveManager.Instance.mainDocumentStack))
            .OrderByDescending(s => s.CurrentSize)
            .FirstOrDefault();

        if (stackToHelp != null && (stackToHelp.IsFull || Random.value < 0.2f))
        {
            if (currentAction != null) StopCoroutine(currentAction);
            currentAction = StartCoroutine(TakeStackToArchiveRoutine(stackToHelp));
            return;
        }
        
        ClerkController absentClerk = ClientSpawner.GetAbsentClerk();
        if (absentClerk != null && absentClerk.role != ClerkController.ClerkRole.Cashier && !clerksBeingCovered.Contains(absentClerk) && Random.value < chanceToCoverDesk)
        {
            if (currentAction != null) StopCoroutine(currentAction);
            currentAction = StartCoroutine(CoverDeskRoutine(absentClerk));
            return;
        }

        ClientPathfinding confusedClient = ClientPathfinding.FindClosestConfusedClient(transform.position);
        if (confusedClient != null && confusedClient.stateMachine.GetCurrentState() == ClientState.Confused)
        {
            if (currentAction != null) StopCoroutine(currentAction);
            helpTarget = confusedClient;
            currentAction = StartCoroutine(HelpConfusedClientRoutine(confusedClient));
            return;
        }

        if (Random.value < chanceToServeFromQueue)
        {
            if (ClientQueueManager.Instance != null && ClientQueueManager.Instance.GetRandomClientFromQueue() is ClientPathfinding queueClient && queueClient != null)
            {
                if (currentAction != null) StopCoroutine(currentAction);
                helpTarget = queueClient;
                currentAction = StartCoroutine(ServeFromQueueRoutine(queueClient));
            }
        }
    }

    private IEnumerator MoveToTarget(Vector2 targetPosition, InternState stateOnArrival)
    {
        agentMover.SetPath(BuildPathTo(targetPosition));
        yield return new WaitUntil(() => !agentMover.IsMoving());
        SetState(stateOnArrival);
    }

    private IEnumerator CoverDeskRoutine(ClerkController clerk)
    {
        if (clerksBeingCovered.Contains(clerk))
        {
            currentAction = StartCoroutine(ReturnToPatrolRoutine());
            yield break;
        }

        clerksBeingCovered.Add(clerk);
        clerkToCover = clerk;

        SetState(InternState.CoveringDesk);
        yield return StartCoroutine(MoveToTarget(clerk.assignedServicePoint.clerkStandPoint.position, InternState.Working));
        while (clerkToCover != null && clerkToCover.IsOnBreak() && Vector2.Distance(clerkToCover.transform.position, clerkToCover.assignedServicePoint.clerkStandPoint.position) > 1.5f)
        {
            yield return new WaitForSeconds(0.5f);
        }

        if (clerksBeingCovered.Contains(clerk))
        {
            clerksBeingCovered.Remove(clerk);
        }
        clerkToCover = null;
        yield return StartCoroutine(ReturnToPatrolRoutine());
    }

    private IEnumerator HelpConfusedClientRoutine(ClientPathfinding client)
    {
        SetState(InternState.HelpingConfused);
        if (client == null)
        {
            ForceReset("Цель (клиент) исчезла до начала движения.");
            yield break;
        }
        yield return StartCoroutine(MoveToTarget(client.transform.position, InternState.TalkingToConfused));
        if (client == null || client.stateMachine.GetCurrentState() != ClientState.Confused)
        {
            helpTarget = null;
            yield return StartCoroutine(ReturnToPatrolRoutine());
            yield break;
        }

        yield return new WaitForSeconds(1f);
        if (client != null)
        {
            Waypoint correctGoal = DetermineCorrectGoalFor(client);
            client.stateMachine.GetHelpFromIntern(correctGoal);
        }

        helpTarget = null;
        yield return StartCoroutine(ReturnToPatrolRoutine());
    }

    private Waypoint DetermineCorrectGoalFor(ClientPathfinding client)
    {
        if (client == null) return ClientQueueManager.Instance.ChooseNewGoal(client);
        DocumentType doc = client.docHolder.GetCurrentDocumentType();
        ClientGoal goal = client.mainGoal;

        if (client.billToPay > 0) return ClientSpawner.GetCashierZone().waitingWaypoint;
        switch (goal)
        {
            case ClientGoal.GetCertificate1:
                if (doc == DocumentType.None || doc == DocumentType.Form2) return ClientSpawner.Instance.formTable.tableWaypoint;
                if (doc == DocumentType.Form1) return ClientSpawner.GetDesk1Zone().waitingWaypoint;
                if (doc == DocumentType.Certificate1) return ClientSpawner.GetCashierZone().waitingWaypoint;
                break;
            case ClientGoal.GetCertificate2:
                if (doc == DocumentType.None || doc == DocumentType.Form1) return ClientSpawner.Instance.formTable.tableWaypoint;
                if (doc == DocumentType.Form2) return ClientSpawner.GetDesk2Zone().waitingWaypoint;
                if (doc == DocumentType.Certificate2) return ClientSpawner.GetCashierZone().waitingWaypoint;
                break;
            case ClientGoal.PayTax:
                return ClientSpawner.GetCashierZone().waitingWaypoint;
            case ClientGoal.VisitToilet:
                return ClientSpawner.GetToiletZone().waitingWaypoint;
        }
        return ClientQueueManager.Instance.ChooseNewGoal(client);
    }

    private IEnumerator ServeFromQueueRoutine(ClientPathfinding client)
    {
        SetState(InternState.ServingFromQueue);
        if (client == null)
        {
            ForceReset("Цель (клиент) исчезла до начала движения.");
            yield break;
        }
        yield return StartCoroutine(MoveToTarget(client.transform.position, InternState.Patrolling));
        if (client != null)
        {
            float choice = Random.value;
            if (choice < 0.5f) client.stateMachine.GetHelpFromIntern(ClientSpawner.GetDesk1Zone().waitingWaypoint);
            else if (choice < 0.8f) client.stateMachine.GetHelpFromIntern(ClientSpawner.GetDesk2Zone().waitingWaypoint);
            else client.stateMachine.GetHelpFromIntern(ClientSpawner.Instance.exitWaypoint);
        }
        helpTarget = null;
        yield return StartCoroutine(ReturnToPatrolRoutine());
    }

    private IEnumerator TakeStackToArchiveRoutine(DocumentStack stack)
    {
        SetState(InternState.TakingStackToArchive);
        yield return StartCoroutine(MoveToTarget(stack.transform.position, InternState.Working));

        Transform dropOffPoint = ArchiveManager.Instance.RequestDropOffPoint();

        if (dropOffPoint != null)
        {
            int docCount = stack.CurrentSize;
            stackHolder.ShowStack(docCount, stack.maxStackSize);
            
            yield return StartCoroutine(MoveToTarget(dropOffPoint.position, InternState.Working));
            
            int takenDocs = stack.TakeEntireStack();
            for (int i = 0; i < takenDocs; i++)
            {
                ArchiveManager.Instance.mainDocumentStack.AddDocumentToStack();
            }
            stackHolder.HideStack();
            yield return new WaitForSeconds(1f);
        }
        else
        {
            Debug.LogWarning($"Стажер {name} не может отнести документы, архив переполнен!");
            yield return new WaitForSeconds(5f);
        }
        
        yield return StartCoroutine(ReturnToPatrolRoutine());
    }
    
    public void GoOnBreak(float duration) { if (currentAction != null) StopCoroutine(currentAction); currentAction = StartCoroutine(BreakRoutine(duration));
    }

    private IEnumerator StartShiftRoutine()
    {
        yield return new WaitForSeconds(Random.Range(1f, 5f));
        isOnDuty = true;
        yield return StartCoroutine(ReturnToPatrolRoutine());
    }

    private IEnumerator EndShiftRoutine()
    {
        isOnDuty = false;
        if (clerkToCover != null && clerksBeingCovered.Contains(clerkToCover)) { clerksBeingCovered.Remove(clerkToCover); }
        SetState(InternState.Inactive);
        if (homePoint != null)
        {
            yield return StartCoroutine(MoveToTarget(homePoint.position, InternState.Inactive));
        }
        currentAction = null;
    }

    private IEnumerator BreakRoutine(float duration)
    {
        isOnDuty = false;
        SetState(InternState.GoingToBreak);
        yield return StartCoroutine(MoveToTarget(kitchenPoint.position, InternState.OnBreak));
        yield return new WaitForSeconds(duration);
        isOnDuty = true;
        yield return StartCoroutine(ReturnToPatrolRoutine());
    }

    private IEnumerator ToiletBreakRoutine()
    {
        SetState(InternState.GoingToToilet);
        yield return StartCoroutine(MoveToTarget(staffToiletPoint.position, InternState.AtToilet));
        yield return new WaitForSeconds(timeInToilet);
        yield return StartCoroutine(ReturnToPatrolRoutine());
    }

    private IEnumerator ReturnToPatrolRoutine()
    {
        SetState(InternState.ReturningToPatrol);
        SelectNewPatrolPoint();
        if (currentPatrolTarget != null)
        {
            yield return StartCoroutine(MoveToTarget(currentPatrolTarget.position, InternState.Patrolling));
        }
        else
        {
            SetState(InternState.Patrolling);
        }
        currentAction = null;
    }

    private void SelectNewPatrolPoint() { if (patrolPoints == null || patrolPoints.Count == 0) return;
        currentPatrolTarget = patrolPoints[Random.Range(0, patrolPoints.Count)]; }

    private Queue<Waypoint> BuildPathTo(Vector2 targetPos)
    {
        var path = new Queue<Waypoint>();
        Waypoint startNode = FindNearestVisibleWaypoint(transform.position);
        Waypoint endNode = FindNearestVisibleWaypoint(targetPos);
        if (startNode == null || endNode == null) return path;
        Dictionary<Waypoint, float> distances = new Dictionary<Waypoint, float>();
        Dictionary<Waypoint, Waypoint> previous = new Dictionary<Waypoint, Waypoint>();
        var queue = new PriorityQueue<Waypoint>();
        foreach (var wp in allWaypoints) { distances[wp] = float.MaxValue; previous[wp] = null;
        }
        distances[startNode] = 0;
        queue.Enqueue(startNode, 0);
        while (queue.Count > 0)
        {
            Waypoint current = queue.Dequeue();
            if (current == endNode) { ReconstructPath(previous, endNode, path); return path;
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
        for (Waypoint at = goal; at != null; at = previous[at]) { pathList.Add(at);
        }
        pathList.Reverse();
        path.Clear();
        foreach (var wp in pathList) { path.Enqueue(wp);
        }
    }

    private Waypoint FindNearestVisibleWaypoint(Vector2 position)
    {
        if (allWaypoints == null) return null;
        Waypoint bestWaypoint = null;
        float minDistance = float.MaxValue;
        foreach (var wp in allWaypoints)
        {
            if (wp == null) continue;
            float distance = Vector2.Distance(position, wp.transform.position);
            if (distance < minDistance)
            {
                RaycastHit2D hit = Physics2D.Linecast(position, wp.transform.position, LayerMask.GetMask("Obstacles"));
                if (hit.collider == null) { minDistance = distance; bestWaypoint = wp;
                }
            }
        }
        return bestWaypoint;
    }

    private class PriorityQueue<T>
    {
        private List<KeyValuePair<T, float>> elements = new List<KeyValuePair<T, float>>();
        public int Count => elements.Count;
        public void Enqueue(T item, float priority) { elements.Add(new KeyValuePair<T, float>(item, priority));
        }
        public T Dequeue()
        {
            int bestIndex = 0;
            for (int i = 0; i < elements.Count; i++) { if (elements[i].Value < elements[bestIndex].Value) { bestIndex = i;
            } }
            T bestItem = elements[bestIndex].Key;
            elements.RemoveAt(bestIndex);
            return bestItem;
        }
    }
}