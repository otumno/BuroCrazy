// Файл: StaffController.cs --- ПОЛНАЯ ИСПРАВЛЕННАЯ ВЕРСИЯ ---
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public abstract class StaffController : MonoBehaviour
{
    public enum Role { Unassigned, Intern, Registrar, Cashier, Archivist, Guard, Janitor, Clerk }
    
    [Header("График работы")]
    public List<string> workPeriods = new List<string>();
    [Header("Патрулирование (для ролей, где это применимо)")]
    public List<Transform> patrolPoints;

    [Header("Стандартные точки")]
    public Transform homePoint;
    public List<Transform> kitchenPoints;
    public Transform staffToiletPoint;

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

	protected float currentFrustration = 0f;
	protected Dictionary<ActionType, float> actionCooldowns = new Dictionary<ActionType, float>();

	public EmotionSpriteCollection spriteCollection;
    public StateEmotionMap stateEmotionMap;

    // --- Защищенные (protected) поля для дочерних классов ---
    protected bool isOnDuty = false;
    protected Coroutine currentAction;
    protected AgentMover agentMover;
    protected CharacterStateLogger logger;
    protected CharacterVisuals visuals;
	protected ThoughtBubbleController thoughtBubble;

    private static List<Transform> occupiedKitchenPoints = new List<Transform>();
    private bool isInitialized = false;

    // --- ОСНОВНЫЕ МЕТОДЫ УПРАВЛЕНИЯ ---

    public bool IsOnDuty() => isOnDuty;
	public bool IsIdle => currentAction == null;
	
	public abstract bool IsOnBreak();

	public virtual string GetStatusInfo()
    {
        return "Статус не определен";
    }

    public virtual void StartShift()
    {
        if (isOnDuty) return;
        isOnDuty = true;
        if (startShiftSound != null) AudioSource.PlayClipAtPoint(startShiftSound, transform.position);
        
        // Запускаем "мозг" сотрудника
        currentAction = StartCoroutine(ActionDecisionLoop());
    }

protected virtual bool CanExecuteActionConditions(ActionType actionType)
{
    // Базовая реализация, которую могут использовать простые действия
    return true;
}

protected bool CheckActionRoll(StaffAction actionData, float priorityBonus)
{
    if (actionData == null) return false;
    
    float skillModifier = 0f;
    skillModifier += skills.GetSkillValue(actionData.primarySkill.skill) * actionData.primarySkill.strength * (actionData.primarySkill.isPositiveEffect ? 1f : -1f);
    if (actionData.useSecondarySkill)
    {
        skillModifier += skills.GetSkillValue(actionData.secondarySkill.skill) * actionData.secondarySkill.strength * (actionData.secondarySkill.isPositiveEffect ? 1f : -1f);
    }
    
    float initialChance = actionData.baseSuccessChance + skillModifier - currentFrustration + priorityBonus;
    float finalChance = Mathf.Clamp(initialChance, actionData.minSuccessChance, actionData.maxSuccessChance);
    float roll = Random.value;
    
    string logMessage = $"[ПРОВЕРКА: {actionData.displayName}] База:{actionData.baseSuccessChance:P0} + Навыки:{skillModifier:P0} - Выгорание:{currentFrustration:P0} + Бонус:{priorityBonus:P0} = {initialChance:P0} (Итог: {finalChance:P0}). Бросок: {roll:P0}.";
    
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



    public virtual void EndShift()
    {
        if (!isOnDuty) return;
        isOnDuty = false;
        
        // Останавливаем "мозг"
        if (currentAction != null)
        {
            StopCoroutine(currentAction);
            currentAction = null;
        }
        
        // Запускаем процесс ухода домой (но не удаления)
        StartCoroutine(GoHomeRoutine());
    }
    
	public void GoHome()
{
    // Эта команда не проверяет, на смене ли сотрудник.
    // Она просто прерывает любое текущее действие и отправляет его домой.
    if (currentAction != null)
    {
        StopCoroutine(currentAction);
        currentAction = null;
    }
    StartCoroutine(GoHomeRoutine());
}
	
    public void FireAndGoHome()
    {
        isOnDuty = false;
        if (currentAction != null)
        {
            StopCoroutine(currentAction);
            currentAction = null;
        }
        StartCoroutine(GoHomeAndDespawnRoutine());
    }
    
    // --- "МОЗГ" AI (УНИВЕРСАЛЬНЫЙ ДВИЖОК ПОВЕДЕНИЯ) ---

private IEnumerator ActionDecisionLoop()
{
    Debug.Log($"<color=lime>AI ЗАПУЩЕН</color> для {characterName}");
    while (isOnDuty)
    {
        if (currentAction == null)
        {
            StaffAction chosenAction = null;
            
            // --- ЭТАП 2: Проверка списка рутинных действий по приоритету ---
            if (chosenAction == null && activeActions.Any())
            {
                // --- ИЗМЕНЕНИЕ: Добавляем цикл с индексом для расчета бонуса ---
                for (int i = 0; i < activeActions.Count; i++)
                {
                    var action = activeActions[i];
                    
                    // Рассчитываем бонус: чем выше действие в списке (меньше i), тем больше бонус.
                    // Максимальный бонус 20% для первого действия, 0% для последнего.
                    float priorityBonus = 0.2f * (1.0f - ((float)i / activeActions.Count));
                    
                    if (CanExecuteActionConditions(action.actionType) && CheckActionRoll(action, priorityBonus))
                    {
                        chosenAction = action;
                        break;
                    }
                }
                // --- КОНЕЦ ИЗМЕНЕНИЯ ---
            }

            // --- ЭТАП 3: Выполнение выбранного действия или переход к отдыху ---
            if (chosenAction != null)
            {
                Debug.Log($"<color=green>AI РЕШЕНИЕ (Рутина):</color> {characterName} будет выполнять '{chosenAction.displayName}'.");
                UpdateFrustration(true);
                currentAction = StartCoroutine(ExecuteActionCoroutine(chosenAction.actionType));
                yield return currentAction;
            }
            else
            {
                Debug.Log($"<color=gray>AI РЕШЕНИЕ (Бездействие):</color> {characterName} не нашел задач, идет на пост.");
                UpdateFrustration(false);
                currentAction = StartCoroutine(ExecuteDefaultAction());
                yield return currentAction;
            }
        }
        yield return null;
    }
}


private void UpdateFrustration(bool wasCycleSuccessful)
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
        currentFrustration = 0f;
        Debug.Log($"<color=gray>ИТОГ ЦИКЛА:</color> Провал. Выгорание сброшено на 0.</color>");
    }
}
    
    // --- АБСТРАКТНЫЕ МЕТОДЫ (ДОЛЖНЫ БЫТЬ РЕАЛИЗОВАНЫ В ДОЧЕРНИХ КЛАССАХ) ---

    public abstract void GoOnBreak(float duration);
    
    //protected abstract bool TryExecuteAction(ActionType actionType);

    protected virtual IEnumerator ExecuteDefaultAction()
{
    // Базовая реализация может быть пустой, наследники ее переопределят
    yield return null;
    currentAction = null; // Убедимся, что сбрасываем действие
}
	
	protected abstract IEnumerator ExecuteActionCoroutine(ActionType actionType);

    protected abstract Queue<Waypoint> BuildPathTo(Vector2 targetPos);
    
    public virtual float GetStressValue() { return 0f; }
    
    public virtual void SetStressValue(float stress) { }

    // --- РУТИНЫ И ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ---
    
