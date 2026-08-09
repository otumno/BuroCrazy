using System.Collections.Generic;
using System.Linq;
using AI;
using Characters;
using Data;
using Data.Calendar;
using DialogueSystem.Data;
using Managers;
using StorySystem;
using UnityEngine;
using UnityEngine.UI;

namespace BuroDebug
{
    /// <summary>
    /// Debug меню для тестирования клиентов
    /// </summary>
    public class ClientDebugMenu : MonoBehaviour
    {
        [Header("Спавнер")]
        public WaveManager waveManager;

        [Header("UI элементы")]
        public GameObject menuPanel;
        public Text infoText;

        private ArchetypeDatabase archetypeDB;
        public SpecialVisitorDatabase specialVisitorsDB;
        private string currentSelectedGroup;

        private void Start()
        {
            Debug.Log("[ClientDebugMenu] Start called");

            if (waveManager == null)
            {
                waveManager = WaveManager.Instance;
                if (waveManager == null)
                {
                    Debug.LogWarning("[ClientDebugMenu] WaveManager not found in scene!");
                }
                else
                {
                    Debug.Log("[ClientDebugMenu] Found WaveManager: " + waveManager.name);
                }
            }

            archetypeDB = Resources.Load<ArchetypeDatabase>("Databases/ArchetypeDatabase");
            if (archetypeDB == null)
            {
                Debug.LogWarning("[ClientDebugMenu] ArchetypeDatabase not found! Make sure it's in Resources/Databases/");
            }
            else
            {
                Debug.Log("[ClientDebugMenu] ArchetypeDatabase loaded successfully");
                if (archetypeDB.allArchetypes != null)
                {
                    Debug.Log($"[ClientDebugMenu] Total archetypes: {archetypeDB.allArchetypes.Count}");
                    for (int i = 0; i < archetypeDB.allArchetypes.Count; i++)
                    {
                        var arch = archetypeDB.allArchetypes[i];
                        Debug.Log($"[ClientDebugMenu] Archetype[{i}]: {(arch != null ? arch.name + " (" + arch.groupID + ")" : "NULL")}");
                    }
                }
            }

            if (menuPanel != null)
            {
                menuPanel.SetActive(false);
                Debug.Log("[ClientDebugMenu] Menu panel initialized (hidden)");
            }
        }

        private void Update()
        {
            // Toggle menu with F1
            if (Input.GetKeyDown(KeyCode.F1))
            {
                Debug.Log("[ClientDebugMenu] F1 pressed, toggling menu...");
                ToggleMenu();
            }
        }

        private void ToggleMenu()
        {
            if (menuPanel == null)
            {
                Debug.LogWarning("[ClientDebugMenu] menuPanel is null!");
                return;
            }

            bool wasActive = menuPanel.activeSelf;
            menuPanel.SetActive(!wasActive);
            Debug.Log($"[ClientDebugMenu] Menu toggled: {wasActive} -> {!wasActive}");

            if (menuPanel.activeSelf)
            {
                RefreshInfo();
            }
        }

        private void RefreshInfo()
        {
            if (infoText == null) return;

            string info = "=== BuroCrazy Debug ===\n\n";
            int activeClients = FindObjectsByType<ClientPathfinding>(FindObjectsSortMode.None).Length;
            info += $"Active clients: {activeClients}\n";
            info += $"Office capacity: {waveManager.officeCapacity}\n";

            // Информация об очереди
            if (waveManager.pendingSpawnQueue != null)
            {
                info += $"Waiting queue: {waveManager.pendingSpawnQueue.Count}/{waveManager.maxQueueSize}\n";
                info += $"Overflow count: {waveManager.overflowClientsCount}\n";
            }

            info += $"Selected group: {(string.IsNullOrEmpty(currentSelectedGroup) ? "(none)" : currentSelectedGroup)}\n";

            // Время
            if (TimeManager.Instance != null)
            {
                var period = TimeManager.Instance.GetCurrentPeriodType();
                bool isNight = period.IsNight();
                info += $"Period: {period} {(isNight ? "(NIGHT)" : "")}\n";
            }

            info += "\n";

            // Показываем группы архетипов
            if (archetypeDB != null && archetypeDB.allArchetypes != null)
            {
                var groups = new HashSet<string>();
                foreach (var a in archetypeDB.allArchetypes)
                {
                    if (a != null) groups.Add(a.groupID);
                }

                info += "Available groups:\n";
                foreach (var g in groups)
                {
                    var count = archetypeDB.GetByGroup(g).Count;
                    info += $"  - {g}: {count} archetypes\n";
                }
            }

            info += "\n[F1] Toggle menu";
            info += "\n[Click button] Spawn client";

            infoText.text = info;
        }

