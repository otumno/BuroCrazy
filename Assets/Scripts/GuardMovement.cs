// Файл: GuardMovement.cs
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(Rigidbody2D), typeof(AgentMover), typeof(CharacterStateLogger))]
public class GuardMovement : StaffController
{
    public enum GuardState { Patrolling, WaitingAtWaypoint, Chasing, Talking, OnPost, GoingToBreak, OnBreak, GoingToToilet, AtToilet, OffDuty, ChasingThief, EscortingThief, Evicting, StressedOut }
    
    [Header("Настройки Охранника")]
    private GuardState currentState = GuardState.OffDuty;
    
    [Header("Внешний вид")]
    [Tooltip("Укажите пол для выбора правильных спрайтов")]
    public Gender gender;
    private CharacterVisuals visuals;

    [Header("Дополнительные объекты")]
    public GameObject nightLight;
    
    [Header("Настройки патрулирования")]
    public List<Waypoint> patrolRoute;
    [SerializeField] private float minWaitTime = 1f;
    [SerializeField] private float maxWaitTime = 3f;
    
    [Header("Прочее поведение")]
    public float chanceToGoToToilet = 0.01f;
    public float timeInToilet = 5f;
    
    [Header("Настройки преследования")]
    public float chaseSpeedMultiplier = 2f;
    public AudioClip chaseShoutClip;
    public float catchDistance = 1.0f;
    public float talkTime = 3f;
    public AudioClip reprimandSound;

    [Header("Система Стресса")]
    public float maxStress = 100f;
    public float stressGainRate = 0.2f;
    public float stressGainPerViolator = 25f;
    public float stressReliefRate = 10f;
    private float currentStress = 0f;

    private AudioSource audioSource;
    private Coroutine shoutCoroutine;
    private Waypoint currentPatrolTarget;
    private ClientPathfinding currentChaseTarget;
    private int guardLayer, clientLayer;
    private List<Waypoint> nightPatrolRoute;
    private Waypoint[] allWaypoints;

    protected override void Awake() 
    { 
        base.Awake();
        visuals = GetComponent<CharacterVisuals>();
        audioSource = GetComponent<AudioSource>();
        allWaypoints = FindObjectsByType<Waypoint>(FindObjectsSortMode.None); 
    }
    
    protected override void Start() 
    { 
        base.Start();
        visuals?.Setup(gender);
        guardLayer = LayerMask.NameToLayer("Guard");
        clientLayer = LayerMask.NameToLayer("Client"); 
        nightPatrolRoute = NightPatrolRoute.GetNightRoute(); 
        
        currentState = GuardState.OffDuty;
        LogCurrentState();
    }
    
    void Update()
    {
        if (Time.timeScale == 0f) return;
        UpdateStress();
    }
    
    public override void StartShift() 
    { 
        if(isOnDuty) return; 
        if(currentAction != null) StopCoroutine(currentAction); 
        currentAction = StartCoroutine(StartShiftRoutine()); 
    }

    public override void EndShift() 
    { 
        if(!isOnDuty) return; 
        if(currentAction != null) StopCoroutine(currentAction); 
        currentAction = StartCoroutine(EndShiftRoutine()); 
    }
    
    private void SetState(GuardState newState) 
    { 
        if (currentState == newState) return; 
        currentState = newState; 
        LogCurrentState();

        visuals?.SetEmotionForState(newState);
    }

