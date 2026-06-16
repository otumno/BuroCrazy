// Файл: ClientPathfinding.cs
using UnityEngine;
using TMPro;
using System.Linq;
using System.Collections.Generic;
using System.Collections;
using Managers;
using Scriptables.Audio;
using DialogueSystem.Data;
using Characters;
using Data;
using Enums;

public class ClientPathfinding : MonoBehaviour
{
    public enum LeaveReason { Normal, Processed, Angry, CalmedDown, Upset, Theft }
    public ClientStateMachine stateMachine;
    public ClientMovement movement;
    public ClientNotification notification;
    public DocumentHolder docHolder;
	
	[Header("Настройки Стресса (Баланс)")]
	   [Tooltip("Множитель скорости стресса, когда клиент стоит в очереди (База = 1.0)")]
	   public float stressMod_Standing = 2.5f;
	   [Tooltip("Множитель скорости, когда клиент сидит (комфорт)")]
	   public float stressMod_Sitting = 1.5f;
	   [Tooltip("Множитель скорости, когда клиент занят делом (идет, в туалете) или обслуживается")]
	   public float stressMod_BusyOrServed = 0.1f;
	   [Tooltip("Сколько стресса добавляет каждая куча мусора/лужа рядом (в секунду)")]
	   public float stressAdd_NearbyMess = 0.5f;
    [Tooltip("Мгновенный стресс при отказе/ошибке (в % от максимума, 0.15 = 15%)")]
    public float stressJump_Refusal = 0.15f;
	
	private float _maxPatienceValue;      // Максимальный "объем" терпения
    private float _currentStressValue = 0f; // Текущий накопленный стресс
	
	public bool IsRemote { get; private set; } // Флаг: это звонок?
    public Sprite iconOverride; // Специальная иконка (телефон)
	private int _remoteLifetimePeriods = 0;
	
	
	public float PatienceHeat 
    {
        get 
        {
            if (reasonForLeaving == LeaveReason.Angry || reasonForLeaving == LeaveReason.Upset) return 1f;
            if (isLeavingSuccessfully) return 0f;
            
            // Защита от деления на 0
            if (_maxPatienceValue <= 0.001f) return 0f;

            return Mathf.Clamp01(_currentStressValue / _maxPatienceValue);
        }
    }
	
	// Таймер для расчета терпения
    private float patienceStartTime;
	
	public float CurrentPatience 
    {
        get 
        {
            if (reasonForLeaving == LeaveReason.Angry || reasonForLeaving == LeaveReason.Upset) return 0f;
            float timeAlive = Time.time - patienceStartTime;
            return Mathf.Max(0, totalPatienceTime - timeAlive);
        }
    }

    [Header("Цели и Характер")]
    public ClientGoal mainGoal;
    [Range(0f, 1f)] public float babushkaFactor;
    [Range(0f, 1f)] public float suetunFactor;
    [Range(0f, 1f)] public float prolazaFactor;
	
	public StaffController assignedHelper = null; // Кто назначен помочь этому клиенту
    
    [Header("Внешний вид")]
    public Gender gender;
    private CharacterVisuals visuals;
	[Tooltip("Скорость анимации ходьбы (сек на кадр). Меньше = быстрее.")]
	public float animationSpeed = 0.3f;
	[Tooltip("Набор спрайтов (одежда) для клиентов")]
	public EmotionSpriteCollection spriteCollection;
	public StateEmotionMap stateEmotionMap;

    [Header("Настройки Терпения")]
    public float totalPatienceTime;
    public float minPatienceTime = 20f;
    public float maxPatienceTime = 45f;

    public bool isLeavingSuccessfully = false;
    public LeaveReason reasonForLeaving = LeaveReason.Normal;
    
    [Header("Service Data")]
    public bool isQueueJumper = false; // Флаг наглеца

    [Header("Grumbling - Промежуточное недовольство")]
    public bool canGrumble = true;
    public float grumblingThreshold = 0.5f;
    public float grumblingFrequency = 0.5f;
    private float _lastGrumbleTime = -10f;
    private const float GRUMBLE_INTERVAL_MIN = 4f;

    /// <summary>
    /// Возвращает эффективный порог ворчания с учётом архетипа.
    /// Суетун: ниже порог (быстрее ворчит)
    /// Бабушка: выше порог (дольше терпит)
    /// </summary>
    public float GetEffectiveGrumblingThreshold()
    {
        float threshold = grumblingThreshold;

        // Суетун быстрее достигает порога ворчания
        if (suetunFactor > 0.5f)
        {
            threshold -= 0.15f * suetunFactor;
        }

        // Бабушка дольше терпит перед ворчанием
        if (babushkaFactor > 0.5f)
        {
            threshold += 0.15f * babushkaFactor;
        }

        return Mathf.Clamp(threshold, 0.2f, 0.9f);
    }

