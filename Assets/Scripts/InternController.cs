using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(Rigidbody2D), typeof(AgentMover), typeof(CharacterStateLogger))]
public class InternController : MonoBehaviour
{
    public enum InternState { Patrolling, HelpingConfused, ServingFromQueue, CoveringDesk, GoingToBreak, OnBreak, GoingToToilet, AtToilet, ReturningToPatrol, Inactive, Working, TalkingToConfused }
    private InternState currentState = InternState.Inactive;
    [Header("Основные параметры")]
    public List<Transform> patrolPoints;
    public Transform kitchenPoint;
    public Transform staffToiletPoint;
    [Header("Параметры поведения")]
    public float helpCheckInterval = 2f;
    public float chanceToServeFromQueue = 0.1f;
    public float chanceToCoverDesk = 0.5f;
    public float chanceToGoToToilet = 0.008f;
    public float timeInToilet = 4f;

    private static List<ClerkController> clerksBeingCovered = new List<ClerkController>();

    private Waypoint[] allWaypoints;
    private Coroutine currentAction;
    private Transform currentPatrolTarget;
    private float helpTimer = 0f;
    private bool isWorking = false;
    private ClientPathfinding helpTarget = null;
    private ClerkController clerkToCover = null;
    
    private AgentMover agentMover;
    private CharacterStateLogger logger;

    void Awake() { allWaypoints = FindObjectsByType<Waypoint>(FindObjectsSortMode.None); agentMover = GetComponent<AgentMover>(); logger = GetComponent<CharacterStateLogger>(); }
    void Start() { LogCurrentState(currentState); }
    void Update() { if (!isWorking || Time.timeScale == 0f) { if(agentMover != null) agentMover.Stop(); return; } helpTimer += Time.deltaTime; if (helpTimer >= helpCheckInterval) { helpTimer = 0f; LookForSomeoneToHelp(); } }
    
    private void SetState(InternState newState) { if(currentState == newState) return; currentState = newState; LogCurrentState(newState); }
    private void LogCurrentState(InternState state) { logger?.LogState(state.ToString()); }
    public InternState GetCurrentState() => currentState;

    private void LookForSomeoneToHelp()
    {
        if (currentState != InternState.Patrolling) return;
        ClerkController absentClerk = ClientSpawner.GetAbsentClerk();
        if (absentClerk != null && !clerksBeingCovered.Contains(absentClerk) && Random.value < chanceToCoverDesk) { clerksBeingCovered.Add(absentClerk); if(currentAction != null) StopCoroutine(currentAction); clerkToCover = absentClerk; currentAction = StartCoroutine(CoverDeskRoutine(absentClerk)); return; }
        ClientPathfinding confusedClient = ClientPathfinding.FindClosestConfusedClient(transform.position);
        if (confusedClient != null && confusedClient.stateMachine.GetCurrentState() == ClientState.Confused) { if(currentAction != null) StopCoroutine(currentAction); helpTarget = confusedClient; currentAction = StartCoroutine(HelpConfusedClientRoutine(confusedClient)); return; }
        if (Random.value < chanceToServeFromQueue) { if (ClientQueueManager.Instance != null && ClientQueueManager.Instance.GetRandomClientFromQueue() is ClientPathfinding queueClient && queueClient != null) { if(currentAction != null) StopCoroutine(currentAction); helpTarget = queueClient; currentAction = StartCoroutine(ServeFromQueueRoutine(queueClient)); } }
    }

    private IEnumerator MoveToTarget(Vector2 targetPosition, InternState stateOnArrival) { agentMover.SetPath(BuildPathTo(targetPosition)); yield return new WaitUntil(() => !agentMover.IsMoving()); SetState(stateOnArrival); }
    
    private IEnumerator CoverDeskRoutine(ClerkController clerk) 
    { 
        SetState(InternState.CoveringDesk); 
        yield return StartCoroutine(MoveToTarget(clerk.assignedServicePoint.clerkStandPoint.position, InternState.Working)); 
        if (clerksBeingCovered.Contains(clerk)) { clerksBeingCovered.Remove(clerk); } 
        clerkToCover = null; 
        yield return StartCoroutine(ReturnToPatrolRoutine()); 
    }
    
    private IEnumerator HelpConfusedClientRoutine(ClientPathfinding client) { SetState(InternState.HelpingConfused); yield return StartCoroutine(MoveToTarget(client.transform.position, InternState.TalkingToConfused)); if (client == null || client.stateMachine.GetCurrentState() != ClientState.Confused) { helpTarget = null; yield return StartCoroutine(ReturnToPatrolRoutine()); yield break; } yield return new WaitForSeconds(1f); if (client != null) { client.stateMachine.GetHelpFromIntern(); } helpTarget = null; yield return StartCoroutine(ReturnToPatrolRoutine()); }
    
