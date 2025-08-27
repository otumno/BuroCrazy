using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(Rigidbody2D), typeof(AgentMover), typeof(CharacterStateLogger))]
public class GuardMovement : MonoBehaviour
{
    public enum GuardState { Patrolling, WaitingAtWaypoint, Chasing, Talking, OnPost, GoingToBreak, OnBreak, GoingToToilet, AtToilet, OffDuty }
    public enum Shift { Day, Night, Universal }
    
    private GuardState currentState = GuardState.OffDuty;
    public Shift assignedShift { get; private set; }

    [Header("Настройки патрулирования")]
    public List<Waypoint> patrolRoute;
    [SerializeField] private float minWaitTime = 1f;
    [SerializeField] private float maxWaitTime = 3f;
    
    [Header("Прочее поведение")]
    public Transform staffToiletPoint;
    public Waypoint homePointWaypoint; 
    public float chanceToGoToToilet = 0.01f;
    public float timeInToilet = 5f;

    [Header("Настройки преследования")]
    public float chaseSpeedMultiplier = 2f;
    public AudioClip chaseShoutClip;
    public float catchDistance = 1.0f;
    public float talkTime = 3f;

    [Header("Звуки смены")]
    public AudioClip startShiftSound;
    public AudioClip endShiftSound;

    private AudioSource audioSource;
    private Coroutine currentAction;
    private Coroutine shoutCoroutine;
    private Waypoint currentPatrolTarget;
    private ClientPathfinding currentChaseTarget;
    private int guardLayer, clientLayer;
    private List<Waypoint> nightPatrolRoute;
    private bool isOnDuty = false;
    private Waypoint[] allWaypoints;
    
    private AgentMover agentMover;
    private CharacterStateLogger logger;

    void Awake() { audioSource = GetComponent<AudioSource>(); allWaypoints = FindObjectsByType<Waypoint>(FindObjectsSortMode.None); agentMover = GetComponent<AgentMover>(); logger = GetComponent<CharacterStateLogger>(); }
    void Start() { guardLayer = LayerMask.NameToLayer("Guard"); clientLayer = LayerMask.NameToLayer("Client"); nightPatrolRoute = NightPatrolRoute.GetNightRoute(); LogCurrentState(); }

    private void SetState(GuardState newState) { if (currentState == newState) return; currentState = newState; LogCurrentState(); }
    private void LogCurrentState() { logger?.LogState(GetStatusInfo()); }
    public GuardState GetCurrentState() => currentState;
    public bool IsAvailableAndOnDuty() => isOnDuty && currentState != GuardState.Chasing && currentState != GuardState.Talking;
    public void AssignShift(Shift shift) => assignedShift = shift;
    
    public void StartShift() { if(!isOnDuty) { if(currentAction != null) StopCoroutine(currentAction); currentAction = StartCoroutine(StartShiftRoutine()); } }
    public void EndShift() { if(currentAction != null) StopCoroutine(currentAction); currentAction = StartCoroutine(EndShiftRoutine()); }
    public void GoOnBreak(Transform breakPoint) { if(currentAction != null) StopCoroutine(currentAction); currentAction = StartCoroutine(BreakRoutine(breakPoint)); }
    public void ReturnToPatrol() { if(currentAction != null) StopCoroutine(currentAction); currentAction = StartCoroutine(ReturnToPatrolRoutine()); }
    public void GoToPost(Transform post) { if(currentAction != null) StopCoroutine(currentAction); currentAction = StartCoroutine(GoToPostRoutine(post)); }
    public void AssignToChase(ClientPathfinding target) { if(currentAction != null) StopCoroutine(currentAction); currentAction = StartCoroutine(ChaseRoutine(target)); }
    
    private IEnumerator StartShiftRoutine() { isOnDuty = true; if(startShiftSound != null && homePointWaypoint != null) AudioSource.PlayClipAtPoint(startShiftSound, homePointWaypoint.transform.position); yield return new WaitForSeconds(Random.Range(0f, 5f)); yield return StartCoroutine(ReturnToPatrolRoutine()); }
    private IEnumerator EndShiftRoutine() { isOnDuty = false; yield return StartCoroutine(MoveToTarget(homePointWaypoint.transform.position, GuardState.OffDuty)); if(endShiftSound != null) AudioSource.PlayClipAtPoint(endShiftSound, homePointWaypoint.transform.position); currentAction = null; }
    private IEnumerator ReturnToPatrolRoutine() { yield return StartCoroutine(PatrolRoutine(patrolRoute)); }
    private IEnumerator BreakRoutine(Transform breakPoint) { SetState(GuardState.GoingToBreak); yield return StartCoroutine(MoveToTarget(breakPoint.position, GuardState.OnBreak)); }
    private IEnumerator GoToPostRoutine(Transform post) { yield return StartCoroutine(MoveToTarget(post.position, GuardState.OnPost)); }
    
