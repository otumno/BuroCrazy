// Файл: Assets/Scripts/Managers/WaveManager.cs
using System.Collections;
using System.Linq;
using Data.Calendar;
using UnityEngine;
using DialogueSystem.Data;

namespace Managers
{
    public class WaveManager : MonoBehaviour
    {
        public static WaveManager Instance { get; private set; }

        [Header("Настройки Спавна")]
        public GameObject clientPrefab;
        public Transform spawnPoint;      // Дверь
        public Transform hiddenSpawnPoint; // Точка для звонков (за экраном)
        
        public int maxClientsOnScene = 10;
        public float initialSpawnDelay = 5f;

        [Header("Зоны")]
        public GameObject waitingZoneObject;
        public Waypoint exitWaypoint;

        [Header("Сюжет")]
        public SpecialVisitorDatabase specialVisitorsDB;

        private Coroutine spawnCoroutine;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            if (TimeManager.Instance != null)
                TimeManager.Instance.OnPeriodChanged += OnPeriodChanged;
        }

        private void OnPeriodChanged(PeriodSettings settings)
        {
            if (spawnCoroutine != null) StopCoroutine(spawnCoroutine);

            if (settings.PeriodType.IsNight()) return;

            int day = TimeManager.Instance.GetCurrentDay();

            // 1. ПРОВЕРЯЕМ УТРЕННИЕ СОБЫТИЯ (Брифинги, Звонки)
            CheckMorningEvents(day);

            // 2. ЗАПУСКАЕМ ОБЫЧНУЮ ВОЛНУ
            int clientsCount = Mathf.RoundToInt(settings.clientCount.Evaluate(day));
            if (clientsCount > 0)
                spawnCoroutine = StartCoroutine(SpawnRoutine(settings, clientsCount));
        }

        // --- НОВЫЙ МЕТОД: УТРЕННИЕ ГОСТИ ---
        private void CheckMorningEvents(int day)
        {
            if (specialVisitorsDB == null) return;

            // Ищем всех, кто должен прийти СЕГОДНЯ и УТРОМ
            var morningGuests = specialVisitorsDB.visitors
                .Where(v => v.dayToSpawn == day && v.spawnAtStartOfDay && Random.value <= v.spawnChance)
                .ToList();

            foreach (var guest in morningGuests)
            {
                SpawnSpecialClient(guest);
            }
        }

        private IEnumerator SpawnRoutine(PeriodSettings settings, int totalClients)
        {
            // 3. ПРОВЕРЯЕМ СЮЖЕТНЫХ ГОСТЕЙ ДЛЯ ВОЛНЫ (КТО ПРИХОДИТ ДНЕМ)
            // Исключаем тех, кто уже пришел утром (!v.spawnAtStartOfDay)
            int currentDay = TimeManager.Instance.GetCurrentDay();
            var dayGuest = specialVisitorsDB?.visitors
                .FirstOrDefault(v => v.dayToSpawn == currentDay && !v.spawnAtStartOfDay && Random.value <= v.spawnChance);

            yield return new WaitForSeconds(initialSpawnDelay);

            float duration = settings.durationInSeconds - initialSpawnDelay;
            if (duration <= 0) duration = 1f;

            float interval = duration / totalClients;
            bool guestSpawned = false;

            for (int i = 0; i < totalClients; i++)
            {
                // Внедряем дневного гостя в середину волны
                if (!guestSpawned && dayGuest != null && i >= totalClients / 2)
                {
                    SpawnSpecialClient(dayGuest);
                    guestSpawned = true;
                    yield return new WaitForSeconds(interval);
                }

                int currentClients = FindObjectsByType<ClientPathfinding>(FindObjectsSortMode.None).Length;
                if (currentClients < maxClientsOnScene)
                {
                    SpawnClient();
                }
                yield return new WaitForSeconds(interval);
            }
        }

        public void SpawnClient()
        {
            if (clientPrefab == null || spawnPoint == null) return;
            GameObject go = Instantiate(clientPrefab, spawnPoint.position, Quaternion.identity);
            ClientPathfinding client = go.GetComponent<ClientPathfinding>();
            if (client != null) client.Initialize(waitingZoneObject, exitWaypoint);
        }

        private void SpawnSpecialClient(SpecialVisitorDatabase.ScheduledVisitor visitorData)
        {
            if (clientPrefab == null) return;

            // Выбор точки спавна (Звонок или Человек)
            Transform point = (visitorData.isRemoteInteraction && hiddenSpawnPoint != null) ? hiddenSpawnPoint : spawnPoint;
            if (point == null) point = spawnPoint;

            GameObject go = Instantiate(clientPrefab, point.position, Quaternion.identity);
            ClientPathfinding client = go.GetComponent<ClientPathfinding>();
            
            if (client != null)
            {
                client.specificDialogue = visitorData.dialogue;
                
                if (visitorData.isRemoteInteraction)
                {
                    // Звонок / Скрытый
                    client.InitializeRemote(visitorData.deskIconOverride);
                    
                    // Принудительно создаем иконку, так как он не дойдет до стола
                    ForceCreateDocumentIcon(client);
                }
                else
                {
                    // Обычный (пешком)
                    client.Initialize(waitingZoneObject, exitWaypoint);
                }
                
                Debug.Log($"[WaveManager] Сюжетный спавн ({visitorData.name}) - Утро: {visitorData.spawnAtStartOfDay}");
            }
        }

        private void ForceCreateDocumentIcon(ClientPathfinding client)
        {
            if (StartOfDayPanel.Instance != null)
            {
                StartOfDayPanel.Instance.CreateDocumentIcon(client);
            }
        }
        
        private void OnDestroy()
        {
            if (TimeManager.Instance)
                TimeManager.Instance.OnPeriodChanged -= OnPeriodChanged;
        }
    }
}