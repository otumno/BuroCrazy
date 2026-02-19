// Assets/Scripts/Characters/Controllers/StaffController.cs
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Gameplay;
using Managers;
using Utilities;
using Data.Calendar;
using Scriptables.Audio;
using Characters;
using Managers.Teletype;

[RequireComponent(typeof(AgentMover))]
[RequireComponent(typeof(CharacterStateLogger))]
[RequireComponent(typeof(ThoughtBubbleController))]
public class StaffController : MonoBehaviour
{
    public enum Role 
    { 
        Unassigned, Intern, Registrar, Cashier, Archivist, Guard, Janitor, Clerk, OfficeManager, Accountant, ServiceWorker, Director
    }

    [Header("Базовые настройки")]
    public string characterName = "Сотрудник";
    public Role role = Role.Unassigned;
    public RoleData roleData; 
    public Gender gender;

    [Header("Карьера")]
    public RankData currentRankData; 
    public RankData currentRank 
    {
        get => currentRankData;
        set => currentRankData = value;
    }
    public int currentRankLevel => currentRankData != null ? currentRankData.rankLevel : 1;

    public int salaryPerPeriod = 100;
    public int unpaidPeriods = 0;
    public int missedPaymentCount = 0;
    public float experiencePoints = 0f;
    public bool promotionAvailableNotificationPlayed = false;

    // Обертка для скиллов (совместимость)
    [System.Serializable]
    public class CharacterSkillsWrapper 
    {
        public float speed = 1f;
        public float efficiency = 1f;
        public float paperworkMastery = 0f;
        public float sedentaryResilience = 0f; 
        public float pedantry = 0f;            
        public float softSkills = 0f;          
        public float corruption = 0f;          
        
        public string GetSkillShortText(SkillType type) => $"{type}: 100%"; 

        public static implicit operator CharacterSkillsWrapper(CharacterSkills s)
        {
            if (s == null) return new CharacterSkillsWrapper();
            return new CharacterSkillsWrapper 
            {
                paperworkMastery = s.paperworkMastery,
                sedentaryResilience = s.sedentaryResilience,
                pedantry = s.pedantry,
                softSkills = s.softSkills,
                corruption = s.corruption
            };
        }
        
        public static implicit operator CharacterSkills(CharacterSkillsWrapper w)
        {
            var s = ScriptableObject.CreateInstance<CharacterSkills>();
            s.paperworkMastery = w.paperworkMastery;
            s.sedentaryResilience = w.sedentaryResilience;
            s.pedantry = w.pedantry;
            s.softSkills = w.softSkills;
            s.corruption = w.corruption;
            return s;
        }
    }
    public CharacterSkillsWrapper skills = new CharacterSkillsWrapper();

    [Header("График работы")]
    public CalendarDayPeriodType WorkShiftMask = CalendarDayPeriodTypeExtensions.FullDay;

    [Header("Пунктуальность")]
    [Tooltip("Педантичность (0-1).")]
    [Range(0f, 1f)]
    public float punctuality = 0.5f;
    public float maxLateness = 30f;
    public float latenessVariance = 5f;

    [Header("Время прихода/ухода")]
    public float shiftStartTime = 0f;
    public float currentLateness = 0f;
    public float currentEarlyLeave = 0f;
    public bool hasArrivedToday = false;
    public bool hasLeftToday = false;

    [Header("Обед и перерывы")]
    public bool hasLunchBreak = true;
    public bool HasTakenBreakToday { get; protected set; }
    public bool isOnBreak = false;
    public float breakStartTime = 0f;
    
    // --- ПЕРЕРЫВЫ И ПОТРЕБНОСТИ (новая система по периодам) ---
    [Header("Перерывы и потребности")]
    public float stressThresholdToilet = 0.9f;  // 90% стресса - срочно в туалет
    public float stressThresholdBreak = 0.6f;    // 60% стресса - можно на перерыв
    public float baseBreakChancePerPeriod = 0.15f; // Базовый шанс перерыва за период
    public float breakDuration = 120f;            // Длительность перерыва (2 минуты)
    public float toiletBreakCooldown = 300f;     // 5 минут между туалетами
    public float breakCheckInterval = 30f;        // Базовый интервал проверки
    public float breakCheckVariation = 15f;       // Вариация (±сек) чтобы не ходили толпой

