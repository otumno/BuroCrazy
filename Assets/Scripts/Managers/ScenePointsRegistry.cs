// Файл: Assets/Scripts/Managers/ScenePointsRegistry.cs
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class ScenePointsRegistry : MonoBehaviour
{
    public static ScenePointsRegistry Instance { get; private set; }

    [Header("Общие точки для персонала")]
    public RectZone staffHomeZone;
    public Transform staffToiletPoint;
    public List<Transform> kitchenPoints;

    [Header("Патрульные маршруты")]
    public List<Transform> internPatrolPoints;
    public List<Transform> guardPatrolPoints;
    public List<Transform> janitorPatrolPoints;

    [Header("Рабочие места и зоны")]
    public List<ServicePoint> allServicePoints;
    public Transform guardPostPoint;
    public ServicePoint guardReportDesk;

    // --- ДОБАВЛЕНО: Ссылка на уникальный объект сцены ---
    [Header("Уникальные интерактивные объекты")]
    public SecurityBarrier securityBarrier;

    private List<Transform> occupiedKitchenPoints = new List<Transform>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); }
        else { Instance = this; }
    }

    public ServicePoint GetServicePointByID(int id)
    {
        return allServicePoints.FirstOrDefault(p => p.deskId == id);
    }

    public Transform RequestKitchenPoint()
    {
        if (kitchenPoints == null || kitchenPoints.Count == 0) return null;
        Transform freePoint = kitchenPoints.FirstOrDefault(p => !occupiedKitchenPoints.Contains(p));
        if (freePoint != null)
        {
            occupiedKitchenPoints.Add(freePoint);
            return freePoint;
        }
        return kitchenPoints.FirstOrDefault();
    }

    public void FreeKitchenPoint(Transform point)
    {
        if (point != null && occupiedKitchenPoints.Contains(point))
        {
            occupiedKitchenPoints.Remove(point);
        }
    }
}