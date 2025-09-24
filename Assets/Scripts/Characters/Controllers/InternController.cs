using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(AgentMover))]
[RequireComponent(typeof(CharacterStateLogger))]
[RequireComponent(typeof(StackHolder))]
public class InternController : StaffController
{
    public enum InternState { Patrolling, HelpingConfused, ServingFromQueue, CoveringDesk, GoingToBreak, OnBreak, GoingToToilet, AtToilet, ReturningToPatrol, Inactive, Working, TalkingToConfused, TakingStackToArchive }
    
    [Header("Настройки стажера")]
    private InternState currentState = InternState.Inactive;
    
    [Header("Внешний вид")]
    public EmotionSpriteCollection spriteCollection;
    public StateEmotionMap stateEmotionMap;
    
    private Transform currentPatrolTarget;
    private StackHolder stackHolder;
    // --- Поля 'visuals' и 'patrolPoints' удалены, так как они теперь в базовом классе StaffController ---

    protected override void Awake()
    {
        base.Awake();
        stackHolder = GetComponent<StackHolder>();
    }

    protected override void Start()
    {
        base.Start(); // Вызывает Start() из StaffController, который запускает инициализацию
        
        // --- Настройка скиллов по умолчанию для стажера ---
        if (skills != null)
        {
            skills.paperworkMastery = 0.5f;
            skills.pedantry = 0.25f;
            skills.softSkills = 0.75f;
            skills.corruption = 0.0f;
        }

        SetState(InternState.Inactive);
    }
    
    // --- "МОЗГ" СТАЖЁРА: РЕАЛИЗАЦИЯ МЕТОДОВ ИЗ STAFFCONTROLLER ---

    protected override bool TryExecuteAction(ActionType actionType)
    {
        // "Диспетчер" для стажера. Проверяет, можно ли запустить то или иное действие.
        switch (actionType)
        {
            case ActionType.HelpConfusedClient:
                var confusedClient = ClientPathfinding.FindClosestConfusedClient(transform.position);
                if (confusedClient != null)
                {
                    currentAction = StartCoroutine(HelpConfusedClientRoutine(confusedClient));
                    return true; // Действие можно выполнить
                }
                return false; // Не найдено потерявшихся клиентов

            case ActionType.CoverDesk:
                var absentClerk = ClientSpawner.GetAbsentClerk();
                // TODO: Добавить проверку, что клерка еще не подменяют
                if (absentClerk != null) 
                {
                    currentAction = StartCoroutine(CoverDeskRoutine(absentClerk));
                    return true; // Действие можно выполнить
                }
                return false; // Нет отсутствующих клерков
            
            // TODO: Добавить сюда case'ы для DeliverDocuments и ServeFromQueue, когда мы их реализуем
        }
        return false;
    }

    protected override void ExecuteDefaultAction()
    {
        // Если других дел нет, стажер просто патрулирует
        currentAction = StartCoroutine(PatrolRoutine());
    }

    // --- РЕАЛИЗАЦИЯ ПОВЕДЕНИЯ (КОРУТИНЫ) ---

    private IEnumerator HelpConfusedClientRoutine(ClientPathfinding client)
    {
        SetState(InternState.HelpingConfused);
        yield return StartCoroutine(MoveToTarget(client.transform.position, InternState.TalkingToConfused));
        
        if (client != null && client.stateMachine.GetCurrentState() == ClientState.Confused)
        {
            yield return new WaitForSeconds(1f);
            Waypoint correctGoal = DetermineCorrectGoalFor(client);
            client.stateMachine.GetHelpFromIntern(correctGoal);
			ExperienceManager.Instance?.GrantXP(this, ActionType.HelpConfusedClient);
        }
        
        currentAction = null; // Завершаем действие
    }
    
    private IEnumerator CoverDeskRoutine(ClerkController clerk)
    {
        if (clerk.assignedServicePoint == null) 
        {
            currentAction = null;
            yield break;
        }

        SetState(InternState.CoveringDesk);
        yield return StartCoroutine(MoveToTarget(clerk.assignedServicePoint.clerkStandPoint.position, InternState.Working));
        
        // Ждем, пока клерк не вернется
        yield return new WaitUntil(() => clerk == null || !clerk.IsOnBreak());

		ExperienceManager.Instance?.GrantXP(this, ActionType.CoverDesk);
        currentAction = null; // Завершаем действие
    }

    private IEnumerator PatrolRoutine()
    {
        SetState(InternState.Patrolling);
        SelectNewPatrolPoint();
        if (currentPatrolTarget != null)
        {
            yield return StartCoroutine(MoveToTarget(currentPatrolTarget.position, InternState.Patrolling));
        }
        yield return new WaitForSeconds(Random.Range(2f, 5f));
        currentAction = null; // Завершаем действие
    }

    public override void GoOnBreak(float duration)
    {
        // TODO: Реализовать логику перерыва
        Debug.Log($"{characterName} уходит на перерыв.");
    }

