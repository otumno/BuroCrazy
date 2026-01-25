// Файл: ClientPathfinding.cs
using UnityEngine;
using TMPro;
using System.Linq;
using System.Collections.Generic;
using Managers;
using Scriptables.Audio;
using DialogueSystem.Data;

public class ClientPathfinding : MonoBehaviour
{
    public enum LeaveReason { Normal, Processed, Angry, CalmedDown, Upset, Theft }
    public ClientStateMachine stateMachine;
    public ClientMovement movement;
    public ClientNotification notification;
    public DocumentHolder docHolder;
	
	[Header("Настройки Стресса (Баланс)")]
    [Tooltip("Множитель скорости стресса, когда клиент стоит в очереди (База = 1.0)")]
    public float stressMod_Standing = 1.0f;
    [Tooltip("Множитель скорости, когда клиент сидит (комфорт)")]
    public float stressMod_Sitting = 0.5f;
    [Tooltip("Множитель скорости, когда клиент занят делом (идет, в туалете) или обслуживается")]
    public float stressMod_BusyOrServed = 0.1f;
    [Tooltip("Сколько стресса добавляет каждая куча мусора/лужа рядом (в секунду)")]
    public float stressAdd_NearbyMess = 0.2f;
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
    public float minPatienceTime = 60f;
    public float maxPatienceTime = 120f;

    public bool isLeavingSuccessfully = false;
    public LeaveReason reasonForLeaving = LeaveReason.Normal;
    
    [Header("Касса")]
    public int billToPay = 0;
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

    gender = (Random.value > 0.5f) ? Gender.Male : Gender.Female;
    
    visuals.Setup(gender, this.spriteCollection, this.stateEmotionMap);
    
    babushkaFactor = Mathf.RoundToInt(Random.Range(0, 5)) * 0.25f;
        suetunFactor = Mathf.RoundToInt(Random.Range(0, 5)) * 0.25f;
        prolazaFactor = Mathf.RoundToInt(Random.Range(0, 5)) * 0.25f;
        documentQuality = 1.0f - (suetunFactor * 0.5f);
		

		_currentStressValue = 0f;
        
        if (mainGoal == default(ClientGoal))
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

        DocumentType startingDoc = DocumentType.None;
        if (Random.value < 0.2f)
        {
            if (mainGoal == ClientGoal.GetCertificate1) { startingDoc = DocumentType.Form2; }
            else if (mainGoal == ClientGoal.GetCertificate2) { startingDoc = DocumentType.Form1; }
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
        movement.Initialize(this);
        float basePatience = Random.Range(minPatienceTime, maxPatienceTime);
        totalPatienceTime = basePatience * (1 + babushkaFactor);
		
		patienceStartTime = Time.time;

        if (spawnSound != null) AudioSource.PlayClipAtPoint(spawnSound, transform.position);
		
		var overlay = GetComponentInChildren<ClientStatusOverlay>(); 
    
		if (overlay != null)
		{
			overlay.Initialize(this);
		}
			else
		{
        Debug.LogError($"[ClientPathfinding] Не найден скрипт ClientStatusOverlay в детях объекта {gameObject.name}!");
    }
		
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
        var allZones = FindObjectsOfType<LimitedCapacityZone>();
        foreach (var z in allZones)
        {
            z.LeaveQueue(gameObject);
        }
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
            Debug.Log($"[ClientPathfinding] Время ожидания звонка от {name} истекло. Удаляем.");
            
            // Если игрок не ответил, считаем это как Upset (расстроен)
            reasonForLeaving = LeaveReason.Upset;
            
            // Уничтожаем объект -> сработает OnDestroy -> удалится иконка со стола
            Destroy(gameObject);
        }
    }
	

    public void OnClientExit()
    {
        if (reasonForLeaving == LeaveReason.Angry || reasonForLeaving == LeaveReason.CalmedDown || reasonForLeaving == LeaveReason.Upset || reasonForLeaving == LeaveReason.Theft)
        {
            clientsExitedAngry++;
        }
        else if(isLeavingSuccessfully)
        {
            clientsExitedProcessed++;
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
	
}