        /// <summary>
        /// Спавн клиента случайной группы
        /// </summary>
        public void SpawnRandom()
        {
            if (archetypeDB == null)
            {
                Debug.LogError("ArchetypeDatabase not found!");
                return;
            }

            var groups = new List<string>();
            foreach (var a in archetypeDB.allArchetypes)
            {
                if (a != null && !groups.Contains(a.groupID))
                    groups.Add(a.groupID);
            }

            if (groups.Count == 0)
            {
                Debug.LogWarning("No archetypes found!");
                return;
            }

            string randomGroup = groups[Random.Range(0, groups.Count)];
            SpawnByGroup(randomGroup);
        }

        /// <summary>
        /// Спавн клиента конкретной группы
        /// </summary>
        public void SpawnByGroup(string groupID)
        {
            if (archetypeDB == null)
            {
                Debug.LogWarning($"[ClientDebugMenu] archetypeDB is null!");
                return;
            }

            if (waveManager.IsNightTime())
            {
                Debug.LogWarning($"[ClientDebugMenu] НОЧЬ! Клиенты не спавнятся ночью.");
                return;
            }

            currentSelectedGroup = groupID;
            Debug.Log($"[ClientDebugMenu] SpawnByGroup('{groupID}'): Using local archetypeDB...");

            ClientArchetype archetype = archetypeDB.GetRandomByGroup(groupID);
            if (archetype == null)
            {
                Debug.LogWarning($"[ClientDebugMenu] Архетипы группы '{groupID}' не найдены");
                return;
            }

            Debug.Log($"[ClientDebugMenu] Spawning {archetype.displayName} (ID: {archetype.archetypeID}, groupID: {archetype.groupID})");

            // Спавним клиента с конкретным архетипом
            waveManager.SpawnClientWithArchetype(archetype);

            RefreshInfo();
        }

        /// <summary>
        /// Спавн клиента группы "Elderly" (пожилые)
        /// </summary>
        public void SpawnElderly()
        {
            SpawnByGroup("Elderly");
        }

        /// <summary>
        /// Спавн нескольких клиентов группы "Elderly" (пожилые)
        /// </summary>
        public void SpawnElderlyMultiple(int count)
        {
            if (waveManager.IsNightTime())
            {
                Debug.LogWarning($"[ClientDebugMenu] НОЧЬ! Клиенты не спавнятся ночью.");
                return;
            }

            if (archetypeDB == null)
            {
                Debug.LogWarning($"[ClientDebugMenu] archetypeDB is null!");
                return;
            }

            // Проверяем что группа существует
            var elderlyArchetypes = archetypeDB.GetByGroup("Elderly");
            if (elderlyArchetypes.Count == 0)
            {
                Debug.LogWarning($"[ClientDebugMenu] Группа 'Elderly' не найдена в базе архетипов!");
                return;
            }

            Debug.Log($"[ClientDebugMenu] SpawnElderlyMultiple({count}): Starting...");

            for (int i = 0; i < count; i++)
            {
                ClientArchetype archetype = archetypeDB.GetRandomByGroup("Elderly");
                if (archetype != null)
                {
                    Debug.Log($"[ClientDebugMenu] [{i+1}/{count}] Spawning Elderly: {archetype.displayName} (ID: {archetype.archetypeID})");
                    waveManager.SpawnClientWithArchetype(archetype);
                }
                else
                {
                    Debug.LogWarning($"[ClientDebugMenu] [{i+1}/{count}] Failed to get archetype for group 'Elderly'");
                }
            }

            Debug.Log($"[ClientDebugMenu] SpawnElderlyMultiple({count}): Completed");
            RefreshInfo();
        }