    /// <summary>
    /// Возвращает эффективную частоту ворчания с учётом архетипа.
    /// Суетун: реже ворчит (но быстрее срывается)
    /// Бабушка: чаще ворчит (но терпеливо ждёт)
    /// </summary>
    public float GetEffectiveGrumblingFrequency()
    {
        float frequency = grumblingFrequency;

        // Суетун ворчит реже, но быстрее срывается
        if (suetunFactor > 0.5f)
        {
            frequency -= 0.3f * suetunFactor;
        }

        // Бабушка ворчит чаще, но держится
        if (babushkaFactor > 0.5f)
        {
            frequency += 0.2f * babushkaFactor;
        }

        return Mathf.Clamp(frequency, 0.1f, 1f);
    }

    [Header("Касса")]
    [SerializeField] private int _billToPay = 0;
    public int billToPay
    {
        get => _billToPay;
        set
        {
            int oldBill = _billToPay;
            _billToPay = value;
            
            // Если счет вырос (нам выставили счет), принимаем решение об оплате
            if (_billToPay > oldBill && _billToPay > 0 && stateMachine != null && stateMachine.GetCurrentState() != ClientState.Leaving)
            {
                TrySteal();
            }
        }
    }
    public GameObject moneyPrefab;
    
    [Header("Создание беспорядка")]
    public List<GameObject> trashPrefabs;
    public List<GameObject> puddlePrefabs;

    [Header("Звуки")]
    public AudioClip spawnSound, exitSound, confusedSound, toiletSound;
    public AudioClip successfulExitSound, dissatisfiedExitSound, helpedByInternSound, paymentSound;
    public AudioClip stampSound;
    public AudioClip impoliteSound;
    public AudioClip theftAttemptSound;
	
	[Header("Аудио")]
    public VoiceData voiceProfile;
    
    public static int totalClients, clientsExited, clientsInWaiting, clientsToToilet, clientsToRegistration, clientsConfused;
    public static int clientsExitedAngry = 0, clientsExitedProcessed = 0;

    [Header("Документы")]
    [Range(0f, 1f)] public float documentQuality;

    [Tooltip("Влияние babushkaFactor на шанс отсутствия документа (0 = всегда есть, 1 = 30% пусто)")]
    [SerializeField, Range(0f, 1f)] private float forgetfulnessFactor = 0.3f;
    [Tooltip("Влияние suetunFactor на шанс неправильного бланка (0 = всегда правильный, 1 = 40% неправильный)")]
    [SerializeField, Range(0f, 1f)] private float confusionFactor = 0.4f;
    
    [Header("Данные документа Директора")]
    public int directorDocumentFee;
    public int directorDocumentBribe;
    public bool hasBeenSentForRevision = false;
	public DirectorDocumentLayout directorDocumentLayout;
	public bool documentChecked = false;
	
	[Header("Сюжет")]
    [Tooltip("Диалог, который запустится, если вызвать этого клиента в кабинет")]
    public DialogueGraph specificDialogue;
    
    public CharacterVisuals GetVisuals() => visuals;
    
