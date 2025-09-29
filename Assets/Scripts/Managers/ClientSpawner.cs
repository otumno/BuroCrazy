// Файл: ClientSpawner.cs
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
    [Header("Основные настройки спавна")]
    public GameObject clientPrefab;
    public Transform spawnPoint;
    public int maxClientsOnScene = 100;
    public float initialSpawnDelay = 5f;
    [Tooltip("Сколько клиентов с документами для директора должно появиться за день")]
    public int directorClientsPerDay = 1;
	[Header("Настройки AI персонала")]
[Tooltip("Как часто (в секундах) система будет проверять, нужно ли сотрудникам начать или закончить смену.")]
public float shiftCheckInterval = 10f;
[Tooltip("Минимальное время, которое сотрудник 'отдыхает' на посту или у кулера (в секундах).")]
public float minChillTime = 5f;
[Tooltip("Максимальное время, которое сотрудник 'отдыхает' на посту или у кулера (в секундах).")]
public float maxChillTime = 10f;
    
    [Header("Ссылки на объекты сцены")]
    public GameObject waitingZoneObject;
    public Waypoint exitWaypoint;
    public FormTable formTable;
    
    [Header("ЗОНЫ ОБСЛУЖИВАНИЯ")]
    public LimitedCapacityZone registrationZone;
    public LimitedCapacityZone desk1Zone;
    public LimitedCapacityZone desk2Zone;
    public LimitedCapacityZone cashierZone;
    public LimitedCapacityZone toiletZone;
    [Tooltip("Перетащите сюда зону 'Приемная Директора'")]
    public LimitedCapacityZone directorReceptionZone;
    
    [Header("Настройки цикла дня и ночи")]
    public SpawningPeriod[] periods;
    [Tooltip("Впишите сюда точные названия периодов, во время которых персонал должен включать фонарики")]
    public List<string> nightPeriodNames;
    public List<GameObject> allControllableLights;
    public float lightFadeDuration = 0.5f;
    public TextMeshProUGUI timeDisplay;
    public UnityEngine.Rendering.Universal.Light2D globalLight;
    
    [Header("UI Элементы")]
    public TextMeshProUGUI dayCounterText;

    [Header("Настройки звука толпы")]
    public AudioSource crowdAudioSource;
    public int minClientsForCrowdSound = 3;
    public int maxClientsForFullVolume = 15;
    
    public static string CurrentPeriodName { get; private set; }
    private int currentPeriodIndex = 0;
    private SpawningPeriod previousPeriod;
    private float periodTimer;
    private Coroutine crowdSpawnCoroutine, lightManagementCoroutine, clerkBreakCoroutine, continuousSpawnCoroutine;
    private int dayCounter = 0;
    public static ClientSpawner Instance { get; private set; }
    private static Dictionary<int, MonoBehaviour> deskOccupants = new Dictionary<int, MonoBehaviour>();
    
    public DailyMandates currentMandates;
    private float globalSpawnRateMultiplier = 1f;
    
    private List<int> directorClientSpawnPeriods = new List<int>();
    
    void Awake()
    {
        Instance = this;
    }

    void Start()
{
	Instance = this;
    if (periods == null || periods.Length == 0)
    {
        enabled = false;
        return;
    }

    // if (pauseUIPanel != null) pauseUIPanel.SetActive(false); // <-- ЭТА СТРОКА УДАЛЕНА

    UpdateDayCounterUI();

    int nightIndex = -1;
    for (int i = 0; i < periods.Length; i++)
    {
        if (periods[i].periodName.Equals("Ночь", System.StringComparison.InvariantCultureIgnoreCase))
        {
            nightIndex = i;
            break;
        }
    }

    if (nightIndex != -1)
    {
        currentPeriodIndex = nightIndex;
        SpawningPeriod nightPeriod = periods[nightIndex];
        periodTimer = Mathf.Max(0, nightPeriod.durationInSeconds - 10f);
        int prevIndex = (nightIndex == 0) ? periods.Length - 1 : nightIndex - 1;
        previousPeriod = periods[prevIndex];
        if (globalLight != null)
        {
            globalLight.color = nightPeriod.lightingSettings.lightColor;
            globalLight.intensity = nightPeriod.lightingSettings.lightIntensity;
        }
        foreach (var lightObj in allControllableLights)
        {
            if (lightObj != null)
            {
                bool shouldBeOn = nightPeriod.lightsToEnable.Contains(lightObj);
                lightObj.SetActive(shouldBeOn);
                var lightSource = lightObj.GetComponent<UnityEngine.Rendering.Universal.Light2D>();
                if(lightSource != null) lightSource.intensity = shouldBeOn ? 1f : 0f;
            }
        }
        
        StartNewPeriod(false);
    }
    else
    {
        Debug.LogWarning("Период с именем 'Ночь' не найден. Игра начнется с первого периода в списке.");
        currentPeriodIndex = -1;
        GoToNextPeriod();
    }
	//StartCoroutine(PeriodicShiftCheckRoutine());
}
    
    void Update()
{
    
    
    // Если игра на паузе, остальная логика Update не выполняется
    if (Time.timeScale == 0f) return;
    
    periodTimer += Time.deltaTime;
    UpdateUITimer();
    UpdateLighting();
    
    string periodNameLower = CurrentPeriodName?.ToLower().Trim();
    if (periodNameLower == "вечер" && MainUIManager.Instance != null && !MainUIManager.Instance.isTransitioning)
    {
        float timeLeft = periods[currentPeriodIndex].durationInSeconds - periodTimer;
        if (timeLeft <= 10f)
        {
            MainUIManager.Instance.TriggerNextDayTransition();
        }
    }
    
    if (periods.Length > currentPeriodIndex && periods[currentPeriodIndex] != null && periodTimer >= periods[currentPeriodIndex].durationInSeconds) 
    { 
        GoToNextPeriod();
    }
    
    CheckCrowdDensity();
}
    
    public void ApplyOrderEffects(DirectorOrder order)
    {
        globalSpawnRateMultiplier = order.clientSpawnRateMultiplier;
    }
    
    public void GoToNextPeriod()
{
    MusicPlayer.Instance?.OnPeriodChanged();
    if (periods.Length > 0)
    {
        string previousPeriodName = periods[currentPeriodIndex].periodName;

        previousPeriod = periods[currentPeriodIndex];
        currentPeriodIndex = (currentPeriodIndex + 1) % periods.Length;
        
        // ВАЖНО: Сбрасываем таймер здесь, до вызова логики смен
        periodTimer = 0; 
        
        // ВЫЗЫВАЕМ ПРОВЕРКУ СМЕН. ТОЛЬКО ЗДЕСЬ. ТОЛЬКО ОДИН РАЗ.
        UpdateAllStaffShifts(periods[currentPeriodIndex].periodName, previousPeriodName);
    }
    
    if (currentPeriodIndex == 0) 
    { 
        dayCounter++;
        UpdateDayCounterUI(); 
        ClientQueueManager.Instance.ResetQueueNumber();
        PlanDirectorClientSpawns();
    }
    StartNewPeriod();
}

    void UpdateDayCounterUI() 
    { 
        if (dayCounterText != null) 
        { 
            dayCounterText.text = $"День: {dayCounter}";
        } 
    }

    void StartNewPeriod(bool resetTimer = true)
    {
        if (resetTimer)
        {
            periodTimer = 0;
        }
        
        SpawningPeriod newPeriod = periods[currentPeriodIndex];
        CurrentPeriodName = newPeriod.periodName;
		
		MusicPlayer.Instance?.OnPeriodChanged();

        if (continuousSpawnCoroutine != null) StopCoroutine(continuousSpawnCoroutine);
        if (crowdSpawnCoroutine != null) StopCoroutine(crowdSpawnCoroutine);
        if (clerkBreakCoroutine != null) StopCoroutine(clerkBreakCoroutine);

        string periodNameLower = newPeriod.periodName.ToLower().Trim();

        bool isNightTime = nightPeriodNames.Any(p => p.Equals(periodNameLower, System.StringComparison.InvariantCultureIgnoreCase));
        ToggleStaffLights(isNightTime);

        //UpdateAllStaffShifts(periodNameLower);
        HandleSpecialPeriodLogic(periodNameLower, newPeriod.durationInSeconds);
        
        if (directorClientSpawnPeriods.Contains(currentPeriodIndex))
        {
            StartCoroutine(SpawnDirectorClientRoutine());
            directorClientSpawnPeriods.Remove(currentPeriodIndex); 
        }

        if (newPeriod.numberOfCrowdsToSpawn > 0 && newPeriod.crowdSpawnCount > 0) 
        { 
            crowdSpawnCoroutine = StartCoroutine(SpawnCrowdsDuringPeriod(newPeriod));
        }
        
        bool canSpawnClients = !isNightTime && periodNameLower != "конец дня";
        if (newPeriod.spawnRate > 0 && canSpawnClients)
        {
            float finalSpawnRate = newPeriod.spawnRate * globalSpawnRateMultiplier;
            if (finalSpawnRate > 0)
            {
                continuousSpawnCoroutine = StartCoroutine(HandleContinuousSpawning(newPeriod));
            }
        }

        if (periodNameLower == "ночь")
        {
            EvacuateAllClients(true);
        }

        if (lightManagementCoroutine != null) StopCoroutine(lightManagementCoroutine);
        lightManagementCoroutine = StartCoroutine(ManageLocalLightsSmoothly(newPeriod));
        
        if (previousPeriod == null) 
        { 
            globalLight.color = newPeriod.lightingSettings.lightColor;
            globalLight.intensity = newPeriod.lightingSettings.lightIntensity; 
        }
    }
    
    private void PlanDirectorClientSpawns()
    {
        directorClientSpawnPeriods.Clear();
        List<int> validPeriodIndices = new List<int>();
        for (int i = 0; i < periods.Length; i++)
        {
            string nameLower = periods[i].periodName.ToLower().Trim();
            if (nameLower != "вечер" && nameLower != "ночь")
            {
                validPeriodIndices.Add(i);
            }
        }

        if (validPeriodIndices.Count == 0)
        {
            Debug.LogWarning("Нет подходящих дневных периодов для спавна клиентов для Директора!");
            return;
        }

        for (int i = 0; i < directorClientsPerDay; i++)
        {
            int randomPeriodIndex = validPeriodIndices[UnityEngine.Random.Range(0, validPeriodIndices.Count)];
            directorClientSpawnPeriods.Add(randomPeriodIndex);
        }
        Debug.Log($"Запланирован спавн {directorClientsPerDay} клиентов для Директора в периодах: {string.Join(", ", directorClientSpawnPeriods)}");
    }

    private IEnumerator SpawnDirectorClientRoutine()
    {
        yield return new WaitForSeconds(UnityEngine.Random.Range(5f, periods[currentPeriodIndex].durationInSeconds * 0.8f));
        if (FindObjectsByType<ClientPathfinding>(FindObjectsSortMode.None).Length < maxClientsOnScene)
        {
            Debug.Log("Спавним клиента с документами для Директора!");
            GameObject clientGO = Instantiate(clientPrefab, spawnPoint.position, Quaternion.identity);
            ClientPathfinding client = clientGO.GetComponent<ClientPathfinding>();
            if (client != null)
            {
                client.mainGoal = ClientGoal.DirectorApproval;
                client.Initialize(Instance.waitingZoneObject, Instance.exitWaypoint);
            }
        }
    }
    
    void ToggleStaffLights(bool enable)
    {
        var allStaff = FindObjectsByType<StaffController>(FindObjectsSortMode.None);
        foreach(var staff in allStaff)
        {
            if (staff is GuardMovement guard && guard.nightLight != null)
            {
                guard.nightLight.SetActive(enable);
            }
            else if (staff is ServiceWorkerController worker && worker.nightLight != null)
            {
                worker.nightLight.SetActive(enable);
            }
        }
    }
    
