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

        // deskId identifies the service, not the physical window. Both registration desks use zero.
        private static readonly Dictionary<ServicePoint, IServiceProvider> providers = new();

        public static IServiceProvider GetServiceProviderAtDesk(ServicePoint desk)
        {
            if (desk == null || !providers.TryGetValue(desk, out var provider)) return null;
            if (provider is Object obj && obj == null || provider.GetWorkstation() != desk)
            {
                providers.Remove(desk);
                return null;
            }
            return provider;
        }

        public static IEnumerable<IServiceProvider> GetServiceProvidersForDeskType(int deskId)
        {
            foreach (var pair in providers)
                if (pair.Key != null && pair.Key.deskId == deskId && !(pair.Value is Object obj && obj == null) &&
                    pair.Value.GetWorkstation() == pair.Key)
                    yield return pair.Value;
        }

        public static IServiceProvider GetServiceProviderAtDesk(int deskId) =>
            GetServiceProvidersForDeskType(deskId).FirstOrDefault();

        public static void ClearServiceProviders() => providers.Clear();

        public static void AssignServiceProviderToDesk(IServiceProvider provider, ServicePoint desk)
        {
            if (desk != null && provider != null) providers[desk] = provider;
        }

        public static void AssignServiceProviderToDesk(IServiceProvider provider, int deskId)
        {
            var desk = provider?.GetWorkstation();
            if (desk != null && desk.deskId == deskId) AssignServiceProviderToDesk(provider, desk);
        }

        public static void UnassignServiceProviderFromDesk(ServicePoint desk, IServiceProvider owner)
        {
            if (desk != null && providers.TryGetValue(desk, out var provider) && ReferenceEquals(provider, owner))
                providers.Remove(desk);
        }

        public static void UnassignServiceProviderFromDesk(int deskId)
        {
            var provider = GetServiceProviderAtDesk(deskId);
            if (provider != null) UnassignServiceProviderFromDesk(provider.GetWorkstation(), provider);
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
