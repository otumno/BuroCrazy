// Файл: Assets/Scripts/Characters/Controllers/StaffController.cs - ФИНАЛЬНАЯ ВЕРСИЯ С UTILITY AI
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

    [Header("Базы данных действий")]
    [Tooltip("Тактические действия, назначаемые игроком в UI.")]
    public List<StaffAction> activeActions = new List<StaffAction>();
    [Tooltip("База данных ВСЕХ системных действий (потребности, патрули).")]
    public ActionDatabase systemActionDatabase; // Новое поле!

    public ServicePoint assignedWorkstation;
    
    [Header("Настройки Выгорания")]
    [SerializeField] private float baseFrustrationGain = 0.1f;
    [SerializeField] private SkillType frustrationResistanceSkill = SkillType.SedentaryResilience;
    
    [Header("Состояние потребностей (от 0.0 до 1.0)")]
    [Range(0f, 1f)] public float frustration = 0f;
    [Range(0f, 1f)] public float bladder = 0f;
    [Range(0f, 1f)] public float energy = 1f;
    [Range(0f, 1f)] public float morale = 1f;
    private Coroutine needsUpdateCoroutine;

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
        if (this is DirectorAvatarController) { yield break; }

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
                // Прерываем, если новое действие ВАЖНЕЕ (приоритет выше) и текущее можно прервать
                if (bestAction != null && bestAction.priority > currentExecutor.actionData.priority && currentExecutor.IsInterruptible)
                {
                    shouldSwitch = true;
                    Debug.Log($"<color=orange>[AI Brain - {characterName}]</color> ПРЕРЫВАНИЕ! Новое действие '{bestAction.displayName}' ({bestAction.priority}) важнее, чем '{currentExecutor.actionData.displayName}' ({currentExecutor.actionData.priority}).");
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

    // ----- ГЛАВНОЕ ИЗМЕНЕНИЕ: ПОЛНОЦЕННЫЙ UTILITY AI "МОЗГ" -----
    private StaffAction FindBestActionToPerform()
    {
        StringBuilder logBuilder = new StringBuilder();
        logBuilder.AppendLine($"<b><color=yellow>--- AI SCORE SHEET: {characterName} ({currentRole}) ---</color></b>");
        
        // 1. Собираем единый список всех возможных действий
        List<StaffAction> allPossibleActions = new List<StaffAction>();
        if(activeActions != null) allPossibleActions.AddRange(activeActions);
        if(systemActionDatabase != null) allPossibleActions.AddRange(systemActionDatabase.allActions);

        StaffAction bestAction = null;
        float bestScore = -1f;

        // 2. Оцениваем каждое действие
        foreach (var action in allPossibleActions)
        {
            if (action == null || !action.applicableRoles.Contains(this.currentRole)) continue;

            bool onCooldown = actionCooldowns.ContainsKey(action.actionType) && Time.time < actionCooldowns[action.actionType];
            if (onCooldown)
            {
                logBuilder.AppendLine($"  - {action.displayName} | <color=grey>НА ПЕРЕЗАРЯДКЕ</color>");
                continue;
            }

            if (!action.AreConditionsMet(this))
            {
                logBuilder.AppendLine($"  - {action.displayName} | <color=red>УСЛОВИЯ НЕ ВЫПОЛНЕНЫ</color>");
                continue;
            }

            // 3. Расчет "полезности" (Score)
            float currentScore = action.priority; // Начинаем с базового приоритета

            // Добавляем бонусы от потребностей
            if (action.actionType == ActionType.GoToToilet) currentScore += this.bladder * 100f; // Чем сильнее хочется, тем выше бонус
            if (action.actionType == ActionType.GoToBreak) currentScore += (1f - this.energy) * 100f; // Чем меньше энергии, тем выше бонус
            if (action.actionType == ActionType.GoToCooler) currentScore += (1f - this.morale) * 100f; // Чем ниже мораль, тем выше бонус
            
            logBuilder.AppendLine($"  - {action.displayName} | Приоритет: {action.priority} | Бонус: {(currentScore - action.priority):F0} | <b>Итоговый счет: {currentScore:F0}</b> | <color=green>ДОСТУПНО</color>");

            // 4. Выбираем действие с максимальным счетом
            if (currentScore > bestScore)
            {
                bestScore = currentScore;
                bestAction = action;
            }
        }
        
        if (bestAction != null)
        {
            logBuilder.AppendLine($"<b>ИТОГ: Выбрано лучшее действие -> <color=lime>'{bestAction.displayName}'</color> (Счет: {bestScore:F0})</b>");
        }
        else
        {
            logBuilder.AppendLine("<b>ИТОГ: <color=orange>Нет доступных действий.</color> Сотрудник будет бездействовать.</b>");
        }

        Debug.Log(logBuilder.ToString());
        return bestAction;
    }
    #endregion
    
    #region Abstract and Virtual Methods
    // ... (Этот раздел без изменений) ...
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
    // ... (Этот раздел без изменений, но убедитесь, что UpdateFrustration использует 'frustration') ...
    public bool IsOnDuty() => isOnDuty;
    public float GetCurrentFrustration() => frustration;
    public void SetCurrentFrustration(float value) => frustration = Mathf.Clamp01(value);
    
    public void ForceInitializeBaseComponents(AgentMover am, CharacterVisuals cv, CharacterStateLogger csl)
    {
        agentMover = am;
        visuals = cv;
        logger = csl;
        thoughtBubble = GetComponent<ThoughtBubbleController>();
    }
	
	public void SetActionCooldown(ActionType type, float duration)
    {
        actionCooldowns[type] = Time.time + duration;
    }

    public virtual void StartShift()
    {
        if (isOnDuty) return;
        isOnDuty = true;
        if (startShiftSound != null) AudioSource.PlayClipAtPoint(startShiftSound, transform.position);

        if (actionDecisionCoroutine != null) StopCoroutine(actionDecisionCoroutine);
        actionDecisionCoroutine = StartCoroutine(ShiftRoutine());

        if (needsUpdateCoroutine != null) StopCoroutine(needsUpdateCoroutine);
        needsUpdateCoroutine = StartCoroutine(NeedsUpdateRoutine());
    }

    public virtual void EndShift()
    {
        if (!isOnDuty) return;
        isOnDuty = false;
        
        if (actionDecisionCoroutine != null) StopCoroutine(actionDecisionCoroutine);
        if (needsUpdateCoroutine != null) StopCoroutine(needsUpdateCoroutine);
        actionDecisionCoroutine = null;
        needsUpdateCoroutine = null;

        if (currentExecutor != null)
        {
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
        if(actionToExecute.category == ActionCategory.Tactic)
        {
            UpdateFrustration(true);
        }
        
        System.Type executorType = actionToExecute.GetExecutorType();
        if (executorType == null) return false;
        
        currentExecutor = gameObject.AddComponent(executorType) as ActionExecutor;
        if (currentExecutor != null)
        {
            currentExecutor.Execute(this, actionToExecute);
            actionCooldowns[actionToExecute.actionType] = Time.time + actionToExecute.actionCooldown;
            return true;
        }
        return false;
    }

    public void OnActionFinished()
    {
        this.currentExecutor = null;
    }
    
    protected virtual ActionExecutor GetIdleActionExecutor() => null;
    protected virtual ActionExecutor GetBurnoutActionExecutor() => gameObject.AddComponent<DefaultActionExecutor>();
    
    protected void UpdateFrustration(bool wasCycleSuccessful)
    {
        if (wasCycleSuccessful)
        {
            float resistance = skills.GetSkillValue(frustrationResistanceSkill);
            float frustrationGain = baseFrustrationGain * (1f - resistance * 0.5f);
            frustration = Mathf.Clamp01(frustration + frustrationGain);
        }
    }
    #endregion
    
    private IEnumerator NeedsUpdateRoutine()
    {
        while (isOnDuty)
        {
            yield return new WaitForSeconds(10f);

            if (currentExecutor != null && !currentExecutor.IsInterruptible)
            {
                continue;
            }

            float bladderGain = 0.05f;
            bladder = Mathf.Clamp01(bladder + bladderGain);

            float energyLoss = 0.02f * (1f - skills.sedentaryResilience * 0.5f);
            energy = Mathf.Clamp01(energy - energyLoss);
            
            float moraleLoss = 0.03f * (1f - skills.pedantry);
            morale = Mathf.Clamp01(morale - moraleLoss);
        }
    }

    #region Unchanged Utility Methods
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
        if (actionDecisionCoroutine != null) StopCoroutine(actionDecisionCoroutine);
        if (currentExecutor != null) Destroy(currentExecutor);
        actionDecisionCoroutine = null;
        currentExecutor = null;
        StartCoroutine(GoHomeAndDespawnRoutine());
    }

    private IEnumerator GoHomeAndDespawnRoutine()
    {
        yield return StartCoroutine(GoHomeRoutine());
        Destroy(gameObject);
    }
    #endregion
}