    [Header("Действия")]
    public List<StaffAction> activeActions = new List<StaffAction>();
    public ActionDatabase systemActionDatabase; 
    
    [Header("Компоненты")]
    public AgentMover agentMover; 
    public ThoughtBubbleController thoughtBubble;
    public CharacterStateLogger logger;
    public CharacterVisuals visuals; 
    public VoiceData voiceProfile; 

    [Header("Состояние")]
    public float energy = 100f;
    public float stress = 0f;
    public float frustration = 0f;
    public float bladder = 0f; 
    public float morale = 100f;

    public ServicePoint assignedWorkstation; 
    public int uiScheduleTrackIndex = -1;

    public StaffAction currentAction; 
    public ActionExecutor currentExecutor; 

    private Coroutine aiLoopCoroutine;

    protected virtual void Awake()
    {
        agentMover = GetComponent<AgentMover>();
        thoughtBubble = GetComponent<ThoughtBubbleController>();
        logger = GetComponent<CharacterStateLogger>();
        visuals = GetComponent<CharacterVisuals>();
        if (systemActionDatabase == null)
        {
            systemActionDatabase = Resources.Load<ActionDatabase>("Databases/ActionDatabase");
        }
    }

        // --- StartShift с новой логикой ---
    public virtual void StartShift()
    {
        if (thoughtBubble) thoughtBubble.ShowPriorityMessage("На работу!", 2f, Color.white);

        hasArrivedToday = false;
        hasLeftToday = false;
        currentLateness = 0f;
        currentEarlyLeave = 0f;
        HasTakenBreakToday = false;
        
        gameObject.SetActive(true); // Включаем, если был выключен

        CalculateArrivalTime();

        // 1. Попытка авто-назначения
        if (assignedWorkstation == null)
        {
            AssignmentManager.Instance?.AutoAssignStaff(this);
        }

        // 2. Логика прибытия
        if (assignedWorkstation != null)
        {
            Debug.Log($"[StaffController] {characterName}: Иду на место {assignedWorkstation.name}");
            StartCoroutine(GoToWorkstationRoutine());
        }
        else
        {
             Debug.Log($"[StaffController] {characterName}: Нет места, иду в зону ожидания");
             StartCoroutine(GoToHangoutRoutine());
        }

        // 3. Запуск "Мозга"
        if (aiLoopCoroutine != null) StopCoroutine(aiLoopCoroutine);
        aiLoopCoroutine = StartCoroutine(AIUpdateLoop());
    }

    protected virtual IEnumerator GoToWorkstationRoutine()
    {
        if (assignedWorkstation == null || assignedWorkstation.clerkStandPoint == null) yield break;

        var targetPos = assignedWorkstation.clerkStandPoint.position;
        if (agentMover != null)
        {
            agentMover.SetPath(PathfindingUtility.BuildPathTo(transform.position, targetPos, gameObject));
            while (agentMover.IsMoving()) yield return null;
        }

        hasArrivedToday = true;
        // Сообщаем в телетайп только когда ФАКТИЧЕСКИ прибыл на рабочее место
        TeletypeManager.Instance?.LogStaffWork(characterName, role.ToString(), isStartShift: true);
    }

    protected virtual IEnumerator GoToHangoutRoutine()
    {
        Vector3 targetPos = transform.position; 
        
        var kitchenPoint = ScenePointsRegistry.Instance?.RequestKitchenPoint();
        if (kitchenPoint != null)
        {
            targetPos = kitchenPoint.transform.position;
        }
        else
        {
            var homeZone = ScenePointsRegistry.Instance?.staffHomeZone;
            if (homeZone != null)
            {
                targetPos = homeZone.GetRandomPointInside();
            }
        }
        
        if (agentMover != null)
        {
            agentMover.SetPath(PathfindingUtility.BuildPathTo(transform.position, targetPos, gameObject));
            while (agentMover.IsMoving()) yield return null;
        }
        
        hasArrivedToday = true;
    }