    private IEnumerator PatrolRoutine(List<Waypoint> route) { SetState(GuardState.Patrolling); while (isOnDuty && (currentState == GuardState.Patrolling || currentState == GuardState.WaitingAtWaypoint)) { SelectNewRandomWaypoint(route); if (currentPatrolTarget != null) { SetState(GuardState.Patrolling); yield return StartCoroutine(MoveToTarget(currentPatrolTarget.transform.position, GuardState.WaitingAtWaypoint)); } SetState(GuardState.WaitingAtWaypoint); yield return new WaitForSeconds(Random.Range(minWaitTime, maxWaitTime)); if (Random.value < chanceToGoToToilet) { yield return StartCoroutine(ToiletBreakRoutine()); } SetState(GuardState.Patrolling); } }
    private IEnumerator ToiletBreakRoutine() { GuardState stateBefore = currentState; SetState(GuardState.GoingToToilet); yield return StartCoroutine(MoveToTarget(staffToiletPoint.position, GuardState.AtToilet)); yield return new WaitForSeconds(timeInToilet); SetState(stateBefore); }
    private IEnumerator ChaseRoutine(ClientPathfinding target) { currentChaseTarget = target; SetState(GuardState.Chasing); agentMover.moveSpeed *= chaseSpeedMultiplier; StartShouting(); Physics2D.IgnoreLayerCollision(guardLayer, clientLayer, true); while(currentChaseTarget != null) { if (Vector2.Distance(transform.position, currentChaseTarget.transform.position) < catchDistance) { yield return StartCoroutine(TalkToClient()); yield break; } else { agentMover.SetPath(BuildPathTo(currentChaseTarget.transform.position)); yield return new WaitForSeconds(1f); } } GoBackToDuties(); }
    private IEnumerator TalkToClient() { SetState(GuardState.Talking); StopShouting(); ClientPathfinding clientToCalm = currentChaseTarget; if (clientToCalm == null) { GoBackToDuties(); yield break; } clientToCalm.Freeze(); agentMover.Stop(); yield return new WaitForSeconds(talkTime); if(clientToCalm != null) { clientToCalm.UnfreezeAndRestartAI(); if (Random.value < 0.5f) { clientToCalm.CalmDownAndReturnToQueue(); } else { clientToCalm.CalmDownAndLeave(); } ClientQueueManager.Instance.dissatisfiedClients.Remove(clientToCalm); } GoBackToDuties(); }
    
    private void GoBackToDuties() { agentMover.moveSpeed /= chaseSpeedMultiplier; if (GuardManager.Instance != null && currentChaseTarget != null) { GuardManager.Instance.ReportTaskFinished(currentChaseTarget); } Physics2D.IgnoreLayerCollision(guardLayer, clientLayer, false); StopShouting(); currentChaseTarget = null; currentAction = null; if (GuardManager.Instance != null) { GuardManager.Instance.SendMessage("OnPeriodChange", ClientSpawner.CurrentPeriodName, SendMessageOptions.DontRequireReceiver); } }
    private IEnumerator MoveToTarget(Vector2 targetPosition, GuardState stateOnArrival) { agentMover.SetPath(BuildPathTo(targetPosition)); yield return new WaitUntil(() => !agentMover.IsMoving()); SetState(stateOnArrival); }
    
