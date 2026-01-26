using System.Collections.Generic;
using UnityEngine;
using Characters;
using Data;

namespace Managers
{
    public class ClientSpawnerWithArchetypes : MonoBehaviour
    {
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

        private void Start()
        {
            if (archetypeDatabase == null)
            {
                archetypeDatabase = Resources.Load<ArchetypeDatabase>("Databases/ArchetypeDatabase");
            }

            if (exitPoint == null)
            {
                exitPoint = transform.Find("ExitPoint");
            }
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

            var visuals = client.GetComponent<CharacterVisuals>();
            if (visuals != null)
            {
                visuals.SetupFromArchetype(archetype);
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

        public void RemoveClient(ClientPathfinding client)
        {
            if (activeClients.Contains(client))
            {
                activeClients.Remove(client);
            }
        }

        public int GetActiveClientCount()
        {
            return activeClients.Count;
        }

        public List<ClientPathfinding> GetActiveClients()
        {
            return new List<ClientPathfinding>(activeClients);
        }
    }
}