    // --- AI Loop ---
    // --- ПЕРЕРЫВЫ И ПОТРЕБНОСТИ ---
    private float currentStress = 0f;
    private int currentPeriodIndex = 0;          // Какой период смены сейчас (0, 1, 2...)
    private bool hasTakenBreakInCurrentPeriod = false;
    private float lastBreakCheckTime = 0f;
    private float lastToiletBreak = -1000f;      // Время последнего туалета

    protected virtual IEnumerator AIUpdateLoop()
    {
        currentPeriodIndex = 0;
        
        // Инициализируем время проверки с вариацией чтобы не ходили толпой
        lastBreakCheckTime = Time.time + Random.Range(-breakCheckVariation, breakCheckVariation);
        
        while (IsOnDuty())
        {
            yield return new WaitForSeconds(2f);
            
            UpdatePeriodIndex();

            // Проверяем перерывы
            if (CheckAndHandleBreaks()) continue;

            // Пропускаем если заняты
            if (currentExecutor != null) continue;
            if (agentMover != null && agentMover.IsMoving()) continue;
            if (IsOnBreak()) continue;

            // Работа: если без стола - пробуем найти
            if (assignedWorkstation == null)
            {
                if (AssignmentManager.Instance != null && AssignmentManager.Instance.AutoAssignStaff(this))
                {
                    StartCoroutine(GoToWorkstationRoutine());
                }
                continue;
            }
            
            TryPickAction();
        }
    }

    private void UpdatePeriodIndex()
    {
        // Простая логика: считаем сколько периодов прошло с начала смены
        // В реальности TimeManager может подсказать текущий индекс
        if (TimeManager.Instance != null)
        {
            var currentPeriod = TimeManager.Instance.GetCurrentPeriodType();
            // Примерная логика - в реальном коде нужно знать с какого периода началась смена
        }
    }

    private bool ShouldTakeBreak()
    {
        // Шанс перерыва = базовый шанс × (1 + periodIndex) × (1 - sedentaryResilience)
        // Непоседливые (низкая усидчивость) чаще хотят перерыв
        float chance = baseBreakChancePerPeriod * (1f + currentPeriodIndex) * (1f - skills.sedentaryResilience);
        
        // Проверяем рандомно
        return Random.value < chance;
    }

    private bool TryStartBreak()
    {
        var breakZone = ScenePointsRegistry.Instance?.kitchenPoints;
        if (breakZone != null && breakZone.Count > 0)
        {
            var targetPoint = breakZone[Random.Range(0, breakZone.Count)];
            if (targetPoint != null && targetPoint.transform != null)
            {
                StartCoroutine(BreakRoutine(targetPoint.transform.position));
                return true;
            }
        }
        return false;
    }

    private bool TryStartToiletBreak()
    {
        var toiletPoints = ScenePointsRegistry.Instance?.toiletPoints;
        if (toiletPoints != null && toiletPoints.Count > 0)
        {
            var targetPoint = toiletPoints[Random.Range(0, toiletPoints.Count)];
            if (targetPoint != null && targetPoint.transform != null)
            {
                StartCoroutine(ToiletRoutine(targetPoint.transform.position));
                return true;
            }
        }
        return false;
    }

