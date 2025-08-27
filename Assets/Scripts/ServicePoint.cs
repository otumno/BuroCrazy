using UnityEngine;

public class ServicePoint : MonoBehaviour
{
    [Tooltip("ID этой стойки (1 для Стойки 1, 2 для Стойки 2 и т.д.)")]
    public int deskId;

    [Tooltip("Точка, где должен стоять клерк")]
    public Transform clerkStandPoint;
    
    [Tooltip("Точка, где должен стоять клиент (это и есть insideWaypoint для зоны)")]
    public Waypoint clientStandPoint;

    [Tooltip("Точка на столе, куда кладется документ")]
    public Transform documentPointOnDesk;
}