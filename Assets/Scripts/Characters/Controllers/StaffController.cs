// Assets/Scripts/Characters/Controllers/StaffController.cs
using UnityEngine;
using Gameplay;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using Managers;
using Utilities;
using Data.Calendar;
using Scriptables.Audio;
using Characters;
using Managers.Teletype;
using Enums;

[RequireComponent(typeof(AgentMover))]
[RequireComponent(typeof(CharacterStateLogger))]
[RequireComponent(typeof(ThoughtBubbleController))]
[RequireComponent(typeof(ActionDiary))]
public class StaffController : MonoBehaviour
{
    // === TRAITS SYSTEM ===
    public enum TraitType
    {
        None, Shilo, Gossip, Allergy, Sloppy, Forgetful, Clumsy, Sprinter, Panicky, Loudmouth, ToiletRush
    }

    [System.Serializable]
    public struct TraitStaticData
    {
        public string Name;
        public string Description;
    }

    public static readonly Dictionary<TraitType, TraitStaticData> TraitLibrary = new Dictionary<TraitType, TraitStaticData>
    {
        { TraitType.None, new TraitStaticData { Name = "", Description = "" } },
        { TraitType.Shilo, new TraitStaticData { Name = "Шило", Description = "Не может стоять на месте" } },
        { TraitType.Gossip, new TraitStaticData { Name = "Сплетник", Description = "Любит поболтать при встрече" } },
        { TraitType.Allergy, new TraitStaticData { Name = "Аллергик", Description = "Чихает и расталкивает всех" } },
        { TraitType.Sloppy, new TraitStaticData { Name = "Неряха", Description = "Оставляет после себя лужи" } },
        { TraitType.Forgetful, new TraitStaticData { Name = "Забывчивый", Description = "Иногда теряет цель пути" } },
        { TraitType.Clumsy, new TraitStaticData { Name = "Неуклюжий", Description = "Падает на ровном месте" } },
        { TraitType.Sprinter, new TraitStaticData { Name = "Спринтер", Description = "Бежит быстро, но недолго" } },
        { TraitType.Panicky, new TraitStaticData { Name = "Паникёр", Description = "Ускоряется при скоплении людей" } },
        { TraitType.Loudmouth, new TraitStaticData { Name = "Несдержанный", Description = "Внезапно кричит от стресса" } },
        { TraitType.ToiletRush, new TraitStaticData { Name = "Туалетная Тревога", Description = "Бежит в туалет быстрее" } }
    };

    public bool HasTrait(TraitType type) => permanentTrait == type;
    
    public enum Role
    {
        Unassigned, Intern, Registrar, Cashier, Archivist, Guard, Janitor, Clerk, OfficeManager, Accountant, ServiceWorker, Director
    }

    [System.Serializable]
    public class StaffNameData
    {
        public string firstName;
        public string lastName;
        public string patronymic;
        public string shortName;
        public string diminutiveName;

        public string GetDisplayName(StaffController.Role role, Enums.Gender gender)
        {
            string elderPrefix = (gender == Enums.Gender.Male) ? "Дядя" : "Тётя";
            switch (role)
            {
                case StaffController.Role.Janitor:
                case StaffController.Role.ServiceWorker: return $"{elderPrefix} {shortName}";
                case StaffController.Role.Guard: return shortName;
                case StaffController.Role.Intern: return diminutiveName;
                case StaffController.Role.Clerk:
                case StaffController.Role.OfficeManager:
                case StaffController.Role.Archivist: return $"{firstName} {lastName}";
                case StaffController.Role.Cashier:
                case StaffController.Role.Accountant:
                case StaffController.Role.Registrar:
                case StaffController.Role.Director: return $"{firstName} {patronymic}";
                default: return $"{firstName} {lastName}";
            }
        }
    }

    [Header("Базовые настройки")]
    public StaffNameData nameData;
    public string characterName => nameData != null ? nameData.GetDisplayName(currentRole, gender) : "Сотрудник";
    public Role role = Role.Unassigned;
    
    // === Для отладки и мониторинга ===
    /// <summary>Текущий подстатус для отображения в дебаггере (например, фазы регистрации)</summary>
    [HideInInspector] public string CurrentSubStatus = "";
    // ================================
    public RoleData roleData;
    public Gender gender;
    
    [Header("Особенности")]
    public TraitType permanentTrait = TraitType.None;

    // Префабы перенесены в StaffPrefabReferences для защиты от удаления при смене роли

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

    [Header("Статистика посещаемости")]
    public int totalLatenessCount = 0;
    public int sickDaysCount = 0;

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

    // === UTILITY AI DEBUG DATA ===
    [System.Serializable]
    public class UtilityDebugData
    {
        public string ActionName;
        public string AssetName; // Имя ассета для резервного отображения
        public bool ConditionsMet;
        public float Score;
        public string StatusMessage; // Градация от действия
    }

    [HideInInspector]
    public List<UtilityDebugData> currentBrainDump = new List<UtilityDebugData>();

    // === TASK DIARY (ДНЕВНИК ЗАДАЧ) ===
    [System.Serializable]
    public class TaskDiaryEntry
    {
        public string TaskName;
        public float StartTime;
        public float EndTime;
        
