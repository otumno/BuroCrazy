using System.Collections.Generic;
using AI;
using Characters;
using Data;
using Data.Calendar;
using DialogueSystem.Data;
using Managers;
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
    }
}