    private IEnumerator ServeFromQueueRoutine(ClientPathfinding client) 
    { 
        SetState(InternState.ServingFromQueue); 
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
    
    public void StartShift() { if(currentAction != null) StopCoroutine(currentAction); currentAction = StartCoroutine(StartShiftRoutine()); }
    public void EndShift() { if(currentAction != null) StopCoroutine(currentAction); currentAction = StartCoroutine(EndShiftRoutine()); }
    public void GoOnBreak(float duration) { if(currentAction != null) StopCoroutine(currentAction); currentAction = StartCoroutine(BreakRoutine(duration)); }
    
    private IEnumerator StartShiftRoutine() { yield return new WaitForSeconds(10f + Random.Range(-10f, 10f)); isWorking = true; yield return StartCoroutine(ReturnToPatrolRoutine()); }
    private IEnumerator EndShiftRoutine() { yield return new WaitForSeconds(10f + Random.Range(-10f, 10f)); isWorking = false; if (clerkToCover != null && clerksBeingCovered.Contains(clerkToCover)) { clerksBeingCovered.Remove(clerkToCover); } SetState(InternState.Inactive); yield return StartCoroutine(MoveToTarget(kitchenPoint.position, InternState.Inactive)); currentAction = null; }
    private IEnumerator BreakRoutine(float duration) { isWorking = false; SetState(InternState.GoingToBreak); yield return StartCoroutine(MoveToTarget(kitchenPoint.position, InternState.OnBreak)); yield return new WaitForSeconds(duration); isWorking = true; yield return StartCoroutine(ReturnToPatrolRoutine()); }
    private IEnumerator ToiletBreakRoutine() { SetState(InternState.GoingToToilet); yield return StartCoroutine(MoveToTarget(staffToiletPoint.position, InternState.AtToilet)); yield return new WaitForSeconds(timeInToilet); yield return StartCoroutine(ReturnToPatrolRoutine()); }
    private IEnumerator ReturnToPatrolRoutine() { SetState(InternState.ReturningToPatrol); SelectNewPatrolPoint(); if(currentPatrolTarget != null) { yield return StartCoroutine(MoveToTarget(currentPatrolTarget.position, InternState.Patrolling)); } SetState(InternState.Patrolling); currentAction = null; }
    
    private void SelectNewPatrolPoint() { if (patrolPoints == null || patrolPoints.Count == 0) return; currentPatrolTarget = patrolPoints[Random.Range(0, patrolPoints.Count)]; }
    
    private Queue<Waypoint> BuildPathTo(Vector2 targetPos) { var path = new Queue<Waypoint>(); Waypoint startNode = FindNearestVisibleWaypoint(transform.position); Waypoint endNode = FindNearestVisibleWaypoint(targetPos); if (startNode == null || endNode == null) return path; Dictionary<Waypoint, float> distances = new Dictionary<Waypoint, float>(); Dictionary<Waypoint, Waypoint> previous = new Dictionary<Waypoint, Waypoint>(); var queue = new PriorityQueue<Waypoint>(); foreach (var wp in allWaypoints) { distances[wp] = float.MaxValue; previous[wp] = null; } distances[startNode] = 0; queue.Enqueue(startNode, 0); while(queue.Count > 0) { Waypoint current = queue.Dequeue(); if (current == endNode) { ReconstructPath(previous, endNode, path); return path; } foreach(var neighbor in current.neighbors) { if(neighbor == null) continue; if (neighbor.forbiddenTags != null && neighbor.forbiddenTags.Contains(gameObject.tag)) continue; float newDist = distances[current] + Vector2.Distance(current.transform.position, neighbor.transform.position); if(distances.ContainsKey(neighbor) && newDist < distances[neighbor]) { distances[neighbor] = newDist; previous[neighbor] = current; queue.Enqueue(neighbor, newDist); } } } return path; }
    private void ReconstructPath(Dictionary<Waypoint, Waypoint> previous, Waypoint goal, Queue<Waypoint> path) { List<Waypoint> pathList = new List<Waypoint>(); for (Waypoint at = goal; at != null; at = previous[at]) { pathList.Add(at); } pathList.Reverse(); path.Clear(); foreach (var wp in pathList) { path.Enqueue(wp); } }
    private Waypoint FindNearestVisibleWaypoint(Vector2 position) { if (allWaypoints == null) return null; Waypoint bestWaypoint = null; float minDistance = float.MaxValue; foreach (var wp in allWaypoints) { if (wp == null) continue; float distance = Vector2.Distance(position, wp.transform.position); if (distance < minDistance) { RaycastHit2D hit = Physics2D.Linecast(position, wp.transform.position, LayerMask.GetMask("Obstacles")); if (hit.collider == null) { minDistance = distance; bestWaypoint = wp; } } } return bestWaypoint; }
    private class PriorityQueue<T> { private List<KeyValuePair<T, float>> elements = new List<KeyValuePair<T, float>>(); public int Count => elements.Count; public void Enqueue(T item, float priority) { elements.Add(new KeyValuePair<T, float>(item, priority)); } public T Dequeue() { int bestIndex = 0; for (int i = 0; i < elements.Count; i++) { if (elements[i].Value < elements[bestIndex].Value) { bestIndex = i; } } T bestItem = elements[bestIndex].Key; elements.RemoveAt(bestIndex); return bestItem; } }
}