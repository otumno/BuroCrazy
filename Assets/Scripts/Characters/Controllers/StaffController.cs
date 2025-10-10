// Файл: Assets/Scripts/Characters/Controllers/StaffController.cs
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public abstract class StaffController : MonoBehaviour
{
    public enum Role { Unassigned, Intern, Registrar, Cashier, Archivist, Guard, Janitor, Clerk }

    [Header("График работы")]
    public List<string> workPeriods = new List<string>();

    [Header("Звуки смены")]
    public AudioClip startShiftSound;
    public AudioClip endShiftSound;

    [Header("Прогрессия и Роль")]
    public string characterName = "Безымянный";
    public Gender gender;
    public CharacterSkills skills;
    public Role currentRole = Role.Intern;
    public int rank = 0;
    public int experiencePoints = 0;
    public int salaryPerPeriod = 15;
	
	[Header("Система зарплаты")]
	public int unpaidPeriods = 0; // Счетчик неоплаченных смен
	public int missedPaymentCount = 0; // Счетчик промахов с получением зарплаты
	
    public bool isReadyForPromotion = false;
    public List<StaffAction> activeActions = new List<StaffAction>();
	public ServicePoint assignedWorkstation;
    
    [Header("Настройки Выгорания")]
    [Tooltip("Базовое значение, на которое увеличивается выгорание за успешный рабочий цикл.")]
    [SerializeField] private float baseFrustrationGain = 0.1f;
    [Tooltip("Навык, который снижает скорость выгорания (например, Усидчивость).")]
    [SerializeField] private SkillType frustrationResistanceSkill = SkillType.SedentaryResilience;

    // --- ИЗМЕНЕНО: Эти поля теперь protected, чтобы наследники имели к ним доступ ---
    protected float currentFrustration = 0f;
    protected Dictionary<ActionType, float> actionCooldowns = new Dictionary<ActionType, float>();

    // --- ИЗМЕНЕНО: Ссылки на компоненты теперь инициализируются в Awake и доступны наследникам ---
    public EmotionSpriteCollection spriteCollection;
    public StateEmotionMap stateEmotionMap;
    protected bool isOnDuty = false;
    protected AgentMover agentMover;
	public AgentMover AgentMover => agentMover;
    protected CharacterStateLogger logger;
    protected CharacterVisuals visuals;
    public ThoughtBubbleController thoughtBubble { get; private set; }

    // --- НОВОЕ: Управление "мозгом" и "исполнителем" ---
    private Coroutine actionDecisionCoroutine;
    protected ActionExecutor currentExecutor;

    // --- ОСНОВНЫЕ МЕТОДЫ ---
    public bool IsOnDuty() => isOnDuty;
    public abstract bool IsOnBreak();
    public virtual string GetStatusInfo() => "Статус не определен";
	
	public virtual string GetCurrentStateName()
	{
		return "Unknown"; // Базовая реализация
	}

public void ForceInitializeBaseComponents(AgentMover am, CharacterVisuals cv, CharacterStateLogger csl)
{
    agentMover = am;
    visuals = cv;
    logger = csl;
    thoughtBubble = GetComponent<ThoughtBubbleController>();
}


	public virtual IEnumerator MoveToTarget(Vector2 targetPosition, string stateOnArrival)
	{
		// Эта базовая реализация просто двигает персонажа, не меняя его состояние.
		// Каждый наследник (Clerk, Intern) должен будет переопределить этот метод.
		agentMover.SetPath(PathfindingUtility.BuildPathTo(transform.position, targetPosition, this.gameObject));
		yield return new WaitUntil(() => !agentMover.IsMoving());
	}

    public virtual void StartShift()
{
    if (isOnDuty) return;
    isOnDuty = true;
    if (startShiftSound != null) AudioSource.PlayClipAtPoint(startShiftSound, transform.position);

    // --- НАЧАЛО ИЗМЕНЕНИЙ: НОВАЯ ЛОГИКА НАЧАЛА СМЕНЫ ---

    // Останавливаем все предыдущие "мозговые" корутины
    if (actionDecisionCoroutine != null) StopCoroutine(actionDecisionCoroutine);

    // Запускаем новую главную корутину смены
    actionDecisionCoroutine = StartCoroutine(ShiftRoutine());
}

/// <summary>
/// Новая главная корутина, управляющая всей сменой сотрудника.
/// </summary>
private IEnumerator ShiftRoutine()
{
    // 1. ПЕРВЫЙ ЭТАП: Идти на назначенное рабочее место (если оно есть)
    if (assignedWorkstation != null)
    {
        // Используем уже существующий исполнитель, но вызываем его напрямую
        var goToWorkExecutor = gameObject.AddComponent<GoToWorkstationExecutor>();
        goToWorkExecutor.Execute(this, null);

        // Ждем, пока исполнитель не самоуничтожится (т.е. пока сотрудник не дойдет до места)
        yield return new WaitUntil(() => goToWorkExecutor == null);
    }

    // 2. ВТОРОЙ ЭТАП: Запускаем стандартный цикл принятия решений ("мозг")
    // Этот код мы перенесли из старого ActionDecisionLoop
    Debug.Log($"<color=lime>AI ЗАПУЩЕН</color> для {characterName}");
    while (isOnDuty)
    {
        if (currentExecutor == null)
        {
            ActionStartResult result = TryToStartConfiguredAction();
            if (result == ActionStartResult.NoActionsAvailable)
            {
                currentExecutor = GetIdleActionExecutor();
                if (currentExecutor != null) currentExecutor.Execute(this, null);
            }
            else if (result == ActionStartResult.AllActionsFailed)
            {
                currentExecutor = GetBurnoutActionExecutor();
                if (currentExecutor != null) currentExecutor.Execute(this, null);
            }
        }
        yield return new WaitForSeconds(1f);
    }
}


public float GetCurrentFrustration()
{
    return currentFrustration;
}

public virtual void Initialize(RoleData data)
    {
        // Убеждаемся, что ссылки на компоненты получены
        if (visuals == null) visuals = GetComponent<CharacterVisuals>();
        if (agentMover == null) agentMover = GetComponent<AgentMover>();

        this.currentRole = data.roleType;

        // 1. Полная настройка визуала (тело, лицо, эмоции, аксессуар)
        if (visuals != null)
        {
            visuals.SetupFromRoleData(data, this.gender);
        }

        // 2. Настройка анимации ходьбы
        if (agentMover != null)
        {
            agentMover.SetAnimationSprites(data.idleSprite, data.walkSprite1, data.walkSprite2);
            agentMover.moveSpeed = data.moveSpeed;
            agentMover.priority = data.priority;
        }

        // 3. Настройка звуков (когда мы добавим их в RoleData)
        // this.startShiftSound = data.startShiftSound;
        // this.endShiftSound = data.endShiftSound;
    }

public void SetCurrentFrustration(float value)
{
    currentFrustration = Mathf.Clamp01(value);
}

    public virtual void EndShift()
{
    if (!isOnDuty) return;
    Debug.Log($"<color=orange>КОНЕЦ СМЕНЫ:</color> Запущена процедура для {characterName}.");
    isOnDuty = false;

    // --- НАЧАЛО ИЗМЕНЕНИЙ: Принудительное прерывание ---

    // 1. Немедленно останавливаем "мозг", чтобы он не начал новое действие.
    if (actionDecisionCoroutine != null)
    {
        StopCoroutine(actionDecisionCoroutine);
        actionDecisionCoroutine = null;
    }

    // 2. Если сотрудник сейчас чем-то занят (currentExecutor существует),
    //    мы принудительно уничтожаем этот компонент-исполнитель.
    if (currentExecutor != null)
    {
        Debug.LogWarning($"{characterName} был занят '{currentExecutor.GetType().Name}', но его смена закончилась. Действие принудительно прервано.");
        Destroy(currentExecutor);
        currentExecutor = null;
    }

    // --- КОНЕЦ ИЗМЕНЕНИЙ ---

    // 3. Теперь, когда сотрудник гарантированно ничем не занят, отправляем его домой.
    StartCoroutine(GoHomeRoutine());
}

// --- НАЧАЛО ФИНАЛЬНОЙ ВЕРСИИ "МОЗГА" AI ---

    // Новый enum для четкого понимания результата цикла
    protected enum ActionStartResult { Success, NoActionsAvailable, AllActionsFailed }

    private IEnumerator ActionDecisionLoop()
    {
        Debug.Log($"<color=lime>AI ЗАПУЩЕН</color> для {characterName}");
        while (isOnDuty)
        {
            if (currentExecutor == null)
            {
                ActionStartResult result = TryToStartConfiguredAction();

                if (result == ActionStartResult.NoActionsAvailable)
                {
                    Debug.Log($"<color=gray>{characterName} не имеет доступных задач. Запускаю действие 'Бездействие'.</color>");
                    currentExecutor = GetIdleActionExecutor();
                    if (currentExecutor != null) currentExecutor.Execute(this, null);
                }
                else if (result == ActionStartResult.AllActionsFailed)
                {
                    Debug.Log($"<color=orange>{characterName} провалил все проверки. Запускаю действие 'Выгорание'.</color>");
                    currentExecutor = GetBurnoutActionExecutor();
                    if (currentExecutor != null) currentExecutor.Execute(this, null);
                }
            }
            yield return new WaitForSeconds(1f);
        }
    }

    protected virtual ActionStartResult TryToStartConfiguredAction()
    {
        if (activeActions == null || !activeActions.Any()) return ActionStartResult.NoActionsAvailable;

        var emergencyActions = activeActions.Where(a => a.priority >= 100).OrderByDescending(a => a.priority);
        foreach (var action in emergencyActions)
        {
            if (action.AreConditionsMet(this))
            {
                if (ExecuteAction(action)) return ActionStartResult.Success;
            }
        }

        bool anyActionWasPossible = false;
        float bonusPerPosition = 0.05f;
        for (int i = 0; i < activeActions.Count; i++)
        {
            var action = activeActions[i];
            if (action.priority >= 100) continue;
            if (actionCooldowns.ContainsKey(action.actionType) && Time.time < actionCooldowns[action.actionType]) continue;

            if (action.AreConditionsMet(this))
            {
                anyActionWasPossible = true;
                if (CheckActionRoll(action, (activeActions.Count - i) * bonusPerPosition))
                {
                    if (ExecuteAction(action)) return ActionStartResult.Success;
                }
            }
        }

        if (anyActionWasPossible)
            return ActionStartResult.AllActionsFailed;
        else
            return ActionStartResult.NoActionsAvailable;
    }

    protected virtual ActionExecutor GetIdleActionExecutor()
    {
        return null;
    }

    protected virtual ActionExecutor GetBurnoutActionExecutor()
    {
        return gameObject.AddComponent<DefaultActionExecutor>();
    }

    public bool ExecuteAction(StaffAction actionToExecute)
    {
        Debug.Log($"<color=green>AI РЕШЕНИЕ:</color> {characterName} будет выполнять '{actionToExecute.displayName}' (Приоритет: {actionToExecute.priority}).");
        UpdateFrustration(true);
        
        System.Type executorType = actionToExecute.GetExecutorType();
        currentExecutor = gameObject.AddComponent(executorType) as ActionExecutor;
        
        if (currentExecutor != null)
        {
            currentExecutor.Execute(this, actionToExecute);
            actionCooldowns[actionToExecute.actionType] = Time.time + actionToExecute.actionCooldown;
            return true;
        }
        else
        {
            Debug.LogError($"Не удалось добавить компонент-исполнитель типа '{executorType.Name}' для действия '{actionToExecute.displayName}'!");
            return false;
        }
    }

    // --- НОВЫЙ ПУБЛИЧНЫЙ МЕТОД, который вызывает "исполнитель" по завершении ---
    public void OnActionFinished()
    {
        this.currentExecutor = null;
    }
    
    protected virtual void Awake()
    {
        agentMover = GetComponent<AgentMover>();
        logger = GetComponent<CharacterStateLogger>();
        visuals = GetComponent<CharacterVisuals>();
        thoughtBubble = GetComponent<ThoughtBubbleController>();
    }

    #region Служебные методы (без изменений)
    protected bool CheckActionRoll(StaffAction actionData, float positionBonus)
{
    // >>> НАЧАЛО ИЗМЕНЕНИЙ <<<
    // ОСОБОЕ ПРАВИЛО: Если это Директор, он всегда успешен.
    if (this is DirectorAvatarController)
    {
        Debug.Log($"[ПРОВЕРКА: {actionData.displayName}] Пропускается для Директора. <color=green>АВТО-УСПЕХ</color>");
        return true;
    }
    // >>> КОНЕЦ ИЗМЕНЕНИЙ <<<

    if (actionData == null) return false;
    
    float skillModifier = 0f;
    skillModifier += skills.GetSkillValue(actionData.primarySkill.skill) * actionData.primarySkill.strength * (actionData.primarySkill.isPositiveEffect ? 1f : -1f);
    if (actionData.useSecondarySkill)
    {
        skillModifier += skills.GetSkillValue(actionData.secondarySkill.skill) * actionData.secondarySkill.strength * (actionData.secondarySkill.isPositiveEffect ? 1f : -1f);
    }
    
    // --- ИЗМЕНЕНИЕ 5: Используем positionBonus в расчете ---
    float initialChance = actionData.baseSuccessChance + skillModifier - currentFrustration + positionBonus;
    float finalChance = Mathf.Clamp(initialChance, actionData.minSuccessChance, actionData.maxSuccessChance);
    float roll = UnityEngine.Random.value;
    
    // В лог тоже добавили новый бонус для наглядности
    string logMessage = $"[ПРОВЕРКА: {actionData.displayName}] База:{actionData.baseSuccessChance:P0} + Навыки:{skillModifier:P0} - Выгорание:{currentFrustration:P0} + БонусПозиции:{positionBonus:P0} = {initialChance:P0} (Итог: {finalChance:P0}). Бросок: {roll:P0}.";
    
    if (roll <= finalChance)
    {
        logMessage += " <color=green>УСПЕХ</color>";
        Debug.Log(logMessage);
        return true;
    }
    else
    {
        logMessage += " <color=orange>ПРОВАЛ</color>";
        Debug.Log(logMessage);
        return false;
    }
}

    protected void UpdateFrustration(bool wasCycleSuccessful)
    {
        float oldFrustration = currentFrustration;
        if (wasCycleSuccessful)
        {
            float resistance = skills.GetSkillValue(frustrationResistanceSkill);
            float frustrationGain = baseFrustrationGain * (1f - resistance * 0.5f);
            currentFrustration = Mathf.Clamp01(currentFrustration + frustrationGain);
            Debug.Log($"<color=yellow>ИТОГ ЦИКЛА:</color> Успех. Выгорание: {oldFrustration:P0} -> {currentFrustration:P0} (+{frustrationGain:P0})");
        }
        else
        {
            Debug.Log($"<color=gray>ИТОГ ЦИКЛА:</color> Провал/Бездействие. Выгорание без изменений.");
        }
    }
    
    private IEnumerator GoHomeRoutine()
{
    // --- НАЧАЛО НОВОЙ ЛОГИКИ ЗАРПЛАТЫ ---

    // 1. Проверяем, есть ли у нас неоплаченные смены
    if (unpaidPeriods > 0)
    {
        var salaryStack = ScenePointsRegistry.Instance?.salaryStackPoint;
        if (salaryStack != null)
        {
            // 2. Идем к столу выдачи зарплаты
            thoughtBubble?.ShowPriorityMessage("За зарплатой...", 3f, Color.yellow);
            AgentMover.SetPath(PathfindingUtility.BuildPathTo(transform.position, salaryStack.transform.position, this.gameObject));
            yield return new WaitUntil(() => !AgentMover.IsMoving());

            // 3. Пытаемся забрать конверт
            if (salaryStack.TakeOneEnvelope())
            {
                // УСПЕХ! Конверт получен.
                int salaryToPay = salaryPerPeriod * unpaidPeriods;

                // Проверяем, есть ли у игрока деньги
                if (PlayerWallet.Instance.GetCurrentMoney() >= salaryToPay)
                {
                    PlayerWallet.Instance.AddMoney(-salaryToPay, $"Зарплата: {characterName}");
                    Debug.Log($"{characterName} получил зарплату: ${salaryToPay} за {unpaidPeriods} смен.");
                    unpaidPeriods = 0;
                    missedPaymentCount = 0; // Сбрасываем счетчик промахов
                    thoughtBubble?.ShowPriorityMessage("Отлично!", 2f, Color.green);
                }
                else
                {
                    // Денег в кассе нет! Возвращаем конверт, промах засчитан.
                    salaryStack.AddEnvelope(); 
                    missedPaymentCount++;
                    Debug.LogWarning($"{characterName} не смог получить зарплату: в казне нет денег! Промах #{missedPaymentCount}");
                    thoughtBubble?.ShowPriorityMessage("В казне пусто?!", 3f, Color.red);
                }
            }
            else
            {
                // ПРОВАЛ! Конвертов в стопке нет.
                thoughtBubble?.ShowPriorityMessage("Где мой конверт?!", 4f, Color.red);
                yield return new WaitForSeconds(10f); // Ждем 10 секунд в гневе
                missedPaymentCount++;
                Debug.LogWarning($"{characterName} не нашел свой конверт! Промах #{missedPaymentCount}");
            }

            // 4. Проверяем, не пора ли увольняться
            if (missedPaymentCount >= 2)
            {
                Debug.LogError($"СОТРУДНИК УВОЛИЛСЯ! {characterName} не получил зарплату два раза подряд. Директор получает страйк!");
                DirectorManager.Instance?.AddStrike();
                FireAndGoHome(); // Запускаем увольнение
                yield break; // Прерываем дальнейшее выполнение
            }
        }
    }

    // --- КОНЕЦ НОВОЙ ЛОГИКИ ЗАРПЛАТЫ ---

    // 5. Стандартная логика: идем домой
    var homeZone = ScenePointsRegistry.Instance?.staffHomeZone;
    if (homeZone != null)
    {
        Vector2 targetPos = homeZone.GetRandomPointInside();
        agentMover.SetPath(PathfindingUtility.BuildPathTo(transform.position, targetPos, this.gameObject));
        yield return new WaitUntil(() => !agentMover.IsMoving());
        if (endShiftSound != null) AudioSource.PlayClipAtPoint(endShiftSound, transform.position);
    }
}
	
	public void FireAndGoHome()
    {
        isOnDuty = false;
        if (actionDecisionCoroutine != null)
        {
            StopCoroutine(actionDecisionCoroutine);
            actionDecisionCoroutine = null;
        }
        if (currentExecutor != null)
        {
            Destroy(currentExecutor);
            currentExecutor = null;
        }
        StartCoroutine(GoHomeAndDespawnRoutine());
    }

    private IEnumerator GoHomeAndDespawnRoutine()
    {
        yield return StartCoroutine(GoHomeRoutine());
        Destroy(gameObject);
    }
	
    #endregion
}