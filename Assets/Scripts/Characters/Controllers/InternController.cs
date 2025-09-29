// Файл: Scripts/Characters/Controllers/InternController.cs --- ПОЛНАЯ ОБНОВЛЕННАЯ ВЕРСИЯ ---
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
    
    private Transform currentPatrolTarget;
    private StackHolder stackHolder;
    
    // <<< --- НОВЫЙ МЕТОД: Инициализация для соответствия архитектуре --- >>>
    public void InitializeFromData(RoleData data)
    {
        var mover = GetComponent<AgentMover>();
        if (mover != null)
        {
            mover.moveSpeed = data.moveSpeed;
            mover.priority = data.priority;
            mover.idleSprite = data.idleSprite;
            mover.walkSprite1 = data.walkSprite1;
            mover.walkSprite2 = data.walkSprite2;
        }
        
        this.spriteCollection = data.spriteCollection;
        this.stateEmotionMap = data.stateEmotionMap;
        this.visuals?.EquipAccessory(data.accessoryPrefab);
        
        // Для стажера нет уникальных параметров поведения в RoleData,
        // поэтому эта часть остается пустой, но сам метод важен для единообразия.
    }

public override bool IsOnBreak()
{
    return currentState == InternState.OnBreak ||
           currentState == InternState.GoingToBreak ||
           currentState == InternState.AtToilet ||
           currentState == InternState.GoingToToilet;
}

    protected override void Awake()
    {
        base.Awake();
        stackHolder = GetComponent<StackHolder>();
    }

    protected override void Start()
    {
        base.Start();
        // Устанавливаем базовые навыки по умолчанию при первом создании
        if (skills != null)
        {
            skills.paperworkMastery = 0.5f;
            skills.pedantry = 0.25f;
            skills.softSkills = 0.75f;
            skills.corruption = 0.0f;
        }
        SetState(InternState.Inactive);
    }
    
    protected override bool CanExecuteActionConditions(ActionType actionType)
{
    switch (actionType)
    {
        case ActionType.HelpConfusedClient:
            return ClientPathfinding.FindClosestConfusedClient(transform.position) != null;
        case ActionType.CoverDesk:
            return ClientSpawner.GetAbsentClerk() != null;
    }
    return false;
}
protected override IEnumerator ExecuteDefaultAction()
{
    // Запускаем патрулирование как действие по умолчанию и ждем его завершения
    yield return StartCoroutine(PatrolRoutine());
}
    
    protected override IEnumerator ExecuteActionCoroutine(ActionType actionType)
    {
        switch (actionType)
        {
            case ActionType.HelpConfusedClient:
                var confusedClient = ClientPathfinding.FindClosestConfusedClient(transform.position);
                if (confusedClient != null)
                {
                    yield return StartCoroutine(HelpConfusedClientRoutine(confusedClient));
                }
                break;
            case ActionType.CoverDesk:
                var absentClerk = ClientSpawner.GetAbsentClerk();
                if (absentClerk != null) 
                {
                    yield return StartCoroutine(CoverDeskRoutine(absentClerk));
                }
                break;
        }
		currentAction = null;
    }

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
        currentAction = null;
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
        yield return new WaitUntil(() => clerk == null || !clerk.IsOnBreak());
        ExperienceManager.Instance?.GrantXP(this, ActionType.CoverDesk);
        currentAction = null;
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
        currentAction = null;
    }

    public override void GoOnBreak(float duration)
    {
        // Для стажеров перерыв может быть реализован как патруль или ничегонеделание
        Debug.Log($"{characterName} (стажер) уходит на перерыв.");
        currentAction = StartCoroutine(PatrolRoutine()); // Например, просто патрулирует
    }

    private IEnumerator MoveToTarget(Vector2 targetPosition, InternState stateOnArrival)
    {
        agentMover.SetPath(PathfindingUtility.BuildPathTo(transform.position, targetPosition, this.gameObject));
        yield return new WaitUntil(() => !agentMover.IsMoving());
        SetState(stateOnArrival);
    }
    
    private void SelectNewPatrolPoint() 
    { 
        var points = ScenePointsRegistry.Instance?.internPatrolPoints;
        if (points == null || !points.Any()) return;
        currentPatrolTarget = points[Random.Range(0, points.Count)];
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
        logger?.LogState(GetStatusInfo());
        visuals?.SetEmotionForState(newState);
    }

    public InternState GetCurrentState()
    {
        return currentState;
    }
    
    public override string GetStatusInfo()
    {
        return currentState.ToString();
    }
}