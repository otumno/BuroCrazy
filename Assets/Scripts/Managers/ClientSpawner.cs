using System.Collections.Generic;
using System.Linq;
using Data.Calendar;
using UnityEngine;

namespace Managers
{
    // ТЕПЕРЬ ЭТО ПРОСТО ХРАНИЛИЩЕ ДАННЫХ И ССЫЛОК (LEGACY)
    public class ClientSpawner : MonoBehaviour
    {
        public static ClientSpawner Instance { get; private set; }

        [Header("Ссылки на зоны")]
        public LimitedCapacityZone registrationZone;
        public List<LimitedCapacityZone> category1DeskZones;
        public List<LimitedCapacityZone> category2DeskZones;
        public List<LimitedCapacityZone> cashierZones;
        public LimitedCapacityZone toiletZone;
        public LimitedCapacityZone directorReceptionZone;
        public Waypoint exitWaypoint;
        public FormTable formTable;

        // тут можно просто юзать метод расширения IsNight из CalendarDayPeriodTypeExtensions
        public List<CalendarDayPeriodType> NightPeriodTypes = new()
        {
            CalendarDayPeriodType.StartNight,
            CalendarDayPeriodType.EndNight
        };

        // Прокси для получения текущего периода
        public static CalendarDayPeriodType CurrentPeriodType 
        {
            get 
            {
                if (TimeManager.Instance != null) return TimeManager.Instance.GetCurrentPeriodType();
                return CalendarDayPeriodType.None;
            }
        }

        private void Awake()
        {
            Instance = this;
        }

        // --- СТАТИЧЕСКИЕ ХЕЛПЕРЫ (Остаются без изменений) ---
        public static LimitedCapacityZone GetZoneByDeskId(int deskId)
        {
            if (Instance == null || ScenePointsRegistry.Instance == null) return null;
            var point = ScenePointsRegistry.Instance.GetServicePointByID(deskId);
            return point?.GetComponentInParent<LimitedCapacityZone>();
        }

        private static Dictionary<int, IServiceProvider> serviceProviderAssignments = new Dictionary<int, IServiceProvider>();

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
                serviceProviderAssignments.Remove(deskId);
        }

        public static LimitedCapacityZone GetRegistrationZone() => Instance?.registrationZone;
        public static LimitedCapacityZone GetToiletZone() => Instance?.toiletZone;
        public static LimitedCapacityZone GetDesk1Zone() => Instance?.category1DeskZones?.FirstOrDefault();
        public static LimitedCapacityZone GetDesk2Zone() => Instance?.category2DeskZones?.FirstOrDefault();
        public static LimitedCapacityZone GetCashierZone() => Instance?.cashierZones?.FirstOrDefault();
        
        public static LimitedCapacityZone GetQuietestZone(List<LimitedCapacityZone> zones)
        {
            if (zones == null || zones.Count == 0) return null;
            return zones.OrderBy(z => z.waitingQueue.Count).FirstOrDefault();
        }

        // Прокси
        public int GetCurrentDay() => TimeManager.Instance != null ? TimeManager.Instance.GetCurrentDay() : 1;
    }
}