// Файл: ServiceWorkerController.cs
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(AgentMover), typeof(CharacterStateLogger))]
public class ServiceWorkerController : StaffController
{
    public enum WorkerState { Idle, SearchingForWork, GoingToMess, Cleaning, GoingToBreak, OnBreak, GoingToToilet, AtToilet, OffDuty, StressedOut }
    
    [Header("Настройки Уборщика")]
    private WorkerState currentState = WorkerState.OffDuty;
    
    [Header("Внешний вид")]
    private CharacterVisuals visuals;
    
    [Header("Дополнительные объекты")]
    public GameObject nightLight;
    public Transform broomTransform;
    
    [Header("Настройки фонарика")]
    [Tooltip("На каком расстоянии от центра будет фонарик при движении")]
    public float flashlightOffsetDistance = 0.5f;
    [Tooltip("Насколько плавно фонарик меняет свое положение")]
    public float flashlightSmoothingSpeed = 5f;
    
    [Header("Параметры уборки")]
    public float cleaningTimeTrash = 2f;
    public float cleaningTimePuddle = 4f;
    public float cleaningTimePerDirtLevel = 1.5f;
    public float trashAreaCleaningRadius = 1.5f;
    public float dirtAreaCleaningRadius = 2f;
    
    [Header("Прочее")]
    public float timeInToilet = 8f;
    public float chanceToGoToToilet = 0.01f;

    [Header("Стресс")]
    public float maxStress = 100f;
    public float stressGainPerTrash = 1f;
    public float stressGainPerDirtLevel = 2f;
    public float stressGainPerPuddle = 5f;
    public float stressReliefRate = 10f;
    public float stressedOutDuration = 20f;
    private float currentStress = 0f;
    
    private Quaternion initialBroomRotation;
    private Waypoint[] allWaypoints;
    private Rigidbody2D rb;
    
    protected override void Awake()
    {
        base.Awake();
        visuals = GetComponent<CharacterVisuals>();
        allWaypoints = FindObjectsByType<Waypoint>(FindObjectsSortMode.None);
        rb = GetComponent<Rigidbody2D>();
    }

    protected override void Start()
    {
        base.Start();
        visuals?.Setup(gender);
        if (broomTransform != null)
        {
            initialBroomRotation = broomTransform.localRotation;
        }
        
        currentState = WorkerState.OffDuty;
        SetState(currentState);
        
        if (skills != null)
        {
            skills.paperworkMastery = 0.0f;
            skills.pedantry = 0.75f;
            skills.softSkills = 0.25f;
            skills.sedentaryResilience = 0.5f;
        }
    }

    void Update()
    {
        if (Time.timeScale == 0f || !isOnDuty) return;
        UpdateStress();
        if (currentAction == null)
        {
            currentAction = StartCoroutine(MainAI_Loop());
        }
    }
    
    void LateUpdate()
    {
        if (nightLight == null || !nightLight.activeSelf)
        {
            return;
        }

        Vector2 velocity = rb.linearVelocity;
        Vector3 targetPosition = Vector3.zero;

        if (velocity.magnitude > 0.1f)
        {
            targetPosition = (Vector3)velocity.normalized * flashlightOffsetDistance;
        }

        nightLight.transform.localPosition = Vector3.Lerp(
            nightLight.transform.localPosition,
            targetPosition,
            Time.deltaTime * flashlightSmoothingSpeed
        );
    }
    
    public override void StartShift()
    {
        if (isOnDuty) return;
        isOnDuty = true;
        SetState(WorkerState.Idle);
        currentAction = null;
    }

    public override void EndShift()
    {
        if (!isOnDuty) return;
        isOnDuty = false;
        if (currentAction != null) StopCoroutine(currentAction);
        currentAction = StartCoroutine(GoHomeRoutine());
    }

    private void SetState(WorkerState newState)
    {
        if (currentState == newState) return;
        currentState = newState;
        logger.LogState(newState.ToString());

        visuals?.SetEmotionForState(newState);
    }