private void UpdateAllStaffShifts(string currentPeriodName, string previousPeriodName)
{
    var allStaffOnScene = HiringManager.Instance.AllStaff;
    foreach (var staffMember in allStaffOnScene)
    {
        if (staffMember == null) continue;

        bool isScheduledNow = staffMember.workPeriods.Any(p => p.Equals(currentPeriodName, StringComparison.InvariantCultureIgnoreCase));
        bool wasScheduledBefore = !string.IsNullOrEmpty(previousPeriodName) && staffMember.workPeriods.Any(p => p.Equals(previousPeriodName, StringComparison.InvariantCultureIgnoreCase));

        if (isScheduledNow && !wasScheduledBefore)
        {
            staffMember.StartShift();
        }
        else if (!isScheduledNow && wasScheduledBefore)
        {
            staffMember.EndShift();
        }
    }
}

    void HandleSpecialPeriodLogic(string periodNameLower, float duration)
    {
        if (periodNameLower == "обед")
        {
            var clerksOnDuty = FindObjectsByType<ClerkController>(FindObjectsSortMode.None).Where(c => c.IsOnDuty()).ToList();
            if (clerksOnDuty.Any())
            {
                if (clerkBreakCoroutine != null) StopCoroutine(clerkBreakCoroutine);
                clerkBreakCoroutine = StartCoroutine(ManageClerkLunchBreaks(clerksOnDuty, duration));
            }

            var guardsOnDuty = FindObjectsByType<GuardMovement>(FindObjectsSortMode.None).Where(g => g.IsOnDuty()).ToList();
            foreach (var guard in guardsOnDuty)
            {
                if (guard.GetCurrentState() != GuardMovement.GuardState.OnBreak && guard.GetCurrentState() != GuardMovement.GuardState.GoingToBreak)
                {
                   guard.GoOnBreak(duration);
                }
            }
        }
        else
        {
            var guardsOnBreak = FindObjectsByType<GuardMovement>(FindObjectsSortMode.None).Where(g => g.GetCurrentState() == GuardMovement.GuardState.OnBreak).ToList();
            foreach (var guard in guardsOnBreak)
            {
                guard.ReturnToPatrol();
            }
        }
    }

    IEnumerator ManageClerkLunchBreaks(List<ClerkController> clerksOnBreak, float totalLunchDuration)
    {
        if (clerksOnBreak == null || clerksOnBreak.Count == 0) yield break;
        float breakDurationPerClerk = totalLunchDuration / clerksOnBreak.Count;
        var shuffledClerks = clerksOnBreak.OrderBy(c => UnityEngine.Random.value).ToList();
        foreach (var clerk in shuffledClerks)
        {
            if (clerk != null && clerk.IsOnDuty())
            {
                clerk.GoOnBreak(breakDurationPerClerk);
                yield return new WaitForSeconds(breakDurationPerClerk);
            }
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

    public static LimitedCapacityZone GetRegistrationZone() => Instance.registrationZone;
    public static LimitedCapacityZone GetDesk1Zone() => Instance.desk1Zone;
    public static LimitedCapacityZone GetDesk2Zone() => Instance.desk2Zone;
    public static LimitedCapacityZone GetCashierZone() => Instance.cashierZone;
    public static LimitedCapacityZone GetToiletZone() => Instance.toiletZone;
    
    public static ClerkController GetClerkAtDesk(int deskId) { if (Instance == null) return null; var clerks = FindObjectsByType<ClerkController>(FindObjectsSortMode.None); return clerks.FirstOrDefault(c => c.assignedServicePoint != null && c.assignedServicePoint.deskId == deskId); }
    public static ClerkController GetAbsentClerk() { if (Instance == null) return null; var clerks = FindObjectsByType<ClerkController>(FindObjectsSortMode.None); return clerks.FirstOrDefault(c => c.IsOnBreak()); }
    public static void ReportDeskOccupation(int deskId, MonoBehaviour occupant) { deskOccupants[deskId] = occupant; }
    
    void SpawnClientBatch(int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (FindObjectsByType<ClientPathfinding>(FindObjectsSortMode.None).Length < maxClientsOnScene)
            {
                GameObject clientGO = Instantiate(clientPrefab, spawnPoint.position, Quaternion.identity);
                ClientPathfinding client = clientGO.GetComponent<ClientPathfinding>();
                if (client != null)
                {
                    client.Initialize(Instance.waitingZoneObject, Instance.exitWaypoint);
                }
            }
            else break;
        }
    }
    
    IEnumerator HandleContinuousSpawning(SpawningPeriod period)
    {
        yield return new WaitForSeconds(initialSpawnDelay);
        while(true)
        {
            SpawnClientBatch(period.spawnBatchSize);
            float finalSpawnRate = period.spawnRate > 0 ? period.spawnRate / globalSpawnRateMultiplier : float.MaxValue;
            if (finalSpawnRate > 0)
                yield return new WaitForSeconds(finalSpawnRate);
            else
                yield return null; // Avoid infinite loop if spawn rate is 0 or less
        }
    }
    
    IEnumerator SpawnCrowdsDuringPeriod(SpawningPeriod period) 
    { 
        float timeSlice = period.durationInSeconds / (period.numberOfCrowdsToSpawn + 1);
        for (int i = 1; i <= period.numberOfCrowdsToSpawn; i++) 
        { 
            yield return new WaitForSeconds(timeSlice);
            SpawnClientBatch(period.crowdSpawnCount);
        } 
    }
    
    void UpdateUITimer() 
    { 
        if (timeDisplay != null) 
        { 
            SpawningPeriod currentPeriod = periods[currentPeriodIndex];
            float timeLeft = currentPeriod.durationInSeconds - periodTimer; 
            string formattedTime = string.Format("{0:00}:{1:00}", Mathf.FloorToInt(timeLeft / 60), Mathf.FloorToInt(timeLeft % 60));
            timeDisplay.text = $"Период: {currentPeriod.periodName}\nОсталось: {formattedTime}"; 
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
    
    void UpdateLighting() 
    { 
        if (globalLight == null || previousPeriod == null) return;
        SpawningPeriod currentPeriod = periods[currentPeriodIndex]; 
        if(currentPeriod.durationInSeconds > 0)
        {
            float progress = Mathf.Clamp01(periodTimer / currentPeriod.durationInSeconds);
            globalLight.color = Color.Lerp(previousPeriod.lightingSettings.lightColor, currentPeriod.lightingSettings.lightColor, progress); 
            globalLight.intensity = Mathf.Lerp(previousPeriod.lightingSettings.lightIntensity, currentPeriod.lightingSettings.lightIntensity, progress);
        }
    }
    
    IEnumerator ManageLocalLightsSmoothly(SpawningPeriod period) 
    { 
        var lightsToTurnOn = period.lightsToEnable.Where(l => l != null && !l.activeSelf).ToList();
        var lightsToTurnOff = allControllableLights.Except(period.lightsToEnable).Where(l => l != null && l.activeSelf).ToList(); 
        lightsToTurnOn = lightsToTurnOn.OrderBy(l => UnityEngine.Random.value).ToList(); 
        lightsToTurnOff = lightsToTurnOff.OrderBy(l => UnityEngine.Random.value).ToList();
        
        foreach (var lightObject in lightsToTurnOff) 
        { 
            if(lightObject != null) StartCoroutine(FadeLight(lightObject, false));
            yield return new WaitForSeconds(UnityEngine.Random.Range(0.05f, 0.2f));
        } 
        foreach (var lightObject in lightsToTurnOn) 
        { 
            if(lightObject != null) StartCoroutine(FadeLight(lightObject, true));
            yield return new WaitForSeconds(UnityEngine.Random.Range(0.05f, 0.2f));
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
    
    public SpawningPeriod GetCurrentPeriod()
    {
        if (periods != null && periods.Length > 0 && currentPeriodIndex >= 0 && currentPeriodIndex < periods.Length)
        {
            return periods[currentPeriodIndex];
        }
        return null;
    }
    
    public SpawningPeriod GetPreviousPeriod()
    {
        return previousPeriod;
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
        {
            ClientQueueManager.Instance.ResetQueueNumber();
        }
    }
}