    public void Initialize(GameObject wZ, Waypoint eW)
    {
        totalClients++;
        stateMachine = gameObject.GetComponent<ClientStateMachine>();
        movement = gameObject.GetComponent<ClientMovement>();
        var agent = GetComponent<AgentMover>();
        if (agent != null)
        {
            agent.animationSpeed = this.animationSpeed;
        }
        notification = gameObject.GetComponent<ClientNotification>();
        docHolder = gameObject.GetComponent<DocumentHolder>();
        visuals = gameObject.GetComponent<CharacterVisuals>();
        if (stateMachine == null || movement == null || notification == null || docHolder == null || visuals == null)
        {
            Debug.LogError($"Критическая ошибка инициализации на клиенте {gameObject.name}!", gameObject);
            enabled = false;
            return;
        }

        // Если spriteCollection уже был установлен архетипом через SetupFromArchetype(),
        // то НЕ вызываем visuals.Setup() - это перезаписало бы спрайт тела на дефолтный!
        if (this.spriteCollection == null)
        {
            gender = (Random.value > 0.5f) ? Gender.Male : Gender.Female;
            visuals.Setup(gender, this.spriteCollection, this.stateEmotionMap);
        }
        else
        {
            // Архетип уже настроил визуал - просто обновляем анимацию в AgentMover
            var bodyRenderer = visuals.GetBodyRenderer();
            if (bodyRenderer != null && agent != null)
            {
                // AgentMover получит спрайты из CharacterVisuals.SetupVisualDiversity
                }
        }
    
    babushkaFactor = Mathf.RoundToInt(Random.Range(0, 5)) * 0.25f;
        suetunFactor = Mathf.RoundToInt(Random.Range(0, 5)) * 0.25f;
        prolazaFactor = Mathf.RoundToInt(Random.Range(0, 5)) * 0.25f;
        documentQuality = 1.0f - (suetunFactor * 0.5f);
		

		_currentStressValue = 0f;
        
        if (mainGoal == default(ClientGoal))
        {
            // Пытаемся взять цель из текущего архетипа клиента
            var visualsComp = GetComponent<CharacterVisuals>();
            var currentArchetype = visualsComp != null ? visualsComp.currentArchetype : null;

            if (currentArchetype != null && currentArchetype.allowedGoals != null && currentArchetype.allowedGoals.Count > 0)
            {
                mainGoal = currentArchetype.allowedGoals[Random.Range(0, currentArchetype.allowedGoals.Count)];
            }
            else
            {
                var goals = System.Enum.GetValues(typeof(ClientGoal));
                if (goals.Length > 0)
                {
                    mainGoal = (ClientGoal)goals.GetValue(Random.Range(0, goals.Length));
                }
                else
                {
                    mainGoal = ClientGoal.AskAndLeave;
                }
            }
        }

        DocumentType startingDoc = DocumentType.None;

        // Документ на подпись директору: всегда в руках, ошибки живут в directorDocumentLayout (мини-игра)
        if (mainGoal == ClientGoal.DirectorApproval)
        {
            startingDoc = DocumentType.Form1;
        }
        else
        {
            DocumentType requiredDoc = DocumentType.None;
            if (mainGoal == ClientGoal.GetCertificate1) requiredDoc = DocumentType.Form1;
            else if (mainGoal == ClientGoal.GetCertificate2) requiredDoc = DocumentType.Form2;

            if (requiredDoc != DocumentType.None)
            {
                float hasDocumentChance = 1f - babushkaFactor * forgetfulnessFactor;
                if (Random.value < hasDocumentChance)
                {
                    float correctChance = 1f - suetunFactor * confusionFactor;
                    if (Random.value < correctChance)
                    {
                        startingDoc = requiredDoc;
                    }
                    else
                    {
                        if (requiredDoc == DocumentType.Form1) startingDoc = DocumentType.Form2;
                        else if (requiredDoc == DocumentType.Form2) startingDoc = DocumentType.Form1;
                    }
                }
            }
        }

        docHolder.SetDocument(startingDoc);
        
        if (mainGoal == ClientGoal.DirectorApproval)
        {
            directorDocumentFee = Random.Range(250, 751);
            if (Random.value < 0.33f)
            {
                directorDocumentBribe = Random.Range(50, 151);
            }
        }

        stateMachine.Initialize(this);
        
        // --- ПРИНУДИТЕЛЬНАЯ РЕГИСТРАЦИЯ В ОЧЕРЕДИ УБРАНА ---
        // Клиенты теперь получают номерок только в TicketTerminal, не при спавне
        // if (ClientQueueManager.Instance != null && !ClientQueueManager.Instance.queue.ContainsKey(this))
        // {
        //     ClientQueueManager.Instance.JoinQueue(this);
        //     // Debug.Log($"[ClientPathfinding] {gameObject.name}: зарегистрирован в очереди с номером {ClientQueueManager.Instance.queue[this]}");
        // }
        movement.Initialize(this);
        float basePatience = Random.Range(minPatienceTime, maxPatienceTime);
        // Бабушка терпит в 2 раза дольше при факторе 1, суетун ждёт вдвое меньше при факторе 1
        float babushkaMultiplier = 1f + babushkaFactor * 1.5f;
        float suetunMultiplier = 1f - suetunFactor * 0.5f;
        totalPatienceTime = basePatience * babushkaMultiplier * suetunMultiplier;
  
  patienceStartTime = Time.time;
        _maxPatienceValue = totalPatienceTime;

        // Звук спавна перенесен в ClientStateMachine.OnEnteredBuilding()
  
  var overlay = GetComponentInChildren<ClientStatusOverlay>();
    
 		if (overlay != null)
 		{
 			overlay.Initialize(this);
 		}
 			else
 		{
         Debug.LogError($"[ClientPathfinding] Не найден скрипт ClientStatusOverlay в детях объекта {gameObject.name}!");
     }

        // Инициализируем систему приветствий
        InitializeGreetingSystem();

        // Показ мысли при появлении перенесен в ClientStateMachine.OnEnteredBuilding()
     }