private IEnumerator GoHomeRoutine()
{
    var homeZone = ScenePointsRegistry.Instance?.staffHomeZone;
    if (homeZone != null)
    {
        Vector2 targetPos = homeZone.GetRandomPointInside();
        agentMover.SetPath(BuildPathTo(targetPos));
        yield return new WaitUntil(() => !agentMover.IsMoving());
        if (endShiftSound != null) AudioSource.PlayClipAtPoint(endShiftSound, transform.position);
        // Можно добавить здесь agentMover.enabled = false; чтобы они замирали до новой смены
    }
}
    
    private IEnumerator GoHomeAndDespawnRoutine()
    {
        Debug.Log($"Сотрудник {characterName} уволен и идет домой...");
        yield return StartCoroutine(GoHomeRoutine());
        Debug.Log($"{characterName} дошел до точки ухода и исчезает.");
        Destroy(gameObject);
    }
    
    // --- МЕТОДЫ UNITY (AWAKE, START) ---

    protected virtual void Awake()
    {
        agentMover = GetComponent<AgentMover>();
        logger = GetComponent<CharacterStateLogger>();
        visuals = GetComponent<CharacterVisuals>();
		thoughtBubble = GetComponent<ThoughtBubbleController>();
    }

    protected virtual void Start()
    {
        if (!isInitialized)
        {
            if (homePoint != null)
            {
                transform.position = homePoint.position;
            }
            InitializeRole();
            isInitialized = true;
        }
    }

    private void InitializeRole()
    {
        // Теперь этот метод просто настраивает визуал для стажера при первом появлении
        if (this.currentRole == Role.Intern)
        {
            var internController = GetComponent<InternController>();
            if (internController != null && visuals != null)
            {
                visuals.Setup(this.gender, internController.spriteCollection, internController.stateEmotionMap);
            }
        }
        // Логика по смене роли теперь полностью лежит на HiringManager
    }

    // --- ОСТАЛЬНЫЕ МЕТОДЫ ---

