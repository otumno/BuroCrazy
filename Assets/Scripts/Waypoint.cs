using UnityEngine;
using System.Collections.Generic;

public class Waypoint : MonoBehaviour
{
    // Перечисление для типов точек
    public enum WaypointType { General, StaffOnly }

    [Tooltip("Тип точки: General - для всех, StaffOnly - только для персонала")]
    public WaypointType type = WaypointType.General;

    public List<Waypoint> neighbors;
}