    private IEnumerator ShowSpawnThoughtDelayed()
    {
        yield return new WaitForSeconds(0.5f);
        ShowSpawnThought();
    }

    void OnDestroy()
    {
        if (Managers.TimeManager.Instance != null)
        {
            Managers.TimeManager.Instance.OnPeriodChanged -= OnRemotePeriodTick;
        }
	
        if (ClientQueueManager.Instance != null)
        {
            ClientQueueManager.Instance.RemoveClientFromQueue(this);
        }

        StartOfDayPanel.Instance?.RemoveDocumentIcon(this);

        // --- Очистка зон ---
        if (stateMachine != null)
        {
            var zone = stateMachine.GetTargetZone();
            var goal = stateMachine.GetCurrentGoal();
            if (zone != null && goal != null)
            {
                zone.ReleaseWaypoint(goal);
            }
            if (zone != null)
            {
                zone.LeaveQueue(gameObject);
            }
        }
        
        // Очистка из всех зон через FindObjectsOfType если нужно
        var allZones = FindObjectsByType<LimitedCapacityZone>(FindObjectsSortMode.None);
        foreach (var z in allZones)
        {
            z.LeaveQueue(gameObject);
        }
    }

    private Coroutine _directorWaitCoroutine;

    /// <summary>
    /// Запустить таймер ожидания директора/приёма. По истечении клиент уходит со штрафом.
    /// </summary>
    public void StartDirectorWaitTimer()
    {
        if (_directorWaitCoroutine != null) StopCoroutine(_directorWaitCoroutine);
        if (mainGoal != ClientGoal.DirectorApproval && mainGoal != ClientGoal.DirectorAudience) return;
        _directorWaitCoroutine = StartCoroutine(DirectorWaitTimerRoutine());
    }

    public void StopDirectorWaitTimer()
    {
        if (_directorWaitCoroutine != null)
        {
            StopCoroutine(_directorWaitCoroutine);
            _directorWaitCoroutine = null;
        }
    }

    private IEnumerator DirectorWaitTimerRoutine()
    {
        float timeout = 60f;
        if (Managers.WaveManager.Instance != null) timeout = Managers.WaveManager.Instance.directorWaitTimeout;

        yield return new WaitForSeconds(timeout);

        if (this == null || isLeavingSuccessfully) yield break;
        if (mainGoal != ClientGoal.DirectorApproval && mainGoal != ClientGoal.DirectorAudience) yield break;
        if (stateMachine == null) yield break;

        // Клиент устал ждать директора
        Debug.Log($"[DirectorWait] {name} устал ждать директора и уходит");

        Managers.DirectorManager.Instance?.OnDirectorVisitorLeft();

        if (visuals != null) visuals.SetEmotion(Emotion.Angry);
        ShowThoughtBubble("Долго же вас приходится ждать...", 3f);

        // Удаляем иконку со стола директора
        StartOfDayPanel.Instance?.RemoveDocumentIcon(this);

        ForceLeave(LeaveReason.Normal);
    }

	public void InitializeRemoteLifetime(int periods)
    {
        if (periods <= 0) return; // 0 = живет вечно (пока день не кончится)

        _remoteLifetimePeriods = periods;
        
        // Подписываемся на событие смены периода
        if (Managers.TimeManager.Instance != null)
        {
            Managers.TimeManager.Instance.OnPeriodChanged += OnRemotePeriodTick;
        }
    }
	
	private void OnRemotePeriodTick(Data.Calendar.PeriodSettings settings)
    {
        if (isLeavingSuccessfully) return; // Если уже "обслужен", таймер не важен

        _remoteLifetimePeriods--;
        
        if (_remoteLifetimePeriods <= 0)
        {
            // Debug.Log($"[ClientPathfinding] Время ожидания звонка от {name} истекло. Удаляем.");
            
            // Если игрок не ответил, считаем это как Upset (расстроен)
            reasonForLeaving = LeaveReason.Upset;
            
            // Уничтожаем объект -> сработает OnDestroy -> удалится иконка со стола
            Destroy(gameObject);
        }
    }
	

