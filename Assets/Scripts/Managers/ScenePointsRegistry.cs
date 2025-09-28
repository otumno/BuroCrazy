// Файл: ScenePointsRegistry.cs --- ФИНАЛЬНАЯ ВЕРСИЯ ---
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
    [Tooltip("Перетащите сюда ВСЕ объекты ServicePoint со сцены (Стойка 1, Стойка 2, Касса и т.д.)")]
    public List<ServicePoint> allServicePoints;
	[Tooltip("Точка, где охранник стоит 'на посту' по умолчанию")]
	public Transform guardPostPoint;
    [Tooltip("Точка, куда охранник ходит писать протокол")]
    public Transform guardReportDesk;
    
    // Внутренний список для управления занятыми точками на кухне
    private List<Transform> occupiedKitchenPoints = new List<Transform>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); }
        else { Instance = this; }
    }

    // --- Публичные методы для доступа к точкам ---

    /// <summary>
    /// Находит и возвращает ServicePoint по его уникальному ID.
    /// </summary>
    public ServicePoint GetServicePointByID(int id)
    {
        return allServicePoints.FirstOrDefault(p => p.deskId == id);
    }

    /// <summary>
    /// Запрашивает свободную точку на кухне и помечает ее как занятую.
    /// </summary>
    public Transform RequestKitchenPoint()
    {
        if (kitchenPoints == null || kitchenPoints.Count == 0) return null;
        Transform freePoint = kitchenPoints.FirstOrDefault(p => !occupiedKitchenPoints.Contains(p));
        if (freePoint != null)
        {
            occupiedKitchenPoints.Add(freePoint);
            return freePoint;
        }
        return kitchenPoints.FirstOrDefault(); // Если все заняты, вернуть хотя бы любую
    }

    /// <summary>
    /// Освобождает точку на кухне.
    /// </summary>
    public void FreeKitchenPoint(Transform point)
    {
        if (point != null && occupiedKitchenPoints.Contains(point))
        {
            occupiedKitchenPoints.Remove(point);
        }
    }
}