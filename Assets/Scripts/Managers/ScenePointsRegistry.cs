// Assets/Scripts/Managers/ScenePointsRegistry.cs
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Gameplay; // Для ServicePoint, NoticeBoard, EnvelopeStack

namespace Managers
{
    public class ScenePointsRegistry : MonoBehaviour
    {
        public static ScenePointsRegistry Instance { get; private set; }

        [Header("Основные зоны")]
        public RectZone staffHomeZone; // Исправлено: RectZone для рандомных точек
        public Waypoint janitorHomePoint; 

        [Header("Рабочие места (Service Points)")]
        public List<ServicePoint> allServicePoints = new List<ServicePoint>();
        
        [Header("Специальные столы")]
        public ServicePoint bookkeepingDesk; 
        public ServicePoint guardPostPoint; 
        public ServicePoint guardReportDesk;

        [Header("Интерактивные объекты")]
        public NoticeBoard noticeBoard;
        public EnvelopeStack salaryStackPoint; // Исправлено: EnvelopeStack для логики зарплат
        public Transform dumpsterPoint;      
        public SecurityBarrier securityBarrier;

        [Header("Точки нужд")]
        public List<Waypoint> kitchenPoints = new List<Waypoint>();
        public List<Waypoint> toiletPoints = new List<Waypoint>();
        public Transform waterCoolerPoint; // Точка кулера для стажёров

        [Header("Патруль")]
        public List<Waypoint> guardPatrolPoints = new List<Waypoint>();
        public List<Waypoint> janitorPatrolPoints = new List<Waypoint>();
        public List<Waypoint> internPatrolPoints = new List<Waypoint>();

        void Awake()
        {
            if (Instance != null) Destroy(gameObject);
            else Instance = this;
            
            if (allServicePoints == null || allServicePoints.Count == 0)
                allServicePoints = FindObjectsByType<ServicePoint>(FindObjectsSortMode.None).ToList();
        }

        // --- МЕТОДЫ ДОСТУПА ---

        public ServicePoint GetServicePointByID(int id)
        {
            if (allServicePoints == null) return null;
            return allServicePoints.FirstOrDefault(sp => sp.deskId == id);
        }

        public Waypoint RequestKitchenPoint()
        {
            if (kitchenPoints == null || kitchenPoints.Count == 0) 
            {
                Debug.LogWarning("[ScenePointsRegistry] kitchenPoints пуст или null");
                return null;
            }
            
            var validPoints = kitchenPoints.Where(p => p != null).ToList();
            if (validPoints.Count == 0)
            {
                Debug.LogWarning("[ScenePointsRegistry] Все kitchenPoints == null");
                return null;
            }
            
            return validPoints[Random.Range(0, validPoints.Count)];
        }
        
        public void FreeKitchenPoint(Waypoint wp) { }

        public Waypoint staffToiletPoint
        {
            get
            {
                if (toiletPoints == null || toiletPoints.Count == 0) return null;
                
                var validPoints = toiletPoints.Where(p => p != null).ToList();
                if (validPoints.Count == 0) return null;
                
                return validPoints[Random.Range(0, validPoints.Count)];
            }
        }

        [ContextMenu("ValidateAllPoints")]
        public void ValidateAllPoints()
        {
            Debug.Log("=== ScenePointsRegistry Validation ===");
            Debug.Log($"kitchenPoints: {CountValid(kitchenPoints)}/{kitchenPoints?.Count ?? 0}");
            Debug.Log($"toiletPoints: {CountValid(toiletPoints)}/{toiletPoints?.Count ?? 0}");
            Debug.Log($"internPatrolPoints: {CountValid(internPatrolPoints)}/{internPatrolPoints?.Count ?? 0}");
            Debug.Log($"guardPatrolPoints: {CountValid(guardPatrolPoints)}/{guardPatrolPoints?.Count ?? 0}");
            Debug.Log($"janitorPatrolPoints: {CountValid(janitorPatrolPoints)}/{janitorPatrolPoints?.Count ?? 0}");
            Debug.Log($"allServicePoints: {CountValidServicePoints(allServicePoints)}/{allServicePoints?.Count ?? 0}");
            Debug.Log("======================================");
        }

        private int CountValid(List<Waypoint> list)
        {
            if (list == null) return 0;
            return list.Count(p => p != null);
        }

        private int CountValidServicePoints(List<ServicePoint> list)
        {
            if (list == null) return 0;
            return list.Count(p => p != null);
        }

        void Start()
        {
            ValidateAllPoints();
        }
    }
}