    public void OnClientExit()
    {
        // Сказать прощание перед уходом
        SayGoodbye();

        if (reasonForLeaving == LeaveReason.Angry || reasonForLeaving == LeaveReason.CalmedDown || reasonForLeaving == LeaveReason.Upset || reasonForLeaving == LeaveReason.Theft)
        {
            clientsExitedAngry++;
        }
        else if(isLeavingSuccessfully)
        {
            clientsExitedProcessed++;

            // Ачивки за характер клиента (только при успешном обслуживании)
            if (babushkaFactor > 0.5f)
                Managers.AchievementManager.Instance?.UnlockAchievement("Achv_GrandmaClientStory");
            if (isQueueJumper)
                Managers.AchievementManager.Instance?.UnlockAchievement("Achv_TheFixerStory");
        }
        clientsExited++;
        totalClients--;
        if (exitSound != null) AudioSource.PlayClipAtPoint(exitSound, transform.position);
        Destroy(gameObject);
    }

    public void UnfreezeAndRestartAI() { if(stateMachine != null) stateMachine.StartCoroutine(stateMachine.MainLogicLoop()); }
    
    public void CalmDownAndLeave() { if (stateMachine != null && stateMachine.GetCurrentState() == ClientState.Enraged) { reasonForLeaving = LeaveReason.CalmedDown; stateMachine.SetState(ClientState.Leaving); } }
    
    public void CalmDownAndReturnToQueue()
    {
        if (stateMachine != null && stateMachine.GetCurrentState() == ClientState.Enraged)
        {
            stateMachine.SetGoal(ClientQueueManager.Instance.ChooseNewGoal(this));
            stateMachine.SetState(ClientState.ReturningToWait);
        }
    }

    public void Freeze() { if(stateMachine != null) stateMachine.StopAllActionCoroutines(); GetComponent<AgentMover>()?.Stop(); }
    
    public void ForceLeave(LeaveReason reason = LeaveReason.CalmedDown)
    {
        if (stateMachine.GetCurrentState() == ClientState.Leaving || stateMachine.GetCurrentState() == ClientState.LeavingUpset)
        {
            reasonForLeaving = reason;
            return;
        }

        LimitedCapacityZone currentZone = stateMachine.GetCurrentGoal()?.GetComponentInParent<LimitedCapacityZone>();
        if(currentZone != null)
        {
            currentZone.LeaveQueue(gameObject);
            currentZone.ReleaseWaypoint(stateMachine.GetCurrentGoal());
        }
        ClientQueueManager.Instance.RemoveClientFromQueue(this);
        if(ClientQueueManager.Instance.dissatisfiedClients.Contains(this))
        {
            ClientQueueManager.Instance.dissatisfiedClients.Remove(this);
        }

        reasonForLeaving = reason;
        if (reason == LeaveReason.Angry && !ClientQueueManager.Instance.dissatisfiedClients.Contains(this))
        {
            ClientQueueManager.Instance.dissatisfiedClients.Add(this);
        }
        
        stateMachine.SetGoal(ClientSpawner.Instance.exitWaypoint);
        if (reason == LeaveReason.Upset)
        {
            stateMachine.SetState(ClientState.LeavingUpset);
        }
        else
        {
            stateMachine.SetState(ClientState.Leaving);
        }
    }

    public static ClientPathfinding FindClosestConfusedClient(Vector3 position) 
    { 
        return FindObjectsByType<ClientPathfinding>(FindObjectsSortMode.None)
            .Where(c => c != null && c.stateMachine != null && c.stateMachine.GetCurrentState() == ClientState.Confused)
            .OrderBy(c => Vector3.Distance(c.transform.position, position))
            .FirstOrDefault();
    }
	
	public void AddStress(float amount)
    {
        if (isLeavingSuccessfully) return;
        _currentStressValue += amount;
    }
	
	public void ApplyStressJump(float percent)
    {
        if (isLeavingSuccessfully) return;
        _currentStressValue += _maxPatienceValue * percent;
    }
	
	public void RelieveStress(float percent)
	   {
	       _currentStressValue -= _maxPatienceValue * percent;
	       if (_currentStressValue < 0) _currentStressValue = 0;
	   }