    // --- ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ---

    private IEnumerator MoveToTarget(Vector2 targetPosition, InternState stateOnArrival)
    {
        agentMover.SetPath(BuildPathTo(targetPosition));
        yield return new WaitUntil(() => !agentMover.IsMoving());
        SetState(stateOnArrival);
    }
    
    private void SelectNewPatrolPoint() 
    { 
        if (patrolPoints == null || patrolPoints.Count == 0) return;
        currentPatrolTarget = patrolPoints[Random.Range(0, patrolPoints.Count)]; 
    }
    
    private Waypoint DetermineCorrectGoalFor(ClientPathfinding client)
    {
        if (client == null) return null;
        DocumentType doc = client.docHolder.GetCurrentDocumentType();
        ClientGoal goal = client.mainGoal;

        if (client.billToPay > 0) return ClientSpawner.GetCashierZone().waitingWaypoint;
        switch (goal)
        {
            case ClientGoal.GetCertificate1:
                if (doc == DocumentType.None || doc == DocumentType.Form2) return ClientSpawner.Instance.formTable.tableWaypoint;
                if (doc == DocumentType.Form1) return ClientSpawner.GetDesk1Zone().waitingWaypoint;
                if (doc == DocumentType.Certificate1) return ClientSpawner.GetCashierZone().waitingWaypoint;
                break;
            case ClientGoal.GetCertificate2:
                if (doc == DocumentType.None || doc == DocumentType.Form1) return ClientSpawner.Instance.formTable.tableWaypoint;
                if (doc == DocumentType.Form2) return ClientSpawner.GetDesk2Zone().waitingWaypoint;
                if (doc == DocumentType.Certificate2) return ClientSpawner.GetCashierZone().waitingWaypoint;
                break;
            case ClientGoal.PayTax:
                return ClientSpawner.GetCashierZone().waitingWaypoint;
            case ClientGoal.VisitToilet:
                return ClientSpawner.GetToiletZone().waitingWaypoint;
        }
        return ClientQueueManager.Instance.ChooseNewGoal(client);
    }

    private void SetState(InternState newState)
    {
        if (currentState == newState) return;
        currentState = newState;
        logger?.LogState(newState.ToString());
        visuals?.SetEmotionForState(newState);
    }

public InternState GetCurrentState()
{
    return currentState;
}

    // --- РЕАЛИЗАЦИЯ ПОИСКА ПУТИ (A*) ---
    // Этот метод требуется базовым классом
    protected override Queue<Waypoint> BuildPathTo(Vector2 targetPos)
    {
        var path = new Queue<Waypoint>();
        var allWaypoints = FindObjectsByType<Waypoint>(FindObjectsSortMode.None);
        if (allWaypoints.Length == 0) return path;

        Waypoint startNode = FindNearestVisibleWaypoint(transform.position, allWaypoints);
        Waypoint endNode = FindNearestVisibleWaypoint(targetPos, allWaypoints);
        if (startNode == null || endNode == null) return path;
        
        Dictionary<Waypoint, float> distances = new Dictionary<Waypoint, float>();
        Dictionary<Waypoint, Waypoint> previous = new Dictionary<Waypoint, Waypoint>();
        List<Waypoint> unvisited = new List<Waypoint>();

        distances[startNode] = 0;
        
        foreach (var wp in allWaypoints)
        {
            if (wp != startNode)
            {
                distances[wp] = float.MaxValue;
                previous[wp] = null;
            }
            unvisited.Add(wp);
        }

        while (unvisited.Count > 0)
        {
            unvisited.Sort((a, b) => distances[a].CompareTo(distances[b]));
            Waypoint current = unvisited[0];
            unvisited.Remove(current);

            if (current == endNode)
            {
                ReconstructPath(previous, endNode, path);
                return path;
            }

            foreach (var neighbor in current.neighbors)
            {
                if (neighbor == null) continue;
                if (neighbor.forbiddenTags != null && neighbor.forbiddenTags.Contains(gameObject.tag)) continue;

                float alt = distances[current] + Vector2.Distance(current.transform.position, neighbor.transform.position);
                if (alt < distances[neighbor])
                {
                    distances[neighbor] = alt;
                    previous[neighbor] = current;
                }
            }
        }
        return path;
    }

    private void ReconstructPath(Dictionary<Waypoint, Waypoint> previous, Waypoint goal, Queue<Waypoint> path)
    {
        List<Waypoint> pathList = new List<Waypoint>();
        for (Waypoint at = goal; at != null; at = previous.ContainsKey(at) ? previous[at] : null)
        {
            pathList.Add(at);
        }
        pathList.Reverse();
        path.Clear();
        foreach (var wp in pathList)
        {
            path.Enqueue(wp);
        }
    }

    private Waypoint FindNearestVisibleWaypoint(Vector2 position, Waypoint[] wps)
    {
        Waypoint bestWaypoint = null;
        float minDistance = float.MaxValue;
        foreach (var wp in wps)
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
}