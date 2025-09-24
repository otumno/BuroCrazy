// Файл: ServiceWorkerController.cs (полностью переработанная версия)
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(AgentMover), typeof(CharacterStateLogger))]
public class ServiceWorkerController : StaffController
{
    public enum WorkerState { Idle, SearchingForWork, GoingToMess, Cleaning, GoingToBreak, OnBreak, GoingToToilet, AtToilet, OffDuty, StressedOut, Patrolling }
    
    [Header("Настройки Уборщика")]
    private WorkerState currentState = WorkerState.OffDuty;
    
    [Header("Внешний вид")]
    public EmotionSpriteCollection spriteCollection;
    public StateEmotionMap stateEmotionMap;

    [Header("Аксессуары")]
    public GameObject accessoryPrefab; // Для швабры
    
    [Header("Дополнительные объекты")]
    public GameObject nightLight;
    public Transform broomTransform; // Для анимации
    
    [Header("Параметры уборки")]
    public float cleaningTimeTrash = 2f;
    public float cleaningTimePuddle = 4f;
    public float cleaningTimePerDirtLevel = 1.5f;

    [Header("Стресс")]
    public float maxStress = 100f;
    public float stressGainPerMess = 2f;
    public float stressReliefRate = 10f;
    private float currentStress = 0f;
    
    private Quaternion initialBroomRotation;
    private Waypoint[] allWaypoints;
    private Rigidbody2D rb;

    // --- ПУБЛИЧНЫЕ МЕТОДЫ ---
    
    public WorkerState GetCurrentState() => currentState;

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

    // --- МЕТОДЫ UNITY ---

    protected override void Awake()
    {
        base.Awake();
        allWaypoints = FindObjectsByType<Waypoint>(FindObjectsSortMode.None);
        rb = GetComponent<Rigidbody2D>();
    }

    protected override void Start()
    {
        base.Start();
        if (broomTransform != null)
        {
            initialBroomRotation = broomTransform.localRotation;
        }
        SetState(WorkerState.OffDuty);
    }

    void Update()
    {
        if (Time.timeScale == 0f || !isOnDuty) return;
        UpdateStress();
    }
    
    // --- "МОЗГ" УБОРЩИКА ---

    protected override bool TryExecuteAction(ActionType actionType)
    {
        MessPoint.MessType messTypeToFind;
        switch (actionType)
        {
            case ActionType.CleanTrash:
                messTypeToFind = MessPoint.MessType.Trash;
                break;
            case ActionType.CleanPuddle:
                messTypeToFind = MessPoint.MessType.Puddle;
                break;
            case ActionType.CleanDirt:
                messTypeToFind = MessPoint.MessType.Dirt;
                break;
            default:
                return false; // Это действие не для уборщика
        }

        var targetMess = FindBestMessToClean(messTypeToFind);
        if (targetMess != null)
        {
            currentAction = StartCoroutine(GoAndCleanRoutine(targetMess));
            return true;
        }

        return false;
    }

    protected override void ExecuteDefaultAction()
    {
        // Если мусора нет, уборщик патрулирует в поисках работы
        currentAction = StartCoroutine(PatrolRoutine());
    }
    
    // --- КОРУТИНЫ ПОВЕДЕНИЯ ---
    
    private IEnumerator GoAndCleanRoutine(MessPoint targetMess)
    {
        SetState(WorkerState.GoingToMess);
        yield return StartCoroutine(MoveToTarget(targetMess.transform.position, WorkerState.Cleaning));

        if (targetMess == null) 
        { 
            currentAction = null; 
            yield break;
        }
        
        float cleaningTime = GetCleaningTime(targetMess);
        StartCoroutine(AnimateBroom(cleaningTime));
        yield return new WaitForSeconds(cleaningTime);

        if (targetMess != null)
        {
            ExperienceManager.Instance?.GrantXP(this, GetActionTypeFromMess(targetMess.type));
            currentStress += stressGainPerMess;
            Destroy(targetMess.gameObject);
        }

        currentAction = null;
    }
    
    private IEnumerator PatrolRoutine()
    {
        SetState(WorkerState.Patrolling);
        var patrolTarget = patrolPoints.Any() ? patrolPoints[Random.Range(0, patrolPoints.Count)] : null;
        if (patrolTarget != null)
        {
            yield return StartCoroutine(MoveToTarget(patrolTarget.position, WorkerState.Idle));
        }
        yield return new WaitForSeconds(Random.Range(3f, 6f));
        currentAction = null;
    }

    public override void GoOnBreak(float duration)
    {
        if (!isOnDuty || currentAction != null) return;
        currentAction = StartCoroutine(BreakRoutine(duration));
    }
    
