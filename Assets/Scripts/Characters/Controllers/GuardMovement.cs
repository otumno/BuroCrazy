using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(Rigidbody2D), typeof(AgentMover), typeof(CharacterStateLogger))]
public class GuardMovement : StaffController
{
    public enum GuardState { Patrolling, WaitingAtWaypoint, Chasing, Talking, OnPost, GoingToBreak, OnBreak, GoingToToilet, AtToilet, OffDuty, ChasingThief, EscortingThief, Evicting, StressedOut, WritingReport, OperatingBarrier }
    
    [Header("Настройки Охранника")]
    private GuardState currentState = GuardState.OffDuty;
    
    [Header("Внешний вид")]
    public EmotionSpriteCollection spriteCollection;
    public StateEmotionMap stateEmotionMap;

    [Header("Аксессуары")]
    public GameObject accessoryPrefab;
    
    [Header("Дополнительные объекты")]
    public GameObject nightLight;
    
    [Header("Параметры поведения")]
    [SerializeField] private float minWaitTime = 1f;
    [SerializeField] private float maxWaitTime = 3f;
    [SerializeField] private float chaseSpeedMultiplier = 1.5f;
    [SerializeField] private float talkTime = 3f;

    [Header("Стресс")]
    public float maxStress = 100f;
    public float stressGainPerViolator = 25f;
    public float stressReliefRate = 10f;
    private float currentStress = 0f;
    
    [Header("Рабочее место и протоколы")]
    [Tooltip("Точка, куда охранник пойдет писать протокол")]
    public Transform deskPoint;
    [Tooltip("Стопка, куда охранник будет складывать протоколы")]
    public DocumentStack protocolStack;

    private ClientPathfinding currentChaseTarget;
    private Rigidbody2D rb;
    private Waypoint[] allWaypoints;

    public GuardState GetCurrentState()
    {
        return currentState;
    }

    public bool IsAvailableAndOnDuty()
    {
        return isOnDuty && currentAction == null;
    }

    public string GetStatusInfo()
    {
        switch (currentState)
        {
            case GuardState.StressedOut: return "СОРВАЛСЯ!";
            case GuardState.WritingReport: return "Пишет протокол";
            case GuardState.Patrolling: return $"Патрулирует. Цель: {patrolPoints.FirstOrDefault()?.name}";
            case GuardState.Chasing: return $"Преследует: {currentChaseTarget?.name}";
            case GuardState.Talking: return $"Разговаривает с: {currentChaseTarget?.name}";
            case GuardState.OffDuty: return "Смена окончена";
            default: return currentState.ToString();
        }
    }
    
    public void AssignToChase(ClientPathfinding target)
    {
        if (!IsAvailableAndOnDuty()) return;
        if (currentAction != null) StopCoroutine(currentAction);
        currentAction = StartCoroutine(ChaseRoutine(target, GuardState.Chasing, ActionType.CalmDownViolator));
    }

    public void AssignToCatchThief(ClientPathfinding target)
    {
        if (!IsAvailableAndOnDuty()) return;
        if (currentAction != null) StopCoroutine(currentAction);
        currentAction = StartCoroutine(CatchThiefRoutine(target));
    }
    
    public void AssignToEvict(ClientPathfinding target)
    {
        if (!IsAvailableAndOnDuty()) return;
        if (currentAction != null) StopCoroutine(currentAction);
        currentAction = StartCoroutine(EvictRoutine(target));
    }

public void AssignToOperateBarrier(SecurityBarrier barrier, bool shouldActivate)
{
    if (!IsAvailableAndOnDuty()) return;
    if (currentAction != null) StopCoroutine(currentAction);
    currentAction = StartCoroutine(OperateBarrierRoutine(barrier, shouldActivate));
}

public void ReturnToPatrol()
{
    if (!isOnDuty) return;
    if (currentAction != null) StopCoroutine(currentAction);
    currentAction = null; // Это заставит ActionDecisionLoop запустить ExecuteDefaultAction (патрулирование)
}

// --- НОВАЯ КОРУТИНА для работы с барьером ---
private IEnumerator OperateBarrierRoutine(SecurityBarrier barrier, bool activate)
{
    SetState(GuardState.OperatingBarrier);
    yield return StartCoroutine(MoveToTarget(barrier.guardInteractionPoint.position, GuardState.OperatingBarrier));
    
    yield return new WaitForSeconds(2.5f);
    if (activate)
    {
        barrier.ActivateBarrier();
    }
    else
    {
        barrier.DeactivateBarrier();
    }
    ExperienceManager.Instance?.GrantXP(this, ActionType.OperateBarrier);
    currentAction = null;
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
        this.accessoryPrefab = data.accessoryPrefab;
    }
    
