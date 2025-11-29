using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Data.Calendar;
using UnityEngine;

namespace Managers
{
    public class ClientSpawner : MonoBehaviour
    {
        #region Fields and Properties
        
        [Header("Настройки Календаря")]
        public CalendarDay mainCalendarDay;

        [Header("Основные настройки спавна")]
        public GameObject clientPrefab;
        public Transform spawnPoint;
        public int maxClientsOnScene = 100;
        public float initialSpawnDelay = 5f;
        public int directorClientsPerDay = 1;

        [Header("Ссылки на объекты сцены")]
        public GameObject waitingZoneObject;
        public Waypoint exitWaypoint;
        public FormTable formTable;

        [Header("ЗОНЫ ОБСЛУЖИВАНИЯ")]
        public LimitedCapacityZone registrationZone; 
        public List<LimitedCapacityZone> category1DeskZones; 
        public List<LimitedCapacityZone> category2DeskZones; 
        public List<LimitedCapacityZone> cashierZones;       
        public LimitedCapacityZone toiletZone;
        public LimitedCapacityZone directorReceptionZone;

        [Header("Настройки звука толпы")]
        public AudioSource crowdAudioSource;
        public int minClientsForCrowdSound = 3;
        public int maxClientsForFullVolume = 15;

        public static CalendarDayPeriodType CurrentPeriodType { get; private set; }
        private int currentPeriodIndex = 0;
    
        private PeriodSettings previousPeriodPlan;
        private float periodTimer;
        private Coroutine continuousSpawnCoroutine;
        private int dayCounter = 1;
        
        public static ClientSpawner Instance { get; private set; }
    
        private float globalSpawnRateMultiplier = 1f;
        private List<int> directorClientSpawnPeriods = new List<int>();

        private static Dictionary<int, IServiceProvider> serviceProviderAssignments = new Dictionary<int, IServiceProvider>();
        
        // Событие смены периода (на него подписывается LightManager)
        public event Action OnPeriodChanged;
        
        #endregion

        #region Unity Lifecycle Methods
    
        void Awake()
        {
            Instance = this;
        }

        void Start()
        {
            if (mainCalendarDay == null || mainCalendarDay.periodSettings.Count == 0)
            {
                Debug.LogError("Календарь не назначен!", this);
                enabled = false;
                return;
            }

            // Инициализация "Нулевого" дня (Ночь перед стартом)
            dayCounter = 0;
            var nightIndex = mainCalendarDay.periodSettings.FindIndex(p => p.PeriodType == CalendarDayPeriodType.Night);
            if (nightIndex == -1) nightIndex = mainCalendarDay.periodSettings.Count - 1;

            currentPeriodIndex = nightIndex;
            PeriodSettings nightPeriodPlan = mainCalendarDay.periodSettings[nightIndex];

            // Ставим таймер на 10 сек до конца ночи
            periodTimer = Mathf.Max(0, nightPeriodPlan.durationInSeconds - 10f);
            previousPeriodPlan = nightPeriodPlan;

            // Запускаем период
            StartNewPeriod(false);
        }

        void Update()
        {
            if (Time.timeScale == 0f) return;
        
            periodTimer += Time.deltaTime;
    
            var currentPlan = GetCurrentPeriodPlan();
            if (currentPlan != null && periodTimer >= currentPlan.durationInSeconds) 
            { 
                GoToNextPeriod();
            }
    
            CheckCrowdDensity();
        }
        #endregion

        #region Period and Spawning Logic

        public void GoToNextPeriod()
        {
            var todayPeriods = mainCalendarDay?.periodSettings;

            if (todayPeriods != null && todayPeriods.Count > 0)
            {
                if (currentPeriodIndex >= 0 && currentPeriodIndex < todayPeriods.Count)
                {
                    previousPeriodPlan = todayPeriods[currentPeriodIndex];
                }
                currentPeriodIndex = (currentPeriodIndex + 1) % todayPeriods.Count;
                periodTimer = 0;
            }

            // Новый день
            if (currentPeriodIndex == 0)
            {
                dayCounter++;
                ClientQueueManager.Instance.ResetQueueNumber();
                PlanDirectorClientSpawns();
            }

            if (todayPeriods != null)
            {
                UpdateStaffShifts(todayPeriods[currentPeriodIndex].PeriodType);
            }

            StartNewPeriod();
        }

