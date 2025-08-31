using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(Rigidbody2D), typeof(AgentMover), typeof(CharacterStateLogger))]
public class ClerkController : MonoBehaviour
{
    public enum ClerkState { Working, GoingToBreak, OnBreak, ReturningToWork, GoingToToilet, AtToilet, Inactive, StressedOut }
    public enum ClerkRole { Regular, Cashier, Registrar }
    
    private ClerkState currentState = ClerkState.Inactive;

    public ClerkRole role = ClerkRole.Regular;
    public ServicePoint assignedServicePoint;
    public Transform kitchenPoint;
    public Transform staffToiletPoint;
    
    public float timeInToilet = 10f;
    public float chanceToGoToToilet = 0.005f;
    public float callClientCooldownDuration = 1f;

    public float maxStress = 100f;
    public float stressGainRate = 0.5f;
    public float stressGainPerClient = 5f;
    public float stressReliefRate = 10f;
    public float stressedOutDuration = 20f;
    private float currentStress = 0f;

    public AudioClip startShiftSound;
    public AudioClip endShiftSound;

    private Coroutine currentActionCoroutine;
    private Waypoint[] allWaypoints;
    private bool isWorking = false;
    private bool isWaitingForClient = false;
    private AgentMover agentMover;
    private CharacterStateLogger logger;
    private float callClientCooldown = 0f;

    private Transform homePoint { get => kitchenPoint; }

    void Awake() 
    { 
        agentMover = GetComponent<AgentMover>();
        logger = GetComponent<CharacterStateLogger>();
        allWaypoints = FindObjectsByType<Waypoint>(FindObjectsSortMode.None);
        if (role == ClerkRole.Cashier && assignedServicePoint != null) assignedServicePoint.deskId = -1;
    }
    
    void Start() 
    { 
        LogCurrentState(); 
    }
    
    void Update() 
    { 
        if (Time.timeScale == 0f) { agentMover?.Stop(); return; } 

        UpdateStress();

        if (role == ClerkRole.Registrar && isWorking && currentState == ClerkState.Working && currentActionCoroutine == null && !isWaitingForClient)
        {
            if (Time.time > callClientCooldown)
            {
                if (Vector2.Distance(transform.position, assignedServicePoint.clerkStandPoint.position) < 0.5f)
                {
                    isWaitingForClient = ClientQueueManager.Instance.CallNextClient(this);
                    if (!isWaitingForClient) callClientCooldown = Time.time + callClientCooldownDuration;
                }
            }
        }

        if (currentState == ClerkState.Working && role == ClerkRole.Regular && Random.value < chanceToGoToToilet * Time.deltaTime) 
        { 
            if (currentActionCoroutine == null) { currentActionCoroutine = StartCoroutine(ToiletBreakRoutine()); } 
        } 
    }

    private void UpdateStress()
    {
        if (currentState == ClerkState.StressedOut) return;

        bool isResting = currentState == ClerkState.OnBreak || currentState == ClerkState.AtToilet || currentState == ClerkState.Inactive;
        if (isWorking && !isResting)
        {
            currentStress += stressGainRate * Time.deltaTime;
        }
        else
        {
            currentStress -= stressReliefRate * Time.deltaTime;
        }
        currentStress = Mathf.Clamp(currentStress, 0, maxStress);

        if (currentStress >= maxStress)
        {
            if(currentActionCoroutine != null) StopCoroutine(currentActionCoroutine);
            currentActionCoroutine = StartCoroutine(StressedOutRoutine());
        }
    }
    
    public void ServiceComplete()
    {
        isWaitingForClient = false;
        currentStress += stressGainPerClient;
        Debug.Log($"[{name}] завершил обслуживание и готов к следующему клиенту. Стресс: {currentStress}");
    }
    
    private IEnumerator StressedOutRoutine()
    {
        SetState(ClerkState.StressedOut);
        ClientSpawner.ReportDeskOccupation(assignedServicePoint.deskId, null);
        isWaitingForClient = false;

        yield return StartCoroutine(MoveToTarget(kitchenPoint.position, ClerkState.StressedOut));
        
        yield return new WaitForSeconds(stressedOutDuration);

        currentStress = maxStress * 0.7f;
        yield return StartCoroutine(ReturnToWorkRoutine());
    }

    public float GetStressPercent() => currentStress / maxStress;

    public void StartShift() 
    { 
        if(!isWorking) 
        { 
            if(currentActionCoroutine != null) StopCoroutine(currentActionCoroutine); 
            currentActionCoroutine = StartCoroutine(StartShiftRoutine()); 
        } 
    }

    public void EndShift() 
    { 
        if(isWorking) 
        { 
            if(currentActionCoroutine != null) StopCoroutine(currentActionCoroutine); 
            currentActionCoroutine = StartCoroutine(EndShiftRoutine()); 
        } 
    }

    public void GoOnBreak(float duration) 
    { 
        if(currentActionCoroutine != null) StopCoroutine(currentActionCoroutine); 
        currentActionCoroutine = StartCoroutine(BreakRoutine(duration)); 
    }

    public ClerkState GetCurrentState() => currentState;
    