    protected override void Awake()
    {
        base.Awake();
        rb = GetComponent<Rigidbody2D>();
        allWaypoints = FindObjectsByType<Waypoint>(FindObjectsSortMode.None);
    }

    protected override void Start()
    {
        base.Start();
        SetState(GuardState.OffDuty);
    }
    
    void Update()
    {
        if (Time.timeScale == 0f || !isOnDuty) return;
        UpdateStress();
    }
    
    protected override bool TryExecuteAction(ActionType actionType)
    {
        switch (actionType)
        {
            case ActionType.CalmDownViolator:
                var violator = FindObjectsByType<ClientPathfinding>(FindObjectsSortMode.None)
                    .FirstOrDefault(c => c != null && c.stateMachine != null && c.stateMachine.GetCurrentState() == ClientState.Enraged);
                if (violator != null)
                {
                    currentAction = StartCoroutine(ChaseRoutine(violator, GuardState.Chasing, ActionType.CalmDownViolator));
                    return true;
                }
                return false;

            case ActionType.CatchThief:
                ClientPathfinding thief = GuardManager.Instance?.GetThiefToCatch();
                if (thief != null)
                {
                    currentAction = StartCoroutine(CatchThiefRoutine(thief));
                    return true;
                }
                return false;
                
            case ActionType.EvictClient:
                ClientPathfinding clientToEvict = GuardManager.Instance?.GetClientToEvict();
                if (clientToEvict != null)
                {
                    currentAction = StartCoroutine(EvictRoutine(clientToEvict));
                    return true;
                }
                return false;
        }
        return false;
    }

    protected override void ExecuteDefaultAction()
    {
        currentAction = StartCoroutine(PatrolRoutine());
    }

    private IEnumerator PatrolRoutine()
    {
        SetState(GuardState.Patrolling);
        var patrolTarget = SelectNewPatrolPoint();
        if (patrolTarget != null)
        {
            yield return StartCoroutine(MoveToTarget(patrolTarget.position, GuardState.WaitingAtWaypoint));
            ExperienceManager.Instance?.GrantXP(this, ActionType.PatrolWaypoint);
        }
        SetState(GuardState.WaitingAtWaypoint);
        yield return new WaitForSeconds(Random.Range(minWaitTime, maxWaitTime));
        currentAction = null;
    }
    
    private IEnumerator ChaseRoutine(ClientPathfinding target, GuardState chaseState, ActionType finalAction)
    {
        currentChaseTarget = target;
        SetState(chaseState);
        agentMover.ApplySpeedMultiplier(chaseSpeedMultiplier);
        
        float catchDistance = 1.2f;
        while (currentChaseTarget != null && Vector2.Distance(transform.position, currentChaseTarget.transform.position) > catchDistance)
        {
            agentMover.StartDirectChase(currentChaseTarget.transform.position);
            yield return new WaitForSeconds(0.2f);
        }
        
        agentMover.StopDirectChase();
        agentMover.ApplySpeedMultiplier(1f);

        if (currentChaseTarget != null)
        {
            agentMover.Stop();
            if(finalAction == ActionType.CalmDownViolator)
            {
                yield return StartCoroutine(TalkToClientRoutine(currentChaseTarget));
            }
        }
        
        if (finalAction != ActionType.CatchThief && finalAction != ActionType.EvictClient)
        {
             currentChaseTarget = null;
             currentAction = null;
        }
    }
    
