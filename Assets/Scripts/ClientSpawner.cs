using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;

[System.Serializable]
public class LightingPreset
{
    public Color lightColor = Color.white;
    [Range(0f, 2f)]
    public float lightIntensity = 1f;
}

[System.Serializable]
public class SpawningPeriod
{
    public string periodName;
    public float durationInSeconds = 60f;
    [Header("Постоянный поток клиентов")]
    public float spawnRate = 5f;
    public int spawnBatchSize = 1;
    [Header("Разовый спавн толпы")]
    public int crowdSpawnCount = 0;
    public int numberOfCrowdsToSpawn = 0;
    [Header("Настройки освещения для периода")]
    public LightingPreset lightingSettings;
    [Header("Локальные источники света")]
    public List<GameObject> lightsToEnable;
}

public class ClientSpawner : MonoBehaviour
{
    [Header("Основные настройки спавна")]
    public GameObject clientPrefab;
    public Transform spawnPoint;
    public int maxClientsOnScene = 100;
    
    [Header("Ссылки на объекты сцены")]
    public GameObject waitingZoneObject, toiletZoneObject, registrationZoneObject, desk1Object, desk2Object;
    public Waypoint exitWaypoint;
    
    [Header("Настройки цикла дня и ночи")]
    public SpawningPeriod[] periods;
    public List<GameObject> allControllableLights;
    public float lightFadeDuration = 0.5f;
    public TextMeshProUGUI timeDisplay;
    public UnityEngine.Rendering.Universal.Light2D globalLight;
    public List<ClerkController> allClerks;
    public List<InternController> allInterns;

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
    private float spawnTimer;
    private Coroutine crowdSpawnCoroutine;
    private Coroutine lightManagementCoroutine;
    private Coroutine clerkBreakCoroutine;
    private int dayCounter = 1;
    private static Dictionary<int, MonoBehaviour> deskOccupants = new Dictionary<int, MonoBehaviour>();
    private static ClientSpawner Instance;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
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
        if (periodTimer >= periods[currentPeriodIndex].durationInSeconds) { GoToNextPeriod(); }
        HandleContinuousSpawning();
        CheckCrowdDensity();
    }
    
    void GoToNextPeriod()
    {
        previousPeriod = (currentPeriodIndex < 0) ? periods[periods.Length - 1] : periods[currentPeriodIndex];
        currentPeriodIndex = (currentPeriodIndex + 1) % periods.Length;
        if (currentPeriodIndex == 0)
        {
            dayCounter++;
            UpdateDayCounterUI();
            ClientQueueManager.ResetQueueNumber();
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

    void StartNewPeriod()
    {
        periodTimer = 0;
        spawnTimer = 0;
        SpawningPeriod newPeriod = periods[currentPeriodIndex];
        CurrentPeriodName = newPeriod.periodName;
        if (clerkBreakCoroutine != null) StopCoroutine(clerkBreakCoroutine);
        if (lightManagementCoroutine != null) StopCoroutine(lightManagementCoroutine);
        if (crowdSpawnCoroutine != null) StopCoroutine(crowdSpawnCoroutine);
        string periodNameLower = newPeriod.periodName.ToLower().Trim();
        string prevPeriodNameLower = previousPeriod?.periodName.ToLower().Trim() ?? "";
        if (periodNameLower == "ночь") { EvacuateAllClients(); if (allClerks != null) foreach (var clerk in allClerks) clerk.EndShift(); }
        else if (periodNameLower == "обед") { if (allClerks != null) clerkBreakCoroutine = StartCoroutine(ManageLunchBreaks(newPeriod.durationInSeconds)); }
        else { if (allClerks != null) foreach (var clerk in allClerks) clerk.StartShift(); }
        if (periodNameLower == "начало дня") { if (allInterns != null) foreach (var intern in allInterns) intern.StartShift(); }
        else if (periodNameLower == "конец дня") { if (allInterns != null) foreach (var intern in allInterns) intern.EndShift(); }
        else if (periodNameLower != "ночь" && prevPeriodNameLower == "обед") { if (allInterns != null) foreach (var intern in allInterns) intern.GoOnBreak(newPeriod.durationInSeconds); }
        lightManagementCoroutine = StartCoroutine(ManageLocalLightsSmoothly(newPeriod));
        if (previousPeriod == null) { globalLight.color = newPeriod.lightingSettings.lightColor; globalLight.intensity = newPeriod.lightingSettings.lightIntensity; }
        if (newPeriod.numberOfCrowdsToSpawn > 0 && newPeriod.crowdSpawnCount > 0) { crowdSpawnCoroutine = StartCoroutine(SpawnCrowdsDuringPeriod(newPeriod)); }
    }
    
    public static void ReportDeskOccupation(int deskId, MonoBehaviour occupant) { deskOccupants[deskId] = occupant; }
    public static int GetVacantDesk() { if (Instance == null) return -1; foreach(var clerk in Instance.allClerks) { int id = clerk.assignedServicePoint; if (!deskOccupants.ContainsKey(id) || deskOccupants[id] == null) return id; } return -1; }
    public static bool ClaimDesk(int deskId, InternController intern) { if (deskOccupants.ContainsKey(deskId) && deskOccupants[deskId] == null) { deskOccupants[deskId] = intern; return true; } return false; }
    public static Transform GetWorkPointForDesk(int deskId) { if (Instance == null) return null; ClerkController clerk = Instance.allClerks.FirstOrDefault(c => c.assignedServicePoint == deskId); return clerk != null ? clerk.workPoint : null; }

    void UpdateLighting() { if (globalLight == null || previousPeriod == null) return; SpawningPeriod currentPeriod = periods[currentPeriodIndex]; float progress = Mathf.Clamp01(periodTimer / currentPeriod.durationInSeconds); globalLight.color = Color.Lerp(previousPeriod.lightingSettings.lightColor, currentPeriod.lightingSettings.lightColor, progress); globalLight.intensity = Mathf.Lerp(previousPeriod.lightingSettings.lightIntensity, currentPeriod.lightingSettings.lightIntensity, progress); }
    IEnumerator ManageLocalLightsSmoothly(SpawningPeriod period) { var lightsToTurnOn = period.lightsToEnable.Where(l => l != null && !l.activeSelf).ToList(); var lightsToTurnOff = allControllableLights.Except(period.lightsToEnable).Where(l => l != null && l.activeSelf).ToList(); lightsToTurnOn = lightsToTurnOn.OrderBy(l => Random.value).ToList(); lightsToTurnOff = lightsToTurnOff.OrderBy(l => Random.value).ToList(); foreach (var lightObject in lightsToTurnOff) { StartCoroutine(FadeLight(lightObject, false)); yield return new WaitForSeconds(Random.Range(0.05f, 0.2f)); } foreach (var lightObject in lightsToTurnOn) { StartCoroutine(FadeLight(lightObject, true)); yield return new WaitForSeconds(Random.Range(0.05f, 0.2f)); } }
    IEnumerator FadeLight(GameObject lightObject, bool turnOn) { var lightSource = lightObject.GetComponent<UnityEngine.Rendering.Universal.Light2D>(); if (lightSource == null) yield break; float startIntensity = turnOn ? 0f : lightSource.intensity; float endIntensity = turnOn ? 1f : 0f; float timer = 0f; if(turnOn) { lightSource.intensity = 0; lightObject.SetActive(true); } while(timer < lightFadeDuration) { timer += Time.deltaTime; lightSource.intensity = Mathf.Lerp(startIntensity, endIntensity, timer / lightFadeDuration); yield return null; } lightSource.intensity = endIntensity; if(!turnOn) { lightObject.SetActive(false); } }
    void EvacuateAllClients() { ClientPathfinding[] allClients = FindObjectsByType<ClientPathfinding>(FindObjectsSortMode.None); foreach (var client in allClients) { if (client != null) { client.ForceLeave(); } } }
    IEnumerator ManageLunchBreaks(float totalLunchDuration) { if (allClerks == null || allClerks.Count == 0) yield break; float breakDurationPerClerk = totalLunchDuration / allClerks.Count; var shuffledClerks = allClerks.OrderBy(c => Random.value).ToList(); foreach (var clerk in shuffledClerks) { clerk.GoOnBreak(breakDurationPerClerk); yield return new WaitForSeconds(breakDurationPerClerk); } }
    void HandleContinuousSpawning() { SpawningPeriod currentPeriod = periods[currentPeriodIndex]; if (currentPeriod.spawnRate <= 0) return; spawnTimer += Time.deltaTime; if (spawnTimer >= currentPeriod.spawnRate) { spawnTimer = 0; SpawnClientBatch(currentPeriod.spawnBatchSize); } }
    void SpawnClientBatch(int count) { for (int i = 0; i < count; i++) { if (ClientPathfinding.totalClients < maxClientsOnScene) { GameObject client = Instantiate(clientPrefab, spawnPoint.position, Quaternion.identity); ClientPathfinding pathfinding = client.GetComponent<ClientPathfinding>(); pathfinding.Initialize(registrationZoneObject, waitingZoneObject, toiletZoneObject, desk1Object, desk2Object, exitWaypoint); } else break; } }
    IEnumerator SpawnCrowdsDuringPeriod(SpawningPeriod period) { float timeBetweenCrowds = period.durationInSeconds / period.numberOfCrowdsToSpawn; for (int i = 0; i < period.numberOfCrowdsToSpawn; i++) { yield return new WaitForSeconds(timeBetweenCrowds); SpawnClientBatch(period.crowdSpawnCount); } }
    void UpdateUITimer() { if (timeDisplay != null) { SpawningPeriod currentPeriod = periods[currentPeriodIndex]; float timeLeft = currentPeriod.durationInSeconds - periodTimer; string formattedTime = string.Format("{0:00}:{1:00}", Mathf.FloorToInt(timeLeft / 60), Mathf.FloorToInt(timeLeft % 60)); timeDisplay.text = $"Период: {currentPeriod.periodName}\nОсталось: {formattedTime}"; } }
    void CheckCrowdDensity() { if (crowdAudioSource == null || waitingZoneObject == null) return; Collider2D[] clients = Physics2D.OverlapCircleAll(waitingZoneObject.transform.position, 2f, LayerMask.GetMask("Client")); int clientCount = clients.Length; if (clientCount >= minClientsForCrowdSound) { if (!crowdAudioSource.isPlaying) crowdAudioSource.Play(); float volume = Mathf.InverseLerp(minClientsForCrowdSound, maxClientsForFullVolume, clientCount); crowdAudioSource.volume = Mathf.Clamp01(volume); } else { if (crowdAudioSource.isPlaying) crowdAudioSource.Stop(); } }
}