// Файл: Assets/Scripts/Characters/Controllers/StaffController.cs
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public abstract class StaffController : MonoBehaviour
{
    #region Fields
    public enum Role { Unassigned, Intern, Registrar, Cashier, Archivist, Guard, Janitor, Clerk }

    [Header("Прогрессия и Роль")]
    public string characterName = "Безымянный";
    public RankData currentRank;
    public Role currentRole = Role.Intern;
    public int experiencePoints = 0;
    public Gender gender;
    public CharacterSkills skills;
    
    [Header("График и Зарплата")]
    public List<string> workPeriods = new List<string>();
    public int salaryPerPeriod = 15;
	public int unpaidPeriods = 0;
	public int missedPaymentCount = 0;
    
    [Header("Базы данных действий")]
    public List<StaffAction> activeActions = new List<StaffAction>();
    public ActionDatabase systemActionDatabase;

    [Header("Рабочее место")]
    public ServicePoint assignedWorkstation;
    
    [Header("Состояние потребностей (от 0.0 до 1.0)")]
    [Range(0f, 1f)] public float frustration = 0f;
    [Range(0f, 1f)] public float bladder = 0f;
    [Range(0f, 1f)] public float energy = 1f;
    [Range(0f, 1f)] public float morale = 1f;
    
    [Header("Настройки Выгорания")]
    [SerializeField] private float baseFrustrationGain = 0.1f;
    [SerializeField] private SkillType frustrationResistanceSkill = SkillType.SedentaryResilience;

    [Header("Звуки смены")]
    public AudioClip startShiftSound;
    public AudioClip endShiftSound;

    // Ссылки на компоненты
    public EmotionSpriteCollection spriteCollection;
    public StateEmotionMap stateEmotionMap;
    protected AgentMover agentMover;
	public AgentMover AgentMover => agentMover;
    protected CharacterStateLogger logger;
    protected CharacterVisuals visuals;
    public ThoughtBubbleController thoughtBubble { get; private set; }
    public ActionExecutor currentExecutor { get; private set; }
    
    // Внутренние переменные
    protected Dictionary<ActionType, float> actionCooldowns = new Dictionary<ActionType, float>();
    protected bool isOnDuty = false;
    private Coroutine needsUpdateCoroutine;
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

        while (isOnDuty)
        {
            StaffAction bestAction = FindBestActionToPerform();

            if (currentExecutor == null)
            {
                if (bestAction != null)
                {
                    if (CheckActionRoll(bestAction))
                    {
                        ExecuteAction(bestAction);
                    }
                    else
                    {
                        thoughtBubble?.ShowPriorityMessage("Эх, не вышло...", 2f, Color.red);
                        SetActionCooldown(bestAction.actionType, 10f);
                    }
                }
            }
            else 
            {
                if (bestAction != null && bestAction.priority > currentExecutor.actionData.priority)
                {
                    if (currentExecutor.IsInterruptible)
                    {
                        Debug.Log($"<color=orange>[AI Brain - {characterName}]</color> ПРЕРЫВАНИЕ! Новое действие '{bestAction.displayName}' ({bestAction.priority}) важнее, чем '{currentExecutor.actionData.displayName}' ({currentExecutor.actionData.priority}).");
                        
                        Destroy(currentExecutor);
                        currentExecutor = null;

                        ExecuteAction(bestAction);
                    }
                }
            }
            
            yield return new WaitForSeconds(1f);
        }
    }

    private StaffAction FindBestActionToPerform()
    {
        StringBuilder logBuilder = new StringBuilder();
        logBuilder.AppendLine($"<b><color=yellow>--- AI SCORE SHEET: {characterName} ({currentRole}) ---</color></b>");
        
        List<StaffAction> allPossibleActions = new List<StaffAction>();
        if(activeActions != null) allPossibleActions.AddRange(activeActions);
        if(systemActionDatabase != null) allPossibleActions.AddRange(systemActionDatabase.allActions);

        StaffAction bestAction = null;
        float bestScore = -1f;

        foreach (var action in allPossibleActions.Distinct())
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
                logBuilder.AppendLine($"  - {action.displayName} ({action.category}) | <color=red>УСЛОВИЯ НЕ ВЫПОЛНЕНЫ</color>");
                continue;
            }

            float currentScore = action.priority;
            if (action.category == ActionCategory.System)
            {
                if (action.actionType == ActionType.GoToToilet) currentScore += this.bladder * 100f;
                if (action.actionType == ActionType.GoToBreak) currentScore += (1f - this.energy) * 100f;
                if (action.actionType == ActionType.GoToCooler) currentScore += (1f - this.morale) * 100f;
            }
            
            logBuilder.AppendLine($"  - {action.displayName} ({action.category}) | Приоритет: {action.priority} | Бонус: {(currentScore - action.priority):F0} | <b>Итоговый счет: {currentScore:F0}</b> | <color=green>ДОСТУПНО</color>");

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

    protected bool CheckActionRoll(StaffAction actionData)
    {
        if (this is DirectorAvatarController) return true;
        if (actionData == null) return false;

        if (actionData.category == ActionCategory.System) return true;

        float rankBonus = 0f;
        if (currentRank != null)
        {
            rankBonus = currentRank.rankLevel * 0.02f;
        }

        float skillModifier = 0f;
        if(skills != null && actionData.primarySkill != null)
        {
            skillModifier = skills.GetSkillValue(actionData.primarySkill.skill) * actionData.primarySkill.strength;
            if (actionData.useSecondarySkill && actionData.secondarySkill != null)
            {
                skillModifier += skills.GetSkillValue(actionData.secondarySkill.skill) * actionData.secondarySkill.strength;
            }
        }
        
        float initialChance = actionData.baseSuccessChance + skillModifier + rankBonus - frustration;
        float finalChance = Mathf.Clamp(initialChance, actionData.minSuccessChance, actionData.maxSuccessChance);
        
        return Random.value <= finalChance;
    }
    #endregion
    
    #region Abstract and Virtual Methods
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
        if (visuals != null) visuals.SetupFromRoleData(data, this.gender);
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
    public float GetCurrentFrustration() => frustration;
    public void SetCurrentFrustration(float value) => frustration = Mathf.Clamp01(value);
    
    public void ForceInitializeBaseComponents(AgentMover am, CharacterVisuals cv, CharacterStateLogger csl)
    {
        agentMover = am;
        visuals = cv;
        logger = csl;
        thoughtBubble = GetComponent<ThoughtBubbleController>();
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
        System.Type executorType = actionToExecute.GetExecutorType();
        if (executorType == null) return false;
        
        currentExecutor = gameObject.AddComponent(executorType) as ActionExecutor;
        if (currentExecutor != null)
        {
            currentExecutor.Execute(this, actionToExecute);
            SetActionCooldown(actionToExecute.actionType, actionToExecute.actionCooldown);
            return true;
        }
        return false;
    }

    public void OnActionFinished()
    {
        if(currentExecutor != null && currentExecutor.actionData.category == ActionCategory.Tactic)
        {
            UpdateFrustration(true);
        }
        this.currentExecutor = null;
    }
    
    public void SetActionCooldown(ActionType type, float duration)
    {
        if (duration > 0)
        {
            actionCooldowns[type] = Time.time + duration;
        }
    }

    protected void UpdateFrustration(bool wasCycleSuccessful)
    {
        if (wasCycleSuccessful)
        {
            float resistance = (skills != null) ? skills.GetSkillValue(frustrationResistanceSkill) : 0f;
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
            if (currentExecutor != null && !currentExecutor.IsInterruptible) continue;
            
            bladder = Mathf.Clamp01(bladder + 0.05f);
            if (skills != null)
            {
                energy = Mathf.Clamp01(energy - (0.02f * (1f - skills.sedentaryResilience * 0.5f)));
                morale = Mathf.Clamp01(morale - (0.03f * (1f - skills.pedantry)));
            }
        }
    }

    #region Utility Methods
    private IEnumerator GoHomeRoutine()
    {
        if (unpaidPeriods > 0)
        {
            var salaryStack = ScenePointsRegistry.Instance?.salaryStackPoint;
            if (salaryStack != null)
            {
                thoughtBubble?.ShowPriorityMessage("За зарплатой...", 3f, Color.yellow);
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
                        thoughtBubble?.ShowPriorityMessage("Отлично!", 2f, Color.green);
                    }
                    else
                    {
                        salaryStack.AddEnvelope(); 
                        missedPaymentCount++;
                        thoughtBubble?.ShowPriorityMessage("В казне пусто?!", 3f, Color.red);
                    }
                }
                else
                {
                    thoughtBubble?.ShowPriorityMessage("Где мой конверт?!", 4f, Color.red);
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