    private IEnumerator TalkToClientRoutine(ClientPathfinding clientToCalm)
    {
        SetState(GuardState.Talking);
        clientToCalm.Freeze();
        yield return new WaitForSeconds(talkTime);
        if(clientToCalm != null)
        {
            ExperienceManager.Instance?.GrantXP(this, ActionType.CalmDownViolator);
            clientToCalm.UnfreezeAndRestartAI();
            if (Random.value < 0.5f) { clientToCalm.CalmDownAndReturnToQueue(); }
            else { clientToCalm.CalmDownAndLeave(); }
        }
        currentStress += stressGainPerViolator;
    }
    
    private IEnumerator CatchThiefRoutine(ClientPathfinding thief)
    {
        SetState(GuardState.ChasingThief);
        yield return StartCoroutine(ChaseRoutine(thief, GuardState.ChasingThief, ActionType.CatchThief));
        if (currentChaseTarget != null)
        {
            yield return StartCoroutine(EscortThiefToCashierRoutine(currentChaseTarget));
            yield return StartCoroutine(WriteReportRoutine());
        }
        currentAction = null;
    }

    private IEnumerator EscortThiefToCashierRoutine(ClientPathfinding thief)
    {
        SetState(GuardState.EscortingThief);
        thief.Freeze();
        yield return new WaitForSeconds(talkTime / 2);
        
        LimitedCapacityZone cashierZone = ClientSpawner.GetCashierZone();
        if (cashierZone != null)
        {
            thief.stateMachine.StopAllActionCoroutines();
            thief.stateMachine.SetGoal(cashierZone.waitingWaypoint);
            thief.stateMachine.SetState(ClientState.MovingToGoal);
            ExperienceManager.Instance?.GrantXP(this, ActionType.CatchThief);
        }
        currentStress += stressGainPerViolator;
    }

    private IEnumerator EvictRoutine(ClientPathfinding client)
    {
        SetState(GuardState.Evicting);
        yield return StartCoroutine(ChaseRoutine(client, GuardState.Evicting, ActionType.EvictClient));
        if (currentChaseTarget != null)
        {
            yield return StartCoroutine(ConfrontAndEvictRoutine(currentChaseTarget));
            yield return StartCoroutine(WriteReportRoutine());
        }
        currentAction = null;
    }
    
    private IEnumerator ConfrontAndEvictRoutine(ClientPathfinding client)
    {
        SetState(GuardState.Talking);
        client.Freeze();
        yield return new WaitForSeconds(talkTime);
        if (client != null)
        {
            client.ForceLeave(ClientPathfinding.LeaveReason.Angry);
            ExperienceManager.Instance?.GrantXP(this, ActionType.EvictClient);
        }
    }

    private IEnumerator WriteReportRoutine()
    {
        if (deskPoint == null || protocolStack == null) yield break;

        SetState(GuardState.WritingReport);
        yield return StartCoroutine(MoveToTarget(deskPoint.position, GuardState.WritingReport));

        Debug.Log($"{name} пишет протокол...");
        yield return new WaitForSeconds(5f);

        protocolStack.AddDocumentToStack();
        Debug.Log($"{name} закончил протокол.");
    }

    public override void GoOnBreak(float duration) 
    {
        if (!isOnDuty || currentAction != null) return;
        currentAction = StartCoroutine(BreakRoutine(duration));
    }

    private IEnumerator BreakRoutine(float duration)
    {
        SetState(GuardState.GoingToBreak);
        Transform breakSpot = RequestKitchenPoint();
        if (breakSpot != null)
        {
            yield return StartCoroutine(MoveToTarget(breakSpot.position, GuardState.OnBreak));
            yield return new WaitForSeconds(duration);
            FreeKitchenPoint(breakSpot);
        }
        else
        {
            yield return new WaitForSeconds(duration);
        }
        currentAction = null;
    }

