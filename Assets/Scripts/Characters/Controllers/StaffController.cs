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
    public bool isReadyForPromotion = false;
    public List<StaffAction> activeActions = new List<StaffAction>();
    
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

    public virtual void StartShift()
    {
        if (isOnDuty) return;
        isOnDuty = true;
        if (startShiftSound != null) AudioSource.PlayClipAtPoint(startShiftSound, transform.position);
        
        if (actionDecisionCoroutine != null) StopCoroutine(actionDecisionCoroutine);
        actionDecisionCoroutine = StartCoroutine(ActionDecisionLoop());
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
        isOnDuty = false;
        
        if (actionDecisionCoroutine != null) StopCoroutine(actionDecisionCoroutine);
        actionDecisionCoroutine = null;

        if (currentExecutor != null) Destroy(currentExecutor);
        currentExecutor = null;
        
        StartCoroutine(GoHomeRoutine());
    }

    // --- НОВЫЙ "МОЗГ" ---
    private IEnumerator ActionDecisionLoop()
{
    Debug.Log($"<color=lime>AI ЗАПУЩЕН</color> для {characterName}");
    while (isOnDuty)
    {
        // 1. КАЖДЫЙ ЦИКЛ проверяем, нет ли экстренных дел.
        StaffAction emergencyAction = FindEmergencyAction();

        if (emergencyAction != null)
        {
            // Если есть "пожар", нужно действовать!
            if (currentExecutor != null) // Если мы сейчас чем-то заняты...
            {
                if (currentExecutor.IsInterruptible) // ... и это дело можно прервать...
                {
                    Debug.LogWarning($"<color=orange>ПРЕРЫВАНИЕ:</color> '{emergencyAction.displayName}' прерывает текущее действие!");
                    Destroy(currentExecutor); // ... то мы уничтожаем текущего исполнителя.
                    currentExecutor = null;
                }
                else
                {
                    // Если текущее дело нельзя прервать (например, мы уже ловим вора), пропускаем цикл.
                    yield return new WaitForSeconds(1f);
                    continue;
                }
            }

            // Если мы свободны (или только что освободились), запускаем экстренное действие.
            if (currentExecutor == null)
            {
                ExecuteAction(emergencyAction);
            }
        }
        else if (currentExecutor == null)
        {
            // 2. Если "пожара" нет и мы свободны, ищем обычную задачу.
            TryToStartStandardAction();
        }

        yield return new WaitForSeconds(1f);
    }
}

private StaffAction FindEmergencyAction()
{
    if (activeActions == null) return null;

    var emergencyActions = activeActions
        .Where(a => a.priority >= 100)
        .OrderByDescending(a => a.priority);

    foreach (var action in emergencyActions)
    {
        if (action.AreConditionsMet(this)) // Для экстренных дел не делаем бросок кубиков, они всегда "успешны", если условия выполнены
        {
            return action; // Возвращаем первое же найденное экстренное дело.
        }
    }
    return null; // Экстренных дел нет.
}

    private bool TryToStartStandardAction()
{
    if (activeActions == null || !activeActions.Any()) return false;

    float bonusPerPosition = 0.05f;

    for (int i = 0; i < activeActions.Count; i++)
    {
        var action = activeActions[i];
        if (action.priority >= 100) continue;

        if (actionCooldowns.ContainsKey(action.actionType) && Time.time < actionCooldowns[action.actionType])
        {
            continue;
        }

        float positionBonus = (activeActions.Count - i) * bonusPerPosition;
        if (action.AreConditionsMet(this) && CheckActionRoll(action, positionBonus))
        {
            return ExecuteAction(action);
        }
    }

    UpdateFrustration(false); // Вызывается только если ни одно стандартное действие не прошло
    return false;
}

// --- НОВЫЙ ВСПОМОГАТЕЛЬНЫЙ МЕТОД, ЧТОБЫ НЕ ДУБЛИРОВАТЬ КОД ---
private bool ExecuteAction(StaffAction actionToExecute)
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
    protected bool CheckActionRoll(StaffAction actionData, float positionBonus) // <-- ИЗМЕНЕНИЕ 4: Переименовали параметр для ясности
{
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