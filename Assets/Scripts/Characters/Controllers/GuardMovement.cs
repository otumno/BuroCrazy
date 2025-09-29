// Файл: Scripts/Characters/Controllers/GuardMovement.cs --- ФИНАЛЬНАЯ ИСПРАВЛЕННАЯ ВЕРСИЯ ---
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(Rigidbody2D), typeof(AgentMover), typeof(CharacterStateLogger))]
public class GuardMovement : StaffController
{
    public enum GuardState { Idle, Patrolling, WaitingAtWaypoint, Chasing, Talking, OnPost, GoingToBreak, OnBreak, GoingToToilet, AtToilet, OffDuty, ChasingThief, EscortingThief, Evicting, StressedOut, WritingReport, OperatingBarrier }
    
    [Header("Состояние Охранника")]
    private GuardState currentState = GuardState.OffDuty;

    [Header("Ссылки на компоненты (Prefab)")]
    public GameObject nightLight; // <<< ВОЗВРАЩЕНО

    // Внутренние переменные, которые заполняются из RoleData
    private float minWaitTime;
    private float maxWaitTime;
    private float chaseSpeedMultiplier;
    private float talkTime;
    private float timeInToilet;
    private float maxStress;
    private float stressGainPerViolator;
    private float stressReliefRate;
    
    private ClientPathfinding currentChaseTarget;
    private Waypoint[] allWaypoints;
    private Coroutine needsCoroutine;
	private int unreportedIncidents = 0;

    public GuardState GetCurrentState() { return currentState; }
    public bool IsAvailableAndOnDuty() { return isOnDuty && currentAction == null; }
    
	public override bool IsOnBreak()
{
    return currentState == GuardState.OnBreak ||
           currentState == GuardState.GoingToBreak || 
           currentState == GuardState.AtToilet || 
           currentState == GuardState.GoingToToilet ||
           currentState == GuardState.StressedOut;
}
	
	public void InterruptWithNewTask(ClientPathfinding target, bool isThief)
    {
        // 1. Если охранник уже что-то делает (например, патрулирует), прерываем это.
        if (currentAction != null)
        {
            StopCoroutine(currentAction);
            Debug.Log($"{characterName} прерывает '{currentAction}' для экстренной задачи!");
        }

        // 2. Немедленно запускаем новую, экстренную задачу.
        if (isThief)
        {
            currentAction = StartCoroutine(CatchThiefRoutine(target));
        }
        else
        {
            currentAction = StartCoroutine(ChaseRoutine(target));
        }
    }
	
    public override string GetStatusInfo()
    {
        switch (currentState)
        {
            case GuardState.StressedOut: return "СОРВАЛСЯ!";
            case GuardState.WritingReport: return "Пишет протокол";
            case GuardState.Patrolling: return "Патрулирует";
            case GuardState.OnPost: return "На посту";
            case GuardState.Chasing: return $"Преследует: {currentChaseTarget?.name}";
            case GuardState.Talking: return $"Разговаривает с: {currentChaseTarget?.name}";
            case GuardState.OffDuty: return "Смена окончена";
            default: return currentState.ToString();
        }
    }

    // --- <<< БЛОК КОМАНД ДЛЯ МЕНЕДЖЕРА ВОЗВРАЩЕН >>> ---
    public void ReturnToPatrol()
    {
        if (!isOnDuty) return;
        if (currentAction != null) StopCoroutine(currentAction);
        currentAction = null; 
    }

    public void AssignToChase(ClientPathfinding target)
    {
        if (!IsAvailableAndOnDuty()) return;
        if (currentAction != null) StopCoroutine(currentAction);
        currentAction = StartCoroutine(ChaseRoutine(target));
    }

    public void AssignToCatchThief(ClientPathfinding target)
    {
        if (!IsAvailableAndOnDuty()) return;
        if (currentAction != null) StopCoroutine(currentAction);
        currentAction = StartCoroutine(CatchThiefRoutine(target));
    }