        void StartNewPeriod(bool resetTimer = true)
        {
            if (resetTimer) periodTimer = 0;
        
            PeriodSettings currentPeriodPlan = GetCurrentPeriodPlan();
            if (currentPeriodPlan == null) return;

            CurrentPeriodType = currentPeriodPlan.PeriodType;

            if (continuousSpawnCoroutine != null) StopCoroutine(continuousSpawnCoroutine);
        
            int clientsForThisPeriod = Mathf.RoundToInt(currentPeriodPlan.clientCount.Evaluate(dayCounter));

            if (clientsForThisPeriod > 0 && !CurrentPeriodType.IsNight())
            {
                continuousSpawnCoroutine = StartCoroutine(HandleContinuousSpawning(currentPeriodPlan, clientsForThisPeriod));
            }
        
            // Эвакуация на ночь
            if (CurrentPeriodType == CalendarDayPeriodType.Night)
                EvacuateAllClients(true);
        
            // Оповещаем другие системы (свет, музыка и т.д.)
            OnPeriodChanged?.Invoke();
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
                    if (spawnInterval > 0)
                        yield return new WaitForSeconds(spawnInterval);
                    else
                        yield return null;
                }
            }
        }
        #endregion
	
        #region Helper Methods

        // Логика звука толпы осталась здесь, так как она завязана на количество клиентов, которых спавнер контролирует
        void CheckCrowdDensity() 
        { 
            if (crowdAudioSource == null || waitingZoneObject == null) return;
            int clientCount = FindObjectsByType<ClientPathfinding>(FindObjectsSortMode.None).Length; 
            if (clientCount >= minClientsForCrowdSound) 
            { 
                if (!crowdAudioSource.isPlaying) crowdAudioSource.Play();
                float volume = Mathf.InverseLerp(minClientsForCrowdSound, maxClientsForFullVolume, clientCount); 
                crowdAudioSource.volume = Mathf.Clamp01(volume); 
            } 
            else 
            { 
                if (crowdAudioSource.isPlaying) crowdAudioSource.Stop();
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

        public void ApplyOrderEffects(DirectorOrder order)
        {
            globalSpawnRateMultiplier = order.clientSpawnRateMultiplier;
        }
    
        public PeriodSettings GetCurrentPeriodPlan()
        {
            var todayPeriods = mainCalendarDay?.periodSettings;
            if (todayPeriods != null && todayPeriods.Count > currentPeriodIndex && currentPeriodIndex >= 0)
                return todayPeriods[currentPeriodIndex];
            return null;
        }

        public PeriodSettings GetPreviousPeriodPlan()
        {
            return previousPeriodPlan;
        }
    
        public float GetPeriodTimer()
        {
            return periodTimer;
        }

        public int GetCurrentDay()
        {
            return dayCounter;
        }

        public void SetDay(int day)
        {
            dayCounter = day;
        }

        public void ResetState()
        {
            dayCounter = 0;
            if (ClientQueueManager.Instance != null)
                ClientQueueManager.Instance.ResetQueueNumber();
        }

        #endregion

        #region Static Helpers (Zones & Staff)
    
        public static IServiceProvider GetServiceProviderAtDesk(int deskId)
        {
            if (serviceProviderAssignments.TryGetValue(deskId, out IServiceProvider provider))
                return provider;
            return null;
        }
    
        public static void AssignServiceProviderToDesk(IServiceProvider provider, int deskId)
        {
            serviceProviderAssignments[deskId] = provider;
        }
    
        public static void UnassignServiceProviderFromDesk(int deskId)
        {
            if (serviceProviderAssignments.ContainsKey(deskId))
            {
                serviceProviderAssignments.Remove(deskId);
            }
        }

        public static LimitedCapacityZone GetRegistrationZone() => Instance.registrationZone;
        public static LimitedCapacityZone GetToiletZone() => Instance.toiletZone;
        public static LimitedCapacityZone GetDesk1Zone() => Instance.category1DeskZones.FirstOrDefault();
        public static LimitedCapacityZone GetDesk2Zone() => Instance.category2DeskZones.FirstOrDefault();
        public static LimitedCapacityZone GetCashierZone() => Instance.cashierZones.FirstOrDefault();

        public static LimitedCapacityZone GetZoneByDeskId(int deskId)
        {
            if (Instance == null || ScenePointsRegistry.Instance == null || ScenePointsRegistry.Instance.allServicePoints == null)
                return null;

            ServicePoint targetPoint = ScenePointsRegistry.Instance.GetServicePointByID(deskId);
            if (targetPoint == null) return null;

            return targetPoint.GetComponentInParent<LimitedCapacityZone>();
        }
    
        public static LimitedCapacityZone GetQuietestZone(List<LimitedCapacityZone> zones)
        {
            if (zones == null || zones.Count == 0) return null;
            return zones.Where(z => z != null).OrderBy(z => z.waitingQueue.Count).FirstOrDefault();
        }

        private void UpdateStaffShifts(CalendarDayPeriodType periodType)
		{
            var allStaffOnScene = HiringManager.Instance.AllStaff;
            foreach (var staffMember in allStaffOnScene)
            {
                if (staffMember == null) continue;
                var shouldWork = staffMember.WorkShiftMask.HasFlag(periodType);
                if (shouldWork && !staffMember.IsOnDuty()) staffMember.StartShift();
                else if (!shouldWork && staffMember.IsOnDuty()) staffMember.EndShift();
            }
		}

        private void PlanDirectorClientSpawns()
        {
            directorClientSpawnPeriods.Clear();
            var todayPeriods = mainCalendarDay?.periodSettings;
            if (todayPeriods == null) return;

            var validPeriodIndexes = new List<int>();
            for (int i = 0; i < todayPeriods.Count; i++)
            {
                var periodType = todayPeriods[i].PeriodType;
                if (periodType != CalendarDayPeriodType.Evening && periodType != CalendarDayPeriodType.Night)
                    validPeriodIndexes.Add(i);
            }

            if (validPeriodIndexes.Count == 0) return;

            for (int i = 0; i < directorClientsPerDay; i++)
            {
                int randomPeriodIndex = validPeriodIndexes[UnityEngine.Random.Range(0, validPeriodIndexes.Count)];
                directorClientSpawnPeriods.Add(randomPeriodIndex);
            }
        }

        private void EvacuateAllClients(bool force = false)
        {
            ClientPathfinding[] allClients = FindObjectsByType<ClientPathfinding>(FindObjectsSortMode.None);
            foreach (var client in allClients)
            {
                if (client != null)
                {
                    var reason = force ? ClientPathfinding.LeaveReason.Angry : ClientPathfinding.LeaveReason.CalmedDown;
                    client.ForceLeave(reason);
                }
            }
        }
        #endregion
    }
}