    public bool IsOnBreak() => currentState != ClerkState.Working && currentState != ClerkState.ReturningToWork;
    
    public string GetStatusInfo() 
    { 
        switch (currentState) 
        { 
            case ClerkState.Working: return $"Работает: {assignedServicePoint.name}"; 
            case ClerkState.OnBreak: return "На перерыве (кухня)"; 
            case ClerkState.AtToilet: return "На перерыве (туалет)"; 
            case ClerkState.ReturningToWork: return $"Возвращается на работу: {assignedServicePoint.name}"; 
            case ClerkState.GoingToBreak: return $"Идет на перерыв: {kitchenPoint.name}";
            case ClerkState.GoingToToilet: return $"Идет в туалет: {staffToiletPoint.name}";
            case ClerkState.StressedOut: return "СОРВАЛСЯ!";
            default: return currentState.ToString();
        } 
    }

    private void SetState(ClerkState newState) 
    { 
        if (currentState == newState) return; 
        currentState = newState; 
        LogCurrentState(); 
    }
    
    private void LogCurrentState() 
    { 
        logger?.LogState(GetStatusInfo()); 
    }

    private IEnumerator StartShiftRoutine() 
    { 
        yield return new WaitForSeconds(Random.Range(0f, 5f)); 
        isWorking = true; 
        if(startShiftSound != null) AudioSource.PlayClipAtPoint(startShiftSound, transform.position); 
        yield return StartCoroutine(ReturnToWorkRoutine()); 
    }

    private IEnumerator EndShiftRoutine() 
    { 
        isWorking = false; 
        ClientSpawner.ReportDeskOccupation(assignedServicePoint.deskId, null); 
        yield return StartCoroutine(MoveToTarget(homePoint.position, ClerkState.Inactive)); 
        if(endShiftSound != null) AudioSource.PlayClipAtPoint(endShiftSound, homePoint.position); 
        currentActionCoroutine = null; 
    }

    private IEnumerator ReturnToWorkRoutine() 
    { 
        SetState(ClerkState.ReturningToWork); 
        yield return StartCoroutine(MoveToTarget(assignedServicePoint.clerkStandPoint.position, ClerkState.Working)); 
        ClientSpawner.ReportDeskOccupation(assignedServicePoint.deskId, this); 
        currentActionCoroutine = null; 
    }

    private IEnumerator BreakRoutine(float duration) 
    { 
        SetState(ClerkState.GoingToBreak); 
        ClientSpawner.ReportDeskOccupation(assignedServicePoint.deskId, null); 
        isWaitingForClient = false; 
        yield return StartCoroutine(MoveToTarget(kitchenPoint.position, ClerkState.OnBreak)); 
        yield return new WaitForSeconds(duration); 
        yield return StartCoroutine(ReturnToWorkRoutine()); 
    }

    private IEnumerator ToiletBreakRoutine() 
    { 
        SetState(ClerkState.GoingToToilet); 
        ClientSpawner.ReportDeskOccupation(assignedServicePoint.deskId, null); 
        isWaitingForClient = false; 
        yield return StartCoroutine(MoveToTarget(staffToiletPoint.position, ClerkState.AtToilet)); 
        yield return new WaitForSeconds(timeInToilet); 
        yield return StartCoroutine(ReturnToWorkRoutine()); 
    }
    
    private IEnumerator MoveToTarget(Vector2 targetPosition, ClerkState stateOnArrival) 
    { 
        agentMover.SetPath(BuildPathTo(targetPosition)); 
        yield return new WaitUntil(() => !agentMover.IsMoving()); 
        SetState(stateOnArrival); 
    }
    
    private Queue<Waypoint> BuildPathTo(Vector2 targetPos) 
    { 
        var path = new Queue<Waypoint>(); 
        Waypoint startNode = FindNearestVisibleWaypoint(); 
        Waypoint endNode = FindNearestVisibleWaypoint(targetPos); 
        if (startNode == null || endNode == null) return path; 
        Dictionary<Waypoint, float> distances = new Dictionary<Waypoint, float>(); 
        Dictionary<Waypoint, Waypoint> previous = new Dictionary<Waypoint, Waypoint>(); 
        var queue = new PriorityQueue<Waypoint>(); 
        foreach (var wp in allWaypoints) { distances[wp] = float.MaxValue; previous[wp] = null; } 
        distances[startNode] = 0; 
        queue.Enqueue(startNode, 0); 
        while(queue.Count > 0) 
        { 
            Waypoint current = queue.Dequeue(); 
            if (current == endNode) { ReconstructPath(previous, endNode, path); return path; } 
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
        for (Waypoint at = goal; at != null; at = previous[at]) { pathList.Add(at); } 
        pathList.Reverse(); 
        path.Clear(); 
        foreach (var wp in pathList) { path.Enqueue(wp); } 
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
                if (hit.collider == null) { minDistance = distance; bestWaypoint = wp; } 
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
            for (int i = 0; i < elements.Count; i++) { if (elements[i].Value < elements[bestIndex].Value) { bestIndex = i; } } 
            T bestItem = elements[bestIndex].Key; 
            elements.RemoveAt(bestIndex); 
            return bestItem; 
        } 
    }
}