protected Transform RequestKitchenPoint()
{
    // Теперь запрашиваем точку напрямую у реестра
    if (ScenePointsRegistry.Instance == null) return null;
    return ScenePointsRegistry.Instance.RequestKitchenPoint();
}

protected void FreeKitchenPoint(Transform point)
{
    // Сообщаем реестру, что точка освободилась
    if (ScenePointsRegistry.Instance == null) return;
    ScenePointsRegistry.Instance.FreeKitchenPoint(point);
}

    protected IEnumerator EnterLimitedZoneAndWaitRoutine(Transform zoneEntrance, float waitDuration)
    {
        if (zoneEntrance == null)
        {
            Debug.LogError($"{name} не может войти в зону, так как точка входа не задана!");
            yield break;
        }
        LimitedCapacityZone zone = zoneEntrance.GetComponentInParent<LimitedCapacityZone>();
        if (zone == null)
        {
            Debug.LogError($"{name} пытается использовать {zoneEntrance.name} как вход в зону, но на родительском объекте нет компонента LimitedCapacityZone!");
            yield return new WaitForSeconds(waitDuration);
            yield break;
        }
        zone.JoinQueue(gameObject);
        yield return new WaitUntil(() => zone.IsFirstInQueue(gameObject));
        Waypoint insidePoint = null;
        while (insidePoint == null)
        {
            if (this == null || !gameObject.activeInHierarchy) yield break;
            insidePoint = zone.RequestAndOccupyWaypoint(gameObject);
            if (insidePoint == null)
            {
                yield return new WaitForSeconds(0.5f);
            }
        }
        zone.LeaveQueue(gameObject);
        agentMover.SetPath(BuildPathTo(insidePoint.transform.position));
        yield return new WaitUntil(() => !agentMover.IsMoving());
        yield return new WaitForSeconds(waitDuration);
        zone.ReleaseWaypoint(insidePoint);
        if (zone.exitWaypoint != null)
        {
            agentMover.SetPath(BuildPathTo(zone.exitWaypoint.transform.position));
            yield return new WaitUntil(() => !agentMover.IsMoving());
        }
    }
	
	public void RefreshAIState()
{

}
}