    private IEnumerator BreakRoutine(float duration)
    {
        SetState(WorkerState.GoingToBreak);
        Transform breakSpot = RequestKitchenPoint();
        if (breakSpot != null)
        {
            yield return StartCoroutine(MoveToTarget(breakSpot.position, WorkerState.OnBreak));
            yield return new WaitForSeconds(duration);
            FreeKitchenPoint(breakSpot);
        }
        else
        {
            yield return new WaitForSeconds(duration);
        }
        currentAction = null;
    }
    
    // --- ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ---
    
    private MessPoint FindBestMessToClean(MessPoint.MessType type)
    {
        return MessManager.Instance?.GetSortedMessList(transform.position)
            .FirstOrDefault(m => m.type == type);
    }

    private float GetCleaningTime(MessPoint mess)
    {
        switch (mess.type)
        {
            case MessPoint.MessType.Trash: return cleaningTimeTrash;
            case MessPoint.MessType.Puddle: return cleaningTimePuddle;
            case MessPoint.MessType.Dirt: return cleaningTimePerDirtLevel * mess.dirtLevel;
            default: return 1f;
        }
    }
    
    private ActionType GetActionTypeFromMess(MessPoint.MessType messType)
    {
        switch (messType)
        {
            case MessPoint.MessType.Trash: return ActionType.CleanTrash;
            case MessPoint.MessType.Puddle: return ActionType.CleanPuddle;
            case MessPoint.MessType.Dirt: return ActionType.CleanDirt;
            default: return ActionType.CleanTrash; // Значение по умолчанию
        }
    }
	
	private IEnumerator AnimateBroom(float duration)
    {
        if (broomTransform == null) yield break;
        float elapsedTime = 0f;
        float animationSpeed = 4f;

        while (elapsedTime < duration)
        {
            float angle = Mathf.Sin(Time.time * animationSpeed) * 15f;
            broomTransform.localRotation = initialBroomRotation * Quaternion.Euler(0, 0, angle);
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        broomTransform.localRotation = initialBroomRotation;
    }

    private void UpdateStress()
    {
        if (currentState == WorkerState.StressedOut || !isOnDuty) return;
        bool isResting = currentState == WorkerState.AtToilet || currentState == WorkerState.OffDuty || currentState == WorkerState.OnBreak;
        
        if (isOnDuty && !isResting) 
        {
            float stressMultiplier = 1f;
            if (skills != null)
            {
                stressMultiplier = (1f - skills.softSkills);
            }
            currentStress += stressGainPerMess * stressMultiplier * Time.deltaTime;
        }
        else
        {
            currentStress -= stressReliefRate * Time.deltaTime;
        }

        currentStress = Mathf.Clamp(currentStress, 0, maxStress);
        if (currentStress >= maxStress)
        {
            if (currentAction != null) StopCoroutine(currentAction);
            currentAction = StartCoroutine(StressedOutRoutine());
        }
    }
    
    // Корутина для стресса, которую может вызвать UpdateStress
    private IEnumerator StressedOutRoutine()
    {
        SetState(WorkerState.StressedOut);
        Transform breakSpot = RequestKitchenPoint();
        if (breakSpot != null)
        {
            yield return StartCoroutine(MoveToTarget(breakSpot.position, WorkerState.StressedOut));
            yield return new WaitForSeconds(20f); // Stressed out duration
            FreeKitchenPoint(breakSpot);
        }
        else
        {
            yield return new WaitForSeconds(20f);
        }
        currentStress = maxStress * 0.5f;
        currentAction = null;
    }

    private IEnumerator MoveToTarget(Vector2 targetPosition, WorkerState stateOnArrival)
    {
        agentMover.SetPath(BuildPathTo(targetPosition));
        yield return new WaitUntil(() => !agentMover.IsMoving());
        SetState(stateOnArrival);
    }

    private void SetState(WorkerState newState)
    {
        if (currentState == newState) return;
        currentState = newState;
        logger.LogState(newState.ToString());
        visuals?.SetEmotionForState(newState);
    }

    // --- Улучшенная версия поиска пути (A*) для консистентности с другими контроллерами ---
    protected override Queue<Waypoint> BuildPathTo(Vector2 targetPos)
    {
        var path = new Queue<Waypoint>();
        if (allWaypoints == null || allWaypoints.Length == 0) return path;

        Waypoint startNode = FindNearestVisibleWaypoint(transform.position);
        Waypoint endNode = FindNearestVisibleWaypoint(targetPos);
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

    public override float GetStressValue() 
    { 
        return currentStress; 
    }

    public override void SetStressValue(float stress) 
    { 
        currentStress = stress; 
    }
}