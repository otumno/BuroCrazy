// Файл: Scripts/Characters/Controllers/ServiceWorkerController.cs --- ПОЛНАЯ ОБНОВЛЕННАЯ ВЕРСЯ ---
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(AgentMover), typeof(CharacterStateLogger))]
public class ServiceWorkerController : StaffController
{
    public enum WorkerState { Idle, SearchingForWork, GoingToMess, Cleaning, GoingToBreak, OnBreak, GoingToToilet, AtToilet, OffDuty, StressedOut, Patrolling }
    
    [Header("Настройки Уборщика")]
    private WorkerState currentState = WorkerState.OffDuty;

    [Header("Дополнительные объекты (Prefab)")]
    public GameObject nightLight; // <<< ОСТАВЛЕНО: Это уникальная ссылка на объект
    public Transform broomTransform; // <<< ОСТАВЛЕНО: Уникальная ссылка на объект

    // <<< --- ИЗМЕНЕНИЕ: Все поля [SerializeField] и public УДАЛЕНЫ отсюда --- >>>
    // Теперь это просто внутренние переменные, которые заполняются из RoleData
    private float cleaningTimeTrash;
    private float cleaningTimePuddle;
    private float cleaningTimePerDirtLevel;
    private float maxStress;
    private float stressGainPerMess;
    private float stressReliefRate;
    
    private Quaternion initialBroomRotation;
    private Waypoint[] allWaypoints;

    public WorkerState GetCurrentState() => currentState;
    
    // <<< --- ИЗМЕНЕНИЕ: Главный метод инициализации --- >>>
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

        this.cleaningTimeTrash = data.worker_cleaningTimeTrash;
        this.cleaningTimePuddle = data.worker_cleaningTimePuddle;
        this.cleaningTimePerDirtLevel = data.worker_cleaningTimePerDirtLevel;
        this.maxStress = data.worker_maxStress;
        this.stressGainPerMess = data.worker_stressGainPerMess;
        this.stressReliefRate = data.worker_stressReliefRate;
    }

	public override bool IsOnBreak()
{
    return currentState == WorkerState.OnBreak ||
           currentState == WorkerState.GoingToBreak ||
           currentState == WorkerState.AtToilet ||
           currentState == WorkerState.GoingToToilet ||
           currentState == WorkerState.StressedOut;
}

    protected override void Awake()
    {
        base.Awake();
        allWaypoints = FindObjectsByType<Waypoint>(FindObjectsSortMode.None);
    }

    protected override void Start()
    {
        base.Start();
        if (broomTransform != null)
        {
            initialBroomRotation = broomTransform.localRotation;
        }
        SetState(WorkerState.OffDuty);
    }
    
    // Логика стресса теперь в базовом классе и в GoAndCleanRoutine
    // void Update() { ... } убран

    protected override bool CanExecuteActionConditions(ActionType actionType)
{
    MessPoint.MessType messTypeToFind;
    switch (actionType)
    {
        case ActionType.CleanTrash:
            messTypeToFind = MessPoint.MessType.Trash;
            break;
        case ActionType.CleanPuddle:
            messTypeToFind = MessPoint.MessType.Puddle;
            break;
        case ActionType.CleanDirt:
            messTypeToFind = MessPoint.MessType.Dirt;
            break;
        default:
            return false;
    }
    return FindBestMessToClean(messTypeToFind) != null;
}

