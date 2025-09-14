// Файл: GuardMovement.cs - ВЕРСИЯ С НОВОЙ ГИБРИДНОЙ ЛОГИКОЙ ПОГОНИ
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
    private CharacterVisuals visuals;
    
    [Header("Дополнительные объекты")]
    public GameObject nightLight;
    
    [Header("Настройки фонарика")]
    [Tooltip("На каком расстоянии от центра будет фонарик при движении")]
    public float flashlightOffsetDistance = 0.5f;
    [Tooltip("Насколько плавно фонарик меняет свое положение")]
    public float flashlightSmoothingSpeed = 5f;
    
    [Header("Настройки патрулирования")]
    public List<Waypoint> patrolRoute;
    [SerializeField] private float minWaitTime = 1f;
    [SerializeField] private float maxWaitTime = 3f;
    
    [Header("Прочее поведение")]
    public float chanceToGoToToilet = 0.01f;
    public float timeInToilet = 5f;
    
    [Header("Настройки преследования")]
    [Tooltip("Множитель скорости во время погони. 1.5 = на 50% быстрее.")]
    public float chaseSpeedMultiplier = 1.5f;
    [Tooltip("С какого расстояния охранник переключится на прямое преследование, если видит цель")]
    public float directChaseDistance = 7f; 
    public AudioClip chaseShoutClip;
    [Tooltip("С какого расстояния охранник может начать разговор с нарушителем")]
    public float catchDistance = 1.2f;
    public float talkTime = 3f;
    public AudioClip reprimandSound;
    [Tooltip("Выберите слой, на котором находятся препятствия (стены, столы и т.д.)")]
    public LayerMask obstacleLayerMask;
    
    [Header("Рабочее место и протоколы")]
    [Tooltip("Точка, куда охранник пойдет писать протокол")]
    public Transform deskPoint;
    [Tooltip("Стопка, куда охранник будет складывать протоколы")]
    public DocumentStack protocolStack;
    
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
    private Waypoint[] allWaypoints;
    private Rigidbody2D rb;
    
    protected override void Awake()
    {
        base.Awake();
        visuals = GetComponent<CharacterVisuals>();
        audioSource = GetComponent<AudioSource>();
        allWaypoints = FindObjectsByType<Waypoint>(FindObjectsSortMode.None);
        rb = GetComponent<Rigidbody2D>();
    }
    
    protected override void Start()
    {
        base.Start();
        visuals?.Setup(gender);
        currentState = GuardState.OffDuty;
        LogCurrentState();
        if (skills != null)
        {
            skills.paperworkMastery = 0.25f;
            skills.sedentaryResilience = 0.5f;
            skills.pedantry = 0.75f;
            skills.softSkills = 0.5f;
        }
    }
    
    void Update()
    {
        if (Time.timeScale == 0f) return;
        UpdateStress();
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
        
        float finalStressGainRate = stressGainRate;
        if (skills != null)
        {
            finalStressGainRate *= (1f - skills.softSkills * 0.5f);
        }

        if (isOnDuty && !isResting)
        {
            currentStress += finalStressGainRate * Time.deltaTime;
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
        currentStress = maxStress * 0.9f;
        var clients = FindObjectsByType<ClientPathfinding>(FindObjectsSortMode.None);
        ClientPathfinding targetClient = clients
            .Where(c => c != null)
            .OrderBy(c => Vector2.Distance(transform.position, c.transform.position))
            .FirstOrDefault();
            
        if (targetClient != null)
        {
            Debug.Log($"Охранник {name} сорвался и выпроваживает клиента {targetClient.name}");
            yield return StartCoroutine(EvictRoutine(targetClient, true));
        } else {
            currentStress = maxStress * 0.7f;
            GoBackToDuties();
        }
    }

    public float GetStressPercent() => currentStress / maxStress;
    private void LogCurrentState() { logger?.LogState(GetStatusInfo()); }
    public GuardState GetCurrentState() => currentState;
    
    public bool IsAvailableAndOnDuty() => isOnDuty && 
        currentState != GuardState.Chasing && 
        currentState != GuardState.Talking && 
        currentState != GuardState.ChasingThief && 
        currentState != GuardState.EscortingThief && 
        currentState != GuardState.Evicting && 
        currentState != GuardState.StressedOut && 
        currentState != GuardState.WritingReport &&
        currentState != GuardState.OperatingBarrier;
        
    public override void GoOnBreak(float duration)
    {
        if(isOnDuty && currentState != GuardState.GoingToBreak && currentState != GuardState.OnBreak)
        {
            if(currentAction != null) StopCoroutine(currentAction);
            currentAction = StartCoroutine(BreakRoutine(duration));
        }
    }

    public void AssignToOperateBarrier(SecurityBarrier barrier, bool shouldActivate)
    {
        if (!IsAvailableAndOnDuty()) return;
        if (currentAction != null) StopCoroutine(currentAction);
        currentAction = StartCoroutine(OperateBarrierRoutine(barrier, shouldActivate));
    }

    private IEnumerator OperateBarrierRoutine(SecurityBarrier barrier, bool activate)
    {
        SetState(GuardState.OperatingBarrier);
        yield return StartCoroutine(MoveToTarget(barrier.guardInteractionPoint.position, GuardState.OperatingBarrier));
        
        Debug.Log($"{name} {(activate ? "активирует" : "деактивирует")} барьер...");
        yield return new WaitForSeconds(2.5f);
        if (activate)
        {
            barrier.ActivateBarrier();
        }
        else
        {
            barrier.DeactivateBarrier();
        }

        GoBackToDuties();
    }

    public void ReturnToPatrol()
    {
        if(isOnDuty && currentState == GuardState.OnBreak)
        {
            if(currentAction != null) StopCoroutine(currentAction);
            currentAction = StartCoroutine(ReturnToPatrolRoutine());
        }
    }
    
    public void AssignToChase(ClientPathfinding target)
    {
        if(currentState == GuardState.OffDuty || !IsAvailableAndOnDuty()) return;
        if(currentAction != null) StopCoroutine(currentAction);
        currentAction = StartCoroutine(ChaseRoutine(target));
    }
    
    public void AssignToCatchThief(ClientPathfinding target)
    {
        if(currentState == GuardState.OffDuty || !IsAvailableAndOnDuty()) return;
        if(currentAction != null) StopCoroutine(currentAction);
        currentAction = StartCoroutine(CatchThiefRoutine(target));
    }
    
    public void AssignToEvict(ClientPathfinding target)
    {
        if(currentState == GuardState.OffDuty || !IsAvailableAndOnDuty()) return;
        if(currentAction != null) StopCoroutine(currentAction);
        currentAction = StartCoroutine(EvictRoutine(target));
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
            Debug.LogWarning($"Для {name} не настроены точки отдыха (Kitchen Points)!");
            yield return new WaitForSeconds(duration);
        }
        currentAction = StartCoroutine(ReturnToPatrolRoutine());
    }
    
    private IEnumerator PatrolRoutine(List<Waypoint> route)
    {
        SetState(GuardState.Patrolling);
        while (isOnDuty && (currentState == GuardState.Patrolling || currentState == GuardState.WaitingAtWaypoint))
        {
            SelectNewRandomWaypoint(route);
            if (currentPatrolTarget != null)
            {
                SetState(GuardState.Patrolling);
                yield return StartCoroutine(MoveToTarget(currentPatrolTarget.transform.position, GuardState.WaitingAtWaypoint));
				ExperienceManager.Instance?.GrantXP(this, ActionType.PatrolWaypoint);
            }
            SetState(GuardState.WaitingAtWaypoint);
            yield return new WaitForSeconds(Random.Range(minWaitTime, maxWaitTime));

            float finalChanceToGoToToilet = chanceToGoToToilet;
            if (skills != null)
            {
                finalChanceToGoToToilet *= (1f - skills.sedentaryResilience);
            }

            if (Random.value < finalChanceToGoToToilet)
            {
                yield return StartCoroutine(ToiletBreakRoutine());
            }
            
            SetState(GuardState.Patrolling);
        }
    }
    
    private IEnumerator ToiletBreakRoutine()
    {
        SetState(GuardState.GoingToToilet);
        yield return StartCoroutine(EnterLimitedZoneAndWaitRoutine(staffToiletPoint, timeInToilet));
        SetState(GuardState.AtToilet);
    }

    private IEnumerator ChaseRoutine(ClientPathfinding target)
    {
        currentChaseTarget = target;
        SetState(GuardState.Chasing);
        agentMover.ApplySpeedMultiplier(chaseSpeedMultiplier);
        StartShouting();

        while (currentChaseTarget != null && Vector2.Distance(transform.position, currentChaseTarget.transform.position) > catchDistance)
        {
            RaycastHit2D hit = Physics2D.Linecast(transform.position, currentChaseTarget.transform.position, obstacleLayerMask);
            bool hasLineOfSight = hit.collider == null;
            float distanceToTarget = Vector2.Distance(transform.position, currentChaseTarget.transform.position);

            if (hasLineOfSight && distanceToTarget < directChaseDistance)
            {
                agentMover.StartDirectChase(currentChaseTarget.transform.position);
            }
            else
            {
                agentMover.SetPath(BuildPathTo(currentChaseTarget.transform.position));
            }
            
            yield return new WaitForSeconds(0.2f);
        }
        
        agentMover.StopDirectChase();

        if (currentChaseTarget != null)
        {
            agentMover.Stop();
            yield return StartCoroutine(TalkToClient());
        }
        else
        {
            GoBackToDuties();
        }
    }
    
    private IEnumerator TalkToClient()
    {
        SetState(GuardState.Talking);
        StopShouting();
        
        ClientPathfinding clientToCalm = currentChaseTarget;
        if (clientToCalm == null)
        {
            GoBackToDuties();
            yield break;
        }

        clientToCalm.Freeze();
        agentMover.Stop();
        
        clientToCalm.GetVisuals()?.SetEmotion(Emotion.Scared);
        if (reprimandSound != null) AudioSource.PlayClipAtPoint(reprimandSound, transform.position);
        yield return new WaitForSeconds(talkTime);
        
        if(clientToCalm != null)
        {
			ExperienceManager.Instance?.GrantXP(this, ActionType.CalmDownViolator);
            clientToCalm.UnfreezeAndRestartAI();
            if (Random.value < 0.5f) { clientToCalm.CalmDownAndReturnToQueue(); }
            else { clientToCalm.CalmDownAndLeave(); }
        }
        
        currentStress += stressGainPerViolator;
        GoBackToDuties();
    }
    
    private IEnumerator CatchThiefRoutine(ClientPathfinding target)
    {
        SetState(GuardState.ChasingThief);
        yield return StartCoroutine(ChaseRoutine(target));

        if (currentChaseTarget != null) 
        { 
            yield return StartCoroutine(EscortThiefToCashier());
        }
    }
    
    private IEnumerator EvictRoutine(ClientPathfinding target, bool isStressedOut = false)
    {
        SetState(GuardState.Evicting);
        yield return StartCoroutine(ChaseRoutine(target));

        if (currentChaseTarget != null)
        {
            SetState(GuardState.Talking);
            agentMover.Stop();
            currentChaseTarget.Freeze();
            if (reprimandSound != null) AudioSource.PlayClipAtPoint(reprimandSound, transform.position);
            yield return new WaitForSeconds(talkTime);
            if (currentChaseTarget != null)
            {
                currentChaseTarget.ForceLeave(ClientPathfinding.LeaveReason.Angry);
            }
        }

        if (isStressedOut)
        {
            currentStress = maxStress * 0.7f;
        }

        GoBackToDuties();
    }

    private IEnumerator EscortThiefToCashier()
    {
        SetState(GuardState.EscortingThief);
        StopShouting();
        ClientPathfinding thief = currentChaseTarget;
        thief?.GetVisuals()?.SetEmotion(Emotion.Sly);
        if (thief != null)
        {
            thief.Freeze();
            agentMover.Stop();
            yield return new WaitForSeconds(talkTime / 2);
            if (reprimandSound != null) AudioSource.PlayClipAtPoint(reprimandSound, transform.position);
            yield return new WaitForSeconds(talkTime / 2);
            LimitedCapacityZone cashierZone = ClientSpawner.GetCashierZone();
            if (cashierZone != null)
            {
                thief.stateMachine.StopAllActionCoroutines();
                thief.stateMachine.SetGoal(cashierZone.waitingWaypoint);
                thief.stateMachine.SetState(ClientState.MovingToGoal);
				ExperienceManager.Instance?.GrantXP(this, ActionType.CatchThief);
            }
        }
        currentStress += stressGainPerViolator;
        GoBackToDuties();
    }
    
    private void GoBackToDuties()
    {
        agentMover.ApplySpeedMultiplier(1f); 
        
        ClientPathfinding finishedTarget = currentChaseTarget;
        if (GuardManager.Instance != null)
        {
            GuardManager.Instance.ReportTaskFinished(currentChaseTarget);
        }
        
        StopShouting();
        currentChaseTarget = null;
        currentAction = null;

        if (finishedTarget != null && protocolStack != null && deskPoint != null)
        {
            currentAction = StartCoroutine(WriteProtocolRoutine());
        }
        else if (isOnDuty)
        {
            currentAction = StartCoroutine(ReturnToPatrolRoutine());
        }
        else
        {
            SetState(GuardState.OffDuty);
        }
    }

    private IEnumerator WriteProtocolRoutine()
    {
        SetState(GuardState.WritingReport);
        yield return StartCoroutine(MoveToTarget(deskPoint.position, GuardState.WritingReport));

        Debug.Log($"{name} пишет протокол...");
        yield return new WaitForSeconds(5f);

        protocolStack.AddDocumentToStack();
        Debug.Log($"{name} закончил протокол.");

        GoBackToDuties();
    }

    private IEnumerator MoveToTarget(Vector2 targetPosition, GuardState stateOnArrival)
    {
        agentMover.SetPath(BuildPathTo(targetPosition));
        yield return new WaitUntil(() => !agentMover.IsMoving());
        SetState(stateOnArrival);
    }
    
    private void SelectNewRandomWaypoint(List<Waypoint> route)
    {
        if (route == null || route.Count == 0) return;
        if (route.Count == 1) { currentPatrolTarget = route[0]; return; }
        Waypoint newWaypoint;
        do { newWaypoint = route[Random.Range(0, route.Count)];
        } while (newWaypoint == currentPatrolTarget);
        currentPatrolTarget = newWaypoint;
    }
    
    private void StartShouting()
    {
        if (shoutCoroutine == null && chaseShoutClip != null && audioSource != null)
        {
            shoutCoroutine = StartCoroutine(ShoutRoutine());
        }
    }
    
    private void StopShouting()
    {
        if (shoutCoroutine != null)
        {
            StopCoroutine(shoutCoroutine);
            shoutCoroutine = null;
        }
    }
    
    private IEnumerator ShoutRoutine()
    {
        while (true)
        {
            if(audioSource.isActiveAndEnabled)
                audioSource.PlayOneShot(chaseShoutClip);
            yield return new WaitForSeconds(Random.Range(3f, 5f));
        }
    }
    
    public string GetStatusInfo()
    {
        switch (currentState)
        {
            case GuardState.StressedOut: return "СОРВАЛСЯ!";
            case GuardState.WritingReport: return "Пишет протокол";
            case GuardState.Patrolling: return $"Патрулирует. Цель: {currentPatrolTarget?.name}";
            case GuardState.OnPost: return $"На посту";
            case GuardState.Chasing: return $"Преследует: {currentChaseTarget?.name}";
            case GuardState.Talking: return $"Разговаривает с: {currentChaseTarget?.name}";
            case GuardState.WaitingAtWaypoint: return $"Ожидает на точке: {currentPatrolTarget?.name}";
            case GuardState.OffDuty: return "Смена окончена";
            case GuardState.ChasingThief: return $"Ловит воришку: {currentChaseTarget?.name}";
            case GuardState.EscortingThief: return $"Ведет в кассу: {currentChaseTarget?.name}";
            case GuardState.Evicting: return $"Выпроваживает: {currentChaseTarget?.name}";
            case GuardState.OperatingBarrier: return "Управляет барьером";
            default: return currentState.ToString();
        }
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