        /// <summary>
        /// Спавн клиента группы "Elderly" (пожилые) для Директора
        /// </summary>
        public void SpawnElderlyForDirector()
        {
            if (waveManager.IsNightTime()) return;

            if (archetypeDB == null) return;

            ClientArchetype archetype = archetypeDB.GetRandomByGroup("Elderly");
            if (archetype != null)
            {
                Debug.Log($"[ClientDebugMenu] Spawning Elderly for Director: {archetype.displayName}");
                
                Vector3 spawnPos = waveManager.spawnPoint != null ? waveManager.spawnPoint.position : Vector3.zero;
                GameObject clientObj = Instantiate(waveManager.clientPrefab, spawnPos, Quaternion.identity);
                ClientPathfinding client = clientObj.GetComponent<ClientPathfinding>();

                if (client != null)
                {
                    client.mainGoal = ClientGoal.DirectorApproval; // ЖЕСТКО ЗАДАЕМ ЦЕЛЬ ДО ИНИЦИАЛИЗАЦИИ
                    
                    client.SetupFromArchetype(archetype);
                    client.SetupGrumblingFromArchetype(archetype);
                    var visuals = client.GetComponent<CharacterVisuals>();
                    if (visuals != null) visuals.SetupVisualDiversity(archetype);

                    client.Initialize(waveManager.waitingZoneObject, waveManager.exitWaypoint);
                    RefreshInfo();
                }
            }
        }

        /// <summary>
        /// Спавн нескольких клиентов текущей выбранной группы
        /// </summary>
        public void SpawnMultiple(int count)
        {
            if (waveManager.IsNightTime())
            {
                Debug.LogWarning($"[ClientDebugMenu] НОЧЬ! Клиенты не спавнятся ночью.");
                return;
            }

            if (archetypeDB == null || archetypeDB.allArchetypes == null || archetypeDB.allArchetypes.Count == 0)
            {
                Debug.LogWarning($"[ClientDebugMenu] SpawnMultiple: archetypeDB is null or empty!");
                return;
            }

            if (string.IsNullOrEmpty(currentSelectedGroup))
            {
                Debug.LogWarning($"[ClientDebugMenu] SpawnMultiple: No group selected! Click a group button first.");
                return;
            }

            Debug.Log($"[ClientDebugMenu] SpawnMultiple({count}) for group '{currentSelectedGroup}': Starting...");

            for (int i = 0; i < count; i++)
            {
                ClientArchetype archetype = archetypeDB.GetRandomByGroup(currentSelectedGroup);
                if (archetype != null)
                {
                    Debug.Log($"[ClientDebugMenu] [{i+1}/{count}] Spawning {archetype.displayName} (ID: {archetype.archetypeID}, groupID: {archetype.groupID})");
                    waveManager.SpawnClientWithArchetype(archetype);
                }
                else
                {
                    Debug.LogWarning($"[ClientDebugMenu] [{i+1}/{count}] Failed to get archetype for group '{currentSelectedGroup}'");
                }
            }

            Debug.Log($"[ClientDebugMenu] SpawnMultiple({count}) for group '{currentSelectedGroup}': Completed");
            RefreshInfo();
        }

        /// <summary>
        /// Спавн первого сюжетного гостя из базы SpecialVisitorDatabase
        /// </summary>
        public void SpawnFirstSpecialVisitor()
        {
            // Пытаемся загрузить базу автоматически, если она не назначена
            if (specialVisitorsDB == null)
            {
                specialVisitorsDB = Resources.Load<SpecialVisitorDatabase>("DialogueClientsDatabase/SpecialVisitorDatabase");
            }

            if (specialVisitorsDB == null || specialVisitorsDB.visitors.Count == 0)
            {
                Debug.LogWarning("[ClientDebugMenu] База SpecialVisitors пуста или не найдена в Resources!");
                return;
            }

            var visitorData = specialVisitorsDB.visitors[0];
            Debug.Log($"[ClientDebugMenu] Спавн сюжетного гостя: {visitorData.name}");

            ClientArchetype archetype = visitorData.forcedArchetype != null ? visitorData.forcedArchetype : (archetypeDB != null ? archetypeDB.GetRandomArchetype() : null);
            Vector3 spawnPos = waveManager.spawnPoint != null ? waveManager.spawnPoint.position : Vector3.zero;

            GameObject clientObj = Instantiate(waveManager.clientPrefab, spawnPos, Quaternion.identity);
            ClientPathfinding client = clientObj.GetComponent<ClientPathfinding>();

            if (client != null)
            {
                client.gameObject.name = visitorData.name;
                client.mainGoal = visitorData.forcedGoal;
                client.specificDialogue = visitorData.dialogue;

                if (archetype != null)
                {
                    client.SetupFromArchetype(archetype);
                    client.SetupGrumblingFromArchetype(archetype);
                    var visuals = client.GetComponent<CharacterVisuals>();
                    if (visuals != null) visuals.SetupVisualDiversity(archetype);
                }

                client.Initialize(waveManager.waitingZoneObject, waveManager.exitWaypoint);
                RefreshInfo();
            }
        }