    private IEnumerator MainAI_Loop()
    {
        SetState(WorkerState.Idle);
        yield return new WaitForSeconds(Random.Range(0.5f, 1.5f));

        float finalChanceToGoToToilet = chanceToGoToToilet;
        if (skills != null)
        {
            finalChanceToGoToToilet *= (1f - skills.sedentaryResilience);
        }

        if (Random.value < finalChanceToGoToToilet)
        {
            yield return StartCoroutine(ToiletBreakRoutine());
        }
        else
        {
            SetState(WorkerState.SearchingForWork);
            MessPoint targetMess = FindBestMessToClean();

            if (targetMess != null)
            {
                yield return StartCoroutine(GoAndCleanRoutine(targetMess));
            }
        }
        
        currentAction = null;
    }
    
    private MessPoint FindBestMessToClean()
    {
        List<MessPoint> allMesses = MessManager.Instance.GetSortedMessList(transform.position);
        if (allMesses.Count == 0) return null;
        
        foreach (var mess in allMesses)
        {
            if (mess != null && CanPathTo(mess.transform.position))
            {
                return mess;
            }
        }
        return null;
    }

    private void UpdateStress()
    {
        if (currentState == WorkerState.StressedOut || !isOnDuty) return;
        bool isResting = currentState == WorkerState.AtToilet || currentState == WorkerState.OffDuty || currentState == WorkerState.OnBreak;
        
        float finalStressGainRate = stressGainPerTrash;
        if (isOnDuty && !isResting) 
        {
            float stressMultiplier = 1f;
            if (skills != null)
            {
                stressMultiplier = (1f - skills.softSkills);
            }
            currentStress += finalStressGainRate * stressMultiplier * Time.deltaTime;
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
    
    private IEnumerator GoAndCleanRoutine(MessPoint initialMess)
    {
        SetState(WorkerState.GoingToMess);
        yield return StartCoroutine(MoveToTarget(initialMess.transform.position, WorkerState.Cleaning));

        if (initialMess == null) { currentAction = null; yield break; }
        
        float cleaningTime = GetCleaningTime(initialMess);
        float totalStressGain = 0;
        
        StartCoroutine(AnimateBroom(cleaningTime));
        yield return new WaitForSeconds(cleaningTime);

ActionType performedAction;
switch (initialMess.type)
{
    case MessPoint.MessType.Trash:
        performedAction = ActionType.CleanTrash;
        break;
    case MessPoint.MessType.Puddle:
        performedAction = ActionType.CleanPuddle;
        break;
    case MessPoint.MessType.Dirt:
        performedAction = ActionType.CleanDirt;
        break;
    default:
        currentAction = null;
        yield break;
}
ExperienceManager.Instance?.GrantXP(this, performedAction);
		
		

        MessPoint.MessType typeToClean = initialMess.type;
        if (typeToClean == MessPoint.MessType.Trash)
        {
            totalStressGain += CleanArea(trashAreaCleaningRadius, typeToClean);
        }
        else if (typeToClean == MessPoint.MessType.Dirt)
        {
            totalStressGain += CleanArea(dirtAreaCleaningRadius, typeToClean);
        }
        else
        {
            if (initialMess != null)
            {
                totalStressGain += GetStressGain(initialMess);
                Destroy(initialMess.gameObject);
            }
        }
        
        currentStress += totalStressGain;
    }
    
    private float CleanArea(float radius, MessPoint.MessType type)
    {
        Collider2D[] nearbyColliders = Physics2D.OverlapCircleAll(transform.position, radius);
        float stressGain = 0;
        foreach (var col in nearbyColliders)
        {
            MessPoint nearbyMess = col.GetComponent<MessPoint>();
            if (nearbyMess != null && nearbyMess.type == type)
            {
                stressGain += GetStressGain(nearbyMess);
                Destroy(nearbyMess.gameObject);
            }
        }
        return stressGain;
    }

    private float GetCleaningTime(MessPoint mess)
    {
        if (mess == null) return 0;
        switch (mess.type)
        {
            case MessPoint.MessType.Trash: return cleaningTimeTrash;
            case MessPoint.MessType.Puddle: return cleaningTimePuddle;
            case MessPoint.MessType.Dirt: return cleaningTimePerDirtLevel * mess.dirtLevel;
            default: return 1f;
        }
    }

    private float GetStressGain(MessPoint mess)
    {
        if (mess == null) return 0;
        switch (mess.type)
        {
            case MessPoint.MessType.Trash: return stressGainPerTrash;
            case MessPoint.MessType.Puddle: return stressGainPerPuddle;
            case MessPoint.MessType.Dirt: return stressGainPerDirtLevel * mess.dirtLevel;
            default: return 0;
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
    
    private IEnumerator GoHomeRoutine()
    {
        if (homePoint != null)
        {
            yield return StartCoroutine(MoveToTarget(homePoint.position, WorkerState.OffDuty));
        }
        currentAction = null;
    }

    private IEnumerator ToiletBreakRoutine()
    {
        SetState(WorkerState.GoingToToilet);
        yield return StartCoroutine(EnterLimitedZoneAndWaitRoutine(staffToiletPoint, timeInToilet));
        SetState(WorkerState.AtToilet);
    }
    
    private IEnumerator StressedOutRoutine()
    {
        SetState(WorkerState.StressedOut);
        Transform breakSpot = RequestKitchenPoint();
        if (breakSpot != null)
        {
            yield return StartCoroutine(MoveToTarget(breakSpot.position, WorkerState.StressedOut));
            yield return new WaitForSeconds(stressedOutDuration);
            FreeKitchenPoint(breakSpot);
        }
        else
        {
            yield return new WaitForSeconds(stressedOutDuration);
        }
        
        currentStress = maxStress * 0.5f;
    }
    
    private IEnumerator MoveToTarget(Vector2 targetPosition, WorkerState stateOnArrival)
    {
        agentMover.SetPath(BuildPathTo(targetPosition));
        yield return new WaitUntil(() => !agentMover.IsMoving());
        SetState(stateOnArrival);
    }
    
    public override void GoOnBreak(float duration)
    {
        if (currentAction != null) StopCoroutine(currentAction);
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
            Debug.LogWarning($"Для {name} не настроены точки отдыха (Kitchen Points)!");
            yield return new WaitForSeconds(duration);
        }
    }

    public WorkerState GetCurrentState() => currentState;
    public float GetStressPercent() => currentStress / maxStress;
    
    private bool CanPathTo(Vector2 targetPosition)
    {
        var path = BuildPathTo(targetPosition);
        return path != null && path.Count > 0;
    }
    
    protected override Queue<Waypoint> BuildPathTo(Vector2 targetPos)
    {
        var path = new Queue<Waypoint>();
        if (allWaypoints.Length == 0) return path;
        
        Waypoint startNode = allWaypoints.OrderBy(wp => Vector2.Distance(transform.position, wp.transform.position)).FirstOrDefault();
        Waypoint endNode = allWaypoints.OrderBy(wp => Vector2.Distance(targetPos, wp.transform.position)).FirstOrDefault();
        if (startNode == null || endNode == null) return path;
        
        Dictionary<Waypoint, float> distances = new Dictionary<Waypoint, float>();
        Dictionary<Waypoint, Waypoint> previous = new Dictionary<Waypoint, Waypoint>();
        var queue = new PriorityQueue<Waypoint>();
        foreach (var wp in allWaypoints)
        {
            distances[wp] = float.MaxValue;
            previous[wp] = null;
        }
        
        distances[startNode] = 0;
        queue.Enqueue(startNode, 0);
        
        while(queue.Count > 0)
        {
            Waypoint current = queue.Dequeue();
            if (current == endNode)
            {
                ReconstructPath(previous, endNode, path);
                return path;
            }
            
            if (current.neighbors == null) continue;
            foreach(var neighbor in current.neighbors)
            {
                if(neighbor == null) continue;
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
        for (Waypoint at = goal; at != null; at = previous[at]) { pathList.Add(at);
        }
        pathList.Reverse();
        path.Clear();
        foreach (var wp in pathList) { path.Enqueue(wp);
        }
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
            for (int i = 0; i < elements.Count; i++)
            {
                if (elements[i].Value < elements[bestIndex].Value)
                {
                    bestIndex = i;
                }
            }
            T bestItem = elements[bestIndex].Key;
            elements.RemoveAt(bestIndex);
            return bestItem;
        }
    }

    public override float GetStressValue() { return currentStress; }
    public override void SetStressValue(float stress) { currentStress = stress; }
}