	   /// <summary>
	   /// Уменьшает терпение клиента на указанное количество секунд.
	   /// </summary>
	   /// <param name="seconds">Количество секунд для уменьшения терпения</param>
	   public void DecreasePatience(float seconds)
	   {
	       if (isLeavingSuccessfully) return;
	       if (seconds <= 0) return;
	       
	       float currentPatience = CurrentPatience;
	       if (currentPatience <= 0) return;
	       
	       float newRemainingTime = Mathf.Max(0, currentPatience - seconds);
	       float timePassed = totalPatienceTime - newRemainingTime;
	       patienceStartTime = Time.time - timePassed;
	   }

	   public void InitializeRemote(Sprite icon)
    {
        IsRemote = true;
        iconOverride = icon;

        // Отключаем визуал тела, чтобы его не было видно
        if (visuals != null) visuals.gameObject.SetActive(false);
        
        // Отключаем коллайдеры, чтобы по нему не кликнули случайно
        var col = GetComponent<Collider2D>();
        if (col) col.enabled = false;

        // Отключаем движение
        var mover = GetComponent<AgentMover>();
        if (mover) mover.enabled = false;

    }

    public void SetupGrumblingFromArchetype(ClientArchetype archetype)
    {
        if (archetype == null) return;

        // canGrumble всегда true - архетип определяет порог и частоту
        canGrumble = true;
        grumblingThreshold = archetype.grumblingThreshold;
        grumblingFrequency = archetype.grumblingFrequency;
    }

    public bool ShouldShowGrumble()
    {
        if (!canGrumble) return false;
        if (Time.time - _lastGrumbleTime < GRUMBLE_INTERVAL_MIN) return false;

        _lastGrumbleTime = Time.time;
        return Random.value < GetEffectiveGrumblingFrequency();
    }

    public string GetGrumblingText(ClientArchetype archetype)
    {
        if (archetype != null)
        {
            return archetype.GetGrumblingLine();
        }
        return "Это несносно...";
    }

    /// <summary>
    /// Настроить параметры клиента из архетипа
    /// </summary>
    public void SetupFromArchetype(ClientArchetype archetype)
    {
        if (archetype == null) return;

        // Терпение
        totalPatienceTime = archetype.patience;
        _maxPatienceValue = totalPatienceTime;

        // Скорость
        var mover = GetComponent<AgentMover>();
        if (mover != null)
        {
            mover.moveSpeed = 2f * archetype.speedMultiplier;
        }

        // Grumbling параметры
        grumblingThreshold = archetype.grumblingThreshold;
        grumblingFrequency = archetype.grumblingFrequency;

        // Настройка визуалов CharacterVisuals
        var visuals = GetComponent<CharacterVisuals>();
        if (visuals != null)
        {
            visuals.SetupFromArchetype(archetype);
        }

        // Голосовой профиль
        if (archetype.voiceProfile != null)
        {
            voiceProfile = archetype.voiceProfile;
        }
    }

    // ============================================================================
    // === СИСТЕМА ПРИВЕТСТВИЙ И ОБЩЕНИЯ КЛИЕНТОВ ================================
    // ============================================================================

    [Header("Система общения")]
    [Tooltip("Кого уже поприветствовал (InstanceID)")]
    private HashSet<int> greetedStaff = new HashSet<int>();

    [Tooltip("С кем уже говорил в очереди")]
    private HashSet<int> greetedClients = new HashSet<int>();

    [Tooltip("Время последнего разговора")]
    private float lastChatTime = -30f;

    [Tooltip("Количество разговоров в очереди")]
    private int chatCount = 0;

    [Tooltip("Максимум разговоров за период ожидания")]
    private const int MAX_CHATS_PER_WAIT = 3;

    [Tooltip("Кулдаун между разговорами (секунды)")]
    private const float CHAT_COOLDOWN = 30f;

    [Tooltip("Кулдаун между приветствиями одного типа работника")]
    private const float GREETING_COOLDOWN_PER_ROLE = 120f;

    [Tooltip("Последнее время приветствия по роли")]
    private Dictionary<string, float> lastGreetingByRole = new Dictionary<string, float>();

    private ClientGreetingDatabase greetingDatabase;
    private ThoughtBubbleController thoughtBubble;

    private void InitializeGreetingSystem()
    {
        if (greetingDatabase == null)
        {
            greetingDatabase = Resources.Load<ClientGreetingDatabase>("ClientGreetingDatabase");
        }

        if (thoughtBubble == null)
        {
            thoughtBubble = GetComponent<ThoughtBubbleController>();
        }

        if (greetingDatabase == null)
        {
            Debug.LogWarning($"[{gameObject.name}] ClientGreetingDatabase не найден!");
        }
    }