    public void AssignToEvict(ClientPathfinding target)
    {
        if (!IsAvailableAndOnDuty()) return;
        if (currentAction != null) StopCoroutine(currentAction);
        currentAction = StartCoroutine(EvictRoutine(target));
    }

    public void AssignToOperateBarrier(SecurityBarrier barrier, bool shouldActivate)
    {
        if (!IsAvailableAndOnDuty()) return;
        if (currentAction != null) StopCoroutine(currentAction);
        currentAction = StartCoroutine(OperateBarrierRoutine(barrier, shouldActivate));
    }
    
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

        this.minWaitTime = data.guard_minWaitTime;
        this.maxWaitTime = data.guard_maxWaitTime;
        this.chaseSpeedMultiplier = data.guard_chaseSpeedMultiplier;
        this.talkTime = data.guard_talkTime;
        this.timeInToilet = data.guard_timeInToilet;
        this.maxStress = data.guard_maxStress;
        this.stressGainPerViolator = data.guard_stressGainPerViolator;
        this.stressReliefRate = data.guard_stressReliefRate;
    }
    
public override void StartShift()
{
    base.StartShift(); // <<< УБЕДИСЬ, ЧТО ЭТА СТРОКА ЕСТЬ!
    thoughtBubble?.ShowPriorityMessage("Приступаю к работе.", 2f, Color.green);
    if (needsCoroutine != null) StopCoroutine(needsCoroutine);
    needsCoroutine = StartCoroutine(NeedsCheckRoutine());
}

    public override void EndShift()
    {
        base.EndShift();
        thoughtBubble?.ShowPriorityMessage("Смена окончена.", 2f, Color.yellow);
        if (needsCoroutine != null) StopCoroutine(needsCoroutine);
    }
    
    private IEnumerator NeedsCheckRoutine()
    {
        while (isOnDuty)
        {
            yield return new WaitForSeconds(Random.Range(45f, 90f));
            if (currentAction == null && ScenePointsRegistry.Instance?.staffToiletPoint != null)
            {
                currentAction = StartCoroutine(GoToToiletRoutine());
                yield return new WaitUntil(() => currentAction == null);
            }
        }
    }

    protected override bool CanExecuteActionConditions(ActionType actionType)
{
    switch (actionType)
    {
        case ActionType.OperateBarrier:
            var barrier = GuardManager.Instance.securityBarrier;
            if (barrier == null) return false;
            string currentPeriod = ClientSpawner.CurrentPeriodName;
            bool isNightTime = ClientSpawner.Instance.nightPeriodNames.Any(p => p.Equals(currentPeriod, System.StringComparison.InvariantCultureIgnoreCase));
            if (!isNightTime && barrier.IsActive()) return true;
            if (isNightTime && !barrier.IsActive() && FindObjectsByType<ClientPathfinding>(FindObjectsSortMode.None).Length == 0) return true;
            return false;

        case ActionType.CalmDownViolator:
            return GuardManager.Instance.GetViolatorToHandle() != null;

        case ActionType.CatchThief:
            return GuardManager.Instance.GetThiefToCatch() != null;

        case ActionType.PatrolWaypoint:
            return true; 

        default:
            return base.CanExecuteActionConditions(actionType);
    }
}
    
    protected override IEnumerator ExecuteActionCoroutine(ActionType actionType)
{
    Debug.Log($"<color=yellow>[ЛОГ #3]</color> {characterName}: Исполнитель получил задачу '{actionType}'.");
    var actionData = activeActions.FirstOrDefault(a => a.actionType == actionType);
    if (actionData == null)
    {
        Debug.LogError($"Не найдены данные для действия {actionType}!");
        yield break;
    }

    switch (actionType)
    {
        case ActionType.PatrolWaypoint:
            yield return StartCoroutine(PatrolRoutine(actionData));
            break;

        case ActionType.OperateBarrier:
            var barrier = GuardManager.Instance.securityBarrier;
            bool isNight = ClientSpawner.CurrentPeriodName == "Ночь";
            yield return StartCoroutine(OperateBarrierRoutine(barrier, isNight));
            break;

        case ActionType.CalmDownViolator:
            ClientPathfinding violator = GuardManager.Instance.GetViolatorToHandle();
            if (violator != null)
            {
                GuardManager.Instance.AssignTarget(violator);
                yield return StartCoroutine(ChaseRoutine(violator));
            }
            break;

        case ActionType.CatchThief:
            ClientPathfinding thief = GuardManager.Instance.GetThiefToCatch();
            if (thief != null)
            {
                GuardManager.Instance.AssignTarget(thief);
                yield return StartCoroutine(CatchThiefRoutine(thief));
            }
            break;
            
        case ActionType.EvictClient:
            // (Если ты будешь добавлять эту логику, она будет здесь)
            break;

        default:
            Debug.LogWarning($"Для охранника не реализована корутина-исполнитель для действия: {actionType}");
            break;
    }
    Debug.Log($"<color=red>[ЛОГ #8]</color> {characterName}: Исполнитель ЗАВЕРШИЛ свою работу для задачи '{actionType}'.");
}
    