    private void UpdateStress()
    {
        if (currentState == GuardState.StressedOut || !isOnDuty) return;
        bool isResting = currentState == GuardState.AtToilet || currentState == GuardState.OffDuty || currentState == GuardState.OnBreak;
        if (!isResting)
        {
             currentStress += stressGainPerViolator * 0.01f * Time.deltaTime;
        }
        else
        {
            currentStress -= stressReliefRate * Time.deltaTime;
        }
        currentStress = Mathf.Clamp(currentStress, 0, maxStress);
    }
    
    private IEnumerator MoveToTarget(Vector2 targetPosition, GuardState stateOnArrival)
    {
        agentMover.SetPath(BuildPathTo(targetPosition));
        yield return new WaitUntil(() => !agentMover.IsMoving());
        SetState(stateOnArrival);
    }
    
    private Transform SelectNewPatrolPoint()
    {
        if (patrolPoints == null || patrolPoints.Count == 0) return null;
        return patrolPoints[Random.Range(0, patrolPoints.Count)];
    }

    private void SetState(GuardState newState)
    {
        if (currentState == newState) return;
        currentState = newState;
        logger?.LogState(newState.ToString());
        visuals?.SetEmotionForState(newState);
    }

    protected override Queue<Waypoint> BuildPathTo(Vector2 targetPos)
    {
        var path = new Queue<Waypoint>();
        if (allWaypoints == null || allWaypoints.Length == 0) return path;

        Waypoint startNode = FindNearestVisibleWaypoint(transform.position);
        Waypoint endNode = FindNearestVisibleWaypoint(targetPos);
        if (startNode == null) startNode = FindNearestWaypoint(transform.position);
        if (endNode == null) endNode = FindNearestWaypoint(targetPos);
        if (startNode == null || endNode == null) return path;
        
        Dictionary<Waypoint, float> distances = new Dictionary<Waypoint, float>();
        Dictionary<Waypoint, Waypoint> previous = new Dictionary<Waypoint, Waypoint>();
        var queue = new PriorityQueue<Waypoint>();
        foreach (var wp in allWaypoints)
        {
            if(wp != null)
            {
                distances[wp] = float.MaxValue;
                previous[wp] = null;
            }
        }
        distances[startNode] = 0;
        queue.Enqueue(startNode, 0);
        while(queue.Count > 0)
        {
            Waypoint current = queue.Dequeue();
            if (current == endNode) { ReconstructPath(previous, endNode, path); return path; }
            if(current.neighbors == null) continue;
            foreach(var neighbor in current.neighbors)
            {
                if(neighbor == null) continue;
                if (neighbor.forbiddenTags != null && neighbor.forbiddenTags.Contains(gameObject.tag)) continue;
                float newDist = distances[current] + Vector2.Distance(current.transform.position, neighbor.transform.position);
                if(distances.ContainsKey(neighbor) && newDist < distances[neighbor])
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
        for (Waypoint at = goal; at != null; at = previous.ContainsKey(at) ? previous[at] : null) { pathList.Add(at); }
        pathList.Reverse();
        path.Clear();
        foreach (var wp in pathList) { path.Enqueue(wp); }
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
                if (hit.collider == null)
                {
                    minDistance = distance;
                    bestWaypoint = wp;
                }
            }
        }
        return bestWaypoint;
    }
    
    private Waypoint FindNearestWaypoint(Vector2 position)
    {
        if (allWaypoints == null) return null;
        return allWaypoints.Where(wp => wp != null).OrderBy(wp => Vector2.Distance(position, wp.transform.position)).FirstOrDefault();
    }
    
    private class PriorityQueue<T>
    {
        private List<KeyValuePair<T, float>> elements = new List<KeyValuePair<T, float>>();
        public int Count => elements.Count;
        public void Enqueue(T item, float priority) { elements.Add(new KeyValuePair<T, float>(item, priority)); }
        public T Dequeue()
        {
            int bestIndex = 0;
            for (int i = 0; i < elements.Count; i++)
            {
                if (elements[i].Value < elements[bestIndex].Value) { bestIndex = i; }
            }
            T bestItem = elements[bestIndex].Key;
            elements.RemoveAt(bestIndex);
            return bestItem;
        }
    }

    public override float GetStressValue() { return currentStress; }
    public override void SetStressValue(float stress) { currentStress = stress; }
}