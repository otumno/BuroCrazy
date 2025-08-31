// Файл: ClientSpawner.cs
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;

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
    
    [Header("Настройки цикла дня и ночи")]
    public SpawningPeriod[] periods;
    public List<GameObject> allControllableLights;
    public float lightFadeDuration = 0.5f;
    public TextMeshProUGUI timeDisplay;
    public UnityEngine.Rendering.Universal.Light2D globalLight;
    
    [Header("Персонал")]
    public List<ClerkController> allClerks;
    public List<InternController> allInterns;
    public List<ServiceWorkerController> allServiceWorkers;
    public List<GuardMovement> allGuards;

    [Header("UI Элементы")]
    public GameObject pauseUIPanel;
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
    private int dayCounter = 1;
    public static ClientSpawner Instance { get; private set; }
    private static Dictionary<int, MonoBehaviour> deskOccupants = new Dictionary<int, MonoBehaviour>();
    
    private int evacuationMilestone = 0;

    void Awake() 
    { 
        Instance = this; 
    }

    void Start() 
    { 
        AssignGuardShifts();
        AssignServiceWorkerShifts();

        if (periods == null || periods.Length == 0) { enabled = false; return; } 
        if (pauseUIPanel != null) pauseUIPanel.SetActive(false); 
        UpdateDayCounterUI(); 
        currentPeriodIndex = -1; 
        GoToNextPeriod(); 
    }
    
    void Update() 
    { 
        if (Input.GetKeyDown(KeyCode.Space)) { bool isPaused = Time.timeScale == 0f; Time.timeScale = isPaused ? 1f : 0f; if (pauseUIPanel != null) pauseUIPanel.SetActive(!isPaused); } 
        if (Time.timeScale == 0f) return; 
        
        periodTimer += Time.deltaTime; 
        UpdateUITimer(); 
        UpdateLighting();
        
        if (CurrentPeriodName != null && CurrentPeriodName.ToLower().Trim() == "вечер")
        {
            ManageEveningEvacuation();
        }
        
        if (periodTimer >= periods[currentPeriodIndex].durationInSeconds) { GoToNextPeriod(); } 
        CheckCrowdDensity(); 
    }
    
    void GoToNextPeriod() 
    { 
        if (previousPeriod != null && previousPeriod.periodName.ToLower().Trim() == "вечер") 
        { 
            if (GuardManager.Instance != null)
            {
                GuardManager.Instance.EvictRemainingClients();
            }
        } 
        
        previousPeriod = (currentPeriodIndex < 0) ? periods[periods.Length - 1] : periods[currentPeriodIndex];
        currentPeriodIndex = (currentPeriodIndex + 1) % periods.Length; 
        if (currentPeriodIndex == 0) { dayCounter++; UpdateDayCounterUI(); ClientQueueManager.Instance.ResetQueueNumber(); } 
        StartNewPeriod();
    }

    void UpdateDayCounterUI() { if (dayCounterText != null) { dayCounterText.text = $"День: {dayCounter}"; } }

    void StartNewPeriod()
    {
        periodTimer = 0;
        evacuationMilestone = 0;

        SpawningPeriod newPeriod = periods[currentPeriodIndex];
        CurrentPeriodName = newPeriod.periodName;

        if (continuousSpawnCoroutine != null) 
        {
            StopCoroutine(continuousSpawnCoroutine);
            continuousSpawnCoroutine = null;
        }
        if (crowdSpawnCoroutine != null) StopCoroutine(crowdSpawnCoroutine);
        if (clerkBreakCoroutine != null) StopCoroutine(clerkBreakCoroutine);

        string periodNameLower = newPeriod.periodName.ToLower().Trim();
        string prevPeriodNameLower = previousPeriod?.periodName.ToLower().Trim() ?? "";
        
        bool isNightTime = (periodNameLower == "ночь" || periodNameLower == "вечер");
        ToggleStaffLights(isNightTime);

        ManageClerkShifts(periodNameLower);
        ManageInternShifts(periodNameLower, prevPeriodNameLower);
        ManageGuardShifts(periodNameLower);
        ManageServiceWorkerShifts(periodNameLower);

        if (newPeriod.numberOfCrowdsToSpawn > 0 && newPeriod.crowdSpawnCount > 0) { crowdSpawnCoroutine = StartCoroutine(SpawnCrowdsDuringPeriod(newPeriod)); }
        
        bool canSpawnClients = (periodNameLower != "ночь" && periodNameLower != "вечер" && periodNameLower != "конец дня");
        if (newPeriod.spawnRate > 0 && canSpawnClients) 
        { 
            continuousSpawnCoroutine = StartCoroutine(HandleContinuousSpawning(newPeriod)); 
        }

        if (periodNameLower == "ночь")
        {
            EvacuateAllClients(true);
        }

        lightManagementCoroutine = StartCoroutine(ManageLocalLightsSmoothly(newPeriod));
        if (previousPeriod == null) { globalLight.color = newPeriod.lightingSettings.lightColor; globalLight.intensity = newPeriod.lightingSettings.lightIntensity; }
    }
    
    void ToggleStaffLights(bool enable)
    {
        foreach(var guard in allGuards) { if (guard.nightLight != null) guard.nightLight.SetActive(enable); }
        foreach(var worker in allServiceWorkers) { if (worker.nightLight != null) worker.nightLight.SetActive(enable); }
    }

    void AssignGuardShifts()
    {
        if (allGuards.Count == 0) return;
        if (allGuards.Count == 1) { allGuards[0].AssignShift(GuardMovement.Shift.Universal); return; }
        
        var shuffled = allGuards.OrderBy(g => Random.value).ToList();
        int dayGuards = 0, nightGuards = 0;
        foreach(var guard in shuffled)
        {
            if (dayGuards <= nightGuards) { guard.AssignShift(GuardMovement.Shift.Day); dayGuards++; }
            else { guard.AssignShift(GuardMovement.Shift.Night); nightGuards++; }
        }
    }

    void AssignServiceWorkerShifts()
    {
        if (allServiceWorkers.Count == 0) return;
        if (allServiceWorkers.Count == 1) { allServiceWorkers[0].AssignShift(ServiceWorkerController.Shift.First); return; }

        var shuffled = allServiceWorkers.OrderBy(w => Random.value).ToList();
        int firstShift = 0, secondShift = 0;
        foreach(var worker in shuffled)
        {
            if (firstShift <= secondShift) { worker.AssignShift(ServiceWorkerController.Shift.First); firstShift++; }
            else { worker.AssignShift(ServiceWorkerController.Shift.Second); secondShift++; }
        }
    }

    void ManageClerkShifts(string periodNameLower)
    {
        if (allClerks == null) return;
        if (periodNameLower == "ночь") { foreach (var clerk in allClerks) clerk.EndShift(); }
        else if (periodNameLower == "обед") { clerkBreakCoroutine = StartCoroutine(ManageLunchBreaks(periods[currentPeriodIndex].durationInSeconds)); }
        else { foreach (var clerk in allClerks) clerk.StartShift(); }
    }

    void ManageInternShifts(string periodNameLower, string prevPeriodNameLower)
    {
        if (allInterns == null) return;
        if (periodNameLower == "начало дня") { foreach (var intern in allInterns) intern.StartShift(); }
        else if (periodNameLower == "конец дня") { foreach (var intern in allInterns) intern.EndShift(); }
        else if (periodNameLower != "ночь" && prevPeriodNameLower == "обед") { foreach (var intern in allInterns) intern.GoOnBreak(periods[currentPeriodIndex].durationInSeconds); }
    }
    
    void ManageGuardShifts(string periodNameLower)
    {
        if (allGuards == null) return;
        bool isDayTime = (periodNameLower == "утро" || periodNameLower == "начало дня" || periodNameLower == "день" || periodNameLower == "обед" || periodNameLower == "конец дня" || periodNameLower == "вечер");
        foreach (var guard in allGuards)
        {
            if (guard.ShouldWork(isDayTime)) guard.StartShift();
            else guard.EndShift();
            
            // --- ИСПРАВЛЕНИЕ ЗДЕСЬ ---
            // Передаем в метод точку для отдыха, которая хранится в самом охраннике
            if (periodNameLower == "обед") guard.GoOnBreak(guard.breakPoint);
            else if (guard.GetCurrentState() == GuardMovement.GuardState.OnBreak) guard.ReturnToPatrol();
        }
    }

    void ManageServiceWorkerShifts(string periodNameLower)
    {
        if (allServiceWorkers == null) return;
        bool isFirstShift = periodNameLower == "ночь" || periodNameLower == "утро" || periodNameLower == "начало дня";
        bool isSecondShift = periodNameLower == "день" || periodNameLower == "обед";

        foreach (var worker in allServiceWorkers)
        {
            if (worker.ShouldWork(isFirstShift, isSecondShift)) worker.StartShift();
            else worker.EndShift();
        }
    }
    
    void ManageEveningEvacuation() 
    { 
        SpawningPeriod currentPeriod = periods[currentPeriodIndex]; 
        float progress = periodTimer / currentPeriod.durationInSeconds; 
        int currentMilestone = Mathf.FloorToInt(progress * 10); 
        if (currentMilestone > evacuationMilestone) 
        { 
            evacuationMilestone = currentMilestone; 
            var allClients = FindObjectsByType<ClientPathfinding>(FindObjectsSortMode.None).Where(c => c != null && c.stateMachine.GetCurrentState() != ClientState.Leaving && c.stateMachine.GetCurrentState() != ClientState.LeavingUpset).ToList(); 
            if (allClients.Count == 0) return; 
            if (currentMilestone >= 9) 
            { 
                EvacuateAllClients();
            } 
            else 
            { 
                int clientsToEvacuateCount = Mathf.CeilToInt(allClients.Count * 0.1f); 
                var shuffledClients = allClients.OrderBy(c => Random.value).ToList(); 
                for (int i = 0; i < clientsToEvacuateCount; i++) 
                { 
                    if (i < shuffledClients.Count) 
                    { 
                        shuffledClients[i].ForceLeave(ClientPathfinding.LeaveReason.CalmedDown); 
                    } 
                } 
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
    public static ClerkController GetClerkAtDesk(int deskId) { if (Instance == null) return null; return Instance.allClerks.FirstOrDefault(c => c.assignedServicePoint != null && c.assignedServicePoint.deskId == deskId); }
    public static ClerkController GetAbsentClerk() { if (Instance == null) return null; return Instance.allClerks.FirstOrDefault(c => c.IsOnBreak()); }
    public static void ReportDeskOccupation(int deskId, MonoBehaviour occupant) { deskOccupants[deskId] = occupant; }
    
    void SpawnClientBatch(int count) 
    { 
        for (int i = 0; i < count; i++) 
        { 
            if (ClientPathfinding.totalClients < maxClientsOnScene) 
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
    
    IEnumerator ManageLunchBreaks(float totalLunchDuration) { if (allClerks == null || allClerks.Count == 0) yield break; float breakDurationPerClerk = totalLunchDuration / allClerks.Count; var shuffledClerks = allClerks.OrderBy(c => Random.value).ToList(); foreach (var clerk in shuffledClerks) { clerk.GoOnBreak(breakDurationPerClerk); yield return new WaitForSeconds(breakDurationPerClerk); } }
    IEnumerator HandleContinuousSpawning(SpawningPeriod period) { yield return new WaitForSeconds(initialSpawnDelay); while(true) { SpawnClientBatch(period.spawnBatchSize); yield return new WaitForSeconds(period.spawnRate); } }
    IEnumerator SpawnCrowdsDuringPeriod(SpawningPeriod period) { float timeSlice = period.durationInSeconds / (period.numberOfCrowdsToSpawn + 1); for (int i = 1; i <= period.numberOfCrowdsToSpawn; i++) { yield return new WaitForSeconds(timeSlice); SpawnClientBatch(period.crowdSpawnCount); } }
    void UpdateUITimer() { if (timeDisplay != null) { SpawningPeriod currentPeriod = periods[currentPeriodIndex]; float timeLeft = currentPeriod.durationInSeconds - periodTimer; string formattedTime = string.Format("{0:00}:{1:00}", Mathf.FloorToInt(timeLeft / 60), Mathf.FloorToInt(timeLeft % 60)); timeDisplay.text = $"Период: {currentPeriod.periodName}\nОсталось: {formattedTime}"; } }
    void CheckCrowdDensity() { if (crowdAudioSource == null || waitingZoneObject == null) return; Collider2D[] clients = Physics2D.OverlapCircleAll(waitingZoneObject.transform.position, 2f, LayerMask.GetMask("Client")); int clientCount = clients.Length; if (clientCount >= minClientsForCrowdSound) { if (!crowdAudioSource.isPlaying) crowdAudioSource.Play(); float volume = Mathf.InverseLerp(minClientsForCrowdSound, maxClientsForFullVolume, clientCount); crowdAudioSource.volume = Mathf.Clamp01(volume); } else { if (crowdAudioSource.isPlaying) crowdAudioSource.Stop(); } }
    void UpdateLighting() { if (globalLight == null || previousPeriod == null) return; SpawningPeriod currentPeriod = periods[currentPeriodIndex]; float progress = Mathf.Clamp01(periodTimer / currentPeriod.durationInSeconds); globalLight.color = Color.Lerp(previousPeriod.lightingSettings.lightColor, currentPeriod.lightingSettings.lightColor, progress); globalLight.intensity = Mathf.Lerp(previousPeriod.lightingSettings.lightIntensity, currentPeriod.lightingSettings.lightIntensity, progress); }
    IEnumerator ManageLocalLightsSmoothly(SpawningPeriod period) { var lightsToTurnOn = period.lightsToEnable.Where(l => l != null && !l.activeSelf).ToList(); var lightsToTurnOff = allControllableLights.Except(period.lightsToEnable).Where(l => l != null && l.activeSelf).ToList(); lightsToTurnOn = lightsToTurnOn.OrderBy(l => Random.value).ToList(); lightsToTurnOff = lightsToTurnOff.OrderBy(l => Random.value).ToList(); foreach (var lightObject in lightsToTurnOff) { StartCoroutine(FadeLight(lightObject, false)); yield return new WaitForSeconds(Random.Range(0.05f, 0.2f)); } foreach (var lightObject in lightsToTurnOn) { StartCoroutine(FadeLight(lightObject, true)); yield return new WaitForSeconds(Random.Range(0.05f, 0.2f)); } }
    IEnumerator FadeLight(GameObject lightObject, bool turnOn) { var lightSource = lightObject.GetComponent<UnityEngine.Rendering.Universal.Light2D>(); if (lightSource == null) yield break; float startIntensity = turnOn ? 0f : lightSource.intensity; float endIntensity = turnOn ? 1f : 0f; float timer = 0f; if(turnOn) { lightSource.intensity = 0; lightObject.SetActive(true); } while(timer < lightFadeDuration) { timer += Time.deltaTime; lightSource.intensity = Mathf.Lerp(startIntensity, endIntensity, timer / lightFadeDuration); yield return null; } lightSource.intensity = endIntensity; if(!turnOn) { lightObject.SetActive(false); } }
    
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
}