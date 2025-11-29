using System;
using System.Collections;
using System.Collections.Generic;
using Data.Calendar;
using UnityEngine;

namespace Managers
{
    public class ClientSpawner : MonoBehaviour
    {
        public static ClientSpawner Instance { get; private set; }

        [Header("Основные настройки спавна")]
        public GameObject clientPrefab;
        public Transform spawnPoint;
        public int maxClientsOnScene = 100;
        public float initialSpawnDelay = 5f;
        public int directorClientsPerDay = 1;

        [Header("Ссылки на объекты сцены")]
        public GameObject waitingZoneObject;
        public Waypoint exitWaypoint;
        
        [Header("ЗОНЫ ОБСЛУЖИВАНИЯ")]
        public LimitedCapacityZone registrationZone; 
        public List<LimitedCapacityZone> category1DeskZones; 
        public List<LimitedCapacityZone> category2DeskZones; 
        public List<LimitedCapacityZone> cashierZones;       
        public LimitedCapacityZone toiletZone;
        public LimitedCapacityZone directorReceptionZone;

        [Header("Звуки")]
        public AudioSource crowdAudioSource;
        public int minClientsForCrowdSound = 3;
        public int maxClientsForFullVolume = 15;

		public FormTable formTable;

        private Coroutine continuousSpawnCoroutine;
        private float globalSpawnRateMultiplier = 1f;
        private static Dictionary<int, IServiceProvider> serviceProviderAssignments = new Dictionary<int, IServiceProvider>();

        void Awake()
        {
            Instance = this;
        }

        void Start()
        {
            // Подписываемся на события DayPeriodManager
            if (DayPeriodManager.Instance != null)
            {
                DayPeriodManager.Instance.OnPeriodChanged += HandlePeriodChange;
            }
            else
            {
                Debug.LogError("[ClientSpawner] DayPeriodManager не найден! Спавн не будет работать.");
            }
        }

        void OnDestroy()
        {
            if (DayPeriodManager.Instance != null)
            {
                DayPeriodManager.Instance.OnPeriodChanged -= HandlePeriodChange;
            }
        }

        void Update()
        {
            CheckCrowdDensity();
        }

        private void HandlePeriodChange()
        {
            var currentPlan = DayPeriodManager.Instance.CurrentPeriodConfig;
            if (currentPlan == null) return;

            // 1. Обновляем смены сотрудников
            UpdateStaffShifts(currentPlan.PeriodType);

            // 2. Останавливаем старый спавн
            if (continuousSpawnCoroutine != null) StopCoroutine(continuousSpawnCoroutine);

            // 3. Считаем, сколько клиентов нужно (берем день из CalendarManager)
            int currentDay = CalendarManager.Instance != null ? CalendarManager.Instance.CurrentDay : 1;
            int clientsForThisPeriod = Mathf.RoundToInt(currentPlan.clientCount.Evaluate(currentDay));

            // 4. Запускаем спавн, если не ночь
            if (clientsForThisPeriod > 0 && !currentPlan.PeriodType.IsNight())
            {
                continuousSpawnCoroutine = StartCoroutine(HandleContinuousSpawning(currentPlan, clientsForThisPeriod));
            }

            // 5. Эвакуация на ночь
            if (currentPlan.PeriodType.IsNight())
            {
                EvacuateAllClients(true);
                // Сброс очереди
                ClientQueueManager.Instance?.ResetQueueNumber();
            }
        }

        IEnumerator HandleContinuousSpawning(PeriodSettings plan, int clientsToSpawn)
        {
            if (clientsToSpawn <= 0) yield break;
            yield return new WaitForSeconds(initialSpawnDelay);
        
            var duration = plan.durationInSeconds;
            if (duration > initialSpawnDelay)
            {
                float spawnInterval = (duration - initialSpawnDelay) / clientsToSpawn;
                spawnInterval /= globalSpawnRateMultiplier;

                for (int i = 0; i < clientsToSpawn; i++)
                {
                    SpawnClientBatch(1);
                    if (spawnInterval > 0) yield return new WaitForSeconds(spawnInterval);
                    else yield return null;
                }
            }
        }