        /// <summary>
        /// Спавн клиента текущего этапа любой активной арки прямо сейчас.
        /// Используется ArcManager.SpawnActiveStageVisitorNow.
        /// </summary>
        public void SpawnArcStageVisitor()
        {
            if (ArcManager.Instance == null)
            {
                Debug.LogWarning("[ClientDebugMenu] ArcManager.Instance недоступен — нечего спавнить.");
                return;
            }

            if (waveManager != null && waveManager.IsNightTime())
            {
                Debug.LogWarning("[ClientDebugMenu] Ночь — клиенты не спавнятся ночью.");
                return;
            }

            bool ok = ArcManager.Instance.SpawnActiveStageVisitorNow();
            if (ok)
            {
                Debug.Log("[ClientDebugMenu] Арковый клиент заспавнен немедленно.");
            }
            else
            {
                Debug.LogWarning("[ClientDebugMenu] Нет активной арки со свободным этапом. Проверьте ArcManager → 'Selected arcs' в инспекторе.");
            }

            RefreshInfo();
        }

        /// <summary>
        /// Спавн клиента конкретной арки по её ID.
        /// </summary>
        public void SpawnArcStageVisitorByID(string arcID)
        {
            if (ArcManager.Instance == null)
            {
                Debug.LogWarning("[ClientDebugMenu] ArcManager.Instance недоступен.");
                return;
            }

            if (waveManager != null && waveManager.IsNightTime())
            {
                Debug.LogWarning("[ClientDebugMenu] Ночь — клиенты не спавнятся ночью.");
                return;
            }

            bool ok = ArcManager.Instance.SpawnActiveStageVisitorNow(arcID);
            Debug.Log(ok
                ? $"[ClientDebugMenu] Арковый клиент арки '{arcID}' заспавнен."
                : $"[ClientDebugMenu] Арка '{arcID}' не найдена или завершена.");
            RefreshInfo();
        }

        /// <summary>
        /// Показать всех активных клиентов
        /// </summary>
        public void ShowActiveClients()
        {
            var clients = FindObjectsByType<ClientPathfinding>(FindObjectsSortMode.None);
            if (clients.Length == 0)
            {
                Debug.Log("No active clients");
                return;
            }

            string info = $"=== Active Clients ({clients.Length}) ===\n";
            foreach (var client in clients)
            {
                info += $"\n{client.name}";
                info += $"\n  Goal: {client.mainGoal}";
                info += $"\n  Document: {client.docHolder?.GetCurrentDocumentType().ToString() ?? "None"}";
                info += $"\n  Patience: {client.totalPatienceTime:F1}s";
                info += $"\n  Heat: {client.PatienceHeat:P0}";
                info += $"\n  State: {client.stateMachine?.GetCurrentState().ToString() ?? "Unknown"}";
            }

            Debug.Log(info);
        }

