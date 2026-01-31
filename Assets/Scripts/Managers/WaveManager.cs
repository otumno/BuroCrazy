using System.Collections;
using System.Linq;
using Data.Calendar;
using UnityEngine;
using DialogueSystem.Data;
using Scriptables.Audio;
using Characters;
using Data;

namespace Managers
{
    // Файл: Assets/Scripts/Managers/WaveManager.cs
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

        [Header("Архетипы")]
        public ArchetypeDatabase archetypeDatabase;

        [Header("=== ТЕСТОВЫЙ РЕЖИМ ===")]
        [Tooltip("Включить автоматический спавн клиентов")]
        public bool enableAutoSpawn = false;

        private Coroutine spawnCoroutine;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            Debug.Log("[WaveManager] === ТЕСТОВЫЙ РЕЖИМ: Авто-спавн " + (enableAutoSpawn ? "ВКЛЮЧЕН" : "ВЫКЛЮЧЕН") + " ===");

            // Загружаем базу архетипов, если не назначена
            if (archetypeDatabase == null)
            {
                archetypeDatabase = Resources.Load<ArchetypeDatabase>("Databases/ArchetypeDatabase");
                if (archetypeDatabase == null)
                {
                    Debug.LogWarning("[WaveManager] ArchetypeDatabase не найден! Визуальное разнообразие не будет работать.");
                }
            }

