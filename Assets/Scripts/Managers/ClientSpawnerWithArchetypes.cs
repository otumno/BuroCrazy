using System.Collections.Generic;
using UnityEngine;
using Characters;
using Data;

namespace Managers
{
    public class ClientSpawnerWithArchetypes : MonoBehaviour
    {
        public static ClientSpawnerWithArchetypes Instance { get; private set; }

        [Header("База данных архетипов")]
        public ArchetypeDatabase archetypeDatabase;

        [Header("Префабы клиентов")]
        public GameObject clientPrefab;
        public Transform spawnPoint;
        public Transform exitPoint;

        [Header("Настройки спавна")]
        public int maxClients = 10;
        public float spawnInterval = 5f;
        public int clientsPerWave = 3;

        [Header("Управление очередью")]
        private List<ClientPathfinding> activeClients = new List<ClientPathfinding>();
        private float lastSpawnTime = 0f;

        private void Awake()
        {
            // Синглтон: спавнер один на сцене, к нему обращается CrowdSoundManager за числом клиентов.
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Start()
        {
            if (archetypeDatabase == null)
            {
                archetypeDatabase = Resources.Load<ArchetypeDatabase>("Databases/ArchetypeDatabase");
            }

            if (spawnPoint == null)
            {
                spawnPoint = transform.Find("SpawnPoint");
                if (spawnPoint == null)
                {
                    Debug.LogWarning($"[ClientSpawner] SpawnPoint not found on {gameObject.name}! Using transform position.");
                }
            }

            if (exitPoint == null)
            {
                exitPoint = transform.Find("ExitPoint");
            }

            Debug.Log($"[ClientSpawner] Start: spawnPoint={(spawnPoint != null ? spawnPoint.name : "NULL")}, maxClients={maxClients}");
        }

        private void Update()
        {
            if (Time.time - lastSpawnTime >= spawnInterval)
            {
                if (activeClients.Count < maxClients)
                {
                    SpawnWave();
                }
                lastSpawnTime = Time.time;
            }
        }

        private void SpawnWave()
        {
            for (int i = 0; i < clientsPerWave; i++)
            {
                if (activeClients.Count >= maxClients) break;

                SpawnClient();
            }
        }

        private void SpawnClient()
        {
            if (archetypeDatabase == null || clientPrefab == null) return;

            ClientArchetype archetype = archetypeDatabase.GetRandomArchetype();

            if (archetype == null)
            {
                Debug.LogWarning("[ClientSpawner] Архетип не найден, используем базовый клиент");
                SpawnBasicClient();
                return;
            }

            GameObject clientObj = Instantiate(clientPrefab, spawnPoint.position, Quaternion.identity);
            ClientPathfinding client = clientObj.GetComponent<ClientPathfinding>();

            if (client != null)
            {
                SetupClientWithArchetype(client, archetype);
                activeClients.Add(client);
            }
        }

        private void SetupClientWithArchetype(ClientPathfinding client, ClientArchetype archetype)
        {
            client.name = $"{archetype.displayName} #{Random.Range(100, 999)}";

            // Инициализируем клиента (устанавливает gender, факторы, терпение)
            Waypoint exitWP = exitPoint != null ? exitPoint.GetComponent<Waypoint>() : null;
            client.Initialize(spawnPoint?.gameObject, exitWP);

            // Параметры из архетипа
            client.SetupFromArchetype(archetype);

            var visuals = client.GetComponent<CharacterVisuals>();
            Debug.Log($"[ClientSpawner] SetupClientWithArchetype: client={client.name}, visuals={(visuals != null ? "FOUND" : "NULL")}");

            if (visuals != null)
            {
                visuals.SetupVisualDiversity(archetype);
            }
            else
            {
                Debug.LogWarning($"[ClientSpawner] CharacterVisuals component not found on {client.name}!");
            }

            client.SetupGrumblingFromArchetype(archetype);

            if (archetype.allowedGoals != null && archetype.allowedGoals.Count > 0)
            {
                client.mainGoal = archetype.allowedGoals[Random.Range(0, archetype.allowedGoals.Count)];
            }

            var stateMachine = client.stateMachine;
            if (stateMachine != null)
            {
                var thoughts = client.GetComponent<ThoughtBubbleController>();
                if (thoughts != null && archetype.thoughtPool != null && archetype.thoughtPool.Count > 0)
                {
                    string thought = archetype.GetRandomThought();
                    thoughts.ShowPriorityMessage(thought, 3f, Color.white);
                }
            }
        }

        /// <summary>
        /// Создать клиента указанной группы
        /// </summary>
        public void SpawnByGroup(string groupID)
        {
            // Проверка лимита клиентов
            if (activeClients.Count >= maxClients)
            {
                Debug.LogWarning($"[ClientSpawner] Достигнут лимит клиентов: {activeClients.Count}/{maxClients}");
                return;
            }

            if (archetypeDatabase == null || clientPrefab == null)
            {
                Debug.LogWarning($"[ClientSpawner] archetypeDatabase={(archetypeDatabase != null)}, clientPrefab={(clientPrefab != null)}");
                return;
            }

            ClientArchetype archetype = archetypeDatabase.GetRandomByGroup(groupID);
            if (archetype == null)
            {
                Debug.LogWarning($"[ClientSpawner] Архетипы группы '{groupID}' не найдены");
                return;
            }

            Vector3 spawnPos = spawnPoint != null ? spawnPoint.position : Vector3.zero;
            Debug.Log($"[ClientSpawner] Spawning {archetype.displayName} at position: {spawnPos}");

            GameObject clientObj = Instantiate(clientPrefab, spawnPos, Quaternion.identity);
            ClientPathfinding client = clientObj.GetComponent<ClientPathfinding>();

            if (client != null)
            {
                SetupClientWithArchetype(client, archetype);
                activeClients.Add(client);
                Debug.Log($"[ClientSpawner] Client spawned successfully: {client.name}");
            }
            else
            {
                Debug.LogError($"[ClientSpawner] ClientPathfinding component not found on prefab!");
                Destroy(clientObj);
            }
        }

        /// <summary>
        /// Создать волну клиентов указанной группы
        /// </summary>
        public void SpawnWaveByGroup(string groupID, int count = 3)
        {
            for (int i = 0; i < count; i++)
            {
                if (activeClients.Count >= maxClients) break;
                SpawnByGroup(groupID);
            }
        }

        private void SpawnBasicClient()
        {
            if (clientPrefab == null) return;

            GameObject clientObj = Instantiate(clientPrefab, spawnPoint.position, Quaternion.identity);
            ClientPathfinding client = clientObj.GetComponent<ClientPathfinding>();
            if (client != null)
            {
                activeClients.Add(client);
            }
            else
            {
                Debug.LogWarning("[ClientSpawner] ClientPathfinding компонент не найден в префабе");
                Destroy(clientObj);
            }
        }

        public bool RemoveClient(ClientPathfinding client) => activeClients.Remove(client);

        public int GetActiveClientCount()
        {
            RemoveDestroyedClients();
            return activeClients.Count;
        }

        public List<ClientPathfinding> GetActiveClients()
        {
            RemoveDestroyedClients();
            return new List<ClientPathfinding>(activeClients);
        }

        private void RemoveDestroyedClients() => activeClients.RemoveAll(client => client == null);
    }
}
