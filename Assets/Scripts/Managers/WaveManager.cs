using System.Collections;
using Data.Calendar;
using UnityEngine;

namespace Managers
{
    public class WaveManager : MonoBehaviour
    {
        public static WaveManager Instance { get; private set; }

        [Header("Настройки Спавна")]
        public GameObject clientPrefab;
        public Transform spawnPoint;
        public int maxClientsOnScene = 100;
        public float initialSpawnDelay = 5f;

        [Header("Зоны")]
        public GameObject waitingZoneObject;
        public Waypoint exitWaypoint;

        private Coroutine spawnCoroutine;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            TimeManager.Instance.OnPeriodChanged += OnPeriodChanged;
        }

        private void OnPeriodChanged(PeriodSettings settings)
        {
            if (spawnCoroutine != null)
                StopCoroutine(spawnCoroutine);

            if (settings.PeriodType.IsNight())
                return;

            int day = TimeManager.Instance.GetCurrentDay();
            int clientsCount = Mathf.RoundToInt(settings.clientCount.Evaluate(day));
            
            if (clientsCount > 0)
                spawnCoroutine = StartCoroutine(SpawnRoutine(settings, clientsCount));
        }

        // по идее, можно просто в апдейт вынести и обойтись без корутины (за корутинами сложно следить, кмк)
        // тут ещё при смене периода, получается, отдохнуть можно 5 секунд
        private IEnumerator SpawnRoutine(PeriodSettings settings, int totalClients)
        {
            // задержку можно в конфиг тоже убрать, кстати, и тоже функцией задать.
            // Можно вообще порофлить и добавить в какой-нибудь день затишье, а потом атаку зергов
            yield return new WaitForSeconds(initialSpawnDelay);

            float duration = settings.durationInSeconds - initialSpawnDelay;
            if (duration <= 0)
            {
                Debug.LogError($"PeriodSettings.durationInSeconds - initialSpawnDelay < 0. Day: {TimeManager.Instance.GetCurrentDay()}");
                duration = 1f;
            }

            // равномерное распределение
            float interval = duration / totalClients;

            for (int i = 0; i < totalClients; i++)
            {
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
            }
        }
        
        private void OnDestroy()
        {
            if (TimeManager.Instance)
                TimeManager.Instance.OnPeriodChanged -= OnPeriodChanged;
        }
    }
}