protected override IEnumerator ExecuteDefaultAction()
{
    // Эта корутина сама установит currentAction = null в конце
    yield return StartCoroutine(GoToPostRoutine()); 
}

    private IEnumerator GoToPostRoutine()
    {
		while (ScenePointsRegistry.Instance == null)
    {
        Debug.LogWarning($"Охранник {characterName} ждет, пока ScenePointsRegistry будет готов...");
        yield return null; // Ждем один кадр
    }
        thoughtBubble?.ShowPriorityMessage("Иду на пост...", 2f);
        var postPoint = ScenePointsRegistry.Instance?.guardPostPoint;
        if (postPoint == null)
        {
            yield return StartCoroutine(PatrolRoutine(null));
            yield break;
        }
        
        SetState(GuardState.OnPost);
        yield return StartCoroutine(MoveToTarget(postPoint.position, GuardState.OnPost));
        thoughtBubble?.ShowPriorityMessage("На посту. Все спокойно.", 3f);
        
        float chillTime = Random.Range(ClientSpawner.Instance.minChillTime, ClientSpawner.Instance.maxChillTime);
        yield return new WaitForSeconds(chillTime); 
        currentAction = null;
    }

    private IEnumerator GoToToiletRoutine()
    {
		while (ScenePointsRegistry.Instance == null)
    {
        Debug.LogWarning($"Охранник {characterName} ждет, пока ScenePointsRegistry будет готов...");
        yield return null; // Ждем один кадр
    }
        thoughtBubble?.ShowPriorityMessage("Нужно отойти.", 2f, Color.yellow);
        SetState(GuardState.GoingToToilet);
        yield return StartCoroutine(MoveToTarget(ScenePointsRegistry.Instance.staffToiletPoint.position, GuardState.AtToilet));
        yield return new WaitForSeconds(timeInToilet);
        SetState(GuardState.Idle);
        currentAction = null;
    }

    private IEnumerator PatrolRoutine(StaffAction patrolActionData)
{
    thoughtBubble?.ShowPriorityMessage("Начинаю патрулирование.", 2f);
    SetState(GuardState.Patrolling);
    var thisPatrolInstance = currentAction; // Запоминаем "себя" для проверки на прерывание

    // --- ВОЗВРАЩАЕМ ЛОГИКУ КОНЕЧНОГО ПАТРУЛЯ ---
    if (patrolActionData != null && patrolActionData.patrolPointsToVisit > 0)
    {
        // --- Патрулирование по количеству точек ---
        for (int i = 0; i < patrolActionData.patrolPointsToVisit; i++)
        {
            if (currentAction != thisPatrolInstance) yield break; // Проверка на прерывание

            var patrolTarget = SelectNewPatrolPoint();
            if (patrolTarget != null)
            {
                yield return StartCoroutine(MoveToTarget(patrolTarget.position, GuardState.WaitingAtWaypoint));
                ExperienceManager.Instance?.GrantXP(this, ActionType.PatrolWaypoint);
                yield return new WaitForSeconds(Random.Range(minWaitTime, maxWaitTime));
            }
            else
            {
                thoughtBubble?.ShowPriorityMessage("Некуда патрулировать!", 2f, Color.red);
                yield return new WaitForSeconds(Random.Range(minWaitTime, maxWaitTime));
                break; 
            }
        }
    }
    else
    {
        // --- Патрулирование по времени (как запасной вариант) ---
        float duration = patrolActionData?.actionDuration ?? 30f;
        float startTime = Time.time;
        while (Time.time < startTime + duration)
        {
            if (currentAction != thisPatrolInstance) yield break; // Проверка на прерывание
            
            // В этой версии он будет просто стоять и ждать, можно добавить движение к 1 точке
            var patrolTarget = SelectNewPatrolPoint();
             if (patrolTarget != null)
            {
                yield return StartCoroutine(MoveToTarget(patrolTarget.position, GuardState.WaitingAtWaypoint));
                ExperienceManager.Instance?.GrantXP(this, ActionType.PatrolWaypoint);
                // Чтобы не зацикливаться на одной точке, ждем до конца таймера
                 while (Time.time < startTime + duration)
                 {
                    if (currentAction != thisPatrolInstance) yield break;
                    yield return null;
                 }
                 break;
            }
        }
		currentAction = null;
    }
    
    thoughtBubble?.ShowPriorityMessage("Патрулирование окончено. Ищу новые задачи.", 2f, Color.gray);
    if (patrolActionData != null)
    {
        actionCooldowns[patrolActionData.actionType] = Time.time + patrolActionData.actionCooldown;
    }
    
    // --- САМАЯ ВАЖНАЯ СТРОКА ---
    // Сообщаем "мозгу", что мы закончили и он может искать новое дело.
    currentAction = null;
}
    
    private Transform SelectNewPatrolPoint()
    {
        var points = ScenePointsRegistry.Instance?.guardPatrolPoints;
        if (points == null || points.Count == 0) return null;
        return points[Random.Range(0, points.Count)];
    }

    public override void GoOnBreak(float duration)
    {
        if (!isOnDuty || currentAction != null) return;
        currentAction = StartCoroutine(BreakRoutine(duration));
    }

    private IEnumerator BreakRoutine(float duration)
    {
		while (ScenePointsRegistry.Instance == null)
    {
        Debug.LogWarning($"Охранник {characterName} ждет, пока ScenePointsRegistry будет готов...");
        yield return null; // Ждем один кадр
    }
        thoughtBubble?.ShowPriorityMessage("Ухожу на обед.", 2f);
        SetState(GuardState.GoingToBreak);
        Transform breakSpot = RequestKitchenPoint();
        if (breakSpot != null)
        {
            yield return StartCoroutine(MoveToTarget(breakSpot.position, GuardState.OnBreak));
            yield return new WaitForSeconds(duration);
            FreeKitchenPoint(breakSpot);
        }
        else { yield return new WaitForSeconds(duration); }
        currentAction = null;
    }
    
    private IEnumerator ChaseRoutine(ClientPathfinding target)
    {
		while (ScenePointsRegistry.Instance == null)
    {
        Debug.LogWarning($"Охранник {characterName} ждет, пока ScenePointsRegistry будет готов...");
        yield return null; // Ждем один кадр
    }
	
        thoughtBubble?.ShowPriorityMessage("Замечен нарушитель! Выдвигаюсь!", 2f, Color.red);
        currentChaseTarget = target;
        SetState(GuardState.Chasing);
        agentMover.ApplySpeedMultiplier(chaseSpeedMultiplier);
        while (currentChaseTarget != null && Vector2.Distance(transform.position, currentChaseTarget.transform.position) > 1.2f)
        {
            agentMover.StartDirectChase(currentChaseTarget.transform.position);
            yield return new WaitForSeconds(0.2f);
        }
        agentMover.StopDirectChase();
        agentMover.ApplySpeedMultiplier(1f);
        if (currentChaseTarget != null)
        {
            agentMover.Stop();
            yield return StartCoroutine(TalkToClientRoutine(currentChaseTarget));
        }
        currentChaseTarget = null;
        currentAction = null;
    }

    private IEnumerator TalkToClientRoutine(ClientPathfinding clientToCalm)
    {
		while (ScenePointsRegistry.Instance == null)
    {
        Debug.LogWarning($"Охранник {characterName} ждет, пока ScenePointsRegistry будет готов...");
        yield return null; // Ждем один кадр
    }
        thoughtBubble?.ShowPriorityMessage("Гражданин, пройдемте...", 3f, Color.blue);
        SetState(GuardState.Talking);
        clientToCalm.Freeze();
        yield return new WaitForSeconds(talkTime);
        if(clientToCalm != null)
        {
			unreportedIncidents++;
            ExperienceManager.Instance?.GrantXP(this, ActionType.CalmDownViolator);
            clientToCalm.UnfreezeAndRestartAI();
            if (Random.value < 0.5f) { clientToCalm.CalmDownAndReturnToQueue(); }
            else { clientToCalm.CalmDownAndLeave(); }
        }
        // <<< ИСПРАВЛЕНО: currentStress заменен на currentFrustration >>>
        currentFrustration += stressGainPerViolator;
    }

    private IEnumerator CatchThiefRoutine(ClientPathfinding thief) 
    {
        SetState(GuardState.ChasingThief);
        yield return StartCoroutine(ChaseRoutine(thief));
        if (currentChaseTarget != null)
        {
            yield return StartCoroutine(EscortThiefToCashierRoutine(currentChaseTarget));
            yield return StartCoroutine(WriteReportRoutine());
        }
        currentAction = null;
    }
    
    private IEnumerator EscortThiefToCashierRoutine(ClientPathfinding thief)
    {
		while (ScenePointsRegistry.Instance == null)
    {
        Debug.LogWarning($"Охранник {characterName} ждет, пока ScenePointsRegistry будет готов...");
        yield return null; // Ждем один кадр
    }
        thoughtBubble?.ShowPriorityMessage("Попался, воришка!", 2f, Color.red);
        SetState(GuardState.EscortingThief);
        thief.Freeze();
        yield return new WaitForSeconds(talkTime / 2);
        
        LimitedCapacityZone cashierZone = ClientSpawner.GetCashierZone();
        if (cashierZone != null)
        {
			unreportedIncidents++;
            thief.stateMachine.StopAllActionCoroutines();
            thief.stateMachine.SetGoal(cashierZone.waitingWaypoint);
            thief.stateMachine.SetState(ClientState.MovingToGoal);
            ExperienceManager.Instance?.GrantXP(this, ActionType.CatchThief);
        }
        // <<< ИСПРАВЛЕНО: currentStress заменен на currentFrustration >>>
        currentFrustration += stressGainPerViolator;
    }

    private IEnumerator EvictRoutine(ClientPathfinding client)
    {
        SetState(GuardState.Evicting);
        yield return StartCoroutine(ChaseRoutine(client));
        if(currentChaseTarget != null)
        {
            yield return StartCoroutine(ConfrontAndEvictRoutine(currentChaseTarget));
            yield return StartCoroutine(WriteReportRoutine());
        }
        currentAction = null;
    }
    
    private IEnumerator ConfrontAndEvictRoutine(ClientPathfinding client)
    {
		while (ScenePointsRegistry.Instance == null)
    {
        Debug.LogWarning($"Охранник {characterName} ждет, пока ScenePointsRegistry будет готов...");
        yield return null; // Ждем один кадр
    }
        thoughtBubble?.ShowPriorityMessage("Прошу покинуть помещение!", 3f, Color.blue);
        SetState(GuardState.Talking);
        client.Freeze();
        yield return new WaitForSeconds(talkTime);
        if (client != null)
        {
            client.ForceLeave(ClientPathfinding.LeaveReason.Angry);
            ExperienceManager.Instance?.GrantXP(this, ActionType.EvictClient);
        }
    }

    private IEnumerator WriteReportRoutine()
    {
		while (ScenePointsRegistry.Instance == null)
    {
        Debug.LogWarning($"Охранник {characterName} ждет, пока ScenePointsRegistry будет готов...");
        yield return null; // Ждем один кадр
    }
        var desk = ScenePointsRegistry.Instance?.guardReportDesk;
        if (desk == null) {
            thoughtBubble?.ShowPriorityMessage("Нужно составить протокол, но где?", 2f, Color.yellow);
            yield break;
        }
        thoughtBubble?.ShowPriorityMessage("Составляю протокол...", 4f);
        SetState(GuardState.WritingReport);
        yield return StartCoroutine(MoveToTarget(desk.position, GuardState.WritingReport));
        yield return new WaitForSeconds(5f);
        DocumentStack stack = desk.GetComponent<DocumentStack>();
        if(stack != null)
        {
             stack.AddDocumentToStack();
			 unreportedIncidents--;
        }
		currentAction = null;
    }
    
    private IEnumerator OperateBarrierRoutine(SecurityBarrier barrier, bool activate) 
    {
		while (ScenePointsRegistry.Instance == null)
    {
        Debug.LogWarning($"Охранник {characterName} ждет, пока ScenePointsRegistry будет готов...");
        yield return null; // Ждем один кадр
    }
        thoughtBubble?.ShowPriorityMessage(activate ? "Закрываю барьер на ночь." : "Открываю барьер.", 2f);
        SetState(GuardState.OperatingBarrier);
        yield return StartCoroutine(MoveToTarget(barrier.guardInteractionPoint.position, GuardState.OperatingBarrier));
        yield return new WaitForSeconds(2.5f);
        if (activate) { barrier.ActivateBarrier(); }
        else { barrier.DeactivateBarrier(); }
        ExperienceManager.Instance?.GrantXP(this, ActionType.OperateBarrier);
        currentAction = null;
    }

    protected override void Awake()
    {
        base.Awake();
        allWaypoints = FindObjectsByType<Waypoint>(FindObjectsSortMode.None);
    }

    protected override void Start()
    {
        base.Start();
        SetState(GuardState.OffDuty);
    }

    private void Update() 
    {
        if(Time.timeScale == 0f || !isOnDuty) return;
        UpdateStress();
    }
    
    private void UpdateStress()
    {
        if (currentState == GuardState.StressedOut || !isOnDuty) return;
        bool isResting = currentState == GuardState.AtToilet || currentState == GuardState.OffDuty || currentState == GuardState.OnBreak;
        if (isResting)
        {
            // <<< ИСПРАВЛЕНО: currentStress заменен на currentFrustration >>>
            currentFrustration -= stressReliefRate * Time.deltaTime;
        }
        // <<< ИСПРАВЛЕНО: currentStress заменен на currentFrustration >>>
        currentFrustration = Mathf.Clamp(currentFrustration, 0, maxStress);
    }

    private void SetState(GuardState newState)
    {
        if (currentState == newState) return;
        currentState = newState;
        logger?.LogState(GetStatusInfo());
        visuals?.SetEmotionForState(newState);
    }

    private IEnumerator MoveToTarget(Vector2 targetPosition, GuardState stateOnArrival)
    {
        agentMover.SetPath(BuildPathTo(targetPosition));
        yield return new WaitUntil(() => !agentMover.IsMoving());
        SetState(stateOnArrival);
    }

    protected override Queue<Waypoint> BuildPathTo(Vector2 targetPos)
    {
        // Теперь здесь всего одна строка!
        return PathfindingUtility.BuildPathTo(transform.position, targetPos, this.gameObject);
    }

    // <<< ИСПРАВЛЕНО: методы Get/SetStressValue теперь работают с currentFrustration >>>
    public override float GetStressValue() { return currentFrustration; }
    public override void SetStressValue(float stress) { currentFrustration = stress; }
}