        void SpawnClientBatch(int count, bool isDirectorClient = false)
        {
            for (int i = 0; i < count; i++)
            {
                if (FindObjectsByType<ClientPathfinding>(FindObjectsSortMode.None).Length < maxClientsOnScene)
                {
                    GameObject clientGO = Instantiate(clientPrefab, spawnPoint.position, Quaternion.identity);
                    ClientPathfinding client = clientGO.GetComponent<ClientPathfinding>();
                    if (client != null)
                    {
                        if (isDirectorClient) client.mainGoal = ClientGoal.DirectorApproval;
                        client.Initialize(Instance.waitingZoneObject, Instance.exitWaypoint);
                    }
                }
                else break;
            }
        }

        // --- Helper Methods ---
        public static LimitedCapacityZone GetZoneByDeskId(int deskId)
        {
            if (Instance == null || ScenePointsRegistry.Instance == null) return null;
            ServicePoint targetPoint = ScenePointsRegistry.Instance.GetServicePointByID(deskId);
            return targetPoint != null ? targetPoint.GetComponentInParent<LimitedCapacityZone>() : null;
        }

        public static IServiceProvider GetServiceProviderAtDesk(int deskId)
        {
            return serviceProviderAssignments.TryGetValue(deskId, out IServiceProvider provider) ? provider : null;
        }

        public static void AssignServiceProviderToDesk(IServiceProvider provider, int deskId) => serviceProviderAssignments[deskId] = provider;
        public static void UnassignServiceProviderFromDesk(int deskId) => serviceProviderAssignments.Remove(deskId);

        // Методы для совместимости (чтобы не сломать остальной код)
        public static LimitedCapacityZone GetRegistrationZone() => Instance.registrationZone;
        public static LimitedCapacityZone GetToiletZone() => Instance.toiletZone;
        public static LimitedCapacityZone GetDesk1Zone() => Instance.category1DeskZones.Count > 0 ? Instance.category1DeskZones[0] : null;
        public static LimitedCapacityZone GetDesk2Zone() => Instance.category2DeskZones.Count > 0 ? Instance.category2DeskZones[0] : null;
        public static LimitedCapacityZone GetCashierZone() => Instance.cashierZones.Count > 0 ? Instance.cashierZones[0] : null;
        
        public static LimitedCapacityZone GetQuietestZone(List<LimitedCapacityZone> zones)
        {
            // (Старый код поиска самой свободной зоны)
            if (zones == null || zones.Count == 0) return null;
            LimitedCapacityZone bestZone = null;
            int minQueue = int.MaxValue;
            foreach(var z in zones) {
                if(z != null && z.waitingQueue.Count < minQueue) { minQueue = z.waitingQueue.Count; bestZone = z; }
            }
            return bestZone;
        }

        private void UpdateStaffShifts(CalendarDayPeriodType periodType)
        {
            if (HiringManager.Instance == null) return;
            var allStaffOnScene = HiringManager.Instance.AllStaff;
            foreach (var staffMember in allStaffOnScene)
            {
                if (staffMember == null) continue;
                var shouldWork = staffMember.WorkShiftMask.HasFlag(periodType);
                if (shouldWork && !staffMember.IsOnDuty()) staffMember.StartShift();
                else if (!shouldWork && staffMember.IsOnDuty()) staffMember.EndShift();
            }
        }

        private void EvacuateAllClients(bool force)
        {
            var allClients = FindObjectsByType<ClientPathfinding>(FindObjectsSortMode.None);
            foreach (var client in allClients)
            {
                if (client != null) client.ForceLeave(force ? ClientPathfinding.LeaveReason.Angry : ClientPathfinding.LeaveReason.CalmedDown);
            }
        }
        
        void CheckCrowdDensity() 
        { 
            if (crowdAudioSource == null) return;
            int clientCount = FindObjectsByType<ClientPathfinding>(FindObjectsSortMode.None).Length; 
            if (clientCount >= minClientsForCrowdSound) { 
                if (!crowdAudioSource.isPlaying) crowdAudioSource.Play();
                float volume = Mathf.InverseLerp(minClientsForCrowdSound, maxClientsForFullVolume, clientCount); 
                crowdAudioSource.volume = Mathf.Clamp01(volume); 
            } else { 
                if (crowdAudioSource.isPlaying) crowdAudioSource.Stop();
            } 
        }
    }
}