    private IEnumerator BreakRoutine(Vector3 breakPos)
    {
        SetBreakState(true);
        thoughtBubble?.ShowPriorityMessage("Перерыв...", 2f, Color.white);
        
        // Идём на точку
        yield return StartCoroutine(MoveToTarget(breakPos, "Break"));
        
        // Снимаем стресс во время перерыва
        float elapsed = 0f;
        while (elapsed < breakDuration && IsOnDuty())
        {
            currentStress = Mathf.Lerp(currentStress, 0f, Time.deltaTime / 5f);
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        lastToiletBreak = Time.time; // Обновляем кулдаун туалета тоже
        hasTakenBreakInCurrentPeriod = true;
        
        SetBreakState(false);
        thoughtBubble?.ShowPriorityMessage("К работе!", 2f, Color.white);
    }

    private IEnumerator ToiletRoutine(Vector3 toiletPos)
    {
        SetBreakState(true);
        thoughtBubble?.ShowPriorityMessage("Туалет...", 2f, Color.white);
        
        yield return StartCoroutine(MoveToTarget(toiletPos, "Toilet"));
        
        // Быстрый перерыв
        yield return new WaitForSeconds(15f);
        
        lastToiletBreak = Time.time;
        currentStress *= 0.7f; // Снимаем 30% стресса
        
        SetBreakState(false);
        thoughtBubble?.ShowPriorityMessage("Лучше...", 2f, Color.white);
    }

    private void SetBreakState(bool onBreak)
    {
        isOnBreak = onBreak;
    }

    // Общий метод проверки перерывов - возвращает true если перерыв начался
    protected bool CheckAndHandleBreaks()
    {
        if (currentExecutor != null || (agentMover != null && agentMover.IsMoving()) || IsOnBreak())
            return false;

        // Туалет
        if (currentStress >= stressThresholdToilet && 
            Time.time - lastToiletBreak >= toiletBreakCooldown)
        {
            if (TryStartToiletBreak()) return true;
        }

        // Перерыв - проверяем периодически с вариацией
        if (Time.time - lastBreakCheckTime >= breakCheckInterval)
        {
            // Следующая проверка с вариацией
            lastBreakCheckTime = Time.time + Random.Range(-breakCheckVariation, breakCheckVariation);
            
            if (!hasTakenBreakInCurrentPeriod && ShouldTakeBreak())
            {
                if (TryStartBreak()) return true;
            }
        }

        return false;
    }

    // Метод для добавления стресса (вызывается извне)
    public void AddStress(float amount)
    {
        if (amount == 0) return;
        currentStress += amount;
        currentStress = Mathf.Clamp01(currentStress);
    }

    public float GetCurrentStress() => currentStress;

    protected void TryPickAction()
    {
        if (activeActions == null || activeActions.Count == 0) return;

        var shuffledActions = new List<StaffAction>(activeActions);
        // Shuffle
        for (int i = 0; i < shuffledActions.Count; i++)
        {
             var temp = shuffledActions[i];
             int randomIndex = Random.Range(i, shuffledActions.Count);
             shuffledActions[i] = shuffledActions[randomIndex];
             shuffledActions[randomIndex] = temp;
        }

        foreach (var action in shuffledActions)
        {
            if (action.AreConditionsMet(this))
            {
                ExecuteAction(action);
                break;
            }
        }
    }
    
    public virtual void EndShift()
    {
        if (aiLoopCoroutine != null) StopCoroutine(aiLoopCoroutine);

        if (thoughtBubble) thoughtBubble.ShowPriorityMessage("Домой...", 2f, Color.white);
        TeletypeManager.Instance?.LogStaffWork(characterName, role.ToString(), isStartShift: false);
        hasLeftToday = true;
        currentEarlyLeave = CalculateEarlyLeave();
        
        if (ScenePointsRegistry.Instance != null && ScenePointsRegistry.Instance.staffHomeZone != null)
        {
            Vector3 exitPos = ScenePointsRegistry.Instance.staffHomeZone.GetRandomPointInside();
            StartCoroutine(ExitRoutine(exitPos));
        }
        else
        {
            gameObject.SetActive(false);
        }
    }
    
    private IEnumerator ExitRoutine(Vector3 target)
    {
        if (agentMover != null)
        {
            agentMover.SetPath(PathfindingUtility.BuildPathTo(transform.position, target, gameObject));
            while (agentMover.IsMoving()) yield return null;
        }
        gameObject.SetActive(false);
    }

    // --- Методы состояния ---

    public virtual bool IsOnBreak() => isOnBreak;

    public virtual void GoToBreak(float duration)
    {
        if (IsOnBreak()) return;
        isOnBreak = true;
        breakStartTime = Time.time;
        breakDuration = duration;
    }

    protected virtual void EndBreak()
    {
        isOnBreak = false;
        if (thoughtBubble != null)
        {
            thoughtBubble.ShowPriorityMessage("Перерыв окончен", 2f, Color.white);
        }
    }

    public bool IsOnDuty()
    {
        if (TimeManager.Instance == null) return true;
        var currentPeriod = TimeManager.Instance.GetCurrentPeriodType();
        bool result = (WorkShiftMask & currentPeriod) != 0 && !IsOnBreak();
        return result;
    }

    // --- Вспомогательные методы ---

    public virtual IEnumerator MoveToTarget(Vector3 targetPosition, string stateOnArrival)
    {
        if (agentMover != null)
        {
            agentMover.SetPath(PathfindingUtility.BuildPathTo(transform.position, targetPosition, gameObject));
            while (agentMover.IsMoving()) yield return null;
        }

        // Устанавливаем состояние после прибытия (виртуальный метод для поддержки разных enum'ов)
        SetArrivalState(stateOnArrival);

        yield return null;
    }

    protected virtual void SetArrivalState(string stateName)
    {
        // Базовый класс не имеет enum состояний - только логируем
        logger?.LogState(stateName ?? "Idle");
    }
    
    // Перегрузка для удобства
    public virtual IEnumerator MoveToTarget(Vector2 targetPosition, string stateOnArrival) => MoveToTarget((Vector3)targetPosition, stateOnArrival);

    public virtual string GetStatusInfo() => "Idle";
    public virtual string GetCurrentStateName() => "Idle";
    public virtual float GetCurrentFrustration() => frustration;
    public void ChangeEnergy(float amount) => energy = Mathf.Clamp(energy + amount, 0, 100);
    public void ChangeStress(float amount) => stress = Mathf.Clamp(stress + amount, 0, 100);
    public void SetCurrentFrustration(float val) => frustration = val;

    protected virtual void CalculateArrivalTime()
    {
        // Простая заглушка, реальная логика в расширениях или наследниках
    }

    protected float CalculateLateness() => 0f; 
    protected float CalculateEarlyLeave() => 0f;

    protected virtual void UpdateBreakLogic()
    {
        // Базовая логика перерывов (можно вызвать extension method если нужно)
    }

    public virtual void Initialize(string name, Role role, RankData rank, Gender gender, CharacterSkillsWrapper skills)
    {
        this.characterName = name;
        this.role = role;
        this.currentRankData = rank;
        this.gender = gender;
        this.skills = skills ?? new CharacterSkillsWrapper();
    }

    public virtual void InitializeFromData(RoleData data)
    {
        this.roleData = data;
        if (data != null && agentMover != null) { agentMover.moveSpeed = data.moveSpeed; agentMover.priority = data.priority; }
        if (data != null && visuals != null) visuals.SetupFromRoleData(data, gender);
    }
    
    public void ForceInitializeBaseComponents(AgentMover mover, CharacterVisuals vis, CharacterStateLogger log) 
    { 
        agentMover = mover;
        visuals = vis;
        logger = log;
    }

    public void AddExperienceAndCheckForPromotion(float amount) { experiencePoints += amount; }
    
    public void FireAndGoHome()
    {
        if (assignedWorkstation != null)
        {
            assignedWorkstation.ClearAssignedStaff();
            if (AssignmentManager.Instance != null)
            {
                AssignmentManager.Instance.UnassignWorkstation(assignedWorkstation);
            }
        }
        StopAllCoroutines();
        if (HiringManager.Instance != null)
        {
            HiringManager.Instance.RemoveStaff(this);
        }
        Destroy(gameObject);
    }
    
    public void OnActionFinished(StaffAction action = null, bool success = true) { currentExecutor = null; }
    
    public void ExecuteAction(StaffAction action)
    {
        if (action == null) return;
        var executorType = action.GetExecutorType();
        if (executorType != null)
        {
            currentAction = action;
            var executor = gameObject.AddComponent(executorType) as ActionExecutor;
            currentExecutor = executor;
            executor.Execute(this, action);
        }
    }
    
    public Role currentRole 
    { 
        get => role; 
        set => role = value; 
    }
    
    // Свойства для доступа
    public AgentMover AgentMover => agentMover;
    public bool IsMoving => agentMover != null && agentMover.IsMoving();
}