protected override IEnumerator ExecuteDefaultAction()
{
    // Уборщик, не найдя мусора, будет патрулировать свою зону
    yield return StartCoroutine(PatrolRoutine());
}

    protected override IEnumerator ExecuteActionCoroutine(ActionType actionType)
    {
        MessPoint.MessType messTypeToFind;
        switch (actionType)
        {
            case ActionType.CleanTrash: messTypeToFind = MessPoint.MessType.Trash; break;
            case ActionType.CleanPuddle: messTypeToFind = MessPoint.MessType.Puddle; break;
            case ActionType.CleanDirt: messTypeToFind = MessPoint.MessType.Dirt; break;
            default: yield break;
        }

        var targetMess = FindBestMessToClean(messTypeToFind);
        if (targetMess != null)
        {
            yield return StartCoroutine(GoAndCleanRoutine(targetMess));
        }
    }

    private IEnumerator GoAndCleanRoutine(MessPoint targetMess)
    {
        SetState(WorkerState.GoingToMess);
        yield return StartCoroutine(MoveToTarget(targetMess.transform.position, WorkerState.Cleaning));

        if (targetMess == null) 
        { 
            currentAction = null;
            yield break;
        }
        
        float cleaningTime = GetCleaningTime(targetMess);
        StartCoroutine(AnimateBroom(cleaningTime));
        yield return new WaitForSeconds(cleaningTime);

        if (targetMess != null)
        {
            ExperienceManager.Instance?.GrantXP(this, GetActionTypeFromMess(targetMess.type));
            // <<< ИСПРАВЛЕНО: currentStress заменен на currentFrustration >>>
            currentFrustration += stressGainPerMess;
            Destroy(targetMess.gameObject);
        }

        currentAction = null;
    }
    
    private IEnumerator PatrolRoutine()
    {
        SetState(WorkerState.Patrolling);
        var patrolTarget = ScenePointsRegistry.Instance?.janitorPatrolPoints.FirstOrDefault();
        if (patrolTarget != null)
        {
            yield return StartCoroutine(MoveToTarget(patrolTarget.position, WorkerState.Idle));
        }
        yield return new WaitForSeconds(Random.Range(3f, 6f));
        currentAction = null;
    }

    public override void GoOnBreak(float duration)
    {
        if (!isOnDuty || currentAction != null) return;
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
            yield return new WaitForSeconds(duration);
        }
        currentAction = null;
    }
    
    private MessPoint FindBestMessToClean(MessPoint.MessType type)
    {
        return MessManager.Instance?.GetSortedMessList(transform.position)
            .FirstOrDefault(m => m.type == type);
    }

    private float GetCleaningTime(MessPoint mess)
    {
        switch (mess.type)
        {
            case MessPoint.MessType.Trash: return cleaningTimeTrash;
            case MessPoint.MessType.Puddle: return cleaningTimePuddle;
            case MessPoint.MessType.Dirt: return cleaningTimePerDirtLevel * mess.dirtLevel;
            default: return 1f;
        }
    }
    
    private ActionType GetActionTypeFromMess(MessPoint.MessType messType)
    {
        switch (messType)
        {
            case MessPoint.MessType.Trash: return ActionType.CleanTrash;
            case MessPoint.MessType.Puddle: return ActionType.CleanPuddle;
            case MessPoint.MessType.Dirt: return ActionType.CleanDirt;
            default: return ActionType.CleanTrash;
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

    private IEnumerator StressedOutRoutine()
    {
        SetState(WorkerState.StressedOut);
        Transform breakSpot = RequestKitchenPoint();
        if (breakSpot != null)
        {
            yield return StartCoroutine(MoveToTarget(breakSpot.position, WorkerState.StressedOut));
            yield return new WaitForSeconds(20f);
            FreeKitchenPoint(breakSpot);
        }
        else
        {
            yield return new WaitForSeconds(20f);
        }
        // <<< ИСПРАВЛЕНО: currentStress заменен на currentFrustration >>>
        currentFrustration = maxStress * 0.5f;
        currentAction = null;
    }

    private IEnumerator MoveToTarget(Vector2 targetPosition, WorkerState stateOnArrival)
    {
        agentMover.SetPath(PathfindingUtility.BuildPathTo(transform.position, targetPosition, this.gameObject));
        yield return new WaitUntil(() => !agentMover.IsMoving());
        SetState(stateOnArrival);
    }

    private void SetState(WorkerState newState)
    {
        if (currentState == newState) return;
        currentState = newState;
        logger.LogState(GetStatusInfo());
        visuals?.SetEmotionForState(newState);
    }
    
    // <<< ИСПРАВЛЕНО: методы Get/SetStressValue теперь работают с currentFrustration >>>
    public override float GetStressValue() { return currentFrustration; }
    public override void SetStressValue(float stress) { currentFrustration = stress; }
}