        /// <summary>
        /// Спавн посетителя для личного приема у директора
        /// </summary>
        public void SpawnVisitorForAudience()
        {
            if (waveManager.IsNightTime() || archetypeDB == null) return;
            
            ClientArchetype archetype = archetypeDB.GetRandomArchetype();
            GameObject clientObj = Instantiate(waveManager.clientPrefab, waveManager.spawnPoint.position, Quaternion.identity);
            ClientPathfinding client = clientObj.GetComponent<ClientPathfinding>();
            if (client != null) {
                client.mainGoal = ClientGoal.DirectorAudience;
                client.specificDialogue = Resources.Load<DialogueSystem.Data.DialogueGraph>("DialoguesBase/Test_Client_Dialogue");
                client.SetupFromArchetype(archetype);
                client.Initialize(waveManager.waitingZoneObject, waveManager.exitWaypoint);
            }
        }

        public void SpawnTestClown()
        {
            if (TemporaryEffectManager.Instance == null)
            {
                Debug.LogError("[Debug] TemporaryEffectManager не найден!");
                return;
            }
            TemporaryEffectManager.Instance.SpawnClown(TemporaryClownAI.ClownMode.Clients);
            Debug.Log("[Debug] Клоун заспавнен вручную");
        }

        public void SpawnTestCleaners()
        {
            if (TemporaryEffectManager.Instance == null)
            {
                Debug.LogError("[Debug] TemporaryEffectManager не найден!");
                return;
            }
            TemporaryEffectManager.Instance.SpawnCleaningCrew();
            Debug.Log("[Debug] Бригада уборщиков заспавнена вручную");
        }

        public void UnlockAllRegions()
        {
            if (ProgressionManager.Instance == null)
            {
                Debug.LogError("[Debug] ProgressionManager не найден!");
                return;
            }

            var regions = ProgressionManager.Instance.allRegionsDatabase;
            if (regions == null || regions.Count == 0)
            {
                Debug.LogError("[Debug] Список регионов пуст!");
                return;
            }

            int count = 0;
            foreach (var region in regions)
            {
                if (!ProgressionManager.Instance.IsRegionUnlocked(region.regionID))
                {
                    ProgressionManager.Instance.FinalizeRegionUnlock(region);
                    count++;
                }
            }

            Debug.Log($"<color=green>[Debug] Открыто регионов: {count}</color>");
        }

#if DEBUG_ENABLED || UNITY_EDITOR
        // ==================== STORY DEBUG (ARCS / ENDINGS) ====================

        [Header("Story Debug")]
        public EndingDatabase endingDatabase;

        private ArcManager arcManager;
        private List<ArcDefinition> allArcs = new List<ArcDefinition>();
        private List<DialogueGraph> allDialogues = new List<DialogueGraph>();
        private ArcInstance debugArcInstance = null;
        private bool isDebugArcRunning = false;
        private bool autoAdvance = true;
        private int pendingStageIndex = -1;

        // События для UI
        public System.Action OnDebugArcStarted;
        public System.Action OnDebugArcStopped;
        public System.Action<int> OnDebugStageWaiting;

        /// <summary>
        /// Получить список всех доступных арок (из Resources + ArcManager, приоритет у ArcManager).
        /// </summary>
        public List<ArcDefinition> GetAllArcs()
        {
            if (arcManager == null)
            {
                arcManager = ArcManager.Instance;
                if (arcManager == null)
                {
                    arcManager = FindFirstObjectByType<ArcManager>();
                }
            }

            if (arcManager != null && arcManager.GetAllArcDefinitions() != null && arcManager.GetAllArcDefinitions().Count > 0)
            {
                allArcs = arcManager.GetAllArcDefinitions();
            }
            else if (allArcs.Count == 0)
            {
                allArcs.AddRange(Resources.LoadAll<ArcDefinition>("Arcs"));
            }

            return allArcs;
        }

        /// <summary>
        /// Получить список диалогов (из Resources).
        /// </summary>
        public List<DialogueGraph> GetAllDialogues()
        {
            if (allDialogues.Count == 0)
            {
                allDialogues.AddRange(Resources.LoadAll<DialogueGraph>("Dialogues"));
                allDialogues.AddRange(Resources.LoadAll<DialogueGraph>("DialoguesBase"));
            }
            return allDialogues;
        }

