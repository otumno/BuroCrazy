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
            if (kitchenPoints == null || kitchenPoints.Count == 0) return null;
            return kitchenPoints[Random.Range(0, kitchenPoints.Count)];
        }
        
        public void FreeKitchenPoint(Waypoint wp) { }

        public Waypoint staffToiletPoint
        {
            get
            {
                if (toiletPoints == null || toiletPoints.Count == 0) return null;
                return toiletPoints[Random.Range(0, toiletPoints.Count)];
            }
        }
    }
}