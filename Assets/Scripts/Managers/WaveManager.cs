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

    /// <summary>
    /// Запланированный посетитель к директору (гарантированный поток)
    /// </summary>
    [System.Serializable]
    public class DirectorVisitorPlan
    {
        public ClientArchetype archetype;
        public ClientGoal forcedGoal;
        public float spawnTime; // Секунд от начала дня
        public int day;
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

        /// <summary>
        /// Точное расписание спавна (время каждого клиента от начала дня)
        /// </summary>
        public List<float> SpawnSchedule { get; private set; } = new List<float>();

        [Header("=== ГАРАНТИРОВАННЫЙ ДИРЕКТОРСКИЙ ПОТОК ===")]
        [Tooltip("Базовое количество посетителей к директору в день (1-й день после анлока первого региона)")]
        public int baseDirectorVisitorsPerDay = 1;
        [Tooltip("Дополнительные посетители за каждый регион сверх первого")]
        public int extraDirectorVisitorsPerRegion = 1;
        [Tooltip("Кап посетителей от разогрева (1->2->3, далее 3)")]
        public int directorVisitorsCap = 3;
        [Tooltip("Длительность разогрева (дней) до капа")]
        public int directorRampUpDays = 3;
        [Tooltip("Таймаут ожидания директора/приёма (секунды). По истечении клиент уходит")]
        public float directorWaitTimeout = 60f;
        [Tooltip("Штраф репутации (HP) за уход клиента из приёмной по таймауту")]
        public float directorRejectionReputationPenalty = 5f;
        [Tooltip("Штраф Влияния за уход клиента из приёмной по таймауту")]
        public int directorRejectionInfluencePenalty = 1;

        [Tooltip("Запланированные директорские посетители на сегодня")]
        public List<DirectorVisitorPlan> directorVisitorsToday = new List<DirectorVisitorPlan>();

        /// <summary>
        /// Событие обновления плана спавна (для подписки TimelineController и других систем)
        /// </summary>
        public System.Action OnSpawnPlanUpdated;

        private Coroutine spawnCoroutine;
        private Coroutine queueCheckerCoroutine;
        private Coroutine directorSpawnCoroutine;

        private int lastCheckedMorningDay = -1;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            // Debug.Log("[WaveManager] === ТЕСТОВЫЙ РЕЖИМ: Авто-спавн " + (enableAutoSpawn ? "ВКЛЮЧЕН" : "ВЫКЛЮЧЕН") + " ===");

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
                // Debug.Log("[WaveManager] Подписан на ProgressionManager.OnDailyFlowUpdated");

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
                    // Debug.Log("[WaveManager] Старт сцены: Обнаружено утро, запускаем проверку событий вручную.");
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
            // Debug.Log("[WaveManager] OnDailyFlowUpdated: Перестраиваем дневной план...");
            RebuildDailySpawnPlan();

            if (enableAutoSpawn && TimeManager.Instance != null)
            {
                var currentPeriod = TimeManager.Instance.GetCurrentPeriodType();
                if (!currentPeriod.IsNight() && dailySpawnPlan != null && dailySpawnPlan.ContainsKey(currentPeriod) && dailySpawnPlan[currentPeriod].clientCount > 0)
                {
                    Debug.Log($"[WaveManager] OnDailyFlowUpdated: принудительный запуск спавна для {currentPeriod}");
                    StartSpawningForCurrentPeriod();
                }
            }
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
                // Debug.Log("[WaveManager] Нет активных регионов. Дневной план пуст.");
                return;
            }

            if (totalFlow <= 0)
            {
                // Debug.Log("[WaveManager] Общий поток равен 0. Дневной план пуст.");
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

            // Строим точное расписание спавна
            BuildSpawnSchedule();

            // Планируем директорских посетителей (добавляет в SpawnSchedule)
            int currentDay = TimeManager.Instance != null ? TimeManager.Instance.GetCurrentDay() : 1;
            ScheduleDirectorVisitors(currentDay);

            // Оповещаем подписчиков (TimelineController и др.)
            OnSpawnPlanUpdated?.Invoke();

            // Debug.Log($"[WaveManager] Дневной план перестроен: {todayTotalClients} клиентов, {activeRegions.Count} регионов");
        }

        /// <summary>
        /// Строит точное расписание спавна (время для каждого клиента от начала дня)
        /// </summary>
        private void BuildSpawnSchedule()
        {
            SpawnSchedule.Clear();

            if (TimeManager.Instance == null || TimeManager.Instance.mainCalendarDay == null)
                return;

            var day = TimeManager.Instance.mainCalendarDay;
            float currentTime = 0f;

            foreach (var ps in day.periodSettings)
            {
                if (ps.PeriodType.IsNight()) // ночные периоды пропускаем
                {
                    currentTime += ps.durationInSeconds;
                    continue;
                }

                if (!dailySpawnPlan.ContainsKey(ps.PeriodType))
                {
                    currentTime += ps.durationInSeconds;
                    continue;
                }

                int totalClients = dailySpawnPlan[ps.PeriodType].clientCount;
                float periodDuration = ps.durationInSeconds;

                // Генерируем времена спавна внутри периода
                for (int i = 0; i < totalClients; i++)
                {
                    float t = currentTime + (periodDuration * (i + 1) / (totalClients + 1));
                    // Небольшой рандомный сдвиг в пределах ±15% от интервала
                    float jitter = Random.Range(
                        -periodDuration / (totalClients + 1) * 0.15f,
                        periodDuration / (totalClients + 1) * 0.15f);
                    t += jitter;
                    t = Mathf.Clamp(t, currentTime + 1f, currentTime + periodDuration - 1f);
                    SpawnSchedule.Add(t);
                    Debug.Log($"[WaveManager]   + событие: {t:F1} сек");
                }

                currentTime += periodDuration;
            }

            // Сортируем времена
            SpawnSchedule.Sort();
            Debug.Log($"[WaveManager] Построен точный план спавна: {SpawnSchedule.Count} событий");
        }

        /// <summary>
        /// Рассчитывает количество директорских посетителей на сегодня.
        /// 1-й регион: разогрев 1->2->3 за directorRampUpDays дней.
        /// Каждый следующий регион: +extraDirectorVisitorsPerRegion.
        /// </summary>
        public int CalculateDirectorVisitorCount()
        {
            if (ProgressionManager.Instance == null) return baseDirectorVisitorsPerDay;

            var activeRegions = ProgressionManager.Instance.GetActiveRegions();
            int totalRegions = activeRegions.Count;

            if (totalRegions == 0) return 0;

            // Находим регион с наименьшим daysOwned (это первый открытый, он и разогревается)
            var firstRegion = activeRegions.OrderBy(r => r.unlockDay).FirstOrDefault();
            int dayInFirstRegion = firstRegion != null ? Mathf.Max(1, firstRegion.daysOwned) : 1;

            int fromRampUp = Mathf.Min(directorVisitorsCap, baseDirectorVisitorsPerDay + (dayInFirstRegion - 1) / Mathf.Max(1, directorRampUpDays / (directorVisitorsCap - baseDirectorVisitorsPerDay + 1)));
            int fromRegions = Mathf.Max(0, totalRegions - 1) * extraDirectorVisitorsPerRegion;

            int total = fromRampUp + fromRegions;
            Debug.Log($"[WaveManager] Директорских посетителей на сегодня: {total} (регионов={totalRegions}, день_в_первом={dayInFirstRegion}, от_разогрева={fromRampUp}, от_регионов={fromRegions})");
            return total;
        }

        /// <summary>
        /// Планирует директорских посетителей на сегодня, равномерно распределяя по дневным периодам.
        /// </summary>
        public void ScheduleDirectorVisitors(int day)
        {
            directorVisitorsToday.Clear();

            if (archetypeDatabase == null || TimeManager.Instance == null || TimeManager.Instance.mainCalendarDay == null)
                return;

            int count = CalculateDirectorVisitorCount();
            if (count <= 0) return;

            // Собираем дневные периоды с их границами
            var dayConfig = TimeManager.Instance.mainCalendarDay;
            var dayPeriods = new List<(float start, float end)>();
            float currentTime = 0f;
            foreach (var ps in dayConfig.periodSettings)
            {
                if (ps.PeriodType.IsNight())
                {
                    currentTime += ps.durationInSeconds;
                    continue;
                }
                dayPeriods.Add((currentTime, currentTime + ps.durationInSeconds));
                currentTime += ps.durationInSeconds;
            }

            if (dayPeriods.Count == 0) return;

            // Равномерно распределяем count клиентов по периодам
            for (int i = 0; i < count; i++)
            {
                // Берём i-й период (с обтеканием, если клиентов больше чем периодов)
                var period = dayPeriods[i % dayPeriods.Count];
                float t = Mathf.Lerp(period.start + 2f, period.end - 2f, (i % 2 == 0) ? 0.3f : 0.7f);

                // Рандомный архетип из базы
                var archetype = archetypeDatabase.GetRandomArchetype();
                if (archetype == null) continue;

                // 50/50 DirectorApproval / DirectorAudience
                var goal = (Random.value < 0.5f) ? ClientGoal.DirectorApproval : ClientGoal.DirectorAudience;

                directorVisitorsToday.Add(new DirectorVisitorPlan
                {
                    archetype = archetype,
                    forcedGoal = goal,
                    spawnTime = t,
                    day = day
                });

                // Добавляем в общий SpawnSchedule для отображения на таймлайне
                SpawnSchedule.Add(t);
            }

            SpawnSchedule.Sort();
            Debug.Log($"[WaveManager] Запланировано {directorVisitorsToday.Count} директорских посетителей на день {day}");
        }

        /// <summary>
        /// Запускает корутину спавна запланированных директорских посетителей
        /// </summary>
        public void StartSpawningDirectorVisitors()
        {
            if (directorSpawnCoroutine != null) StopCoroutine(directorSpawnCoroutine);
            if (directorVisitorsToday.Count == 0) return;
            directorSpawnCoroutine = StartCoroutine(SpawnDirectorVisitorsRoutine());
        }

        private IEnumerator SpawnDirectorVisitorsRoutine()
        {
            float currentTime = TimeManager.Instance != null ? TimeManager.Instance.GetCurrentTimeSinceDayStart() : 0f;

            foreach (var plan in directorVisitorsToday)
            {
                if (plan == null || plan.archetype == null) continue;

                float delay = plan.spawnTime - currentTime;
                if (delay > 0) yield return new WaitForSeconds(delay);
                currentTime = plan.spawnTime;

                if (IsNightTime()) continue;

                UpdateClientsCount();
                if (currentClientsInOffice >= officeCapacity)
                {
                    TryAddToQueue(plan.archetype, $"Director_{plan.forcedGoal}", 5);
                }
                else
                {
                    var client = SpawnClientInternalWithGoal(plan.archetype, plan.forcedGoal, $"Director_{plan.forcedGoal}");
                }
            }

            directorSpawnCoroutine = null;
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
                // Debug.Log("[WaveManager] Ночь - спавн не запускается");
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

            Debug.Log($"[WaveManager] StartSpawning для {currentPeriod}: {clientsForPeriod} клиентов");

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
            	// Debug.Log("[WaveManager] ForceCheckMorningEvents: Принудительная проверка утренних событий.");
            	int day = TimeManager.Instance.GetCurrentDay();
            	CheckMorningEvents(day);

            	   if (enableAutoSpawn)
            	   {
            	       StartSpawningForCurrentPeriod();
            	       StartSpawningDirectorVisitors();
            	   }
            	else
            	{
                	// Debug.Log("[WaveManager] Авто-спавн отключен. Используйте F1 меню для ручного спавна.");
            	}
        	}
    	}

        private void OnPeriodChanged(PeriodSettings settings)
        {
            if (spawnCoroutine != null) StopCoroutine(spawnCoroutine);

            if (settings.PeriodType.IsNight())
            {
                // Debug.Log("[WaveManager] Ночь - спавн не запускается");
                return;
            }

            int day = TimeManager.Instance.GetCurrentDay();

            // ПРОВЕРЯЕМ УТРЕННИЕ СОБЫТИЯ
            CheckMorningEvents(day);

            // Используем НОВУЮ СИСТЕМУ с дневным планом
            if (enableAutoSpawn)
            {
                StartSpawningForCurrentPeriod();
                StartSpawningDirectorVisitors();
            }
            else
            {
                // Debug.Log("[WaveManager] Авто-спавн отключен. Используйте F1 меню для ручного спавна.");
            }
        }

        /// <summary>
        /// Новая корутина спавна на основе дневного плана и SpawnSchedule
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

            // Вычисляем границы периода (секунды от начала дня)
            float periodStart = 0f;
            var day = TimeManager.Instance.mainCalendarDay;
            for (int i = 0; i < day.periodSettings.Count; i++)
            {
                if (day.periodSettings[i].PeriodType == period) break;
                periodStart += day.periodSettings[i].durationInSeconds;
            }
            float periodEnd = periodStart + settings.durationInSeconds;

            // Получаем отфильтрованный список времён из SpawnSchedule
            var timesForPeriod = SpawnSchedule.Where(t => t >= periodStart && t < periodEnd).ToList();

            Debug.Log($"[WaveManager] Запущен точный спавн для периода {period}, событий: {timesForPeriod.Count}");

            if (timesForPeriod.Count == 0)
            {
                Debug.Log($"[WaveManager] Нет событий спавна для периода {period}");
                yield break;
            }

            bool guestSpawned = false;
            int archetypeIndex = 0;
            float currentTime = TimeManager.Instance.GetCurrentTimeSinceDayStart();

            foreach (float t in timesForPeriod)
            {
                // Вычисляем задержку от текущего времени
                float delay = t - currentTime;
                if (delay > 0) yield return new WaitForSeconds(delay);
                currentTime = t; // обновляем текущее время после ожидания

                // Внедряем дневного гостя в середину волны
                if (!guestSpawned && dayGuest != null && archetypeIndex >= timesForPeriod.Count / 2)
                {
                    SpawnSpecialClient(dayGuest);
                    guestSpawned = true;
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
                }
                archetypeIndex++;
            }

            // Debug.Log($"[WaveManager] SpawnRoutineFromPlan завершён для {period}: {timesForPeriod.Count} клиентов");
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

            // Debug.Log($"[WaveManager] --- НАЧАЛО ПРОВЕРКИ УТРЕННИХ СОБЫТИЙ (День {day}) ---");
            // Debug.Log($"[WaveManager] Всего записей в базе: {specialVisitorsDB.visitors.Count}");

            foreach (var specialVisitor in specialVisitorsDB.visitors)
            {
                string prefix = $"[WaveManager] Гость '{specialVisitor.name}': ";

                if (specialVisitor.dayToSpawn != day)
                {
                    // Debug.Log(prefix + $"ПРОПУСК. День {specialVisitor.dayToSpawn} != {day}");
                    continue;
                }

                if (!specialVisitor.spawnAtStartOfDay)
                {
                    // Debug.Log(prefix + $"ПРОПУСК. Галочка 'Spawn At Start Of Day' выключена.");
                    continue;
                }

                if (!AreSpawnConditionsMet(specialVisitor))
                {
                    string reqFlag = string.IsNullOrEmpty(specialVisitor.requiredFlagKey) ? "Нет" : $"{specialVisitor.requiredFlagKey} == {specialVisitor.requiredFlagValue}";
                    // Debug.Log(prefix + $"ПРОПУСК. Условия флага не выполнены. Требуется: {reqFlag}");
                    continue;
                }

                // Если дошли сюда — успех
                // Debug.Log(prefix + "<color=green>УСПЕХ! Начинаю спавн.</color>");
                SpawnSpecialClient(specialVisitor);
            }
            // Debug.Log($"[WaveManager] --- КОНЕЦ ПРОВЕРКИ ---");
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
                    // Debug.Log($"[WaveManager] OFFICE FULL! Client {archetype.displayName} added to waiting queue. Queue: {pendingSpawnQueue.Count}/{maxQueueSize}");
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
                    // Debug.Log($"[WaveManager] НОЧЬ! Очистка очереди ожидания: {pendingSpawnQueue.Count} клиентов удалено.");
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
                        // Debug.Log($"[WaveManager] Клиент {client.archetype.displayName} не дождался и ушёл. WaitTime: {Time.time - client.enqueueTime:F1}s");
                    }
                    continue;
                }

                // Спавним клиента с наивысшим приоритетом
                var nextClient = pendingSpawnQueue.OrderByDescending(x => x.priority).First();
                pendingSpawnQueue.Remove(nextClient);

                // Debug.Log($"[WaveManager] Queue processed: spawning {nextClient.archetype.displayName} (queue: {pendingSpawnQueue.Count})");
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
            // Debug.Log($"[WaveManager] SpawnClientInternal: {archetype.displayName} (groupID: {archetype.groupID}) from {requestedBy}");

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
                    // Debug.Log($"[WaveManager] Client configured: {archetype.displayName} | group: {archetype.groupID} | body: {(archetype.bodySprite != null ? archetype.bodySprite.name : "NULL")} | hair: {(archetype.hairSprites?.Count ?? 0)} options | outfit: {(archetype.outfitSprites?.Count ?? 0)} options");
                }

                // Debug.Log($"[WaveManager] Client spawned successfully: {client.name} = {archetype.displayName} ({archetype.groupID})");
            }
            else
            {
                Debug.LogError("[WaveManager] ClientPathfinding component not found!");
                Destroy(go);
            }
        }

        /// <summary>
        /// Спавн клиента с принудительной целью (для директорского потока).
        /// </summary>
        private ClientPathfinding SpawnClientInternalWithGoal(ClientArchetype archetype, ClientGoal goal, string requestedBy)
        {
            if (clientPrefab == null || spawnPoint == null || archetype == null) return null;

            if (IsNightTime()) return null;

            UpdateClientsCount();
            if (currentClientsInOffice >= officeCapacity) return null;

            Vector3 spawnPos = spawnPoint.position;
            GameObject go = Instantiate(clientPrefab, spawnPos, Quaternion.identity);
            ClientPathfinding client = go.GetComponent<ClientPathfinding>();

            if (client == null)
            {
                Debug.LogError("[WaveManager] ClientPathfinding component not found!");
                Destroy(go);
                return null;
            }

            // Устанавливаем принудительную цель ДО инициализации
            client.mainGoal = goal;

            client.SetupFromArchetype(archetype);
            client.SetupGrumblingFromArchetype(archetype);

            var visuals = client.GetComponent<CharacterVisuals>();
            if (visuals != null)
            {
                visuals.SetupVisualDiversity(archetype);
            }

            client.Initialize(waitingZoneObject, exitWaypoint);
            Debug.Log($"[WaveManager] Директорский посетитель заспавнен: {client.name} ({goal})");
            return client;
        }

        private void SpawnSpecialClient(SpecialVisitorDatabase.ScheduledVisitor visitorData)
        {
            // 1. ОБРАБОТКА ЗВОНКА (БЕЗ СПАВНА КЛИЕНТА)
            if (visitorData.isRemoteInteraction)
            {
                // Debug.Log($"[WaveManager] Входящий звонок: {visitorData.name}");

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
                // Debug.Log($"[WaveManager] Special visitor queued/spawned: {visitorData.name}");
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