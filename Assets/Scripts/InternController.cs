using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(Rigidbody2D))]
public class InternController : MonoBehaviour
{
    public enum InternState { Patrolling, HelpingConfused, ServingFromQueue, CoveringDesk, GoingToBreak, OnBreak, GoingToToilet, AtToilet, ReturningToPatrol, Inactive, Working, TalkingToConfused }
    private InternState currentState = InternState.Inactive;

    [Header("Основные параметры")]
    public float moveSpeed = 2.5f;
    public List<Transform> patrolPoints;
    public Transform kitchenPoint;
    public Transform staffToiletPoint;

    [Header("Параметры поведения")]
    public float helpCheckInterval = 2f;
    public float chanceToServeFromQueue = 0.1f;
    public float chanceToCoverDesk = 0.5f;
    public float chanceToGoToToilet = 0.008f;
    public float timeInToilet = 4f;
    public float stoppingDistance = 0.2f;

    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private Waypoint[] allWaypoints;
    private Coroutine currentAction;
    private Transform currentPatrolTarget;
    private Vector2 targetPosition;
    private float helpTimer = 0f;
    private bool isWorking = false;
    private Queue<Waypoint> path = new Queue<Waypoint>();

    void Awake() { rb = GetComponent<Rigidbody2D>(); spriteRenderer = GetComponentInChildren<SpriteRenderer>(); allWaypoints = FindObjectsByType<Waypoint>(FindObjectsSortMode.None); }

    void Update() { if (!isWorking || Time.timeScale == 0f) { rb.linearVelocity = Vector2.zero; return; } helpTimer += Time.deltaTime; if (helpTimer >= helpCheckInterval) { helpTimer = 0f; LookForSomeoneToHelp(); } }
    void FixedUpdate() { if (!isWorking) return; UpdateSpriteDirection(); }

    public InternState GetCurrentState() => currentState;

    private void LookForSomeoneToHelp()
    {
        if (currentState != InternState.Patrolling) return;
        int vacantDeskId = ClientSpawner.GetVacantDesk();
        if (vacantDeskId != -1 && Random.value < chanceToCoverDesk) { if (ClientSpawner.ClaimDesk(vacantDeskId, this)) { if(currentAction != null) StopCoroutine(currentAction); currentAction = StartCoroutine(CoverDeskRoutine(vacantDeskId)); Debug.Log($"[ИНТЕРН] {name}: Вижу свободный стол #{vacantDeskId}, иду на замену."); return; } }
        ClientPathfinding confusedClient = ClientPathfinding.FindClosestConfusedClient(transform.position);
        if (confusedClient != null) { if(currentAction != null) StopCoroutine(currentAction); currentAction = StartCoroutine(HelpConfusedClientRoutine(confusedClient)); Debug.Log($"[ИНТЕРН] {name}: Вижу потеряшку {confusedClient.name}, иду помогать."); return; }
        if (Random.value < chanceToServeFromQueue) { ClientPathfinding queueClient = ClientQueueManager.GetRandomClientFromQueue(); if (queueClient != null) { if(currentAction != null) StopCoroutine(currentAction); currentAction = StartCoroutine(ServeFromQueueRoutine(queueClient)); Debug.Log($"[ИНТЕРН] {name}: Решил помочь клиенту из очереди {queueClient.name}."); } }
    }

    private IEnumerator MoveToTarget(Vector2 targetPosition, InternState stateOnArrival) { BuildPathTo(targetPosition); yield return StartCoroutine(FollowPath()); currentState = stateOnArrival; }
    private IEnumerator FollowPath() { while (path.Count > 0) { Waypoint targetWaypoint = path.Peek(); while (Vector2.Distance(transform.position, targetWaypoint.transform.position) > stoppingDistance) { rb.linearVelocity = ((Vector2)targetWaypoint.transform.position - (Vector2)transform.position).normalized * moveSpeed; yield return null; } rb.linearVelocity = Vector2.zero; path.Dequeue(); } }

    private IEnumerator CoverDeskRoutine(int deskId)
    {
        currentState = InternState.CoveringDesk;
        Transform targetWorkPoint = ClientSpawner.GetWorkPointForDesk(deskId);
        if (targetWorkPoint == null) { yield return StartCoroutine(ReturnToPatrolRoutine()); yield break; }
        yield return StartCoroutine(MoveToTarget(targetWorkPoint.position, InternState.Working));
        ClientQueueManager.SetServicePointAvailability(deskId, true);
        yield return new WaitUntil(() => ClientSpawner.GetVacantDesk() != deskId || !isWorking); 
        ClientQueueManager.SetServicePointAvailability(deskId, false);
        ClientSpawner.ReportDeskOccupation(deskId, null);
        yield return StartCoroutine(ReturnToPatrolRoutine());
    }

    private IEnumerator HelpConfusedClientRoutine(ClientPathfinding client)
    {
        currentState = InternState.HelpingConfused;
        yield return StartCoroutine(MoveToTarget(client.transform.position, InternState.TalkingToConfused));
        yield return new WaitForSeconds(1f);
        if (client != null) { client.stateMachine.ForceEndConfusion(); }
        yield return StartCoroutine(ReturnToPatrolRoutine());
    }

