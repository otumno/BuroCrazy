using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(AgentMover), typeof(CharacterStateLogger))]
public class ServiceWorkerController : MonoBehaviour
{
    public enum WorkerState { Idle, SearchingForWork, GoingToMess, Cleaning, GoingToToilet, AtToilet, OffDuty, StressedOut }
    public enum Shift { First, Second }
    private WorkerState currentState = WorkerState.OffDuty;
    private Shift assignedShift;

    public GameObject nightLight;
    
    public float cleaningTimeTrash = 2f;
    public float cleaningTimePuddle = 4f;
    public float cleaningTimePerDirtLevel = 1.5f;

    public float trashAreaCleaningRadius = 1.5f;
    public float dirtAreaCleaningRadius = 2f;

    public Transform staffToiletPoint;
    public Waypoint homePointWaypoint;
    public float timeInToilet = 8f;
    public float chanceToGoToToilet = 0.01f;

    public float maxStress = 100f;
    public float stressGainPerTrash = 1f;
    public float stressGainPerDirtLevel = 2f;
    public float stressGainPerPuddle = 5f;
    public float stressReliefRate = 10f;
    public float stressedOutDuration = 20f;
    private float currentStress = 0f;
    
    private AgentMover agentMover;
    private CharacterStateLogger logger;
    private Coroutine currentMainAction;
    private bool isOnDuty = false;
    private Quaternion initialBroomRotation;
    public Transform broomTransform;

    void Awake()
    {
        agentMover = GetComponent<AgentMover>();
        logger = GetComponent<CharacterStateLogger>();
    }

    void Start()
    {
        if (broomTransform != null)
        {
            initialBroomRotation = broomTransform.localRotation;
        }
    }

    void Update()
    {
        if (Time.timeScale == 0f || !isOnDuty) return;
        UpdateStress();
        if (currentMainAction == null)
        {
            currentMainAction = StartCoroutine(MainAI_Loop());
        }
    }

    private IEnumerator MainAI_Loop()
    {
        SetState(WorkerState.Idle);
        yield return new WaitForSeconds(Random.Range(0.5f, 1.5f));

        if (Random.value < chanceToGoToToilet)
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
        
        currentMainAction = null;
    }
    
    private MessPoint FindBestMessToClean()
    {
        List<MessPoint> allMesses = MessManager.Instance.GetSortedMessList(transform.position);
        List<MessPoint> reachableMesses = new List<MessPoint>();

        foreach (var mess in allMesses)
        {
            if (mess != null && CanPathTo(mess.transform.position))
            {
                reachableMesses.Add(mess);
            }
        }

        if (reachableMesses.Count == 0)
        {
            return null;
        }

        return reachableMesses[Random.Range(0, reachableMesses.Count)];
    }

    private void UpdateStress()
    {
        if (currentState == WorkerState.StressedOut || !isOnDuty) return;
        bool isResting = currentState == WorkerState.AtToilet || currentState == WorkerState.OffDuty;
        if (isResting) { currentStress -= stressReliefRate * Time.deltaTime; }
        currentStress = Mathf.Clamp(currentStress, 0, maxStress);
        if (currentStress >= maxStress)
        {
            if (currentMainAction != null) StopCoroutine(currentMainAction);
            currentMainAction = StartCoroutine(StressedOutRoutine());
        }
    }

