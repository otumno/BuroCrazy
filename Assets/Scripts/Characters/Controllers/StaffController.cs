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
    public Gender gender; // ИСПОЛЬЗУЕМ ВАШ ГЛОБАЛЬНЫЙ ENUM GENDER

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

    [Header("Пунктуальность (расширенная система)")]
    [Tooltip("Педантичность (0-1). Наследуется из навыков или задается отдельно.")]
    [Range(0f, 1f)]
    public float punctuality = 0.5f;

    [Tooltip("Максимальное время опоздания в секундах")]
    public float maxLateness = 30f;

    [Tooltip("Базовый разброс времени опоздания (секунды)")]
    public float latenessVariance = 5f;

    [Header("Время прихода/ухода (runtime)")]
    [Tooltip("Время начала смены")]
    public float shiftStartTime = 0f;

    [Tooltip("Текущее опоздание в секундах")]
    public float currentLateness = 0f;

    [Tooltip("Ранний уход в секундах")]
    public float currentEarlyLeave = 0f;

    [Tooltip("Пришел ли сегодня")]
    public bool hasArrivedToday = false;

    [Tooltip("Ушел ли сегодня")]
    public bool hasLeftToday = false;

    [Header("Обед и перерывы")]
    [Tooltip("Есть ли обед (для стажеров - нет)")]
    public bool hasLunchBreak = true;

    [Tooltip("Продолжительность перерыва в секундах")]
    public float breakDuration = 900f;

    [Tooltip("Время начала перерыва от начала смены")]
    public float breakStartTime = 14400f;

    [Tooltip("Взял ли обед сегодня")]
    public bool HasTakenBreakToday { get; protected set; }

    [Header("Действия")]
    public List<StaffAction> activeActions = new List<StaffAction>();
    public ActionDatabase systemActionDatabase; 
    
    [Header("Компоненты")]
    public AgentMover agentMover; 
    public ThoughtBubbleController thoughtBubble;
    public CharacterStateLogger logger;
    
    // ИСПРАВЛЕНИЕ: Используем CharacterVisuals вместо StaffPrefabReferences
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

    // Для совместимости с Executor-ами
    public StaffAction currentAction; 
    public ActionExecutor currentExecutor; 

    protected virtual void Awake()
    {
        agentMover = GetComponent<AgentMover>();
        thoughtBubble = GetComponent<ThoughtBubbleController>();
        logger = GetComponent<CharacterStateLogger>();
        visuals = GetComponent<CharacterVisuals>();
        if (systemActionDatabase == null)
        {
            systemActionDatabase = Resources.Load<ActionDatabase>("Databases/ActionDatabase");
            if (systemActionDatabase == null)
            {
                Debug.LogWarning($"[StaffController] ActionDatabase не найден для {characterName}");
            }
        }
    }

    public virtual IEnumerator MoveToTarget(Vector3 targetPosition, string stateOnArrival)
    {
        if (agentMover != null) { agentMover.SetPath(PathfindingUtility.BuildPathTo(transform.position, targetPosition, gameObject)); while (agentMover.IsMoving()) yield return null; }
        yield return null;
    }
    public virtual IEnumerator MoveToTarget(Vector2 targetPosition, string stateOnArrival) => MoveToTarget((Vector3)targetPosition, stateOnArrival);

    public virtual string GetStatusInfo() => "Idle";
    public virtual string GetCurrentStateName() => "Idle";
    public virtual float GetCurrentFrustration() => frustration;

    public void ChangeEnergy(float amount) => energy = Mathf.Clamp(energy + amount, 0, 100);
    public void ChangeStress(float amount) => stress = Mathf.Clamp(stress + amount, 0, 100);
    public void SetCurrentFrustration(float val) => frustration = val;

    public virtual void StartShift()
    {
        if (thoughtBubble) thoughtBubble.ShowPriorityMessage("На работу!", 2f, Color.white);

        hasArrivedToday = false;
        hasLeftToday = false;
        currentLateness = 0f;
        currentEarlyLeave = 0f;
        HasTakenBreakToday = false;

        CalculateArrivalTime();
    }

    public virtual void EndShift()
    {
        if (thoughtBubble) thoughtBubble.ShowPriorityMessage("Домой...", 2f, Color.white);
        hasLeftToday = true;
        currentEarlyLeave = CalculateEarlyLeave();
    }

    protected virtual void CalculateArrivalTime()
    {
        if (hasArrivedToday) return;

        punctuality = skills.pedantry;

        float lateness = CalculateLateness();
        currentLateness = lateness;

        if (Time.time >= shiftStartTime + lateness)
        {
            hasArrivedToday = true;
            currentLateness = 0f;
        }
    }

    protected float CalculateLateness()
    {
        if (punctuality >= 1f)
        {
            return Random.Range(0f, 2f);
        }

        float noLatenessChance = punctuality;
        float randomValue = Random.value;

        if (randomValue < noLatenessChance)
        {
            return Random.Range(0f, 2f);
        }
        else
        {
            float latenessChance = 1f - noLatenessChance;
            float latenessMultiplier = latenessChance * (1f - punctuality * 0.5f);
            float maxLatenessAdjusted = maxLateness * (1f + latenessMultiplier);

            return Random.Range(0f, maxLatenessAdjusted);
        }
    }

    protected float CalculateEarlyLeave()
    {
        float earlyLeaveChance = (1f - punctuality) * 0.3f;

        if (Random.value > earlyLeaveChance)
        {
            return 0f;
        }

        return currentLateness;
    }

    protected virtual void UpdateBreakLogic()
    {
        if (IsOnBreak()) return;

        if (!hasLunchBreak) return;

        if (TimeManager.Instance == null) return;

        float shiftDuration = 480f; // Значение по умолчанию
        
        if (HiringManager.Instance != null)
        {
            // Попытка найти shiftDuration через reflection
            var shiftDurationField = typeof(HiringManager).GetField("shiftDuration", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (shiftDurationField != null)
            {
                shiftDuration = (float)shiftDurationField.GetValue(HiringManager.Instance);
            }
            else
            {
                // Если поле не найдено, пробуем свойство
                var shiftDurationProp = typeof(HiringManager).GetProperty("shiftDuration", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (shiftDurationProp != null)
                {
                    shiftDuration = (float)shiftDurationProp.GetValue(HiringManager.Instance);
                }
            }
        }

        if (shiftDuration <= 0) shiftDuration = 480f;

        float timeInShift = Time.time - shiftStartTime;
        float lunchTime = shiftDuration / 2f;

        if (timeInShift >= lunchTime && !HasTakenBreakToday)
        {
            GoToBreak(breakDuration);
            HasTakenBreakToday = true;
        }
    }

    [Header("Перерывы")]
    public bool isOnBreak = false;

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
        return (WorkShiftMask & currentPeriod) != 0 && !IsOnBreak();
    }

    public virtual void Initialize(string name, Role role, RankData rank, Gender gender, CharacterSkillsWrapper skills)
    {
        this.characterName = name;
        this.role = role;
        this.currentRankData = rank;
        this.gender = gender;
        this.skills = skills ?? new CharacterSkillsWrapper();
    }

    public void Initialize(string name, Role role, int rankLevel, Gender gender, CharacterSkillsWrapper skills) 
    {
         this.characterName = name;
         this.role = role;
         this.gender = gender;
         this.skills = skills;
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
    public void ForceInitializeBaseComponents(string name, Role role, RankData rank) 
    { 
        this.characterName = name; this.role = role; this.currentRankData = rank; Awake(); 
    }

    public void AddExperienceAndCheckForPromotion(float amount) { experiencePoints += amount; }
    
    public void FireAndGoHome()
    {
        // Очистка перед уничтожением
        if (assignedWorkstation != null)
        {
            assignedWorkstation.ClearAssignedStaff();
            if (AssignmentManager.Instance != null)
            {
                AssignmentManager.Instance.UnassignWorkstation(assignedWorkstation);
            }
        }
        
        // Остановка всех корутин
        StopAllCoroutines();
        
        // Удаление из HiringManager
        if (HiringManager.Instance != null)
        {
            HiringManager.Instance.RemoveStaff(this);
        }
        
        Destroy(gameObject);
    }
    
    public void OnActionFinished(StaffAction action = null, bool success = true) { }
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
    
    // Свойства для доступа к AgentMover, чтобы исправить ошибки CS1061
    public AgentMover AgentMover => agentMover;
    public bool IsMoving => agentMover != null && agentMover.IsMoving();
}