            if (TimeManager.Instance != null)
            {
                // 1. Подписываемся на будущие изменения
                TimeManager.Instance.OnPeriodChanged += OnPeriodChanged;

                // 2. --- ФИКС: Проверяем текущее состояние ПРЯМО СЕЙЧАС ---
                // Если TimeManager уже инициализировался и сейчас утро,
                // мы могли пропустить событие. Запускаем проверку вручную.
                var currentSettings = TimeManager.Instance.GetCurrentPeriodSettings();
                if (currentSettings != null && !currentSettings.PeriodType.IsNight())
                {
                    Debug.Log("[WaveManager] Старт сцены: Обнаружено утро, запускаем проверку событий вручную.");
                    int day = TimeManager.Instance.GetCurrentDay();
                    CheckMorningEvents(day);

                    // === ТЕСТОВЫЙ РЕЖИМ: Отключаем авто-спавн ===
                    if (enableAutoSpawn)
                    {
                        int clientsCount = Mathf.RoundToInt(currentSettings.clientCount.Evaluate(day));
                        if (clientsCount > 0 && spawnCoroutine == null)
                            spawnCoroutine = StartCoroutine(SpawnRoutine(currentSettings, clientsCount));
                    }
                    else
                    {
                        Debug.Log("[WaveManager] Авто-спавн отключен. Используйте F1 меню для ручного спавна.");
                    }
                }
            }
        }
		
		public void ForceCheckMorningEvents()
    	{
        	if (TimeManager.Instance == null) return;

        	var currentSettings = TimeManager.Instance.GetCurrentPeriodSettings();
        	if (currentSettings != null && !currentSettings.PeriodType.IsNight())
        	{
            	Debug.Log("[WaveManager] ForceCheckMorningEvents: Принудительная проверка утренних событий.");
            	int day = TimeManager.Instance.GetCurrentDay();
            	CheckMorningEvents(day);

            	// === ТЕСТОВЫЙ РЕЖИМ: Отключаем авто-спавн ===
            	if (enableAutoSpawn)
            	{
                	int clientsCount = Mathf.RoundToInt(currentSettings.clientCount.Evaluate(day));
                	if (clientsCount > 0 && spawnCoroutine == null)
                	{
                    	spawnCoroutine = StartCoroutine(SpawnRoutine(currentSettings, clientsCount));
                	}
            	}
            	else
            	{
                	Debug.Log("[WaveManager] Авто-спавн отключен. Используйте F1 меню для ручного спавна.");
            	}
        	}
    	}

        private void OnPeriodChanged(PeriodSettings settings)
        {
            if (spawnCoroutine != null) StopCoroutine(spawnCoroutine);

            if (settings.PeriodType.IsNight()) return;

            int day = TimeManager.Instance.GetCurrentDay();

            // 1. ПРОВЕРЯЕМ УТРЕННИЕ СОБЫТИЯ (Брифинги, Звонки)
            CheckMorningEvents(day);

            // === ТЕСТОВЫЙ РЕЖИМ: Отключаем авто-спавн ===
            if (enableAutoSpawn)
            {
                int clientsCount = Mathf.RoundToInt(settings.clientCount.Evaluate(day));
                if (clientsCount > 0)
                    spawnCoroutine = StartCoroutine(SpawnRoutine(settings, clientsCount));
            }
            else
            {
                Debug.Log("[WaveManager] Авто-спавн отключен. Используйте F1 меню для ручного спавна.");
            }
        }

        // --- ПРОВЕРКА УСЛОВИЙ (СЮЖЕТНЫЕ ФЛАГИ) ---
        private bool AreSpawnConditionsMet(SpecialVisitorDatabase.ScheduledVisitor visitor)
        {
            // Если ключ флага не задан — считаем, что условий нет, спавним всегда
            if (string.IsNullOrEmpty(visitor.requiredFlagKey)) return true;

            // Проверяем флаг через StoryStateManager
            if (StoryStateManager.Instance != null)
            {
                int actualValue = StoryStateManager.Instance.GetFlag(visitor.requiredFlagKey);
                // Проверяем точное совпадение значения
                return actualValue == visitor.requiredFlagValue;
            }

            // Если менеджера сюжета нет, но условие есть - лучше не спавнить, чтобы не сломать логику
            return false; 
        }

        private void CheckMorningEvents(int day)
        {
            if (specialVisitorsDB == null)
            {
                Debug.LogError("[WaveManager] ОШИБКА: Не назначена база данных SpecialVisitorsDB!");
                return;
            }

            Debug.Log($"[WaveManager] --- НАЧАЛО ПРОВЕРКИ УТРЕННИХ СОБЫТИЙ (День {day}) ---");
            Debug.Log($"[WaveManager] Всего записей в базе: {specialVisitorsDB.visitors.Count}");

            foreach (var v in specialVisitorsDB.visitors)
            {
                string prefix = $"[WaveManager] Гость '{v.name}': ";

                if (v.dayToSpawn != day)
                {
                    Debug.Log(prefix + $"ПРОПУСК. День {v.dayToSpawn} != {day}");
                    continue;
                }

                if (!v.spawnAtStartOfDay)
                {
                    Debug.Log(prefix + $"ПРОПУСК. Галочка 'Spawn At Start Of Day' выключена.");
                    continue;
                }

                if (!AreSpawnConditionsMet(v))
                {
                    string reqFlag = string.IsNullOrEmpty(v.requiredFlagKey) ? "Нет" : $"{v.requiredFlagKey} == {v.requiredFlagValue}";
                    Debug.Log(prefix + $"ПРОПУСК. Условия флага не выполнены. Требуется: {reqFlag}");
                    continue;
                }

                // Если дошли сюда — успех
                Debug.Log(prefix + "<color=green>УСПЕХ! Начинаю спавн.</color>");
                SpawnSpecialClient(v);
            }
            Debug.Log($"[WaveManager] --- КОНЕЦ ПРОВЕРКИ ---");
        }

        private IEnumerator SpawnRoutine(PeriodSettings settings, int totalClients)
        {
            // 3. ПРОВЕРЯЕМ СЮЖЕТНЫХ ГОСТЕЙ ДЛЯ ВОЛНЫ (КТО ПРИХОДИТ ДНЕМ)
            int currentDay = TimeManager.Instance.GetCurrentDay();
            
            var dayGuest = specialVisitorsDB?.visitors
                .Where(v => v.dayToSpawn == currentDay && !v.spawnAtStartOfDay && Random.value <= v.spawnChance)
                .FirstOrDefault(v => AreSpawnConditionsMet(v)); // <--- Проверка флага

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

            if (client != null)
            {
                client.Initialize(waitingZoneObject, exitWaypoint);

                // Применяем архетип для визуального разнообразия
                if (archetypeDatabase != null)
                {
                    ClientArchetype archetype = archetypeDatabase.GetRandomArchetype();
                    if (archetype != null)
                    {
                        var visuals = client.GetComponent<CharacterVisuals>();
                        if (visuals != null)
                        {
                            visuals.SetupVisualDiversity(archetype);
                        }

                        client.SetupFromArchetype(archetype);
                        client.SetupGrumblingFromArchetype(archetype);
                    }
                }
            }
        }

        /// <summary>
        /// Спавн клиента с конкретным архетипом (для дебаг меню)
        /// </summary>
        public void SpawnClientWithArchetype(ClientArchetype archetype)
        {
            if (clientPrefab == null || spawnPoint == null)
            {
                Debug.LogWarning($"[WaveManager] SpawnClientWithArchetype: clientPrefab={(clientPrefab != null)}, spawnPoint={(spawnPoint != null)}");
                return;
            }

            if (archetype == null)
            {
                Debug.LogWarning("[WaveManager] SpawnClientWithArchetype: archetype is null!");
                return;
            }

            Vector3 spawnPos = spawnPoint.position;
            Debug.Log($"[WaveManager] SpawnClientWithArchetype: {archetype.displayName} (groupID: {archetype.groupID}) at {spawnPos}");

            GameObject go = Instantiate(clientPrefab, spawnPos, Quaternion.identity);
            ClientPathfinding client = go.GetComponent<ClientPathfinding>();

            if (client != null)
            {
                var visuals = client.GetComponent<CharacterVisuals>();
                if (visuals != null)
                {
                    visuals.SetupVisualDiversity(archetype);
                }

                client.Initialize(waitingZoneObject, exitWaypoint);

                client.SetupFromArchetype(archetype);
                client.SetupGrumblingFromArchetype(archetype);

                if (visuals != null)
                {
                    Debug.Log($"[WaveManager] Client configured: {archetype.displayName} | group: {archetype.groupID} | body: {(archetype.bodySprite != null ? archetype.bodySprite.name : "NULL")} | hair: {(archetype.hairSprites?.Count ?? 0)} options | outfit: {(archetype.outfitSprites?.Count ?? 0)} options");
                }

                Debug.Log($"[WaveManager] Client spawned successfully: {client.name} = {archetype.displayName} ({archetype.groupID})");
            }
            else
            {
                Debug.LogError("[WaveManager] ClientPathfinding component not found!");
                Destroy(go);
            }
        }

        private void SpawnSpecialClient(SpecialVisitorDatabase.ScheduledVisitor visitorData)
        {
            // 1. ОБРАБОТКА ЗВОНКА (БЕЗ СПАВНА КЛИЕНТА)
            if (visitorData.isRemoteInteraction)
            {
                Debug.Log($"[WaveManager] Входящий звонок: {visitorData.name}");
				
				if (visitorData.arrivalSound != null && AudioManager.Instance != null)
                {
                    // Играем как SFX
                    AudioManager.Instance.PlaySound(SoundID.None, transform.position); // Или используй PlayClipAtPoint
                    AudioSource.PlayClipAtPoint(visitorData.arrivalSound, hiddenSpawnPoint.position);
                }
                
                if (PhoneManager.Instance != null)
                {
                    PhoneManager.Instance.RegisterIncomingCall(visitorData.dialogue);
                }
                return; // Выходим, физический объект не создаем
            }

            // 2. ОБЫЧНЫЙ СПАВН
            if (clientPrefab == null) return;
            GameObject go = Instantiate(clientPrefab, spawnPoint.position, Quaternion.identity);
            ClientPathfinding client = go.GetComponent<ClientPathfinding>();

            if (client != null)
            {
                client.specificDialogue = visitorData.dialogue;
                // Принудительно ставим цель "Аудиенция", если это не звонок
                client.mainGoal = ClientGoal.DirectorAudience;

                client.Initialize(waitingZoneObject, exitWaypoint);

                // Применяем архетип для визуального разнообразия
                if (archetypeDatabase != null)
                {
                    ClientArchetype archetype = archetypeDatabase.GetRandomArchetype();
                    if (archetype != null)
                    {
                        var visuals = client.GetComponent<CharacterVisuals>();
                        if (visuals != null)
                        {
                            visuals.SetupVisualDiversity(archetype);
                        }

                        client.SetupFromArchetype(archetype);
                        client.SetupGrumblingFromArchetype(archetype);
                    }
                }

                Debug.Log($"[WaveManager] Спавн посетителя: {visitorData.name}");
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