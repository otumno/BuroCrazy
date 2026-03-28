using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Data.Calendar;
using UnityEngine;
using DialogueSystem.Data;
using Scriptables.Audio;
using Characters;
using Data;

namespace Managers
{
    /// <summary>
    /// Запрос на спавн клиента, ожидающий в очереди
    /// </summary>
    [System.Serializable]
    public class QueuedClientRequest
    {
        public ClientArchetype archetype;
        public float enqueueTime;
        public string requestedBy; // "Debug", "Wave", "Special"
        public int priority; // 0 = normal, 1 = high (special visitors)

        public QueuedClientRequest(ClientArchetype arch, string source, int prio = 0)
        {
            archetype = arch;
            enqueueTime = Time.time;
            requestedBy = source;
            priority = prio;
        }
    }

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

        [Header("=== ГЛОБАЛЬНЫЙ CAPACITY ===")]
        [Tooltip("Максимальное количество клиентов в офисе одновременно")]
        public int officeCapacity = 150;

        [Tooltip("Текущее количество клиентов в офисе (Read Only)")]
        public int currentClientsInOffice = 0;

        [Tooltip("Клиенты, которые не смогли попасть в офис (переполнение)")]
        public int overflowClientsCount = 0;

        [Header("=== CAPACITY QUEUE ===")]
        [Tooltip("Очередь ожидающих клиентов (для отложенного спавна)")]
        public List<QueuedClientRequest> pendingSpawnQueue = new List<QueuedClientRequest>();

        [Tooltip("Максимум клиентов в очереди ожидания")]
        public int maxQueueSize = 50;

        [Tooltip("Интервал проверки очереди (секунды)")]
        public float queueCheckInterval = 1f;

        [Header("=== ДНЕВНОЙ ПЛАН СПАВНА (НОВАЯ СИСТЕМА) ===")]
        [Tooltip("Дневной план спавна (автоматически перестраивается при смене дня)")]
        public Dictionary<CalendarDayPeriodType, TimeDistributionCalculator.PeriodDistribution> dailySpawnPlan;

        [Tooltip("Общее количество клиентов на сегодня")]
        public int todayTotalClients = 0;

        private Coroutine spawnCoroutine;
        private Coroutine queueCheckerCoroutine;

        private int lastCheckedMorningDay = -1;

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

            // Подписываемся на обновление потоков от ProgressionManager
            if (ProgressionManager.Instance != null)
            {
                ProgressionManager.Instance.OnDailyFlowUpdated += OnDailyFlowUpdated;
                Debug.Log("[WaveManager] Подписан на ProgressionManager.OnDailyFlowUpdated");

                // Сразу строим дневной план
                RebuildDailySpawnPlan();
            }
            else
            {
                Debug.LogWarning("[WaveManager] ProgressionManager.Instance не найден! Новая система потоков не будет работать.");
            }