    /// <summary>
    /// Попытаться поприветствовать работника
    /// </summary>
    public void TryGreetStaff(StaffController staff)
    {
        if (staff == null) return;
        if (greetingDatabase == null) InitializeGreetingSystem();
        if (greetingDatabase == null) return;

        int staffID = staff.GetInstanceID();

        // Уже приветствовал этого работника?
        if (greetedStaff.Contains(staffID)) return;

        // Проверяем роль - приветствуем только определённые
        if (!IsGreetableRole(staff.role)) return;

        // Проверяем кулдаун по роли
        string roleKey = staff.role.ToString();
        if (lastGreetingByRole.TryGetValue(roleKey, out float lastTime))
        {
            if (Time.time - lastTime < GREETING_COOLDOWN_PER_ROLE) return;
        }

        // Получаем приветствие с учётом типа работника и его текущей позиции
        string greeting = GetAppropriateGreeting(staff);

        // Показываем
        ShowThoughtBubble(greeting, 3f);

        // Запоминаем
        greetedStaff.Add(staffID);
        lastGreetingByRole[roleKey] = Time.time;

        var archetype = GetVisuals()?.currentArchetype;
        if (archetype != null)
        {
            // Debug.Log($"[{gameObject.name}] Приветствует {staff.role}: {greeting}");
        }
    }

    /// <summary>
    /// Получить подходящее приветствие с учётом роли и позиции
    /// </summary>
    private string GetAppropriateGreeting(StaffController staff)
    {
        // Если это директор, смотрим где он работает
        if (staff is DirectorAvatarController director)
        {
            var workstation = director.GetWorkstation();
            if (workstation != null)
            {
                // Директор за регистратурой
                if (workstation.deskId == 0)
                    return greetingDatabase.GetGreetingForRole(StaffController.Role.Registrar);
                // Директор за кассой
                if (workstation.deskId == -1)
                    return greetingDatabase.GetGreetingForRole(StaffController.Role.Cashier);
            }
        }
        // Clerk/Casher/etc используют assignedWorkstation
        else if (staff.assignedWorkstation != null)
        {
            if (staff.assignedWorkstation.deskId == 0)
                return greetingDatabase.GetGreetingForRole(StaffController.Role.Registrar);
            if (staff.assignedWorkstation.deskId == -1)
                return greetingDatabase.GetGreetingForRole(StaffController.Role.Cashier);
        }

        // Стандартное приветствие по роли
        return greetingDatabase.GetGreetingForRole(staff.role);
    }

    /// <summary>
    /// Может ли клиент приветствовать эту роль
    /// </summary>
    private bool IsGreetableRole(StaffController.Role role)
    {
        return role == StaffController.Role.Registrar ||
               role == StaffController.Role.Clerk ||
               role == StaffController.Role.Cashier ||
               role == StaffController.Role.Director;
    }

    /// <summary>
    /// Попытаться поговорить с другими клиентами в очереди
    /// </summary>
    public void TryChatWithNearbyClients()
    {
        if (greetingDatabase == null) InitializeGreetingSystem();
        if (greetingDatabase == null) return;

        // Проверяем лимиты
        if (Time.time - lastChatTime < CHAT_COOLDOWN) return;
        if (chatCount >= MAX_CHATS_PER_WAIT) return;

        // Проверяем состояние - только если стоим в очереди
        if (stateMachine == null) return;

        var currentState = stateMachine.GetCurrentState();
        bool isWaiting = currentState == ClientState.SittingInWaitingArea ||
                         currentState == ClientState.AtWaitingArea ||
                         currentState == ClientState.ReturningToWait;

        if (!isWaiting) return;

        // Ищем соседей
        var nearby = FindNearbyClients(3f);
        foreach (var client in nearby)
        {
            if (client == this) continue;
            if (greetedClients.Contains(client.GetInstanceID())) continue;

            // Начинаем разговор
            string message = GetAppropriateSmallTalk();
            ShowThoughtBubble(message, 3f);

            // Запоминаем
            greetedClients.Add(client.GetInstanceID());
            chatCount++;
            lastChatTime = Time.time;

            var archetype = GetVisuals()?.currentArchetype;
            if (archetype != null)
            {
                // Small talk лог убран
            }
            break;
        }
    }

    /// <summary>
    /// Получить подходящую реплику для small talk
    /// </summary>
    private string GetAppropriateSmallTalk()
    {
        var archetype = GetVisuals()?.currentArchetype;
        if (archetype != null)
        {
            // Пробуем архетип-специфичную реплику
            string archetypeTalk = archetype.GetSmallTalk();
            if (archetypeTalk != "...") return archetypeTalk;
        }

        return greetingDatabase.GetRandomSmallTalk();
    }