    private IEnumerator ServeFromQueueRoutine(ClientPathfinding client) { currentState = InternState.ServingFromQueue; yield return StartCoroutine(MoveToTarget(client.transform.position, InternState.Patrolling)); if (client != null) { float choice = Random.value; if (choice < 0.5f && ClientQueueManager.IsDesk1Available()) client.stateMachine.GetHelpFromIntern(ClientQueueManager.GetServicePointTransform(1).GetComponent<Waypoint>()); else if (choice < 0.8f && ClientQueueManager.IsDesk2Available()) client.stateMachine.GetHelpFromIntern(ClientQueueManager.GetServicePointTransform(2).GetComponent<Waypoint>()); else client.stateMachine.GetHelpFromIntern(ClientQueueManager.GetExitWaypointStatic()); } yield return StartCoroutine(ReturnToPatrolRoutine()); }
    public void StartShift() { if(currentAction != null) StopCoroutine(currentAction); currentAction = StartCoroutine(StartShiftRoutine()); }
    public void EndShift() { if(currentAction != null) StopCoroutine(currentAction); currentAction = StartCoroutine(EndShiftRoutine()); }
    public void GoOnBreak(float duration) { if(currentAction != null) StopCoroutine(currentAction); currentAction = StartCoroutine(BreakRoutine(duration)); }
    private IEnumerator StartShiftRoutine() { yield return new WaitForSeconds(10f + Random.Range(-10f, 10f)); isWorking = true; yield return StartCoroutine(ReturnToPatrolRoutine()); }
    private IEnumerator EndShiftRoutine() { yield return new WaitForSeconds(10f + Random.Range(-10f, 10f)); isWorking = false; currentState = InternState.Inactive; yield return StartCoroutine(MoveToTarget(kitchenPoint.position, InternState.Inactive)); currentAction = null; }
    private IEnumerator BreakRoutine(float duration) { isWorking = false; currentState = InternState.GoingToBreak; yield return StartCoroutine(MoveToTarget(kitchenPoint.position, InternState.OnBreak)); yield return new WaitForSeconds(duration); isWorking = true; yield return StartCoroutine(ReturnToPatrolRoutine()); }
    private IEnumerator ToiletBreakRoutine() { currentState = InternState.GoingToToilet; yield return StartCoroutine(MoveToTarget(staffToiletPoint.position, InternState.AtToilet)); yield return new WaitForSeconds(timeInToilet); yield return StartCoroutine(ReturnToPatrolRoutine()); }
    
    private IEnumerator ReturnToPatrolRoutine()
    {
        currentState = InternState.ReturningToPatrol;
        SelectNewPatrolPoint();
        if(currentPatrolTarget != null)
        {
            yield return StartCoroutine(MoveToTarget(currentPatrolTarget.position, InternState.Patrolling));
        }
        currentState = InternState.Patrolling;
        currentAction = null;
    }

    private void HandlePatrolling() { if (currentAction == null) { currentAction = StartCoroutine(ReturnToPatrolRoutine()); } }
    private void SelectNewPatrolPoint() { if (patrolPoints == null || patrolPoints.Count == 0) return; currentPatrolTarget = patrolPoints[Random.Range(0, patrolPoints.Count)]; }

    private void BuildPathTo(Vector2 targetPos) { path.Clear(); Waypoint startNode = FindNearestVisibleWaypoint(transform.position); Waypoint endNode = FindNearestVisibleWaypoint(targetPos); if (startNode == null || endNode == null) return; Dictionary<Waypoint, float> distances = new Dictionary<Waypoint, float>(); Dictionary<Waypoint, Waypoint> previous = new Dictionary<Waypoint, Waypoint>(); PriorityQueue<Waypoint, float> queue = new PriorityQueue<Waypoint, float>(); foreach (var wp in allWaypoints) { distances[wp] = float.MaxValue; previous[wp] = null; } distances[startNode] = 0; queue.Enqueue(startNode, 0); while(queue.Count > 0) { Waypoint current = queue.Dequeue(); if (current == endNode) { ReconstructPath(previous, endNode); return; } foreach(var neighbor in current.neighbors) { if(neighbor == null) continue; float newDist = distances[current] + Vector2.Distance(current.transform.position, neighbor.transform.position); if(distances.ContainsKey(neighbor) && newDist < distances[neighbor]) { distances[neighbor] = newDist; previous[neighbor] = current; queue.Enqueue(neighbor, newDist); } } } }
    private void ReconstructPath(Dictionary<Waypoint, Waypoint> previous, Waypoint goal) { List<Waypoint> pathList = new List<Waypoint>(); for (Waypoint at = goal; at != null; at = previous[at]) { pathList.Add(at); } pathList.Reverse(); path.Clear(); foreach (var wp in pathList) { path.Enqueue(wp); } }
    private void UpdateSpriteDirection() { if (rb.linearVelocity.x > 0.1f) spriteRenderer.flipX = false; else if (rb.linearVelocity.x < -0.1f) spriteRenderer.flipX = true; }
    private Waypoint FindNearestVisibleWaypoint(Vector2 position) { if (allWaypoints == null) return null; Waypoint bestWaypoint = null; float minDistance = float.MaxValue; foreach (var wp in allWaypoints) { if (wp == null) continue; float distance = Vector2.Distance(position, wp.transform.position); if (distance < minDistance) { RaycastHit2D hit = Physics2D.Linecast(position, wp.transform.position, LayerMask.GetMask("Obstacles")); if (hit.collider == null) { minDistance = distance; bestWaypoint = wp; } } } return bestWaypoint; }
    private class PriorityQueue<T, U> where U : System.IComparable<U> { private SortedDictionary<U, Queue<T>> d = new SortedDictionary<U, Queue<T>>(); public int Count => d.Sum(p => p.Value.Count); public void Enqueue(T i, U p) { if (!d.ContainsKey(p)) d[p] = new Queue<T>(); d[p].Enqueue(i); } public T Dequeue() { var p = d.First(); T i = p.Value.Dequeue(); if (p.Value.Count == 0) d.Remove(p.Key); return i; } }
}