    private void UpdateStress()
    {
        if (currentState == GuardState.StressedOut) return;
        bool isResting = currentState == GuardState.OnBreak || currentState == GuardState.AtToilet || currentState == GuardState.OffDuty;
        if (isOnDuty && !isResting)
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
            if(currentAction != null) StopCoroutine(currentAction);
            currentAction = StartCoroutine(StressedOutRoutine());
        }
    }

    private IEnumerator StressedOutRoutine()
    {
        SetState(GuardState.StressedOut);
        var clients = FindObjectsByType<ClientPathfinding>(FindObjectsSortMode.None);
        ClientPathfinding targetClient = clients
            .Where(c => c != null)
            .OrderBy(c => Vector2.Distance(transform.position, c.transform.position))
            .FirstOrDefault();
        if (targetClient != null)
        {
            Debug.Log($"Охранник {name} сорвался и выпроваживает клиента {targetClient.name}");
            yield return StartCoroutine(EvictRoutine(targetClient));
        }

        currentStress = maxStress * 0.7f;
        GoBackToDuties();
    }

    public float GetStressPercent() => currentStress / maxStress;
    private void LogCurrentState() { logger?.LogState(GetStatusInfo()); }
    public GuardState GetCurrentState() => currentState;
    public bool IsAvailableAndOnDuty() => isOnDuty && currentState != GuardState.Chasing && currentState != GuardState.Talking && currentState != GuardState.ChasingThief && currentState != GuardState.EscortingThief && currentState != GuardState.Evicting && currentState != GuardState.StressedOut;
    
    public void GoOnBreak(Transform breakPointTarget) 
    { 
        if(isOnDuty && breakPointTarget != null && currentState != GuardState.GoingToBreak && currentState != GuardState.OnBreak) 
        { 
            if(currentAction != null) StopCoroutine(currentAction);
            currentAction = StartCoroutine(BreakRoutine(breakPointTarget)); 
        } 
    }

    public void ReturnToPatrol() { if(isOnDuty && currentState == GuardState.OnBreak) { if(currentAction != null) StopCoroutine(currentAction);
        currentAction = StartCoroutine(ReturnToPatrolRoutine()); } }
    public void GoToPost(Transform post) { if(currentAction != null) StopCoroutine(currentAction); currentAction = StartCoroutine(GoToPostRoutine(post));
    }
    public void AssignToChase(ClientPathfinding target) { if(currentState == GuardState.OffDuty) return; if(currentAction != null) StopCoroutine(currentAction); currentAction = StartCoroutine(ChaseRoutine(target));
    }
    public void AssignToCatchThief(ClientPathfinding target) { if(currentState == GuardState.OffDuty) return; if(currentAction != null) StopCoroutine(currentAction); currentAction = StartCoroutine(CatchThiefRoutine(target));
    }
    public void AssignToEvict(ClientPathfinding target) { if(currentState == GuardState.OffDuty) return; if(currentAction != null) StopCoroutine(currentAction); currentAction = StartCoroutine(EvictRoutine(target));
    }
    private IEnumerator StartShiftRoutine() 
    { 
        isOnDuty = true; 
        if(startShiftSound != null) AudioSource.PlayClipAtPoint(startShiftSound, transform.position);
        yield return StartCoroutine(ReturnToPatrolRoutine()); 
    }
        
    private IEnumerator EndShiftRoutine() 
    { 
        isOnDuty = false; 
        SetState(GuardState.OffDuty);
        if (homePoint != null) { 
            yield return StartCoroutine(MoveToTarget(homePoint.position, GuardState.OffDuty)); 
            if(endShiftSound != null) AudioSource.PlayClipAtPoint(endShiftSound, homePoint.position);
        } else { 
            Debug.LogWarning($"HomePoint не назначен для {gameObject.name}. Охранник останется на месте."); 
        } 
        currentAction = null;
    }
    
    private IEnumerator ReturnToPatrolRoutine() 
    {
        yield return StartCoroutine(PatrolRoutine(patrolRoute));
        currentAction = null;
    }
    
    private IEnumerator BreakRoutine(Transform breakPointTarget) 
    { 
        SetState(GuardState.GoingToBreak);
        yield return StartCoroutine(MoveToTarget(breakPointTarget.position, GuardState.OnBreak));
        
        while (ClientSpawner.CurrentPeriodName != null && ClientSpawner.CurrentPeriodName.ToLower().Trim() == "обед")
        {
            yield return null;
        }
    }
    
    private IEnumerator GoToPostRoutine(Transform post) { yield return StartCoroutine(MoveToTarget(post.position, GuardState.OnPost));
    }
    private IEnumerator PatrolRoutine(List<Waypoint> route) { SetState(GuardState.Patrolling);
        while (isOnDuty && (currentState == GuardState.Patrolling || currentState == GuardState.WaitingAtWaypoint)) { SelectNewRandomWaypoint(route); if (currentPatrolTarget != null) { SetState(GuardState.Patrolling);
            yield return StartCoroutine(MoveToTarget(currentPatrolTarget.transform.position, GuardState.WaitingAtWaypoint)); } SetState(GuardState.WaitingAtWaypoint); yield return new WaitForSeconds(Random.Range(minWaitTime, maxWaitTime)); if (Random.value < chanceToGoToToilet) { yield return StartCoroutine(ToiletBreakRoutine());
        } SetState(GuardState.Patrolling); } }
    private IEnumerator ToiletBreakRoutine() { GuardState stateBefore = currentState; SetState(GuardState.GoingToToilet); yield return StartCoroutine(MoveToTarget(staffToiletPoint.position, GuardState.AtToilet));
        yield return new WaitForSeconds(timeInToilet); SetState(stateBefore); }
    private IEnumerator ChaseRoutine(ClientPathfinding target) { currentChaseTarget = target; SetState(GuardState.Chasing);
        agentMover.moveSpeed *= chaseSpeedMultiplier; StartShouting(); Physics2D.IgnoreLayerCollision(guardLayer, clientLayer, true); while(currentChaseTarget != null) { if (Vector2.Distance(transform.position, currentChaseTarget.transform.position) < catchDistance) { yield return StartCoroutine(TalkToClient());
            yield break; } else { agentMover.SetPath(BuildPathTo(currentChaseTarget.transform.position)); yield return new WaitForSeconds(1f); } } GoBackToDuties();
    }
    private IEnumerator CatchThiefRoutine(ClientPathfinding target) { currentChaseTarget = target; SetState(GuardState.ChasingThief); agentMover.moveSpeed *= chaseSpeedMultiplier; StartShouting(); Physics2D.IgnoreLayerCollision(guardLayer, clientLayer, true);
        while(currentChaseTarget != null && Vector2.Distance(transform.position, currentChaseTarget.transform.position) > catchDistance) { agentMover.SetPath(BuildPathTo(currentChaseTarget.transform.position)); yield return new WaitForSeconds(0.5f);
        } if (currentChaseTarget != null) { yield return StartCoroutine(EscortThiefToCashier()); } else { GoBackToDuties();
        } }
    private IEnumerator EvictRoutine(ClientPathfinding target) { currentChaseTarget = target; SetState(GuardState.Evicting);
        while(currentChaseTarget != null && Vector2.Distance(transform.position, currentChaseTarget.transform.position) > catchDistance) { agentMover.SetPath(BuildPathTo(currentChaseTarget.transform.position)); yield return new WaitForSeconds(0.5f);
        } if (currentChaseTarget != null) { SetState(GuardState.Talking); agentMover.Stop(); currentChaseTarget.Freeze(); transform.position = (Vector2)currentChaseTarget.transform.position + Random.insideUnitCircle * 0.6f;
            if (reprimandSound != null) AudioSource.PlayClipAtPoint(reprimandSound, transform.position); yield return new WaitForSeconds(talkTime); if (currentChaseTarget != null) { currentChaseTarget.stateMachine.GetHelpFromIntern(ClientSpawner.Instance.exitWaypoint); } } GoBackToDuties();
    }
    private IEnumerator TalkToClient() { 
        SetState(GuardState.Talking); 
        StopShouting(); 
        ClientPathfinding clientToCalm = currentChaseTarget; 
        clientToCalm?.GetVisuals()?.SetEmotion(Emotion.Scared);
        if (clientToCalm == null) { GoBackToDuties();
            yield break; } 
        clientToCalm.Freeze(); agentMover.Stop(); transform.position = (Vector2)clientToCalm.transform.position + Random.insideUnitCircle * 0.6f; if (reprimandSound != null) AudioSource.PlayClipAtPoint(reprimandSound, transform.position);
        yield return new WaitForSeconds(talkTime); if(clientToCalm != null) { clientToCalm.UnfreezeAndRestartAI(); if (Random.value < 0.5f) { clientToCalm.CalmDownAndReturnToQueue(); } else { clientToCalm.CalmDownAndLeave();
        } if(ClientQueueManager.Instance != null) ClientQueueManager.Instance.dissatisfiedClients.Remove(clientToCalm); } currentStress += stressGainPerViolator; GoBackToDuties(); }
    private IEnumerator EscortThiefToCashier() { 
        SetState(GuardState.EscortingThief); 
        StopShouting();
        ClientPathfinding thief = currentChaseTarget; 
        thief?.GetVisuals()?.SetEmotion(Emotion.Scared);
        if (thief != null) { thief.Freeze(); agentMover.Stop(); transform.position = (Vector2)thief.transform.position + Random.insideUnitCircle * 0.6f;
            if (reprimandSound != null) AudioSource.PlayClipAtPoint(reprimandSound, transform.position); yield return new WaitForSeconds(talkTime); LimitedCapacityZone cashierZone = ClientSpawner.GetCashierZone(); if (cashierZone != null) { thief.stateMachine.StopAllActionCoroutines();
                thief.stateMachine.SetGoal(cashierZone.waitingWaypoint); thief.stateMachine.SetState(ClientState.MovingToGoal); } } currentStress += stressGainPerViolator; GoBackToDuties(); }
    private void GoBackToDuties() { if(currentState == GuardState.Chasing || currentState == GuardState.ChasingThief) {agentMover.moveSpeed /= chaseSpeedMultiplier;} if (GuardManager.Instance != null && currentChaseTarget != null) { GuardManager.Instance.ReportTaskFinished(currentChaseTarget);
    } Physics2D.IgnoreLayerCollision(guardLayer, clientLayer, false); StopShouting(); currentChaseTarget = null; currentAction = null;
        if (ClientSpawner.Instance != null && isOnDuty) { ClientSpawner.Instance.SendMessage("HandleSpecialPeriodLogic", ClientSpawner.CurrentPeriodName.ToLower().Trim(), SendMessageOptions.DontRequireReceiver);
        } }
    private IEnumerator MoveToTarget(Vector2 targetPosition, GuardState stateOnArrival) { var path = BuildPathTo(targetPosition);
        if (path.Count == 0 && Vector2.Distance(transform.position, targetPosition) > 0.5f) { Debug.LogWarning($"Охранник {gameObject.name} не смог построить путь к {targetPosition}. Проверьте доступность точки.");
            SetState(stateOnArrival); yield break; } agentMover.SetPath(path); yield return new WaitUntil(() => !agentMover.IsMoving()); SetState(stateOnArrival);
    }
    private void SelectNewRandomWaypoint(List<Waypoint> route) { if (route == null || route.Count == 0) return;
        if (route.Count == 1) { currentPatrolTarget = route[0]; return; } Waypoint newWaypoint; do { newWaypoint = route[Random.Range(0, route.Count)];
        } while (newWaypoint == currentPatrolTarget); currentPatrolTarget = newWaypoint; }
    private void StartShouting() { if (shoutCoroutine == null && chaseShoutClip != null && audioSource != null) { shoutCoroutine = StartCoroutine(ShoutRoutine());
    } }
    private void StopShouting() { if (shoutCoroutine != null) { StopCoroutine(shoutCoroutine); shoutCoroutine = null;
    } }
    private IEnumerator ShoutRoutine() { while (true) { audioSource.PlayOneShot(chaseShoutClip); yield return new WaitForSeconds(Random.Range(3f, 5f));
    } }
    public string GetStatusInfo() { switch (currentState) { case GuardState.StressedOut: return "СОРВАЛСЯ!";
        case GuardState.Patrolling: return $"Патрулирует. Цель: {currentPatrolTarget?.name}"; case GuardState.OnPost: return $"На посту"; case GuardState.Chasing: return $"Преследует: {currentChaseTarget?.name}";
        case GuardState.Talking: return $"Разговаривает с: {currentChaseTarget?.name}"; case GuardState.WaitingAtWaypoint: return $"Ожидает на точке: {currentPatrolTarget?.name}"; case GuardState.OffDuty: return "Смена окончена";
        case GuardState.ChasingThief: return $"Ловит воришку: {currentChaseTarget?.name}"; case GuardState.EscortingThief: return $"Ведет в кассу: {currentChaseTarget?.name}"; case GuardState.Evicting: return $"Выпроваживает: {currentChaseTarget?.name}";
        default: return currentState.ToString(); } }
    private Queue<Waypoint> BuildPathTo(Vector2 targetPos) { var path = new Queue<Waypoint>();
        Waypoint startNode = FindNearestVisibleWaypoint(transform.position); Waypoint endNode = FindNearestVisibleWaypoint(targetPos); if (startNode == null || endNode == null) { startNode = FindNearestWaypoint(transform.position);
            endNode = FindNearestWaypoint(targetPos); } if (startNode == null || endNode == null) return path;
        Dictionary<Waypoint, float> distances = new Dictionary<Waypoint, float>(); Dictionary<Waypoint, Waypoint> previous = new Dictionary<Waypoint, Waypoint>();
        PriorityQueue<Waypoint, float> queue = new PriorityQueue<Waypoint, float>(); foreach (var wp in allWaypoints) { distances[wp] = float.MaxValue; previous[wp] = null;
        } distances[startNode] = 0; queue.Enqueue(startNode, 0); while(queue.Count > 0) { Waypoint current = queue.Dequeue();
            if (current == endNode) { ReconstructPath(previous, endNode, path); return path; } foreach(var neighbor in current.neighbors) { if(neighbor == null) continue;
                if (neighbor.forbiddenTags != null && neighbor.forbiddenTags.Contains(gameObject.tag)) continue; float newDist = distances[current] + Vector2.Distance(current.transform.position, neighbor.transform.position);
                if(distances.ContainsKey(neighbor) && newDist < distances[neighbor]) { distances[neighbor] = newDist; previous[neighbor] = current; queue.Enqueue(neighbor, newDist); } } } return path;
    }
    private void ReconstructPath(Dictionary<Waypoint, Waypoint> previous, Waypoint goal, Queue<Waypoint> path) { List<Waypoint> pathList = new List<Waypoint>();
        for (Waypoint at = goal; at != null; at = previous[at]) { pathList.Add(at); } pathList.Reverse(); path.Clear();
        foreach (var wp in pathList) { path.Enqueue(wp); } }
    private Waypoint FindNearestVisibleWaypoint(Vector2 position) { if (allWaypoints == null) return null;
        Waypoint bestWaypoint = null; float minDistance = float.MaxValue; foreach (var wp in allWaypoints) { if (wp == null) continue;
            float distance = Vector2.Distance(position, wp.transform.position); if (distance < minDistance) { RaycastHit2D hit = Physics2D.Linecast(position, wp.transform.position, LayerMask.GetMask("Obstacles"));
                if (hit.collider == null) { minDistance = distance; bestWaypoint = wp; } } } return bestWaypoint;
    }
    private Waypoint FindNearestWaypoint(Vector2 position) { if (allWaypoints == null) return null; return allWaypoints.OrderBy(wp => Vector2.Distance(position, wp.transform.position)).FirstOrDefault();
    }
    private class PriorityQueue<T, U> where U : System.IComparable<U> { private SortedDictionary<U, Queue<T>> d = new SortedDictionary<U, Queue<T>>();
        public int Count => d.Sum(p => p.Value.Count); public void Enqueue(T i, U p) { if (!d.ContainsKey(p)) d[p] = new Queue<T>();
            d[p].Enqueue(i); } public T Dequeue() { var p = d.First(); T i = p.Value.Dequeue(); if (p.Value.Count == 0) d.Remove(p.Key);
            return i; } }
}