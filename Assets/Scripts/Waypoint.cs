using UnityEngine;
using System.Collections.Generic;

public class Waypoint : MonoBehaviour
{
    public enum WaypointType { General, StaffOnly }

    [Tooltip("Тип точки: General - для всех, StaffOnly - только для персонала")]
    public WaypointType type = WaypointType.General;

    // --- НОВОЕ ПОЛЕ ---
    [Tooltip("Список тегов, которым ЗАПРЕЩЕНО использовать этот вейпоинт")]
    public List<string> forbiddenTags;

    public List<Waypoint> neighbors;
}