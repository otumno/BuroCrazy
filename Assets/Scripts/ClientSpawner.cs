using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;

[System.Serializable]
public class LightingPreset
{
    [Tooltip("Цвет глобального освещения для этого периода")]
    public Color lightColor = Color.white;
    [Tooltip("Интенсивность (яркость) глобального света. 1 - нормально, <1 - темнее, >1 - ярче")]
    [Range(0f, 2f)]
    public float lightIntensity = 1f;
}

[System.Serializable]
public class SpawningPeriod
{
    [Tooltip("Название периода (например, 'Утро')")]
    public string periodName;
    [Tooltip("Продолжительность этого периода в секундах")]
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
    [Tooltip("Объекты, которые должны быть включены в этот период (например, фонари)")]
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
    [Tooltip("Сюда перетащите ВСЕ управляемые источники света (фонари и т.д.)")]
    public List<GameObject> allControllableLights;
    [Tooltip("Как быстро лампы будут загораться/тухнуть (в секундах)")]
    public float lightFadeDuration = 0.5f;
    public TextMeshProUGUI timeDisplay;
    public UnityEngine.Rendering.Universal.Light2D globalLight;
    [Tooltip("Перетащите сюда всех клерков со сцены")]
    public List<ClerkController> allClerks;
    
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

    void Start()
    {
        if (periods == null || periods.Length == 0)
        {
            Debug.LogError("Не настроены периоды спавна в ClientSpawner!", this);
            this.enabled = false; return;
        }
        
        currentPeriodIndex = -1;
        GoToNextPeriod();
    }

    void Update()
    {
        periodTimer += Time.deltaTime;
        
        UpdateUITimer();
        UpdateLighting();

        if (periodTimer >= periods[currentPeriodIndex].durationInSeconds)
        {
            GoToNextPeriod();
        }
        
        HandleContinuousSpawning();
        CheckCrowdDensity();
    }
    
    void UpdateLighting()
    {
        if (globalLight == null || previousPeriod == null) return;
        SpawningPeriod currentPeriod = periods[currentPeriodIndex];
        float progress = Mathf.Clamp01(periodTimer / currentPeriod.durationInSeconds);
        globalLight.color = Color.Lerp(previousPeriod.lightingSettings.lightColor, currentPeriod.lightingSettings.lightColor, progress);
        globalLight.intensity = Mathf.Lerp(previousPeriod.lightingSettings.lightIntensity, currentPeriod.lightingSettings.lightIntensity, progress);
    }

    void GoToNextPeriod()
    {
        previousPeriod = (currentPeriodIndex < 0) ? periods[periods.Length - 1] : periods[currentPeriodIndex];
        currentPeriodIndex = (currentPeriodIndex + 1) % periods.Length;
        StartNewPeriod();
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
        if (periodNameLower == "ночь")
        {
            EvacuateAllClients();
            if (allClerks != null) foreach (var clerk in allClerks) clerk.GoToNightPost();
        }
        else if (periodNameLower == "обед")
        {
            clerkBreakCoroutine = StartCoroutine(ManageLunchBreaks(newPeriod.durationInSeconds));
        }
        else
        {
            // Используем новый, исправленный метод
            if (allClerks != null) foreach (var clerk in allClerks) clerk.GoToWorkStation();
        }

        lightManagementCoroutine = StartCoroutine(ManageLocalLightsSmoothly(newPeriod));

        if (previousPeriod == null)
        {
            globalLight.color = newPeriod.lightingSettings.lightColor;
            globalLight.intensity = newPeriod.lightingSettings.lightIntensity;
        }

        if (newPeriod.numberOfCrowdsToSpawn > 0 && newPeriod.crowdSpawnCount > 0)
        {
            crowdSpawnCoroutine = StartCoroutine(SpawnCrowdsDuringPeriod(newPeriod));
        }
    }

    IEnumerator ManageLocalLightsSmoothly(SpawningPeriod period)
    {
        var lightsToTurnOn = period.lightsToEnable.Where(l => l != null && !l.activeSelf).ToList();
        var lightsToTurnOff = allControllableLights.Except(period.lightsToEnable).Where(l => l != null && l.activeSelf).ToList();
        
        lightsToTurnOn = lightsToTurnOn.OrderBy(l => Random.value).ToList();
        lightsToTurnOff = lightsToTurnOff.OrderBy(l => Random.value).ToList();

        foreach (var lightObject in lightsToTurnOff)
        {
            StartCoroutine(FadeLight(lightObject, false));
            yield return new WaitForSeconds(Random.Range(0.05f, 0.2f));
        }

        foreach (var lightObject in lightsToTurnOn)
        {
            StartCoroutine(FadeLight(lightObject, true));
            yield return new WaitForSeconds(Random.Range(0.05f, 0.2f));
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
        if(!turnOn)
        {
            lightObject.SetActive(false);
        }
    }
    
    void EvacuateAllClients()
    {
        ClientPathfinding[] allClients = FindObjectsByType<ClientPathfinding>(FindObjectsSortMode.None);
        Debug.Log($"Наступила ночь. Принудительно выгоняем {allClients.Length} клиентов.");

        foreach (var client in allClients)
        {
            if (client != null)
            {
                client.ForceLeave();
            }
        }
    }

    IEnumerator ManageLunchBreaks(float totalLunchDuration)
    {
        if (allClerks == null || allClerks.Count == 0) yield break;

        float breakDurationPerClerk = totalLunchDuration / allClerks.Count;
        var shuffledClerks = allClerks.OrderBy(c => Random.value).ToList();

        foreach (var clerk in shuffledClerks)
        {
            clerk.GoOnBreak(breakDurationPerClerk);
            yield return new WaitForSeconds(breakDurationPerClerk);
        }
    }
    
    void HandleContinuousSpawning()
    {
        SpawningPeriod currentPeriod = periods[currentPeriodIndex];
        if (currentPeriod.spawnRate <= 0) return;
        spawnTimer += Time.deltaTime;
        if (spawnTimer >= currentPeriod.spawnRate)
        {
            spawnTimer = 0;
            SpawnClientBatch(currentPeriod.spawnBatchSize);
        }
    }
    
    void SpawnClientBatch(int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (ClientPathfinding.totalClients < maxClientsOnScene)
            {
                GameObject client = Instantiate(clientPrefab, spawnPoint.position, Quaternion.identity);
                ClientPathfinding pathfinding = client.GetComponent<ClientPathfinding>();
                pathfinding.Initialize(registrationZoneObject, waitingZoneObject, toiletZoneObject, desk1Object, desk2Object, exitWaypoint);
            }
            else break;
        }
    }
    
    IEnumerator SpawnCrowdsDuringPeriod(SpawningPeriod period)
    {
        float timeBetweenCrowds = period.durationInSeconds / period.numberOfCrowdsToSpawn;
        for (int i = 0; i < period.numberOfCrowdsToSpawn; i++)
        {
            yield return new WaitForSeconds(timeBetweenCrowds);
            Debug.Log($"Спавн толпы из {period.crowdSpawnCount} клиентов в период '{period.periodName}'");
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
        Collider2D[] clients = Physics2D.OverlapCircleAll(waitingZoneObject.transform.position, 2f, LayerMask.GetMask("Client"));
        int clientCount = clients.Length;
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
}