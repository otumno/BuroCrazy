using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using System;

[System.Serializable]
public class LightingPreset
{
    public Color lightColor = Color.white;
    [Range(0f, 2f)] public float lightIntensity = 1f;
}

// Этот класс нужен только конвертеру. После успешной конвертации его можно удалить.
[System.Serializable]
public class SpawningPeriod
{
    public string periodName;
    public float durationInSeconds = 60f;
    public float spawnRate = 5f;
    public int spawnBatchSize = 1;
    public int crowdSpawnCount = 0;
    public int numberOfCrowdsToSpawn = 0;
    public LightingPreset lightingSettings;
    public Color panelColor = new Color(1,1,1,0);
    public List<GameObject> lightsToEnable;
}

public class ClientSpawner : MonoBehaviour
{
    #region Fields and Properties
    [Header("Настройки Календаря")]
    public GameCalendar mainCalendar;

    [Header("Основные настройки спавна")]
    public GameObject clientPrefab;
    public Transform spawnPoint;
    public int maxClientsOnScene = 100;
    public float initialSpawnDelay = 5f;
    public int directorClientsPerDay = 1;

    [Header("Ссылки на объекты сцены")]
    public GameObject waitingZoneObject;
    public Waypoint exitWaypoint;
    public FormTable formTable;

    [Header("ЗОНЫ ОБСЛУЖИВАНИЯ")]
	public LimitedCapacityZone registrationZone; // Регистратура остается одна
	public List<LimitedCapacityZone> category1DeskZones; // Список столов для Справки 1
	public List<LimitedCapacityZone> category2DeskZones; // Список столов для Справки 2
	public List<LimitedCapacityZone> cashierZones;       // Список касс
	public LimitedCapacityZone toiletZone;
	public LimitedCapacityZone directorReceptionZone;
    
    [Header("Настройки цикла дня и ночи (Для конвертера)")]
    public SpawningPeriod[] periods; 

    [Header("Настройки света и UI")]
    public List<string> nightPeriodNames;
    public List<GameObject> allControllableLights;
    public float lightFadeDuration = 0.5f;
    public TextMeshProUGUI timeDisplay;
    public UnityEngine.Rendering.Universal.Light2D globalLight;
    public TextMeshProUGUI dayCounterText;
    [Header("Настройки звука толпы")]
    public AudioSource crowdAudioSource;
    public int minClientsForCrowdSound = 3;
    public int maxClientsForFullVolume = 15;
    
    public static string CurrentPeriodName { get; private set; }
    private int currentPeriodIndex = 0;
    private PeriodSettings previousPeriodPlan;
    private float periodTimer;
    private Coroutine lightManagementCoroutine, continuousSpawnCoroutine;
    private int dayCounter = 1;
    public static ClientSpawner Instance { get; private set; }
    
    private float globalSpawnRateMultiplier = 1f;
    private List<int> directorClientSpawnPeriods = new List<int>();

    private static Dictionary<int, IServiceProvider> serviceProviderAssignments = new Dictionary<int, IServiceProvider>();
	// Событие, которое будет срабатывать при смене периода
public event System.Action OnPeriodChanged;
// >>> КОНЕЦ ИЗМЕНЕНИЙ <<<
    #endregion

    #region Unity Lifecycle Methods (Awake, Start, Update)
    
    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        if (mainCalendar == null || mainCalendar.periodSettings.Count == 0)
        {
            Debug.LogError("Календарь (Main Calendar) не назначен или пуст в ClientSpawner! Работа невозможна.", this);
            enabled = false;
            return;
        }

        // --- ФИНАЛЬНАЯ ВЕРСИЯ СТАРТА ---

        // 1. Начинаем с Дня 1.
        dayCounter = 1;
        UpdateDayCounterUI();

        // 2. Находим индекс "Ночи" (последнего периода)
        int nightIndex = mainCalendar.periodSettings.FindIndex(p => p.periodName.Equals("Ночь", System.StringComparison.InvariantCultureIgnoreCase));
        if (nightIndex == -1) nightIndex = mainCalendar.periodSettings.Count - 1; // Если "Ночь" не найдена, берем просто последний

        // 3. Устанавливаем текущий период на "Ночь" и "предыдущий" тоже на "Ночь"
        currentPeriodIndex = nightIndex;
        previousPeriodPlan = mainCalendar.periodSettings[nightIndex];