        public TaskDiaryEntry(string taskName, float startTime)
        {
            TaskName = taskName;
            StartTime = startTime;
            EndTime = 0f;
        }
        
        public float Duration => EndTime > 0f ? EndTime - StartTime : 0f;
        public bool IsCompleted => EndTime > 0f;
    }
    
    [HideInInspector]
    public List<TaskDiaryEntry> taskDiary = new List<TaskDiaryEntry>();
    public TaskDiaryEntry CurrentTaskEntry { get; private set; }
    
    /// <summary>
    /// Начинает запись новой задачи в дневнике
    /// </summary>
    public void LogTaskStart(string taskName)
    {
        // Завершаем текущую задачу если есть
        if (CurrentTaskEntry != null && !CurrentTaskEntry.IsCompleted)
        {
            LogTaskEnd();
        }
        
        CurrentTaskEntry = new TaskDiaryEntry(taskName, Time.time);
        taskDiary.Add(CurrentTaskEntry);
        Debug.Log($"[TaskDiary] {characterName} START: {taskName}");
    }
    
    /// <summary>
    /// Завершает текущую задачу
    /// </summary>
    public void LogTaskEnd()
    {
        if (CurrentTaskEntry != null && !CurrentTaskEntry.IsCompleted)
        {
            CurrentTaskEntry.EndTime = Time.time;
            Debug.Log($"[TaskDiary] {characterName} END: {CurrentTaskEntry.TaskName} ({CurrentTaskEntry.Duration:F1}s)");
            CurrentTaskEntry = null;
            
            // Сбрасываем подстатус при завершении задачи
            CurrentSubStatus = "";
        }
    }

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
    public float stressThresholdToilet = 90f;   // 90% стресса - срочно в туалет
    public float stressThresholdBreak = 60f;   // 60% стресса - можно на перерыв
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
    
    // === СИСТЕМА МИКРОМЕНЕДЖМЕНТА ===
    public StaffAction forcedAction; // Принудительное действие от директора

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
        // Логируем начало смены в дневнике задач
        LogTaskStart($"Смена: {role}");
        
        if (thoughtBubble) thoughtBubble.ShowPriorityMessage("На работу!", 2f, Color.white);

        hasArrivedToday = false;
        hasLeftToday = false;
        currentLateness = 0f;
        currentEarlyLeave = 0f;
        HasTakenBreakToday = false;
        
        gameObject.SetActive(true); // Включаем, если был выключен
        
        // Спринтер - повышаем скорость на 30%
        if (permanentTrait == TraitType.Sprinter && agentMover != null)
        {
            agentMover.moveSpeed *= 1.3f;
            if (thoughtBubble != null)
            {
                thoughtBubble.ShowPriorityMessage("Погнали!", 1.5f, Color.yellow);
            }
        }

        CalculateArrivalTime();

        // 1. Попытка авто-назначения (только для ролей, которым нужен стол)
        bool needsDesk = (role == Role.Clerk || role == Role.Registrar || role == Role.Cashier || role == Role.Archivist || role == Role.Accountant);
        if (needsDesk && assignedWorkstation == null)
        {
            AssignmentManager.Instance?.AutoAssignStaff(this);
        }