        /// <summary>
        /// Запустить диалог напрямую (без клиента).
        /// </summary>
        public void PlayDialogue(DialogueGraph graph)
        {
            if (graph == null)
            {
                Debug.LogWarning("[Debug] PlayDialogue: graph == null");
                return;
            }
            if (DialogueUIManager.Instance == null)
            {
                Debug.LogWarning("[Debug] DialogueUIManager.Instance недоступен.");
                return;
            }
            Debug.Log($"[Debug] Запуск диалога: {graph.name}");
            DialogueUIManager.Instance.StartDialogue(graph, null);
        }

        /// <summary>
        /// Запуск дебаг-арки. Если autoAdvance == true, диалоги идут подряд;
        /// иначе после каждого этапа вызывается OnDebugStageWaiting.
        /// </summary>
        public void StartDebugArc(ArcDefinition arc, bool autoAdvance = true)
        {
            if (arc == null) return;
            if (isDebugArcRunning)
            {
                Debug.LogWarning("[Debug] Арка уже запущена. Сначала остановите её.");
                return;
            }

            this.autoAdvance = autoAdvance;
            int currentDay = TimeManager.Instance != null ? TimeManager.Instance.GetCurrentDay() : 1;
            debugArcInstance = new ArcInstance(arc, currentDay);
            isDebugArcRunning = true;
            pendingStageIndex = -1;
            Debug.Log($"[Debug] Запуск арки: {arc.displayName} ({arc.arcID})");

            OnDebugArcStarted?.Invoke();

            // Запускаем первый доступный этап
            int firstStage = FindNextAvailableStage(0);
            if (firstStage >= 0)
                RunStage(firstStage);
            else
                FinishDebugArc();
        }

        private void RunStage(int stageIndex)
        {
            if (debugArcInstance == null || debugArcInstance.definition == null)
            {
                FinishDebugArc();
                return;
            }

            var stages = debugArcInstance.definition.stages;
            if (stages == null || stageIndex >= stages.Count)
            {
                Debug.Log("[Debug] Все этапы пройдены. Арка завершена.");
                FinishDebugArc();
                return;
            }

            var stage = stages[stageIndex];

            // Проверяем условие этапа (requiredFlag)
            if (!string.IsNullOrEmpty(stage.requiredFlag))
            {
                int flagValue = StoryStateManager.Instance != null ? StoryStateManager.Instance.GetFlag(stage.requiredFlag) : 0;
                if (flagValue != 1)
                {
                    Debug.LogWarning($"[Debug] Этап {stageIndex} пропущен: флаг '{stage.requiredFlag}' не установлен.");
                    int nextIndex = FindNextAvailableStage(stageIndex + 1);
                    if (nextIndex >= 0)
                        RunStage(nextIndex);
                    else
                        FinishDebugArc();
                    return;
                }
            }

            // Устанавливаем onStartFlag перед запуском
            if (!string.IsNullOrEmpty(stage.onStartFlag) && StoryStateManager.Instance != null)
            {
                StoryStateManager.Instance.SetFlag(stage.onStartFlag, 1);
            }

            // Если диалога нет — пропускаем этап, но считаем его «пройденным»
            if (stage.dialogue == null)
            {
                Debug.Log($"[Debug] Этап {stageIndex} без диалога — пропускаем.");
                OnStageComplete(stageIndex);
                return;
            }

            if (DialogueUIManager.Instance == null)
            {
                Debug.LogWarning("[Debug] DialogueUIManager.Instance недоступен — пропускаем этап.");
                OnStageComplete(stageIndex);
                return;
            }

            Debug.Log($"[Debug] Запуск этапа {stageIndex}: {stage.characterName}");

            int currentStageIndex = stageIndex; // захватываем для замыкания
            DialogueUIManager.Instance.StartDialogue(stage.dialogue, null, () =>
            {
                OnStageComplete(currentStageIndex);
            });
        }