    private IEnumerator GoAndCleanRoutine(MessPoint initialMess)
    {
        SetState(WorkerState.GoingToMess);
        yield return StartCoroutine(MoveToTarget(initialMess.transform.position, WorkerState.Cleaning));

        if (initialMess == null) { yield break; }
        
        float cleaningTime = GetCleaningTime(initialMess);
        float totalStressGain = 0;
        
        StartCoroutine(AnimateBroom(cleaningTime));
        yield return new WaitForSeconds(cleaningTime);

        if (initialMess == null) { yield break; }

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
    
    public void AssignShift(Shift shift)
    {
        assignedShift = shift;
    }

    public bool ShouldWork(bool isFirstShift, bool isSecondShift)
    {
        if (isFirstShift && assignedShift == Shift.First) return true;
        if (isSecondShift && assignedShift == Shift.Second) return true;
        return false;
    }

    public void StartShift() { if (isOnDuty) return; isOnDuty = true; SetState(WorkerState.Idle); currentMainAction = null; }
    public void EndShift() { if (!isOnDuty) return; isOnDuty = false; if (currentMainAction != null) StopCoroutine(currentMainAction); currentMainAction = StartCoroutine(GoHomeRoutine()); }
    private IEnumerator GoHomeRoutine() { yield return StartCoroutine(MoveToTarget(homePointWaypoint.transform.position, WorkerState.OffDuty)); currentMainAction = null; }
    private IEnumerator ToiletBreakRoutine() { yield return StartCoroutine(MoveToTarget(staffToiletPoint.position, WorkerState.AtToilet)); yield return new WaitForSeconds(timeInToilet); }
    private IEnumerator StressedOutRoutine() { SetState(WorkerState.StressedOut); yield return StartCoroutine(MoveToTarget(homePointWaypoint.transform.position, WorkerState.StressedOut)); yield return new WaitForSeconds(stressedOutDuration); currentStress = maxStress * 0.5f; }
    private IEnumerator MoveToTarget(Vector2 targetPosition, WorkerState stateOnArrival) { agentMover.SetPath(BuildPathTo(targetPosition)); yield return new WaitUntil(() => !agentMover.IsMoving()); SetState(stateOnArrival); }
    public WorkerState GetCurrentState() => currentState;
    private void SetState(WorkerState newState) { if (currentState == newState) return; currentState = newState; logger.LogState(newState.ToString()); }
    public float GetStressPercent() => currentStress / maxStress;
    private bool CanPathTo(Vector2 targetPosition) { var path = BuildPathTo(targetPosition); return path != null && path.Count > 0; }
    private Queue<Waypoint> BuildPathTo(Vector2 targetPos) { var path = new Queue<Waypoint>(); Waypoint[] allWaypoints = FindObjectsByType<Waypoint>(FindObjectsSortMode.None); if (allWaypoints.Length == 0) return path; Waypoint startNode = allWaypoints.OrderBy(wp => Vector2.Distance(transform.position, wp.transform.position)).FirstOrDefault(); Waypoint endNode = allWaypoints.OrderBy(wp => Vector2.Distance(targetPos, wp.transform.position)).FirstOrDefault(); if (startNode == null || endNode == null) return path; Dictionary<Waypoint, float> distances = new Dictionary<Waypoint, float>(); Dictionary<Waypoint, Waypoint> previous = new Dictionary<Waypoint, Waypoint>(); var queue = new PriorityQueue<Waypoint>(); foreach (var wp in allWaypoints) { distances[wp] = float.MaxValue; previous[wp] = null; } distances[startNode] = 0; queue.Enqueue(startNode, 0); while(queue.Count > 0) { Waypoint current = queue.Dequeue(); if (current == endNode) { ReconstructPath(previous, endNode, path); return path; } if (current.neighbors == null) continue; foreach(var neighbor in current.neighbors) { if(neighbor == null) continue; float newDist = distances[current] + Vector2.Distance(current.transform.position, neighbor.transform.position); if(distances.ContainsKey(neighbor) && newDist < distances[neighbor]) { distances[neighbor] = newDist; previous[neighbor] = current; queue.Enqueue(neighbor, newDist); } } } return path; }
    private void ReconstructPath(Dictionary<Waypoint, Waypoint> previous, Waypoint goal, Queue<Waypoint> path) { List<Waypoint> pathList = new List<Waypoint>(); for (Waypoint at = goal; at != null; at = previous[at]) { pathList.Add(at); } pathList.Reverse(); path.Clear(); foreach (var wp in pathList) { path.Enqueue(wp); } }
    private class PriorityQueue<T> { private List<KeyValuePair<T, float>> elements = new List<KeyValuePair<T, float>>(); public int Count => elements.Count; public void Enqueue(T item, float priority) { elements.Add(new KeyValuePair<T, float>(item, priority)); } public T Dequeue() { int bestIndex = 0; for (int i = 0; i < elements.Count; i++) { if (elements[i].Value < elements[bestIndex].Value) { bestIndex = i; } } T bestItem = elements[bestIndex].Key; elements.RemoveAt(bestIndex); return bestItem; } }
}