    private void SelectNewRandomWaypoint(List<Waypoint> route) { if (route == null || route.Count == 0) return; if (route.Count == 1) { currentPatrolTarget = route[0]; return; } Waypoint newWaypoint; do { newWaypoint = route[Random.Range(0, route.Count)]; } while (newWaypoint == currentPatrolTarget); currentPatrolTarget = newWaypoint; }
    private void StartShouting() { if (shoutCoroutine == null && chaseShoutClip != null && audioSource != null) { shoutCoroutine = StartCoroutine(ShoutRoutine()); } }
    private void StopShouting() { if (shoutCoroutine != null) { StopCoroutine(shoutCoroutine); shoutCoroutine = null; } }
    private IEnumerator ShoutRoutine() { while (true) { audioSource.PlayOneShot(chaseShoutClip); yield return new WaitForSeconds(Random.Range(3f, 5f)); } }
    public string GetStatusInfo() { switch (currentState) { case GuardState.Patrolling: return $"Патрулирует. Цель: {currentPatrolTarget?.name}"; case GuardState.OnPost: return $"На посту"; case GuardState.Chasing: return $"Преследует: {currentChaseTarget?.name}"; case GuardState.Talking: return $"Разговаривает с: {currentChaseTarget?.name}"; case GuardState.WaitingAtWaypoint: return $"Ожидает на точке: {currentPatrolTarget?.name}"; case GuardState.OffDuty: return "Смена окончена"; default: return currentState.ToString(); } }
    
    private Queue<Waypoint> BuildPathTo(Vector2 targetPos) { var path = new Queue<Waypoint>(); Waypoint startNode = FindNearestVisibleWaypoint(transform.position); Waypoint endNode = FindNearestVisibleWaypoint(targetPos); if (startNode == null || endNode == null) { startNode = FindNearestWaypoint(transform.position); endNode = FindNearestWaypoint(targetPos); } if (startNode == null || endNode == null) return path; Dictionary<Waypoint, float> distances = new Dictionary<Waypoint, float>(); Dictionary<Waypoint, Waypoint> previous = new Dictionary<Waypoint, Waypoint>(); PriorityQueue<Waypoint, float> queue = new PriorityQueue<Waypoint, float>(); foreach (var wp in allWaypoints) { distances[wp] = float.MaxValue; previous[wp] = null; } distances[startNode] = 0; queue.Enqueue(startNode, 0); while(queue.Count > 0) { Waypoint current = queue.Dequeue(); if (current == endNode) { ReconstructPath(previous, endNode, path); return path; } foreach(var neighbor in current.neighbors) { if(neighbor == null) continue; if (neighbor.forbiddenTags != null && neighbor.forbiddenTags.Contains(gameObject.tag)) continue; float newDist = distances[current] + Vector2.Distance(current.transform.position, neighbor.transform.position); if(distances.ContainsKey(neighbor) && newDist < distances[neighbor]) { distances[neighbor] = newDist; previous[neighbor] = current; queue.Enqueue(neighbor, newDist); } } } return path; }
    private void ReconstructPath(Dictionary<Waypoint, Waypoint> previous, Waypoint goal, Queue<Waypoint> path) { List<Waypoint> pathList = new List<Waypoint>(); for (Waypoint at = goal; at != null; at = previous[at]) { pathList.Add(at); } pathList.Reverse(); path.Clear(); foreach (var wp in pathList) { path.Enqueue(wp); } }
    private Waypoint FindNearestVisibleWaypoint(Vector2 position) { if (allWaypoints == null) return null; Waypoint bestWaypoint = null; float minDistance = float.MaxValue; foreach (var wp in allWaypoints) { if (wp == null) continue; float distance = Vector2.Distance(position, wp.transform.position); if (distance < minDistance) { RaycastHit2D hit = Physics2D.Linecast(position, wp.transform.position, LayerMask.GetMask("Obstacles")); if (hit.collider == null) { minDistance = distance; bestWaypoint = wp; } } } return bestWaypoint; }
    private Waypoint FindNearestWaypoint(Vector2 position) { if (allWaypoints == null) return null; return allWaypoints.OrderBy(wp => Vector2.Distance(position, wp.transform.position)).FirstOrDefault(); }
    private class PriorityQueue<T, U> where U : System.IComparable<U> { private SortedDictionary<U, Queue<T>> d = new SortedDictionary<U, Queue<T>>(); public int Count => d.Sum(p => p.Value.Count); public void Enqueue(T i, U p) { if (!d.ContainsKey(p)) d[p] = new Queue<T>(); d[p].Enqueue(i); } public T Dequeue() { var p = d.First(); T i = p.Value.Dequeue(); if (p.Value.Count == 0) d.Remove(p.Key); return i; } }
}