        // 2. Логика прибытия
        if (assignedWorkstation != null)
        {
            Debug.Log($"[StaffController] {characterName}: Иду на место {assignedWorkstation.name}");
            StartCoroutine(GoToWorkstationRoutine());
        }
        else if (needsDesk)
        {
             // Только если нужен стол, но не нашли - идем в зону ожидания
             Debug.Log($"[StaffController] {characterName}: Нет места, иду в зону ожидания");
             StartCoroutine(GoToHangoutRoutine());
        }
        // Для ролей без стола (Intern, Guard, Janitor, OfficeManager) - сразу начинаем работу

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
        Managers.Teletype.TeletypeManager.Instance?.LogStaffWork(characterName, role.ToString(), isStartShift: true);
    }

    // --- AI Loop ---
    // --- ПЕРЕРЫВЫ И ПОТРЕБНОСТИ ---
    // Примечание: stress используется как 0-100f (публичное поле в состоянии)
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
            yield return new WaitForSeconds(1f);
            
            // === МЕТАБОЛИЗМ (каждую секунду) ===
            var aiConfig = AIBalanceConfig.Instance;
            if (aiConfig != null)
            {
                // Energy: падает, модификатор - resilience
                float energyResilience = 1f - (skills.sedentaryResilience * 0.5f);
                float energyDelta = aiConfig.baseEnergyLoss * energyResilience * Time.deltaTime;
                energyDelta = ApplyTraitModifiers("Energy", energyDelta);
                energy = Mathf.Clamp(energy - energyDelta, 0f, 100f);
                
                // Bladder: растет, модификатор - resilience
                float bladderResilience = 1f - (skills.sedentaryResilience * 0.5f);
                float bladderDelta = aiConfig.baseBladderGain * bladderResilience * Time.deltaTime;
                bladderDelta = ApplyTraitModifiers("Bladder", bladderDelta);
                bladder = Mathf.Clamp(bladder + bladderDelta, 0f, 100f);
                
                // Morale: падает, интроверты теряют медленнее
                float moraleResilience = 1f - (skills.softSkills * 0.3f);
                float moraleDelta = aiConfig.baseMoraleLoss * moraleResilience * Time.deltaTime;
                moraleDelta = ApplyTraitModifiers("Morale", moraleDelta);
                morale = Mathf.Clamp(morale - moraleDelta, 0f, 100f);
                
                // Stress: базовая скорость + бонус от мусора
                float stressDelta = aiConfig.baseStressGain * Time.deltaTime;
                
                // Проверка мусора рядом
                bool hasMess = false;
                if (MessManager.Instance != null)
                {
                    var messList = MessManager.Instance.GetSortedMessList(transform.position);
                    hasMess = messList != null && messList.Any(m => Vector2.Distance(transform.position, m.transform.position) < 5f);
                }
                
                if (hasMess && skills.pedantry > 0.5f)
                {
                    stressDelta *= aiConfig.messStressMultiplier;
                }
                stressDelta = ApplyTraitModifiers("Stress", stressDelta);
                stress = Mathf.Clamp(stress + stressDelta, 0f, 100f);
            }
            
            // === ОБРАБОТКА ТРЕЙТОВ ===
            ProcessTraitBehavior();

            // 2. Если заняты делом - проверяем можно ли прервать
            if (currentExecutor != null)
            {
                // Если действие НЕльзя прервать - пропускаем выбор
                if (!currentExecutor.IsInterruptible)
                    continue;
            }
            
            // Если идем или на перерыве - пропускаем
            if ((agentMover != null && agentMover.IsMoving()) || IsOnBreak())
                continue;

            // 3. Авто-поиск стола, если его нет
            // --- ИСПРАВЛЕНО: Только клерки и бухгалтеры нуждаются в столах ---
            bool needsDesk = (role == Role.Clerk || role == Role.Registrar || role == Role.Cashier || role == Role.Archivist || role == Role.Accountant);
            
            if (assignedWorkstation == null && needsDesk)
            {
                if (AssignmentManager.Instance != null && AssignmentManager.Instance.AutoAssignStaff(this))
                {
                    StartCoroutine(GoToWorkstationRoutine());
                }
                continue;
            }
            
            // 4. Выбор лучшего действия (Utility AI)
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
        
        // Туалетная тревога - повышает шанс перерыва на 50%
        if (permanentTrait == TraitType.ToiletRush)
        {
            chance *= 1.5f;
        }
        
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
            stress = Mathf.Lerp(stress, 0f, Time.deltaTime * 5f);
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
        stress *= 0.7f; // Снимаем 30% стресса
        
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
        if (stress >= stressThresholdToilet &&
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
        stress += amount;
        stress = Mathf.Clamp(stress, 0f, 100f);
    }

    /// <summary>
    /// Виртуальный метод для применения модификаторов от черт характера.
    /// Переопределяется в наследниках для кастомизации метаболизма.
    /// </summary>
    /// <param name="vitalType">Тип потребности: "Energy", "Bladder", "Morale", "Stress"</param>
    /// <param name="baseDelta">Базовая дельта изменения</param>
    /// <returns>Модифицированная дельта</returns>
    protected virtual float ApplyTraitModifiers(string vitalType, float baseDelta)
    {
        // Модификаторы от черт характера
        switch (permanentTrait)
        {
            case TraitType.Sprinter:
                // Спринтер тратит энергию быстрее на 30%
                if (vitalType == "Energy")
                    return baseDelta * 1.3f;
                break;
            case TraitType.Sloppy:
                // Неряха - немного повышает стресс
                if (vitalType == "Stress")
                    return baseDelta * 1.1f;
                break;
            case TraitType.Clumsy:
                // Неуклюжий получает больше стресса
                if (vitalType == "Stress")
                    return baseDelta * 1.15f;
                break;
            case TraitType.Panicky:
                // Паникёр получает больше стресса
                if (vitalType == "Stress")
                    return baseDelta * 1.25f;
                break;
        }
        
        return baseDelta;
    }

    // === СИСТЕМА МИКРОМЕНЕДЖМЕНТА ===
    /// <summary>
    /// Получить приказ от директора. Принудительно заставляет сотрудника выполнить действие.
    /// </summary>
    public virtual void ReceiveOrder(StaffAction action)
    {
        if (action == null) return;
        
        // Записываем приказ
        forcedAction = action;
        
        // Добавляем стресс от приказа
        stress = Mathf.Clamp(stress + 15f, 0f, 100f);
        
        // Показываем реакцию
        thoughtBubble?.ShowPriorityMessage("Да иду я, иду...", 2f, Color.yellow);
        
        // Если есть выполняемое действие и оно прерываемое - прерываем
        if (currentExecutor != null && currentExecutor.IsInterruptible)
        {
            currentExecutor.Interrupt();
        }
        
        Debug.Log($"[MicroManagement] {characterName} получил приказ: {action.displayName}");
    }

    public float GetCurrentStress() => stress;

    protected void TryPickAction()
    {
        // Очищаем brain dump перед новым решением
        currentBrainDump.Clear();

        // === ГАРАНТИЯ БАЗЫ ДАННЫХ ===
        // Если база не загружена - пробуем загрузить
        if (systemActionDatabase == null)
        {
            systemActionDatabase = Resources.Load<ActionDatabase>("Databases/ActionDatabase");
            if (systemActionDatabase == null)
            {
                Debug.LogWarning($"[StaffController] {characterName}: База действий не найдена! AI не может выбрать действия.");
            }
        }
        
        // Собираем ВСЕ действия: и тактические (назначенные), и системные (базовые нужды)
        var allAvailable = new List<StaffAction>();
        if (activeActions != null) allAvailable.AddRange(activeActions);
        
        // Добавляем системные действия если есть база
        if (systemActionDatabase != null && systemActionDatabase.allActions != null)
            allAvailable.AddRange(systemActionDatabase.allActions);

        StaffAction bestAction = null;
        float highestUtility = -1f;

        foreach (var action in allAvailable)
        {
            if (action == null) continue;

            bool conditionsMet = action.AreConditionsMet(this);
            float utility = 0f;

            if (conditionsMet)
            {
                utility = action.CalculateUtility(this);
                
                // === ПРИНУДИТЕЛЬНОЕ ДЕЙСТВИЕ (МИКРОМЕНЕДЖМЕНТ) ===
                if (forcedAction != null && action == forcedAction)
                {
                    utility = 10000f; // Абсолютный приоритет
                }
                
                // Немного рандома для живости
                utility += Random.Range(0f, 2f);
                
                // --- ИНЕРЦИЯ: +30 очков если это текущее действие ---
                if (currentAction != null && action == currentAction)
                {
                    utility += 30f;
                }
                
                // === ПРИОРИТЕТ РЕГИСТРАТОРА ===
                // Если это Регистратор и в радиусе 2 метров есть клиент
                if (this is ClerkController clerk && clerk.clerkRole == ClerkController.ClerkRole.Registrar)
                {
                    var clientStandPoint = clerk.GetClientStandPoint();
                    if (clientStandPoint != null && Vector2.Distance(transform.position, clientStandPoint.position) <= 2f)
                    {
                        // Проверяем есть ли клиент на точке (не уходит)
                        var clients = FindObjectsOfType<ClientPathfinding>();
                        foreach (var client in clients)
                        {
                            if (client != null && client.stateMachine != null)
                            {
                                var state = client.stateMachine.GetCurrentState();
                                // Проверяем что клиент не уходит
                                bool isLeaving = state.ToString().Contains("Leaving") || state.ToString().Contains("AtCashier");
                                
                                if (!isLeaving && Vector2.Distance(client.transform.position, clientStandPoint.position) < 1.5f)
                                {
                                    // Клиент найден - повышаем приоритет Action_ServiceAtRegistration
                                    if (action.displayName.Contains("Регистрац") || action.displayName.Contains("ServiceAtRegistration"))
                                    {
                                        utility += 2000f;
                                        Debug.Log($"[RegistrarPriority] {characterName}: Клиент обнаружен, повышаю приоритет регистрации!");
                                    }
                                    break;
                                }
                            }
                        }
                    }
                }

                if (utility > highestUtility)
                {
                    highestUtility = utility;
                    bestAction = action;
                }
            }

            // Записываем в brain dump для отладки
            currentBrainDump.Add(new UtilityDebugData
            {
                ActionName = action != null ? action.displayName : "NULL",
                AssetName = action != null ? action.name : "",
                ConditionsMet = conditionsMet,
                Score = utility,
                StatusMessage = action != null ? action.GetDebugInfo(this) : ""
            });
        }

        if (bestAction != null)
        {
            // --- ПРОВЕРКА НА ПРЕРЫВАНИЕ ---
            // Если лучшее действие = текущему, не делаем ничего
            if (currentAction != null && bestAction == currentAction)
            {
                return; // Остаемся на текущем действии
            }
            
            // Если есть текущее действие и оно прерываемое - прерываем
            if (currentExecutor != null && currentExecutor.IsInterruptible)
            {
                currentExecutor.Interrupt();
            }
            
            // Выполняем новое действие
            // --- ЖИВЫЕ РЕАКЦИИ ПЕРЕД ВЫПОЛНЕНИЕМ ---
            if (bestAction is Action_GoToToilet && highestUtility > 80f)
            {
                thoughtBubble?.ShowPriorityMessage("Ой-ой, срочно отлучусь!", 3f, Color.yellow);
            }
            else if (bestAction.actionType == ActionType.ServiceAtRegistration || bestAction.actionType == ActionType.ProcessDocumentCat1)
            {
                if (highestUtility > 100f) 
                {
                    thoughtBubble?.ShowPriorityMessage("Ужас, какая толпа! Работаю!", 2f, new Color(1f, 0.4f, 0.4f));
                }
            }
            else if (bestAction is Action_GoToCooler)
            {
                thoughtBubble?.ShowPriorityMessage("Горло пересохло...", 2f, Color.cyan);
            }
            else if (bestAction is SortPapersAction)
            {
                if (Random.value < 0.3f) 
                    thoughtBubble?.ShowPriorityMessage("Пока никого нет...", 2f, Color.gray);
            }

            Debug.Log($"[Utility AI] {characterName} выбрал {bestAction.displayName} (Вес: {highestUtility:F1})");
            
            // Логируем начало нового действия в дневнике задач (с защитой от пустого имени)
            string taskNameToLog = string.IsNullOrEmpty(bestAction.displayName) ? $"[{bestAction.name}]" : bestAction.displayName;
            LogTaskStart(taskNameToLog);
            
            ExecuteAction(bestAction);
            
            // Сбрасываем приказ после выполнения (одноразовый приказ)
            forcedAction = null;
        }
        else
        {
            if (Random.value < 0.05f)
            {
                // --- ИСПРАВЛЕНО: Только клерки и бухгалтеры нуждаются в столах ---
                bool needsDesk = (role == Role.Clerk || role == Role.Registrar || role == Role.Cashier || role == Role.Archivist || role == Role.Accountant);
                if (assignedWorkstation == null && needsDesk)
                    thoughtBubble?.ShowPriorityMessage("Мне негде работать...", 3f, Color.red);
                else
                    thoughtBubble?.ShowPriorityMessage("Что бы поделать?", 2f, Color.gray);
            }
        }
    }
    
    public virtual void EndShift()
    {
        // Логируем окончание смены в дневнике задач
        LogTaskEnd();
        
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
    
    /// <summary>
    /// Показывает визуальный эффект успеха/неудачи над головой персонажа.
    /// Директор переопределит этот метод как пустой.
    /// </summary>
    public virtual void ShowActionEffect(bool success)
    {
        var refs = GetComponent<StaffPrefabReferences>();
        if (refs == null) return;

        GameObject prefab = success ? refs.successEffectPrefab : refs.failureEffectPrefab;
        Transform fixedSpawnPoint = success ? refs.successSpawnPoint : refs.failureSpawnPoint;
        
        // Берем точку из сейфа. Если забыли назначить - фоллбэк над головой
        Vector3 finalSpawnPos = fixedSpawnPoint != null ? fixedSpawnPoint.position : (transform.position + Vector3.up * 1.0f);

        // FIXED: Force spawn slightly in front of the worker on Z axis to guarantee visibility even without correct Sorting Layer order
        finalSpawnPos.z -= 0.1f;

        if (prefab != null)
        {
            // 1. ПЛЕЙ ЗВУК
            Scriptables.Audio.SoundID soundIDToPlay = success ? refs.successSoundID : refs.failureSoundID;
            if (soundIDToPlay != Scriptables.Audio.SoundID.None && Managers.AudioManager.Instance != null)
            {
                // Используем 3D звук в позиции работника
                Managers.AudioManager.Instance.PlaySound(soundIDToPlay, transform.position);
            }

            // 2. Спавним в мировых координатах в фиксированной точке (со сдвигом по Z)
            GameObject effect = Instantiate(prefab, finalSpawnPos, Quaternion.identity);
            effect.transform.SetParent(null); // Отвязываем, чтобы клерк мог уйти

            // Принудительно выводим поверх всех слоев
            SpriteRenderer[] renderers = effect.GetComponentsInChildren<SpriteRenderer>();
            foreach (var sr in renderers)
            {
                sr.sortingLayerName = "UI";
                sr.sortingOrder = 32000;
            }

            // 3. Устанавливаем жесткое направление полета (плюс вверх, минус вниз)
            ActionFeedbackIcon iconScript = effect.GetComponent<ActionFeedbackIcon>();
            if (iconScript != null)
            {
                iconScript.SetFixedDirection(success ? Vector3.up : Vector3.down);
            }
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
        // Сотрудник считается "на смене", пока он физически не ушел домой
        // Теперь AI сам решит, когда уйти (на основе Action_EndShift)
        return !hasLeftToday;
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
        // characterName is now computed dynamically from nameData
        // For backward compatibility, create a basic nameData from the name string
        this.nameData = new StaffNameData
        {
            firstName = name,
            lastName = "",
            patronymic = "",
            shortName = name,
            diminutiveName = name
        };
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
        
        // --- Логирование начала задачи в ActionDiary ---
        GetComponent<ActionDiary>()?.LogEvent($"Начал задачу: {action.displayName}");
        
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
    
    // === МЕТОДЫ ОБРАБОТКИ ТРЕЙТОВ ===
    
    private float _traitTimer;
    private float _distanceAccumulated;
    
    public void ProcessTraitBehavior()
    {
        if (permanentTrait == TraitType.None) return;
        
        _traitTimer += Time.deltaTime;
        
        var aiConfig = AIBalanceConfig.Instance;
        
        // === ОБРАБОТКА ПО ТАЙМЕРУ (для персонажей за столами - Allergy, Loudmouth) ===
        // Эти трейты должны работать ВСЕГДА, не только при движении
        float checkInterval = aiConfig != null ? aiConfig.traitCheckInterval : 250f;
        if (_traitTimer >= checkInterval)
        {
            _traitTimer = 0f;
            
            // Аллергик - чихает редко
            float allergyChance = aiConfig != null ? aiConfig.allergyChance : 0.3f;
            if (permanentTrait == TraitType.Allergy && Random.value < allergyChance)
            {
                ProcessAllergyTrait();
                return;
            }
            // Несдержанный - каждые traitCheckInterval проверяем
            float loudmouthChance = aiConfig != null ? aiConfig.loudmouthChance : 0.3f;
            if (permanentTrait == TraitType.Loudmouth && Random.value < loudmouthChance)
            {
                ProcessLoudmouthTrait();
                return;
            }
        }
        
        // === ОБРАБОТКА ПО ДИСТАНЦИИ (для движущихся - Sloppy, Clumsy) ===
        if (agentMover != null && agentMover.IsMoving())
        {
            Rigidbody2D rb = GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                _distanceAccumulated += rb.linearVelocity.magnitude * Time.deltaTime;
            }
        }
        
        switch (permanentTrait)
        {
            case TraitType.Gossip:
                ProcessGossipTrait();
                break;
            case TraitType.Allergy:
                ProcessAllergyTrait();
                break;
            case TraitType.Sloppy:
                ProcessSloppyTrait();
                break;
            case TraitType.Forgetful:
                ProcessForgetfulTrait();
                break;
            case TraitType.Clumsy:
                ProcessClumsyTrait();
                break;
            case TraitType.Sprinter:
                ProcessSprinterTrait();
                break;
            case TraitType.Shilo:
                ProcessShiloTrait();
                break;
            case TraitType.Panicky:
                ProcessPanickyTrait();
                break;
            case TraitType.Loudmouth:
                ProcessLoudmouthTrait();
                break;
            case TraitType.ToiletRush:
                ProcessToiletRushTrait();
                break;
        }
    }
    
    private void ProcessGossipTrait()
    {
        var aiConfig = AIBalanceConfig.Instance;
        float gossipCooldown = aiConfig != null ? aiConfig.gossipCooldown : 20f;
        
        // Каждые gossipCooldown секунд при встрече с другим сотрудником - болтовня
        if (_traitTimer >= gossipCooldown)
        {
            _traitTimer = 0f;
            
            // Проверяем сотрудников в радиусе 1.5f через слой Staff
            int staffLayer = LayerMask.NameToLayer("Staff");
            if (staffLayer == -1) staffLayer = 0; // Фолбек на дефолтный слой
            
            var colliders = Physics2D.OverlapCircleAll(transform.position, 1.5f, 1 << staffLayer);
            foreach (var col in colliders)
            {
                if (col.gameObject == gameObject) continue;
                
                var otherStaff = col.GetComponent<StaffController>();
                if (otherStaff != null)
                {
                    // Проверяем что другой сотрудник тоже движется
                    var otherMover = otherStaff.GetComponent<AgentMover>();
                    if (otherMover != null && otherMover.IsMoving())
                    {
                        if (thoughtBubble != null)
                        {
                            thoughtBubble.ShowPriorityMessage("Слышали новость?", 3f, Color.cyan);
                        }
                        
                        // Останавливаем обоих на 3 секунды
                        if (agentMover != null) agentMover.Stop();
                        otherMover.Stop();
                        
                        Debug.Log($"[Trait] {characterName} сплетничает с {otherStaff.characterName}");
                        break;
                    }
                }
            }
        }
    }
    
    private void ProcessSloppyTrait()
    {
        var aiConfig = AIBalanceConfig.Instance;
        float sloppyDistance = aiConfig != null ? aiConfig.sloppyDistance : 0.5f;
        float sloppyChance = aiConfig != null ? aiConfig.sloppyChance : 0.2f;
        
        // Срабатывание каждые sloppyDistance единиц дистанции
        if (_distanceAccumulated >= sloppyDistance)
        {
            _distanceAccumulated = 0f; // Сброс после срабатывания
            
            // sloppyChance шанс создать лужу
            if (Random.value < sloppyChance)
            {
                if (thoughtBubble != null)
                {
                    thoughtBubble.ShowPriorityMessage("Ой... Упс!", 3f, Color.gray);
                }
                
                // Проверяем лимит MessManager перед спавном
                if (MessManager.Instance != null && !MessManager.Instance.CanCreateMess())
                {
                    Debug.Log($"[Trait] {characterName} (Неряха) хотел создать беспорядок, но лимит исчерпан");
                    return;
                }
                
                // Спавн лужи или мусора
                try {
                    bool createPuddle = Random.value < 0.5f;
                    GameObject prefabToSpawn = null;
                    string messType = "";
                    
                    // Берем префабы напрямую из сейфа (StaffPrefabReferences)
                    var refs = GetComponent<StaffPrefabReferences>();
                    if (createPuddle) {
                        prefabToSpawn = refs != null ? refs.puddlePrefab : null;
                        messType = "лужу";
                    } else {
                        prefabToSpawn = refs != null ? refs.trashPrefab : null;
                        messType = "мусор";
                    }
                    
                    // Fallback: если поля не назначены, пробуем Resources.Load
                    if (prefabToSpawn == null) {
                        if (createPuddle) {
                            prefabToSpawn = Resources.Load<GameObject>("Prefabs/Whater1_0");
                        } else {
                            prefabToSpawn = Resources.Load<GameObject>("Prefabs/Trash_Object1");
                        }
                    }
                    
                    if (prefabToSpawn != null) {
                        Instantiate(prefabToSpawn, transform.position, Quaternion.identity);
                        Debug.Log($"[Trait] {characterName} (Неряха) создал {messType}!");
                    } else {
                        Debug.LogWarning($"[Sloppy] Не удалось найти префаб для {messType}");
                    }
                } catch (System.Exception e) {
                    Debug.LogWarning($"[Sloppy] Не удалось создать лужу/мусор: {e.Message}");
                }
            }
        }
    }
    
    private void ProcessClumsyTrait()
    {
        var aiConfig = AIBalanceConfig.Instance;
        float clumsyDistance = aiConfig != null ? aiConfig.clumsyDistance : 3.0f;
        float clumsyChance = aiConfig != null ? aiConfig.clumsyChance : 0.2f;
        
        // Срабатывание каждые clumsyDistance метра
        if (_distanceAccumulated >= clumsyDistance)
        {
            _distanceAccumulated = 0f; // Сброс после срабатывания
            
            // clumsyChance шанс поскользнуться
            if (Random.value < clumsyChance && agentMover != null && agentMover.IsMoving())
            {
                if (thoughtBubble != null)
                {
                    thoughtBubble.ShowPriorityMessage("БРЯК!", 3f, Color.red);
                }
                
                // Вызов SlipAndRecover
                agentMover.SlipAndRecover();
                
                Debug.Log($"[Trait] {characterName} (Неуклюжий) поскользнулся!");
                
                // --- Логирование в ActionDiary ---
                GetComponent<ActionDiary>()?.LogEvent("Сработал трейт: Неуклюжий (упал)");
            }
        }
    }
    
    private void ProcessSprinterTrait()
    {
        var aiConfig = AIBalanceConfig.Instance;
        float sprinterDistance = aiConfig != null ? aiConfig.sprinterDistance : 8.0f;
        
        // Срабатывание каждые sprinterDistance единиц дистанции
        if (_distanceAccumulated >= sprinterDistance + Random.Range(0f, 2f))
        {
            _distanceAccumulated = 0f;
            
            // Остановка на 2 секунды
            agentMover.Stop();
            
            if (thoughtBubble != null)
            {
                thoughtBubble.ShowPriorityMessage("Ффух...", 1.5f, Color.cyan);
            }
            
            // Возврат скорости к норме (если была увеличена)
            StartCoroutine(ResetSprinterSpeed());
            
            Debug.Log($"[Trait] {characterName} (Спринтер) остановился передохнуть");
            
            // --- Логирование в ActionDiary ---
            GetComponent<ActionDiary>()?.LogEvent("Сработал трейт: Спринтер (передохнул)");
        }
    }
    
    private IEnumerator ResetSprinterSpeed()
    {
        yield return new WaitForSeconds(2f);
        // Скорость вернётся к норме благодаря перезапуску смены или другому механизму
    }
    
    private void ProcessToiletRushTrait()
    {
        // Туалетная тревога - обрабатывается в ShouldTakeBreak() - уменьшаем порог
    }
    
    private void ProcessAllergyTrait()
    {
        var aiConfig = AIBalanceConfig.Instance;
        float allergyRadius = aiConfig != null ? aiConfig.allergyRadius : 2.5f;
        float allergyPushForce = aiConfig != null ? aiConfig.allergyPushForce : 200f;
        
        // Показываем бабл "А-аааПЧХИИИ!" на короткое время
        if (thoughtBubble != null)
        {
            thoughtBubble.ShowPriorityMessage("А-аааПЧХИИИ!", 0.8f, Color.white);
        }
        
        // Звук чихания
        AudioManager.Instance?.PlaySound(Scriptables.Audio.SoundID.Sneeze, transform.position);
        
        // Отталкиваем всех вокруг (радиус allergyRadius, сила allergyPushForce)
        var colliders = Physics2D.OverlapCircleAll(transform.position, allergyRadius);
        foreach (var col in colliders)
        {
            if (col.gameObject == gameObject) continue;
            
            Rigidbody2D rb = col.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                Vector2 pushDirection = (col.transform.position - transform.position).normalized;
                Vector2 originalPos = col.transform.position;
                
                // Фаза 1: отталкиваем
                rb.AddForce(pushDirection * 5f, ForceMode2D.Impulse); // было 20f, уменьшили в 4 раза
                
                // Фаза 2: на резинке возвращаем обратно через 0.4 сек
                StartCoroutine(ElasticReturnRoutine(rb, originalPos, pushDirection));
            }
        }
        
        Debug.Log($"[Trait] {characterName} чихнул и оттолкнул окружающих!");
    }
    
    private IEnumerator ElasticReturnRoutine(Rigidbody2D rb, Vector2 originalPos, Vector2 pushDir)
    {
        yield return new WaitForSeconds(0.4f);
        if (rb == null) yield break;
        
        // Резинка: возвращаем с силой 30% от начального отталкивания, направление противоположное
        Vector2 pullDirection = -pushDir;
        rb.AddForce(pullDirection * 6f, ForceMode2D.Impulse);
    }
    
    private void ProcessForgetfulTrait()
    {
        // 0.1% шанс в секунду при ходьбе забыть цель
        if (agentMover.IsMoving() && Random.value < 0.001f)
        {
            agentMover.Stop();
            
            if (thoughtBubble != null)
            {
                thoughtBubble.ShowPriorityMessage("А я куда?", 2f, Color.yellow);
            }
            
            StartCoroutine(ForgetfulRecover());
        }
    }
    
    private IEnumerator ForgetfulRecover()
    {
        yield return new WaitForSeconds(1f);
        
        // Разворот
        if (agentMover != null)
        {
            Vector3 currentPos = transform.position;
            // Идём назад 2 метра
            Vector2 direction = -transform.right;
            // Попробуем вернуться к цели
            TryPickAction();
        }
        
        if (thoughtBubble != null)
        {
            thoughtBubble.ShowPriorityMessage("А, не забыл!", 2f, Color.green);
        }
    }
    
    private void ProcessShiloTrait()
    {
        // Если персонаж бездельничает и стоит на месте более 5 секунд - начинает бесцельно ходить
        if (currentAction == null && !agentMover.IsMoving() && _traitTimer >= 5f)
        {
            _traitTimer = 0f;
            
            // Генерируем случайную точку в пределах 3-7 метров от текущей позиции
            float distance = Random.Range(3f, 7f);
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            
            Vector3 currentPos = transform.position;
            Vector3 targetPos = currentPos + new Vector3(Mathf.Cos(angle) * distance, Mathf.Sin(angle) * distance, 0);
            
            Debug.Log($"[Trait] {characterName} (Шило) пошёл бесцельно бродить к точке {targetPos}");
            
            // Показываем мысль
            if (thoughtBubble != null)
            {
                thoughtBubble.ShowPriorityMessage("Надо куда-нибудь пойти!", 1.5f, Color.yellow);
            }
            
            // Запускаем перемещение
            StartCoroutine(MoveToTarget(targetPos, "Idle"));
        }
    }
    
    private void ProcessLoudmouthTrait()
    {
        // 1% шанс крика в секунду ВСЕГДА при наличии трейта
        if (Random.value < 0.01f)
        {
            if (thoughtBubble != null)
            {
                thoughtBubble.ShowPriorityMessage("ААААААА!!!", 3f, Color.red);
            }
            
            // Пытаемся воспроизвести крик напрямую без AudioManager
            try {
                AudioClip screamClip = Resources.Load<AudioClip>("Sounds/scream_7");
                if (screamClip != null) {
                    AudioSource.PlayClipAtPoint(screamClip, transform.position);
                } else {
                    // Резервный вариант - просто меняем pitch текущего источника
                    var audioSource = GetComponent<AudioSource>();
                    if (audioSource != null) {
                        audioSource.pitch = 1.5f;
                    }
                }
            } catch (System.Exception e) {
                Debug.LogWarning($"[Loudmouth] Не удалось воспроизвести крик: {e.Message}");
            }
            
            // Кратковременное ускорение на 3 секунды
            if (agentMover != null)
            {
                agentMover.ApplySpeedMultiplier(1.2f);
                StartCoroutine(ResetLoudmouthSpeed());
            }
            
            Debug.Log($"[Trait] {characterName} (Несдержанный) закричал!");
        }
    }
    
    private IEnumerator ResetLoudmouthSpeed()
    {
        yield return new WaitForSeconds(3f);
        agentMover.ApplySpeedMultiplier(1f); // Возврат к норме
    }
    
    private void ProcessPanickyTrait()
    {
        // При скоплении людей (более 3х в радиусе 5м) - убегает
        int nearbyCount = 0;
        var colliders = Physics2D.OverlapCircleAll(transform.position, 5f);
        foreach (var col in colliders)
        {
            if (col.gameObject != gameObject) nearbyCount++;
        }
        
        if (nearbyCount >= 3 && _traitTimer >= 5f) // Раз в 5 секунд максимум
        {
            _traitTimer = 0f;
            
            // Бабл
            if (thoughtBubble != null)
            {
                thoughtBubble.ShowPriorityMessage("ИХ СЛИШКОМ МНОГО!", 1.5f, new Color(1f, 0.5f, 0f)); // Оранжевый
            }
            
            // Бросаем текущее действие
            currentAction = null;
            
            // Ускорение в 1.5 раза
            agentMover.ApplySpeedMultiplier(1.5f);
            
            // Находим случайную точку для побега (пытаемся найти staffHomeZone)
            // Пока просто убегаем в случайном направлении
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float distance = Random.Range(5f, 10f);
            Vector3 targetPos = transform.position + new Vector3(Mathf.Cos(angle) * distance, Mathf.Sin(angle) * distance, 0);
            
            // Останавливаем текущий путь и идём к новой точке
            agentMover.Stop();
            StartCoroutine(MoveToTarget(targetPos, "Idle"));
            
            Debug.Log($"[Trait] {characterName} (Паникёр) сбежал от толпы!");
        }
    }
}