    /// <summary>
    /// Попрощаться при уходе
    /// </summary>
    public void SayGoodbye()
    {
        if (greetingDatabase == null) InitializeGreetingSystem();
        if (greetingDatabase == null) return;

        string farewell;

        // Определяем тип прощания
        bool isEnraged = reasonForLeaving == LeaveReason.Angry;
        bool isUpset = reasonForLeaving == LeaveReason.Upset ||
                       reasonForLeaving == LeaveReason.CalmedDown ||
                       reasonForLeaving == LeaveReason.Theft;
        bool isHappy = isLeavingSuccessfully && !isUpset && !isEnraged;

        // Для архетипов с gender-specific репликами
        var archetype = GetVisuals()?.currentArchetype;
        if (archetype != null && archetype.useGenderSpecificLines)
        {
            int clientGender = (int)gender;

            if (isEnraged)
                farewell = archetype.GetGrumbling(clientGender);
            else if (isHappy)
                farewell = archetype.GetHappy(clientGender);
            else if (isUpset)
                farewell = archetype.GetSad(clientGender);
            else
                farewell = archetype.GetHappy(clientGender); // Нейтральный, но довольный
        }
        else
        {
            // Универсальные прощания
            farewell = greetingDatabase.GetFarewell(isHappy, isEnraged, isUpset);
        }

        ShowThoughtBubble(farewell, 4f);

        if (archetype != null)
        {
            // Debug.Log($"[{gameObject.name}] Прощание ({reasonForLeaving}): {farewell}");
        }
    }

    /// <summary>
    /// Показать мысль/реплику в пузыре
    /// </summary>
    public void ShowThoughtBubble(string text, float duration)
    {
        if (thoughtBubble == null) InitializeGreetingSystem();
        if (thoughtBubble != null && !string.IsNullOrEmpty(text))
        {
            thoughtBubble.ShowPriorityMessage(text, duration, Color.white);
        }
    }

    /// <summary>
    /// Найти клиентов рядом
    /// </summary>
    private List<ClientPathfinding> FindNearbyClients(float radius)
    {
        var result = new List<ClientPathfinding>();
        var colliders = Physics2D.OverlapCircleAll(transform.position, radius);

        foreach (var collider in colliders)
        {
            var client = collider.GetComponent<ClientPathfinding>();
            if (client != null && client != this && client.stateMachine != null)
            {
                var state = client.stateMachine.GetCurrentState();
                if (state != ClientState.Leaving && state != ClientState.LeavingUpset)
                {
                    result.Add(client);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Показать мысль при появлении
    /// </summary>
    public void ShowSpawnThought()
    {
        var archetype = GetVisuals()?.currentArchetype;
        if (archetype != null)
        {
            string thought = archetype.GetRandomThought();
            ShowThoughtBubble(thought, 4f);
        }
    }

    /// <summary>
    /// Попытка украсть и сбежать вместо оплаты
    /// </summary>
    public void TrySteal()
    {
        // Бабушки никогда не воруют
        if (babushkaFactor > 0.5f) return;
        
        // Шанс кражи зависит от фактора "пролазы" (например, максимум 30% шанс)
        float theftChance = prolazaFactor * 0.3f;
        
        if (Random.value < theftChance)
        {
            // Debug.Log($"[Theft] Клиент {name} решил не платить и сбежать!");
            
            // Сбрасываем очередь и текущие действия
            isQueueJumper = false;
            stateMachine.StopAllActionCoroutines();
            
            // Эмоция и звук
            visuals?.SetEmotion(Emotion.Sly);
            if (theftAttemptSound != null && Managers.AudioManager.Instance != null)
            {
                Managers.AudioManager.Instance.PlayAudioClip2D(theftAttemptSound);
            }
            
            ShowThoughtBubble("Платить? Щас!", 3f);
            
            // Регистрируем вора для охраны
            if (Managers.GuardManager.Instance != null)
            {
                Managers.GuardManager.Instance.currentThief = this.gameObject;
            }
            
            // Замедляем скорость при побеге
            var mover = GetComponent<AgentMover>();
            if (mover != null) mover.ApplySpeedMultiplier(0.8f);
            
            // Убегаем
            reasonForLeaving = LeaveReason.Theft;
            stateMachine.SetGoal(Managers.ClientSpawner.Instance.exitWaypoint);
            stateMachine.SetState(ClientState.Leaving);
        }
    }
}