        // 4. Устанавливаем таймер так, чтобы до конца "Ночи" оставалось 0.1 секунды
        var nightPlan = mainCalendar.periodSettings[nightIndex];
        float duration = nightPlan.durationInSeconds.Evaluate(dayCounter);
        periodTimer = duration - 0.1f;

        // 5. Запускаем "Ночь" без сброса таймера, чтобы через 0.1 сек произошел плавный переход на "Утро"
        StartNewPeriod(false);
    }

    void Update()
    {
        if (Time.timeScale == 0f) return;
        
        periodTimer += Time.deltaTime;
        UpdateUITimer();
        UpdateLighting();
    
        var currentPlan = GetCurrentPeriodPlan();
        if (currentPlan != null && periodTimer >= currentPlan.durationInSeconds.Evaluate(dayCounter)) 
        { 
            GoToNextPeriod();
        }
    
        CheckCrowdDensity();
    }
    #endregion

    #region Period and Spawning Logic

    public void GoToNextPeriod()
    {
		string previousPeriodName = "";
        var todayPeriods = mainCalendar?.periodSettings;
        if (todayPeriods != null && todayPeriods.Count > 0)
        {
            previousPeriodName = todayPeriods[currentPeriodIndex].periodName;
            previousPeriodPlan = todayPeriods[currentPeriodIndex];
            MusicPlayer.Instance?.OnPeriodChanged();
            
            currentPeriodIndex = (currentPeriodIndex + 1) % todayPeriods.Count;
            periodTimer = 0;
        }
        
        if (currentPeriodIndex == 0) 
        { 
            dayCounter++;
			DirectorManager.Instance?.EvaluateEndOfDayStrikes();
            UpdateDayCounterUI(); 
            ClientQueueManager.Instance.ResetQueueNumber();
            PlanDirectorClientSpawns();
        }
        
        if (todayPeriods != null)
        {
            UpdateAllStaffShifts(todayPeriods[currentPeriodIndex].periodName, previousPeriodName);
        }
        StartNewPeriod();
    }

    void StartNewPeriod(bool resetTimer = true)
    {
        if (resetTimer) periodTimer = 0;
        
        PeriodSettings currentPeriodPlan = GetCurrentPeriodPlan();
        if (currentPeriodPlan == null) return;

        CurrentPeriodName = currentPeriodPlan.periodName;

        if (continuousSpawnCoroutine != null) StopCoroutine(continuousSpawnCoroutine);
        
        string periodNameLower = CurrentPeriodName.ToLower().Trim();
        bool isNightTime = nightPeriodNames.Any(p => p.Equals(periodNameLower, StringComparison.InvariantCultureIgnoreCase));
        ToggleStaffLights(isNightTime);

        // FIX: Added the missing ApplyDayEvent method call.
        ApplyDayEvent(DayEvent.None);

        int clientsForThisPeriod = Mathf.RoundToInt(currentPeriodPlan.clientCount.Evaluate(dayCounter));

        if (clientsForThisPeriod > 0 && !isNightTime)
        {
            continuousSpawnCoroutine = StartCoroutine(HandleContinuousSpawning(currentPeriodPlan, clientsForThisPeriod));
        }
        
        if (lightManagementCoroutine != null) StopCoroutine(lightManagementCoroutine);
        // FIX: Added the missing ManageLocalLightsSmoothly method call.
        lightManagementCoroutine = StartCoroutine(ManageLocalLightsSmoothly(currentPeriodPlan));

        if (periodNameLower == "ночь")
        {
            EvacuateAllClients(true);
        }
        // Сообщаем всем подписчикам (нашим часам), что период вот-вот сменится
        OnPeriodChanged?.Invoke();
    }
    
    IEnumerator HandleContinuousSpawning(PeriodSettings plan, int clientsToSpawn)
    {
        if (clientsToSpawn <= 0) yield break;

        yield return new WaitForSeconds(initialSpawnDelay);
        
        float duration = plan.durationInSeconds.Evaluate(dayCounter);
        if (duration > initialSpawnDelay)
        {
            float spawnInterval = (duration - initialSpawnDelay) / clientsToSpawn;
            spawnInterval /= globalSpawnRateMultiplier;

            for (int i = 0; i < clientsToSpawn; i++)
            {
                SpawnClientBatch(1);
                if (spawnInterval > 0)
                    yield return new WaitForSeconds(spawnInterval);
                else
                    yield return null;
            }
        }
    }
    
    #endregion
	
	#region Helper and Static Methods

    // FIX: Added missing method.
    private void ApplyDayEvent(DayEvent dayEvent)
    {
        // TODO: Implement logic for special day events like "PensionDay" or "ClownDay".
        // This could involve changing client prefabs, modifying character stats, etc.
    }

    // FIX: Added missing coroutine.
    private IEnumerator ManageLocalLightsSmoothly(PeriodSettings periodPlan)
    {
        var lightsToEnable = new HashSet<GameObject>();
        foreach (var lightName in periodPlan.lightsToEnableNames)
        {
            var lightObj = allControllableLights.FirstOrDefault(l => l.name == lightName);
            if (lightObj != null)
            {
                lightsToEnable.Add(lightObj);
            }
        }

        foreach (var lightGO in allControllableLights)
        {
            if (lightGO == null) continue;

            bool shouldBeOn = lightsToEnable.Contains(lightGO);
            bool isCurrentlyOn = lightGO.activeSelf;

            if (shouldBeOn && !isCurrentlyOn)
            {
                // >>> НАЧАЛО ИЗМЕНЕНИЙ: Случайная задержка <<<
                // Ждем от 0 до 0.5 секунд перед включением
                yield return new WaitForSeconds(Random.Range(0f, 0.5f));
                // >>> КОНЕЦ ИЗМЕНЕНИЙ <<<

                // Проверяем, не изменилась ли ситуация, пока мы ждали
                if (GetCurrentPeriodPlan() == periodPlan)
                {
                    StartCoroutine(FadeLight(lightGO, true));
                }
            }
            else if (!shouldBeOn && isCurrentlyOn)
            {
                StartCoroutine(FadeLight(lightGO, false));
            }
        }
    }

    private void UpdateUITimer() 
    { 
        if (timeDisplay != null) 
        { 
            var currentPeriodPlan = GetCurrentPeriodPlan();
            if (currentPeriodPlan == null) return;
            float duration = currentPeriodPlan.durationInSeconds.Evaluate(dayCounter);
            float timeLeft = duration - periodTimer; 
            string formattedTime = string.Format("{0:00}:{1:00}", Mathf.FloorToInt(timeLeft / 60), Mathf.FloorToInt(timeLeft % 60));
            timeDisplay.text = formattedTime;
        } 
    }
    
    private void UpdateLighting() 
    { 
        if (globalLight == null || previousPeriodPlan == null) return;
        var currentPeriodPlan = GetCurrentPeriodPlan();
        if(currentPeriodPlan == null) return;

        float duration = currentPeriodPlan.durationInSeconds.Evaluate(dayCounter);
        if(duration <= 0) return;

        float progress = Mathf.Clamp01(periodTimer / duration);
        globalLight.color = Color.Lerp(previousPeriodPlan.lightingSettings.lightColor, currentPeriodPlan.lightingSettings.lightColor, progress); 
        globalLight.intensity = Mathf.Lerp(previousPeriodPlan.lightingSettings.lightIntensity, currentPeriodPlan.lightingSettings.lightIntensity, progress);
    }
    
    public PeriodSettings GetCurrentPeriodPlan()
    {
        var todayPeriods = mainCalendar?.periodSettings;
        if (todayPeriods != null && todayPeriods.Count > currentPeriodIndex && currentPeriodIndex >= 0)
            return todayPeriods[currentPeriodIndex];
        return null;
    }
    
    public static IServiceProvider GetServiceProviderAtDesk(int deskId)
    {
        if (serviceProviderAssignments.TryGetValue(deskId, out IServiceProvider provider))
            return provider;
        return null;
    }
    
    public static void AssignServiceProviderToDesk(IServiceProvider provider, int deskId)
    {
        serviceProviderAssignments[deskId] = provider;
        Debug.Log($"На станцию #{deskId} назначен работник.");
    }
    
    public static void UnassignServiceProviderFromDesk(int deskId)
    {
        if (serviceProviderAssignments.ContainsKey(deskId))
        {
            serviceProviderAssignments.Remove(deskId);
            Debug.Log($"Работник снят со станции #{deskId}.");
        }
    }

    public static LimitedCapacityZone GetRegistrationZone() => Instance.registrationZone;
	public static LimitedCapacityZone GetToiletZone() => Instance.toiletZone;

	// Обновленные методы для совместимости со старым кодом
	public static LimitedCapacityZone GetDesk1Zone() => Instance.category1DeskZones.FirstOrDefault();
	public static LimitedCapacityZone GetDesk2Zone() => Instance.category2DeskZones.FirstOrDefault();
	public static LimitedCapacityZone GetCashierZone() => Instance.cashierZones.FirstOrDefault();

    private void UpdateAllStaffShifts(string currentPeriodName, string previousPeriodName)
    {
        var allStaffOnScene = HiringManager.Instance.AllStaff;
        foreach (var staffMember in allStaffOnScene)
        {
            if (staffMember == null) continue;
            bool isScheduledNow = staffMember.workPeriods.Any(p => p.Equals(currentPeriodName, StringComparison.InvariantCultureIgnoreCase));
            
            if (isScheduledNow && !staffMember.IsOnDuty())
            {
                staffMember.StartShift();
            }
            else if (!isScheduledNow && staffMember.IsOnDuty())
            {
                staffMember.EndShift();
            }
        }
    }
    
    private void UpdateDayCounterUI() 
    { 
        if (dayCounterText != null) 
            dayCounterText.text = $"День: {dayCounter}";
    }

    private void PlanDirectorClientSpawns()
    {
        directorClientSpawnPeriods.Clear();
        var todayPeriods = mainCalendar?.periodSettings;
        if (todayPeriods == null) return;

        List<int> validPeriodIndices = new List<int>();
        for (int i = 0; i < todayPeriods.Count; i++)
        {
            string nameLower = todayPeriods[i].periodName.ToLower().Trim();
            if (nameLower != "вечер" && nameLower != "ночь")
                validPeriodIndices.Add(i);
        }

        if (validPeriodIndices.Count == 0) return;

        for (int i = 0; i < directorClientsPerDay; i++)
        {
            int randomPeriodIndex = validPeriodIndices[UnityEngine.Random.Range(0, validPeriodIndices.Count)];
            directorClientSpawnPeriods.Add(randomPeriodIndex);
        }
    }

    private IEnumerator SpawnDirectorClientRoutine()
    {
        var currentPeriodPlan = GetCurrentPeriodPlan();
        if (currentPeriodPlan == null) yield break;

        float duration = currentPeriodPlan.durationInSeconds.Evaluate(dayCounter);
        yield return new WaitForSeconds(UnityEngine.Random.Range(5f, duration * 0.8f));
        SpawnClientBatch(1, true);
    }

    void ToggleStaffLights(bool enable)
    {
        var allStaff = FindObjectsByType<StaffController>(FindObjectsSortMode.None);
        foreach(var staff in allStaff)
        {
            if (staff is GuardMovement guard && guard.nightLight != null)
                guard.nightLight.SetActive(enable);
            else if (staff is ServiceWorkerController worker && worker.nightLight != null)
                worker.nightLight.SetActive(enable);
        }
    }

    void EvacuateAllClients(bool force = false)
    {
        ClientPathfinding[] allClients = FindObjectsByType<ClientPathfinding>(FindObjectsSortMode.None);
        foreach (var client in allClients)
        {
            if (client != null)
            {
                var reason = force ? ClientPathfinding.LeaveReason.Angry : ClientPathfinding.LeaveReason.CalmedDown;
                client.ForceLeave(reason);
            }
        }
    }
    
    IEnumerator SpawnCrowdsDuringPeriod(PeriodSettings periodPlan) 
    { 
        int crowdCount = Mathf.RoundToInt(periodPlan.numberOfCrowdsToSpawn.Evaluate(dayCounter));
        if (crowdCount <= 0) yield break;

        float duration = periodPlan.durationInSeconds.Evaluate(dayCounter);
        float timeSlice = duration / (crowdCount + 1);
        for (int i = 1; i <= crowdCount; i++) 
        { 
            yield return new WaitForSeconds(timeSlice);
            int batchSize = Mathf.RoundToInt(periodPlan.crowdSpawnCount.Evaluate(dayCounter));
            SpawnClientBatch(batchSize);
        } 
    }
    
    void CheckCrowdDensity() 
    { 
        if (crowdAudioSource == null || waitingZoneObject == null) return;
        int clientCount = FindObjectsByType<ClientPathfinding>(FindObjectsSortMode.None).Length; 
        if (clientCount >= minClientsForCrowdSound) 
        { 
            if (!crowdAudioSource.isPlaying) crowdAudioSource.Play();
            float volume = Mathf.InverseLerp(minClientsForCrowdSound, maxClientsForFullVolume, clientCount); 
            crowdAudioSource.volume = Mathf.Clamp01(volume); 
        } 
        else 
        { 
            if (crowdAudioSource.isPlaying) crowdAudioSource.Stop();
        } 
    }
    
    IEnumerator FadeLight(GameObject lightObject, bool turnOn) 
    { 
        var lightSource = lightObject.GetComponent<UnityEngine.Rendering.Universal.Light2D>();
        if (lightSource == null) yield break; 
        
        float startIntensity = turnOn ? 0f : lightSource.intensity; 
        float endIntensity = turnOn ? 1f : 0f; 
        float timer = 0f; 
        
        if(turnOn) 
        { 
            lightSource.intensity = 0;
            lightObject.SetActive(true);
        } 
        
        while(timer < lightFadeDuration) 
        { 
            timer += Time.deltaTime;
            lightSource.intensity = Mathf.Lerp(startIntensity, endIntensity, timer / lightFadeDuration); 
            yield return null;
        } 
        
        lightSource.intensity = endIntensity;
        if(!turnOn) { lightObject.SetActive(false); } 
    }
    
    void SpawnClientBatch(int count, bool isDirectorClient = false)
    {
        for (int i = 0; i < count; i++)
        {
            if (FindObjectsByType<ClientPathfinding>(FindObjectsSortMode.None).Length < maxClientsOnScene)
            {
                GameObject clientGO = Instantiate(clientPrefab, spawnPoint.position, Quaternion.identity);
                ClientPathfinding client = clientGO.GetComponent<ClientPathfinding>();
                if (client != null)
                {
                    if (isDirectorClient) client.mainGoal = ClientGoal.DirectorApproval;
                    client.Initialize(Instance.waitingZoneObject, Instance.exitWaypoint);
                }
            }
            else break;
        }
    }

    public void ApplyOrderEffects(DirectorOrder order)
    {
        globalSpawnRateMultiplier = order.clientSpawnRateMultiplier;
    }
    
    public PeriodSettings GetPreviousPeriodPlan()
    {
        return previousPeriodPlan;
    }
    
    public float GetPeriodTimer()
    {
        return periodTimer;
    }

    public int GetCurrentDay()
    {
        return dayCounter;
    }

    public void SetDay(int day)
    {
        dayCounter = day;
        UpdateDayCounterUI();
    }

    public void ResetState()
    {
        dayCounter = 0;
        UpdateDayCounterUI();
        if (ClientQueueManager.Instance != null)
            ClientQueueManager.Instance.ResetQueueNumber();
    }

    #endregion
	
	public static LimitedCapacityZone GetZoneByDeskId(int deskId)
{
    if (Instance == null) return null;
    switch (deskId)
    {
        case 0: 
            return Instance.registrationZone;
        case 1: 
            // Возвращаем первую зону из списка для столов 1-й категории
            return Instance.category1DeskZones.FirstOrDefault(); 
        case 2: 
            // Возвращаем первую зону из списка для столов 2-й категории
            return Instance.category2DeskZones.FirstOrDefault();
        case -1: 
            // Возвращаем первую зону из списка касс
            return Instance.cashierZones.FirstOrDefault();
        default:
            if (Instance.directorReceptionZone != null && Instance.directorReceptionZone.insideWaypoints.Any(wp => wp.GetComponentInParent<ServicePoint>()?.deskId == deskId))
            {
                return Instance.directorReceptionZone;
            }
            return null;
    }
}
	
	/// <summary>
    /// Находит самую свободную зону из предоставленного списка.
    /// </summary>
    public static LimitedCapacityZone GetQuietestZone(List<LimitedCapacityZone> zones)
    {
        if (zones == null || zones.Count == 0)
        {
            return null;
        }

        // Используем Linq, чтобы найти зону с наименьшим количеством людей в очереди
        return zones
            .Where(z => z != null) // Исключаем пустые элементы списка
            .OrderBy(z => z.waitingQueue.Count) // Сортируем по размеру очереди
            .FirstOrDefault(); // Берем первую (самую свободную)
    }

	
	
}