        private void OnStageComplete(int completedStageIndex)
        {
            if (!isDebugArcRunning) return;

            var stages = debugArcInstance.definition.stages;
            if (completedStageIndex < 0 || completedStageIndex >= stages.Count) return;

            var stage = stages[completedStageIndex];
            if (!string.IsNullOrEmpty(stage.onCompleteFlag) && StoryStateManager.Instance != null)
            {
                StoryStateManager.Instance.SetFlag(stage.onCompleteFlag, 1);
            }

            int nextIndex = FindNextAvailableStage(completedStageIndex + 1);
            if (nextIndex >= 0)
            {
                if (autoAdvance)
                {
                    RunStage(nextIndex);
                }
                else
                {
                    pendingStageIndex = nextIndex;
                    OnDebugStageWaiting?.Invoke(nextIndex);
                    Debug.Log($"[Debug] Этап {completedStageIndex} завершён. Ожидание продолжения.");
                }
            }
            else
            {
                Debug.Log("[Debug] Все доступные этапы пройдены. Арка завершена.");
                FinishDebugArc();
            }
        }

        private int FindNextAvailableStage(int startIndex)
        {
            if (debugArcInstance == null || debugArcInstance.definition == null) return -1;
            var stages = debugArcInstance.definition.stages;
            if (stages == null) return -1;

            for (int i = startIndex; i < stages.Count; i++)
            {
                var stage = stages[i];
                if (string.IsNullOrEmpty(stage.requiredFlag))
                    return i;
                int flagValue = StoryStateManager.Instance != null ? StoryStateManager.Instance.GetFlag(stage.requiredFlag) : 0;
                if (flagValue == 1)
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// Продолжить выполнение арки в ручном режиме.
        /// </summary>
        public void ContinueDebugArc()
        {
            if (!isDebugArcRunning) return;
            if (pendingStageIndex < 0)
            {
                Debug.LogWarning("[Debug] Нет ожидающих этапов для продолжения.");
                return;
            }
            int index = pendingStageIndex;
            pendingStageIndex = -1;
            RunStage(index);
        }

        /// <summary>
        /// Остановить выполнение арки.
        /// </summary>
        public void StopDebugArc()
        {
            if (!isDebugArcRunning) return;
            FinishDebugArc();
        }

        private void FinishDebugArc()
        {
            isDebugArcRunning = false;
            debugArcInstance = null;
            pendingStageIndex = -1;
            Debug.Log("[Debug] Дебаг-арка завершена.");
            OnDebugArcStopped?.Invoke();
        }

        // ==================== ENDINGS ====================

        /// <summary>
        /// Получить список концовок из базы.
        /// </summary>
        public List<EndingEntry> GetAllEndings()
        {
            if (endingDatabase == null)
            {
                endingDatabase = Resources.Load<EndingDatabase>("EndingDatabase");
            }
            if (endingDatabase != null) return endingDatabase.endings;
            return new List<EndingEntry>();
        }

        /// <summary>
        /// Принудительно запустить концовку по ID.
        /// Сбрасывает черты и (если ID == имени черты) выставляет её в максимум,
        /// чтобы доминирующая черта совпала с endingID.
        /// </summary>
        public void TriggerEnding(string endingID)
        {
            if (string.IsNullOrEmpty(endingID)) return;

            var entry = GetAllEndings().FirstOrDefault(e => e != null && e.endingID == endingID);
            if (entry == null)
            {
                Debug.LogWarning($"[Debug] Концовка '{endingID}' не найдена в EndingDatabase.");
                return;
            }

            if (TraitManager.Instance != null)
            {
                TraitManager.Instance.SetAllTraits(0);
                // Если endingID совпадает с одной из черт — поднимаем её.
                if (endingID == TraitManager.TRAIT_LAW ||
                    endingID == TraitManager.TRAIT_EMPATHY ||
                    endingID == TraitManager.TRAIT_MASK ||
                    endingID == TraitManager.TRAIT_AMBITION)
                {
                    TraitManager.Instance.SetTrait(endingID, 100);
                }
            }

            if (EndingManager.Instance == null)
            {
                Debug.LogWarning("[Debug] EndingManager.Instance недоступен.");
                return;
            }

            Debug.Log($"[Debug] Принудительный запуск концовки: {endingID}");
            EndingManager.Instance.TriggerEnding(endingID);
        }

        /// <summary>
        /// Запуск концовки по индексу в списке.
        /// </summary>
        public void TriggerEndingByIndex(int index)
        {
            var endings = GetAllEndings();
            if (index >= 0 && index < endings.Count && endings[index] != null)
                TriggerEnding(endings[index].endingID);
        }
#endif
    }
}
