// Файл: Assets/Scripts/Characters/Controllers/StaffController.cs - ПОЛНАЯ ИСПРАВЛЕННАЯ ВЕРСИЯ
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public abstract class StaffController : MonoBehaviour
{
    #region Fields
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
    public int unpaidPeriods = 0;
    public int missedPaymentCount = 0;
    
    public bool isReadyForPromotion = false;
    public List<StaffAction> activeActions = new List<StaffAction>();
    public ServicePoint assignedWorkstation;
    
    [Header("Настройки Выгорания")]
    [Tooltip("Базовое значение, на которое увеличивается выгорание за успешный рабочий цикл.")]
    [SerializeField] private float baseFrustrationGain = 0.1f;
    [Tooltip("Навык, который снижает скорость выгорания (например, Усидчивость).")]
    [SerializeField] private SkillType frustrationResistanceSkill = SkillType.SedentaryResilience;
    
    protected float currentFrustration = 0f;
    protected Dictionary<ActionType, float> actionCooldowns = new Dictionary<ActionType, float>();

    public EmotionSpriteCollection spriteCollection;
    public StateEmotionMap stateEmotionMap;
    protected bool isOnDuty = false;
    protected AgentMover agentMover;
	public AgentMover AgentMover => agentMover;
    protected CharacterStateLogger logger;
    protected CharacterVisuals visuals;
    public ThoughtBubbleController thoughtBubble { get; private set; }

    public ActionExecutor currentExecutor { get; private set; }
    private Coroutine actionDecisionCoroutine;
    #endregion

    #region Core AI Logic (The "Brain")
    private IEnumerator ShiftRoutine()
    {
        if (this is DirectorAvatarController)
        {
            yield break; 
        }

        if (assignedWorkstation != null)
        {
            var goToWorkExecutor = gameObject.AddComponent<GoToWorkstationExecutor>();
            goToWorkExecutor.Execute(this, null);
            yield return new WaitUntil(() => goToWorkExecutor == null);
        }

        Debug.Log($"<color=lime>AI ЗАПУЩЕН</color> для {characterName} с новой системой Utility AI.");
        while (isOnDuty)
        {
            StaffAction bestAction = FindBestActionToPerform();

            bool shouldSwitch = false;
            if (currentExecutor == null)
            {
                shouldSwitch = (bestAction != null);
            }
            else
            {
                if (bestAction != null && currentExecutor.actionData != bestAction && currentExecutor.IsInterruptible)
                {
                    shouldSwitch = true;
                    Debug.Log($"<color=orange>[AI Brain - {characterName}]</color> ПРЕРЫВАНИЕ! Новое действие '{bestAction.displayName}' важнее, чем текущее '{currentExecutor.GetType().Name}'. МЕНЯЮ ЗАДАЧУ.");
                }
            }

            if (shouldSwitch)
            {
                if (currentExecutor != null)
                {
                    Destroy(currentExecutor);
                    currentExecutor = null;
                }
                ExecuteAction(bestAction);
            }
            
            yield return new WaitForSeconds(1f);
        }
    }

    private StaffAction FindBestActionToPerform()
    {
        StringBuilder logBuilder = new StringBuilder();
        logBuilder.AppendLine($"<b><color=yellow>--- AI SCORE SHEET: {characterName} ({currentRole}) ---</color></b>");

        StaffAction chosenAction = null;
        int topScore = -1000; // Start with a very low score

        if (activeActions != null && activeActions.Any())
        {
            foreach (var action in activeActions.OrderByDescending(a => a.priority))
            {
                if (action == null) continue;

                bool conditionsMet = action.AreConditionsMet(this);
                bool onCooldown = actionCooldowns.ContainsKey(action.actionType) && Time.time < actionCooldowns[action.actionType];
                string status;
                
                if (onCooldown)
                {
                    status = "<color=grey>НА ПЕРЕЗАРЯДКЕ</color>";
                }
                else if (conditionsMet)
                {
                    status = "<color=green>УСЛОВИЯ ВЫПОЛНЕНЫ</color>";
                    if (chosenAction == null) // First suitable action becomes the best one due to sorting
                    {
                        chosenAction = action;
                        topScore = action.priority;
                    }
                }
                else
                {
                    status = "<color=red>УСЛОВИЯ НЕ ВЫПОЛНЕНЫ</color>";
                }

                logBuilder.AppendLine($"  - Действие: <b>{action.displayName}</b> | Приоритет: {action.priority} | Статус: {status}");
            }
        }
        else
        {
            logBuilder.AppendLine("  <color=red>НЕТ НАЗНАЧЕННЫХ ДЕЙСТВИЙ!</color>");
        }

        if (chosenAction != null)
        {
            logBuilder.AppendLine($"<b>ИТОГ: Выбрано лучшее действие -> <color=lime>'{chosenAction.displayName}'</color> (Приоритет: {topScore})</b>");
        }
        else
        {
            logBuilder.AppendLine("<b>ИТОГ: <color=orange>Нет доступных действий для выполнения.</color> Сотрудник будет бездействовать.</b>");
        }

        Debug.Log(logBuilder.ToString());
        return chosenAction;
    }
    #endregion
    
    #region Abstract and Virtual Methods
    // These methods MUST be here for derived classes to override them
    public abstract bool IsOnBreak();
    public virtual string GetStatusInfo() => "Статус не определен";
    public virtual string GetCurrentStateName() => "Unknown";
    public virtual IEnumerator MoveToTarget(Vector2 targetPosition, string stateOnArrival)
    {
        agentMover.SetPath(PathfindingUtility.BuildPathTo(transform.position, targetPosition, this.gameObject));
        yield return new WaitUntil(() => !agentMover.IsMoving());
    }
    public virtual void Initialize(RoleData data)
    {
        if (visuals == null) visuals = GetComponent<CharacterVisuals>();
        if (agentMover == null) agentMover = GetComponent<AgentMover>();

        this.currentRole = data.roleType;
        if (visuals != null)
        {
            visuals.SetupFromRoleData(data, this.gender);
        }
        if (agentMover != null)
        {
            agentMover.SetAnimationSprites(data.idleSprite, data.walkSprite1, data.walkSprite2);
            agentMover.moveSpeed = data.moveSpeed;
            agentMover.priority = data.priority;
        }
    }
    #endregion

    #region Standard Methods
    public bool IsOnDuty() => isOnDuty;
    public float GetCurrentFrustration() => currentFrustration;
    public void SetCurrentFrustration(float value) => currentFrustration = Mathf.Clamp01(value);
    
    public void ForceInitializeBaseComponents(AgentMover am, CharacterVisuals cv, CharacterStateLogger csl)
    {
        agentMover = am;
        visuals = cv;
        logger = csl;
        thoughtBubble = GetComponent<ThoughtBubbleController>();
    }
	
	protected void UpdateFrustration(bool wasCycleSuccessful)
{
    float oldFrustration = currentFrustration;
    if (wasCycleSuccessful)
    {
        float resistance = skills.GetSkillValue(frustrationResistanceSkill);
        float frustrationGain = baseFrustrationGain * (1f - resistance * 0.5f);
        currentFrustration = Mathf.Clamp01(currentFrustration + frustrationGain);
    }
}

    public virtual void StartShift()
    {
        if (isOnDuty) return;
        isOnDuty = true;
        if (startShiftSound != null) AudioSource.PlayClipAtPoint(startShiftSound, transform.position);

        if (actionDecisionCoroutine != null) StopCoroutine(actionDecisionCoroutine);
        actionDecisionCoroutine = StartCoroutine(ShiftRoutine());
    }

    public virtual void EndShift()
    {
        if (!isOnDuty) return;
        Debug.Log($"<color=orange>КОНЕЦ СМЕНЫ:</color> Запущена процедура для {characterName}.");
        isOnDuty = false;

        if (actionDecisionCoroutine != null)
        {
            StopCoroutine(actionDecisionCoroutine);
            actionDecisionCoroutine = null;
        }

        if (currentExecutor != null)
        {
            Debug.LogWarning($"{characterName} был занят '{currentExecutor.GetType().Name}', но его смена закончилась. Действие принудительно прервано.");
            Destroy(currentExecutor);
            currentExecutor = null;
        }

        StartCoroutine(GoHomeRoutine());
    }
    
    protected virtual void Awake()
    {
        agentMover = GetComponent<AgentMover>();
        logger = GetComponent<CharacterStateLogger>();
        visuals = GetComponent<CharacterVisuals>();
        thoughtBubble = GetComponent<ThoughtBubbleController>();
    }
    
    public bool ExecuteAction(StaffAction actionToExecute)
    {
        UpdateFrustration(true);
        
        System.Type executorType = actionToExecute.GetExecutorType();
        if (executorType == null)
        {
             Debug.LogError($"Для действия '{actionToExecute.displayName}' не назначен исполнитель (GetExecutorType вернул null)!");
             return false;
        }
        
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

    public void OnActionFinished()
    {
        this.currentExecutor = null;
    }
    
    protected virtual ActionExecutor GetIdleActionExecutor() => null;
    protected virtual ActionExecutor GetBurnoutActionExecutor() => gameObject.AddComponent<DefaultActionExecutor>();
    
    // ... Other utility methods ...
    private IEnumerator GoHomeRoutine()
    {
        if (unpaidPeriods > 0)
        {
            var salaryStack = ScenePointsRegistry.Instance?.salaryStackPoint;
            if (salaryStack != null)
            {
                AgentMover.SetPath(PathfindingUtility.BuildPathTo(transform.position, salaryStack.transform.position, this.gameObject));
                yield return new WaitUntil(() => !AgentMover.IsMoving());

                if (salaryStack.TakeOneEnvelope())
                {
                    int salaryToPay = salaryPerPeriod * unpaidPeriods;

                    if (PlayerWallet.Instance.GetCurrentMoney() >= salaryToPay)
                    {
                        PlayerWallet.Instance.AddMoney(-salaryToPay, $"Зарплата: {characterName}");
                        unpaidPeriods = 0;
                        missedPaymentCount = 0;
                    }
                    else
                    {
                        salaryStack.AddEnvelope(); 
                        missedPaymentCount++;
                    }
                }
                else
                {
                    yield return new WaitForSeconds(10f);
                    missedPaymentCount++;
                }

                if (missedPaymentCount >= 2)
                {
                    DirectorManager.Instance?.AddStrike();
                    FireAndGoHome(); 
                    yield break;
                }
            }
        }

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