            if (TimeManager.Instance != null)
            {
                TimeManager.Instance.OnPeriodChanged += OnPeriodChanged;

                // Проверяем текущее состояние
                var currentSettings = TimeManager.Instance.GetCurrentPeriodSettings();
                if (currentSettings != null && !currentSettings.PeriodType.IsNight())
                {
                    Debug.Log("[WaveManager] Старт сцены: Обнаружено утро, запускаем проверку событий вручную.");
                    int day = TimeManager.Instance.GetCurrentDay();
                    CheckMorningEvents(day);

                    if (enableAutoSpawn)
                    {
                        StartSpawningForCurrentPeriod();
                    }
                }
            }
        }

        /// <summary>
        /// Вызывается когда ProgressionManager обновляет потоки (новый день или захват региона)
        /// </summary>
        private void OnDailyFlowUpdated()
        {
            Debug.Log("[WaveManager] OnDailyFlowUpdated: Перестраиваем дневной план...");
            RebuildDailySpawnPlan();
        }

        /// <summary>
        /// Перестраивает дневной план спавна на основе текущих регионов
        /// </summary>
        public void RebuildDailySpawnPlan()
        {
            if (ProgressionManager.Instance == null || archetypeDatabase == null)
            {
                Debug.LogWarning("[WaveManager] RebuildDailySpawnPlan: ProgressionManager или ArchetypeDatabase не инициализированы!");
                return;
            }

            // Получаем активные регионы и общий поток
            var activeRegions = ProgressionManager.Instance.GetActiveRegions();
            int totalFlow = ProgressionManager.Instance.GetTotalDailyFlow();

            todayTotalClients = totalFlow;
            dailySpawnPlan = new Dictionary<CalendarDayPeriodType, TimeDistributionCalculator.PeriodDistribution>();

            if (activeRegions.Count == 0)
            {
                Debug.Log("[WaveManager] Нет активных регионов. Дневной план пуст.");
                return;
            }

            if (totalFlow <= 0)
            {
                Debug.Log("[WaveManager] Общий поток равен 0. Дневной план пуст.");
                return;
            }

            // Получаем все доступные архетипы
            var availableArchetypes = archetypeDatabase.allArchetypes?
                .Where(a => a != null)
                .ToList() ?? new List<ClientArchetype>();

            if (availableArchetypes.Count == 0)
            {
                Debug.LogWarning("[WaveManager] В базе архетипов нет данных!");
                return;
            }

            // Распределяем по периодам
            var periods = TimeDistributionCalculator.GetActivePeriods();
            dailySpawnPlan = TimeDistributionCalculator.CalculateDistribution(totalFlow, availableArchetypes, periods);

            Debug.Log($"[WaveManager] Дневной план перестроен: {todayTotalClients} клиентов, {activeRegions.Count} регионов");
        }

        /// <summary>
        /// Запускает спавн для текущего периода на основе дневного плана
        /// </summary>
        public void StartSpawningForCurrentPeriod()
        {
            if (TimeManager.Instance == null) return;

            var currentPeriod = TimeManager.Instance.GetCurrentPeriodType();
            if (currentPeriod.IsNight())
            {
                Debug.Log("[WaveManager] Ночь - спавн не запускается");
                return;
            }

            if (dailySpawnPlan == null || !dailySpawnPlan.ContainsKey(currentPeriod))
            {
                Debug.LogWarning($"[WaveManager] Дневной план не содержит периода {currentPeriod}!");
                return;
            }

            var periodDistribution = dailySpawnPlan[currentPeriod];
            int clientsForPeriod = periodDistribution.clientCount;

            if (clientsForPeriod <= 0)
            {
                Debug.Log($"[WaveManager] Нет клиентов для периода {currentPeriod}");
                return;
            }

            Debug.Log($"[WaveManager] Запускаем спавн для {currentPeriod}: {clientsForPeriod} клиентов");

            var currentSettings = TimeManager.Instance.GetCurrentPeriodSettings();
            if (currentSettings != null)
            {
                StartCoroutine(SpawnRoutineFromPlan(currentPeriod, currentSettings, clientsForPeriod));
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

            	   if (enableAutoSpawn)
            	   {
            	       StartSpawningForCurrentPeriod();
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

            if (settings.PeriodType.IsNight())
            {
                Debug.Log("[WaveManager] Ночь - спавн не запускается");
                return;
            }

            int day = TimeManager.Instance.GetCurrentDay();

            // ПРОВЕРЯЕМ УТРЕННИЕ СОБЫТИЯ
            CheckMorningEvents(day);

            // Используем НОВУЮ СИСТЕМУ с дневным планом
            if (enableAutoSpawn)
            {
                StartSpawningForCurrentPeriod();
            }
            else
            {
                Debug.Log("[WaveManager] Авто-спавн отключен. Используйте F1 меню для ручного спавна.");
            }
        }

        /// <summary>
        /// Новая корутина спавна на основе дневного плана
        /// </summary>
        private IEnumerator SpawnRoutineFromPlan(CalendarDayPeriodType period, PeriodSettings settings, int totalClients)
        {
            if (dailySpawnPlan == null || !dailySpawnPlan.ContainsKey(period))
            {
                Debug.LogError($"[WaveManager] SpawnRoutineFromPlan: дневной план не содержит периода {period}!");
                yield break;
            }

            var periodDistribution = dailySpawnPlan[period];
            var archetypesToSpawn = periodDistribution.archetypes;

            if (archetypesToSpawn == null || archetypesToSpawn.Count == 0)
            {
                Debug.LogWarning($"[WaveManager] Нет архетипов для периода {period}!");
                yield break;
            }

            // ПРОВЕРЯЕМ СЮЖЕТНЫХ ГОСТЕЙ ДЛЯ ВОЛНЫ
            int currentDay = TimeManager.Instance.GetCurrentDay();
            var dayGuest = specialVisitorsDB?.visitors
                .Where(v => v.dayToSpawn == currentDay && !v.spawnAtStartOfDay && Random.value <= v.spawnChance)
                .FirstOrDefault(v => AreSpawnConditionsMet(v));

            yield return new WaitForSeconds(initialSpawnDelay);

            float duration = settings.durationInSeconds - initialSpawnDelay;
            if (duration <= 0) duration = 1f;

            float interval = duration / totalClients;
            bool guestSpawned = false;
            int archetypeIndex = 0;

            for (int i = 0; i < totalClients; i++)
            {
                // Внедряем дневного гостя в середину волны
                if (!guestSpawned && dayGuest != null && i >= totalClients / 2)
                {
                    SpawnSpecialClient(dayGuest);
                    guestSpawned = true;
                    yield return new WaitForSeconds(interval);
                }

                // Проверяем capacity перед спавном
                UpdateClientsCount();
                if (currentClientsInOffice >= officeCapacity)
                {
                    // Добавляем в очередь ожидания
                    var archetype = archetypesToSpawn[archetypeIndex % archetypesToSpawn.Count];
                    TryAddToQueue(archetype, $"Plan_{period}", 0);
                }
                else
                {
                    // Спавним запланированного клиента
                    var archetype = archetypesToSpawn[archetypeIndex % archetypesToSpawn.Count];
                    SpawnClientInternal(archetype, $"Plan_{period}");
                    archetypeIndex++;
                }

                yield return new WaitForSeconds(interval);
            }

            Debug.Log($"[WaveManager] SpawnRoutineFromPlan завершён для {period}: {totalClients} клиентов");
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
            if (lastCheckedMorningDay == day) return;
            lastCheckedMorningDay = day;

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
            SpawnClientWithArchetype(archetype, "Debug");
        }

        /// <summary>
        /// Спавн клиента с конкретным архетипом и указанием источника
        /// </summary>
        public void SpawnClientWithArchetype(ClientArchetype archetype, string requestedBy, int priority = 0)
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

            // Проверка ночи
            if (IsNightTime())
            {
                Debug.LogWarning($"[WaveManager] НОЧЬ! Клиент {archetype.displayName} не может появиться.");
                return;
            }

            UpdateClientsCount();

            if (currentClientsInOffice >= officeCapacity)
            {
                // Пытаемся добавить в очередь ожидания
                if (TryAddToQueue(archetype, requestedBy, priority))
                {
                    Debug.Log($"[WaveManager] OFFICE FULL! Client {archetype.displayName} added to waiting queue. Queue: {pendingSpawnQueue.Count}/{maxQueueSize}");
                }
                else
                {
                    overflowClientsCount++;
                    Debug.LogWarning($"[WaveManager] OFFICE CAPACITY REACHED! ({currentClientsInOffice}/{officeCapacity}). Queue is full! Overflow count: {overflowClientsCount}");
                }
                return;
            }

            // Место есть - спавним сразу
            SpawnClientInternal(archetype, requestedBy);
        }

        /// <summary>
        /// Пытается добавить клиента в очередь ожидания
        /// </summary>
        private bool TryAddToQueue(ClientArchetype archetype, string requestedBy, int priority)
        {
            if (pendingSpawnQueue.Count >= maxQueueSize) return false;

            var request = new QueuedClientRequest(archetype, requestedBy, priority);
            pendingSpawnQueue.Add(request);

            // Запускаем проверку очереди если ещё не запущена
            if (queueCheckerCoroutine == null)
            {
                queueCheckerCoroutine = StartCoroutine(ProcessSpawnQueue());
            }

            return true;
        }

        /// <summary>
        /// Обрабатывает очередь ожидания - пытается заспавнить клиентов когда есть место
        /// </summary>
        private IEnumerator ProcessSpawnQueue()
        {
            while (pendingSpawnQueue.Count > 0)
            {
                UpdateClientsCount();

                if (currentClientsInOffice >= officeCapacity)
                {
                    // Места нет, ждём
                    yield return new WaitForSeconds(queueCheckInterval);
                    continue;
                }

                // Проверяем ночь
                if (IsNightTime())
                {
                    // Ночью очищаем очередь (все уходят)
                    Debug.Log($"[WaveManager] НОЧЬ! Очистка очереди ожидания: {pendingSpawnQueue.Count} клиентов удалено.");
                    overflowClientsCount += pendingSpawnQueue.Count;
                    pendingSpawnQueue.Clear();
                    break;
                }

                // Проверяем не истёк ли период для клиентов
                var currentPeriod = TimeManager.Instance?.GetCurrentPeriodType() ?? CalendarDayPeriodType.Day;
                var expiredClients = new List<QueuedClientRequest>();

                foreach (var request in pendingSpawnQueue)
                {
                    // Если клиент ждёт слишком долго (>30 секунд) или период ночи - удаляем
                    float waitTime = Time.time - request.enqueueTime;
                    if (waitTime > 30f || currentPeriod.IsNight())
                    {
                        expiredClients.Add(request);
                    }
                }

                if (expiredClients.Count > 0)
                {
                    overflowClientsCount += expiredClients.Count;
                    foreach (var client in expiredClients)
                    {
                        pendingSpawnQueue.Remove(client);
                        Debug.Log($"[WaveManager] Клиент {client.archetype.displayName} не дождался и ушёл. WaitTime: {Time.time - client.enqueueTime:F1}s");
                    }
                    continue;
                }

                // Спавним клиента с наивысшим приоритетом
                var nextClient = pendingSpawnQueue.OrderByDescending(x => x.priority).First();
                pendingSpawnQueue.Remove(nextClient);

                Debug.Log($"[WaveManager] Queue processed: spawning {nextClient.archetype.displayName} (queue: {pendingSpawnQueue.Count})");
                SpawnClientInternal(nextClient.archetype, $"Queue_{nextClient.requestedBy}");

                yield return new WaitForSeconds(0.5f); // Небольшая пауза между спавнами из очереди
            }

            queueCheckerCoroutine = null;
        }

        /// <summary>
        /// Внутренний метод - реальный спавн клиента
        /// </summary>
        private void SpawnClientInternal(ClientArchetype archetype, string requestedBy)
        {
            Vector3 spawnPos = spawnPoint.position;
            Debug.Log($"[WaveManager] SpawnClientInternal: {archetype.displayName} (groupID: {archetype.groupID}) from {requestedBy}");

            GameObject go = Instantiate(clientPrefab, spawnPos, Quaternion.identity);
            ClientPathfinding client = go.GetComponent<ClientPathfinding>();

            if (client != null)
            {
                // СНАЧАЛА применяем архетип - это устанавливает spriteCollection и gender
                client.SetupFromArchetype(archetype);
                client.SetupGrumblingFromArchetype(archetype);

                // СНАЧАЛА настраиваем визуал - body sprite установится в archetype body sprite
                var visuals = client.GetComponent<CharacterVisuals>();
                if (visuals != null)
                {
                    visuals.SetupVisualDiversity(archetype);
                }

                // ПОТОМ инициализируем - теперь bodySpriteAlreadySet = true и спрайт НЕ будет перезаписан
                client.Initialize(waitingZoneObject, exitWaypoint);

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
                    AudioManager.Instance.PlaySound(SoundID.None, transform.position);
                    AudioSource.PlayClipAtPoint(visitorData.arrivalSound, hiddenSpawnPoint.position);
                }

                if (PhoneManager.Instance != null)
                {
                    PhoneManager.Instance.RegisterIncomingCall(visitorData.dialogue);
                }
                return;
            }

            // 2. ОБЫЧНЫЙ СПАВН - используем очередь с высоким приоритетом
            if (clientPrefab == null) return;

            // Специальные посетители получают случайный архетип
            ClientArchetype archetype = null;
            if (archetypeDatabase != null)
            {
                archetype = archetypeDatabase.GetRandomArchetype();
            }

            // Используем новый метод с приоритетом 10 для специальных посетителей
            SpawnClientWithArchetype(archetype, $"Special_{visitorData.name}", 10);

            // Если клиент был создан (не в очереди), настраиваем его
            if (archetype != null)
            {
                // Примечание: настройка специального клиента происходит в SpawnClientInternal
                // но нам нужно добавить specificDialogue и цель
                // Это можно сделать через событие или отдельный метод
                Debug.Log($"[WaveManager] Special visitor queued/spawned: {visitorData.name}");
            }
        }

        /// <summary>
        /// Применяет архетип к клиенту (используется для специальных посетителей)
        /// </summary>
        private void ApplyArchetypeToClient(ClientPathfinding client, ClientArchetype archetype)
        {
            var visuals = client.GetComponent<CharacterVisuals>();
            if (visuals != null)
            {
                visuals.SetupVisualDiversity(archetype);
            }

            client.SetupFromArchetype(archetype);
            client.SetupGrumblingFromArchetype(archetype);
        }

        /// <summary>
        /// Обновляет счетчик клиентов в офисе
        /// </summary>
        public void UpdateClientsCount()
        {
            currentClientsInOffice = FindObjectsByType<ClientPathfinding>(FindObjectsSortMode.None).Length;
        }

        /// <summary>
        /// Проверяет доступность места в офисе
        /// </summary>
        public bool HasOfficeSpace()
        {
            UpdateClientsCount();
            return currentClientsInOffice < officeCapacity;
        }

        /// <summary>
        /// Получает текущее количество клиентов в офисе
        /// </summary>
        public int GetClientsInOffice()
        {
            UpdateClientsCount();
            return currentClientsInOffice;
        }

        /// <summary>
        /// Проверяет, сейчас ли ночь (клиенты не должны спавниться)
        /// </summary>
        public bool IsNightTime()
        {
            if (TimeManager.Instance == null) return false;
            return TimeManager.Instance.IsNight();
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

            if (ProgressionManager.Instance != null)
                ProgressionManager.Instance.OnDailyFlowUpdated -= OnDailyFlowUpdated;
        }
    }
}